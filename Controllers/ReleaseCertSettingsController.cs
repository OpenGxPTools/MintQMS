using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenQMS.Authorization;
using OpenQMS.Data;
using OpenQMS.Models.Release;

namespace OpenQMS.Controllers
{
    [Authorize(Roles = "Administrator,QualityManager,Auditor")]
    public class ReleaseCertSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReleaseCertSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            await EnsureSeededAsync();
            var settings = await _context.ReleaseCertificationSettings
                .OrderBy(s => s.ReleaseType)
                .ToListAsync();
            ViewBag.CanEdit = User.IsInRole(Constants.AdministratorsRole);
            ViewBag.AuditLogs = await _context.ReleaseCertSettingAuditLogs
                .OrderByDescending(a => a.ChangedOn)
                .Take(100)
                .ToListAsync();
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Administrator")]
        public async Task<IActionResult> Edit(int id, string statement)
        {
            var setting = await _context.ReleaseCertificationSettings.FindAsync(id);
            if (setting == null) return NotFound();

            var oldStatement = setting.Statement ?? "";
            var newStatement = statement?.Trim() ?? "";

            // Field-level change tracking for the certification statement
            if (oldStatement != newStatement)
            {
                _context.ReleaseCertSettingAuditLogs.Add(new ReleaseCertSettingAuditLog
                {
                    ReleaseCertificationSettingId = setting.Id,
                    ReleaseType        = setting.ReleaseType,
                    ProductGroupLabel  = setting.ProductGroupLabel,
                    FieldName          = "Statement",
                    OldValue           = oldStatement,
                    NewValue           = newStatement,
                    ChangedBy          = User.Identity?.Name ?? "System",
                    ChangedOn          = DateTime.UtcNow
                });
            }

            setting.Statement        = newStatement;
            setting.LastModifiedBy   = User.Identity?.Name;
            setting.LastModifiedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Statement for \"{setting.ProductGroupLabel}\" under {GetReleaseTypeLabel(setting.ReleaseType)} updated.";
            return RedirectToAction(nameof(Index));
        }

        internal static string GetReleaseTypeLabel(ReleaseType rt) => rt switch
        {
            ReleaseType.ReleaseForSupply            => "Release For Supply",
            ReleaseType.ReleaseUnderQuarantine      => "Release Under Quarantine",
            ReleaseType.ReleaseForFurtherProcessing => "Release For Further Processing",
            ReleaseType.ReleaseToCustomer           => "Release To Customer",
            ReleaseType.QualityOversight            => "Quality Oversight",
            _                                        => rt.ToString()
        };

        private async Task EnsureSeededAsync()
        {
            if (await _context.ReleaseCertificationSettings.AnyAsync()) return;
            _context.ReleaseCertificationSettings.AddRange(BuildDefaultSettings());
            await _context.SaveChangesAsync();
        }

