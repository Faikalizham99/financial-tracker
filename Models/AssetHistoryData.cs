namespace FinancialTracker.Models;

public sealed record AssetHistoryBreakdownItem(
    AssetCatalogItem Asset,
    long AmountMinor);

public sealed record AssetHistoryPoint(
    DateTime Month,
    DateTime EntryDate,
    long TotalMinor,
    long TotalWithoutKwspMinor,
    long AccessibleTotalMinor,
    long KwspTotalMinor,
    IReadOnlyList<AssetHistoryBreakdownItem> Assets);

public sealed record AssetHistoryData(
    string CurrencySymbol,
    DateTime ThroughMonth,
    IReadOnlyList<AssetHistoryPoint> Points);
