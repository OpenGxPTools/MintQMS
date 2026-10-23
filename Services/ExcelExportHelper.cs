/*
This file is part of the OpenQMS.net project (https://github.com/C-realize/OpenQMS).
Copyright (C) 2022-2025  C-realize IT Services SRL (https://www.c-realize.com)
*/
using ClosedXML.Excel;

namespace OpenQMS.Services
{
    /// <summary>
    /// Adds a standardised "Export Info" worksheet to Excel workbooks, satisfying the GMP
    /// requirement (EU Annex 11 §9, 21 CFR 11.10(b)) that exported data records include
    /// provenance metadata (who, when, source system) so that auditors can verify the export
    /// was generated from the validated system and has not been tampered with.
    /// </summary>
    public static class ExcelExportHelper
    {
        public static void AddExportInfoSheet(IXLWorkbook wb, string exportedBy, string reportTitle, int recordCount)
        {
            var ws = wb.Worksheets.Add("Export Info");

            ws.Cell("A1").Value = "MintQMS — Electronic Quality Management System";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 13;

            ws.Cell("A3").Value = "Report Title";
            ws.Cell("B3").Value = reportTitle;

            ws.Cell("A4").Value = "Exported By";
            ws.Cell("B4").Value = exportedBy;

            ws.Cell("A5").Value = "Exported On (UTC)";
            ws.Cell("B5").Value = SiteTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + SiteTime.Label;

            ws.Cell("A6").Value = "Record Count";
            ws.Cell("B6").Value = recordCount;

            ws.Cell("A8").Value = "IMPORTANT";
            ws.Cell("A8").Style.Font.Bold = true;
            ws.Cell("A8").Style.Font.FontColor = XLColor.DarkRed;
            ws.Cell("B8").Value = "This file was generated from a validated QMS. Any modification to this file after export invalidates its regulatory value. Do not alter the data.";
            ws.Cell("B8").Style.Font.Italic = true;

            ws.Column("A").Style.Font.Bold = true;
            ws.Column("A").Width = 22;
            ws.Column("B").Width = 70;
            ws.Column("B").Style.Alignment.WrapText = true;

            // Move the Export Info sheet to the last position
            ws.Position = wb.Worksheets.Count;
        }
    }
}
