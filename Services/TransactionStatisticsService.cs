using FinancialTracker.Data;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class TransactionStatisticsService(LocalDatabase database)
{
    private const int DisplayedMonthCount = 6;

    public async Task<TransactionStatisticsData> GetAsync(
        DateTime sourceMonth,
        CurrencyOption currency,
        bool includeInvestment)
    {
        var normalizedMonth = new DateTime(sourceMonth.Year, sourceMonth.Month, 1);
        var firstComparisonMonth = normalizedMonth.AddMonths(-DisplayedMonthCount);
        var endDateExclusive = normalizedMonth.AddMonths(1);
        var records = await database.GetTransactionsAsync(
            firstComparisonMonth,
            endDateExclusive).ConfigureAwait(false);

        var includedRecords = includeInvestment
            ? records
            : records
                .Where(record => !record.Category.Equals(
                    TransactionCatalog.InvestmentCategoryKey,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();

        var recordsByMonth = includedRecords
            .GroupBy(record => new DateTime(
                record.TransactionDate.Year,
                record.TransactionDate.Month,
                1))
            .ToDictionary(group => group.Key, group => group.ToList());

        var timeline = new List<TransactionStatisticsMonth>(DisplayedMonthCount + 1);
        for (var offset = 0; offset <= DisplayedMonthCount; offset++)
        {
            var month = firstComparisonMonth.AddMonths(offset);
            recordsByMonth.TryGetValue(month, out var monthRecords);
            long incomeMinor = 0;
            long expenseMinor = 0;

            if (monthRecords is not null)
            {
                foreach (var record in monthRecords)
                {
                    if (TransactionCatalog.IsIncomeType(record.Type))
                    {
                        incomeMinor += record.AmountMinor;
                    }
                    else if (TransactionCatalog.IsExpenseType(record.Type))
                    {
                        expenseMinor += record.AmountMinor;
                    }
                }
            }

            timeline.Add(new TransactionStatisticsMonth(month, incomeMinor, expenseMinor));
        }

        recordsByMonth.TryGetValue(normalizedMonth, out var sourceRecords);
        recordsByMonth.TryGetValue(normalizedMonth.AddMonths(-1), out var previousRecords);

        return new TransactionStatisticsData(
            normalizedMonth,
            currency.Symbol,
            timeline,
            timeline.Skip(1).ToList(),
            includedRecords,
            sourceRecords ?? [],
            previousRecords ?? []);
    }
}
