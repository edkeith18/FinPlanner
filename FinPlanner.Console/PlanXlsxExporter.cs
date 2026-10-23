using System.Globalization;
using ClosedXML.Excel;
using FinPlanner.Engine;

namespace FinPlanner.Console;

internal static class PlanXlsxExporter
{
    private const string AccountColor = "#008000";
    private const string ExpenseColor = "#F08080";
    private const string CurrencyFormat = "$#,##0;($#,##0)";
    private const double FilterClearance = 6;
    private const double HeaderVerticalPadding = 2;

    public static string Write(
        Plan plan,
        FileInfo scenarioFile)
    {
        if (plan.PlanYears.Count == 0)
        {
            throw new InvalidOperationException(plan.FailureReason ?? "The plan has no years to export.");
        }
        var timestamp = DateTime.Now.ToString(
            "yyyyMMdd_HHmmss",
            CultureInfo.InvariantCulture);
        var outputFileName =
            $"{Path.GetFileNameWithoutExtension(scenarioFile.Name)}_{timestamp}.xlsx";
        var outputPath = Path.Combine(
            scenarioFile.DirectoryName
                ?? Directory.GetCurrentDirectory(),
            outputFileName);

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Financial Plan");
        var firstYear = plan.PlanYears[0];
        var accountTotalColumn = 3 + firstYear.Accounts.Count;
        var firstExpenseColumn = accountTotalColumn + 1;
        var expenseTotalColumn = firstExpenseColumn + firstYear.Expenses.Count;
        var headers = new[] { "Year at start of 12 month period", "Age at start of 12 month period" }
            .Concat(firstYear.Accounts.Select(account => account.Name))
            .Append("Total Account Balances after 12 months")
            .Concat(firstYear.Expenses.Select(expense => expense.Name))
            .Append("Total Expenses for past 12 months")
            .ToList();

        for (var column = 0; column < headers.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        WritePlanRows(sheet, plan, accountTotalColumn, firstExpenseColumn, expenseTotalColumn);

        var lastRow = plan.PlanYears.Count + 1;
        FormatPlanSheet(sheet, lastRow, headers.Count, accountTotalColumn, firstExpenseColumn, expenseTotalColumn);
        SizeColumnsAndHeaders(workbook, sheet, headers, lastRow);

        CollapseDetailColumns(sheet, firstYear.Accounts.Count, firstYear.Expenses.Count, accountTotalColumn, firstExpenseColumn, expenseTotalColumn);
        workbook.SaveAs(outputPath);

        return outputPath;
    }

    private static void WritePlanRows(IXLWorksheet sheet, Plan plan, int accountTotalColumn,
        int firstExpenseColumn, int expenseTotalColumn)
    {
        for (var index = 0; index < plan.PlanYears.Count; index++)
        {
            var year = plan.PlanYears[index];
            var row = index + 2;
            sheet.Cell(row, 1).Value = year.EndingYear;
            sheet.Cell(row, 2).Value = year.EndingAge;
            var column = 3;
            foreach (var account in year.Accounts)
            {
                sheet.Cell(row, column++).Value = account.EndingBalance;
            }
            sheet.Cell(row, accountTotalColumn).Value = year.EndingBalance;
            column = firstExpenseColumn;
            foreach (var expense in year.Expenses)
            {
                sheet.Cell(row, column++).Value = expense.Amount;
            }
            sheet.Cell(row, expenseTotalColumn).Value = year.TotalExpenses;
        }

    }

    private static void FormatPlanSheet(IXLWorksheet sheet, int lastRow, int columnCount,
        int accountTotalColumn, int firstExpenseColumn, int expenseTotalColumn)
    {
        sheet.Range(1, 1, lastRow, columnCount).Style.Fill.PatternType = XLFillPatternValues.None;
        sheet.Range(1, 3, lastRow, accountTotalColumn).Style.Font.FontColor =
            XLColor.FromHtml(AccountColor);
        sheet.Range(1, firstExpenseColumn, lastRow, expenseTotalColumn).Style.Font.FontColor =
            XLColor.FromHtml(ExpenseColor);
        sheet.Range(2, 3, lastRow, columnCount).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Column(accountTotalColumn).Style.Font.Bold = true;
        sheet.Column(expenseTotalColumn).Style.Font.Bold = true;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Alignment.WrapText = true;
        sheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.SheetView.FreezeRows(1);
        sheet.SheetView.FreezeColumns(2);
        sheet.Range(1, 1, lastRow, columnCount).SetAutoFilter();
    }

    private static void SizeColumnsAndHeaders(XLWorkbook workbook, IXLWorksheet sheet,
        IReadOnlyList<string> headers, int lastRow)
    {
        sheet.Columns(1, headers.Count).AdjustToContents(2, lastRow);
        // Measure individual words using the actual header font, rather than character counts.
        // This temporary sheet is removed before saving the workbook.
        var sizingSheet = workbook.Worksheets.Add("Header sizing");
        for (var index = 0; index < headers.Count; index++)
        {
            // Treat this duration as one wrapping unit, including filter-clearance sizing.
            var words = headers[index].Replace("12 months", "12\u00a0months")
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var wordIndex = 0; wordIndex < words.Length; wordIndex++)
            {
                var cell = sizingSheet.Cell(wordIndex + 1, index + 1);
                cell.Style.Font = sheet.Cell(1, index + 1).Style.Font;
                cell.Value = words[wordIndex];
            }
            sizingSheet.Column(index + 1).AdjustToContents();
            // Centered labels need clearance on both sides so the filter button
            // at the right edge cannot overlap the longest word.
            sheet.Column(index + 1).Width = Math.Max(
                sheet.Column(index + 1).Width,
                sizingSheet.Column(index + 1).Width + FilterClearance);

            // Use the full width for upper lines. Only the bottom line shares space with the filter button.
            var lines = new List<string>();
            var line = string.Empty;
            var measuringCell = sizingSheet.Cell(1, index + 1);
            foreach (var word in words)
            {
                var candidate = line.Length == 0 ? word : $"{line} {word}";
                measuringCell.Value = candidate;
                sizingSheet.Column(index + 1).AdjustToContents(1, 1);
                if (line.Length > 0 && sizingSheet.Column(index + 1).Width >
                    sheet.Column(index + 1).Width)
                {
                    lines.Add(line);
                    line = word;
                }
                else
                {
                    line = candidate;
                }
            }
            lines.Add(line);
            // Move trailing words onto a final line only when the filter needs that space.
            measuringCell.Value = lines[^1];
            sizingSheet.Column(index + 1).AdjustToContents(1, 1);
            if (sizingSheet.Column(index + 1).Width > sheet.Column(index + 1).Width - FilterClearance)
            {
                var lastSpace = lines[^1].LastIndexOf(' ');
                if (lastSpace >= 0)
                {
                    var lastWord = lines[^1][(lastSpace + 1)..];
                    lines[^1] = lines[^1][..lastSpace];
                    lines.Add(lastWord);
                }
            }
            sheet.Cell(1, index + 1).Value = string.Join("\n", lines);
            measuringCell.Value = sheet.Cell(1, index + 1).Value;
        }
        // Explicit line breaks let the font engine measure the required height accurately.
        sizingSheet.Row(1).AdjustToContents();
        sheet.Row(1).Height = sizingSheet.Row(1).Height + HeaderVerticalPadding;
        sizingSheet.Delete();
    }

    private static void CollapseDetailColumns(IXLWorksheet sheet, int accountCount, int expenseCount,
        int accountTotalColumn, int firstExpenseColumn, int expenseTotalColumn)
    {
        // Summary columns remain visible and separate the two independent outline groups.
        if (accountCount > 0)
        {
            sheet.Columns(3, accountTotalColumn - 1).Group(collapse: true);
            sheet.Columns(3, accountTotalColumn - 1).Hide();
        }
        if (expenseCount > 0)
        {
            sheet.Columns(firstExpenseColumn, expenseTotalColumn - 1).Group(collapse: true);
            sheet.Columns(firstExpenseColumn, expenseTotalColumn - 1).Hide();
        }
    }
}