        public static List<ReleaseCertificationSetting> BuildDefaultSettings() => new()
        {
            // ── Release For Supply ──────────────────────────────────────────────────────
            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "PharmaBio",
                ProductGroupLabel = "Pharmaceutical / Biological",
                Statement = "I hereby certify that this batch has been manufactured, tested, and reviewed in accordance with the conditions of the Marketing Authorisation and all applicable GMP requirements. All manufacturing records, quality control results, deviation status, and regulatory obligations have been reviewed and found satisfactory. This batch is approved for release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "MedDevice",
                ProductGroupLabel = "Medical Device",
                Statement = "I hereby certify that this batch/lot has been manufactured, inspected, and tested in accordance with the approved Device Master Record and the requirements of ISO 13485 and all applicable regulatory frameworks. The Device History Record, quality control inspection results, non-conformance status, and regulatory obligations have been reviewed and found compliant. This lot is approved for release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "ConsumerHealth",
                ProductGroupLabel = "Consumer Health",
                Statement = "I hereby certify that this batch has been manufactured, tested, and reviewed in accordance with all applicable TGA requirements, GMP obligations, and the approved product specification. Manufacturing records, quality control results, and regulatory compliance have been reviewed and found satisfactory. This batch is approved for release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "Cosmetic",
                ProductGroupLabel = "Cosmetic",
                Statement = "I hereby certify that this batch has been manufactured and tested in compliance with applicable cosmetic regulations and the approved product specification. Safety assessment documentation, manufacturing records, and quality control results have been reviewed and found satisfactory. This batch is approved for release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "RawMatAPI",
                ProductGroupLabel = "Raw Material — API",
                Statement = "I hereby certify that this batch has been manufactured and tested in compliance with ICH Q7 Good Manufacturing Practice for Active Pharmaceutical Ingredients and the approved material specification. All quality control results, manufacturing records, and Certificate of Analysis have been reviewed and found satisfactory. This batch is approved for release for use in manufacturing." },

            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "RawMatOther",
                ProductGroupLabel = "Raw Material — Excipient / Cosmetic Ingredient / Packaging",
                Statement = "I hereby certify that this batch has been reviewed against the approved material specification and applicable compendial or regulatory requirements. Certificate of Analysis, supplier qualification records, and incoming quality control testing have been reviewed and found satisfactory. This batch is approved for release for use." },

            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "MedComponent",
                ProductGroupLabel = "Raw Material — Device Component (Part / Subcomponent)",
                Statement = "I hereby certify that this batch/lot has been inspected and reviewed in accordance with the approved component specification, Device Master Record requirements, and applicable ISO 13485 requirements. Incoming inspection records, Certificate of Conformance, and supplier qualification status have been reviewed and found satisfactory. This lot is approved for release for use in device manufacturing." },

            new() { ReleaseType = ReleaseType.ReleaseForSupply, ProductGroup = "Consumable",
                ProductGroupLabel = "Raw Material — Consumable",
                Statement = "I hereby certify that this batch/shipment has been reviewed against the approved specification and supplier qualification requirements. All relevant receiving inspection and quality records have been reviewed and found satisfactory. This batch is approved for use." },

            // ── Release Under Quarantine ────────────────────────────────────────────────
            new() { ReleaseType = ReleaseType.ReleaseUnderQuarantine, ProductGroup = "PharmaBio",
                ProductGroupLabel = "Pharmaceutical / Biological",
                Statement = "I hereby certify that this batch is conditionally approved for release under quarantine. All available manufacturing records and quality control data reviewed to date are satisfactory. Certain testing or documentation remains outstanding and must be completed and confirmed satisfactory before transfer to fully released stock. Physical quarantine controls must remain in place until all release conditions are met." },

            new() { ReleaseType = ReleaseType.ReleaseUnderQuarantine, ProductGroup = "MedDevice",
                ProductGroupLabel = "Medical Device",
                Statement = "I hereby certify that this lot is conditionally approved for release under quarantine. Available manufacturing and inspection records have been reviewed and are satisfactory. Outstanding activities must be completed and verified before this lot may be transferred to released stock. Quarantine controls must remain in place until all conditions are resolved." },

            new() { ReleaseType = ReleaseType.ReleaseUnderQuarantine, ProductGroup = "RawMatAPI",
                ProductGroupLabel = "Raw Material — API",
                Statement = "I hereby certify that this batch is conditionally approved for receipt under quarantine. Available documentation and incoming inspection results are satisfactory. Outstanding quality control testing or documentation must be completed and found satisfactory before this material may be used in manufacturing." },

            new() { ReleaseType = ReleaseType.ReleaseUnderQuarantine, ProductGroup = "RawMatOther",
                ProductGroupLabel = "Raw Material — Excipient / Cosmetic Ingredient / Packaging",
                Statement = "I hereby certify that this batch is conditionally approved for receipt under quarantine. Available documentation and incoming inspection results are satisfactory. Outstanding quality control testing or documentation must be completed and found satisfactory before this material may be used in manufacturing." },

            // ── Release For Further Processing ──────────────────────────────────────────
            new() { ReleaseType = ReleaseType.ReleaseForFurtherProcessing, ProductGroup = "PharmaBio",
                ProductGroupLabel = "Pharmaceutical / Biological",
                Statement = "I hereby certify that this batch meets the approved intermediate or bulk specification and is approved for release for further processing. All in-process controls, manufacturing records, and quality control results applicable to this stage of manufacture have been reviewed and found satisfactory. This approval is limited to further processing only and does not constitute a finished product release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForFurtherProcessing, ProductGroup = "MedDevice",
                ProductGroupLabel = "Medical Device",
                Statement = "I hereby certify that this lot meets the approved intermediate assembly or sub-assembly specification and is approved for release for further processing. Manufacturing and inspection records for this stage have been reviewed and found compliant with the Device Master Record. This approval is limited to further assembly or processing and does not constitute a final device release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForFurtherProcessing, ProductGroup = "ConsumerHealth",
                ProductGroupLabel = "Consumer Health",
                Statement = "I hereby certify that this batch meets the approved intermediate specification and is approved for further processing into a finished product. Manufacturing and quality control records for this stage have been reviewed and found satisfactory. This is not a finished product release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForFurtherProcessing, ProductGroup = "Cosmetic",
                ProductGroupLabel = "Cosmetic",
                Statement = "I hereby certify that this batch meets the approved intermediate specification and is approved for further processing into a finished product. Manufacturing and quality control records for this stage have been reviewed and found satisfactory. This is not a finished product release for supply." },

            new() { ReleaseType = ReleaseType.ReleaseForFurtherProcessing, ProductGroup = "RawMatAPI",
                ProductGroupLabel = "Raw Material — API",
                Statement = "I hereby certify that this batch complies with the approved bulk or intermediate specification and is approved for further processing. Manufacturing records and quality control results have been reviewed and found satisfactory. This batch is not approved for final use or supply without subsequent processing and release." },

            // ── Release To Customer ─────────────────────────────────────────────────────
            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "PharmaBio",
                ProductGroupLabel = "Pharmaceutical / Biological",
                Statement = "I hereby certify that this batch conforms to the approved product specification and all applicable Marketing Authorisation, GMP, and contractual quality requirements. This Certificate of Conformance is issued on the basis of reviewed manufacturing records, quality control results, and regulatory compliance documentation. This document does not constitute a release for supply under regulatory batch release requirements." },

            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "MedDevice",
                ProductGroupLabel = "Medical Device",
                Statement = "I hereby certify that this batch/lot conforms to the approved product specification, Device Master Record requirements, and all applicable regulatory and contractual quality requirements. This Certificate of Conformance is issued on the basis of the Device History Record, quality control inspection results, and regulatory compliance records. This document does not constitute a release for supply under regulatory batch release requirements." },

            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "ConsumerHealth",
                ProductGroupLabel = "Consumer Health",
                Statement = "I hereby certify that this batch conforms to the approved product specification and all applicable regulatory and contractual quality requirements. This Certificate of Conformance is issued on the basis of reviewed manufacturing records and quality control results. This document does not constitute a release for supply under regulatory batch release requirements." },

            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "Cosmetic",
                ProductGroupLabel = "Cosmetic",
                Statement = "I hereby certify that this batch conforms to the approved product specification and all applicable regulatory and contractual quality requirements. This Certificate of Conformance is issued on the basis of reviewed manufacturing records and quality control results. This document does not constitute a release for supply under regulatory batch release requirements." },

            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "RawMatAPI",
                ProductGroupLabel = "Raw Material — API",
                Statement = "I hereby certify that this batch conforms to the agreed material specification and all applicable quality and contractual requirements. This Certificate of Conformance is issued on the basis of reviewed manufacturing or testing records, Certificate of Analysis data, and supplier qualification status. This document does not constitute a release for supply under regulatory batch release requirements." },

            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "RawMatOther",
                ProductGroupLabel = "Raw Material — Excipient / Cosmetic Ingredient / Packaging",
                Statement = "I hereby certify that this batch conforms to the agreed material specification and all applicable quality and contractual requirements. This Certificate of Conformance is issued on the basis of reviewed manufacturing or testing records, Certificate of Analysis data, and supplier qualification status. This document does not constitute a release for supply under regulatory batch release requirements." },

            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "MedComponent",
                ProductGroupLabel = "Raw Material — Device Component (Part / Subcomponent)",
                Statement = "I hereby certify that this batch/lot conforms to the agreed component specification and all applicable quality, regulatory, and contractual requirements. This Certificate of Conformance is issued on the basis of incoming inspection records, Certificate of Conformance documentation, and supplier qualification status. This document does not constitute a release for supply under regulatory batch release requirements." },

            new() { ReleaseType = ReleaseType.ReleaseToCustomer, ProductGroup = "Consumable",
                ProductGroupLabel = "Raw Material — Consumable",
                Statement = "I hereby certify that this batch conforms to the agreed specification and supplier quality requirements. This Certificate of Conformance is issued on the basis of receiving inspection records and quality documentation reviewed. This document does not constitute a release for supply under regulatory batch release requirements." },

            // ── Quality Oversight ───────────────────────────────────────────────────────
            new() { ReleaseType = ReleaseType.QualityOversight, ProductGroup = "All",
                ProductGroupLabel = "All Product Types",
                Statement = "I hereby confirm that quality oversight activities have been completed and reviewed in accordance with applicable GMP, regulatory, and internal quality requirements. All relevant records, documentation, and quality metrics reviewed were found compliant. No unresolved quality issues were identified at the time of this review. This Quality Oversight Report does not constitute a batch release certification for supply." },
        };
    }
}
