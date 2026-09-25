using FinancialTracker.Data;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class MonthlyBudgetService(LocalDatabase database)
{
    private readonly Dictionary<int, Task<MonthlyBudgetRecord?>> cache = [];
    private readonly object cacheLock = new();

    public Task<MonthlyBudgetRecord?> GetAsync(DateTime month)
    {
        var monthKey = ToMonthKey(month);
        Task<MonthlyBudgetRecord?> queryTask;
        lock (cacheLock)
        {
            if (cache.TryGetValue(monthKey, out var cachedTask))
            {
                return cachedTask;
            }

            queryTask = database.GetMonthlyBudgetAsync(monthKey);
            cache[monthKey] = queryTask;
        }

        return ObserveQueryAsync(monthKey, queryTask);
    }

    public async Task<IReadOnlyList<MonthlyBudgetRecord>> GetYearAsync(int year)
    {
        var firstMonthKey = (year * 100) + 1;
        var budgets = await database.GetMonthlyBudgetsAsync(
            firstMonthKey,
            firstMonthKey + 11);
        var budgetsByMonth = budgets.ToDictionary(budget => budget.MonthKey);

        lock (cacheLock)
        {
            for (var monthKey = firstMonthKey;
                 monthKey <= firstMonthKey + 11;
                 monthKey++)
            {
                budgetsByMonth.TryGetValue(monthKey, out var budget);
                cache[monthKey] = Task.FromResult<MonthlyBudgetRecord?>(budget);
            }
        }

        return budgets;
    }

    public Task SaveAsync(
        DateTime month,
        long includingInvestmentMinor,
        long excludingInvestmentMinor,
        string currencyCode) =>
        SaveCoreAsync(new MonthlyBudgetRecord
        {
            MonthKey = ToMonthKey(month),
            BudgetIncludingInvestmentMinor = includingInvestmentMinor,
            BudgetExcludingInvestmentMinor = excludingInvestmentMinor,
            CurrencyCode = currencyCode,
            UpdatedAtUtc = DateTime.UtcNow
        });

    public void InvalidateCache()
    {
        lock (cacheLock)
        {
            cache.Clear();
        }
    }

    private async Task SaveCoreAsync(MonthlyBudgetRecord budget)
    {
        await database.SaveMonthlyBudgetAsync(budget);
        lock (cacheLock)
        {
            cache[budget.MonthKey] = Task.FromResult<MonthlyBudgetRecord?>(budget);
        }
    }

    private async Task<MonthlyBudgetRecord?> ObserveQueryAsync(
        int monthKey,
        Task<MonthlyBudgetRecord?> queryTask)
    {
        try
        {
            return await queryTask;
        }
        catch
        {
            lock (cacheLock)
            {
                if (cache.TryGetValue(monthKey, out var cachedTask) &&
                    ReferenceEquals(cachedTask, queryTask))
                {
                    cache.Remove(monthKey);
                }
            }

            throw;
        }
    }

    private static int ToMonthKey(DateTime month) =>
        (month.Year * 100) + month.Month;
}
