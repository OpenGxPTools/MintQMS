using System.IO.Compression;
using System.Text;
using System.Xml;
using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Extgstate;
using OpenQMS.Models;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using PdfRect      = iText.Kernel.Geom.Rectangle;
using SixFontFamily = SixLabors.Fonts.FontFamily;
using SixSystemFonts = SixLabors.Fonts.SystemFonts;

namespace OpenQMS.Services;

public enum WatermarkKind { Document, Attachment, RegulatoryAttachment }

public sealed record WatermarkContext(
    WatermarkKind Kind,
    AppDocument.DocumentStatus? DocStatus,
    string RecordType,
    string RecordId,
    string? Version,
    string UserName,
    DateTime DownloadTime,
    // Controlled-document identity, shown live in the watermark header for
    // Kind.Document only. Optional so the many attachment call sites are
    // unaffected; left null for attachments.
    string? DocNumber = null,
    string? DocTypeCode = null,
    string? DocTitle = null)
{
    // Set when downloading a soft-deleted attachment from the "Deleted attachments" view.
    public bool Deleted { get; init; }
}

public static class WatermarkService
{
    private static readonly DeviceRgb PdfGrey = new(180, 180, 180);
    private static readonly DeviceRgb PdfDark = new( 50,  50,  50);
    private static readonly DeviceRgb PdfRed  = new(204,   0,   0);

    // ─────────────────────────────────────────────────────────────────
    // Public entry point — dispatches by file extension.
    // Returns original bytes unchanged if extension is unsupported or
    // if watermarking fails, so downloads are never broken.
    // ─────────────────────────────────────────────────────────────────

    public static byte[] Apply(byte[] fileBytes, string fileExtension, WatermarkContext ctx)
    {
        if (fileBytes == null || fileBytes.Length == 0)
            return fileBytes ?? Array.Empty<byte>();
        try
        {
            return fileExtension.ToLowerInvariant().TrimStart('.') switch
            {
                "pdf"                             => ApplyToPdf(fileBytes, ctx),
                "docx"                            => ApplyToDocx(fileBytes, ctx),
                "xlsx"                            => ApplyToXlsx(fileBytes, ctx),
                "odt" or "ods"                    => ApplyToOdf(fileBytes, ctx),
                "jpg" or "jpeg" or "png" or "gif" => ApplyToImage(fileBytes, fileExtension, ctx),
                _                                 => fileBytes
            };
        }
        catch
        {
            return fileBytes;
        }
    }

