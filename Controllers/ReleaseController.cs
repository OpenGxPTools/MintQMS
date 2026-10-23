using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models;
using OpenQMS.Models.Release;
using OpenQMS.Services;
using OpenQMS.ViewModels.Release;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,QualityUser,RegulatoryManager,RegulatoryUser,AuthorisedPerson,Auditor,Trainer,ReadOnly,OperationsUser")]
    public class ReleaseController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IPasswordHasher<AppUser> _passwordHasher;
        private readonly ReleaseWorkflowService _workflow;

        // Default AP certification statement (PIC/S Annex 16 compliant)
        private const string DefaultAPStatement =
            "I hereby certify that this batch complies with the requirements of the Marketing Authorisation " +
            "and the principles of Good Manufacturing Practice, and that all relevant manufacturing, quality " +
            "control, and regulatory requirements have been satisfied.";

        // Default checklist items seeded on record creation
        // ── Pharmaceutical / Biological / Consumer Health / Cosmetic / Raw Material (API, Excipient, Cosmetic Ingredient, Packaging)
        // Tuple: (Category, ItemText, IsBlocking, NonBlockingForQO)
        // NonBlockingForQO=true means the item is created as non-blocking on Quality Oversight records.
        private static readonly (string Category, string ItemText, bool IsBlocking, bool NonBlockingForQO)[] DefaultChecklistItems = new[]
        {
            ("Manufacturing", "Batch Manufacturing Record (BMR) reviewed and found in conformity with GMP and the approved Product Specification File", true,  true),
            ("Manufacturing", "Production conditions (process parameters, yields) reviewed and within approved limits",                                  true,  true),
            ("Manufacturing", "In-process control results reviewed and compliant",                                                                      true,  true),
            ("Manufacturing", "Equipment cleaning records reviewed; no cross-contamination risk identified",                                            true,  true),
            ("Manufacturing", "Labelling and artwork reconciliation completed and confirmed correct",                                                    true,  false),
            ("Manufacturing", "Process validation status confirmed current and appropriate for this batch",                                             false, false),
            ("Manufacturing", "Equipment qualification status confirmed current",                                                                       false, false),
            ("QC",            "Finished product QC test results reviewed and comply with the approved product specification",                           true,  false),
            ("QC",            "Raw material / excipient Certificates of Analysis reviewed",                                                             true,  true),
            ("QC",            "Environmental monitoring data reviewed; no unresolved OOT or OOS results with batch impact",                            true,  true),
            ("QC",            "Reference standards and reagents used are within validity",                                                              false, false),
            ("Regulatory",    "Manufacturing site holds a current TGA GMP Licence / GMP Clearance for the manufacturing steps performed",              true,  false),
            ("Regulatory",    "Product is registered / approved for supply under the applicable pathway (ARTG, SAS Cat B, AP, etc.)",                  true,  false),
            ("Regulatory",    "Any applicable post-marketing regulatory commitments addressed",                                                         false, false),
            ("Regulatory",    "Technical Agreement in place and current (if applicable)",                                                               false, false),
            ("Deviations",    "All batch-related deviations reviewed; no outstanding deviations with unresolved quality impact",                        true,  false),
            ("Deviations",    "All CAPAs linked to batch-relevant deviations reviewed; no outstanding CAPAs with unresolved batch impact",             true,  false),
            ("Distribution",  "Ongoing stability programme confirmed in place; no adverse findings impacting this batch",                               false, false),
            ("Distribution",  "No applicable product recalls, quality alerts, or field safety corrective actions at time of certification",             true,  false),
            ("Distribution",  "Storage and distribution conditions confirmed appropriate for batch dispatch",                                           false, false),
            ("Distribution",  "Quarantine controls in place during transit (if applicable — Release Under Quarantine type)",                           false, false),
            ("Distribution",  "Most recent Product Quality Review confirmed as completed, no adverse findings impacting this batch",                    false, false),
        };

        // ── Medical Device / Raw Material (Device Part, Device Subcomponent)
        private static readonly (string Category, string ItemText, bool IsBlocking, bool NonBlockingForQO)[] MedicalDeviceChecklistItems = new[]
        {
            ("Manufacturing / Production", "Device History Record (DHR) reviewed; all manufacturing steps completed in conformity with the approved Device Master Record (DMR)", true,  false),
            ("Manufacturing / Production", "In-process inspection records reviewed; all results within acceptance criteria",                                                      true,  false),
            ("Manufacturing / Production", "Equipment qualification and calibration status confirmed current for all equipment used in manufacture",                             true,  false),
            ("Manufacturing / Production", "Process validation / verification status confirmed current for all manufacturing processes applied to this batch",                   true,  false),
            ("Manufacturing / Production", "Labelling reviewed and confirmed compliant with applicable regulatory requirements; UDI applied where required",                     true,  false),
            ("Manufacturing / Production", "Traceability records complete — lot / batch, serial numbers, and component / raw material traceability documented",                 true,  false),
            ("Manufacturing / Production", "Sterilisation cycle records reviewed and batch release criteria met (if applicable)",                                                false, false),
            ("Manufacturing / Production", "Packaging and sterile barrier integrity confirmed; no evidence of compromise (if applicable)",                                       false, false),
            ("Quality Control / Inspection", "Final product inspection and test results reviewed; all results comply with the approved product specification and acceptance criteria", true,  false),
            ("Quality Control / Inspection", "Incoming component and sub-assembly inspection records reviewed and accepted",                                                          true,  false),
            ("Quality Control / Inspection", "Non-conformances and deviations reviewed; all resolved or formally accepted with documented risk justification; no outstanding items with unresolved quality impact", true, false),
            ("Quality Control / Inspection", "Risk Management File reviewed; no new hazards or risks identified that affect device safety or performance for this batch",             true,  false),
            ("Quality Control / Inspection", "Software verification and validation status confirmed current (if device includes software / firmware)",                                false, false),
            ("Regulatory", "Manufacturing site holds a current regulatory licence / registration appropriate for the device class and manufacturing steps performed",  true,  false),
            ("Regulatory", "Device registered / approved under the applicable regulatory framework (TGA ARTG, CE MDR 2017/745, FDA 510(k) / PMA, etc.)",              true,  false),
            ("Regulatory", "Unique Device Identifier (UDI) confirmed assigned, labelled and reported to the applicable regulatory database as required",               false, false),
            ("Regulatory", "Post-Market Surveillance (PMS) review completed; no unresolved safety signals or outstanding Field Safety Corrective Actions (FSCAs) affecting this device type", true, false),
            ("Regulatory", "Technical / Quality Agreement confirmed in place with contract manufacturer or critical component supplier (if applicable)",                false, false),
            ("Distribution", "Storage conditions during quarantine confirmed appropriate; no adverse environmental exposure identified",                                true,  false),
            ("Distribution", "No applicable recalls, field safety corrective actions or market withdrawal notices in effect at time of release",                        true,  false),
            ("Distribution", "Device release traceability system confirmed in place; release can be traced to consignee level",                                         false, false),
            ("Distribution", "Most recent Post-Market Surveillance (PMS) / Periodic Safety Update Report (PSUR) confirmed as completed, no adverse findings impacting this device type", false, false),
        };

        // ── Raw Material (Consumable) — lighter framework
        private static readonly (string Category, string ItemText, bool IsBlocking, bool NonBlockingForQO)[] ConsumableChecklistItems = new[]
        {
            ("Receipt & Identification", "Delivery documentation reviewed (delivery note / invoice); product description and quantity match the purchase order", true,  false),
            ("Receipt & Identification", "Product identity confirmed — labelling, markings and packaging consistent with the ordered specification",             true,  false),
            ("Receipt & Identification", "Lot / batch number or other traceability reference recorded",                                                         true,  false),
            ("Receipt & Identification", "Expiry / use-by date confirmed acceptable for intended use",                                                          true,  false),
            ("Visual Inspection",  "Visual inspection completed; packaging intact, no visible damage, contamination or deterioration observed",                  true,  false),
            ("Visual Inspection",  "Storage and transport conditions confirmed appropriate upon receipt",                                                        false, false),
            ("Documentation",      "Certificate of Conformance (CoC) or equivalent supplier documentation received and reviewed",                               false, false),
            ("Documentation",      "Material Safety Data Sheet (MSDS / SDS) available where applicable",                                                        false, false),
            ("Release Decision",   "No open supplier quality issues or quality alerts affecting this material at time of release",                               true,  false),
            ("Release Decision",   "Material approved for release to stores / production use",                                                                   true,  false),
        };

        // Determine which checklist template applies based on product type and raw material category
        private static string GetCertProductGroup(OpenQMS.Models.ProductType? productType, string? rawMaterialTypeCategory)
        {
            if (productType == OpenQMS.Models.ProductType.MedicalDevice)  return "MedDevice";
            if (productType == OpenQMS.Models.ProductType.ConsumerHealth) return "ConsumerHealth";
            if (productType == OpenQMS.Models.ProductType.Cosmetic)       return "Cosmetic";
            if (productType == OpenQMS.Models.ProductType.RawMaterial)
            {
                var cat = rawMaterialTypeCategory?.Trim() ?? "";
                if (cat == "Device Part" || cat == "Device Subcomponent") return "MedComponent";
                if (cat == "API")        return "RawMatAPI";
                if (cat == "Consumable") return "Consumable";
                return "RawMatOther"; // Excipient, Cosmetic Ingredient, Packaging
            }
            return "PharmaBio"; // Pharmaceutical, Biological, or unknown
        }

        private static string GetChecklistTemplate(OpenQMS.Models.ProductType? productType, string? rawMaterialTypeCategory)
        {
            if (productType == OpenQMS.Models.ProductType.MedicalDevice)
                return "MedicalDevice";
            if (productType == OpenQMS.Models.ProductType.RawMaterial)
            {
                var cat = rawMaterialTypeCategory?.Trim() ?? "";
                if (cat == "Device Part" || cat == "Device Subcomponent")
                    return "MedicalDevice";
                if (cat == "Consumable")
                    return "Consumable";
            }
            return "Standard";
        }

        public ReleaseController(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            IPasswordHasher<AppUser> passwordHasher,
            ReleaseWorkflowService workflow)
        {
            _context = context;
            _userManager = userManager;
            _passwordHasher = passwordHasher;
            _workflow = workflow;
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private string CurrentUser => User.Identity?.Name ?? "System";

        private async Task PopulateSitesViewBag()
        {
            var approvedStatuses = new[] { OpenQMS.Models.SupplierStatus.Approved, OpenQMS.Models.SupplierStatus.Qualified };
            var suppliers = await _context.Supplier
                .Where(s => !s.IsDeleted && approvedStatuses.Contains(s.Status))
                .OrderBy(s => s.LegalName)
                .Select(s => new { s.LegalName, s.SupplierType })
                .ToListAsync();

            ViewBag.ManufacturingSuppliers = suppliers
                .Where(s => s.SupplierType == OpenQMS.Models.SupplierType.Product)
                .Select(s => s.LegalName).ToList();

            ViewBag.ServiceSuppliers = suppliers
                .Where(s => s.SupplierType == OpenQMS.Models.SupplierType.Service)
                .Select(s => s.LegalName).ToList();

            ViewBag.ReleasingSuppliers = suppliers
                .Select(s => s.LegalName).ToList();

            var settings = await _context.SystemSettings.FindAsync(1);
            ViewBag.OwnCompanyName = settings?.OrganisationName ?? string.Empty;
        }

        private void AddAudit(int recordId, string action, string? fromStatus, string? toStatus, string? details = null)
        {
            _context.ReleaseAuditEntries.Add(new ReleaseAuditEntry
            {
                ReleaseRecordId = recordId,
                Action = action,
                FromStatus = fromStatus,
                ToStatus = toStatus,
                Details = details,
                PerformedBy = CurrentUser,
                Timestamp = DateTime.UtcNow,
                IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
            });
        }

        // Finding 1: credential verification for all workflow transitions (email + password)
        private async Task<bool> VerifyCredentials(string email, string pwd)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null || !string.Equals(currentUser.Email, email, StringComparison.OrdinalIgnoreCase))
                return false;
            if (string.IsNullOrEmpty(pwd)) return false;
            return _passwordHasher.VerifyHashedPassword(currentUser, currentUser.PasswordHash!, pwd)
                == PasswordVerificationResult.Success;
        }

        private async Task<string> GenerateRecordNumber()
        {
            var year = DateTime.UtcNow.Year;
            var count = await _context.ReleaseRecords.CountAsync(r => r.CreatedDate.Year == year);
            return $"REL-{year}-{(count + 1):D4}";
        }

        private async Task<string> GenerateCertificateNumber(CertificateType type)
        {
            var year = DateTime.UtcNow.Year;
            var prefix = type switch
            {
                CertificateType.CoA      => "COA",
                CertificateType.CoC      => "COC",
                CertificateType.Combined => "COAC",
                _                        => "CERT"
            };
            var count = await _context.ReleaseCertificates
                .CountAsync(c => c.IssuedDate.Year == year && c.CertificateType == type);
            return $"{prefix}-{year}-{(count + 1):D4}";
        }

        private static string EnumDisplayName<T>(T value) where T : Enum
        {
            var fi = value.GetType().GetField(value.ToString()!);
            var attr = fi?.GetCustomAttribute<DisplayAttribute>();
            return attr?.Name ?? value.ToString()!;
        }

        private static string ReleaseStatusLabel(ReleaseStatus s) =>
            s == ReleaseStatus.Closed ? "Released" : EnumDisplayName(s);

        private bool IsPrivileged =>
            User.IsInRole(Constants.AdministratorsRole) ||
            User.IsInRole(Constants.QualityManagerRole) ||
            User.IsInRole(Constants.AuthorisedPersonRole) ||
            User.IsInRole(Constants.QualityUserRole) ||
            User.IsInRole(Constants.OperationsUserRole);

        private bool IsAP =>
            User.IsInRole(Constants.AdministratorsRole) ||
            User.IsInRole(Constants.AuthorisedPersonRole);

        // ── Dashboard ────────────────────────────────────────────────────────

        public async Task<IActionResult> Dashboard()
        {
            var now = DateTime.UtcNow;
            var yearStart = new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            var allRecords = await _context.ReleaseRecords.ToListAsync();

            var vm = new ReleaseDashboardViewModel
            {
                TotalRecordsThisYear = allRecords.Count(r => r.CreatedDate >= yearStart),
                PendingAPDecision    = allRecords.Count(r => r.Status == ReleaseStatus.PendingAPDecision),
                ReleasedYTD          = allRecords.Count(r => r.CertificationDate >= yearStart &&
                                          (r.Status == ReleaseStatus.Certified ||
                                           r.Status == ReleaseStatus.Released  ||
                                           r.Status == ReleaseStatus.Closed)),
                OnHold               = allRecords.Count(r => r.Status == ReleaseStatus.OnHold),
                Rejected             = allRecords.Count(r => r.Status == ReleaseStatus.Rejected),
                ByType               = allRecords.GroupBy(r => EnumDisplayName(r.ReleaseType))
                                          .ToDictionary(g => g.Key, g => g.Count()),
                ByStatus             = allRecords.GroupBy(r => ReleaseStatusLabel(r.Status))
                                          .ToDictionary(g => g.Key, g => g.Count()),
                RecentRecords        = allRecords.OrderByDescending(r => r.CreatedDate).Take(10).ToList()
            };

            // Monthly trend — current calendar year
            vm.MonthlyTrend = Enumerable.Range(1, now.Month).Select(m =>
            {
                var monthRecords = allRecords.Where(r =>
                    r.CreatedDate.Year == now.Year && r.CreatedDate.Month == m).ToList();
                return new MonthlyReleaseData
                {
                    MonthLabel        = new DateTime(now.Year, m, 1).ToString("MMM"),
                    RFS               = monthRecords.Count(r => r.ReleaseType == ReleaseType.ReleaseForSupply),
                    Quarantine        = monthRecords.Count(r => r.ReleaseType == ReleaseType.ReleaseUnderQuarantine),
                    RFFP              = monthRecords.Count(r => r.ReleaseType == ReleaseType.ReleaseForFurtherProcessing),
                    ReleaseToCustomer = monthRecords.Count(r => r.ReleaseType == ReleaseType.ReleaseToCustomer),
                    QualityOversight  = monthRecords.Count(r => r.ReleaseType == ReleaseType.QualityOversight),
                };
            }).ToList();

            return View(vm);
        }

        // ── Index ────────────────────────────────────────────────────────────

        public async Task<IActionResult> Index()
        {
            var records = await _context.ReleaseRecords
                .OrderByDescending(r => r.CreatedDate)
                .ToListAsync();
            return View(records);
        }

        // ── Details ──────────────────────────────────────────────────────────

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var record = await _context.ReleaseRecords
                .Include(r => r.ChecklistItems)
                .Include(r => r.TestResults)
                .Include(r => r.Documents)
                .Include(r => r.Certificates)
                .Include(r => r.AuditEntries)
                .Include(r => r.CrossReferences)
                .Include(r => r.TestingLabs)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();

            if (record.LinkedProductId.HasValue)
            {
                ViewBag.LinkedProductSpecs = await _context.ProductSpecLines
                    .Where(s => s.ProductId == record.LinkedProductId.Value)
                    .OrderBy(s => s.DisplayOrder)
                    .ToListAsync();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser != null)
            {
                var fullName = $"{currentUser.FirstName} {currentUser.LastName}".Trim();
                if (!string.IsNullOrWhiteSpace(currentUser.APQualification))
                    fullName += $", {currentUser.APQualification}";
                ViewBag.APDefaultName = fullName;
            }

            // Load context-appropriate default certification statement from settings table
            var productGroup = record.ReleaseType == ReleaseType.QualityOversight
                ? "All"
                : GetCertProductGroup(record.ProductType, record.RawMaterialTypeCategory);
            var certSetting = await _context.ReleaseCertificationSettings
                .FirstOrDefaultAsync(s => s.ReleaseType == record.ReleaseType && s.ProductGroup == productGroup);
            ViewBag.APDefaultStatement = certSetting?.Statement;

            // Soft-deleted files for the "Deleted attachments" section (retained for traceability)
            ViewBag.DeletedFiles = await _context.ReleaseDocuments.DeletedOnly().Where(f => f.ReleaseRecordId == id).ToListAsync();
            return View(record);
        }

        // ── Create ───────────────────────────────────────────────────────────

        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> Create()
        {
            ViewBag.ProductsList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Product
                    .Where(p => p.Status == OpenQMS.Models.Product.ProductStatus.Approved && !p.IsDeleted)
                    .OrderBy(p => p.Name)
                    .Select(p => p.Name)
                    .ToListAsync());
            await PopulateSitesViewBag();
            return View(new ReleaseRecord());
        }

        [HttpGet]
        public async Task<IActionResult> GetProductDetails(string name)
        {
            var products = await _context.Product
                .Include(p => p.Strengths)
                .Include(p => p.Specifications)
                .Where(p => p.Name == name && !p.IsDeleted && p.Status == OpenQMS.Models.Product.ProductStatus.Approved)
                .ToListAsync();

            var product = products.OrderByDescending(p => p.Version).FirstOrDefault();

            if (product == null) return Json(null);

            return Json(new
            {
                productId              = product.Id,
                productType            = product.ProductType?.ToString() ?? "",
                innName                = product.InnName,
                brandName              = product.BrandName,
                dosageForm             = product.DosageForm,
                artgReference          = product.ArtgReference,
                inciName               = product.InciName,
                presentationForm       = product.PresentationForm,
                deviceTypeCategory     = product.DeviceTypeCategory,
                modelVersion           = product.ModelVersion,
                standardScientificName = product.StandardScientificName,
                rawMaterialTypeCategory= product.RawMaterialTypeCategory,
                grade                  = product.Grade,
                strengths              = product.Strengths?
                                            .OrderBy(s => s.Value)
                                            .Select(s => s.Value)
                                            .ToList() ?? new List<string>(),
                hasSpecs               = (product.Specifications?.Count ?? 0) > 0
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> Create([Bind(
            "ReleaseType,ProductName,ProductType,BrandName,InnName,DosageForm,Strength,ArtgReference," +
            "InciName,PresentationForm,DeviceTypeCategory,ModelVersion,StandardScientificName,RawMaterialTypeCategory,Grade," +
            "BatchNumber,BatchSize,PackSize,ManufacturingDate,ExpiryDate,RetestDate," +
            "ManufacturingSite,ManufacturingSiteGmpRef,PackagingSite,PackagingSiteGmpRef," +
            "ReleasingCompany,ReleasingCompanyLicenceRef,FinalReleaseParty,TechnicalAgreementRef," +
            "SpecificationReference,StorageConditions,DistributionConditions,Notes,LinkedProductId"
        )] ReleaseRecord record, string[]? labNames, string[]? labRefs)
        {
            // RecordNumber is auto-generated server-side; remove it from validation
            ModelState.Remove(nameof(ReleaseRecord.RecordNumber));

            if (ModelState.IsValid)
            {
                record.RecordNumber = await GenerateRecordNumber();
                record.Status = ReleaseStatus.Draft;
                record.CreatedDate = DateTime.UtcNow;
                record.CreatedBy = CurrentUser;
                record.IsLocked = false;

                _context.Add(record);
                await _context.SaveChangesAsync();

                // Save testing labs
                if (labNames != null)
                {
                    for (int i = 0; i < labNames.Length; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(labNames[i]))
                        {
                            _context.ReleaseTestingLabs.Add(new ReleaseTestingLab
                            {
                                ReleaseRecordId = record.Id,
                                LaboratoryName  = labNames[i].Trim(),
                                GmpLicenceRef   = (labRefs != null && i < labRefs.Length) ? labRefs[i]?.Trim() : null
                            });
                        }
                    }
                }

                // Seed checklist items — template chosen based on product type / material category
                var checklistTemplate = GetChecklistTemplate(record.ProductType, record.RawMaterialTypeCategory);
                var checklistItems = checklistTemplate switch
                {
                    "MedicalDevice" => MedicalDeviceChecklistItems,
                    "Consumable"    => ConsumableChecklistItems,
                    _               => DefaultChecklistItems
                };
                var order = 1;
                bool isQORecord = record.ReleaseType == ReleaseType.QualityOversight;
                foreach (var (cat, text, blocking, nonBlockingForQO) in checklistItems)
                {
                    _context.ReleaseChecklistItems.Add(new ReleaseChecklistItem
                    {
                        ReleaseRecordId = record.Id,
                        Category        = cat,
                        ItemText        = text,
                        IsBlocking      = blocking && !(isQORecord && nonBlockingForQO),
                        DisplayOrder    = order++,
                        Status          = ChecklistItemStatus.NotReviewed
                    });
                }

                // Seed test results from linked product specifications
                if (record.LinkedProductId.HasValue)
                {
                    var specs = await _context.ProductSpecLines
                        .Where(s => s.ProductId == record.LinkedProductId.Value)
                        .OrderBy(s => s.DisplayOrder)
                        .ToListAsync();

                    var specOrder = 1;
                    foreach (var spec in specs)
                    {
                        _context.ReleaseTestResults.Add(new ReleaseTestResult
                        {
                            ReleaseRecordId     = record.Id,
                            TestParameter       = spec.TestParameter,
                            TestMethod          = spec.Method,
                            AcceptanceCriterion = spec.AcceptanceCriterion,
                            Comment             = spec.Comments,
                            Status              = TestResultStatus.Pending,
                            DisplayOrder        = specOrder++,
                            EnteredBy           = CurrentUser,
                            EnteredDate         = DateTime.UtcNow
                        });
                    }
                }

                AddAudit(record.Id, "Created", null, record.Status.ToString());
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Release record {record.RecordNumber} created.";
                return RedirectToAction(nameof(Details), new { id = record.Id });
            }

            await PopulateSitesViewBag();
            ViewBag.ProductsList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(
                await _context.Product
                    .Where(p => p.Status == OpenQMS.Models.Product.ProductStatus.Approved && !p.IsDeleted)
                    .OrderBy(p => p.Name).Select(p => p.Name).ToListAsync());
            ViewBag.ExistingLabNames = labNames ?? Array.Empty<string>();
            ViewBag.ExistingLabRefs  = labRefs  ?? Array.Empty<string>();
            return View(record);
        }

        // ── Edit ─────────────────────────────────────────────────────────────

        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var record = await _context.ReleaseRecords
                .Include(r => r.ChecklistItems)
                .Include(r => r.TestResults)
                .Include(r => r.Documents)
                .Include(r => r.Certificates)
                .Include(r => r.AuditEntries)
                .Include(r => r.CrossReferences)
                .Include(r => r.TestingLabs)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();

            if (record.IsLocked)
            {
                TempData["Error"] = "This record is locked and cannot be edited.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Finding 3: Rejected records allow document uploads but not core field editing
            if (record.Status == ReleaseStatus.Rejected)
                TempData["WorkflowError"] = "This record has been Rejected. Core fields cannot be modified — you may upload supporting documents before closing.";

            var products = await _context.Product
                .Where(p => !p.IsDeleted && p.Status == OpenQMS.Models.Product.ProductStatus.Approved)
                .OrderBy(p => p.Name)
                .Select(p => p.Name)
                .ToListAsync();
            ViewBag.ProductsList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(products);
            await PopulateSitesViewBag();

            return View(record);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> Edit(int id, [Bind(
            "Id,RecordNumber,ReleaseType,ProductName,ProductType,BrandName,InnName,DosageForm,Strength,ArtgReference," +
            "InciName,PresentationForm,DeviceTypeCategory,ModelVersion,StandardScientificName,RawMaterialTypeCategory,Grade," +
            "BatchNumber,BatchSize,PackSize,ManufacturingDate,ExpiryDate,RetestDate," +
            "ManufacturingSite,ManufacturingSiteGmpRef,PackagingSite,PackagingSiteGmpRef," +
            "ReleasingCompany,ReleasingCompanyLicenceRef,FinalReleaseParty,TechnicalAgreementRef," +
            "SpecificationReference,StorageConditions,DistributionConditions," +
            "OpenDeviationsNotes,LimitationsNotes,Notes,CreatedDate,CreatedBy,LinkedProductId"
        )] ReleaseRecord record, string[]? labNames, string[]? labRefs)
        {
            if (id != record.Id) return NotFound();

            var existing = await _context.ReleaseRecords.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
            if (existing == null) return NotFound();

            if (existing.IsLocked || existing.Status == ReleaseStatus.Rejected)
            {
                TempData["Error"] = "This record cannot be edited in its current state.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    record.LastModifiedDate = DateTime.UtcNow;
                    record.LastModifiedBy = CurrentUser;

                    // Field-level change tracking for the audit log (existing = old, record = new)
                    var diff = new List<string>();
                    AuditDiffHelper.TrackEnum(diff, "Release Type",               existing.ReleaseType,               record.ReleaseType);
                    AuditDiffHelper.Track(diff,     "Product Name",               existing.ProductName,               record.ProductName);
                    AuditDiffHelper.TrackEnum(diff, "Product Type",               existing.ProductType,               record.ProductType);
                    AuditDiffHelper.Track(diff,     "Brand Name",                 existing.BrandName,                 record.BrandName);
                    AuditDiffHelper.Track(diff,     "INN Name",                   existing.InnName,                   record.InnName);
                    AuditDiffHelper.Track(diff,     "Dosage Form",                existing.DosageForm,                record.DosageForm);
                    AuditDiffHelper.Track(diff,     "Strength",                   existing.Strength,                  record.Strength);
                    AuditDiffHelper.Track(diff,     "ARTG Reference",             existing.ArtgReference,             record.ArtgReference);
                    AuditDiffHelper.Track(diff,     "INCI Name",                  existing.InciName,                  record.InciName);
                    AuditDiffHelper.Track(diff,     "Presentation Form",          existing.PresentationForm,          record.PresentationForm);
                    AuditDiffHelper.Track(diff,     "Device Type / Category",     existing.DeviceTypeCategory,        record.DeviceTypeCategory);
                    AuditDiffHelper.Track(diff,     "Model / Version",            existing.ModelVersion,              record.ModelVersion);
                    AuditDiffHelper.Track(diff,     "Scientific Name",            existing.StandardScientificName,    record.StandardScientificName);
                    AuditDiffHelper.Track(diff,     "Raw Material Type / Cat.",   existing.RawMaterialTypeCategory,   record.RawMaterialTypeCategory);
                    AuditDiffHelper.Track(diff,     "Grade",                      existing.Grade,                     record.Grade);
                    AuditDiffHelper.Track(diff,     "Batch Number",               existing.BatchNumber,               record.BatchNumber);
                    AuditDiffHelper.Track(diff,     "Batch Size",                 existing.BatchSize,                 record.BatchSize);
                    AuditDiffHelper.Track(diff,     "Pack Size",                  existing.PackSize,                  record.PackSize);
                    AuditDiffHelper.TrackDate(diff, "Manufacturing Date",         existing.ManufacturingDate,         record.ManufacturingDate);
                    AuditDiffHelper.TrackDate(diff, "Expiry Date",                existing.ExpiryDate,                record.ExpiryDate);
                    AuditDiffHelper.TrackDate(diff, "Retest Date",                existing.RetestDate,                record.RetestDate);
                    AuditDiffHelper.Track(diff,     "Manufacturing Site",         existing.ManufacturingSite,         record.ManufacturingSite);
                    AuditDiffHelper.Track(diff,     "Manufacturing Site GMP Ref", existing.ManufacturingSiteGmpRef,   record.ManufacturingSiteGmpRef);
                    AuditDiffHelper.Track(diff,     "Packaging Site",             existing.PackagingSite,             record.PackagingSite);
                    AuditDiffHelper.Track(diff,     "Packaging Site GMP Ref",     existing.PackagingSiteGmpRef,       record.PackagingSiteGmpRef);
                    AuditDiffHelper.Track(diff,     "Releasing Company",          existing.ReleasingCompany,          record.ReleasingCompany);
                    AuditDiffHelper.Track(diff,     "Releasing Company Lic. Ref", existing.ReleasingCompanyLicenceRef, record.ReleasingCompanyLicenceRef);
                    AuditDiffHelper.Track(diff,     "Final Release Party",        existing.FinalReleaseParty,         record.FinalReleaseParty);
                    AuditDiffHelper.Track(diff,     "Technical Agreement Ref",    existing.TechnicalAgreementRef,     record.TechnicalAgreementRef);
                    AuditDiffHelper.Track(diff,     "Specification Reference",    existing.SpecificationReference,    record.SpecificationReference);
                    AuditDiffHelper.Track<int>(diff,"Linked Product (Id)",        existing.LinkedProductId,           record.LinkedProductId);
                    AuditDiffHelper.Track(diff,     "Storage Conditions",         existing.StorageConditions,         record.StorageConditions);
                    AuditDiffHelper.Track(diff,     "Distribution Conditions",    existing.DistributionConditions,    record.DistributionConditions);
                    AuditDiffHelper.Track(diff,     "Open Deviations Notes",      existing.OpenDeviationsNotes,       record.OpenDeviationsNotes);
                    AuditDiffHelper.Track(diff,     "Limitations Notes",          existing.LimitationsNotes,          record.LimitationsNotes);
                    AuditDiffHelper.Track(diff,     "Notes",                      existing.Notes,                     record.Notes);

                    // These fields are workflow/system-managed and excluded from [Bind]; since
                    // _context.Update writes every column, restore them from the DB so a field
                    // edit never wipes hold/certification/rejection state or resets status/lock.
                    record.Status                  = existing.Status;
                    record.IsLocked                = existing.IsLocked;
                    record.HoldReason              = existing.HoldReason;
                    record.HoldDate                = existing.HoldDate;
                    record.HoldBy                  = existing.HoldBy;
                    record.APCertificationStatement = existing.APCertificationStatement;
                    record.CertificationDate       = existing.CertificationDate;
                    record.CertifiedBy             = existing.CertifiedBy;
                    record.APFullNameQualification = existing.APFullNameQualification;
                    record.ReleaseRegisterRef      = existing.ReleaseRegisterRef;
                    record.RejectionReason         = existing.RejectionReason;
                    record.RejectionDate           = existing.RejectionDate;
                    record.RejectedBy              = existing.RejectedBy;
                    record.CreatedDate             = existing.CreatedDate;
                    record.CreatedBy               = existing.CreatedBy;
                    _context.Update(record);
                    if (diff.Count > 0) AddAudit(record.Id, "Edited", null, null, AuditDiffHelper.Build(diff));

                    // Update testing labs only when core fields are editable (Draft status)
                    if (existing.Status == ReleaseStatus.Draft)
                    {
                        var oldLabs = _context.ReleaseTestingLabs.Where(t => t.ReleaseRecordId == id);
                        _context.ReleaseTestingLabs.RemoveRange(oldLabs);
                        if (labNames != null)
                        {
                            for (int i = 0; i < labNames.Length; i++)
                            {
                                if (!string.IsNullOrWhiteSpace(labNames[i]))
                                {
                                    _context.ReleaseTestingLabs.Add(new ReleaseTestingLab
                                    {
                                        ReleaseRecordId = id,
                                        LaboratoryName  = labNames[i].Trim(),
                                        GmpLicenceRef   = (labRefs != null && i < labRefs.Length) ? labRefs[i]?.Trim() : null
                                    });
                                }
                            }
                        }
                    }

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!await _context.ReleaseRecords.AnyAsync(r => r.Id == id))
                        return NotFound();
                    throw;
                }

                TempData["Success"] = "Record updated.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Reload collections for re-display
            record.ChecklistItems  = await _context.ReleaseChecklistItems.Where(c => c.ReleaseRecordId == id).ToListAsync();
            record.TestResults     = await _context.ReleaseTestResults.Where(t => t.ReleaseRecordId == id).ToListAsync();
            record.Documents       = await _context.ReleaseDocuments.Where(d => d.ReleaseRecordId == id).ToListAsync();
            record.Certificates    = await _context.ReleaseCertificates.Where(c => c.ReleaseRecordId == id).ToListAsync();
            record.AuditEntries    = await _context.ReleaseAuditEntries.Where(a => a.ReleaseRecordId == id).OrderByDescending(a => a.Timestamp).ToListAsync();
            record.CrossReferences = await _context.ReleaseCrossReferences.Where(x => x.ReleaseRecordId == id).ToListAsync();
            record.TestingLabs     = await _context.ReleaseTestingLabs.Where(t => t.ReleaseRecordId == id).ToListAsync();
            var products2 = await _context.Product
                .Where(p => !p.IsDeleted && p.Status == OpenQMS.Models.Product.ProductStatus.Approved)
                .OrderBy(p => p.Name).Select(p => p.Name).ToListAsync();
            ViewBag.ProductsList = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(products2);
            await PopulateSitesViewBag();
            return View(record);
        }

        // ── Workflow Transitions ─────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> SubmitForReview(int id, string? comments, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.UnderReview, CurrentUser, comments);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record submitted for review."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> SubmitToAP(int id, string? comments, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.PendingAPDecision, CurrentUser, comments);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record submitted to Authorised Person."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> PlaceOnHold(int id, string? reason, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.OnHold, CurrentUser, reason);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record placed on hold."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Finding 8: targetState allows lifting hold directly to Draft
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,AuthorisedPerson")]
        public async Task<IActionResult> LiftHold(int id, string? comments, string? targetState, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var target = targetState == "Draft" ? ReleaseStatus.Draft : ReleaseStatus.UnderReview;
            var successMsg = target == ReleaseStatus.Draft
                ? "Hold lifted. Record returned to Draft for rework."
                : "Hold lifted. Record returned to Under Review.";
            var result = await _workflow.TransitionAsync(id, target, CurrentUser, comments);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success ? successMsg : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,AuthorisedPerson")]
        public async Task<IActionResult> Certify(int id, string? statement, string? pwd, string? apFullName, string email)
        {
            if (!await VerifyCredentials(email, pwd ?? string.Empty))
            {
                TempData["WorkflowError"] = "Invalid credentials. Certification was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var record = await _context.ReleaseRecords.FindAsync(id);
            if (record == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(statement))
                record.APCertificationStatement = statement;

            var result = await _workflow.TransitionAsync(id, ReleaseStatus.Certified, CurrentUser, null, apFullName);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? $"Batch certified. Record {record.RecordNumber} is now Certified."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,AuthorisedPerson")]
        public async Task<IActionResult> Reject(int id, string? reason, string? pwd, string email)
        {
            if (!await VerifyCredentials(email, pwd ?? string.Empty))
            {
                TempData["WorkflowError"] = "Invalid credentials. Rejection was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.Rejected, CurrentUser, reason);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record rejected."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> ReturnForReview(int id, string? comments, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.Draft, CurrentUser, comments);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record returned to Draft."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,AuthorisedPerson")]
        public async Task<IActionResult> ReturnToUnderReview(int id, string? comments, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.UnderReview, CurrentUser, comments);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record returned for review."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,AuthorisedPerson,QualityManager")]
        public async Task<IActionResult> ConfirmRelease(int id, string? comments, string? releaseRegisterRef, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Set the release reference on the tracked entity without saving yet —
            // TransitionAsync's transaction will save it atomically with the transition,
            // avoiding a separate UPDATE on the locked Certified record (SQLite trigger).
            if (!string.IsNullOrWhiteSpace(releaseRegisterRef))
            {
                var refRecord = await _context.ReleaseRecords.FindAsync(id);
                if (refRecord != null)
                    refRecord.ReleaseRegisterRef = releaseRegisterRef;
            }

            var result = await _workflow.TransitionAsync(id, ReleaseStatus.Released, CurrentUser, comments);
            if (!result.Success)
            {
                TempData["WorkflowError"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }
            // Auto-close immediately — Released is recorded in the audit trail for future ERP integration
            var closeResult = await _workflow.TransitionAsync(id, ReleaseStatus.Closed, CurrentUser, "Automatically closed on release.");
            TempData[closeResult.Success ? "Success" : "WorkflowError"] = closeResult.Success
                ? "Transfer to saleable stock confirmed. Record released and closed."
                : $"Released but auto-close failed: {closeResult.Error}";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson")]
        public async Task<IActionResult> Close(int id, string? comments, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.Closed, CurrentUser, comments);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record closed."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser")]
        public async Task<IActionResult> Cancel(int id, string? reason, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.Cancelled, CurrentUser, reason);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Record cancelled."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // Finding 5: AP can override a rejection and return the record to Under Review
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,AuthorisedPerson")]
        public async Task<IActionResult> ReturnFromRejection(int id, string? comments, string email, string pwd)
        {
            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }
            if (string.IsNullOrWhiteSpace(comments))
            {
                TempData["WorkflowError"] = "A reason is required to override a rejection.";
                return RedirectToAction(nameof(Details), new { id });
            }
            var result = await _workflow.TransitionAsync(id, ReleaseStatus.UnderReview, CurrentUser, comments);
            TempData[result.Success ? "Success" : "WorkflowError"] = result.Success
                ? "Rejection overridden. Record returned to Under Review."
                : result.Error;
            return RedirectToAction(nameof(Details), new { id });
        }

        // ── Checklist ────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> UpdateChecklistItem(int itemId, ChecklistItemStatus status, string? comment)
        {
            try
            {
                var item = await _context.ReleaseChecklistItems
                    .FirstOrDefaultAsync(i => i.Id == itemId);

                if (item == null) return Json(new { success = false, error = "Checklist item not found." });

                var record = await _context.ReleaseRecords
                    .AsNoTracking()
                    .Select(r => new { r.Id, r.IsLocked, r.Status })
                    .FirstOrDefaultAsync(r => r.Id == item.ReleaseRecordId);

                if (record == null) return Json(new { success = false, error = "Release record not found." });
                if (record.IsLocked || record.Status == ReleaseStatus.Rejected || record.Status == ReleaseStatus.PendingAPDecision)
                    return Json(new { success = false, error = "This record is locked and cannot be modified." });

                item.Status = status;
                item.Comment = comment;
                item.ReviewedBy = CurrentUser;
                item.ReviewedDate = DateTime.UtcNow;

                var snippet = (item.ItemText?.Length ?? 0) > 60
                    ? item.ItemText!.Substring(0, 60) + "..."
                    : (item.ItemText ?? "(no text)");
                AddAudit(item.ReleaseRecordId, "Checklist Item Updated", null, null,
                    $"Item '{snippet}' set to {status}");
                await _context.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // ── Test Results ─────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> AddTestResult(int releaseRecordId,
            string testParameter, string? testMethod, string acceptanceCriterion,
            string? result, TestResultStatus status, string? comment)
        {
            var record = await _context.ReleaseRecords.FindAsync(releaseRecordId);
            if (record == null) return NotFound();
            if (record.IsLocked || record.Status == ReleaseStatus.Rejected)
            {
                TempData["Error"] = "Test results cannot be added in the current record state.";
                TempData["ActiveTab"] = "tabTests";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            var maxOrder = await _context.ReleaseTestResults
                .Where(t => t.ReleaseRecordId == releaseRecordId)
                .MaxAsync(t => (int?)t.DisplayOrder) ?? 0;

            var tr = new ReleaseTestResult
            {
                ReleaseRecordId     = releaseRecordId,
                TestParameter       = testParameter,
                TestMethod          = testMethod,
                AcceptanceCriterion = acceptanceCriterion,
                Result              = result,
                Status              = status,
                Comment             = comment,
                DisplayOrder        = maxOrder + 1,
                EnteredBy           = CurrentUser,
                EnteredDate         = DateTime.UtcNow
            };

            _context.ReleaseTestResults.Add(tr);
            AddAudit(releaseRecordId, "Test Result Added", null, null, $"Parameter: {testParameter}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Test result added.";
            TempData["ActiveTab"] = "tabTests";
            return RedirectToAction(nameof(Details), new { id = releaseRecordId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> UpdateTestResult(int resultId, string? testParameter,
            string? testMethod, string? acceptanceCriterion, string? result,
            TestResultStatus status, string? comment)
        {
            var tr = await _context.ReleaseTestResults.FindAsync(resultId);
            if (tr == null) return NotFound();

            var parentRecord = await _context.ReleaseRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == tr.ReleaseRecordId);
            if (tr.IsLocked || parentRecord?.IsLocked == true || parentRecord?.Status == ReleaseStatus.Rejected)
                return Json(new { success = false, error = "Record cannot be modified in its current state." });

            if (!string.IsNullOrWhiteSpace(testParameter)) tr.TestParameter = testParameter;
            tr.TestMethod          = testMethod;
            tr.AcceptanceCriterion = acceptanceCriterion ?? tr.AcceptanceCriterion;
            tr.Result              = result;
            tr.Status              = status;
            tr.Comment             = comment;
            tr.EnteredBy           = CurrentUser;
            tr.EnteredDate         = DateTime.UtcNow;

            AddAudit(tr.ReleaseRecordId, "Test Result Updated", null, null, $"Parameter: {tr.TestParameter}");
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> DeleteTestResult(int resultId)
        {
            var tr = await _context.ReleaseTestResults.FindAsync(resultId);
            if (tr == null) return NotFound();

            var parentForDelete = await _context.ReleaseRecords.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == tr.ReleaseRecordId);
            if (tr.IsLocked || parentForDelete?.IsLocked == true || parentForDelete?.Status == ReleaseStatus.Rejected)
            {
                TempData["Error"] = "Test results cannot be deleted in the current record state.";
                TempData["ActiveTab"] = "tabTests";
                return RedirectToAction(nameof(Details), new { id = tr.ReleaseRecordId });
            }

            var recordId = tr.ReleaseRecordId;
            _context.ReleaseTestResults.Remove(tr);
            AddAudit(recordId, "Test Result Deleted", null, null, $"Parameter: {tr.TestParameter}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Test result deleted.";
            TempData["ActiveTab"] = "tabTests";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> ImportSpecsFromProduct(int releaseRecordId)
        {
            var record = await _context.ReleaseRecords
                .Include(r => r.TestResults)
                .FirstOrDefaultAsync(r => r.Id == releaseRecordId);

            if (record == null) return NotFound();
            if (record.IsLocked || record.Status == ReleaseStatus.Rejected)
            {
                TempData["Error"] = "Specifications cannot be imported in the current record state.";
                TempData["ActiveTab"] = "tabTests";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }
            if (!record.LinkedProductId.HasValue)
            {
                TempData["Error"] = "No linked product found. Link a product first before importing specifications.";
                TempData["ActiveTab"] = "tabTests";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            var specs = await _context.ProductSpecLines
                .Where(s => s.ProductId == record.LinkedProductId.Value)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();

            var existingParams = (record.TestResults ?? new List<ReleaseTestResult>())
                .Select(t => t.TestParameter.Trim().ToLowerInvariant())
                .ToHashSet();

            int maxOrder = (record.TestResults?.Any() == true)
                ? record.TestResults.Max(t => t.DisplayOrder) : 0;

            int added = 0;
            foreach (var spec in specs.Where(s => !existingParams.Contains(s.TestParameter.Trim().ToLowerInvariant())))
            {
                _context.ReleaseTestResults.Add(new ReleaseTestResult
                {
                    ReleaseRecordId     = releaseRecordId,
                    TestParameter       = spec.TestParameter,
                    TestMethod          = spec.Method,
                    AcceptanceCriterion = spec.AcceptanceCriterion,
                    Comment             = spec.Comments,
                    Status              = TestResultStatus.Pending,
                    DisplayOrder        = ++maxOrder,
                    EnteredBy           = CurrentUser,
                    EnteredDate         = DateTime.UtcNow
                });
                added++;
            }

            if (added > 0)
            {
                AddAudit(releaseRecordId, "Specs Imported", null, null,
                    $"{added} specification line(s) imported from product master");
                await _context.SaveChangesAsync();
                TempData["Success"] = $"{added} specification line(s) imported from product master.";
            }
            else
            {
                TempData["Success"] = "All specification lines already present in Analytical Results.";
            }

            TempData["ActiveTab"] = "tabTests";
            return RedirectToAction(nameof(Details), new { id = releaseRecordId });
        }

        // ── Documents ────────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> UploadDocument(int releaseRecordId, IFormFile? file,
            string? category, string? description)
        {
            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "Please select a file to upload.";
                TempData["ActiveTab"] = "tabDocs";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            var uploadRecord = await _context.ReleaseRecords.FindAsync(releaseRecordId);
            if (uploadRecord == null) return NotFound();
            if (uploadRecord.IsLocked)
            {
                TempData["Error"] = "Cannot upload documents to a locked record.";
                TempData["ActiveTab"] = "tabDocs";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            // Validate extension
            var allowed = UploadRules.AllowedExtensions;
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowed.Contains(ext))
            {
                TempData["Error"] = "File type not permitted.";
                TempData["ActiveTab"] = "tabDocs";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            // Validate size (25 MB)
            if (file.Length > UploadRules.MaxBytes)
            {
                TempData["Error"] = "File exceeds 25 MB limit.";
                TempData["ActiveTab"] = "tabDocs";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            var uploadDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "release", "documents");
            Directory.CreateDirectory(uploadDir);
            var safeFileName = $"{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadDir, safeFileName);

            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var fileBytes = ms.ToArray();
            var fileHash = OpenQMS.Services.FileHashHelper.ComputeSha256(fileBytes);
            await System.IO.File.WriteAllBytesAsync(filePath, fileBytes);

            _context.ReleaseDocuments.Add(new ReleaseDocument
            {
                ReleaseRecordId  = releaseRecordId,
                FileName         = file.FileName,
                FilePath         = $"/uploads/release/documents/{safeFileName}",
                ContentType      = file.ContentType,
                FileSizeBytes    = file.Length,
                FileHash         = fileHash,
                DocumentCategory = category,
                Description      = description,
                UploadedDate     = DateTime.UtcNow,
                UploadedBy       = CurrentUser
            });

            AddAudit(releaseRecordId, "Document Uploaded", null, null, $"File: {file.FileName} ({category})");
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Document '{file.FileName}' uploaded.";
            TempData["ActiveTab"] = "tabDocs";
            return RedirectToAction(nameof(Details), new { id = releaseRecordId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> DeleteDocument(int documentId, string? deletionReason)
        {
            var doc = await _context.ReleaseDocuments
                .Include(d => d.ReleaseRecord)
                .FirstOrDefaultAsync(d => d.Id == documentId);

            if (doc == null) return NotFound();
            if (doc.ReleaseRecord?.IsLocked == true)
            {
                TempData["Error"] = "Cannot delete documents from a locked record.";
                TempData["ActiveTab"] = "tabDocs";
                return RedirectToAction(nameof(Details), new { id = doc.ReleaseRecordId });
            }

            var recordId = doc.ReleaseRecordId;

            if (string.IsNullOrWhiteSpace(deletionReason))
            {
                TempData["Error"] = "A reason is required to delete a document.";
                TempData["ActiveTab"] = "tabDocs";
                return RedirectToAction(nameof(Details), new { id = recordId });
            }

            // Soft delete: the row and file are retained for traceability.
            doc.IsDeleted      = true;
            doc.DeletedBy      = _userManager.GetUserName(User) ?? "Unknown";
            doc.DeletedOn      = DateTime.UtcNow;
            doc.DeletionReason = deletionReason.Trim();
            AddAudit(recordId, "Document Deleted", null, null, $"File: {doc.FileName} — Reason: {doc.DeletionReason}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Document deleted.";
            TempData["ActiveTab"] = "tabDocs";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        public async Task<IActionResult> DownloadDocument(int documentId, bool deleted = false)
        {
            var doc = await _context.ReleaseDocuments.ForDownload(deleted).FirstOrDefaultAsync(e => e.Id == documentId);
            if (doc == null) return NotFound();

            if (string.IsNullOrEmpty(doc.FilePath)) return NotFound();

            var physicalPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
                doc.FilePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(physicalPath)) return NotFound();

            if (!await OpenQMS.Services.FileIntegrity.MatchesAsync(physicalPath, doc.FileHash))
            {
                AddAudit(doc.ReleaseRecordId, OpenQMS.Services.FileIntegrity.AuditAction, null, null, OpenQMS.Services.FileIntegrity.AuditNote(doc.FileName));
                await _context.SaveChangesAsync();
                TempData["Error"] = OpenQMS.Services.FileIntegrity.FailureMessage(doc.FileName);
                TempData["ActiveTab"] = "tabDocs";
                return RedirectToAction(nameof(Details), new { id = doc.ReleaseRecordId });
            }

            AddAudit(doc.ReleaseRecordId, (deleted ? "Deleted Document Downloaded" : "Document Downloaded"), null, null, $"File: {doc.FileName}");
            await _context.SaveChangesAsync();

            var contentType  = doc.ContentType ?? "application/octet-stream";
            var fileBytes    = await System.IO.File.ReadAllBytesAsync(physicalPath);
            var wmCtx = new WatermarkContext(
                WatermarkKind.Attachment, null, "Release",
                doc.ReleaseRecordId.ToString(), null,
                _userManager.GetUserName(User) ?? "Unknown", DateTime.UtcNow) { Deleted = deleted };
            var watermarked = WatermarkService.Apply(
                fileBytes, Path.GetExtension(doc.FileName), wmCtx);
            return File(watermarked, contentType, doc.FileName);
        }

        // ── Certificates ─────────────────────────────────────────────────────

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,AuthorisedPerson")]
        public async Task<IActionResult> IssueCertificate(int releaseRecordId, CertificateType type, string? email, string? pwd, string? statement)
        {
            if (!await VerifyCredentials(email ?? string.Empty, pwd ?? string.Empty))
            {
                TempData["Error"] = "Invalid credentials. Certificate was not issued.";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            var record = await _context.ReleaseRecords
                .Include(r => r.TestResults)
                .Include(r => r.ChecklistItems)
                .Include(r => r.Certificates)
                .Include(r => r.TestingLabs)
                .FirstOrDefaultAsync(r => r.Id == releaseRecordId);

            if (record == null) return NotFound();

            if (record.Status != ReleaseStatus.Certified && record.Status != ReleaseStatus.Released)
            {
                TempData["Error"] = "Certificates can only be issued for Certified or Released records.";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            // Check for existing issued certificate of same type — supersede it
            var existing = record.Certificates?
                .Where(c => c.CertificateType == type && c.Status == CertificateStatus.Issued)
                .ToList();

            if (existing != null)
                foreach (var old in existing)
                    old.Status = CertificateStatus.Superseded;

            // Resolve issuer full name from their profile
            var issuer = await _userManager.GetUserAsync(User);
            var issuerFullName = issuer != null
                ? ($"{issuer.FirstName} {issuer.LastName}".Trim() +
                   (string.IsNullOrWhiteSpace(issuer.APQualification) ? "" : $", {issuer.APQualification}"))
                : CurrentUser;

            var certNumber = await GenerateCertificateNumber(type);
            var snapshot   = BuildSnapshot(record, type, certNumber);
            snapshot.CertificateIssuanceStatement = string.IsNullOrWhiteSpace(statement) ? null : statement.Trim();
            snapshot.IssuedByFullName             = issuerFullName;

            var cert = new ReleaseCertificate
            {
                ReleaseRecordId         = releaseRecordId,
                CertificateNumber       = certNumber,
                CertificateType         = type,
                Status                  = CertificateStatus.Issued,
                ContentSnapshot         = JsonSerializer.Serialize(snapshot),
                Version                 = (existing?.Count ?? 0) + 1,
                IssuedDate              = DateTime.UtcNow,
                IssuedBy                = CurrentUser,
                APFullNameQualification = issuerFullName
            };

            _context.ReleaseCertificates.Add(cert);
            AddAudit(releaseRecordId, "Certificate Issued", null, null,
                $"Certificate {certNumber} ({type}) issued by {CurrentUser}");
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Certificate {certNumber} issued.";
            return RedirectToAction(nameof(ViewCertificate), new { certificateId = cert.Id });
        }

        public async Task<IActionResult> ViewCertificate(int certificateId)
        {
            var cert = await _context.ReleaseCertificates
                .Include(c => c.ReleaseRecord)
                .FirstOrDefaultAsync(c => c.Id == certificateId);

            if (cert == null) return NotFound();

            CertificateSnapshot? snapshot = null;
            if (!string.IsNullOrEmpty(cert.ContentSnapshot))
                snapshot = JsonSerializer.Deserialize<CertificateSnapshot>(cert.ContentSnapshot);

            ViewBag.Certificate = cert;
            return View(snapshot);
        }

        // ── Release Register ─────────────────────────────────────────────────

        public async Task<IActionResult> Register()
        {
            var certs = await _context.ReleaseCertificates
                .Include(c => c.ReleaseRecord)
                .OrderByDescending(c => c.IssuedDate)
                .ToListAsync();
            return View(certs);
        }

        public async Task<IActionResult> ExportRegister()
        {
            var certs = await _context.ReleaseCertificates
                .Include(c => c.ReleaseRecord)
                .Where(c => c.Status == CertificateStatus.Issued)
                .OrderByDescending(c => c.IssuedDate)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Release Register");

            // Headers
            ws.Cell(1, 1).Value = "Certificate No.";
            ws.Cell(1, 2).Value = "Record No.";
            ws.Cell(1, 3).Value = "Product";
            ws.Cell(1, 4).Value = "Batch No.";
            ws.Cell(1, 5).Value = "Release Type";
            ws.Cell(1, 6).Value = "Cert Type";
            ws.Cell(1, 7).Value = "AP Name";
            ws.Cell(1, 8).Value = "Certification Date";
            ws.Cell(1, 9).Value = "Issue Date";
            ws.Cell(1, 10).Value = "Status";

            var row = 2;
            foreach (var c in certs)
            {
                ws.Cell(row, 1).Value = c.CertificateNumber;
                ws.Cell(row, 2).Value = c.ReleaseRecord?.RecordNumber;
                ws.Cell(row, 3).Value = c.ReleaseRecord?.ProductName;
                ws.Cell(row, 4).Value = c.ReleaseRecord?.BatchNumber;
                ws.Cell(row, 5).Value = c.ReleaseRecord?.ReleaseType.ToString();
                ws.Cell(row, 6).Value = c.CertificateType.ToString();
                ws.Cell(row, 7).Value = c.APFullNameQualification;
                ws.Cell(row, 8).Value = c.ReleaseRecord?.CertificationDate.ToSite()?.ToString("dd-MMM-yyyy");
                ws.Cell(row, 9).Value = c.IssuedDate.ToSite().ToString("dd-MMM-yyyy");
                ws.Cell(row, 10).Value = c.Status.ToString();
                row++;
            }

            ws.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"ReleaseRegister_{SiteTime.Today:yyyyMMdd}.xlsx");
        }

        // ── Detail Export ────────────────────────────────────────────────────

        public async Task<IActionResult> ExportDetailPdf(int id)
        {
            var record = await _context.ReleaseRecords
                .Include(r => r.ChecklistItems)
                .Include(r => r.TestResults)
                .Include(r => r.Certificates)
                .Include(r => r.TestingLabs)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (record == null) return NotFound();

            var fields = new List<(string, string?)>
            {
                ("Record Number",              record.RecordNumber),
                ("Release Type",               record.ReleaseType.ToString()),
                ("Status",                     ReleaseStatusLabel(record.Status)),
                ("Product Name",               record.ProductName),
                ("Product Type",              record.ProductType?.ToString()),
                ("INN / Generic",              record.InnName),
                ("INCI Name / Ingredient Ref.", record.InciName),
                ("Standard / Scientific Name", record.StandardScientificName),
                ("Brand Name",                 record.BrandName),
                ("Dosage Form",                record.DosageForm),
                ("Presentation / Form",        record.PresentationForm),
                ("Device Type / Category",     record.DeviceTypeCategory),
                ("Model / Version",            record.ModelVersion),
                ("Material Type / Category",   record.RawMaterialTypeCategory),
                ("Grade",                      record.Grade),
                ("Strength / Presentation",    record.Strength),
                ("ARTG Reference",             record.ArtgReference),
                ("Batch Number",               record.BatchNumber),
                ("Batch Size",                 record.BatchSize),
                ("Pack Size",                  record.PackSize),
                ("Manufacturing Date",         record.ManufacturingDate?.ToString("dd-MMM-yyyy")),
                ("Expiry Date",                record.ExpiryDate?.ToString("dd-MMM-yyyy")),
                ("Retest Date",                record.RetestDate?.ToString("dd-MMM-yyyy")),
                ("Manufacturing Site",         record.ManufacturingSite),
                ("Manufacturing GMP Ref.",     record.ManufacturingSiteGmpRef),
                ("Packaging Site",             record.PackagingSite),
                ("Packaging GMP Ref.",         record.PackagingSiteGmpRef),
                ("Testing Laboratories",       string.Join("; ", (record.TestingLabs ?? new List<ReleaseTestingLab>()).Select(t => string.IsNullOrEmpty(t.GmpLicenceRef) ? t.LaboratoryName : $"{t.LaboratoryName} [{t.GmpLicenceRef}]"))),
                ("Releasing Company",          record.ReleasingCompany),
                ("TGA Licence / Clearance",    record.ReleasingCompanyLicenceRef),
                ("Final Release Party",        record.FinalReleaseParty),
                ("Technical Agreement Ref.",   record.TechnicalAgreementRef),
                ("Specification Ref.",         record.SpecificationReference),
                ("Storage Conditions",         record.StorageConditions),
                ("Distribution Conditions",    record.DistributionConditions),
                ("Open Deviations / Exceptions", record.OpenDeviationsNotes),
                ("Limitations / Conditions",   record.LimitationsNotes),
                ("Notes",                      record.Notes),
                ("AP Name & Qualification",    record.APFullNameQualification),
                ("AP Certification Statement", record.APCertificationStatement),
                ("Certification Date",         record.CertificationDate.ToSite()?.ToString("dd-MMM-yyyy")),
                ("Release Reference Number",   record.ReleaseRegisterRef),
                ("Created By",                 record.CreatedBy),
                ("Created Date",               record.CreatedDate.ToSite().ToString("dd-MMM-yyyy HH:mm")),
                ("Last Modified By",           record.LastModifiedBy),
                ("Last Modified Date",         record.LastModifiedDate.ToSite()?.ToString("dd-MMM-yyyy HH:mm")),
            };

            var tables = new List<(string, string[], IEnumerable<string[]>)>
            {
                ("Batch Release Checklist",
                 new[] { "Category", "Item", "Status", "Comment" },
                 (record.ChecklistItems ?? new List<ReleaseChecklistItem>())
                     .OrderBy(c => c.DisplayOrder)
                     .Select(c => new[] { c.Category ?? "", c.ItemText, c.Status.ToString(), c.Comment ?? "" })),
            };

            tables.Add(("Analytical Results",
                new[] { "Test Parameter", "Method", "Acceptance Criterion", "Result", "Status" },
                (record.TestResults ?? new List<ReleaseTestResult>())
                    .OrderBy(t => t.DisplayOrder)
                    .Select(t => new[] { t.TestParameter, t.TestMethod ?? "", t.AcceptanceCriterion ?? "", t.Result ?? "", t.Status.ToString() })));

            tables.Add(("Certificates",
                new[] { "Certificate No.", "Type", "AP Name", "Issued Date", "Status" },
                (record.Certificates ?? new List<ReleaseCertificate>())
                    .Select(c => new[] { c.CertificateNumber, c.CertificateType.ToString(), c.APFullNameQualification ?? "", c.IssuedDate.ToSite().ToString("dd-MMM-yyyy"), c.Status.ToString() })));

            AddAudit(record.Id, "Exported (PDF)", null, null);
            await _context.SaveChangesAsync();

            var _s = await _context.SystemSettings.FindAsync(1);
            string? _logo = PdfReportService.ResolveLogoPath(_s?.LogoPath);
            var result = PdfReportService.Generate(
                "Release Record — Batch Release Report",
                record.RecordNumber,
                ReleaseStatusLabel(record.Status),
                fields,
                tables,
                orgName: _s?.OrganisationName,
                orgAddress: _s?.OrganisationAddress,
                logoPhysicalPath: _logo);

            return File(result, "application/pdf", $"Release-{record.RecordNumber}.pdf");
        }

        public async Task<IActionResult> ExportDetailCsv(int id)
        {
            var record = await _context.ReleaseRecords
                .Include(r => r.TestingLabs)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (record == null) return NotFound();

            AddAudit(record.Id, "Exported (CSV)", null, null);
            await _context.SaveChangesAsync();

            var builder = new System.Text.StringBuilder();
            var labsSummary = string.Join(" | ", (record.TestingLabs ?? new List<ReleaseTestingLab>())
                .Select(t => string.IsNullOrEmpty(t.GmpLicenceRef) ? t.LaboratoryName : $"{t.LaboratoryName} [{t.GmpLicenceRef}]"));

            builder.AppendLine(
                "Record Number,Release Type,Status,Product Name,Product Type,INN/Generic,INCI Name,Standard/Scientific Name," +
                "Brand Name,Dosage Form,Presentation/Form,Device Type/Category,Model/Version," +
                "Material Type/Category,Grade,Strength/Presentation,ARTG Reference," +
                "Batch Number,Batch Size,Pack Size," +
                "Manufacturing Date,Expiry Date,Retest Date,Manufacturing Site,Manufacturing GMP Ref," +
                "Packaging Site,Packaging GMP Ref,Testing Laboratories,Releasing Company,TGA Licence/Clearance Ref," +
                "Final Release Party,Technical Agreement,Specification Ref,Storage Conditions," +
                "Distribution Conditions,Open Deviations Notes,Limitations Notes,Notes," +
                "AP Name & Qualification,Certification Date,Release Reference Number," +
                "Created By,Created Date");

            builder.AppendLine(string.Join(",",
                CsvSafe(record.RecordNumber),
                record.ReleaseType.ToString(),
                ReleaseStatusLabel(record.Status),
                CsvSafe(record.ProductName),
                CsvSafe(record.ProductType?.ToString()),
                CsvSafe(record.InnName),
                CsvSafe(record.InciName),
                CsvSafe(record.StandardScientificName),
                CsvSafe(record.BrandName),
                CsvSafe(record.DosageForm),
                CsvSafe(record.PresentationForm),
                CsvSafe(record.DeviceTypeCategory),
                CsvSafe(record.ModelVersion),
                CsvSafe(record.RawMaterialTypeCategory),
                CsvSafe(record.Grade),
                CsvSafe(record.Strength),
                CsvSafe(record.ArtgReference),
                CsvSafe(record.BatchNumber),
                CsvSafe(record.BatchSize),
                CsvSafe(record.PackSize),
                record.ManufacturingDate?.ToString("dd-MMM-yyyy") ?? "",
                record.ExpiryDate?.ToString("dd-MMM-yyyy") ?? "",
                record.RetestDate?.ToString("dd-MMM-yyyy") ?? "",
                CsvSafe(record.ManufacturingSite),
                CsvSafe(record.ManufacturingSiteGmpRef),
                CsvSafe(record.PackagingSite),
                CsvSafe(record.PackagingSiteGmpRef),
                CsvSafe(labsSummary),
                CsvSafe(record.ReleasingCompany),
                CsvSafe(record.ReleasingCompanyLicenceRef),
                CsvSafe(record.FinalReleaseParty),
                CsvSafe(record.TechnicalAgreementRef),
                CsvSafe(record.SpecificationReference),
                CsvSafe(record.StorageConditions),
                CsvSafe(record.DistributionConditions),
                CsvSafe(record.OpenDeviationsNotes),
                CsvSafe(record.LimitationsNotes),
                CsvSafe(record.Notes),
                CsvSafe(record.APFullNameQualification),
                record.CertificationDate.ToSite()?.ToString("dd-MMM-yyyy") ?? "",
                CsvSafe(record.ReleaseRegisterRef),
                CsvSafe(record.CreatedBy),
                record.CreatedDate.ToSite().ToString("dd-MMM-yyyy HH:mm")));

            return File(
                System.Text.Encoding.UTF8.GetBytes(builder.ToString()),
                "text/csv",
                $"Release-{record.RecordNumber}.csv");
        }

        private static string CsvSafe(string? value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            if (value[0] is '=' or '+' or '-' or '@' or '\t' or '\r')
                return $"\"{value}\"";
            return value;
        }

        // ── Cross-References ─────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetLinkedRecords(string moduleType)
        {
            object records = moduleType switch
            {
                "Deviation"  => await _context.Deviation!
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.Id)
                    .Select(d => new { id = d.Id, recordNumber = d.DeviationId, title = d.Title })
                    .ToListAsync(),
                "Complaint"  => await _context.Complaint
                    .Where(c => !c.IsDeleted)
                    .OrderByDescending(c => c.Id)
                    .Select(c => new { id = c.Id, recordNumber = c.ComplaintId, title = c.Title })
                    .ToListAsync(),
                "CAPA"       => await _context.Capa!
                    .Where(c => !c.IsDeleted)
                    .OrderByDescending(c => c.Id)
                    .Select(c => new { id = c.Id, recordNumber = c.CapaId, title = c.Title })
                    .ToListAsync(),
                "Change"     => await _context.Change!
                    .Where(c => !c.IsDeleted)
                    .OrderByDescending(c => c.Id)
                    .Select(c => new { id = c.Id, recordNumber = c.ChangeId, title = c.Title })
                    .ToListAsync(),
                "Audit"      => await _context.Audit
                    .Where(a => !a.IsDeleted)
                    .OrderByDescending(a => a.Id)
                    .Select(a => new { id = a.Id, recordNumber = a.AuditId, title = a.Title })
                    .ToListAsync(),
                "Risk"       => await _context.Risk
                    .Where(r => !r.IsDeleted)
                    .OrderByDescending(r => r.Id)
                    .Select(r => new { id = r.Id, recordNumber = r.RiskId, title = r.Title })
                    .ToListAsync(),
                "Supplier"   => await _context.Supplier
                    .Where(s => !s.IsDeleted)
                    .OrderBy(s => s.SupplierCode)
                    .Select(s => new { id = s.Id, recordNumber = s.SupplierCode, title = s.LegalName })
                    .ToListAsync(),
                "LicCert"    => await _context.LicenceCertificationRecords
                    .Where(l => !l.IsDeleted)
                    .OrderByDescending(l => l.Id)
                    .Select(l => new { id = l.Id, recordNumber = l.LCNumber, title = l.HolderName })
                    .ToListAsync(),
                "Document"   => await _context.AppDocument
                    .Where(d => !d.IsDeleted)
                    .OrderByDescending(d => d.Id)
                    .Select(d => new { id = d.Id, recordNumber = d.DocumentId, title = d.Title })
                    .ToListAsync(),
                _ => new List<object>()
            };
            return Json(records);
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> AddCrossReference(int releaseRecordId,
            string moduleType, int? linkedRecordId, string? linkedRecordNumber,
            string? linkedRecordTitle, string? notes)
        {
            var xrefRecord = await _context.ReleaseRecords.FindAsync(releaseRecordId);
            if (xrefRecord == null) return NotFound();
            if (xrefRecord.IsLocked)
            {
                TempData["Error"] = "Cannot modify cross-references on a locked record.";
                TempData["ActiveTab"] = "tabLinked";
                return RedirectToAction(nameof(Details), new { id = releaseRecordId });
            }

            _context.ReleaseCrossReferences.Add(new ReleaseCrossReference
            {
                ReleaseRecordId    = releaseRecordId,
                ModuleType         = moduleType,
                LinkedRecordId     = linkedRecordId,
                LinkedRecordNumber = linkedRecordNumber,
                LinkedRecordTitle  = linkedRecordTitle,
                Notes              = notes
            });
            AddAudit(releaseRecordId, "Cross-Reference Added", null, null,
                $"{moduleType}: {linkedRecordNumber} — {linkedRecordTitle}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cross-reference added.";
            TempData["ActiveTab"] = "tabLinked";
            return RedirectToAction(nameof(Details), new { id = releaseRecordId });
        }

        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator,QualityManager,QualityUser,AuthorisedPerson,OperationsUser")]
        public async Task<IActionResult> DeleteCrossReference(int id)
        {
            var xref = await _context.ReleaseCrossReferences
                .Include(x => x.ReleaseRecord)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (xref == null) return NotFound();
            if (xref.ReleaseRecord?.IsLocked == true)
            {
                TempData["Error"] = "Cannot modify cross-references on a locked record.";
                TempData["ActiveTab"] = "tabLinked";
                return RedirectToAction(nameof(Details), new { id = xref.ReleaseRecordId });
            }
            var recordId = xref.ReleaseRecordId;
            _context.ReleaseCrossReferences.Remove(xref);
            AddAudit(recordId, "Cross-Reference Removed", null, null,
                $"{xref.ModuleType}: {xref.LinkedRecordNumber}");
            await _context.SaveChangesAsync();

            TempData["Success"] = "Cross-reference removed.";
            TempData["ActiveTab"] = "tabLinked";
            return RedirectToAction(nameof(Details), new { id = recordId });
        }

        // ── Private helpers ──────────────────────────────────────────────────

        private CertificateSnapshot BuildSnapshot(ReleaseRecord record, CertificateType type, string certNumber)
        {
            return new CertificateSnapshot
            {
                RecordNumber              = record.RecordNumber,
                CertificateNumber         = certNumber,
                CertificateType           = type,
                ReleaseType               = record.ReleaseType,
                ProductName               = record.ProductName,
                ProductType               = record.ProductType,
                BrandName                 = record.BrandName,
                InnName                   = record.InnName,
                InciName                  = record.InciName,
                DosageForm                = record.DosageForm,
                Strength                  = record.Strength,
                PresentationForm          = record.PresentationForm,
                ArtgReference             = record.ArtgReference,
                DeviceTypeCategory        = record.DeviceTypeCategory,
                ModelVersion              = record.ModelVersion,
                StandardScientificName    = record.StandardScientificName,
                RawMaterialTypeCategory   = record.RawMaterialTypeCategory,
                Grade                     = record.Grade,
                BatchNumber               = record.BatchNumber,
                BatchSize                 = record.BatchSize,
                PackSize                  = record.PackSize,
                ManufacturingDate         = record.ManufacturingDate,
                ExpiryDate                = record.ExpiryDate,
                RetestDate                = record.RetestDate,
                ManufacturingSite         = record.ManufacturingSite,
                ManufacturingSiteGmpRef   = record.ManufacturingSiteGmpRef,
                PackagingSite             = record.PackagingSite,
                PackagingSiteGmpRef       = record.PackagingSiteGmpRef,
                TestingLabs               = (record.TestingLabs ?? new List<ReleaseTestingLab>())
                                                .Select(t => new TestingLabSnapshot { LaboratoryName = t.LaboratoryName, GmpLicenceRef = t.GmpLicenceRef })
                                                .ToList(),
                ReleasingCompany          = record.ReleasingCompany,
                ReleasingCompanyLicenceRef = record.ReleasingCompanyLicenceRef,
                FinalReleaseParty         = record.FinalReleaseParty,
                TechnicalAgreementRef     = record.TechnicalAgreementRef,
                StorageConditions         = record.StorageConditions,
                OpenDeviationsNotes       = record.OpenDeviationsNotes,
                LimitationsNotes          = record.LimitationsNotes,
                APName                    = record.APFullNameQualification,
                APCertificationStatement  = record.APCertificationStatement ?? DefaultAPStatement,
                CertificationDate         = record.CertificationDate,
                IssuedDate                = DateTime.UtcNow,
                ReleaseRegisterRef        = record.ReleaseRegisterRef,
                TestResults = record.TestResults?
                    .OrderBy(t => t.DisplayOrder)
                    .Select(t => new TestResultSnapshot
                    {
                        TestParameter       = t.TestParameter,
                        TestMethod          = t.TestMethod,
                        AcceptanceCriterion = t.AcceptanceCriterion,
                        Result              = t.Result,
                        Status              = t.Status
                    }).ToList() ?? new(),
                ChecklistItems = record.ChecklistItems?
                    .Where(c => c.Status == ChecklistItemStatus.Satisfactory ||
                                c.Status == ChecklistItemStatus.SatisfactoryWithComment)
                    .Select(c => new ChecklistItemSnapshot
                    {
                        Category = c.Category ?? string.Empty,
                        ItemText = c.ItemText,
                        Status   = c.Status,
                        Comment  = c.Comment
                    }).ToList() ?? new()
            };
        }

        [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = "Administrator")]
        public async Task<IActionResult> AdminReopen(int id, string reason, string? deviationRef, string email, string pwd)
        {
            var record = await _context.ReleaseRecords
                .Include(r => r.Certificates)
                .FirstOrDefaultAsync(r => r.Id == id);
            if (record == null) return NotFound();

            var reopenableStatuses = new[] { ReleaseStatus.Closed, ReleaseStatus.Released, ReleaseStatus.Cancelled };
            if (!reopenableStatuses.Contains(record.Status))
            {
                TempData["WorkflowError"] = "This record is not in a state that can be administratively reopened.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (!await VerifyCredentials(email, pwd))
            {
                TempData["WorkflowError"] = "Invalid credentials. Action was not applied.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                TempData["WorkflowError"] = "A justification reason is required.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var targetStatus = record.Status == ReleaseStatus.Cancelled
                ? ReleaseStatus.Draft
                : ReleaseStatus.Certified;

            var fromStatus = record.Status;
            var certsCancelled = false;

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                // SQLite trigger bypass: unlock before status change
                if (record.IsLocked)
                {
                    record.IsLocked = false;
                    await _context.SaveChangesAsync();
                }

                // Withdraw all issued certificates
                if (record.Certificates != null)
                {
                    foreach (var cert in record.Certificates.Where(c => c.Status == CertificateStatus.Issued))
                    {
                        cert.Status = CertificateStatus.Withdrawn;
                        certsCancelled = true;
                    }
                }

                // Unlock test results
                var testResults = await _context.ReleaseTestResults
                    .Where(t => t.ReleaseRecordId == id).ToListAsync();
                foreach (var tr in testResults)
                    tr.IsLocked = false;

                record.Status = targetStatus;
                record.LastModifiedDate = DateTime.UtcNow;
                record.LastModifiedBy = CurrentUser;

                var details = $"Justification: {reason}";
                if (!string.IsNullOrWhiteSpace(deviationRef))
                    details += $" | Linked Deviation: {deviationRef}";

                _context.ReleaseAuditEntries.Add(new ReleaseAuditEntry
                {
                    ReleaseRecordId = id,
                    Action = "Administrative Reopen",
                    FromStatus = fromStatus.ToString(),
                    ToStatus = targetStatus.ToString(),
                    Details = details,
                    PerformedBy = CurrentUser,
                    Timestamp = DateTime.UtcNow
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }

            var certNote = certsCancelled ? " All issued certificates have been withdrawn." : "";
            TempData["Success"] = $"Record has been administratively reopened to {targetStatus} stage.{certNote}";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
