using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Data;
using OpenQMS.Localization;
using OpenQMS.Models;
using OpenQMS.Models.Navigation;
using OpenQMS.Models.Release;
using OpenQMS.ViewModels.Release;
using OpenQMS.ViewModels.Trainings;

namespace OpenQMS.Services
{
    /// <summary>
    /// The documents MintQMS issues, and when each one is fixed:
    ///   * release certificate        — when the certificate is issued;
    ///   * training certificate       — when that trainee's training is completed (one per trainee row);
    ///   * training attendance list   — when the training is completed.
    /// Each is written in the site language at that moment and stored by <see cref="IssuedRecordService"/>.
    /// Documents that were issued before this installation kept copies are fixed once, in English, the way
    /// they read then ("frozen on upgrade") — see <see cref="BackfillAsync"/>.
    /// </summary>
    public class IssuedDocumentsService
    {
        public const string ReleaseCertificateView  = "~/Views/Release/_CertificateDocument.cshtml";
        public const string TrainingCertificateView = "~/Views/Trainings/_TrainingCertificateDocument.cshtml";
        public const string AttendanceListView      = "~/Views/Trainings/_AttendanceListDocument.cshtml";

        private readonly ApplicationDbContext _db;
        private readonly IssuedRecordService _issued;
        private readonly ILogger<IssuedDocumentsService> _log;

        public IssuedDocumentsService(ApplicationDbContext db, IssuedRecordService issued, ILogger<IssuedDocumentsService> log)
        {
            _db = db; _issued = issued; _log = log;
        }

        public IssuedRecordService Copies => _issued;

        // ── Which language / origin ─────────────────────────────────────────────────────────────

        private async Task<DateTime?> CopiesSinceAsync() =>
            await _db.SystemSettings.AsNoTracking().Where(s => s.Id == 1)
                .Select(s => s.IssuedCopiesSince).FirstOrDefaultAsync();

        /// <summary>Language and origin for a document issued at <paramref name="issuedAt"/> (UTC):
        /// before copies were kept → English, "frozen on upgrade"; otherwise the site language.</summary>
        private async Task<(string Language, string Origin)> LanguageForAsync(DateTime? issuedAt)
        {
            // No start marker yet = no document predates stored copies (fresh database).
            var since = await CopiesSinceAsync();
            if (since != null && (!issuedAt.HasValue || issuedAt.Value < since.Value))
                return (Languages.Default, IssuedRecordOrigins.FrozenOnUpgrade);
            return (await _issued.SiteLanguageAsync(), IssuedRecordOrigins.Issued);
        }

        // ── Release certificate ─────────────────────────────────────────────────────────────────

        private async Task<CertificateDocumentModel?> ReleaseModelAsync(ReleaseCertificate cert)
        {
            if (string.IsNullOrEmpty(cert.ContentSnapshot)) return null;
            var snap = JsonSerializer.Deserialize<CertificateSnapshot>(cert.ContentSnapshot);
            if (snap == null) return null;
            return new CertificateDocumentModel
            {
                Snapshot = snap,
                CertificateType = cert.CertificateType,
                APFullNameQualification = cert.APFullNameQualification,
                Org = await _issued.OrgHeaderAsync(),
            };
        }

        /// <summary>Issues the stored copy of a NEW certificate (cert must already have its Id; the caller saves).</summary>
        public async Task IssueReleaseCertificateAsync(ReleaseCertificate cert, string? by, HttpContext? http)
        {
            var model = await ReleaseModelAsync(cert)
                ?? throw new InvalidOperationException($"Certificate {cert.CertificateNumber} has no content snapshot.");
            await _issued.IssueAsync(IssuedRecordKinds.ReleaseCertificate, cert.Id, ReleaseCertificateView, model,
                await _issued.SiteLanguageAsync(), IssuedRecordOrigins.Issued, by, http);
        }

        /// <summary>The stored copy of a certificate; one issued before copies were kept is fixed now (saved).</summary>
        public async Task<IssuedCopyResult> ReleaseCertificateAsync(ReleaseCertificate cert, HttpContext? http)
        {
            var r = await _issued.GetActiveAsync(IssuedRecordKinds.ReleaseCertificate, cert.Id);
            if (r.Copy != null) return r;
            var model = await ReleaseModelAsync(cert);
            if (model == null) return r;
            var (lang, origin) = await LanguageForAsync(cert.IssuedDate);
            await _issued.IssueAsync(IssuedRecordKinds.ReleaseCertificate, cert.Id, ReleaseCertificateView, model,
                lang, origin, "system", http);
            await _db.SaveChangesAsync();
            return await _issued.GetActiveAsync(IssuedRecordKinds.ReleaseCertificate, cert.Id);
        }

