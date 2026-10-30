using System.Globalization;
using ClosedXML.Excel;
using FinPlanner.Engine;

namespace FinPlanner.Console;

internal static class PlanXlsxExporter
{
    private const string AccountColor = "#8064A2";
    private const string ExpenseColor = "#F08080";
    private const string MandatoryIncomeColor = "#008080";
    private const string VoluntaryIncomeColor = "#0070C0";
    private const string TotalIncomeColor = "#008000";
    private const string CurrencyFormat = "$#,##0;($#,##0)";
    private const double FilterClearance = 6;
    private const double HeaderVerticalPadding = 2;

    public static string Write(
        IReadOnlyCollection<Plan> plans,
        FileInfo scenarioFile)
    {
        if (plans.Count == 0)
        {
            throw new ArgumentException("At least one plan is required to export.", nameof(plans));
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
        foreach (var plan in plans)
        {
            WritePlanSheet(workbook, plan);
            WriteTransfersSheet(workbook, plan);
        }
        workbook.SaveAs(outputPath);

        return outputPath;
    }

    private static void WriteTransfersSheet(XLWorkbook workbook, Plan plan)
    {
        var sheet = workbook.Worksheets.Add($"{plan.Name} transfers");
        string[] headers = ["Period", "Starting age", "Name", "Reason", "From", "To", "Requested", "Transferred"];
        for (var column = 0; column < headers.Length; column++)
            sheet.Cell(1, column + 1).Value = headers[column];
        var row = 2;
        foreach (var year in plan.PlanYears)
        foreach (var transfer in year.Transfers)
        {
            sheet.Cell(row, 1).Value = $"{year.StartingYear} -> {year.EndingYear}";
            sheet.Cell(row, 2).Value = year.StartingAge;
            sheet.Cell(row, 3).Value = transfer.Name;
            sheet.Cell(row, 4).Value = transfer.Reason.ToString();
            sheet.Cell(row, 5).Value = transfer.Reason == TransferReason.ExternalIncome
                ? "External" : transfer.FromAccount?.Name ?? "Expense cash";
            sheet.Cell(row, 6).Value = transfer.ToAccount?.Name ?? "Expense cash";
            sheet.Cell(row, 7).Value = transfer.RequestedAmount;
            sheet.Cell(row, 8).Value = transfer.Amount;
            row++;
        }
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Columns(7, 8).Style.NumberFormat.Format = CurrencyFormat;
        sheet.Columns(1, 8).AdjustToContents();
        sheet.SheetView.FreezeRows(1);
        sheet.Range(1, 1, Math.Max(1, row - 1), 8).SetAutoFilter();
    }

    private static void WritePlanSheet(XLWorkbook workbook, Plan plan)
    {
        if (plan.PlanYears.Count == 0)
        {
            throw new InvalidOperationException(plan.FailureReason ?? $"Plan '{plan.Name}' has no years to export.");
        }
        var sheet = workbook.Worksheets.Add(plan.Name);
        var firstYear = plan.PlanYears[0];
        var firstEarningsColumn = plan.IncomeNames.Count > 0 ? 4 + plan.IncomeNames.Count : 3;
        var externalIncomeTotalColumn = firstEarningsColumn - 1;
        var mandatoryTotalColumn = firstEarningsColumn + firstYear.AccountEarnings.Count;
        var transferTotalColumn = mandatoryTotalColumn + 1 + firstYear.Accounts.Count;
        var voluntaryTotalColumn = transferTotalColumn + 1 + firstYear.Accounts.Count;
        var incomeTotalColumn = voluntaryTotalColumn + 1;
        var firstExpenseColumn = incomeTotalColumn + 1;
        var expenseTotalColumn = firstExpenseColumn + firstYear.Expenses.Count;
        var firstAccountColumn = expenseTotalColumn + 1;
        var accountTotalColumn = firstAccountColumn + firstYear.Accounts.Count;
        var externalIncomeHeaders = plan.IncomeNames.Count > 0
            ? plan.IncomeNames.Append("Total External Income for period") : Enumerable.Empty<string>();
        var headers = new[] { "Year", "Age" }
            .Concat(externalIncomeHeaders)
            .Concat(firstYear.AccountEarnings.Select(income => income.Account.Name))
            .Append("Total Earned Income for period")
            .Concat(firstYear.Accounts.Select(account => account.Name))
            .Append("Total Mandatory Transfers for period")
            .Concat(firstYear.Accounts.Select(account => account.Name))
            .Append("Total Voluntary Transfers for period")
            .Append("Total Cash Flow for period")
            .Concat(firstYear.Expenses.Select(expense => expense.Name))
            .Append("Total Expenses for period")
            .Concat(firstYear.Accounts.Select(account => account.Name))
            .Append("Total Account Balances at end")
            .ToList();

        for (var column = 0; column < headers.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = headers[column];
        }

        WritePlanRows(sheet, plan, firstEarningsColumn, mandatoryTotalColumn, transferTotalColumn, voluntaryTotalColumn, incomeTotalColumn, firstExpenseColumn, expenseTotalColumn,
            firstAccountColumn, accountTotalColumn);

        var lastRow = plan.PlanYears.Count + 2;
        FormatPlanSheet(sheet, lastRow, headers.Count, firstEarningsColumn, mandatoryTotalColumn, transferTotalColumn, voluntaryTotalColumn, incomeTotalColumn, firstExpenseColumn,
            expenseTotalColumn, firstAccountColumn, accountTotalColumn);
        sheet.Range(2, 1, 2, headers.Count).Style.Font.Bold = true;
        sheet.Range(2, 1, 2, headers.Count).Style.Font.FontColor = XLColor.Black;
        sheet.Cell(2, 1).Style.Font.Bold = false;
        sheet.Cell(2, 2).Style.Font.Bold = false;
        sheet.Range(2, 1, 2, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Range(3, 1, lastRow, headers.Count).Style.Font.Bold = false;
        SizeColumnsAndHeaders(workbook, sheet, headers, lastRow);

        sheet.Row(1).InsertRowsAbove(1);
        sheet.Range(1, 1, 1, headers.Count).Merge().Value = "Each row covers a 12 month period";
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
        sheet.Row(1).Style.Alignment.WrapText = false;
        sheet.Row(1).Height = sheet.RowHeight;
        sheet.SheetView.FreezeRows(2);
        sheet.SheetView.FreezeColumns(2);
        sheet.Range(2, 1, lastRow + 1, headers.Count).SetAutoFilter();

        if (plan.IncomeNames.Count > 0)
            CollapseDetailColumns(sheet, 3, externalIncomeTotalColumn);
        CollapseDetailColumns(sheet, firstEarningsColumn, mandatoryTotalColumn);
        CollapseDetailColumns(sheet, mandatoryTotalColumn + 1, transferTotalColumn);
        CollapseDetailColumns(sheet, transferTotalColumn + 1, voluntaryTotalColumn);
        CollapseDetailColumns(sheet, 3, incomeTotalColumn);
        CollapseDetailColumns(sheet, firstExpenseColumn, expenseTotalColumn);
        CollapseDetailColumns(sheet, firstAccountColumn, accountTotalColumn);
    }

    private static void WritePlanRows(IXLWorksheet sheet, Plan plan, int firstEarningsColumn, int mandatoryTotalColumn, int transferTotalColumn,
        int voluntaryTotalColumn, int incomeTotalColumn,
        int firstExpenseColumn, int expenseTotalColumn, int firstAccountColumn, int accountTotalColumn)
    {
        var firstYear = plan.PlanYears[0];
        sheet.Cell(2, 1).Value = firstYear.StartingYear;
        sheet.Cell(2, 2).Value = firstYear.StartingAge;
        var accountColumn = firstAccountColumn;
        foreach (var account in firstYear.Accounts)
        {
            sheet.Cell(2, accountColumn++).Value = account.BeginningBalance;
        }
        sheet.Cell(2, accountTotalColumn).Value =
            firstYear.Accounts.Sum(account => account.BeginningBalance);

        for (var index = 0; index < plan.PlanYears.Count; index++)
        {
            var year = plan.PlanYears[index];
            var row = index + 3;
            sheet.Cell(row, 1).Value = $"{year.StartingYear} -> {year.EndingYear}";
            sheet.Cell(row, 2).Value = $"{year.StartingAge} -> {year.EndingAge}";
            var column = 3;
            if (plan.IncomeNames.Count > 0)
            {
                foreach (var name in plan.IncomeNames)
                    sheet.Cell(row, column++).Value = year.Transfers
                        .Where(transfer => transfer.Reason == TransferReason.ExternalIncome && transfer.Name == name)
                        .Sum(transfer => transfer.Amount);
                sheet.Cell(row, column++).Value = year.ExternalIncome;
            }
            column = firstEarningsColumn;
            foreach (var income in year.AccountEarnings)
            {
                sheet.Cell(row, column++).Value = income.Amount;
            }
            sheet.Cell(row, mandatoryTotalColumn).Value = year.AccountEarnings.Sum(income => income.Amount);
            column = mandatoryTotalColumn + 1;
            foreach (var account in year.Accounts)
            {
                sheet.Cell(row, column++).Value = year.Transfers
                    .Where(transfer => transfer.FromAccount?.Id == account.Id)
                    .Sum(transfer => transfer.MandatoryAmount);
            }
            sheet.Cell(row, transferTotalColumn).Value = year.Transfers.Sum(transfer => transfer.MandatoryAmount);
            column = transferTotalColumn + 1;
            foreach (var account in year.Accounts)
            {
                sheet.Cell(row, column++).Value = year.Transfers
                    .Where(transfer => transfer.FromAccount?.Id == account.Id)
                    .Sum(transfer => transfer.VoluntaryAmount);
            }
            sheet.Cell(row, voluntaryTotalColumn).Value = year.Transfers.Sum(transfer => transfer.VoluntaryAmount);
            sheet.Cell(row, incomeTotalColumn).Value = year.TotalCashFlow;
            column = firstExpenseColumn;
            foreach (var expense in year.Expenses)
            {
                sheet.Cell(row, column++).Value = expense.Amount;
            }
            sheet.Cell(row, expenseTotalColumn).Value = year.TotalExpenses;
            column = firstAccountColumn;
            foreach (var account in year.Accounts)
            {
                sheet.Cell(row, column++).Value = account.EndingBalance;
            }
            sheet.Cell(row, accountTotalColumn).Value = year.EndingBalance;
        }

    }

    private static void FormatPlanSheet(IXLWorksheet sheet, int lastRow, int columnCount,
        int firstEarningsColumn, int mandatoryTotalColumn, int transferTotalColumn, int voluntaryTotalColumn, int incomeTotalColumn,
        int firstExpenseColumn, int expenseTotalColumn,
        int firstAccountColumn, int accountTotalColumn)
    {
        sheet.Range(1, 1, lastRow, columnCount).Style.Fill.PatternType = XLFillPatternValues.None;
        sheet.Range(1, 3, lastRow, mandatoryTotalColumn).Style.Font.FontColor =
            XLColor.FromHtml(MandatoryIncomeColor);
        sheet.Range(1, mandatoryTotalColumn + 1, lastRow, voluntaryTotalColumn).Style.Font.FontColor =
            XLColor.FromHtml(VoluntaryIncomeColor);
        sheet.Range(1, incomeTotalColumn, lastRow, incomeTotalColumn).Style.Font.FontColor =
            XLColor.FromHtml(TotalIncomeColor);
        sheet.Range(1, firstAccountColumn, lastRow, accountTotalColumn).Style.Font.FontColor =
            XLColor.FromHtml(AccountColor);
        sheet.Range(1, firstExpenseColumn, lastRow, expenseTotalColumn).Style.Font.FontColor =
            XLColor.FromHtml(ExpenseColor);
        sheet.Range(2, 3, lastRow, columnCount).Style.NumberFormat.Format = CurrencyFormat;
        if (firstEarningsColumn > 3)
            sheet.Column(firstEarningsColumn - 1).Style.Font.Bold = true;
        sheet.Column(accountTotalColumn).Style.Font.Bold = true;
        sheet.Column(incomeTotalColumn).Style.Font.Bold = true;
        sheet.Column(mandatoryTotalColumn).Style.Font.Bold = true;
        sheet.Column(transferTotalColumn).Style.Font.Bold = true;
        sheet.Column(voluntaryTotalColumn).Style.Font.Bold = true;
        sheet.Column(expenseTotalColumn).Style.Font.Bold = true;
        sheet.Row(1).Style.Font.Bold = true;
        sheet.Row(1).Style.Alignment.WrapText = true;
        sheet.Row(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
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
                .Replace("for period", "for\u00a0period")
                .Replace("Balances at end", "Balances\u00a0at\u00a0end")
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

    private static void CollapseDetailColumns(IXLWorksheet sheet, int firstColumn, int totalColumn)
    {
        // The total stays outside its group; nested totals belong to their parent group.
        if (totalColumn > firstColumn)
        {
            sheet.Columns(firstColumn, totalColumn - 1).Group(collapse: true);
            sheet.Columns(firstColumn, totalColumn - 1).Hide();
        }
    }
}