    // ─────────────────────────────────────────────────────────────────
    // Working Copy mark — a single discreet vertical stamp up the RIGHT
    // margin. Deliberately avoids the top-banner and bottom-footer strips
    // used by the normal watermarks, so that when a completed working copy
    // is later re-uploaded as an attachment and re-watermarked, the marks
    // never overlap. PDF only; other types are returned unchanged.
    // ─────────────────────────────────────────────────────────────────
    public static byte[] ApplyWorkingCopyMark(byte[] fileBytes, string fileExtension, string markText)
    {
        if (fileBytes == null || fileBytes.Length == 0)
            return fileBytes ?? Array.Empty<byte>();
        if (!string.Equals(fileExtension?.TrimStart('.'), "pdf", StringComparison.OrdinalIgnoreCase))
            return fileBytes;
        try
        {
            using var inMs  = new MemoryStream(fileBytes);
            using var outMs = new MemoryStream();
            using var reader = new PdfReader(inMs);
            using var writer = new PdfWriter(outMs);
            using var pdf    = new PdfDocument(reader, writer);

            var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
            for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
            {
                var page     = pdf.GetPage(i);
                var pageSize = page.GetPageSizeWithRotation();
                var over     = new PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdf);
                DrawRightMargin(over, pageSize, font, markText);
                over.Release();
            }
            pdf.Close();
            return outMs.ToArray();
        }
        catch
        {
            return fileBytes;
        }
    }

    // Discreet text rotated 90° up the right edge, vertically centred.
    private static void DrawRightMargin(PdfCanvas canvas, PdfRect page, PdfFont font, string text)
    {
        const float fs = 7f;
        float x  = page.GetWidth() - 10f;          // 10pt in from the right edge
        float tw = font.GetWidth(text, fs);
        float y  = (page.GetHeight() - tw) / 2f;    // centre the (rotated) text run vertically

        var gs = new PdfExtGState();
        gs.SetFillOpacity(0.55f);

        canvas.SaveState();
        canvas.SetExtGState(gs);
        canvas.SetFillColor(PdfDark);
        canvas.BeginText();
        canvas.SetFontAndSize(font, fs);
        // 90° rotation: text reads bottom-to-top up the right margin.
        canvas.SetTextMatrix(0, 1, -1, 0, x, y);
        canvas.ShowText(text);
        canvas.EndText();
        canvas.RestoreState();
    }

    // ─────────────────────────────────────────────────────────────────
    // Shared text helpers
    // ─────────────────────────────────────────────────────────────────

    // Returns (header banner text, diagonal text) — exactly one is non-null per call.
    private static (string? Header, string? Diagonal) ResolveMarks(WatermarkContext ctx)
    {
        if (ctx.Deleted && ctx.Kind != WatermarkKind.Document)
            return (null, "DELETED ATTACHMENT - RETAINED FOR TRACEABILITY");

        if (ctx.Kind == WatermarkKind.Attachment)
            return ("SUPPORTING GMP RECORD ATTACHMENT", null);

        if (ctx.Kind == WatermarkKind.RegulatoryAttachment)
            return ("SUPPORTING REGULATORY RECORD ATTACHMENT", null);

        return ctx.DocStatus switch
        {
            AppDocument.DocumentStatus.Approved  or
            AppDocument.DocumentStatus.Effective => ("APPROVED COPY - VALID AT TIME OF DOWNLOAD", null),
            AppDocument.DocumentStatus.Obsolete  => (null, "OBSOLETE DOCUMENT - FOR REVIEW ONLY"),
            _                                    => (null, "UNAPPROVED DOCUMENT - FOR REVIEW ONLY")
        };
    }

    // True when the context carries controlled-document identity to render in
    // the watermark header (Kind.Document with a document number supplied).
    private static bool HasDocumentIdentity(WatermarkContext ctx) =>
        ctx.Kind == WatermarkKind.Document && !string.IsNullOrWhiteSpace(ctx.DocNumber);

    // Identity line for the document watermark header, e.g. "DOC-0042 · SOP · v3.00".
    // Only the parts that are present are included.
    private static string BuildIdentityLine(WatermarkContext ctx)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(ctx.DocNumber))   parts.Add(ctx.DocNumber!);
        if (!string.IsNullOrWhiteSpace(ctx.DocTypeCode)) parts.Add(ctx.DocTypeCode!);
        if (!string.IsNullOrWhiteSpace(ctx.Version))     parts.Add($"v{ctx.Version}");
        return string.Join(" · ", parts);
    }

    // Status tag shown at the right of the document watermark header. Unlike
    // ResolveMarks (which suppresses the top banner for unapproved/obsolete in
    // favour of the diagonal), this returns a status word for every state so the
    // header always carries it.
    private static string ResolveStatusTag(WatermarkContext ctx) => ctx.DocStatus switch
    {
        AppDocument.DocumentStatus.Approved  or
        AppDocument.DocumentStatus.Effective => "APPROVED COPY - VALID AT TIME OF DOWNLOAD",
        AppDocument.DocumentStatus.Obsolete  => "OBSOLETE DOCUMENT - FOR REVIEW ONLY",
        _                                    => "UNAPPROVED DOCUMENT - FOR REVIEW ONLY"
    };

    private static string BuildFooter(WatermarkContext ctx)
    {
        if (ctx.Kind == WatermarkKind.Document)
        {
            // Document version now lives in the watermark header identity line, so
            // the footer carries only download provenance and the uncontrolled notice.
            return $"Downloaded by {ctx.UserName} on {ctx.DownloadTime.ToSite():dd-MMM-yyyy HH:mm} {SiteTime.Label} from MintQMS. " +
                   "Printed/downloaded copies are uncontrolled and may become outdated. " +
                   "Verify current approval status in the system prior to use.";
        }

        string ver = ctx.Version != null ? $" Version {ctx.Version}" : "";
        string electronicRecord = ctx.Kind == WatermarkKind.RegulatoryAttachment
            ? "This file forms part of the regulatory electronic record."
            : "This file forms part of the GMP electronic record.";
        return $"Downloaded from {ctx.RecordType} {ctx.RecordId}{ver} in MintQMS. " +
               $"Downloaded by {ctx.UserName} on {ctx.DownloadTime.ToSite():dd-MMM-yyyy HH:mm} {SiteTime.Label}. " +
               $"{electronicRecord} " +
               "Printed/downloaded copies are uncontrolled.";
    }

    // ─────────────────────────────────────────────────────────────────
    // PDF — iText 8
    // ─────────────────────────────────────────────────────────────────

    private static byte[] ApplyToPdf(byte[] input, WatermarkContext ctx)
    {
        using var inMs  = new MemoryStream(input);
        using var outMs = new MemoryStream();

        using var reader = new PdfReader(inMs);
        using var writer = new PdfWriter(outMs);
        using var pdf    = new PdfDocument(reader, writer);

        var bold    = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
        var regular = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

        var (header, diagonal) = ResolveMarks(ctx);
        bool   docIdentity = HasDocumentIdentity(ctx);
        string identity    = docIdentity ? BuildIdentityLine(ctx) : "";
        string statusTag   = docIdentity ? ResolveStatusTag(ctx)  : "";
        bool   warn        = diagonal != null;
        string footer      = BuildFooter(ctx);

        for (int i = 1; i <= pdf.GetNumberOfPages(); i++)
        {
            var page     = pdf.GetPage(i);
            var pageSize = page.GetPageSizeWithRotation();
            var over     = new PdfCanvas(page.NewContentStreamAfter(), page.GetResources(), pdf);

            if (diagonal != null) DrawDiagonal(over, pageSize, bold, diagonal);
            if (docIdentity)
                DrawDocumentHeader(over, pageSize, bold, regular, identity, statusTag, ctx.DocTitle, warn);
            else if (header != null)
                DrawHeaderBanner(over, pageSize, bold, header);
            DrawFooter(over, pageSize, regular, footer);

            over.Release();
        }

        pdf.Close();
        return outMs.ToArray();
    }

    private static void DrawDiagonal(PdfCanvas canvas, PdfRect page, PdfFont font, string text)
    {
        double ang = Math.Atan2(page.GetHeight(), page.GetWidth());
        float  cos = (float)Math.Cos(ang);
        float  sin = (float)Math.Sin(ang);
        float  cx  = page.GetWidth()  / 2f;
        float  cy  = page.GetHeight() / 2f;

        // Scale font to fill ~65% of the page diagonal so text always fits.
        float diag = (float)Math.Sqrt(
            page.GetWidth() * page.GetWidth() + page.GetHeight() * page.GetHeight());
        float fs = diag * 0.65f / font.GetWidth(text, 1f);
        fs = Math.Max(Math.Min(fs, 64f), 18f);

        float tw = font.GetWidth(text, fs);

        var gs = new PdfExtGState();
        gs.SetFillOpacity(0.20f);

        canvas.SaveState();
        canvas.SetExtGState(gs);
        canvas.SetFillColor(PdfGrey);
        canvas.BeginText();
        canvas.SetFontAndSize(font, fs);
        // Centre text on page, rotated to page diagonal angle.
        canvas.SetTextMatrix(cos, sin, -sin, cos,
            cx - tw / 2f * cos + fs * 0.3f * sin,
            cy - tw / 2f * sin - fs * 0.3f * cos);
        canvas.ShowText(text);
        canvas.EndText();
        canvas.RestoreState();
    }

    private static void DrawHeaderBanner(PdfCanvas canvas, PdfRect page, PdfFont font, string text)
    {
        const float bannerH = 28f;
        const float fs      =  8.5f;
        float bannerY = page.GetTop() - bannerH;

        // Translucent grey background strip
        var gsBg = new PdfExtGState();
        gsBg.SetFillOpacity(0.18f);
        canvas.SaveState();
        canvas.SetExtGState(gsBg);
        canvas.SetFillColor(PdfGrey);
        canvas.Rectangle(0, bannerY, page.GetWidth(), bannerH);
        canvas.Fill();
        canvas.RestoreState();

        // Centred text at 75% opacity
        float tw = font.GetWidth(text, fs);
        float tx = (page.GetWidth() - tw) / 2f;
        float ty = bannerY + (bannerH - fs) / 2f;

        var gsText = new PdfExtGState();
        gsText.SetFillOpacity(0.75f);
        canvas.SaveState();
        canvas.SetExtGState(gsText);
        canvas.SetFillColor(PdfDark);
        canvas.BeginText();
        canvas.SetFontAndSize(font, fs);
        canvas.MoveText(tx, ty);
        canvas.ShowText(text);
        canvas.EndText();
        canvas.RestoreState();
    }

    // Controlled-document header band: two lines on a translucent grey strip,
    // drawn on every page. Line 1 = identity (left) + status (right); line 2 =
    // the document title. All values are live from the MintQMS record.
    private static void DrawDocumentHeader(PdfCanvas canvas, PdfRect page, PdfFont bold,
                                           PdfFont regular, string identity, string statusTag,
                                           string? title, bool warn)
    {
        const float bannerH = 34f;
        const float fsLine1 =  8.5f;
        const float fsLine2 =  8f;
        const float pad     = 10f;
        float top     = page.GetTop();
        float bannerY = top - bannerH;
        float maxW    = page.GetWidth() - 2f * pad;

        // Translucent grey background strip
        var gsBg = new PdfExtGState();
        gsBg.SetFillOpacity(0.18f);
        canvas.SaveState();
        canvas.SetExtGState(gsBg);
        canvas.SetFillColor(PdfGrey);
        canvas.Rectangle(0, bannerY, page.GetWidth(), bannerH);
        canvas.Fill();
        canvas.RestoreState();

        var gsText = new PdfExtGState();
        gsText.SetFillOpacity(0.80f);

        // Line 1 — identity (left) and status (right)
        float ty1 = top - 13f;
        canvas.SaveState();
        canvas.SetExtGState(gsText);
        canvas.SetFillColor(PdfDark);
        canvas.BeginText();
        canvas.SetFontAndSize(bold, fsLine1);
        canvas.MoveText(pad, ty1);
        canvas.ShowText(Truncate(identity, bold, fsLine1, maxW * 0.55f));
        canvas.EndText();
        canvas.RestoreState();

        float stW = bold.GetWidth(statusTag, fsLine1);
        canvas.SaveState();
        canvas.SetExtGState(gsText);
        canvas.SetFillColor(warn ? PdfRed : PdfDark);
        canvas.BeginText();
        canvas.SetFontAndSize(bold, fsLine1);
        canvas.MoveText(page.GetWidth() - pad - stW, ty1);
        canvas.ShowText(statusTag);
        canvas.EndText();
        canvas.RestoreState();

        // Line 2 — title
        if (!string.IsNullOrWhiteSpace(title))
        {
            float ty2 = top - 26f;
            canvas.SaveState();
            canvas.SetExtGState(gsText);
            canvas.SetFillColor(PdfDark);
            canvas.BeginText();
            canvas.SetFontAndSize(regular, fsLine2);
            canvas.MoveText(pad, ty2);
            canvas.ShowText(Truncate(title!, regular, fsLine2, maxW));
            canvas.EndText();
            canvas.RestoreState();
        }
    }

    // Trims text with an ellipsis so it never exceeds maxW at the given size.
    private static string Truncate(string text, PdfFont font, float fs, float maxW)
    {
        if (string.IsNullOrEmpty(text) || font.GetWidth(text, fs) <= maxW) return text;
        const string ell = "…";
        var sb = new StringBuilder();
        foreach (char c in text)
        {
            if (font.GetWidth(sb.ToString() + c + ell, fs) > maxW) break;
            sb.Append(c);
        }
        return sb.Length > 0 ? sb.ToString().TrimEnd() + ell : ell;
    }

    private static void DrawFooter(PdfCanvas canvas, PdfRect page, PdfFont font, string text)
    {
        const float fs     = 6.5f;
        const float margin = 8f;
        // Raised above the very bottom edge so the footer clears the DocuSeal
        // "Document ID: …" stamp that e-signed PDFs imprint at the page bottom.
        const float bottomMargin = 22f;
        float maxW = page.GetWidth() - 2f * margin;

        var lines  = WordWrap(text, font, fs, maxW);
        float lineH  = fs + 2f;
        float startY = bottomMargin + (lines.Count - 1) * lineH;

        canvas.SaveState();
        canvas.SetFillColor(ColorConstants.BLACK);
        canvas.BeginText();
        canvas.SetFontAndSize(font, fs);
        canvas.MoveText(margin, startY);

        for (int j = 0; j < lines.Count; j++)
        {
            if (j > 0) canvas.MoveText(0, -lineH);
            canvas.ShowText(lines[j]);
        }

        canvas.EndText();
        canvas.RestoreState();
    }

    private static List<string> WordWrap(string text, PdfFont font, float fs, float maxW)
    {
        var lines   = new List<string>();
        var words   = text.Split(' ');
        var current = new StringBuilder();

        foreach (var word in words)
        {
            string test = current.Length > 0 ? current + " " + word : word;
            if (font.GetWidth(test, fs) > maxW && current.Length > 0)
            {
                lines.Add(current.ToString());
                current.Clear();
                current.Append(word);
            }
            else
            {
                if (current.Length > 0) current.Append(' ');
                current.Append(word);
            }
        }
        if (current.Length > 0) lines.Add(current.ToString());
        return lines;
    }

    // ─────────────────────────────────────────────────────────────────
    // DOCX — Open XML SDK (available transitively via ClosedXML)
    // Header/footer text is injected; existing content is replaced so
    // the watermark always appears on every page.
    // ─────────────────────────────────────────────────────────────────

    private static byte[] ApplyToDocx(byte[] input, WatermarkContext ctx)
    {
        using var ms = new MemoryStream();
        ms.Write(input, 0, input.Length);
        ms.Position = 0;

        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var mainPart = doc.MainDocumentPart!;
            var body     = mainPart.Document.Body!;

            var sectPr = body.Elements<SectionProperties>().LastOrDefault()
                      ?? body.AppendChild(new SectionProperties());

            var (header, diagonal) = ResolveMarks(ctx);
            // For unapproved/obsolete there is no banner header — use the diagonal text
            // as the header text and colour it red.
            string labelText  = header ?? diagonal ?? "";
            bool   isWarning  = diagonal != null;
            string footerText = BuildFooter(ctx);

            var hdrPart = mainPart.AddNewPart<HeaderPart>();
            if (HasDocumentIdentity(ctx))
                BuildDocxDocumentHeader(hdrPart, BuildIdentityLine(ctx), ResolveStatusTag(ctx), ctx.DocTitle, isWarning);
            else
                BuildDocxHeader(hdrPart, labelText, isWarning);

            var ftrPart = mainPart.AddNewPart<FooterPart>();
            BuildDocxFooter(ftrPart, footerText);

            sectPr.RemoveAllChildren<HeaderReference>();
            sectPr.RemoveAllChildren<FooterReference>();
            sectPr.InsertAt(new HeaderReference
            {
                Type = HeaderFooterValues.Default,
                Id   = mainPart.GetIdOfPart(hdrPart)
            }, 0);
            sectPr.InsertAt(new FooterReference
            {
                Type = HeaderFooterValues.Default,
                Id   = mainPart.GetIdOfPart(ftrPart)
            }, 1);

            mainPart.Document.Save();
        }

        return ms.ToArray();
    }

    private static void BuildDocxHeader(HeaderPart part, string text, bool isWarning)
    {
        // Warning (unapproved/obsolete) → red; approved/attachment → grey
        string colorHex = isWarning ? "CC0000" : "808080";
        part.Header = new Header(
            new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(
                    new RunProperties(
                        new Bold(),
                        new DocumentFormat.OpenXml.Wordprocessing.Color { Val = colorHex },
                        new FontSize { Val = "18" }   // 9 pt = 18 half-points
                    ),
                    new DocumentFormat.OpenXml.Wordprocessing.Text(text)
                )
            )
        );
        part.Header.Save();
    }

    // Controlled-document DOCX header: line 1 = identity (left) + status (right,
    // red when unapproved/obsolete) via a right tab stop; line 2 = the title.
    private static void BuildDocxDocumentHeader(HeaderPart part, string identity, string statusTag, string? title, bool isWarning)
    {
        string statusColor = isWarning ? "CC0000" : "808080";

        var line1Props = new ParagraphProperties(
            new Tabs(new TabStop { Val = TabStopValues.Right, Position = 9020 }));
        var line1 = new Paragraph(
            line1Props,
            new Run(
                new RunProperties(new Bold(),
                    new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "808080" },
                    new FontSize { Val = "17" }),
                new DocumentFormat.OpenXml.Wordprocessing.Text(identity) { Space = SpaceProcessingModeValues.Preserve }),
            new Run(new TabChar()),
            new Run(
                new RunProperties(new Bold(),
                    new DocumentFormat.OpenXml.Wordprocessing.Color { Val = statusColor },
                    new FontSize { Val = "17" }),
                new DocumentFormat.OpenXml.Wordprocessing.Text(statusTag) { Space = SpaceProcessingModeValues.Preserve }));

        var paragraphs = new List<OpenXmlElement> { line1 };
        if (!string.IsNullOrWhiteSpace(title))
        {
            paragraphs.Add(new Paragraph(
                new Run(
                    new RunProperties(
                        new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "808080" },
                        new FontSize { Val = "16" }),
                    new DocumentFormat.OpenXml.Wordprocessing.Text(title!) { Space = SpaceProcessingModeValues.Preserve })));
        }

        part.Header = new Header(paragraphs);
        part.Header.Save();
    }

    private static void BuildDocxFooter(FooterPart part, string text)
    {
        part.Footer = new Footer(
            new Paragraph(
                new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
                new Run(
                    new RunProperties(
                        new FontSize { Val = "14" },   // 7 pt = 14 half-points
                        new DocumentFormat.OpenXml.Wordprocessing.Color { Val = "404040" }
                    ),
                    new DocumentFormat.OpenXml.Wordprocessing.Text(text)
                    {
                        Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve
                    }
                )
            )
        );
        part.Footer.Save();
    }

    // ─────────────────────────────────────────────────────────────────
    // XLSX — ClosedXML
    // Injects page header and footer visible in Print Preview / printed output.
    // ─────────────────────────────────────────────────────────────────

    private static byte[] ApplyToXlsx(byte[] input, WatermarkContext ctx)
    {
        using var inMs = new MemoryStream(input);
        using var wb   = new XLWorkbook(inMs);

        var (header, diagonal) = ResolveMarks(ctx);
        string hText = header ?? diagonal ?? "";
        bool   warn  = diagonal != null;
        string fText = BuildFooter(ctx);

        bool   docIdentity = HasDocumentIdentity(ctx);
        string identity    = docIdentity ? BuildIdentityLine(ctx) : "";
        string statusTag   = docIdentity ? ResolveStatusTag(ctx)  : "";

        foreach (var ws in wb.Worksheets)
        {
            if (docIdentity)
            {
                // Identity (left) · title (centre) · status (right, red when unapproved/obsolete).
                ws.PageSetup.Header.Left.AddText(identity)
                    .SetBold(true).SetFontSize(9).SetFontColor(XLColor.FromHtml("#808080"));
                if (!string.IsNullOrWhiteSpace(ctx.DocTitle))
                    ws.PageSetup.Header.Center.AddText(ctx.DocTitle!)
                        .SetFontSize(9).SetFontColor(XLColor.FromHtml("#808080"));
                ws.PageSetup.Header.Right.AddText(statusTag)
                    .SetBold(true).SetFontSize(9).SetFontColor(XLColor.FromHtml(warn ? "#CC0000" : "#808080"));
            }
            else
            {
                // Match the DOCX/ODF scheme: bold 9pt header (red for unapproved/obsolete,
                // grey otherwise).
                ws.PageSetup.Header.Center.AddText(hText)
                    .SetBold(true).SetFontSize(9)
                    .SetFontColor(XLColor.FromHtml(warn ? "#CC0000" : "#808080"));
            }
            ws.PageSetup.Footer.Center.AddText(fText)
                .SetFontSize(7).SetFontColor(XLColor.FromHtml("#404040"));
        }

        using var outMs = new MemoryStream();
        wb.SaveAs(outMs);
        return outMs.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────
    // ODF — OpenDocument (LibreOffice .odt / .ods)
    // No mature .NET ODF API, so the watermark is injected directly into
    // styles.xml: a master-page header (status line; red when unapproved/
    // obsolete) and footer (download / uncontrolled line), mirroring the
    // DOCX/XLSX text-watermark behaviour. Any existing page header/footer is
    // replaced so the watermark always shows. Returns input unchanged on any
    // problem (caller already swallows exceptions too).
    // ─────────────────────────────────────────────────────────────────

    private const string OdfOffice = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
    private const string OdfStyle  = "urn:oasis:names:tc:opendocument:xmlns:style:1.0";
    private const string OdfText   = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    private const string OdfFo     = "urn:oasis:names:tc:opendocument:xmlns:xsl-fo-compatible:1.0";

    private static byte[] ApplyToOdf(byte[] input, WatermarkContext ctx)
    {
        var (header, diagonal) = ResolveMarks(ctx);
        string label  = header ?? diagonal ?? "";
        bool   warn   = diagonal != null;
        string footer = BuildFooter(ctx);

        bool   docIdentity = HasDocumentIdentity(ctx);
        // For a controlled document the header line 1 carries identity + status.
        string headerLine1 = docIdentity ? $"{BuildIdentityLine(ctx)}  ·  {ResolveStatusTag(ctx)}" : label;
        string? headerTitle = docIdentity ? ctx.DocTitle : null;

        using var ms = new MemoryStream();
        ms.Write(input, 0, input.Length);
        ms.Position = 0;

        using (var zip = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("styles.xml");
            if (entry == null) return input;

            string xml;
            using (var sr = new StreamReader(entry.Open()))
                xml = sr.ReadToEnd();

            string updated = InjectOdfMarks(xml, headerLine1, warn, footer, headerTitle);
            if (updated == null) return input;

            entry.Delete();
            var fresh = zip.CreateEntry("styles.xml");
            using var sw = new StreamWriter(fresh.Open(), new UTF8Encoding(false));
            sw.Write(updated);
        }
        return ms.ToArray();
    }

    private static string InjectOdfMarks(string stylesXml, string label, bool warn, string footer, string? title = null)
    {
        var doc = new XmlDocument { PreserveWhitespace = true };
        try { doc.LoadXml(stylesXml); } catch { return null; }

        var nsm = new XmlNamespaceManager(doc.NameTable);
        nsm.AddNamespace("office", OdfOffice);
        nsm.AddNamespace("style",  OdfStyle);

        var masterPages = doc.SelectNodes("//style:master-page", nsm);
        if (masterPages == null || masterPages.Count == 0) return null;

        const string hStyle = "MintQmsWmHeader";
        const string fStyle = "MintQmsWmFooter";

        var officeStyles = doc.SelectSingleNode("//office:styles", nsm);
        if (officeStyles == null)
        {
            officeStyles = doc.CreateElement("office", "styles", OdfOffice);
            doc.DocumentElement!.AppendChild(officeStyles);
        }
        if (doc.SelectSingleNode($"//style:style[@style:name='{hStyle}']", nsm) == null)
        {
            officeStyles.AppendChild(OdfParaStyle(doc, hStyle, bold: true,
                color: warn ? "#CC0000" : "#808080", sizePt: "9pt"));
            officeStyles.AppendChild(OdfParaStyle(doc, fStyle, bold: false,
                color: "#404040", sizePt: "7pt"));
        }

        foreach (XmlNode mp in masterPages)
        {
            var plName = mp.Attributes?.GetNamedItem("page-layout-name", OdfStyle)?.Value;
            if (!string.IsNullOrEmpty(plName))
            {
                var pl = doc.SelectSingleNode($"//style:page-layout[@style:name='{plName}']", nsm);
                if (pl != null)
                {
                    OdfEnsureHfStyle(doc, pl, "header-style", "margin-bottom");
                    OdfEnsureHfStyle(doc, pl, "footer-style", "margin-top");
                }
            }

            OdfRemove(mp, new[] { "header", "header-left", "footer", "footer-left" });

            var hdr = doc.CreateElement("style", "header", OdfStyle);
            hdr.AppendChild(OdfTextP(doc, hStyle, label));
            if (!string.IsNullOrWhiteSpace(title))
                hdr.AppendChild(OdfTextP(doc, fStyle, title!));   // title line (non-bold grey)
            mp.AppendChild(hdr);

            var ftr = doc.CreateElement("style", "footer", OdfStyle);
            ftr.AppendChild(OdfTextP(doc, fStyle, footer));
            mp.AppendChild(ftr);
        }

        return doc.OuterXml;
    }

    private static void OdfSetAttr(XmlDocument doc, XmlElement el, string prefix, string local, string ns, string val)
    {
        var a = doc.CreateAttribute(prefix, local, ns);
        a.Value = val;
        el.Attributes.Append(a);
    }

    private static XmlElement OdfParaStyle(XmlDocument doc, string name, bool bold, string color, string sizePt)
    {
        var st = doc.CreateElement("style", "style", OdfStyle);
        OdfSetAttr(doc, st, "style", "name", OdfStyle, name);
        OdfSetAttr(doc, st, "style", "family", OdfStyle, "paragraph");
        var pp = doc.CreateElement("style", "paragraph-properties", OdfStyle);
        OdfSetAttr(doc, pp, "fo", "text-align", OdfFo, "center");
        st.AppendChild(pp);
        var tp = doc.CreateElement("style", "text-properties", OdfStyle);
        if (bold) OdfSetAttr(doc, tp, "fo", "font-weight", OdfFo, "bold");
        OdfSetAttr(doc, tp, "fo", "color", OdfFo, color);
        OdfSetAttr(doc, tp, "fo", "font-size", OdfFo, sizePt);
        st.AppendChild(tp);
        return st;
    }

    private static XmlElement OdfTextP(XmlDocument doc, string styleName, string text)
    {
        var p = doc.CreateElement("text", "p", OdfText);
        OdfSetAttr(doc, p, "text", "style-name", OdfText, styleName);
        p.AppendChild(doc.CreateTextNode(text ?? ""));
        return p;
    }

    private static void OdfEnsureHfStyle(XmlDocument doc, XmlNode pageLayout, string which, string marginAttr)
    {
        foreach (XmlNode c in pageLayout.ChildNodes)
            if (c.LocalName == which && c.NamespaceURI == OdfStyle) return;
        var hs  = doc.CreateElement("style", which, OdfStyle);
        var hfp = doc.CreateElement("style", "header-footer-properties", OdfStyle);
        OdfSetAttr(doc, hfp, "fo", "min-height", OdfFo, "0.5cm");
        OdfSetAttr(doc, hfp, "fo", marginAttr, OdfFo, "0.2cm");
        hs.AppendChild(hfp);
        pageLayout.AppendChild(hs);
    }

    private static void OdfRemove(XmlNode parent, string[] localNames)
    {
        var kill = new List<XmlNode>();
        foreach (XmlNode c in parent.ChildNodes)
            if (c.NamespaceURI == OdfStyle && Array.IndexOf(localNames, c.LocalName) >= 0)
                kill.Add(c);
        foreach (var n in kill) parent.RemoveChild(n);
    }

    // ─────────────────────────────────────────────────────────────────
    // Images — SixLabors.ImageSharp + ImageSharp.Drawing
    // ─────────────────────────────────────────────────────────────────

    private static readonly SixLabors.ImageSharp.Formats.DecoderOptions ImageDecoderOptions = new()
    {
        Configuration = new SixLabors.ImageSharp.Configuration(
            new SixLabors.ImageSharp.Formats.Png.PngConfigurationModule(),
            new SixLabors.ImageSharp.Formats.Jpeg.JpegConfigurationModule(),
            new SixLabors.ImageSharp.Formats.Gif.GifConfigurationModule())
    };

    private static byte[] ApplyToImage(byte[] input, string ext, WatermarkContext ctx)
    {
        var (header, diagonal) = ResolveMarks(ctx);
        bool   docIdentity = HasDocumentIdentity(ctx);
        string bannerText  = docIdentity ? $"{BuildIdentityLine(ctx)} · {ResolveStatusTag(ctx)}" : (header ?? "");
        bool   drawBanner  = docIdentity || header != null;
        string diagonalText = diagonal ?? "";
        string footerText  = BuildFooter(ctx);
        bool   isDiagonal  = diagonal != null;

        // Decode only the formats that are watermarked. Image.Load detects the format from the content,
        // so without this a TIFF renamed to .png would reach the TIFF decoder (GHSA-wmxv-xphr-5c9g).
        using var image = SixLabors.ImageSharp.Image.Load(ImageDecoderOptions, input);
        float w = image.Width;
        float h = image.Height;

        // Attempt to resolve a system font; return original if none found.
        SixFontFamily fontFamily = default;
        bool found = SixSystemFonts.TryGet("Arial", out fontFamily)
                  || SixSystemFonts.TryGet("Liberation Sans", out fontFamily)
                  || SixSystemFonts.TryGet("DejaVu Sans", out fontFamily)
                  || SixSystemFonts.TryGet("FreeSans", out fontFamily);

        if (!found)
        {
            var all = SixSystemFonts.Families.ToList();
            if (all.Count == 0) return input;
            fontFamily = all[0];
        }

        var bannerFont = fontFamily.CreateFont(Math.Max(w / 45f, 10f), FontStyle.Bold);
        var diagFont   = fontFamily.CreateFont(Math.Max(w / 18f, 14f), FontStyle.Bold);
        // Shrink the diagonal mark until it fits ~85% of the image diagonal (long marks overflowed)
        if (isDiagonal)
        {
            float diagLen  = (float)Math.Sqrt(w * w + h * h) * 0.85f;
            float textLen  = TextMeasurer.MeasureSize(diagonalText, new TextOptions(diagFont)).Width;
            if (textLen > diagLen)
                diagFont = fontFamily.CreateFont(Math.Max(diagFont.Size * diagLen / textLen, 8f), FontStyle.Bold);
        }
        var footerFont = fontFamily.CreateFont(Math.Max(w / 90f,  7f));

        var bannerBg  = SixLabors.ImageSharp.Color.FromRgba(180, 180, 180, 50);
        var diagColor = SixLabors.ImageSharp.Color.FromRgba(150, 150, 150, 70);
        var textColor = SixLabors.ImageSharp.Color.FromRgba( 50,  50,  50, 200);

        image.Mutate(proc =>
        {
            if (drawBanner)
            {
                // Translucent header banner across top ~5.5% of image height
                proc.Fill(bannerBg, new RectangularPolygon(0f, 0f, w, h * 0.055f));

                var hOpts = new RichTextOptions(bannerFont)
                {
                    Origin              = new System.Numerics.Vector2(w / 2f, h * 0.027f),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                proc.DrawText(hOpts, bannerText, textColor);
            }

            // Diagonal overlay for unapproved / obsolete documents (~-35 degrees)
            if (isDiagonal)
            {
                float angle = -(float)(35.0 * Math.PI / 180.0);
                proc.SetDrawingTransform(System.Numerics.Matrix3x2.CreateRotation(
                    angle, new System.Numerics.Vector2(w / 2f, h / 2f)));
                var dOpts = new RichTextOptions(diagFont)
                {
                    Origin              = new System.Numerics.Vector2(w / 2f, h / 2f),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                proc.DrawText(dOpts, diagonalText, diagColor);
                proc.SetDrawingTransform(System.Numerics.Matrix3x2.Identity);
            }

            // Footer at bottom of image
            // Anchored by its bottom edge so a footer that wraps onto two lines stays inside the image
            var fOpts = new RichTextOptions(footerFont)
            {
                Origin              = new System.Numerics.Vector2(w / 2f, h * 0.99f),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Bottom,
                TextAlignment       = SixLabors.Fonts.TextAlignment.Center,
                WrappingLength      = w * 0.95f
            };
            proc.DrawText(fOpts, footerText, textColor);
        });

        using var outMs = new MemoryStream();
        if (ext.TrimStart('.').ToLowerInvariant() is "jpg" or "jpeg")
            image.SaveAsJpeg(outMs);
        else
            image.SaveAsPng(outMs);

        return outMs.ToArray();
    }
}
