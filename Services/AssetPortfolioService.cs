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

    public async Task<AssetHistoryData> GetHistoryAsync(
        DateTime throughMonth,
        CurrencyOption fallbackCurrency)
    {
        var normalizedMonth = new DateTime(throughMonth.Year, throughMonth.Month, 1);
        var snapshots = await database.GetAssetSnapshotsThroughAsync(
            MonthKeyConverter.FromDate(normalizedMonth),
            int.MaxValue).ConfigureAwait(false);
        var points = snapshots
            .OrderBy(item => item.Snapshot.MonthKey)
            .Select(item =>
            {
                var assets = item.Values
                    .Select(value => new AssetHistoryBreakdownItem(
                        AssetCatalog.GetHistoricalItem(value.AssetKey),
                        value.AmountMinor))
                    .OrderBy(value => value.Asset.DisplayOrder)
                    .ThenBy(value => value.Asset.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();
                return new AssetHistoryPoint(
                    MonthKeyConverter.ToDate(item.Snapshot.MonthKey),
                    item.Snapshot.EntryDate,
                    assets.Sum(value => value.AmountMinor),
                    assets
                        .Where(value => !value.Asset.Key.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
                        .Sum(value => value.AmountMinor),
                    assets
                        .Where(value => value.Asset.IsAccessible)
                        .Sum(value => value.AmountMinor),
                    assets
                        .Where(value => value.Asset.Key.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
                        .Sum(value => value.AmountMinor),
                    assets);
            })
            .ToList();

        return new AssetHistoryData(
            fallbackCurrency.Symbol,
            normalizedMonth,
            points);
    }

    public Task<AssetSnapshotData?> GetPreviousMonthSnapshotAsync(DateTime month)
    {
        var previousMonth = new DateTime(month.Year, month.Month, 1).AddMonths(-1);
        return database.GetAssetSnapshotAsync(MonthKeyConverter.FromDate(previousMonth));
    }

    public Task DeleteAsync(DateTime month) =>
        database.DeleteAssetSnapshotAsync(MonthKeyConverter.FromDate(month));

    public async Task<AssetPerformanceData> GetPerformanceAsync(
        string assetKey,
        CurrencyOption fallbackCurrency)
    {
        var asset = AssetCatalog.GetHistoricalItem(assetKey);
        if (!asset.IsActive)
        {
            throw new InvalidOperationException("This asset is no longer available.");
        }

        var records = await database.GetAssetPerformanceRecordsAsync(assetKey)
            .ConfigureAwait(false);
        var points = new List<AssetPerformancePoint>(records.Count);
        AssetPerformanceRecord? previous = null;

        foreach (var record in records.OrderBy(item => item.EntryDate))
        {
            var totalMinor = checked(record.InvestedMinor + record.ProfitLossMinor);
            points.Add(new AssetPerformancePoint(
                record.Id,
                record.EntryDate.Date,
                record.InvestedMinor,
                record.ProfitLossMinor,
                totalMinor,
                previous is null
                    ? null
                    : record.InvestedMinor - previous.InvestedMinor,
                previous is null
                    ? null
                    : record.ProfitLossMinor - previous.ProfitLossMinor,
                previous is null
                    ? null
                    : totalMinor - checked(
                        previous.InvestedMinor + previous.ProfitLossMinor),
                record.InvestedMinor > 0
                    ? record.ProfitLossMinor * 100m / record.InvestedMinor
                    : null,
                previous is null
                    ? null
                    : Math.Max(0, (record.EntryDate.Date - previous.EntryDate.Date).Days),
                record.Note));
            previous = record;
        }

        return new AssetPerformanceData(
            asset,
            fallbackCurrency.Symbol,
            points);
    }

    public Task<AssetPerformanceRecord?> GetPerformanceRecordAsync(
        string assetKey,
        DateTime entryDate) =>
        database.GetAssetPerformanceRecordAsync(assetKey, entryDate.Date);

    public async Task SavePerformanceRecordAsync(
        string assetKey,
        DateTime entryDate,
        long investedMinor,
        long profitLossMinor,
        string? note,
        CurrencyOption currency)
    {
        if (!AssetCatalog.IsActive(assetKey))
        {
            throw new InvalidOperationException("This asset is no longer available.");
        }

        if (entryDate.Date > DateTime.Today)
        {
            throw new InvalidOperationException("A performance record cannot be dated in the future.");
        }

        if (investedMinor < 0)
        {
            throw new InvalidOperationException("The invested amount cannot be negative.");
        }

        var totalMinor = checked(investedMinor + profitLossMinor);
        if (totalMinor < 0)
        {
            throw new InvalidOperationException(
                "The loss cannot be greater than the invested amount.");
        }

        var normalizedNote = (note ?? string.Empty).Trim();
        if (normalizedNote.Length > 180)
        {
            throw new InvalidOperationException("Keep the note within 180 characters.");
        }

        var normalizedDate = entryDate.Date;
        await database.SaveAssetPerformanceRecordAsync(new AssetPerformanceRecord
        {
            AssetKey = assetKey,
            EntryDate = normalizedDate,
            InvestedMinor = investedMinor,
            ProfitLossMinor = profitLossMinor,
            CurrencyCode = currency.Code,
            Note = normalizedNote
        }).ConfigureAwait(false);

        var monthStart = new DateTime(normalizedDate.Year, normalizedDate.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var latestInMonth = (await database.GetAssetPerformanceRecordsAsync(assetKey)
            .ConfigureAwait(false))
            .Where(item =>
                item.EntryDate >= monthStart &&
                item.EntryDate < monthEnd)
            .MaxBy(item => item.EntryDate);
        if (latestInMonth is null)
        {
            return;
        }

        await SyncPerformanceToSnapshotAsync(
            assetKey,
            monthStart,
            latestInMonth.EntryDate,
            checked(latestInMonth.InvestedMinor + latestInMonth.ProfitLossMinor),
            currency.Code).ConfigureAwait(false);
    }

    public async Task DeletePerformanceRecordAsync(
        string assetKey,
        int recordId,
        DateTime recordDate,
        CurrencyOption currency)
    {
        await database.DeleteAssetPerformanceRecordAsync(recordId)
            .ConfigureAwait(false);

        var monthStart = new DateTime(recordDate.Year, recordDate.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var remainingRecords = await database
            .GetAssetPerformanceRecordsAsync(assetKey)
            .ConfigureAwait(false);
        var latestRemaining = remainingRecords
            .Where(item =>
                item.EntryDate >= monthStart &&
                item.EntryDate < monthEnd)
            .MaxBy(item => item.EntryDate);
        var previousRecord = latestRemaining is null
            ? remainingRecords
                .Where(item => item.EntryDate < monthStart)
                .MaxBy(item => item.EntryDate)
            : null;
        var replacement = latestRemaining ?? previousRecord;

        await SyncPerformanceToSnapshotAsync(
            assetKey,
            monthStart,
            latestRemaining?.EntryDate ?? recordDate.Date,
            replacement is null
                ? 0
                : checked(
                    replacement.InvestedMinor +
                    replacement.ProfitLossMinor),
            currency.Code).ConfigureAwait(false);
    }

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

    private async Task SyncPerformanceToSnapshotAsync(
        string assetKey,
        DateTime snapshotMonth,
        DateTime entryDate,
        long totalMinor,
        string currencyCode)
    {
        var month = new DateTime(snapshotMonth.Year, snapshotMonth.Month, 1);
        var monthKey = MonthKeyConverter.FromDate(month);
        var existing = await database.GetAssetSnapshotAsync(monthKey)
            .ConfigureAwait(false);
        var source = existing;
        if (source is null)
        {
            source = (await database.GetAssetSnapshotsThroughAsync(monthKey, 1)
                .ConfigureAwait(false))
                .FirstOrDefault(item => item.Snapshot.MonthKey < monthKey);
        }

        var sourceValues = source?.Values.ToDictionary(
            item => item.AssetKey,
            item => item.AmountMinor,
            StringComparer.Ordinal)
            ?? new Dictionary<string, long>(StringComparer.Ordinal);
        var amounts = AssetCatalog.ActiveItems.ToDictionary(
            item => item.Key,
            item => sourceValues.GetValueOrDefault(item.Key),
            StringComparer.Ordinal);
        amounts[assetKey] = totalMinor;

        await SaveAsync(
            month,
            existing is null
                ? entryDate
                : new[] { existing.Snapshot.EntryDate.Date, entryDate }
                    .Max(),
            existing?.Snapshot.CurrencyCode ?? currencyCode,
            amounts).ConfigureAwait(false);
    }
}
