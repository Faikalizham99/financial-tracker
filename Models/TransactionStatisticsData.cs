namespace FinancialTracker.Models;

public enum TransactionStatisticsMode
{
    All,
    Expense,
    Income
}

public enum TransactionStatisticsBreakdownDimension
{
    Category,
    PaymentMethod
}

public enum TransactionStatisticsChartKind
{
    Bar,
    RunningTotal,
    Composition
}

public sealed record TransactionStatisticsMonth(
    DateTime Month,
    long IncomeMinor,
    long ExpenseMinor)
{
    public long NetMinor => IncomeMinor - ExpenseMinor;
}

public sealed record TransactionStatisticsData(
    DateTime SourceMonth,
    string CurrencySymbol,
    IReadOnlyList<TransactionStatisticsMonth> Timeline,
    IReadOnlyList<TransactionStatisticsMonth> DisplayMonths,
    IReadOnlyList<TransactionRecord> TimelineRecords,
    IReadOnlyList<TransactionRecord> SourceMonthRecords,
    IReadOnlyList<TransactionRecord> PreviousMonthRecords);

public sealed record TransactionStatisticsChartPoint(
    DateTime Month,
    long ValueMinor);

public sealed record TransactionStatisticsRunningTotalPoint(
    int Day,
    long CurrentValueMinor,
    long PreviousValueMinor);

public sealed record TransactionStatisticsCompositionSlice(
    string Key,
    string Label,
    long AmountMinor,
    Color Color);

public sealed class TransactionStatisticsBreakdownItem
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string IconAsset { get; init; }
    public required string AmountText { get; init; }
    public required string DetailText { get; init; }
    public required string ChangeText { get; init; }
    public long AmountMinor { get; init; }
    public double Share { get; init; }
    public required Color ChartColor { get; init; }
    public bool IsFavorable { get; init; }
    public bool IsUnfavorable { get; init; }
    public bool IsSelected { get; init; }
    public bool ShowDivider { get; set; }
}

public enum TransactionStatisticsDetailRange
{
    Daily,
    Monthly
}

public sealed record TransactionStatisticsDetailRequest(
    DateTime SourceMonth,
    string CurrencySymbol,
    TransactionStatisticsBreakdownDimension Dimension,
    string OptionKey,
    string Title,
    string IconAsset,
    bool IsIncome,
    IReadOnlyList<TransactionRecord> TimelineRecords);

public sealed record TransactionStatisticsDetailChartPoint(
    int Key,
    string Label,
    long ValueMinor);
