using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Utils;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using OpenQMS.Models;

namespace OpenQMS.Services
{
    public static class PdfReportService
    {
        private static readonly DeviceRgb HeaderColor    = new(0, 70, 127);
        private static readonly DeviceRgb TableHeaderBg  = new(70, 100, 140);
        private static readonly DeviceRgb RowShade       = new(245, 245, 245);
        private static readonly DeviceRgb BorderColor    = new(200, 200, 200);

        /// <summary>
        /// Resolves a logo URL path (e.g. "/uploads/logo/abc.png") to a physical file path under wwwroot.
        /// Returns null if the path is empty or the file does not exist.
        /// </summary>
        public static string? ResolveLogoPath(string? logoUrlPath)
        {
            if (string.IsNullOrEmpty(logoUrlPath)) return null;
            var physical = System.IO.Path.Combine(
                Directory.GetCurrentDirectory(), "wwwroot",
                logoUrlPath.TrimStart('/').Replace('/', System.IO.Path.DirectorySeparatorChar));
            return System.IO.File.Exists(physical) ? physical : null;
        }

        /// <summary>
        /// Generates a detail PDF for any QMS module.
        /// </summary>
        /// <param name="moduleTitle">e.g. "Change Detail Report"</param>
        /// <param name="recordId">e.g. "CHG-2026-0001"</param>
        /// <param name="status">Current record status string</param>
        /// <param name="fields">Ordered label/value pairs for the main fields section</param>
        /// <param name="tables">Optional related-records tables: (title, column headers, data rows)</param>
        public static byte[] Generate(
            string moduleTitle,
            string recordId,
            string status,
            IEnumerable<(string Label, string? Value)> fields,
            IEnumerable<(string Title, string[] Headers, IEnumerable<string[]> Rows)>? tables = null,
            string? orgName = null,
            string? orgAddress = null,
            string? logoPhysicalPath = null)
        {
            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf    = new PdfDocument(writer);
            var doc    = new Document(pdf, PageSize.A4);
            doc.SetMargins(36, 36, 36, 36);

            var bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // ── Organisation Header ──────────────────────────────────────────────
            if (!string.IsNullOrEmpty(orgName) || !string.IsNullOrEmpty(logoPhysicalPath))
            {
                var orgTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2 }))
                    .SetWidth(UnitValue.CreatePercentValue(100))
                    .SetMarginBottom(8);

                var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(4);
                if (!string.IsNullOrEmpty(logoPhysicalPath) && System.IO.File.Exists(logoPhysicalPath))
                {
                    try
                    {
                        var imgData = ImageDataFactory.Create(logoPhysicalPath);
                        var img = new Image(imgData).SetMaxHeight(55).SetMaxWidth(180);
                        logoCell.Add(img);
                    }
                    catch { /* unreadable logo — skip */ }
                }
                orgTable.AddCell(logoCell);

                var infoCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(4)
                    .SetTextAlignment(TextAlignment.RIGHT);
                if (!string.IsNullOrEmpty(orgName))
                    infoCell.Add(new Paragraph(orgName)
                        .SetFont(bold).SetFontSize(10)
                        .SetFontColor(HeaderColor).SetMarginBottom(2));
                if (!string.IsNullOrEmpty(orgAddress))
                {
                    // Split on newlines so multi-line addresses render correctly in PDF
                    var addrPara = new Paragraph()
                        .SetFont(normal).SetFontSize(8)
                        .SetFontColor(ColorConstants.GRAY);
                    var addrLines = orgAddress.Replace("\r\n", "\n").Split('\n');
                    for (int i = 0; i < addrLines.Length; i++)
                    {
                        if (i > 0) addrPara.Add(new iText.Layout.Element.Text("\n"));
                        addrPara.Add(addrLines[i]);
                    }
                    infoCell.Add(addrPara);
                }
                orgTable.AddCell(infoCell);

