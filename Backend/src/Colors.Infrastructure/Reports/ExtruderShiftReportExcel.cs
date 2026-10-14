using System.Globalization;
using ClosedXML.Excel;
using Colors.Application.Features.Reports;

namespace Colors.Infrastructure.Reports;

/// <summary>
/// Writes the extruder's shift report as an Excel file (specification section 13.1).
///
/// The factory has no paper form for the extruder, so this is laid out from scratch: the
/// shift at the top, one row per roll with everything recorded against it, the totals, and
/// the shift's rolls by product. Headings are in English and Arabic, as the people reading
/// it are both.
///
/// The averages, the absorbency and the totals are formulas rather than numbers, so
/// somebody correcting a reading in Excel sees them follow — the same choice the thermo
/// form makes.
/// </summary>
public class ExtruderShiftReportExcel : IExtruderShiftReportExcel
{
    /// <summary>Where the roll table starts; the shift's details sit above it.</summary>
    public const int HeaderRow = 7;

    private static readonly XLColor HeaderFill = XLColor.FromHtml("#E8EEF7");
    private static readonly XLColor TotalFill = XLColor.FromHtml("#F3F4F6");
    private static readonly XLColor OutOfSpecFill = XLColor.FromHtml("#FDECEC");

    // The roll table's columns, in order. The letters are fixed so the formulas can name them.
    private static readonly (string Letter, string English, string Arabic, double Width)[] Columns =
    [
        ("A", "No.", "الرقم", 6),
        ("B", "Roll code", "رمز الرول", 24),
        ("C", "Out at", "وقت الخروج", 9),
        ("D", "Batch", "الخلطة", 8),
        ("E", "Recipe", "الوصفة", 16),
        ("F", "Made for", "المنتج", 22),
        ("G", "Colour", "اللون", 10),
        ("H", "Status", "الحالة", 13),
        ("I", "Weight (kg)", "الوزن", 10),
        ("J", "Length", "الطول", 9),
        ("K", "Plate (g)", "وزن الصحن", 9),
        ("L", "RS", "RS", 7),
        ("M", "RM", "RM", 7),
        ("N", "LM", "LM", 7),
        ("O", "LS", "LS", 7),
        ("P", "Avg thickness", "متوسط السماكة", 10),
        ("Q", "Range", "المدى", 11),
        ("R", "In spec", "ضمن المواصفة", 9),
        ("S", "Dry (g)", "قبل الماء", 9),
        ("T", "Wet (g)", "بعد الماء", 9),
        ("U", "Absorbency %", "نسبة الامتصاص", 11),
        ("V", "Made by", "أنتجه", 18),
        ("W", "Measured by", "قاسه", 18),
        ("X", "Tested by", "فحصه", 18),
        ("Y", "Formed on", "شُكّل في", 16),
        ("Z", "Notes", "ملاحظات", 30),
    ];

    private static readonly Dictionary<string, string> StatusWords = new()
    {
        ["NeedsTest"] = "Needs test",
        ["Available"] = "Available",
        ["InThermo"] = "In thermo",
        ["Processed"] = "Processed",
        ["Scrapped"] = "Scrapped",
    };

    public byte[] Build(ExtruderShiftReportDto report, TimeSpan utcOffset)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Extruder");

        WriteHeading(sheet, report);

        // ---- the roll table ----------------------------------------------------------
        foreach (var (letter, english, arabic, width) in Columns)
        {
            var cell = sheet.Cell($"{letter}{HeaderRow}");
            cell.Value = $"{english}\n{arabic}";
            sheet.Column(letter).Width = width;
        }

        var header = sheet.Range($"A{HeaderRow}:Z{HeaderRow}");
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = HeaderFill;
        header.Style.Alignment.WrapText = true;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        sheet.Row(HeaderRow).Height = 32;

        var row = HeaderRow + 1;
        foreach (var roll in report.Rolls)
        {
            WriteRoll(sheet, row, roll, utcOffset);
            row++;
        }

