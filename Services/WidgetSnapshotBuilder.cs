using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class WidgetSnapshotBuilder
{
    private const int SchemaVersion = 4;
    public const int HistoryMonthCount = 12;

    public static WidgetSnapshot Build(
        IReadOnlyList<TransactionRecord> records,
        IReadOnlyList<MonthlyBudgetRecord> budgets,
        CurrencyOption currency,
        bool includeInvestment,
        DateTime currentMonth,
        DateTimeOffset updatedAt)
    {
        var normalizedCurrentMonth = new DateTime(
            currentMonth.Year,
            currentMonth.Month,
            1);
        var recordsByMonth = records
            .GroupBy(record => MonthKeyConverter.FromDate(record.TransactionDate))
            .ToDictionary(group => group.Key, group => (IReadOnlyList<TransactionRecord>)
                group.ToList());
        var budgetsByMonth = budgets.ToDictionary(budget => budget.MonthKey);
        var months = new List<WidgetMonthSnapshot>(HistoryMonthCount);

        for (var offset = HistoryMonthCount - 1; offset >= 0; offset--)
        {
            var month = normalizedCurrentMonth.AddMonths(-offset);
            var monthKey = MonthKeyConverter.FromDate(month);
            recordsByMonth.TryGetValue(monthKey, out var monthRecords);
            budgetsByMonth.TryGetValue(monthKey, out var budget);
            months.Add(BuildMonth(
                month,
                monthRecords ?? [],
                budget,
                currency.Symbol));
        }

        var current = months[^1];
        var selectedSummary = includeInvestment
            ? current.WithInvestment
            : current.WithoutInvestment;

        return new WidgetSnapshot(
            SchemaVersion,
            current.MonthText,
            selectedSummary.AvailableText,
            selectedSummary.IncomeText,
            selectedSummary.ExpenseText,
            current.TransactionCount,
            selectedSummary.HasBudget,
            selectedSummary.BudgetSpentText,
            selectedSummary.BudgetLimitText,
            selectedSummary.BudgetUsageText,
            selectedSummary.BudgetRemainingText,
            selectedSummary.BudgetProgress,
            includeInvestment,
            current.WithInvestment,
            current.WithoutInvestment,
            current.MonthKey,
            months,
            updatedAt.ToUnixTimeSeconds());
    }

    private static WidgetMonthSnapshot BuildMonth(
        DateTime month,
        IReadOnlyList<TransactionRecord> records,
        MonthlyBudgetRecord? budget,
        string currencySymbol) =>
        new(
            MonthKeyConverter.FromDate(month),
            month.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            records.Count,
            BuildSummary(
                records,
                currencySymbol,
                includeInvestment: true,
                budget),
            BuildSummary(
                records,
                currencySymbol,
                includeInvestment: false,
                budget));

    private static WidgetSummary BuildSummary(
        IReadOnlyList<TransactionRecord> records,
        string currencySymbol,
        bool includeInvestment,
        MonthlyBudgetRecord? budget)
    {
        long incomeMinor = 0;
        long budgetIncomeMinor = 0;
        long expenseMinor = 0;

        foreach (var record in records)
        {
            if (!includeInvestment &&
                record.Category.Equals(
                    TransactionCatalog.InvestmentCategoryKey,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (TransactionCatalog.IsIncomeType(record.Type))
            {
                incomeMinor += record.AmountMinor;
                if (!TransactionCatalog.IsIncomeExcludedFromBudgetUsage(record.Category))
                {
                    budgetIncomeMinor += record.AmountMinor;
                }
            }
            else if (TransactionCatalog.IsExpenseType(record.Type))
            {
                expenseMinor += record.AmountMinor;
            }
        }

        var budgetMinor = budget is null
            ? 0
            : includeInvestment
                ? budget.BudgetIncludingInvestmentMinor
                : budget.BudgetExcludingInvestmentMinor;
        var hasBudget = budgetMinor > 0;
        var netSpendingMinor = Math.Max(expenseMinor - budgetIncomeMinor, 0);
        var budgetUsageRatio = hasBudget
            ? (double)netSpendingMinor / budgetMinor
            : 0;
        var budgetRemainingMinor = budgetMinor - netSpendingMinor;

        return new WidgetSummary(
            MoneyFormatter.FormatMinor(
                incomeMinor - expenseMinor,
                currencySymbol,
                separateSign: true),
            MoneyFormatter.FormatMinor(incomeMinor, currencySymbol),
            MoneyFormatter.FormatMinor(expenseMinor, currencySymbol),
            hasBudget,
            MoneyFormatter.FormatMinor(netSpendingMinor, currencySymbol),
            MoneyFormatter.FormatMinor(budgetMinor, currencySymbol),
            $"{Math.Max(0, budgetUsageRatio * 100):0.#}% USED",
            budgetRemainingMinor >= 0
                ? $"{MoneyFormatter.FormatMinor(budgetRemainingMinor, currencySymbol)} left"
                : $"{MoneyFormatter.FormatMinor(-budgetRemainingMinor, currencySymbol)} over budget",
            Math.Clamp(budgetUsageRatio, 0, 1));
    }
}
