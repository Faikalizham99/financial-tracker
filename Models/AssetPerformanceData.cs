namespace FinancialTracker.Models;

public sealed record AssetPerformancePoint(
    int Id,
    DateTime EntryDate,
    long InvestedMinor,
    long ProfitLossMinor,
    long TotalMinor,
    long? InvestedChangeMinor,
    long? ProfitChangeMinor,
    long? TotalChangeMinor,
    decimal? ReturnPercentage,
    int? DaysSincePrevious,
    string Note);

public sealed record AssetPerformanceData(
    AssetCatalogItem Asset,
    string CurrencySymbol,
    IReadOnlyList<AssetPerformancePoint> Points)
{
    public AssetPerformancePoint? Latest => Points.LastOrDefault();
}
