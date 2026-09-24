using FinancialTracker.Helpers;

namespace FinancialTracker.Models;

public sealed class BudgetHistoryMonthItem
{
    public required DateTime Month { get; init; }
    public required string MonthName { get; init; }
    public required string IncludingInvestmentText { get; init; }
    public required string ExcludingInvestmentText { get; init; }
    public bool HasBudget { get; init; }
    public bool IsCurrentMonth { get; init; }
    public double ContentOpacity => HasBudget ? 1 : 0.48;

    public static BudgetHistoryMonthItem Create(
        DateTime month,
        MonthlyBudgetRecord? budget,
        string currencySymbol)
    {
        var hasBudget = budget is not null;
        return new BudgetHistoryMonthItem
        {
            Month = month,
            MonthName = month.ToString("MMM"),
            HasBudget = hasBudget,
            IsCurrentMonth = month.Year == DateTime.Today.Year &&
                month.Month == DateTime.Today.Month,
            IncludingInvestmentText = hasBudget
                ? MoneyFormatter.FormatMinor(
                    budget!.BudgetIncludingInvestmentMinor,
                    currencySymbol)
                : "Not set",
            ExcludingInvestmentText = hasBudget
                ? MoneyFormatter.FormatMinor(
                    budget!.BudgetExcludingInvestmentMinor,
                    currencySymbol)
                : "Not set"
        };
    }
}
