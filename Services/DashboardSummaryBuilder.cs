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
        DateTime today)
    {
        today = today.Date;
        var previousMonth = today.AddMonths(-1);
        var comparableDay = Math.Min(
            today.Day,
            DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));
        var expenseRecords = new List<TransactionRecord>();
        var incomeRecords = new List<TransactionRecord>();
        long previousExpenseMinor = 0;

        foreach (var record in records)
        {
            var transactionDate = record.TransactionDate;
            var isExpense = record.Type.Equals(
                "Expense",
                StringComparison.OrdinalIgnoreCase);
            if (transactionDate.Year == today.Year &&
                transactionDate.Month == today.Month)
            {
                if (isExpense)
                {
                    expenseRecords.Add(record);
                }
                else if (record.Type.Equals(
                             "Income",
                             StringComparison.OrdinalIgnoreCase))
                {
                    incomeRecords.Add(record);
                }
            }
            else if (isExpense &&
                     transactionDate.Year == previousMonth.Year &&
                     transactionDate.Month == previousMonth.Month &&
                     transactionDate.Day <= comparableDay)
            {
                previousExpenseMinor += record.AmountMinor;
            }
        }

        var totalExpenseMinor = expenseRecords.Sum(record => record.AmountMinor);
        var totalIncomeMinor = incomeRecords.Sum(record => record.AmountMinor);
        var expenseThroughTodayMinor = expenseRecords
            .Where(record => record.TransactionDate.Date <= today)
            .Sum(record => record.AmountMinor);
        var spentTodayMinor = expenseRecords
            .Where(record => record.TransactionDate.Date == today)
            .Sum(record => record.AmountMinor);
        var dailyAverageMinor = (long)Math.Round(
            expenseThroughTodayMinor / (double)today.Day);

        var recentRecords = records.Take(3).ToList();
        var recentActivity = recentRecords
            .Select((record, index) => TransactionActivityItem.FromRecord(
                record,
                selectedCurrency.Symbol,
                index < recentRecords.Count - 1,
                canModifyTransaction: canModifyTransactions))
            .ToList();

        return new DashboardSummary(
            MoneyFormatter.FormatMinor(spentTodayMinor, selectedCurrency.Symbol),
            MoneyFormatter.FormatMinor(dailyAverageMinor, selectedCurrency.Symbol),
            (DateTime.DaysInMonth(today.Year, today.Month) - today.Day)
                .ToString(CultureInfo.InvariantCulture),
            MoneyFormatter.FormatMinor(totalExpenseMinor, selectedCurrency.Symbol),
            MoneyFormatter.FormatMinor(totalIncomeMinor, selectedCurrency.Symbol),
            BuildMonthlyInsight(
                expenseRecords,
                incomeRecords,
                expenseThroughTodayMinor,
                previousExpenseMinor),
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

    private static string BuildMonthlyInsight(
        IReadOnlyList<TransactionRecord> expenseRecords,
        IReadOnlyList<TransactionRecord> incomeRecords,
        long expenseThroughTodayMinor,
        long previousExpenseMinor)
    {
        if (previousExpenseMinor > 0)
        {
            var differenceMinor = expenseThroughTodayMinor - previousExpenseMinor;
            var percentageDifference = Math.Abs(differenceMinor) / (double)previousExpenseMinor;
            if (percentageDifference < 0.01)
            {
                return "Your spending is nearly unchanged from this point last month.";
            }

            var direction = differenceMinor > 0 ? "higher" : "lower";
            return $"Spending is {percentageDifference:P0} {direction} than at this point last month.";
        }

        if (expenseRecords.Count > 0)
        {
            var topCategory = expenseRecords
                .GroupBy(record => TransactionCatalog.GetCategory(
                    record.Category,
                    isIncome: false))
                .Select(group => new
                {
                    Category = group.Key,
                    AmountMinor = group.Sum(record => record.AmountMinor)
                })
                .OrderByDescending(item => item.AmountMinor)
                .First();
            var totalExpenseMinor = expenseRecords.Sum(record => record.AmountMinor);
            var share = totalExpenseMinor > 0
                ? topCategory.AmountMinor / (double)totalExpenseMinor
                : 0;
            return $"{topCategory.Category.Title} is your largest expense at {share:P0} of this month\u2019s spending.";
        }

        return incomeRecords.Count > 0
            ? "You have recorded income this month and no expenses yet."
            : "Record a transaction to start receiving monthly spending insights.";
    }
}