        var first = HeaderRow + 1;
        var last = Math.Max(first, row - 1);

        // ---- totals ------------------------------------------------------------------
        var totals = row;
        sheet.Cell($"A{totals}").Value = "Total · المجموع";
        sheet.Range($"A{totals}:B{totals}").Merge();
        sheet.Cell($"C{totals}").FormulaA1 = $"COUNTA(B{first}:B{last})";
        sheet.Cell($"D{totals}").Value = "rolls";
        sheet.Cell($"I{totals}").FormulaA1 = $"SUM(I{first}:I{last})";
        sheet.Cell($"J{totals}").FormulaA1 = $"SUM(J{first}:J{last})";
        sheet.Cell($"P{totals}").FormulaA1 = $"IFERROR(ROUND(AVERAGE(P{first}:P{last}),3),\"\")";
        var totalRange = sheet.Range($"A{totals}:Z{totals}");
        totalRange.Style.Font.Bold = true;
        totalRange.Style.Fill.BackgroundColor = TotalFill;

        var table = sheet.Range($"A{HeaderRow}:Z{totals}");
        table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        table.Style.Border.InsideBorderColor = XLColor.FromHtml("#D1D5DB");

        sheet.Range($"I{first}:J{totals}").Style.NumberFormat.Format = "#,##0.###";
        sheet.Range($"K{first}:P{totals}").Style.NumberFormat.Format = "0.###";
        sheet.Range($"S{first}:U{totals}").Style.NumberFormat.Format = "0.###";

        // ---- by product --------------------------------------------------------------
        var summaryRow = totals + 2;
        WriteProducts(sheet, summaryRow, report);

        // ---- notes -------------------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(report.Notes))
        {
            var notesRow = summaryRow + report.Products.Count + 3;
            sheet.Cell($"A{notesRow}").Value = "Notes · ملاحظات";
            sheet.Cell($"A{notesRow}").Style.Font.Bold = true;
            sheet.Cell($"A{notesRow + 1}").Value = report.Notes;
            sheet.Range($"A{notesRow + 1}:Z{notesRow + 1}").Merge().Style.Alignment.WrapText = true;
            sheet.Row(notesRow + 1).Height = 45;
        }