                doc.Add(orgTable);
                doc.Add(new Paragraph()
                    .SetBorderBottom(new SolidBorder(BorderColor, 0.5f))
                    .SetMarginBottom(8));
            }

            // ── Header ──────────────────────────────────────────────────────────
            doc.Add(new Paragraph(moduleTitle)
                .SetFont(bold).SetFontSize(14)
                .SetFontColor(HeaderColor)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(2));

            doc.Add(new Paragraph(
                    $"Record: {recordId}   |   Status: {status}   |   Generated: {SiteTime.Now:dd MMM yyyy}")
                .SetFont(normal).SetFontSize(8.5f)
                .SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(12));

            // ── Fields table (Label | Value) ────────────────────────────────────
            var fieldTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2 }))
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginBottom(16);

            bool shade = false;
            foreach (var (label, value) in fields)
            {
                var bg = shade ? RowShade : ColorConstants.WHITE;
                shade = !shade;

                fieldTable.AddCell(new Cell()
                    .Add(new Paragraph(label).SetFont(bold).SetFontSize(9))
                    .SetBackgroundColor(bg)
                    .SetBorder(new SolidBorder(BorderColor, 0.5f))
                    .SetPadding(4));

                fieldTable.AddCell(new Cell()
                    .Add(new Paragraph(value ?? string.Empty).SetFont(normal).SetFontSize(9))
                    .SetBackgroundColor(bg)
                    .SetBorder(new SolidBorder(BorderColor, 0.5f))
                    .SetPadding(4));
            }
            doc.Add(fieldTable);

            // ── Related-records tables ───────────────────────────────────────────
            if (tables != null)
            {
                foreach (var (title, headers, rows) in tables)
                {
                    var rowList = rows.ToList();

                    doc.Add(new Paragraph(title)
                        .SetFont(bold).SetFontSize(10)
                        .SetFontColor(ColorConstants.WHITE)
                        .SetBackgroundColor(TableHeaderBg)
                        .SetPadding(5)
                        .SetMarginBottom(0)
                        .SetMarginTop(8));

                    var t = new Table(UnitValue.CreatePercentArray(
                                Enumerable.Repeat(1f, headers.Length).ToArray()))
                        .SetWidth(UnitValue.CreatePercentValue(100))
                        .SetMarginBottom(10);

                    foreach (var h in headers)
                        t.AddHeaderCell(new Cell()
                            .Add(new Paragraph(h).SetFont(bold).SetFontSize(8.5f)
                                .SetFontColor(ColorConstants.WHITE))
                            .SetBackgroundColor(TableHeaderBg)
                            .SetBorder(new SolidBorder(BorderColor, 0.5f))
                            .SetPadding(4));

                    if (rowList.Count == 0)
                    {
                        t.AddCell(new Cell(1, headers.Length)
                            .Add(new Paragraph("No records.")
                                .SetFont(normal).SetFontSize(8.5f)
                                .SetFontColor(ColorConstants.GRAY))
                            .SetBorder(new SolidBorder(BorderColor, 0.5f))
                            .SetPadding(4));
                    }
                    else
                    {
                        shade = false;
                        foreach (var row in rowList)
                        {
                            var bg = shade ? RowShade : ColorConstants.WHITE;
                            shade = !shade;
                            foreach (var cell in row)
                                t.AddCell(new Cell()
                                    .Add(new Paragraph(cell ?? string.Empty)
                                        .SetFont(normal).SetFontSize(8.5f))
                                    .SetBackgroundColor(bg)
                                    .SetBorder(new SolidBorder(BorderColor, 0.5f))
                                    .SetPadding(4));
                        }
                    }
                    doc.Add(t);
                }
            }

            doc.Close();
            return ms.ToArray();
        }

        /// <summary>
        /// Generates a Document Control Sheet page for the given AppDocument.
        /// </summary>
        public static byte[] GenerateDocumentControlSheet(
            AppDocument document,
            string? orgName = null,
            string? orgAddress = null,
            string? logoPhysicalPath = null)
        {
            using var ms = new MemoryStream();
            var writer = new PdfWriter(ms);
            var pdf    = new PdfDocument(writer);
            var doc    = new Document(pdf, PageSize.A4);
            doc.SetMargins(36, 36, 36, 36);

            var bold   = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            var normal = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // ── Organisation Header ──────────────────────────────────────────────
            if (!string.IsNullOrEmpty(orgName) || !string.IsNullOrEmpty(logoPhysicalPath))
            {
                var orgTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2 }))
                    .SetWidth(UnitValue.CreatePercentValue(100)).SetMarginBottom(8);

                var logoCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(4);
                if (!string.IsNullOrEmpty(logoPhysicalPath) && System.IO.File.Exists(logoPhysicalPath))
                {
                    try
                    {
                        var imgData = ImageDataFactory.Create(logoPhysicalPath);
                        logoCell.Add(new Image(imgData).SetMaxHeight(55).SetMaxWidth(180));
                    }
                    catch { }
                }
                orgTable.AddCell(logoCell);

                var infoCell = new Cell().SetBorder(Border.NO_BORDER).SetPadding(4)
                    .SetTextAlignment(TextAlignment.RIGHT);
                if (!string.IsNullOrEmpty(orgName))
                    infoCell.Add(new Paragraph(orgName).SetFont(bold).SetFontSize(10)
                        .SetFontColor(HeaderColor).SetMarginBottom(2));
                if (!string.IsNullOrEmpty(orgAddress))
                {
                    var addrPara = new Paragraph().SetFont(normal).SetFontSize(8)
                        .SetFontColor(ColorConstants.GRAY);
                    var lines = orgAddress.Replace("\r\n", "\n").Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (i > 0) addrPara.Add(new iText.Layout.Element.Text("\n"));
                        addrPara.Add(lines[i]);
                    }
                    infoCell.Add(addrPara);
                }
                orgTable.AddCell(infoCell);
                doc.Add(orgTable);
                doc.Add(new Paragraph()
                    .SetBorderBottom(new SolidBorder(BorderColor, 0.5f))
                    .SetMarginBottom(8));
            }

            // ── Title ────────────────────────────────────────────────────────────
            doc.Add(new Paragraph("Document Control Sheet")
                .SetFont(bold).SetFontSize(14)
                .SetFontColor(HeaderColor)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginBottom(12));

            // ── Fields table with section headers ────────────────────────────────
            var table = new Table(UnitValue.CreatePercentArray(new float[] { 1, 2 }))
                .SetWidth(UnitValue.CreatePercentValue(100))
                .SetMarginBottom(16);

            bool shade = false;

            void AddSectionHeader(string title)
            {
                shade = false;
                table.AddCell(new Cell(1, 2)
                    .Add(new Paragraph(title).SetFont(bold).SetFontSize(9)
                        .SetFontColor(ColorConstants.WHITE))
                    .SetBackgroundColor(HeaderColor)
                    .SetBorder(new SolidBorder(BorderColor, 0.5f))
                    .SetPadding(5));
            }

            void AddRow(string label, string? value)
            {
                var bg = shade ? RowShade : ColorConstants.WHITE;
                shade = !shade;
                table.AddCell(new Cell()
                    .Add(new Paragraph(label).SetFont(bold).SetFontSize(9))
                    .SetBackgroundColor(bg)
                    .SetBorder(new SolidBorder(BorderColor, 0.5f))
                    .SetPadding(4));
                table.AddCell(new Cell()
                    .Add(new Paragraph(string.IsNullOrWhiteSpace(value) ? "—" : value)
                        .SetFont(normal).SetFontSize(9))
                    .SetBackgroundColor(bg)
                    .SetBorder(new SolidBorder(BorderColor, 0.5f))
                    .SetPadding(4));
            }

            // Document Details
            AddSectionHeader("Document Details");
            AddRow("Document ID", document.DocumentId);
            AddRow("Title", document.Title);
            if (!string.IsNullOrWhiteSpace(document.Description))
                AddRow("Description", document.Description);
            if (document.Category.HasValue)
                AddRow("Category", EnumHelper.Display(document.Category.Value));
            if (document.DocType.HasValue)
                AddRow("Document Type", EnumHelper.Display(document.DocType.Value));
            AddRow("Version", $"v{document.Version:0.00}");
            AddRow("Status", EnumHelper.Display(document.Status));

            // Workflow / Acceptance Trail
            AddSectionHeader(document.IsExternal ? "System Acceptance Trail" : "Workflow Trail");
            AddRow(document.IsExternal ? "Uploaded By"        : "Authored By",
                   document.AuthoredBy);
            AddRow(document.IsExternal ? "Uploaded On"        : "Authored On",
                   document.AuthoredOn.ToSite().ToString("dd MMM yyyy HH:mm"));
            AddRow(document.IsExternal ? "Accepted into QMS By" : "Approved By",
                   document.ApprovedBy ?? "—");
            AddRow(document.IsExternal ? "Accepted into QMS On" : "Approved On",
                   document.ApprovedOn.ToSite()?.ToString("dd MMM yyyy HH:mm") ?? "—");
            if (!string.IsNullOrEmpty(document.IssuedBy))
                AddRow("Issued By", document.IssuedBy);
            if (document.IssuedOn.HasValue)
                AddRow("Issued On", document.IssuedOn.Value.ToSite().ToString("dd MMM yyyy"));
            if (document.PeriodicReviewRequired && document.ReviewDueDate.HasValue)
                AddRow("Next Review Date", document.ReviewDueDate.Value.ToString("dd MMM yyyy"));

            // Document Provenance (external docs only)
            if (document.IsExternal)
            {
                AddSectionHeader("Document Provenance (Original)");
                AddRow("Source / Origin",    document.ExternalSource ?? "—");
                if (!string.IsNullOrEmpty(document.ExternalVersion))
                    AddRow("Original Version / Reference", document.ExternalVersion);
                AddRow("Original Author",        document.ExternalAuthorName ?? "—");
                AddRow("Originally Authored On", document.ExternalAuthoredOn?.ToString("dd MMM yyyy") ?? "—");
                AddRow("Original Approver",      document.ExternalApproverName ?? "—");
                AddRow("Originally Approved On", document.ExternalApprovedOn?.ToString("dd MMM yyyy") ?? "—");
            }

            doc.Add(table);

            // ── Footer ───────────────────────────────────────────────────────────
            doc.Add(new Paragraph(
                    $"This page was automatically generated by the Quality Management System on {SiteTime.Now:dd MMM yyyy HH:mm} {SiteTime.Label}.")
                .SetFont(normal).SetFontSize(7)
                .SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMarginTop(16));

            doc.Close();
            return ms.ToArray();
        }

        /// <summary>
        /// Appends <paramref name="pageToAppend"/> (a single-page PDF) to the end of
        /// <paramref name="originalPdf"/>. Returns the original bytes unchanged if merging fails.
        /// </summary>
        public static byte[] AppendToPdf(byte[] originalPdf, byte[] pageToAppend)
        {
            try
            {
                using var outputStream = new MemoryStream();
                using var outputDoc = new PdfDocument(new PdfWriter(outputStream));
                var merger = new PdfMerger(outputDoc);

                using var origStream = new MemoryStream(originalPdf);
                var origReader = new PdfReader(origStream);
                origReader.SetStrictnessLevel(PdfReader.StrictnessLevel.LENIENT);
                using var origDoc = new PdfDocument(origReader);
                merger.Merge(origDoc, 1, origDoc.GetNumberOfPages());
                origDoc.Close();

                using var appendStream = new MemoryStream(pageToAppend);
                using var appendDoc = new PdfDocument(new PdfReader(appendStream));
                merger.Merge(appendDoc, 1, appendDoc.GetNumberOfPages());
                appendDoc.Close();

                outputDoc.Close();
                return outputStream.ToArray();
            }
            catch
            {
                return originalPdf;
            }
        }
    }
}
