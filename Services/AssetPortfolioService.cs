using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class AssetPortfolioService(LocalDatabase database)
{
    public async Task<AssetPortfolio> GetPortfolioAsync(
        DateTime month,
        CurrencyOption fallbackCurrency)
    {
        var selectedMonth = new DateTime(month.Year, month.Month, 1);
        var selectedMonthKey = MonthKeyConverter.FromDate(selectedMonth);
        var snapshots = await database.GetAssetSnapshotsThroughAsync(
            selectedMonthKey,
            13).ConfigureAwait(false);
        var current = snapshots.FirstOrDefault(
            item => item.Snapshot.MonthKey == selectedMonthKey);
        var previous = snapshots.FirstOrDefault(
            item => item.Snapshot.MonthKey < selectedMonthKey);
        var currencySymbol = fallbackCurrency.Symbol;

        if (current is null)
        {
            return new AssetPortfolio(
                currencySymbol,
                null,
                previous?.Snapshot.EntryDate,
                0,
                null,
                null,
                0,
                null,
                null,
                0,
                null,
                null,
                [],
                BuildTrend(snapshots),
                BuildTrend(snapshots, AssetCatalog.KwspKey),
                false,
                [new FinancialInsightItem(
                    "Add a snapshot",
                    "Add this month's snapshot to start tracking your portfolio.")],
                [new FinancialInsightItem(
                    "Add a snapshot",
                    "Add this month's snapshot to start tracking your portfolio.")]);
        }

        var currentValues = current.Values.ToDictionary(value => value.AssetKey);
        var previousCompatible = previous;
        var previousValues = previousCompatible?.Values.ToDictionary(value => value.AssetKey)
            ?? new Dictionary<string, AssetSnapshotValueRecord>();
        var keys = AssetCatalog.Items.Select(item => item.Key)
            .Concat(currentValues.Keys)
            .Concat(previousValues.Keys)
            .Distinct(StringComparer.Ordinal)
            .Select(AssetCatalog.GetHistoricalItem)
            .OrderBy(item => item.DisplayOrder)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase);
        var comparisons = new List<AssetComparisonItem>();

        foreach (var asset in keys)
        {
            currentValues.TryGetValue(asset.Key, out var currentValue);
            previousValues.TryGetValue(asset.Key, out var previousValue);
            var currentAmount = currentValue?.AmountMinor ?? 0;
            var isNew = currentValue is not null && previousCompatible is not null && previousValue is null;
            long? change = previousCompatible is null || isNew
                ? null
                : currentAmount - (previousValue?.AmountMinor ?? 0);
            decimal? percentage = change.HasValue && previousValue?.AmountMinor > 0
                ? change.Value * 100m / previousValue.AmountMinor
                : null;
            comparisons.Add(new AssetComparisonItem(
                asset,
                previousValue?.AmountMinor,
                currentAmount,
                change,
                percentage,
                isNew));
        }

        var total = current.Values.Sum(value => value.AmountMinor);
        var previousTotal = previousCompatible?.Values.Sum(value => value.AmountMinor);
        long? totalChange = previousTotal.HasValue ? total - previousTotal.Value : null;
        decimal? totalPercentage = totalChange.HasValue && previousTotal is > 0
            ? totalChange.Value * 100m / previousTotal.Value
            : null;
        var totalWithoutKwsp = current.Values
            .Where(value => !value.AssetKey.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
            .Sum(value => value.AmountMinor);
        var previousTotalWithoutKwsp = previousCompatible?.Values
            .Where(value => !value.AssetKey.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
            .Sum(value => value.AmountMinor);
        long? totalWithoutKwspChange = previousTotalWithoutKwsp.HasValue
            ? totalWithoutKwsp - previousTotalWithoutKwsp.Value
            : null;
        decimal? totalWithoutKwspPercentage = totalWithoutKwspChange.HasValue && previousTotalWithoutKwsp is > 0
            ? totalWithoutKwspChange.Value * 100m / previousTotalWithoutKwsp.Value
            : null;
        var accessibleTotal = current.Values
            .Where(value => AssetCatalog.IsAccessible(value.AssetKey))
            .Sum(value => value.AmountMinor);
        var previousAccessible = previousCompatible?.Values
            .Where(value => AssetCatalog.IsAccessible(value.AssetKey))
            .Sum(value => value.AmountMinor);
        long? accessibleChange = previousAccessible.HasValue
            ? accessibleTotal - previousAccessible.Value
            : null;
        decimal? accessibleChangePercentage = accessibleChange.HasValue && previousAccessible is > 0
            ? accessibleChange.Value * 100m / previousAccessible.Value
            : null;
        var comparisonsWithoutKwsp = comparisons
            .Where(item => !item.Asset.Key.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
            .ToList();
        var insights = FinancialInsightService.BuildAssetInsights(
            comparisons,
            total,
            accessibleTotal,
            currencySymbol,
            previousCompatible is not null);
        var insightsWithoutKwsp = FinancialInsightService.BuildAssetInsights(
            comparisonsWithoutKwsp,
            totalWithoutKwsp,
            accessibleTotal,
            currencySymbol,
            previousCompatible is not null);

        return new AssetPortfolio(
            currencySymbol,
            current.Snapshot.EntryDate,
            previousCompatible?.Snapshot.EntryDate,
            total,
            totalChange,
            totalPercentage,
            totalWithoutKwsp,
            totalWithoutKwspChange,
            totalWithoutKwspPercentage,
            accessibleTotal,
            accessibleChange,
            accessibleChangePercentage,
            comparisons,
            BuildTrend(snapshots),
            BuildTrend(snapshots, AssetCatalog.KwspKey),
            true,
            insights,
            insightsWithoutKwsp);
    }

    public Task<AssetSnapshotData?> GetSnapshotAsync(DateTime month) =>
        database.GetAssetSnapshotAsync(MonthKeyConverter.FromDate(month));

    public Task<AssetSnapshotData?> GetPreviousMonthSnapshotAsync(DateTime month)
    {
        var previousMonth = new DateTime(month.Year, month.Month, 1).AddMonths(-1);
        return database.GetAssetSnapshotAsync(MonthKeyConverter.FromDate(previousMonth));
    }

    public Task DeleteAsync(DateTime month) =>
        database.DeleteAssetSnapshotAsync(MonthKeyConverter.FromDate(month));

    public Task SaveAsync(
        DateTime month,
        DateTime entryDate,
        string currencyCode,
        IReadOnlyDictionary<string, long> amounts)
    {
        var normalizedMonth = new DateTime(month.Year, month.Month, 1);

        if (amounts.Count != AssetCatalog.ActiveItems.Count ||
            amounts.Keys.Any(key => !AssetCatalog.IsActive(key)))
        {
            throw new InvalidOperationException("Enter a value for every active asset.");
        }

        return database.SaveAssetSnapshotAsync(
            new AssetSnapshotRecord
            {
                MonthKey = MonthKeyConverter.FromDate(normalizedMonth),
                EntryDate = entryDate.Date,
                CurrencyCode = currencyCode
            },
            amounts.Select(item => new AssetSnapshotValueRecord
            {
                AssetKey = item.Key,
                AmountMinor = item.Value
            }).ToList());
    }

    private static IReadOnlyList<AssetTrendPoint> BuildTrend(
        IReadOnlyList<AssetSnapshotData> snapshots,
        string? excludedAssetKey = null)
    {
        var compatible = snapshots
            .OrderBy(item => item.Snapshot.MonthKey)
            .TakeLast(12)
            .Select(item => new
            {
                Month = MonthKeyConverter.ToDate(item.Snapshot.MonthKey),
                Total = item.Values
                    .Where(value => excludedAssetKey is null ||
                        !value.AssetKey.Equals(excludedAssetKey, StringComparison.Ordinal))
                    .Sum(value => value.AmountMinor)
            })
            .ToList();
        var maximum = compatible.Count == 0 ? 0 : compatible.Max(item => item.Total);
        return compatible.Select(item => new AssetTrendPoint(
            item.Month,
            item.Total,
            maximum <= 0 ? 0 : Math.Max(0.08, (double)item.Total / maximum)))
            .ToList();
    }
}
