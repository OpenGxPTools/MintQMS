/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2024  C-realize IT Services SRL (https://www.c-realize.com)

This program is offered under a commercial and under the AGPL license.
For commercial licensing, contact us at https://www.c-realize.com/contact.  For AGPL licensing, see below.

AGPL:
This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as published by
the Free Software Foundation, either version 3 of the License, or
(at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see http://www.gnu.org/licenses/.
*/

using System.Data;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.Navigation;
using OpenQMS.Models.ViewModels;
using OpenQMS.Services;

namespace OpenQMS.Controllers
{
    [Authorize]
    public class TrainingsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<AppRole> _roleManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly DocumentWorkflowService _docWorkflow;

        public TrainingsController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            RoleManager<AppRole> roleManager,
            IPasswordHasher<AppUser> passwordHasher,
            DocumentWorkflowService docWorkflow)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _passwordHasher = passwordHasher;
            _docWorkflow = docWorkflow;
        }

        // GET: Trainings
        public async Task<IActionResult> Index()
        {
            var trainings = await _context.Training
                .Where(t => !t.IsDeleted)
                .Include(t => t.Trainees)
                .ToListAsync();

            var isAuthorized = User.IsInRole(Constants.AdministratorsRole) ||
                               User.IsInRole(Constants.QualityManagerRole) ||
                               User.IsInRole(Constants.RegulatoryManagerRole) ||
                               User.IsInRole(Constants.TrainerRole) ||
                               User.IsInRole(Constants.AuthorisedPersonRole) ||
                               User.IsInRole(Constants.AuditorRole);
            if (!isAuthorized)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                var userTrainingIds = await _context.UserTraining
                    .Where(x => x.TraineeId == currentUser!.Id)
                    .Select(x => x.TrainingId)
                    .ToListAsync();
                trainings = trainings.Where(t => userTrainingIds.Contains(t.Id)).ToList();
            }

            var policyIds = trainings.Where(t => t.PolicyId > 0).Select(t => t.PolicyId).Distinct().ToList();
            ViewBag.PolicyDocs = await _context.AppDocument
                .Where(d => policyIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d);

            return View(trainings);
        }

        // GET: Trainings/Dashboard
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson,Auditor")]
        public async Task<IActionResult> Dashboard()
        {
            var trainings = await _context.Training
                .Where(t => !t.IsDeleted)
                .Include(t => t.Trainees)
                .ToListAsync();

            ViewBag.TotalTrainings  = trainings.Count;
            ViewBag.ScheduledCount  = trainings.Count(t => t.Status == Training.TrainingStatus.Scheduled);
            ViewBag.CompletedCount  = trainings.Count(t => t.Status == Training.TrainingStatus.Completed);
            ViewBag.OverdueCount    = trainings.Count(t => t.Status == Training.TrainingStatus.Scheduled && t.DueDate.HasValue && t.DueDate.Value.Date < SiteTime.Today);

            ViewBag.OverdueTrainings    = trainings.Where(t => t.Status == Training.TrainingStatus.Scheduled && t.DueDate.HasValue && t.DueDate.Value.Date < SiteTime.Today)
                                                   .OrderBy(t => t.DueDate).ToList();
            ViewBag.UpcomingTrainings   = trainings.Where(t => t.Status == Training.TrainingStatus.Scheduled && (!t.DueDate.HasValue || t.DueDate.Value.Date >= SiteTime.Today))
                                                   .OrderBy(t => t.DueDate).Take(10).ToList();
            ViewBag.RecentlyCompleted   = trainings.Where(t => t.Status == Training.TrainingStatus.Completed)
                                                   .OrderByDescending(t => t.CompletedOn).Take(10).ToList();

            return View();
        }

        // GET: Trainings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var currentTraining = await _context.Training
                .Include(x => x.CompletedByUser)
                .Include(s => s.Trainees).ThenInclude(t => t.Trainee)
                .Include(s => s.Trainees).ThenInclude(t => t.Attachments)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (currentTraining == null) return NotFound();

            var isAuthorized = User.IsInRole(Constants.AdministratorsRole) ||
                               User.IsInRole(Constants.QualityManagerRole) ||
                               User.IsInRole(Constants.RegulatoryManagerRole) ||
                               User.IsInRole(Constants.TrainerRole) ||
                               User.IsInRole(Constants.AuthorisedPersonRole) ||
                               User.IsInRole(Constants.AuditorRole);
            if (!isAuthorized)
            {
                bool userIsTrainee = currentTraining.Trainees
                    .Any(t => t.Trainee.UserName == _userManager.GetUserName(User));
                if (!userIsTrainee) return Forbid();
            }

            ViewBag.AuditLogs = await _context.MasterDataAuditLog
                .Where(l => l.EntityType == "Training" && l.ParentId == id)
                .OrderByDescending(l => l.PerformedOn)
                .ToListAsync();

            ViewBag.PolicyDocument = currentTraining.PolicyId > 0
                ? await _context.AppDocument.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == currentTraining.PolicyId)
                : null;

            return View(currentTraining);
        }

        // GET: Trainings/Create
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> Create()
        {
            ViewData["Policies"] = await _context.AppDocument
                .Where(d => d.Status == AppDocument.DocumentStatus.Approved
                         || d.Status == AppDocument.DocumentStatus.Effective)
                .ToListAsync();
            var trainers = (await _userManager.GetUsersInRoleAsync(Constants.TrainerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.QualityManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.RegulatoryManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.AdministratorsRole))
                .DistinctBy(u => u.Id).ToList();
            ViewData["Trainer"] = trainers;
            ViewData["Roles"]   = _roleManager.Roles.ToList();
            return View();
        }

        // POST: Trainings/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> Create([Bind("Id,TrainingId,Name,Description,DueDate,PolicyId,PolicyTitle,TrainerId,TrainerEmail,TrainingType")] Training training, string roles)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    // 4-digit numeric ID generation
                    var allIds = await _context.Training.Select(t => t.TrainingId).ToListAsync();
                    int maxNum = allIds
                        .Select(s => { var p = s?.Split('-'); return p?.Length == 2 && int.TryParse(p[1], out var n) ? n : 0; })
                        .DefaultIfEmpty(0).Max();
                    int nextNum = maxNum + 1;

                    var policy  = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == training.PolicyId);
                    if (policy == null) return NotFound();

                    var trainer = string.IsNullOrEmpty(training.TrainerId)
                        ? null
                        : await _userManager.FindByIdAsync(training.TrainerId);
                    if (trainer == null && training.TrainingType == TrainingType.CompetencyAssessment)
                    {
                        ModelState.AddModelError("TrainerId", "A trainer is required for Competency Assessment.");
                        goto returnView;
                    }

                    training.TrainingId   = $"TRN-{nextNum.ToString().PadLeft(4, '0')}";
                    training.CreatedOn    = DateTime.UtcNow;
                    training.Status       = Training.TrainingStatus.Scheduled;
                    training.PolicyTitle  = policy.Title;
                    training.TrainerEmail = trainer?.Email ?? string.Empty;
                    training.Trainees     = new List<UserTraining>();

                    var usersInRole = await _userManager.GetUsersInRoleAsync(roles);
                    foreach (var user in usersInRole)
                    {
                        training.Trainees.Add(new UserTraining
                        {
                            TraineeId  = user.Id,
                            TrainingId = training.Id,
                        });
                    }

                    _context.Add(training);
                    await _context.SaveChangesAsync();
                    AddAuditLog(training.Id, "Created", null, "Scheduled",
                        $"ID: {training.TrainingId}; Name: {training.Name}; Policy: {training.PolicyTitle}; Trainer: {training.TrainerEmail}");
                    await _context.SaveChangesAsync();
                    TempData["Success"] = $"Training {training.TrainingId} created successfully.";
                    return RedirectToAction(nameof(Details), new { id = training.Id });
                }
            }
            catch (DataException)
            {
                ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists see your system administrator.");
            }

            returnView:
            ViewData["Policies"] = await _context.AppDocument.Where(d => d.Status == AppDocument.DocumentStatus.Approved).ToListAsync();
            var trainers = (await _userManager.GetUsersInRoleAsync(Constants.TrainerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.QualityManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.RegulatoryManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.AdministratorsRole))
                .DistinctBy(u => u.Id).ToList();
            ViewData["Trainer"]  = trainers;
            ViewData["Roles"]    = _roleManager.Roles.ToList();
            return View(training);
        }

        // GET: Trainings/Edit/5
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var training = await _context.Training
                .Include(t => t.Trainees).ThenInclude(t => t.Trainee)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);

            if (training == null) return NotFound();
            if (training.Status == Training.TrainingStatus.Completed) return Forbid();

            ViewData["Policies"] = _context.AppDocument.ToList();
            var trainersEdit = (await _userManager.GetUsersInRoleAsync(Constants.TrainerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.QualityManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.RegulatoryManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.AdministratorsRole))
                .DistinctBy(u => u.Id).ToList();
            ViewData["Trainer"]  = trainersEdit;
            PopulateAssignedTrainees(training);
            return View(training);
        }

        private void PopulateAssignedTrainees(Training training)
        {
            var allUsers       = _userManager.Users;
            var assignedSet    = new HashSet<int>(training.Trainees.Select(t => t.TraineeId));
            var viewModel      = new List<AssignedTrainees>();
            foreach (var user in allUsers)
            {
                viewModel.Add(new AssignedTrainees
                {
                    UserID   = user.Id,
                    Email    = user.Email,
                    Assigned = assignedSet.Contains(user.Id)
                });
            }
            ViewData["Trainees"] = viewModel;
        }

        // POST: Trainings/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> Edit(int? id, string[] selectedTrainees)
        {
            if (id == 0) return NotFound();

            var trainingToUpdate = await _context.Training
                .Include(t => t.Trainees).ThenInclude(t => t.Trainee)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (trainingToUpdate == null) return NotFound();

            if (trainingToUpdate.Status == Training.TrainingStatus.Completed) return Forbid();

            var snap = await _context.Training.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);

            if (await TryUpdateModelAsync<Training>(trainingToUpdate, "",
                t => t.Name, t => t.Description, t => t.DueDate,
                t => t.PolicyId, t => t.PolicyTitle, t => t.TrainerId, t => t.TrainerEmail))
            {
                UpdateAssignedTrainees(selectedTrainees, trainingToUpdate);
                try
                {
                    var policy  = await _context.AppDocument.FirstOrDefaultAsync(m => m.Id == trainingToUpdate.PolicyId);
                    var trainer = await _userManager.FindByIdAsync(trainingToUpdate.TrainerId);
                    if (policy == null) return NotFound();
                    trainingToUpdate.PolicyTitle  = policy.Title;
                    trainingToUpdate.TrainerEmail = trainer!.Email!;

                    var diff = new List<string>();
                    if (snap != null)
                    {
                        AuditDiffHelper.Track(diff,     "Name",        snap.Name,         trainingToUpdate.Name);
                        AuditDiffHelper.Track(diff,     "Description", snap.Description,  trainingToUpdate.Description);
                        AuditDiffHelper.TrackDate(diff, "Due Date",    snap.DueDate,      trainingToUpdate.DueDate);
                        AuditDiffHelper.Track(diff,     "Document",    snap.PolicyTitle,  trainingToUpdate.PolicyTitle);
                        AuditDiffHelper.Track(diff,     "Trainer",     snap.TrainerEmail, trainingToUpdate.TrainerEmail);
                    }
                    if (diff.Count > 0) AddAuditLog(trainingToUpdate.Id, "Edited", null, null, AuditDiffHelper.Build(diff));
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Training updated successfully.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                catch (DbUpdateException)
                {
                    ModelState.AddModelError("", "Unable to save changes. Try again, and if the problem persists, see your system administrator.");
                }
            }

            PopulateAssignedTrainees(trainingToUpdate);
            ViewData["Policies"] = _context.AppDocument.ToList();
            var trainersEditPost = (await _userManager.GetUsersInRoleAsync(Constants.TrainerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.QualityManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.RegulatoryManagerRole))
                .Union(await _userManager.GetUsersInRoleAsync(Constants.AdministratorsRole))
                .DistinctBy(u => u.Id).ToList();
            ViewData["Trainer"]  = trainersEditPost;
            return View(trainingToUpdate);
        }

        private void UpdateAssignedTrainees(string[] selectedTrainees, Training trainingToUpdate)
        {
            if (selectedTrainees == null)
            {
                trainingToUpdate.Trainees = new List<UserTraining>();
                return;
            }

            var selectedSet  = new HashSet<string>(selectedTrainees);
            var assignedSet  = new HashSet<int>(trainingToUpdate.Trainees.Select(t => t.TraineeId));
            foreach (var user in _userManager.Users)
            {
                if (selectedSet.Contains(user.Id.ToString()))
                {
                    if (!assignedSet.Contains(user.Id))
                        trainingToUpdate.Trainees.Add(new UserTraining { TrainingId = trainingToUpdate.Id, TraineeId = user.Id });
                }
                else
                {
                    if (assignedSet.Contains(user.Id))
                    {
                        var toRemove = trainingToUpdate.Trainees.FirstOrDefault(t => t.TraineeId == user.Id);
                        if (toRemove != null) trainingToUpdate.Trainees.Remove(toRemove);
                    }
                }
            }
        }

        // GET: Trainings/Delete/5
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var training = await _context.Training
                .Include(s => s.Trainees).ThenInclude(t => t.Trainee)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (training == null) return NotFound();
            if (training.Status == Training.TrainingStatus.Completed) return Forbid();

            return View(training);
        }

        // POST: Trainings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var training = await _context.Training.FindAsync(id);
            if (training == null) return NotFound();

            if (training.Status == Training.TrainingStatus.Completed) return Forbid();

            AddAuditLog(training.Id, "Deleted", training.Status.ToString(), null, training.Name);
            training.IsDeleted = true;
            training.DeletedBy = _userManager.GetUserName(User);
            training.DeletedOn = DateTime.UtcNow;
            _context.Training.Update(training);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // POST: Trainings/Complete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> Complete(int id, string InputEmail, string InputPassword, string? CompletionNotes)
        {
            var training = await _context.Training.FirstOrDefaultAsync(x => x.Id == id);
            if (training == null) return NotFound();

            if (training.Status != Training.TrainingStatus.Scheduled)
            {
                TempData["WorkflowError"] = "Training is not in Scheduled status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(CompletionNotes))
            {
                TempData["WorkflowError"] = "Competency Verification Notes are required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // 21 CFR Part 11 §11.100(a): signature must be from the currently logged-in user
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, InputEmail, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (_passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash ?? string.Empty, InputPassword) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Email or password is incorrect.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Enforce that all trainees have completed required steps before closing out
            var utRows = await _context.UserTraining
                .Include(ut => ut.Trainee)
                .Where(ut => ut.TrainingId == id)
                .OrderByDescending(ut => ut.Id)
                .ToListAsync();

            var latestPerTrainee = utRows
                .GroupBy(ut => ut.TraineeId)
                .Select(g => g.First())
                .ToList();

            if (latestPerTrainee.Any())
            {
                var notAcknowledged = latestPerTrainee
                    .Where(ut => ut.ReadAcknowledgedOn == null)
                    .Select(ut => ut.Trainee?.UserName ?? "unknown")
                    .ToList();

                if (notAcknowledged.Any())
                {
                    TempData["WorkflowError"] = $"Cannot complete: the following trainee(s) have not yet acknowledged reading the document: {string.Join(", ", notAcknowledged)}.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                var notCompleted = latestPerTrainee
                    .Where(ut => ut.Status != UserTrainingStatus.Completed)
                    .Select(ut => ut.Trainee?.UserName ?? "unknown")
                    .ToList();

                if (notCompleted.Any())
                {
                    var reason = training.TrainingType == TrainingType.CompetencyAssessment
                        ? "competency assessment has not been trainer-verified as passed"
                        : "acknowledgement is not yet fully recorded";
                    TempData["WorkflowError"] = $"Cannot complete: the following trainee(s) have outstanding requirements ({reason}): {string.Join(", ", notCompleted)}.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            training.Status          = Training.TrainingStatus.Completed;
            training.CompletedBy     = currentUser.Id;
            training.CompletedOn     = DateTime.UtcNow;
            training.CompletionNotes = CompletionNotes.Trim();
            var _trainSets = await _context.SystemSettings.FindAsync(1) ?? new OpenQMS.Models.SystemSettings();
            training.RetainUntil = training.CompletedOn.Value.AddYears(_trainSets.TrainingRetentionYears);
            AddAuditLog(training.Id, "Completed", "Scheduled", "Completed",
                $"Completed by: {currentUser.UserName}; Notes: {CompletionNotes.Trim()}");
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Training {training.TrainingId} competency verified and marked as Completed.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Trainings/ExportRegister
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> ExportRegister()
        {
            var trainings = await _context.Training
                .Where(t => !t.IsDeleted)
                .Include(t => t.CompletedByUser)
                .Include(t => t.Trainees)
                .OrderBy(t => t.TrainingId)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Training Register");

            var headers = new[] { "Training ID", "Name", "Description", "Date Created", "Policy", "Trainer", "Trainees", "Status", "Completed By", "Completed On" };
            for (int i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).SetValue(headers[i]);

            var headerRow = ws.Row(1);
            headerRow.Style.Font.Bold = true;
            headerRow.Style.Fill.BackgroundColor = XLColor.FromHtml("#2196F3");
            headerRow.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var t in trainings)
            {
                ws.Cell(row, 1).SetValue(t.TrainingId);
                ws.Cell(row, 2).SetValue(t.Name);
                ws.Cell(row, 3).SetValue(t.Description);
                ws.Cell(row, 4).SetValue(t.CreatedOn.ToSite().ToString("dd/MM/yyyy HH:mm"));
                ws.Cell(row, 5).SetValue(t.PolicyTitle);
                ws.Cell(row, 6).SetValue(t.TrainerEmail);
                ws.Cell(row, 7).SetValue(t.Trainees?.Count ?? 0);
                ws.Cell(row, 8).SetValue(t.Status.ToString());
                ws.Cell(row, 9).SetValue(t.CompletedByUser?.UserName ?? "-");
                ws.Cell(row, 10).SetValue(t.CompletedOn.HasValue ? t.CompletedOn.Value.ToSite().ToString("dd/MM/yyyy HH:mm") : "-");

                if (row % 2 == 0)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
                row++;
            }

            ws.Columns().AdjustToContents();
            ws.SheetView.FreezeRows(1);

            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"TrainingRegister_{SiteTime.Today:yyyyMMdd}.xlsx");
        }

        // GET: Training certificate (trainees) — browser print-to-PDF
        public async Task<IActionResult> ExportTrainingCertificate(int id)
        {
            var training = await _context.Training
                .Include(x => x.CompletedByUser)
                .Include(s => s.Trainees).ThenInclude(t => t.Trainee)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (training == null) return NotFound();

            var trainee = await _userManager.GetUserAsync(User);
            bool userIsTrainee = training.Trainees.Any(u => u.Trainee.UserName == trainee!.UserName);
            if (!userIsTrainee) return Forbid();

            var ut = training.Trainees
                .Where(t => t.Trainee.UserName == trainee!.UserName)
                .OrderByDescending(t => t.Id)
                .FirstOrDefault();

            var policyDoc = training.PolicyId > 0
                ? await _context.AppDocument.AsNoTracking()
                    .FirstOrDefaultAsync(d => d.Id == training.PolicyId)
                : null;

            ViewBag.Trainee    = trainee;
            ViewBag.UserTraining = ut;
            ViewBag.PolicyDocument = policyDoc;
            return View("TrainingCertificate", training);
        }

        // GET: Attendance list (managers)
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> ExportTrainingAttendance(int id)
        {
            var training = await _context.Training
                .Include(x => x.CompletedByUser)
                .Include(s => s.Trainees).ThenInclude(t => t.Trainee)
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == id);
            if (training == null) return NotFound();

            ViewBag.PolicyDocument = await _context.AppDocument
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == training.PolicyId);

            return View("AttendanceList", training);
        }

        private bool TrainingExists(int id) => _context.Training.Any(e => e.Id == id);

        // POST: Trainings/Cancel
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,RegulatoryManager,Trainer,AuthorisedPerson")]
        public async Task<IActionResult> Cancel(int id, string email, string pwd, string reason)
        {
            var training = await _context.Training.FindAsync(id);
            if (training == null) return NotFound();

            if (training.Status != Training.TrainingStatus.Scheduled)
            {
                TempData["WorkflowError"] = "Only Scheduled trainings can be cancelled.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash ?? string.Empty, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = "A reason is required to cancel a training.";
                return RedirectToAction(nameof(Details), new { id });
            }

            training.Status = Training.TrainingStatus.Cancelled;
            AddAuditLog(training.Id, "Cancelled", Training.TrainingStatus.Scheduled.ToString(), Training.TrainingStatus.Cancelled.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Training {training.TrainingId} has been cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AdminReopen(int id, string reason, string email, string pwd)
        {
            var training = await _context.Training.FindAsync(id);
            if (training == null) return NotFound();

            if (training.Status != Training.TrainingStatus.Completed)
            {
                TempData["WorkflowError"] = "This record is not in a state that can be administratively reopened.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
            {
                TempData["WorkflowError"] = "Electronic signature must use your own credentials.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrEmpty(pwd) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash ?? string.Empty, pwd) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = "A justification reason is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            training.Status          = Training.TrainingStatus.Scheduled;
            training.CompletedBy     = null;
            training.CompletedOn     = null;
            training.CompletionNotes = null;
            training.RetainUntil     = null;

            AddAuditLog(training.Id, "Administrative Reopen", Training.TrainingStatus.Completed.ToString(), Training.TrainingStatus.Scheduled.ToString(), reason);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Training record has been administratively reopened to Scheduled stage.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Trainings/MyTraining
        public async Task<IActionResult> MyTraining()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Forbid();

            var rows = await _context.UserTraining
                .Where(ut => ut.TraineeId == currentUser.Id)
                .Include(ut => ut.Training)
                    .ThenInclude(t => t.Trainees)
                .OrderByDescending(ut => ut.Id)
                .ToListAsync();

            // Latest row per training (account for re-attempts)
            var latestRows = rows
                .GroupBy(ut => ut.TrainingId)
                .Select(g => g.OrderByDescending(ut => ut.Id).First())
                .ToList();

            // Load linked documents for training records that have a PolicyId
            var docIds = latestRows
                .Where(r => r.Training?.PolicyId > 0)
                .Select(r => r.Training!.PolicyId)
                .Distinct()
                .ToList();
            var docs = await _context.AppDocument
                .Where(d => docIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d);

            // For docs that are no longer Effective/Approved, resolve the latest Effective version
            // of the same logical DocumentId so the view can link to it instead.
            var nonEffectiveDocs = docs.Values
                .Where(d => d.Status != AppDocument.DocumentStatus.Effective &&
                            d.Status != AppDocument.DocumentStatus.Approved)
                .ToList();
            var latestEffectiveDocs = new Dictionary<int, AppDocument>();
            if (nonEffectiveDocs.Count > 0)
            {
                var logicalIds = nonEffectiveDocs.Select(d => d.DocumentId).Distinct().ToList();
                var effectiveCandidates = await _context.AppDocument
                    .Where(d => logicalIds.Contains(d.DocumentId) &&
                                (d.Status == AppDocument.DocumentStatus.Effective ||
                                 d.Status == AppDocument.DocumentStatus.Approved))
                    .ToListAsync();
                foreach (var obsoleteDoc in nonEffectiveDocs)
                {
                    var latest = effectiveCandidates
                        .Where(d => d.DocumentId == obsoleteDoc.DocumentId)
                        .OrderByDescending(d => d.Version)
                        .FirstOrDefault();
                    if (latest != null)
                        latestEffectiveDocs[obsoleteDoc.Id] = latest;
                }
            }

            ViewBag.LinkedDocs = docs;
            ViewBag.LatestEffectiveDocs = latestEffectiveDocs;

            var outstanding = latestRows.Where(r =>
                r.Status != UserTrainingStatus.Completed &&
                r.Status != UserTrainingStatus.Failed &&
                r.Status != UserTrainingStatus.Cancelled &&
                r.Training?.Status != Training.TrainingStatus.Cancelled).ToList();
            var completed   = latestRows.Where(r => r.Status == UserTrainingStatus.Completed).ToList();

            ViewBag.Outstanding = outstanding;
            ViewBag.Completed   = completed;

            return View(latestRows);
        }

        // POST: Trainings/AcknowledgeReading
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcknowledgeReading(int userTrainingId, string InputEmail, string InputPassword)
        {
            var row = await _context.UserTraining
                .Include(ut => ut.Training)
                .FirstOrDefaultAsync(ut => ut.Id == userTrainingId);

            if (row == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || currentUser.Id != row.TraineeId)
            {
                TempData["WorkflowError"] = "You can only acknowledge your own training record.";
                return RedirectToAction(nameof(MyTraining));
            }

            if (row.Status != UserTrainingStatus.Scheduled)
            {
                TempData["WorkflowError"] = "This training record has already been acknowledged.";
                return RedirectToAction(nameof(MyTraining));
            }

            if (row.Training?.Status == Training.TrainingStatus.Cancelled)
            {
                TempData["WorkflowError"] = "This training has been cancelled.";
                return RedirectToAction(nameof(MyTraining));
            }

            if (!string.Equals(currentUser.Email, InputEmail, StringComparison.OrdinalIgnoreCase) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash ?? string.Empty, InputPassword) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(MyTraining));
            }

            var training = row.Training!;
            row.ReadAcknowledgedOn        = DateTime.UtcNow;
            row.ReadAcknowledgedSignature = currentUser.Email;

            if (training.TrainingType == TrainingType.ReadAndAcknowledge)
                row.Status = UserTrainingStatus.Completed;
            else
                row.Status = UserTrainingStatus.PendingTrainerConfirmation;

            _context.UserTraining.Update(row);
            await RecalculateTrainingStatus(training);

            var ackAction = training.TrainingType == TrainingType.ReadAndAcknowledge
                ? "Reading Acknowledged"
                : "Submitted for Competency Assessment";
            AddAuditLog(training.Id, ackAction, "Scheduled",
                row.Status.ToString(),
                $"Trainee: {currentUser.UserName}");

            await _context.SaveChangesAsync();

            // Check if document auto-transitions to Effective
            await _docWorkflow.CheckAndAutoEffective(training.Id, _userManager.GetUserName(User));
            await _context.SaveChangesAsync();

            if (training.TrainingType == TrainingType.ReadAndAcknowledge)
            {
                TempData["Success"] = "Training acknowledged and marked as complete.";
            }
            else
            {
                TempData["Success"] = "Document reading acknowledged.";
                TempData["WorkflowInfo"] = "This training requires a competency assessment. Your reading acknowledgement has been recorded, but this training will only be fully complete once your trainer has confirmed your competency. Please complete the assessment as instructed by your trainer.";
            }
            return RedirectToAction(nameof(MyTraining));
        }

        // POST: Trainings/VerifyCompetency
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,Trainer")]
        public async Task<IActionResult> VerifyCompetency(int userTrainingId, bool passed, string? Notes, string InputEmail, string InputPassword)
        {
            var row = await _context.UserTraining
                .Include(ut => ut.Training)
                .FirstOrDefaultAsync(ut => ut.Id == userTrainingId);

            if (row == null) return NotFound();

            if (row.Status != UserTrainingStatus.PendingTrainerConfirmation)
            {
                TempData["WorkflowError"] = "This trainee is not pending trainer confirmation.";
                return RedirectToAction(nameof(Details), new { id = row.TrainingId });
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null ||
                !string.Equals(currentUser.Email, InputEmail, StringComparison.OrdinalIgnoreCase) ||
                _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash ?? string.Empty, InputPassword) != PasswordVerificationResult.Success)
            {
                TempData["WorkflowError"] = "Invalid email or password.";
                return RedirectToAction(nameof(Details), new { id = row.TrainingId });
            }

            var training = row.Training!;

            if (passed)
            {
                row.Status                       = UserTrainingStatus.Completed;
                row.CompetencyVerifiedOn         = DateTime.UtcNow;
                row.CompetencyVerifiedBy         = _userManager.GetUserName(User);
                row.CompetencyVerifiedSignature  = currentUser.Email;
                row.CompetencyNotes              = Notes;
                _context.UserTraining.Update(row);
            }
            else
            {
                // Mark current row as Failed (preserving the record) and create a new Scheduled row
                row.Status                       = UserTrainingStatus.Failed;
                row.CompetencyVerifiedOn         = DateTime.UtcNow;
                row.CompetencyVerifiedBy         = _userManager.GetUserName(User);
                row.CompetencyVerifiedSignature  = currentUser.Email;
                row.CompetencyNotes              = Notes;
                _context.UserTraining.Update(row);

                var newAttempt = new UserTraining
                {
                    TraineeId  = row.TraineeId,
                    TrainingId = row.TrainingId,
                    Status     = UserTrainingStatus.Scheduled
                };
                _context.UserTraining.Add(newAttempt);
            }

            await RecalculateTrainingStatus(training, completedById: currentUser.Id);

            var traineeUser = await _userManager.FindByIdAsync(row.TraineeId.ToString());
            AddAuditLog(training.Id,
                passed ? "Competency Verified — Passed" : "Competency Verified — Failed",
                "PendingTrainerConfirmation",
                passed ? "Completed" : "Scheduled",
                $"Trainee: {traineeUser?.UserName ?? row.TraineeId.ToString()}; Verifier: {_userManager.GetUserName(User)}" +
                (string.IsNullOrEmpty(Notes) ? "" : $"; Notes: {Notes}"));

            await _context.SaveChangesAsync();

            if (passed)
            {
                await _docWorkflow.CheckAndAutoEffective(training.Id, _userManager.GetUserName(User));
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = passed
                ? "Competency verified. Trainee marked as completed."
                : "Competency failed. A new training attempt has been created for the trainee.";
            return RedirectToAction(nameof(Details), new { id = row.TrainingId });
        }

        // POST: Trainings/UploadAttachment
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadAttachment(int userTrainingId, IFormFile file)
        {
            if (User.IsInRole(Constants.ReadOnlyRole)) return Forbid();
            var row = await _context.UserTraining
                .Include(ut => ut.Training)
                .FirstOrDefaultAsync(ut => ut.Id == userTrainingId);
            if (row == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Forbid();

            bool isManagerOrTrainer = User.IsInRole(Constants.AdministratorsRole)
                                   || User.IsInRole(Constants.QualityManagerRole)
                                   || User.IsInRole(Constants.TrainerRole)
                                   || User.IsInRole(Constants.AuthorisedPersonRole);
            bool isOwnRecord = currentUser.Id == row.TraineeId;

            if (!isManagerOrTrainer && !isOwnRecord)
                return Forbid();

            var trainingStatus = row.Training.Status;
            if (trainingStatus == Training.TrainingStatus.Completed || trainingStatus == Training.TrainingStatus.Cancelled)
            {
                TempData["WorkflowError"] = "Attachments cannot be added to a completed or cancelled training.";
                TempData["ActiveTab"] = "trainees";
                return RedirectToAction(nameof(Details), new { id = row.TrainingId });
            }

            if (file == null || file.Length == 0)
            {
                TempData["WorkflowError"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "trainees";
                return RedirectToAction(nameof(Details), new { id = row.TrainingId });
            }

            if (!UploadRules.IsAllowed(file.FileName))
            {
                TempData["WorkflowError"] = $"File type not allowed. Accepted: {UploadRules.AcceptedLabel}.";
                TempData["ActiveTab"] = "trainees";
                return RedirectToAction(nameof(Details), new { id = row.TrainingId });
            }

            // Training attachments are stored in the database, hence the lower limit
            if (file.Length > 10L * 1024 * 1024)
            {
                TempData["WorkflowError"] = "File exceeds the 10 MB limit.";
                TempData["ActiveTab"] = "trainees";
                return RedirectToAction(nameof(Details), new { id = row.TrainingId });
            }

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var content = ms.ToArray();
            _context.TrainingAttachment.Add(new TrainingAttachment
            {
                UserTrainingId = userTrainingId,
                FileName       = Path.GetFileName(file.FileName),
                Content        = content,
                FileHash       = OpenQMS.Services.FileHashHelper.ComputeSha256(content),
                ContentType    = file.ContentType,
                UploadedBy     = _userManager.GetUserName(User) ?? "Unknown",
                UploadedOn     = DateTime.UtcNow
            });
            AddAuditLog(row.TrainingId, "Attachment uploaded", null, null,
                $"{Path.GetFileName(file.FileName)} (trainee record: {userTrainingId}; uploaded by: {_userManager.GetUserName(User) ?? "Unknown"})");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Attachment uploaded.";
            TempData["ActiveTab"] = "trainees";
            return RedirectToAction(nameof(Details), new { id = row.TrainingId });
        }

        // GET: Trainings/DownloadAttachment/5
        public async Task<IActionResult> DownloadAttachment(int id)
        {
            var att = await _context.TrainingAttachment
                .Include(a => a.UserTraining)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (att == null) return NotFound();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Forbid();

            bool isManagerOrTrainer = User.IsInRole(Constants.AdministratorsRole)
                                   || User.IsInRole(Constants.QualityManagerRole)
                                   || User.IsInRole(Constants.TrainerRole)
                                   || User.IsInRole(Constants.AuthorisedPersonRole);
            bool isOwnRecord = att.UserTraining?.TraineeId == currentUser.Id;

            if (!isManagerOrTrainer && !isOwnRecord) return Forbid();

            if (!OpenQMS.Services.FileIntegrity.Matches(att.Content, att.FileHash))
            {
                AddAuditLog(att.UserTraining!.TrainingId, OpenQMS.Services.FileIntegrity.AuditAction, null, null,
                    $"{OpenQMS.Services.FileIntegrity.AuditNote(att.FileName)} (trainee: {att.UserTraining.TraineeId})");
                await _context.SaveChangesAsync();
                TempData["WorkflowError"] = OpenQMS.Services.FileIntegrity.FailureMessage(att.FileName);
                TempData["ActiveTab"] = "trainees";
                return RedirectToAction(nameof(Details), new { id = att.UserTraining.TrainingId });
            }

            AddAuditLog(att.UserTraining!.TrainingId, "Attachment downloaded", null, null,
                $"{att.FileName} (trainee: {att.UserTraining.TraineeId})");
            await _context.SaveChangesAsync();

            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Training",
                att.UserTraining.TrainingId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow);
            var watermarked = WatermarkService.Apply(
                att.Content, Path.GetExtension(att.FileName), wmCtx);
            return File(watermarked, att.ContentType, att.FileName);
        }

        // GET: Trainings/Compliance
        [Authorize(Roles = "Administrator,QualityManager,Trainer")]
        public async Task<IActionResult> Compliance(int? documentId, string? userId, string? status, bool overdueOnly = false)
        {
            var isTrainer = User.IsInRole(Constants.TrainerRole)
                         && !User.IsInRole(Constants.QualityManagerRole)
                         && !User.IsInRole(Constants.AdministratorsRole);

            var query = _context.UserTraining
                .Include(ut => ut.Training)
                .Include(ut => ut.Trainee)
                .AsQueryable();

            if (isTrainer)
            {
                var trainerUser = await _userManager.GetUserAsync(User);
                query = query.Where(ut => ut.Training != null && ut.Training.TrainerId == trainerUser!.Id.ToString());
            }

            if (documentId.HasValue)
                query = query.Where(ut => ut.Training != null && ut.Training.PolicyId == documentId.Value);

            if (!string.IsNullOrEmpty(userId) && int.TryParse(userId, out var uid))
                query = query.Where(ut => ut.TraineeId == uid);

            if (!string.IsNullOrEmpty(status) && Enum.TryParse<UserTrainingStatus>(status, out var statusEnum))
                query = query.Where(ut => ut.Status == statusEnum);

            if (overdueOnly)
                query = query.Where(ut => ut.Training != null && ut.Training.DueDate.HasValue
                                        && ut.Training.DueDate.Value.Date < SiteTime.Today
                                        && ut.Status != UserTrainingStatus.Completed);

            var rows = await query.OrderByDescending(ut => ut.Id).Take(500).ToListAsync();

            // Load documents for filter dropdown
            ViewBag.Documents = await _context.AppDocument
                .Where(d => !d.IsDeleted)
                .OrderBy(d => d.DocumentId)
                .ToListAsync();
            ViewBag.AllUsers     = _userManager.Users.OrderBy(u => u.UserName).ToList();
            ViewBag.DocumentId   = documentId;
            ViewBag.UserId       = userId;
            ViewBag.Status       = status;
            ViewBag.OverdueOnly  = overdueOnly;
            ViewBag.StatusValues = Enum.GetValues<UserTrainingStatus>();

            return View(rows);
        }

        // Recalculate Training aggregate status based on latest UserTraining row per trainee
        private async Task RecalculateTrainingStatus(Training training, int? completedById = null)
        {
            var allRows = await _context.UserTraining
                .Where(ut => ut.TrainingId == training.Id)
                .OrderByDescending(ut => ut.Id)
                .ToListAsync();

            var latestPerTrainee = allRows
                .GroupBy(ut => ut.TraineeId)
                .Select(g => g.First())
                .ToList();

            Training.TrainingStatus newStatus;
            if (latestPerTrainee.Count == 0)
                newStatus = Training.TrainingStatus.Completed;
            else if (latestPerTrainee.All(r => r.Status == UserTrainingStatus.Completed))
                newStatus = Training.TrainingStatus.Completed;
            else if (latestPerTrainee.All(r => r.Status == UserTrainingStatus.Completed
                                            || r.Status == UserTrainingStatus.PendingTrainerConfirmation))
                newStatus = Training.TrainingStatus.PendingTrainerConfirmation;
            else if (latestPerTrainee.All(r => r.Status == UserTrainingStatus.Scheduled))
                newStatus = Training.TrainingStatus.Scheduled;
            else
                newStatus = Training.TrainingStatus.InProgress;

            if (training.Status != Training.TrainingStatus.Cancelled)
            {
                training.Status = newStatus;
                // Capture completion metadata when auto-completing so the Overview tab is never blank
                if (newStatus == Training.TrainingStatus.Completed && training.CompletedOn == null)
                {
                    training.CompletedOn = DateTime.UtcNow;
                    training.CompletedBy = completedById; // null = system/auto; trainer id when CA verifier triggers it
                }
            }
            _context.Training.Update(training);
        }

        private void AddAuditLog(int parentId, string action, string? fromStatus, string? toStatus, string? notes = null)
        {
            _context.MasterDataAuditLog.Add(new MasterDataAuditLog
            {
                EntityType = "Training",
                ParentId = parentId,
                Action = action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                PerformedBy = User.Identity?.Name ?? "System",
                PerformedOn = DateTime.UtcNow,
                Notes = notes
            });
        }
    }
}
