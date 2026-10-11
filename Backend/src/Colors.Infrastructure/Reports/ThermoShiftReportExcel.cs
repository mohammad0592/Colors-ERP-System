using System.Globalization;
using ClosedXML.Excel;
using Colors.Application.Features.Reports;

namespace Colors.Infrastructure.Reports;

/// <summary>
/// Writes the thermoforming report into the factory's own form (specification section 13).
///
/// The form is the factory's spreadsheet, kept as a file beside this class and filled in
/// cell by cell, so the export looks like the paper it replaces — the same layout,
/// colours, merged boxes and print setup — and keeps its formulas, so somebody correcting
/// a number in Excel sees the totals follow.
///
/// The form has room for nine rolls, five pallets and two products. A busier shift gets
/// rows added inside each table rather than being cut short; everything below moves down
/// with them.
/// </summary>
public class ThermoShiftReportExcel : IThermoShiftReportExcel
{
    private const string TemplateResource = "Colors.Infrastructure.Reports.Templates.thermo-shift-report.xlsx";

    // Where each table sits in the untouched form.
    private const int RollFirstRow = 13;
    private const int RollRowsInForm = 9;
    private const int PalletFirstRow = 26;
    private const int PalletRowsInForm = 5;
    private const int YieldFirstRow = 32;
    private const int YieldRowsInForm = 2;
    private const int PackagingColumns = 6;

    private static readonly string[] ArabicDays =
        ["الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"];

    public byte[] Build(ThermoShiftReportDto report)
    {
        using var template = typeof(ThermoShiftReportExcel).Assembly.GetManifestResourceStream(TemplateResource)
            ?? throw new InvalidOperationException($"The report form {TemplateResource} is missing from the build.");
        using var workbook = new XLWorkbook(template);
        var sheet = workbook.Worksheet(1);

        // Grow the tables first, bottom one first, so the rows each later step names are
        // already where they will end up.
        var extraYield = Math.Max(0, report.Products.Count - YieldRowsInForm);
        var extraPallets = Math.Max(0, report.Pallets.Count - PalletRowsInForm);
        var extraRolls = Math.Max(0, report.Rolls.Count - RollRowsInForm);

        // Each copies only its own table's columns: the yield block shares its rows with
        // the pallet totals, which must not be repeated down the new rows.
        Grow(sheet, YieldFirstRow + YieldRowsInForm - 1, extraYield, "H", "L", merge: null);
        Grow(sheet, PalletFirstRow + PalletRowsInForm - 1, extraPallets, "A", "F", merge: ("E", "F"));
        Grow(sheet, RollFirstRow + RollRowsInForm - 1, extraRolls, "A", "N", merge: ("K", "L"));

        var rollLast = RollFirstRow + RollRowsInForm + extraRolls - 1;
        var totalsRow = rollLast + 2;
        var palletFirst = PalletFirstRow + extraRolls;
        var palletLast = palletFirst + PalletRowsInForm + extraPallets - 1;
        var notesRow = palletFirst - 1;
        var yieldFirst = YieldFirstRow + extraRolls + extraPallets;
        var palletTotalsRow = yieldFirst + 1;
        var packagingHeader = 36 + extraRolls + extraPallets + extraYield;

        WriteHeader(sheet, report);
        WriteRolls(sheet, report, RollFirstRow, rollLast, totalsRow);
        WritePallets(sheet, report, palletFirst, palletLast, palletTotalsRow);
        sheet.Cell(notesRow, "H").Value = report.Notes ?? string.Empty;
        WriteYield(sheet, report, yieldFirst, RollFirstRow, rollLast);
        WritePackaging(sheet, report, packagingHeader);

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }

    /// <summary>
    /// Adds rows below a table's last row, each made to look like it — borders, fills and
    /// number formats over the table's own columns, and its merged boxes.
    /// </summary>
    private static void Grow(
        IXLWorksheet sheet,
        int lastRow,
        int extra,
        string firstColumn,
        string lastColumn,
        (string From, string To)? merge)
    {
        if (extra == 0)
        {
            return;
        }

        sheet.Row(lastRow).InsertRowsBelow(extra);
        var pattern = sheet.Range($"{firstColumn}{lastRow}:{lastColumn}{lastRow}");

        for (var row = lastRow + 1; row <= lastRow + extra; row++)
        {
            pattern.CopyTo(sheet.Cell(row, firstColumn));
            sheet.Range($"{firstColumn}{row}:{lastColumn}{row}").Clear(XLClearOptions.Contents);
            sheet.Row(row).Height = sheet.Row(lastRow).Height;
            if (merge is { } columns)
            {
                sheet.Range($"{columns.From}{row}:{columns.To}{row}").Merge();
            }
        }
    }

