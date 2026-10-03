using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class AssetWidgetSnapshotBuilder
{
    public const int HistoryMonthCount = 13;

    public static AssetWidgetSnapshot Build(
        IReadOnlyList<AssetSnapshotData> snapshots,
        CurrencyOption currency,
        DateTime throughMonth,
        DateTimeOffset updatedAt)
    {
        var normalizedMonth = new DateTime(throughMonth.Year, throughMonth.Month, 1);
        var orderedSnapshots = snapshots
            .OrderBy(item => item.Snapshot.MonthKey)
            .ToList();
        var snapshotsByMonth = orderedSnapshots.ToDictionary(
            item => item.Snapshot.MonthKey);
        var months = new List<AssetWidgetMonthSnapshot>(HistoryMonthCount);

        for (var offset = 1 - HistoryMonthCount; offset <= 0; offset++)
        {
            var month = normalizedMonth.AddMonths(offset);
            var monthKey = MonthKeyConverter.FromDate(month);
            snapshotsByMonth.TryGetValue(monthKey, out var current);
            var previous = orderedSnapshots.LastOrDefault(
                item => item.Snapshot.MonthKey < monthKey);
            months.Add(BuildMonth(month, current, previous, currency.Symbol));
        }

        return new AssetWidgetSnapshot(
            Version: 1,
            CurrentMonthKey: MonthKeyConverter.FromDate(normalizedMonth),
            Months: months,
            UpdatedAtUnixSeconds: updatedAt.ToUnixTimeSeconds());
    }

    private static AssetWidgetMonthSnapshot BuildMonth(
        DateTime month,
        AssetSnapshotData? current,
        AssetSnapshotData? previous,
        string currencySymbol)
    {
        var monthKey = MonthKeyConverter.FromDate(month);
        if (current is null)
        {
            var emptySummary = new AssetWidgetSummary(
                MoneyFormatter.FormatMinor(0, currencySymbol),
                "—",
                MoneyFormatter.FormatMinor(0, currencySymbol),
                "—",
                0,
                0);
            return new AssetWidgetMonthSnapshot(
                monthKey,
                month.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
                HasSnapshot: false,
                ComparisonDateText: "No snapshot yet",
                emptySummary,
                emptySummary,
                []);
        }

        var currentValues = current.Values.ToDictionary(
            item => item.AssetKey,
            StringComparer.Ordinal);
        var previousValues = previous?.Values.ToDictionary(
            item => item.AssetKey,
            StringComparer.Ordinal)
            ?? new Dictionary<string, AssetSnapshotValueRecord>(StringComparer.Ordinal);
        var items = AssetCatalog.ActiveItems.Select(asset =>
        {
            var currentAmount = currentValues.GetValueOrDefault(asset.Key)?.AmountMinor ?? 0;
            var previousValue = previousValues.GetValueOrDefault(asset.Key);
            var change = previous is null || previousValue is null
                ? (long?)null
                : currentAmount - previousValue.AmountMinor;
            return new AssetWidgetItem(
                asset.Key,
                asset.DisplayName,
                previousValue is null
                    ? "—"
                    : MoneyFormatter.FormatMinor(previousValue.AmountMinor, currencySymbol),
                MoneyFormatter.FormatMinor(currentAmount, currencySymbol),
                FormatChange(change, currencySymbol),
                GetDirection(change),
                asset.Key.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal));
        }).ToList();

        var total = current.Values.Sum(item => item.AmountMinor);
        var previousTotal = previous?.Values.Sum(item => item.AmountMinor);
        var totalWithoutKwsp = current.Values
            .Where(item => !item.AssetKey.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
            .Sum(item => item.AmountMinor);
        var previousTotalWithoutKwsp = previous?.Values
            .Where(item => !item.AssetKey.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
            .Sum(item => item.AmountMinor);
        var accessible = current.Values
            .Where(item => AssetCatalog.IsAccessible(item.AssetKey))
            .Sum(item => item.AmountMinor);
        var previousAccessible = previous?.Values
            .Where(item => AssetCatalog.IsAccessible(item.AssetKey))
            .Sum(item => item.AmountMinor);

        return new AssetWidgetMonthSnapshot(
            monthKey,
            month.ToString("MMMM yyyy", CultureInfo.CurrentCulture),
            HasSnapshot: true,
            ComparisonDateText: previous is null
                ? $"Baseline · {current.Snapshot.EntryDate:dd MMM yyyy}"
                : $"{previous.Snapshot.EntryDate:dd MMM yyyy} → {current.Snapshot.EntryDate:dd MMM yyyy}",
            BuildSummary(
                total,
                previousTotal,
                accessible,
                previousAccessible,
                currencySymbol),
            BuildSummary(
                totalWithoutKwsp,
                previousTotalWithoutKwsp,
                accessible,
                previousAccessible,
                currencySymbol),
            items);
    }

    private static AssetWidgetSummary BuildSummary(
        long total,
        long? previousTotal,
        long accessible,
        long? previousAccessible,
        string symbol)
    {
        var totalChange = previousTotal.HasValue ? total - previousTotal.Value : (long?)null;
        var accessibleChange = previousAccessible.HasValue
            ? accessible - previousAccessible.Value
            : (long?)null;
        return new AssetWidgetSummary(
            MoneyFormatter.FormatMinor(total, symbol),
            FormatChange(totalChange, previousTotal, symbol),
            MoneyFormatter.FormatMinor(accessible, symbol),
            FormatChange(accessibleChange, previousAccessible, symbol),
            GetDirection(totalChange),
            GetDirection(accessibleChange));
    }

    private static string FormatChange(long? amount, string symbol) =>
        amount.HasValue
            ? MoneyFormatter.FormatMinor(amount.Value, symbol, showPositiveSign: true)
            : "—";

    private static string FormatChange(
        long? amount,
        long? previousAmount,
        string symbol)
    {
        var amountText = FormatChange(amount, symbol);
        if (!amount.HasValue || previousAmount is not > 0)
        {
            return amountText;
        }

        var percentage = amount.Value * 100m / previousAmount.Value;
        return $"{amountText} ({percentage:+0.0;-0.0;0.0}%)";
    }

    private static int GetDirection(long? amount) => amount switch
    {
        > 0 => 1,
        < 0 => -1,
        _ => 0
    };
}
