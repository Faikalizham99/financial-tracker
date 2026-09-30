using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed class TransactionStatisticsDetailViewModel : ObservableObject
{
    private TransactionStatisticsDetailRequest? request;
    private TransactionStatisticsDetailRange selectedRange =
        TransactionStatisticsDetailRange.Daily;
    private IReadOnlyList<TransactionStatisticsDetailChartPoint> dailyPoints = [];
    private IReadOnlyList<TransactionStatisticsDetailChartPoint> monthlyPoints = [];
    private IReadOnlyList<TransactionActivityGroup> groups = [];

    public TransactionStatisticsDetailRange SelectedRange
    {
        get => selectedRange;
        private set => SetProperty(ref selectedRange, value);
    }

    public IReadOnlyList<TransactionActivityGroup> Groups
    {
        get => groups;
        private set => SetProperty(ref groups, value);
    }

    public IReadOnlyList<TransactionStatisticsDetailChartPoint> ChartPoints =>
        SelectedRange == TransactionStatisticsDetailRange.Daily
            ? dailyPoints
            : monthlyPoints;

    public string Title => request?.Title ?? string.Empty;
    public string IconAsset => request?.IconAsset ?? string.Empty;
    public string MonthLabel => request?.SourceMonth.ToString(
        "MMMM yyyy",
        CultureInfo.CurrentCulture) ?? string.Empty;
    public string ScopeLabel => request is null
        ? string.Empty
        : $"{(request.IsIncome ? "Income" : "Expense")} \u00B7 " +
          (request.Dimension == TransactionStatisticsBreakdownDimension.Category
              ? "Category"
              : "Payment method");
    public string TotalText { get; private set; } = string.Empty;
    public string ChartTitle => SelectedRange == TransactionStatisticsDetailRange.Daily
        ? $"DAILY TOTALS \u00B7 {MonthLabel.ToUpperInvariant()}"
        : "LAST 6 MONTHS";
    public bool IsIncome => request?.IsIncome == true;
    public bool IsExpense => request is not null && !request.IsIncome;
    public bool HasTransactions => Groups.Count > 0;
    public bool HasNoTransactions => !HasTransactions;

    public void Load(TransactionStatisticsDetailRequest detailRequest)
    {
        request = detailRequest;
        SelectedRange = TransactionStatisticsDetailRange.Daily;

        var matchingRecords = detailRequest.TimelineRecords
            .Where(record => Matches(detailRequest, record))
            .OrderByDescending(record => record.TransactionDate)
            .ThenByDescending(record => record.CreatedAtUtc)
            .ThenByDescending(record => record.Id)
            .ToList();
        var sourceEnd = detailRequest.SourceMonth.AddMonths(1);
        var sourceRecords = matchingRecords
            .Where(record =>
                record.TransactionDate >= detailRequest.SourceMonth &&
                record.TransactionDate < sourceEnd)
            .ToList();

        TotalText = MoneyFormatter.FormatMinor(
            sourceRecords.Sum(record => record.AmountMinor),
            detailRequest.CurrencySymbol,
            separateSign: true);
        Groups = TransactionActivityGroupBuilder.Build(
            sourceRecords,
            detailRequest.CurrencySymbol,
            new HashSet<int>(),
            new HashSet<DateTime>(),
            canModifyTransactions: true,
            DateTime.Today);
        dailyPoints = BuildDailyPoints(detailRequest, sourceRecords);
        monthlyPoints = BuildMonthlyPoints(detailRequest, matchingRecords);
        NotifyAllStateChanged();
    }

    public void SelectRange(TransactionStatisticsDetailRange range)
    {
        if (SelectedRange == range)
        {
            return;
        }

        SelectedRange = range;
        OnPropertyChanged(nameof(ChartPoints));
        OnPropertyChanged(nameof(ChartTitle));
    }

    private static bool Matches(
        TransactionStatisticsDetailRequest detailRequest,
        TransactionRecord record)
    {
        var matchesTransactionType = detailRequest.IsIncome
            ? TransactionCatalog.IsIncomeType(record.Type)
            : TransactionCatalog.IsExpenseType(record.Type);
        if (!matchesTransactionType)
        {
            return false;
        }

        var recordKey = detailRequest.Dimension ==
            TransactionStatisticsBreakdownDimension.Category
                ? TransactionCatalog.GetCategory(record.Category, detailRequest.IsIncome).Key
                : TransactionCatalog.GetPaymentMethod(record.PaymentMethod).Key;
        return recordKey.Equals(
            detailRequest.OptionKey,
            StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<TransactionStatisticsDetailChartPoint> BuildDailyPoints(
        TransactionStatisticsDetailRequest detailRequest,
        IReadOnlyList<TransactionRecord> sourceRecords)
    {
        var totals = sourceRecords
            .GroupBy(record => record.TransactionDate.Day)
            .ToDictionary(group => group.Key, group => group.Sum(record => record.AmountMinor));
        var dayCount = DateTime.DaysInMonth(
            detailRequest.SourceMonth.Year,
            detailRequest.SourceMonth.Month);
        return Enumerable.Range(1, dayCount)
            .Select(day => new TransactionStatisticsDetailChartPoint(
                day,
                day.ToString(CultureInfo.CurrentCulture),
                totals.GetValueOrDefault(day)))
            .ToList();
    }

    private static IReadOnlyList<TransactionStatisticsDetailChartPoint> BuildMonthlyPoints(
        TransactionStatisticsDetailRequest detailRequest,
        IReadOnlyList<TransactionRecord> matchingRecords)
    {
        var totals = matchingRecords
            .GroupBy(record => new DateTime(
                record.TransactionDate.Year,
                record.TransactionDate.Month,
                1))
            .ToDictionary(group => group.Key, group => group.Sum(record => record.AmountMinor));
        var firstMonth = detailRequest.SourceMonth.AddMonths(-5);
        return Enumerable.Range(0, 6)
            .Select(offset => firstMonth.AddMonths(offset))
            .Select(month => new TransactionStatisticsDetailChartPoint(
                MonthKeyConverter.FromDate(month),
                month.ToString("MMM", CultureInfo.CurrentCulture),
                totals.GetValueOrDefault(month)))
            .ToList();
    }

    private void NotifyAllStateChanged()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(IconAsset));
        OnPropertyChanged(nameof(MonthLabel));
        OnPropertyChanged(nameof(ScopeLabel));
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(ChartPoints));
        OnPropertyChanged(nameof(ChartTitle));
        OnPropertyChanged(nameof(IsIncome));
        OnPropertyChanged(nameof(IsExpense));
        OnPropertyChanged(nameof(Groups));
        OnPropertyChanged(nameof(HasTransactions));
        OnPropertyChanged(nameof(HasNoTransactions));
    }
}