    private static void WriteHeader(IXLWorksheet sheet, ThermoShiftReportDto report)
    {
        var date = report.ProductionDate;
        sheet.Cell("A3").Value = report.OperatorEmployeeNumber ?? string.Empty;
        sheet.Cell("D3").Value = report.OperatorName ?? string.Empty;
        sheet.Cell("H3").Value = report.ShiftName;
        // As the factory writes it: الاثنين 05-OCT-2026.
        sheet.Cell("I3").Value =
            $"{ArabicDays[(int)date.DayOfWeek]} {date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture).ToUpperInvariant()}";

        sheet.Cell("C6").Value = Number(report.ElectricityEndMeter);
        sheet.Cell("D6").Value = Number(report.ElectricityStartMeter);
        sheet.Cell("A6").FormulaA1 = "=IF(OR(C6=\"\",D6=\"\"),\"\",C6-D6)";

        // The form writes the unit beside the figure, in the same box.
        sheet.Cell("G6").Value = report.CycleTimeSeconds is null ? "-" : $"{Plain(report.CycleTimeSeconds.Value)} ثانية";
        sheet.Cell("H6").Value = report.FeedDistanceMm is null ? "-" : $"{report.FeedDistanceMm} ملم";
        sheet.Cell("I6").Value = report.MachineSpeed is null ? "-" : (XLCellValue)report.MachineSpeed.Value;
        sheet.Cell("J6").Value = string.Join("، ", report.Products.Select(p => p.ProductName));

        sheet.Cell("J9").Value = Time(report.ProductionStartTime);
        sheet.Cell("I9").Value = Time(report.ProductionEndTime);
        // A length of time, not a time of day: 6.67 hours shows as 6:40, never as 4 PM.
        sheet.Cell("H9").Value = report.ActualProductionHours is null
            ? Blank.Value
            : (XLCellValue)TimeSpan.FromHours((double)report.ActualProductionHours.Value);
        sheet.Cell("H9").Style.NumberFormat.Format = "[h]:mm";

        sheet.Cell("A9").Value = report.Workers.Count == 0 ? "-" : string.Join(" - ", report.Workers);
        sheet.Cell("A11").Value = report.Trainees.Count == 0 ? "-" : string.Join(" - ", report.Trainees);
    }

    private static void WriteRolls(IXLWorksheet sheet, ThermoShiftReportDto report, int first, int last, int totalsRow)
    {
        for (var row = first; row <= last; row++)
        {
            foreach (var column in new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "M", "N" })
            {
                sheet.Cell(row, column).Clear(XLClearOptions.Contents);
            }
        }

        for (var i = 0; i < report.Rolls.Count; i++)
        {
            var roll = report.Rolls[i];
            var row = first + i;

            sheet.Cell(row, "A").Value = $"{roll.RollCode}-{roll.ColorName}";
            sheet.Cell(row, "B").Value = Number(roll.RollWeight);
            sheet.Cell(row, "C").Value = Number(roll.RollLength);
            sheet.Cell(row, "D").Value = Number(roll.AverageThickness);
            sheet.Cell(row, "E").Value = roll.TotalTimeMinutes is null ? Blank.Value : (XLCellValue)roll.TotalTimeMinutes.Value;
            sheet.Cell(row, "F").Value = roll.ProductName ?? string.Empty;
            // The form's column was sized for "Lunch Box P1"; a longer name shrinks to fit
            // rather than being cut off.
            sheet.Cell(row, "F").Style.Alignment.ShrinkToFit = true;
            sheet.Cell(row, "N").Value = roll.RollShiftName;

            // A roll still in the machine has nothing counted yet, and its row says so by
            // being empty rather than by showing a run of zeros.
            if (roll.BagCount is null)
            {
                continue;
            }

            sheet.Cell(row, "G").Value = roll.BagCount.Value;
            // Plates are bags times what the product holds, as the form works it out — from
            // the product, not a fixed 250, so a 500-plate bag counts right.
            sheet.Cell(row, "H").FormulaA1 = $"=G{row}*{roll.PiecesPerBag ?? 0}";
            sheet.Cell(row, "I").Value = Number(roll.PieceWeight);
            sheet.Cell(row, "J").Value = roll.AbsorbentPercentage is null ? "-" : (XLCellValue)roll.AbsorbentPercentage.Value;
            sheet.Cell(row, "K").Value = Number(roll.BagWeight);
            if (roll.RecycledPercentage is null)
            {
                sheet.Cell(row, "M").Value = "-";
            }
            else
            {
                sheet.Cell(row, "M").Value = roll.RecycledPercentage.Value / 100m;
                sheet.Cell(row, "M").Style.NumberFormat.Format = "0%";
            }
        }

