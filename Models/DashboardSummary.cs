namespace FinancialTracker.Models;

public sealed record DashboardSummary(
    string TodaySpentText,
    string DailyAverageText,
    string DaysRemainingText,
    string ExpenseTotalText,
    string IncomeTotalText,
    IReadOnlyList<FinancialInsightItem> Insights,
    IReadOnlyList<CategorySummaryItem> ExpenseCategories,
    IReadOnlyList<CategorySummaryItem> IncomeCategories,
    IReadOnlyList<TransactionActivityItem> RecentActivity);
