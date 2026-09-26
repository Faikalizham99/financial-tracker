namespace FinancialTracker.Models;

public sealed record AssetComparisonItem(
    AssetCatalogItem Asset,
    long? PreviousAmountMinor,
    long CurrentAmountMinor,
    long? ChangeMinor,
    decimal? ChangePercentage,
    bool IsNew);

public sealed record AssetTrendPoint(
    DateTime Month,
    long TotalMinor,
    double RelativeHeight);

public sealed record AssetPortfolio(
    DateTime Month,
    string CurrencyCode,
    string CurrencySymbol,
    DateTime? EntryDate,
    DateTime? PreviousEntryDate,
    long TotalMinor,
    long? TotalChangeMinor,
    decimal? TotalChangePercentage,
    long TotalWithoutKwspMinor,
    long? TotalWithoutKwspChangeMinor,
    decimal? TotalWithoutKwspChangePercentage,
    long AccessibleTotalMinor,
    long? AccessibleChangeMinor,
    IReadOnlyList<AssetComparisonItem> Comparisons,
    IReadOnlyList<AssetTrendPoint> Trend,
    IReadOnlyList<AssetTrendPoint> TrendWithoutKwsp,
    bool HasSnapshot,
    string InsightText,
    string InsightWithoutKwspText);
