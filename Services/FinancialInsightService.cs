using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class FinancialInsightService
{
    public static IReadOnlyList<FinancialInsightItem> BuildTransactionInsights(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption currency,
        DateTime today,
        bool includeInvestment)
    {
        today = today.Date;
        var currentMonth = new DateTime(today.Year, today.Month, 1);
        var previousMonth = currentMonth.AddMonths(-1);
        var comparableDay = Math.Min(
            today.Day,
            DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));
        var currentExpenses = records
            .Where(record => IsInMonth(record, currentMonth) &&
                TransactionCatalog.IsExpenseType(record.Type) &&
                (includeInvestment || !IsInvestment(record)))
            .ToList();
        var currentIncome = records
            .Where(record => IsInMonth(record, currentMonth) &&
                TransactionCatalog.IsIncomeType(record.Type))
            .ToList();
        var expenseThroughToday = currentExpenses
            .Where(record => record.TransactionDate.Date <= today)
            .Sum(record => record.AmountMinor);
        var previousExpenseThroughComparableDay = records
            .Where(record => IsInMonth(record, previousMonth) &&
                record.TransactionDate.Day <= comparableDay &&
                TransactionCatalog.IsExpenseType(record.Type) &&
                (includeInvestment || !IsInvestment(record)))
            .Sum(record => record.AmountMinor);
        var insights = new List<FinancialInsightItem>(4);

        AddSpendingPaceInsight(
            insights,
            expenseThroughToday,
            previousExpenseThroughComparableDay,
            currency.Symbol,
            today);
        AddTopCategoryInsight(insights, currentExpenses, currency.Symbol);
        AddCashFlowInsight(insights, currentExpenses, currentIncome, currency.Symbol);
        AddHighestSpendingDayInsight(insights, currentExpenses, currency.Symbol);

        if (insights.Count == 0)
        {
            insights.Add(new FinancialInsightItem(
                "Start your monthly picture",
                currentIncome.Count > 0
                    ? "Income is recorded, but there are no expenses yet this month."
                    : "Record a transaction to start receiving spending and cash-flow insights."));
        }

        return insights;
    }

    public static IReadOnlyList<FinancialInsightItem> BuildAssetInsights(
        IEnumerable<AssetComparisonItem> source,
        long totalMinor,
        long accessibleTotalMinor,
        string currencySymbol,
        bool hasPreviousSnapshot)
    {
        var comparisons = source.ToList();
        var insights = new List<FinancialInsightItem>(4);
        var largest = comparisons
            .Where(item => item.CurrentAmountMinor > 0)
            .OrderByDescending(item => item.CurrentAmountMinor)
            .FirstOrDefault();
        if (largest is not null)
        {
            var share = totalMinor > 0
                ? largest.CurrentAmountMinor / (double)totalMinor
                : 0;
            insights.Add(new FinancialInsightItem(
                "Largest holding",
                $"{largest.Asset.DisplayName} is {share:P0} of the displayed portfolio at " +
                $"{MoneyFormatter.FormatMinor(largest.CurrentAmountMinor, currencySymbol)}."));
        }

        if (hasPreviousSnapshot)
        {
            var biggestIncrease = comparisons
                .Where(item => item.ChangeMinor > 0 && !item.IsNew)
                .OrderByDescending(item => item.ChangeMinor)
                .FirstOrDefault();
            if (biggestIncrease is not null)
            {
                insights.Add(new FinancialInsightItem(
                    "Biggest increase",
                    $"{biggestIncrease.Asset.DisplayName} increased by " +
                    FormatAssetChange(biggestIncrease, currencySymbol) + "."));
            }

            var biggestDecrease = comparisons
                .Where(item => item.ChangeMinor < 0)
                .OrderBy(item => item.ChangeMinor)
                .FirstOrDefault();
            if (biggestDecrease is not null)
            {
                insights.Add(new FinancialInsightItem(
                    "Biggest decrease",
                    $"{biggestDecrease.Asset.DisplayName} decreased by " +
                    FormatAssetChange(biggestDecrease, currencySymbol, useAbsoluteValue: true) + "."));
            }

            if (biggestIncrease is null && biggestDecrease is null)
            {
                insights.Add(new FinancialInsightItem(
                    "Portfolio unchanged",
                    "Recorded asset values are unchanged from the previous snapshot."));
            }
        }
        else if (comparisons.Count > 0)
        {
            insights.Add(new FinancialInsightItem(
                "Baseline established",
                "Add another monthly snapshot to reveal increases, decreases and portfolio drivers."));
        }

        if (totalMinor > 0)
        {
            var accessibleShare = Math.Clamp(
                accessibleTotalMinor / (double)totalMinor,
                0,
                1);
            insights.Add(new FinancialInsightItem(
                "Accessible share",
                $"Accessible assets represent {accessibleShare:P0} of the displayed portfolio, or " +
                $"{MoneyFormatter.FormatMinor(accessibleTotalMinor, currencySymbol)}."));
        }

        return insights.Count > 0
            ? insights
            : [new FinancialInsightItem(
                "No asset values yet",
                "Enter asset values to start receiving portfolio insights.")];
    }

    private static void AddSpendingPaceInsight(
        ICollection<FinancialInsightItem> insights,
        long currentMinor,
        long previousMinor,
        string currencySymbol,
        DateTime today)
    {
        if (previousMinor <= 0)
        {
            return;
        }

        var difference = currentMinor - previousMinor;
        var percentage = Math.Abs(difference) / (double)previousMinor;
        if (percentage < 0.01)
        {
            insights.Add(new FinancialInsightItem(
                "Spending pace is steady",
                $"Spending through {today:d MMM} is nearly unchanged from the same point last month."));
            return;
        }

        var direction = difference > 0 ? "higher" : "lower";
        insights.Add(new FinancialInsightItem(
            difference > 0 ? "Spending pace increased" : "Spending pace decreased",
            $"Spending is {percentage:P0} {direction} than at the same point last month, a difference of " +
            $"{MoneyFormatter.FormatMinor(Math.Abs(difference), currencySymbol)}."));
    }

    private static void AddTopCategoryInsight(
        ICollection<FinancialInsightItem> insights,
        IReadOnlyCollection<TransactionRecord> expenses,
        string currencySymbol)
    {
        var total = expenses.Sum(record => record.AmountMinor);
        if (total <= 0)
        {
            return;
        }

        var top = expenses
            .GroupBy(record => TransactionCatalog.GetCategory(record.Category, isIncome: false))
            .Select(group => new
            {
                Category = group.Key,
                AmountMinor = group.Sum(record => record.AmountMinor)
            })
            .OrderByDescending(item => item.AmountMinor)
            .First();
        insights.Add(new FinancialInsightItem(
            "Largest expense category",
            $"{top.Category.Title} accounts for {top.AmountMinor / (double)total:P0} of this month's spending at " +
            $"{MoneyFormatter.FormatMinor(top.AmountMinor, currencySymbol)}."));
    }

    private static void AddCashFlowInsight(
        ICollection<FinancialInsightItem> insights,
        IReadOnlyCollection<TransactionRecord> expenses,
        IReadOnlyCollection<TransactionRecord> income,
        string currencySymbol)
    {
        var incomeTotal = income.Sum(record => record.AmountMinor);
        if (incomeTotal <= 0)
        {
            return;
        }

        var expenseTotal = expenses.Sum(record => record.AmountMinor);
        var net = incomeTotal - expenseTotal;
        if (net >= 0)
        {
            insights.Add(new FinancialInsightItem(
                "Positive monthly cash flow",
                $"Income exceeds expenses by {MoneyFormatter.FormatMinor(net, currencySymbol)}, " +
                $"leaving {net / (double)incomeTotal:P0} of recorded income unspent."));
        }
        else
        {
            insights.Add(new FinancialInsightItem(
                "Expenses exceed income",
                $"Recorded expenses are {MoneyFormatter.FormatMinor(-net, currencySymbol)} above this month's income."));
        }
    }

    private static void AddHighestSpendingDayInsight(
        ICollection<FinancialInsightItem> insights,
        IReadOnlyCollection<TransactionRecord> expenses,
        string currencySymbol)
    {
        var dailyTotals = expenses
            .GroupBy(record => record.TransactionDate.Date)
            .Select(group => new
            {
                Date = group.Key,
                AmountMinor = group.Sum(record => record.AmountMinor)
            })
            .OrderByDescending(item => item.AmountMinor)
            .ToList();
        if (dailyTotals.Count < 2)
        {
            return;
        }

        var highest = dailyTotals[0];
        insights.Add(new FinancialInsightItem(
            "Highest-spending day",
            $"{highest.Date.ToString("d MMM", CultureInfo.CurrentCulture)} had the highest expense total at " +
            $"{MoneyFormatter.FormatMinor(highest.AmountMinor, currencySymbol)}."));
    }

    private static bool IsInMonth(TransactionRecord record, DateTime month) =>
        record.TransactionDate.Year == month.Year &&
        record.TransactionDate.Month == month.Month;

    private static bool IsInvestment(TransactionRecord record) =>
        record.Category.Equals(
            TransactionCatalog.InvestmentCategoryKey,
            StringComparison.OrdinalIgnoreCase);

    private static string FormatAssetChange(
        AssetComparisonItem item,
        string currencySymbol,
        bool useAbsoluteValue = false)
    {
        var amount = item.ChangeMinor ?? 0;
        if (useAbsoluteValue)
        {
            amount = Math.Abs(amount);
        }

        var formatted = MoneyFormatter.FormatMinor(amount, currencySymbol);
        return item.ChangePercentage.HasValue
            ? $"{formatted} ({Math.Abs(item.ChangePercentage.Value):0.0}%)"
            : formatted;
    }
}