        sheet.Cell(totalsRow, "B").FormulaA1 = $"=SUM(B{first}:B{last})";
        sheet.Cell(totalsRow, "G").FormulaA1 = $"=SUM(G{first}:G{last})";
        sheet.Cell(totalsRow, "H").FormulaA1 = $"=SUM(H{first}:H{last})";
    }

    private static void WritePallets(IXLWorksheet sheet, ThermoShiftReportDto report, int first, int last, int totalsRow)
    {
        for (var row = first; row <= last; row++)
        {
            foreach (var column in new[] { "A", "B", "C", "D", "E" })
            {
                sheet.Cell(row, column).Clear(XLClearOptions.Contents);
            }
        }

        for (var i = 0; i < report.Pallets.Count; i++)
        {
            var pallet = report.Pallets[i];
            var row = first + i;

            sheet.Cell(row, "A").Value = string.Join(", ", pallet.RollCodes);
            sheet.Cell(row, "A").Style.Alignment.ShrinkToFit = true;
            sheet.Cell(row, "B").Value = pallet.PalletNumber;
            sheet.Cell(row, "C").Value = pallet.BagCount;
            sheet.Cell(row, "D").FormulaA1 = $"=C{row}*{pallet.PiecesPerBag ?? 0}";
            sheet.Cell(row, "E").Value = string.Join(" ", new[] { pallet.ProductName, pallet.ColorName }
                .Where(part => !string.IsNullOrEmpty(part)));
        }

        sheet.Cell(totalsRow, "A").FormulaA1 = $"=COUNT(B{first}:B{last})";
        sheet.Cell(totalsRow, "C").FormulaA1 = $"=SUM(C{first}:C{last})";
        sheet.Cell(totalsRow, "D").FormulaA1 = $"=SUM(D{first}:D{last})";
    }

    /// <summary>
    /// One line per product: rolls in, plates out, and the loss between. Worked out from
    /// the roll table above, so a correction there carries through.
    /// </summary>
    private static void WriteYield(IXLWorksheet sheet, ThermoShiftReportDto report, int first, int rollFirst, int rollLast)
    {
        var rows = Math.Max(YieldRowsInForm, report.Products.Count);
        var products = $"$F${rollFirst}:$F${rollLast}";
        var bags = $"$G${rollFirst}:$G${rollLast}";

        for (var i = 0; i < rows; i++)
        {
            var row = first + i;
            foreach (var column in new[] { "H", "I", "J", "K", "L" })
            {
                sheet.Cell(row, column).Clear(XLClearOptions.Contents);
            }

            if (i >= report.Products.Count)
            {
                sheet.Cell(row, "L").Value = "-";
                continue;
            }

            // Quotes doubled: a product name is text inside a formula.
            var name = report.Products[i].ProductName.Replace("\"", "\"\"");

            // Only rolls whose bags were counted, as on the screen — an uncounted roll has a
            // weight but no plates, and would read as entirely lost.
            sheet.Cell(row, "J").FormulaA1 =
                $"=SUMIFS($B${rollFirst}:$B${rollLast},{products},\"{name}\",{bags},\"<>\")";
            sheet.Cell(row, "K").FormulaA1 = $"=SUMIFS($H${rollFirst}:$H${rollLast},{products},\"{name}\")";
            // Rolls in, less plates out at their weighed weight (grams).
            sheet.Cell(row, "I").FormulaA1 =
                $"=J{row}-SUMPRODUCT(({products}=\"{name}\")*($H${rollFirst}:$H${rollLast})*($I${rollFirst}:$I${rollLast}))/1000";
            sheet.Cell(row, "H").FormulaA1 = $"=IF(J{row}=0,0,I{row}/J{row})";
            sheet.Cell(row, "H").Style.NumberFormat.Format = "0%";
            sheet.Cell(row, "L").Value = report.Products[i].ProductCode;
        }
    }

    /// <summary>
    /// The packaging used, under the factory's own material names. The form has six boxes;
    /// a seventh material would have nowhere to go, so the six largest are kept.
    /// </summary>
    private static void WritePackaging(IXLWorksheet sheet, ThermoShiftReportDto report, int headerRow)
    {
        var used = report.Packaging
            .OrderByDescending(p => p.Quantity)
            .Take(PackagingColumns)
            .ToList();

        for (var i = 0; i < PackagingColumns; i++)
        {
            var column = (char)('A' + i);
            var header = sheet.Cell(headerRow, column.ToString());
            var count = sheet.Cell(headerRow + 1, column.ToString());
            var weight = sheet.Cell(headerRow + 3, column.ToString());

            if (i >= used.Count)
            {
                header.Value = string.Empty;
                count.Value = "-";
                weight.Value = "-";
                continue;
            }

            header.Value = used[i].MaterialName;
            count.Value = used[i].Quantity;
            weight.Value = used[i].Weight is null ? "-" : (XLCellValue)used[i].Weight!.Value;
        }
    }

    private static XLCellValue Number(decimal? value) =>
        value is null ? Blank.Value : (XLCellValue)value.Value;

    /// <summary>"08:25" as an Excel time, so the form's own time format shows it.</summary>
    private static XLCellValue Time(string? hhmm) =>
        TimeSpan.TryParseExact(hhmm, @"hh\:mm", CultureInfo.InvariantCulture, out var time)
            ? time
            : Blank.Value;

    private static string Plain(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture);
}
