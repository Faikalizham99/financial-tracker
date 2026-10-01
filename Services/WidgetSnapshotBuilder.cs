using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class WidgetSnapshotBuilder
{
    private const int SchemaVersion = 3;

    public static WidgetSnapshot Build(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency,
        bool includeInvestment,
        DateTime month,
        MonthlyBudgetRecord? budget,
        DateTimeOffset updatedAt)
    {
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);
        var withInvestment = BuildSummary(
            records,
            currency.Symbol,
            includeInvestment: true,
            normalizedMonth,
            budget);
        var withoutInvestment = BuildSummary(
            records,
            currency.Symbol,
            includeInvestment: false,
            normalizedMonth,
            budget);
        var selectedSummary = includeInvestment
            ? withInvestment
            : withoutInvestment;
        var transactionCount = records.Count(record =>
            record.TransactionDate.Year == normalizedMonth.Year &&
            record.TransactionDate.Month == normalizedMonth.Month);

        return new WidgetSnapshot(
            SchemaVersion,
            normalizedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            selectedSummary.AvailableText,
            selectedSummary.IncomeText,
            selectedSummary.ExpenseText,
            transactionCount,
            selectedSummary.HasBudget,
            selectedSummary.BudgetSpentText,
            selectedSummary.BudgetLimitText,
            selectedSummary.BudgetUsageText,
            selectedSummary.BudgetRemainingText,
            selectedSummary.BudgetProgress,
            includeInvestment,
            withInvestment,
            withoutInvestment,
            updatedAt.ToUnixTimeSeconds());
    }

    private static WidgetSummary BuildSummary(
        IReadOnlyList<TransactionRecord> records,
        string currencySymbol,
        bool includeInvestment,
        DateTime normalizedMonth,
        MonthlyBudgetRecord? budget)
    {
        long incomeMinor = 0;
        long budgetIncomeMinor = 0;
        long expenseMinor = 0;

        foreach (var record in records)
        {
            var transactionDate = record.TransactionDate.Date;
            if (transactionDate.Year != normalizedMonth.Year ||
                transactionDate.Month != normalizedMonth.Month)
            {
                continue;
            }

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