        // ── Training ────────────────────────────────────────────────────────────────────────────

        private static DateTime? CompletedAt(UserTraining ut) =>
            ut.CompetencyVerifiedOn ?? ut.ReadAcknowledgedOn;

        private async Task<AppDocument?> PolicyDocAsync(Training t) =>
            t.PolicyId > 0 ? await _db.AppDocument.AsNoTracking().FirstOrDefaultAsync(d => d.Id == t.PolicyId) : null;

        private async Task<TrainingCertificateDocumentModel> TrainingCertModelAsync(Training t, UserTraining ut)
        {
            var trainee = ut.Trainee ?? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == ut.TraineeId);
            return new TrainingCertificateDocumentModel
            {
                Training = t, Row = ut, Trainee = trainee, PolicyDocument = await PolicyDocAsync(t),
                IssuedOn = DateTime.UtcNow, Org = await _issued.OrgHeaderAsync(),
            };
        }

        private async Task<AttendanceListDocumentModel> AttendanceModelAsync(Training t, List<UserTraining> rows)
        {
            var completedBy = t.CompletedBy.HasValue
                ? await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == t.CompletedBy.Value)
                : null;
            return new AttendanceListDocumentModel
            {
                Training = t, Rows = rows, CompletedByUser = completedBy, PolicyDocument = await PolicyDocAsync(t),
                IssuedOn = DateTime.UtcNow, Org = await _issued.OrgHeaderAsync(),
            };
        }

        /// <summary>
        /// Brings a training's stored copies in line with its state, using the TRACKED (possibly unsaved)
        /// entities so it can run in the same save as the completion: a completed trainee row gets its
        /// certificate, a completed training gets its attendance list; a row or training that is no longer
        /// completed has its copy superseded. Call before SaveChanges. Never throws for a rendering problem
        /// (logged; the copy is then made on the next view instead).
        /// </summary>
        public async Task SyncTrainingAsync(int trainingId, string? by, HttpContext? http, string? supersedeReason = null)
        {
            try
            {
                var training = _db.Training.Local.FirstOrDefault(x => x.Id == trainingId)
                               ?? await _db.Training.FirstOrDefaultAsync(x => x.Id == trainingId);
                if (training == null) return;

                // Loads every row of this training; rows already tracked keep their in-memory (unsaved) values.
                var rows = await _db.UserTraining.Include(ut => ut.Trainee)
                    .Where(ut => ut.TrainingId == trainingId).ToListAsync();
                rows = rows.Concat(_db.UserTraining.Local.Where(ut => ut.TrainingId == trainingId))
                           .Distinct().OrderBy(ut => ut.Id).ToList();

                foreach (var ut in rows.Where(r => r.Id > 0))
                {
                    var has = await _issued.HasActiveAsync(IssuedRecordKinds.TrainingCertificate, ut.Id);
                    if (ut.Status == UserTrainingStatus.Completed && !has)
                    {
                        var (lang, origin) = await LanguageForAsync(CompletedAt(ut));
                        await _issued.IssueAsync(IssuedRecordKinds.TrainingCertificate, ut.Id, TrainingCertificateView,
                            await TrainingCertModelAsync(training, ut), lang, origin, by, http);
                    }
                    else if (ut.Status != UserTrainingStatus.Completed && has)
                    {
                        await _issued.SupersedeAsync(IssuedRecordKinds.TrainingCertificate, ut.Id,
                            supersedeReason ?? $"Trainee record is now {ut.Status}");
                    }
                }

                var hasList = await _issued.HasActiveAsync(IssuedRecordKinds.AttendanceList, training.Id);
                if (training.Status == Training.TrainingStatus.Completed && !hasList)
                {
                    var (lang, origin) = await LanguageForAsync(training.CompletedOn);
                    await _issued.IssueAsync(IssuedRecordKinds.AttendanceList, training.Id, AttendanceListView,
                        await AttendanceModelAsync(training, rows), lang, origin, by, http);
                }
                else if (training.Status != Training.TrainingStatus.Completed && hasList)
                {
                    await _issued.SupersedeAsync(IssuedRecordKinds.AttendanceList, training.Id,
                        supersedeReason ?? $"Training is now {training.Status}");
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Issued copies for training #{TrainingId} could not be brought up to date.", trainingId);
            }
        }

        /// <summary>The stored certificate of one trainee row (made now if missing and the row is completed).</summary>
        public async Task<IssuedCopyResult> TrainingCertificateAsync(UserTraining ut, HttpContext? http)
        {
            var r = await _issued.GetActiveAsync(IssuedRecordKinds.TrainingCertificate, ut.Id);
            if (r.Copy != null || ut.Status != UserTrainingStatus.Completed) return r;
            await SyncTrainingAsync(ut.TrainingId, "system", http);
            await _db.SaveChangesAsync();
            return await _issued.GetActiveAsync(IssuedRecordKinds.TrainingCertificate, ut.Id);
        }

        /// <summary>The stored attendance list of a completed training (made now if missing).</summary>
        public async Task<IssuedCopyResult> AttendanceListAsync(Training t, HttpContext? http)
        {
            var r = await _issued.GetActiveAsync(IssuedRecordKinds.AttendanceList, t.Id);
            if (r.Copy != null || t.Status != Training.TrainingStatus.Completed) return r;
            await SyncTrainingAsync(t.Id, "system", http);
            await _db.SaveChangesAsync();
            return await _issued.GetActiveAsync(IssuedRecordKinds.AttendanceList, t.Id);
        }

        /// <summary>Renders the LIVE (not yet issued) attendance list of a training that is still open.</summary>
        public async Task<string> RenderLiveAttendanceAsync(Training t, HttpContext http)
        {
            var rows = await _db.UserTraining.AsNoTracking().Include(ut => ut.Trainee)
                .Where(ut => ut.TrainingId == t.Id).OrderBy(ut => ut.Id).ToListAsync();
            var model = await AttendanceModelAsync(t, rows);
            model.IsLive = true;
            return await _issued.RenderAsync(AttendanceListView, model,
                System.Globalization.CultureInfo.CurrentUICulture.Name, http);
        }

        // ── Start-up back-fill ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Fixes every issued document that has no stored copy yet. The first run marks the moment copies
        /// started (SystemSettings.IssuedCopiesSince); documents issued before it are fixed in English as
        /// "frozen on upgrade". Safe to run on every start: it only touches documents without a copy, and a
        /// failure on one document is logged and retried on the next start (or when it is next opened).
        /// </summary>
        public async Task BackfillAsync()
        {
            // The start marker lives on the settings row, which MintQMS otherwise creates the first time the
            // Settings page is opened. Create it now with the same defaults (every reader of the settings
            // already falls back to these defaults when the row is missing, so nothing else changes).
            var settings = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Id == 1);
            if (settings == null)
            {
                settings = new SystemSettings { Id = 1 };
                _db.SystemSettings.Add(settings);
            }
            if (settings.IssuedCopiesSince == null)
            {
                settings.IssuedCopiesSince = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            int made = 0, failed = 0;

            var haveCert = await _db.IssuedRecordCopies.Where(c => c.Kind == IssuedRecordKinds.ReleaseCertificate)
                .Select(c => c.SourceId).Distinct().ToListAsync();
            var certs = await _db.ReleaseCertificates.AsNoTracking()
                .Where(c => c.ContentSnapshot != null && !haveCert.Contains(c.Id)).ToListAsync();
            foreach (var cert in certs)
            {
                try { await ReleaseCertificateAsync(cert, null); made++; }
                catch (Exception ex) { failed++; _db.ChangeTracker.Clear(); _log.LogError(ex, "Back-fill: certificate {No} not fixed.", cert.CertificateNumber); }
            }

            var trainingIds = await _db.Training
                .Where(t => t.Status == Training.TrainingStatus.Completed
                         || _db.UserTraining.Any(ut => ut.TrainingId == t.Id && ut.Status == UserTrainingStatus.Completed))
                .Select(t => t.Id).ToListAsync();
            foreach (var id in trainingIds)
            {
                try
                {
                    await SyncTrainingAsync(id, "system", null);
                    made += _db.ChangeTracker.Entries<IssuedRecordCopy>().Count(e => e.State == EntityState.Added);
                    await _db.SaveChangesAsync();
                    _db.ChangeTracker.Clear();
                }
                catch (Exception ex) { failed++; _db.ChangeTracker.Clear(); _log.LogError(ex, "Back-fill: training #{Id} not fixed.", id); }
            }

            if (made > 0 || failed > 0)
                _log.LogInformation("Issued documents back-fill: {Made} fixed, {Failed} failed.", made, failed);
        }
    }
}
