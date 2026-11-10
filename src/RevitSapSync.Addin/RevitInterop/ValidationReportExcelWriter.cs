using Microsoft.Office.Interop.Excel;
using RevitSapSync.Core.Models;
using Range = Microsoft.Office.Interop.Excel.Range;

namespace RevitSapSync.RevitInterop;

/// <summary>
/// Reimplements the reference project's <c>ExcelUtils.AddDataToWorksheet</c>: writes each
/// part of a <see cref="SectionValidationReport"/> into its own worksheet (matching sheet
/// names: MatchingBeams, MatchingColumns, UnMatchingRevitBeams, UnMatchingRevitColumns),
/// highlights (yellow fill) the Revit/SAP cell pair whenever they differ by more than
/// <see cref="MismatchThresholdPercent"/>%, adds a trailing column-totals row, and autofits
/// columns - same output as the reference. Uses direct typed property access instead of
/// the reference's runtime reflection, since our records are concrete types rather than
/// anonymous objects built ad hoc; behavior is unchanged. Leaves the workbook open and
/// unsaved (Excel.Visible = true) for the user to review/save, matching the reference
/// exactly - no auto-save/close/quit.
/// </summary>
public sealed class ValidationReportExcelWriter
{
    private const double MismatchThresholdPercent = 10.0;

    private static readonly string[] MatchingHeaders =
    {
        "RevitTypeName", "RevitMemberCount", "RevitTotalLengthMeters", "RevitTotalCutLengthMeters", "RevitTotalTonnage",
        "SapSectionName", "SapMemberCount", "SapTotalLengthMeters", "SapTotalTonnage", "SapCrossSectionAreaMm2",
    };

    private static readonly string[] UnmatchedHeaders =
    {
        "RevitTypeName", "RevitMemberCount", "RevitTotalLengthMeters", "RevitTotalCutLengthMeters", "RevitTotalTonnage",
    };

    public void Export(SectionValidationReport report)
    {
        var excelApp = new Application { Visible = true };
        Workbook workbook = excelApp.Workbooks.Add();

        if (report.MatchingBeams.Count > 0)
            WriteMatchingSheet(workbook, report.MatchingBeams, "MatchingBeams");

        if (report.MatchingColumns.Count > 0)
            WriteMatchingSheet(workbook, report.MatchingColumns, "MatchingColumns");

        if (report.UnmatchedRevitBeams.Count > 0)
            WriteUnmatchedSheet(workbook, report.UnmatchedRevitBeams, "UnMatchingRevitBeams");

        if (report.UnmatchedRevitColumns.Count > 0)
            WriteUnmatchedSheet(workbook, report.UnmatchedRevitColumns, "UnMatchingRevitColumns");
    }

    private static void WriteMatchingSheet(Workbook workbook, List<SectionValidationRecord> records, string sheetName)
    {
        var worksheet = (Worksheet)workbook.Sheets.Add();
        worksheet.Name = sheetName;
        WriteHeaderRow(worksheet, MatchingHeaders);

        int rowIndex = 2;
        foreach (var record in records)
        {
            WriteRow(worksheet, rowIndex, new object?[]
            {
                record.RevitTypeName, record.RevitMemberCount, record.RevitTotalLengthMeters, record.RevitTotalCutLengthMeters, record.RevitTotalTonnage,
                record.SapSectionName, record.SapMemberCount, record.SapTotalLengthMeters, record.SapTotalTonnage, record.SapCrossSectionAreaMm2,
            });

            if (IsMismatch(record.RevitMemberCount, record.SapMemberCount))
            {
                Highlight(worksheet, rowIndex, 2);
                Highlight(worksheet, rowIndex, 7);
            }

            if (IsMismatch(record.RevitTotalLengthMeters, record.SapTotalLengthMeters))
            {
                Highlight(worksheet, rowIndex, 3);
                Highlight(worksheet, rowIndex, 8);
            }

            if (IsMismatch(record.RevitTotalTonnage, record.SapTotalTonnage))
            {
                Highlight(worksheet, rowIndex, 5);
                Highlight(worksheet, rowIndex, 9);
            }

            rowIndex++;
        }

        WriteTotalsRow(worksheet, MatchingHeaders.Length, rowIndex);
        AutoFitColumns(worksheet, MatchingHeaders.Length);
    }

    private static void WriteUnmatchedSheet(Workbook workbook, List<UnmappedRevitTypeRecord> records, string sheetName)
    {
        var worksheet = (Worksheet)workbook.Sheets.Add();
        worksheet.Name = sheetName;
        WriteHeaderRow(worksheet, UnmatchedHeaders);

        int rowIndex = 2;
        foreach (var record in records)
        {
            WriteRow(worksheet, rowIndex, new object?[]
            {
                record.RevitTypeName, record.RevitMemberCount, record.RevitTotalLengthMeters, record.RevitTotalCutLengthMeters, record.RevitTotalTonnage,
            });
            rowIndex++;
        }

        WriteTotalsRow(worksheet, UnmatchedHeaders.Length, rowIndex);
        AutoFitColumns(worksheet, UnmatchedHeaders.Length);
    }

    private static void WriteHeaderRow(Worksheet worksheet, string[] headers)
    {
        for (int col = 0; col < headers.Length; col++)
            worksheet.Cells[1, col + 1] = headers[col];
    }

    private static void WriteRow(Worksheet worksheet, int rowIndex, object?[] values)
    {
        for (int col = 0; col < values.Length; col++)
            worksheet.Cells[rowIndex, col + 1] = values[col];
    }

    private static void Highlight(Worksheet worksheet, int row, int col)
    {
        var cell = (Range)worksheet.Cells[row, col];
        cell.Interior.Color = System.Drawing.ColorTranslator.ToOle(System.Drawing.Color.Yellow);
    }

    private static bool IsMismatch(double revitValue, double sapValue)
    {
        double max = Math.Max(revitValue, sapValue);
        if (max == 0)
            return false;

        return Math.Abs(revitValue - sapValue) / max * 100 > MismatchThresholdPercent;
    }

    private static void WriteTotalsRow(Worksheet worksheet, int columnCount, int totalsRow)
    {
        for (int col = 1; col <= columnCount; col++)
        {
            double sum = 0;
            for (int row = 2; row < totalsRow; row++)
            {
                if (double.TryParse(((Range)worksheet.Cells[row, col]).Value?.ToString(), out double cellValue))
                    sum += cellValue;
            }

            if (sum != 0)
                ((Range)worksheet.Cells[totalsRow, col]).Value = sum;
        }
    }

    private static void AutoFitColumns(Worksheet worksheet, int columnCount)
    {
        Range usedRange = worksheet.Range[worksheet.Cells[1, 1], worksheet.Cells[worksheet.Rows.Count, columnCount]];
        usedRange.Columns.AutoFit();
    }
}
