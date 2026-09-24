using FinancialTracker.Data;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class MonthlyBudgetService(LocalDatabase database)
{
    public Task<MonthlyBudgetRecord?> GetAsync(DateTime month) =>
        database.GetMonthlyBudgetAsync(ToMonthKey(month));

    public Task<IReadOnlyList<MonthlyBudgetRecord>> GetYearAsync(int year) =>
        database.GetMonthlyBudgetsAsync((year * 100) + 1, (year * 100) + 12);

    public Task SaveAsync(
        DateTime month,
        long includingInvestmentMinor,
        long excludingInvestmentMinor,
        string currencyCode) =>
        database.SaveMonthlyBudgetAsync(new MonthlyBudgetRecord
        {
            MonthKey = ToMonthKey(month),
            BudgetIncludingInvestmentMinor = includingInvestmentMinor,
            BudgetExcludingInvestmentMinor = excludingInvestmentMinor,
            CurrencyCode = currencyCode,
            UpdatedAtUtc = DateTime.UtcNow
        });

    private static int ToMonthKey(DateTime month) =>
        (month.Year * 100) + month.Month;
}
