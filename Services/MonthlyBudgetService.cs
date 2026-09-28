using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class MonthlyBudgetService(LocalDatabase database)
{
    private readonly Dictionary<int, Task<MonthlyBudgetRecord?>> cache = [];
    private readonly object cacheLock = new();

    public Task<MonthlyBudgetRecord?> GetAsync(DateTime month)
    {
        var monthKey = MonthKeyConverter.FromDate(month);
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
        var firstMonthKey = MonthKeyConverter.FromDate(new DateTime(year, 1, 1));
        var budgets = await database.GetMonthlyBudgetsAsync(
            firstMonthKey,
            firstMonthKey + 11).ConfigureAwait(false);
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
            MonthKey = MonthKeyConverter.FromDate(month),
            BudgetIncludingInvestmentMinor = includingInvestmentMinor,
            BudgetExcludingInvestmentMinor = excludingInvestmentMinor,
            CurrencyCode = currencyCode,
            UpdatedAtUtc = DateTime.UtcNow
        });

    public async Task DeleteAsync(DateTime month)
    {
        var monthKey = MonthKeyConverter.FromDate(month);
        await database.DeleteMonthlyBudgetAsync(monthKey).ConfigureAwait(false);
        lock (cacheLock)
        {
            cache[monthKey] = Task.FromResult<MonthlyBudgetRecord?>(null);
        }
    }

    public void InvalidateCache()
    {
        lock (cacheLock)
        {
            cache.Clear();
        }
    }

    private async Task SaveCoreAsync(MonthlyBudgetRecord budget)
    {
        await database.SaveMonthlyBudgetAsync(budget).ConfigureAwait(false);
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
            return await queryTask.ConfigureAwait(false);
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
}
