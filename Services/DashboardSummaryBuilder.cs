using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class DashboardSummaryBuilder
{
    public static DashboardSummary Build(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency,
        bool canModifyTransactions,
        bool includeInvestment,
        DateTime today)
    {
        today = today.Date;
        var expenseRecords = new List<TransactionRecord>();
        var incomeRecords = new List<TransactionRecord>();
        long totalExpenseMinor = 0;
        long totalIncomeMinor = 0;
        long expenseThroughTodayMinor = 0;
        long spentTodayMinor = 0;

        foreach (var record in records)
        {
            var transactionDate = record.TransactionDate.Date;
            var isExpense = TransactionCatalog.IsExpenseType(record.Type);
            if (transactionDate.Year == today.Year &&
                transactionDate.Month == today.Month)
            {
                if (isExpense)
                {
                    expenseRecords.Add(record);
                    totalExpenseMinor += record.AmountMinor;
                    if (transactionDate <= today)
                    {
                        expenseThroughTodayMinor += record.AmountMinor;
                    }

                    if (transactionDate == today)
                    {
                        spentTodayMinor += record.AmountMinor;
                    }
                }
                else if (TransactionCatalog.IsIncomeType(record.Type))
                {
                    incomeRecords.Add(record);
                    totalIncomeMinor += record.AmountMinor;
                }
            }
        }

        var dailyAverageMinor = (long)Math.Round(
            expenseThroughTodayMinor / (double)today.Day);

        var recentCount = Math.Min(records.Count, 3);
        var recentActivity = new List<TransactionActivityItem>(recentCount);
        for (var index = 0; index < recentCount; index++)
        {
            recentActivity.Add(TransactionActivityItem.FromRecord(
                records[index],
                selectedCurrency.Symbol,
                index < recentCount - 1,
                canModifyTransaction: canModifyTransactions));
        }

        return new DashboardSummary(
            MoneyFormatter.FormatMinor(spentTodayMinor, selectedCurrency.Symbol),
            MoneyFormatter.FormatMinor(dailyAverageMinor, selectedCurrency.Symbol),
            (DateTime.DaysInMonth(today.Year, today.Month) - today.Day)
                .ToString(CultureInfo.InvariantCulture),
            MoneyFormatter.FormatMinor(totalExpenseMinor, selectedCurrency.Symbol),
            MoneyFormatter.FormatMinor(totalIncomeMinor, selectedCurrency.Symbol),
            FinancialInsightService.BuildTransactionInsights(
                records,
                selectedCurrency,
                today,
                includeInvestment),
            BuildCategorySummary(
                TransactionCatalog.ExpenseCategories,
                expenseRecords,
                totalExpenseMinor,
                selectedCurrency.Symbol,
                isIncome: false),
            BuildCategorySummary(
                TransactionCatalog.IncomeCategories,
                incomeRecords,
                totalIncomeMinor,
                selectedCurrency.Symbol,
                isIncome: true),
            recentActivity);
    }

    private static IReadOnlyList<CategorySummaryItem> BuildCategorySummary(
        IReadOnlyList<TransactionOption> categories,
        IReadOnlyList<TransactionRecord> records,
        long totalMinor,
        string currencySymbol,
        bool isIncome)
    {
        var amountsByCategory = categories.ToDictionary(
            category => category.Key,
            _ => 0L,
            StringComparer.OrdinalIgnoreCase);

        foreach (var record in records)
        {
            var category = TransactionCatalog.GetCategory(record.Category, isIncome);
            amountsByCategory[category.Key] += record.AmountMinor;
        }

        return categories
            .Select((category, index) => new CategorySummaryItem(
                category,
                amountsByCategory[category.Key],
                totalMinor,
                currencySymbol,
                isIncome,
                index < categories.Count - 1))
            .ToList();
    }

}