        // ---- reading and printing ------------------------------------------------------
        sheet.SheetView.FreezeRows(HeaderRow);
        sheet.SheetView.FreezeColumns(2);
        sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        sheet.PageSetup.PaperSize = XLPaperSize.A4Paper;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(HeaderRow, HeaderRow);
        sheet.PageSetup.Margins.Left = 0.3;
        sheet.PageSetup.Margins.Right = 0.3;
        sheet.Style.Font.FontName = "Calibri";
        sheet.Style.Font.FontSize = 10;

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteHeading(IXLWorksheet sheet, ExtruderShiftReportDto report)
    {
        sheet.Cell("A1").Value = "COLORS — PAPER & PLASTIC";
        sheet.Cell("A1").Style.Font.FontSize = 9;
        sheet.Cell("A1").Style.Font.FontColor = XLColor.Gray;

        sheet.Cell("A2").Value = "Extruder shift report · تقرير البثق للوردية";
        sheet.Cell("A2").Style.Font.Bold = true;
        sheet.Cell("A2").Style.Font.FontSize = 15;

        Label(sheet, "A3", "Date · التاريخ", report.ProductionDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
        Label(sheet, "A4", "Shift · الوردية", report.ShiftName);
        Label(sheet, "A5", "Line · الخط", report.LineName);

        Label(sheet, "F3", "Operator · المشغّل", report.OperatorName is null
            ? "—"
            : $"{report.OperatorName}{(report.OperatorEmployeeNumber is null ? "" : $" ({report.OperatorEmployeeNumber})")}");
        Label(sheet, "F4", "Hours · ساعات العمل",
            $"{report.ProductionStartTime ?? "—"} – {report.ProductionEndTime ?? "—"}"
            + (report.DowntimeHours is null ? "" : $", {Number(report.DowntimeHours)} h down")
            + (report.ActualProductionHours is null ? "" : $", {Number(report.ActualProductionHours)} h producing"));
        Label(sheet, "F5", "Batch · الخلطة", report.BatchNumbers.Count == 0
            ? "—"
            : string.Join(", ", report.BatchNumbers));

        Label(sheet, "K3", "Crew · الطاقم", report.Workers.Count == 0 ? "—" : string.Join(", ", report.Workers));
        Label(sheet, "K4", "Trainees · المتدربون", report.Trainees.Count == 0 ? "—" : string.Join(", ", report.Trainees));
        Label(sheet, "K5", "Status · الحالة", report.Status);

        Label(sheet, "S3", "Rolls · الرولات", report.RollCount.ToString(CultureInfo.InvariantCulture));
        Label(sheet, "S4", "Measured / tested", $"{report.MeasuredCount} / {report.TestedCount}");
        Label(sheet, "S5", "Out of spec · خارج المواصفة", report.OutOfSpecCount.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>A caption in grey and its value in bold beside it, two cells wide.</summary>
    private static void Label(IXLWorksheet sheet, string address, string caption, string value)
    {
        var cell = sheet.Cell(address);
        cell.Value = caption;
        cell.Style.Font.FontColor = XLColor.Gray;

        var valueCell = cell.CellRight(2);
        valueCell.Value = value;
        valueCell.Style.Font.Bold = true;
    }

    private static void WriteRoll(IXLWorksheet sheet, int row, ExtruderReportRollDto roll, TimeSpan utcOffset)
    {
        sheet.Cell($"A{row}").Value = roll.RollNumber;
        sheet.Cell($"B{row}").Value = roll.RollCode;
        sheet.Cell($"B{row}").Style.Font.FontName = "Consolas";
        sheet.Cell($"C{row}").Value = roll.ProducedAt.ToOffset(utcOffset)
            .ToString("HH:mm", CultureInfo.InvariantCulture);
        sheet.Cell($"D{row}").Value = roll.BatchNumber;
        sheet.Cell($"E{row}").Value = $"{roll.RecipeNumber} {roll.RecipeFamilyName}";
        sheet.Cell($"F{row}").Value = roll.ProductName ?? "—";
        sheet.Cell($"G{row}").Value = roll.ColorName;
        sheet.Cell($"H{row}").Value = StatusWords.GetValueOrDefault(roll.Status, roll.Status);

        Put(sheet.Cell($"I{row}"), roll.Weight);
        Put(sheet.Cell($"J{row}"), roll.Length);
        Put(sheet.Cell($"K{row}"), roll.PlateWeight);
        Put(sheet.Cell($"L{row}"), roll.ThicknessRs);
        Put(sheet.Cell($"M{row}"), roll.ThicknessRm);
        Put(sheet.Cell($"N{row}"), roll.ThicknessLm);
        Put(sheet.Cell($"O{row}"), roll.ThicknessLs);
        // Worked out in the sheet, once all four readings are there.
        sheet.Cell($"P{row}").FormulaA1 = $"IF(COUNT(L{row}:O{row})=4,ROUND(AVERAGE(L{row}:O{row}),3),\"\")";

        sheet.Cell($"Q{row}").Value = Range(roll.MinThickness, roll.MaxThickness);
        // The verdict kept when it was tested, not worked out again from today's range.
        sheet.Cell($"R{row}").Value = roll.ThicknessInSpec switch
        {
            true => "Yes",
            false => "No",
            null => "",
        };
        if (roll.ThicknessInSpec == false)
        {
            sheet.Range($"P{row}:R{row}").Style.Fill.BackgroundColor = OutOfSpecFill;
        }

        if (roll.IsAbsorbent)
        {
            Put(sheet.Cell($"S{row}"), roll.AbsorbencyDryWeight);
            Put(sheet.Cell($"T{row}"), roll.AbsorbencyWetWeight);
            sheet.Cell($"U{row}").FormulaA1 =
                $"IF(AND(ISNUMBER(S{row}),ISNUMBER(T{row}),S{row}>0),ROUND((T{row}-S{row})/S{row}*100,2),\"\")";
        }
        else
        {
            sheet.Range($"S{row}:U{row}").Style.Font.FontColor = XLColor.Gray;
            sheet.Cell($"U{row}").Value = "n/a";
        }

        sheet.Cell($"V{row}").Value = roll.ProducedByName;
        sheet.Cell($"W{row}").Value = roll.MeasuredByName ?? "";
        sheet.Cell($"X{row}").Value = roll.TestedByName ?? "";
        sheet.Cell($"Y{row}").Value = roll.FormedOnDate is null
            ? ""
            : $"{roll.FormedOnDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)} shift {roll.FormedOnShiftName}";
        sheet.Cell($"Z{row}").Value = string.Join(" · ", new[] { roll.Notes, roll.TestNotes }
            .Where(n => !string.IsNullOrWhiteSpace(n)));
    }

    private static void WriteProducts(IXLWorksheet sheet, int row, ExtruderShiftReportDto report)
    {
        sheet.Cell($"A{row}").Value = "By product · حسب المنتج";
        sheet.Cell($"A{row}").Style.Font.Bold = true;

        var head = row + 1;
        string[] captions =
        [
            "Product\nالمنتج", "", "Rolls\nالرولات", "Weight (kg)\nالوزن", "Length\nالطول",
            "Avg thickness\nمتوسط السماكة", "Out of spec\nخارج المواصفة",
        ];
        string[] letters = ["B", "C", "D", "E", "F", "G", "H"];
        for (var i = 0; i < captions.Length; i++)
        {
            sheet.Cell($"{letters[i]}{head}").Value = captions[i];
        }

        sheet.Range($"B{head}:C{head}").Merge();
        var headRange = sheet.Range($"B{head}:H{head}");
        headRange.Style.Font.Bold = true;
        headRange.Style.Fill.BackgroundColor = HeaderFill;
        headRange.Style.Alignment.WrapText = true;
        sheet.Row(head).Height = 32;

        var line = head + 1;
        foreach (var product in report.Products)
        {
            sheet.Cell($"B{line}").Value = product.ProductName;
            sheet.Range($"B{line}:C{line}").Merge();
            sheet.Cell($"D{line}").Value = product.RollCount;
            sheet.Cell($"E{line}").Value = product.TotalWeight;
            sheet.Cell($"F{line}").Value = product.TotalLength;
            Put(sheet.Cell($"G{line}"), product.AverageThickness);
            sheet.Cell($"H{line}").Value = product.OutOfSpecCount;
            line++;
        }

        var all = sheet.Range($"B{head}:H{Math.Max(head, line - 1)}");
        all.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        all.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        all.Style.Border.InsideBorderColor = XLColor.FromHtml("#D1D5DB");
        sheet.Range($"E{head + 1}:G{Math.Max(head + 1, line - 1)}").Style.NumberFormat.Format = "#,##0.###";
    }

    /// <summary>A number, or an empty cell — never a zero standing in for "not taken".</summary>
    private static void Put(IXLCell cell, decimal? value)
    {
        if (value is not null)
        {
            cell.Value = value.Value;
        }
    }

    private static string Range(decimal? min, decimal? max) => (min, max) switch
    {
        ({ } low, { } high) => $"{Number(low)}–{Number(high)}",
        ({ } low, null) => $"≥ {Number(low)}",
        (null, { } high) => $"≤ {Number(high)}",
        _ => "",
    };

    private static string Number(decimal? value) =>
        value?.ToString("0.###", CultureInfo.InvariantCulture) ?? "";
}
