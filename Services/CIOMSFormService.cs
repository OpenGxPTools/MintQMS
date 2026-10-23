using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    public class CIOMSFormService
    {
        private static readonly DeviceRgb HeaderColor = new(0, 70, 127);
        private static readonly DeviceRgb RowShade    = new(245, 245, 245);
        private static readonly DeviceRgb WatermarkColor = new(210, 210, 210);
        private static readonly DeviceRgb BorderColor    = new(200, 200, 200);

        public byte[] GenerateCIOMS(
            AdverseEvent ae,
            AEReporter? reporter,
            AEPatient? patient,
            AESuspectProduct? product)
        {
            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf = new PdfDocument(writer);
            var doc = new Document(pdf, PageSize.A4);
            doc.SetMargins(28, 28, 36, 28);

            var boldFont   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // ── Title ───────────────────────────────────────────────────────────
            doc.Add(new Paragraph("CIOMS-I FORM — INDIVIDUAL CASE SAFETY REPORT")
                .SetFont(boldFont).SetFontSize(12)
                .SetFontColor(HeaderColor)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(2));

            doc.Add(new Paragraph(
                    $"Case: {ae.AENumber}   |   Report Date: {ae.ReportDate.ToSite():dd MMM yyyy}   |   Status: {ae.Status}")
                .SetFont(normalFont).SetFontSize(8.5f)
                .SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(8));

            // ── A: Patient ──────────────────────────────────────────────────────
            AddSectionHeader(doc, boldFont, "A. PATIENT INFORMATION");

            var patTable = NewTable(new float[] { 1, 1, 1, 1 });
            AddLH(patTable, boldFont, "Patient Initials / ID");
            AddLH(patTable, boldFont, "Date of Birth / Age");
            AddLH(patTable, boldFont, "Sex");
            AddLH(patTable, boldFont, "Weight / Height");
            AddLD(patTable, normalFont, patient?.PatientIdentifier ?? "");
            AddLD(patTable, normalFont, patient?.DateOfBirth?.ToString("dd MMM yyyy") ??
                (patient?.Age.HasValue == true ? $"{patient.Age} {patient.AgeUnit}" : ""));
            AddLD(patTable, normalFont, patient?.Sex?.ToString() ?? "");
            AddLD(patTable, normalFont,
                string.Join(" / ", new[]
                {
                    patient?.WeightKg.HasValue == true ? $"{patient.WeightKg} kg" : "",
                    patient?.HeightCm.HasValue == true ? $"{patient.HeightCm} cm" : ""
                }.Where(s => s.Length > 0)));
            doc.Add(patTable);

            AddMultilineRow(doc, boldFont, normalFont,
                "Medical History / Concurrent Conditions:", patient?.MedicalHistory);
            AddMultilineRow(doc, boldFont, normalFont,
                "Concomitant Medications:", patient?.ConcomitantMedications);

            // ── B: Suspect Drug / Device ────────────────────────────────────────
            AddSectionHeader(doc, boldFont, "B. SUSPECT DRUG / DEVICE");

            var prod1 = NewTable(new float[] { 2, 1, 1 });
            AddLH(prod1, boldFont, "Product Name");
            AddLH(prod1, boldFont, "ARTG Number");
            AddLH(prod1, boldFont, "Batch / Lot");
            AddLD(prod1, normalFont, product?.ProductName ?? "");
            AddLD(prod1, normalFont, product?.ARTGNumber ?? "");
            AddLD(prod1, normalFont, product?.BatchNumber ?? "");
            doc.Add(prod1);

            var prod2 = NewTable(new float[] { 1, 1, 1, 1 });
            AddLH(prod2, boldFont, "Route");
            AddLH(prod2, boldFont, "Dose / Frequency");
            AddLH(prod2, boldFont, "Indication");
            AddLH(prod2, boldFont, "Action Taken");
            AddLD(prod2, normalFont, product?.RouteOfAdministration ?? "");
            AddLD(prod2, normalFont, string.Join(" ", new[]
            {
                product?.Dose, product?.DoseUnit, product?.DoseFrequency
            }.Where(s => !string.IsNullOrWhiteSpace(s))));
            AddLD(prod2, normalFont, product?.Indication ?? "");
            AddLD(prod2, normalFont, product?.ActionTaken?.ToString() ?? "");
            doc.Add(prod2);

            var prod3 = NewTable(new float[] { 1, 1, 1, 1 });
            AddLH(prod3, boldFont, "Treatment Start");
            AddLH(prod3, boldFont, "Treatment End");
            AddLH(prod3, boldFont, "Dechallenge");
            AddLH(prod3, boldFont, "Rechallenge");
            AddLD(prod3, normalFont, product?.TreatmentStartDate?.ToString("dd MMM yyyy") ?? "");
            AddLD(prod3, normalFont, product?.TreatmentEndDate?.ToString("dd MMM yyyy") ?? "");
            AddLD(prod3, normalFont, product?.DechallengeResult?.ToString() ?? "");
            AddLD(prod3, normalFont, product?.RechallengeResult?.ToString() ?? "");
            doc.Add(prod3);

            // ── C: Adverse Reaction / Event ─────────────────────────────────────
            AddSectionHeader(doc, boldFont, "C. ADVERSE REACTION / EVENT");

            var rx1 = NewTable(new float[] { 1, 1, 1 });
            AddLH(rx1, boldFont, "AE Start Date");
            AddLH(rx1, boldFont, "AE End Date");
            AddLH(rx1, boldFont, "Outcome");
            AddLD(rx1, normalFont, ae.AEStartDate?.ToString("dd MMM yyyy") ?? "");
            AddLD(rx1, normalFont, ae.AEEndDate?.ToString("dd MMM yyyy") ?? "");
            AddLD(rx1, normalFont, ae.Outcome?.ToString() ?? "");
            doc.Add(rx1);

            var rx2 = NewTable(new float[] { 1, 1 });
            AddLH(rx2, boldFont, "MedDRA Preferred Term");
            AddLH(rx2, boldFont, "MedDRA System Organ Class");
            AddLD(rx2, normalFont, ae.MedDRAPT ?? "");
            AddLD(rx2, normalFont, ae.MedDRASOC ?? "");
            doc.Add(rx2);

            AddMultilineRow(doc, boldFont, normalFont, "Adverse Event Description:", ae.AEDescription);

            var seriousnessText = ae.IsSerious
                ? "SERIOUS — Criteria: " + ae.SeriousnessCriteria.ToString()
                : "Non-serious";
            doc.Add(new Paragraph()
                .Add(new Text("Seriousness: ").SetFont(boldFont).SetFontSize(9))
                .Add(new Text(seriousnessText).SetFont(normalFont).SetFontSize(9))
                .SetMarginBottom(4));

            var rx3 = NewTable(new float[] { 1, 1, 1 });
            AddLH(rx3, boldFont, "Causality");
            AddLH(rx3, boldFont, "Severity");
            AddLH(rx3, boldFont, "Expectedness");
            AddLD(rx3, normalFont, ae.Causality?.ToString() ?? "");
            AddLD(rx3, normalFont, ae.Severity?.ToString() ?? "");
            AddLD(rx3, normalFont, ae.Expectedness?.ToString() ?? "");
            doc.Add(rx3);

            // ── D: Reporter ─────────────────────────────────────────────────────
            AddSectionHeader(doc, boldFont, "D. REPORTER");

            var rep1 = NewTable(new float[] { 2, 1, 1 });
            AddLH(rep1, boldFont, "Reporter Name / Initials");
            AddLH(rep1, boldFont, "Type");
            AddLH(rep1, boldFont, "Qualification");
            AddLD(rep1, normalFont, reporter?.ReporterName ?? "");
            AddLD(rep1, normalFont, reporter?.Type?.ToString() ?? "");
            AddLD(rep1, normalFont, reporter?.Qualification?.ToString() ?? "");
            doc.Add(rep1);

            var rep2 = NewTable(new float[] { 1, 1, 1 });
            AddLH(rep2, boldFont, "Institution");
            AddLH(rep2, boldFont, "Country");
            AddLH(rep2, boldFont, "Report Date to Company");
            AddLD(rep2, normalFont, reporter?.Institution ?? "");
            AddLD(rep2, normalFont, reporter?.Country ?? "");
            AddLD(rep2, normalFont, reporter?.ReportDate.ToSite()?.ToString("dd MMM yyyy") ?? "");
            doc.Add(rep2);

            // ── E: Regulatory ───────────────────────────────────────────────────
            AddSectionHeader(doc, boldFont, "E. REGULATORY / COMPANY");

            var reg1 = NewTable(new float[] { 1, 1, 1 });
            AddLH(reg1, boldFont, "Report Type");
            AddLH(reg1, boldFont, "Reportable to TGA");
            AddLH(reg1, boldFont, "TGA Deadline");
            AddLD(reg1, normalFont, ae.ReportType.ToString());
            AddLD(reg1, normalFont,
                ae.IsReportable == null ? "Pending Assessment" :
                ae.IsReportable == true ? "Yes" : "No");
            AddLD(reg1, normalFont, ae.TGADeadlineDate?.ToString("dd MMM yyyy") ?? "N/A");
            doc.Add(reg1);

            if (!string.IsNullOrWhiteSpace(ae.MedicalNarrative))
                AddMultilineRow(doc, boldFont, normalFont, "Medical Narrative / Assessment:", ae.MedicalNarrative);

            // ── Footer ──────────────────────────────────────────────────────────
            doc.Add(new Paragraph(
                    $"Generated: {SiteTime.Now:dd MMM yyyy HH:mm} {SiteTime.Label}   |   Created by: {ae.CreatedBy}   |   " +
                    "CONFIDENTIAL — Not for distribution")
                .SetFont(normalFont).SetFontSize(7)
                .SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(10));

            // ── Watermark ───────────────────────────────────────────────────────
            var wmFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
            {
                var page   = pdf.GetPage(i);
                var canvas = new PdfCanvas(page.NewContentStreamBefore(), page.GetResources(), pdf);
                canvas.SaveState();
                canvas.SetFillColor(WatermarkColor);
                canvas.BeginText();
                canvas.SetFontAndSize(wmFont, 55f);
                canvas.SetTextMatrix(
                    (float)Math.Cos(Math.PI / 4), (float)Math.Sin(Math.PI / 4),
                    -(float)Math.Sin(Math.PI / 4), (float)Math.Cos(Math.PI / 4),
                    80, 280);
                canvas.ShowText("CONFIDENTIAL");
                canvas.EndText();
                canvas.RestoreState();
            }

            doc.Close();
            return ms.ToArray();
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static Table NewTable(float[] cols) =>
            new Table(cols).UseAllAvailableWidth().SetMarginBottom(0);

        private static void AddSectionHeader(Document doc, PdfFont boldFont, string title)
        {
            doc.Add(new Paragraph(title)
                .SetFont(boldFont).SetFontSize(9)
                .SetFontColor(ColorConstants.WHITE)
                .SetBackgroundColor(HeaderColor)
                .SetPaddingLeft(5).SetPaddingTop(3).SetPaddingBottom(3)
                .SetMarginTop(8).SetMarginBottom(0));
        }

        private static void AddLH(Table table, PdfFont boldFont, string label)
        {
            table.AddHeaderCell(new Cell()
                .Add(new Paragraph(label).SetFont(boldFont).SetFontSize(7.5f))
                .SetBackgroundColor(RowShade)
                .SetPadding(3)
                .SetBorder(new SolidBorder(BorderColor, 0.5f)));
        }

        private static void AddLD(Table table, PdfFont normalFont, string value)
        {
            table.AddCell(new Cell()
                .Add(new Paragraph(value.Length > 0 ? value : " ").SetFont(normalFont).SetFontSize(8.5f))
                .SetPadding(3)
                .SetBorder(new SolidBorder(BorderColor, 0.5f)));
        }

        private static void AddMultilineRow(
            Document doc, PdfFont boldFont, PdfFont normalFont,
            string label, string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            doc.Add(new Paragraph()
                .Add(new Text(label + "  ").SetFont(boldFont).SetFontSize(8.5f))
                .Add(new Text(value).SetFont(normalFont).SetFontSize(8.5f))
                .SetMarginBottom(4));
        }
    }
}
