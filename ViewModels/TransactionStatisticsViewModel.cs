using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed partial class TransactionStatisticsViewModel(
    TransactionStatisticsService statisticsService) : ObservableObject
{
    private TransactionStatisticsData? data;
    private TransactionStatisticsMode selectedMode = TransactionStatisticsMode.All;
    private TransactionStatisticsBreakdownDimension selectedBreakdownDimension =
        TransactionStatisticsBreakdownDimension.Category;
    private TransactionStatisticsChartKind selectedChartKind =
        TransactionStatisticsChartKind.Bar;
    private string? selectedBreakdownKey;
    private DateTime highlightedMonth;
    private IReadOnlyList<TransactionStatisticsBreakdownItem> breakdownItems = [];

    public TransactionStatisticsMode SelectedMode
    {
        get => selectedMode;
        private set => SetProperty(ref selectedMode, value);
    }

    public TransactionStatisticsBreakdownDimension SelectedBreakdownDimension
    {
        get => selectedBreakdownDimension;
        private set => SetProperty(ref selectedBreakdownDimension, value);
    }

    public TransactionStatisticsChartKind SelectedChartKind
    {
        get => selectedChartKind;
        private set => SetProperty(ref selectedChartKind, value);
    }

    public string? SelectedBreakdownKey
    {
        get => selectedBreakdownKey;
        private set => SetProperty(ref selectedBreakdownKey, value);
    }

    public DateTime HighlightedMonth
    {
        get => highlightedMonth;
        private set => SetProperty(ref highlightedMonth, value);
    }

    public IReadOnlyList<TransactionStatisticsBreakdownItem> BreakdownItems
    {
        get => breakdownItems;
        private set => SetProperty(ref breakdownItems, value);
    }

    public IReadOnlyList<TransactionStatisticsChartPoint> ChartPoints => data is null
        ? []
        : data.DisplayMonths
            .Select(month => new TransactionStatisticsChartPoint(
                month.Month,
                GetMetric(month)))
            .ToList();

    public int HighlightedMonthKey => MonthKeyConverter.FromDate(HighlightedMonth);

    public string SourceMonthLabel => data?.SourceMonth.ToString(
        "MMMM yyyy",
        CultureInfo.CurrentCulture) ?? string.Empty;

    public string HeroTitle
    {
        get
        {
            var value = GetHighlightedMetric();
            return SelectedMode switch
            {
                TransactionStatisticsMode.Expense => "Spent",
                TransactionStatisticsMode.Income => "Received",
                _ when value < 0 => "Short",
                _ => "Kept"
            };
        }
    }

    public string HeroAmountText => FormatMoney(Math.Abs(GetHighlightedMetric()));

    public string HeroComparisonText
    {
        get
        {
            if (data is null)
            {
                return string.Empty;
            }

            var current = GetHighlightedMonth();
            var previous = GetMonth(current.Month.AddMonths(-1));
            return FormatDetailedComparison(
                GetMetric(current),
                previous is null ? null : GetMetric(previous),
                current.Month.AddMonths(-1));
        }
    }

    public string HeroDailyText
    {
        get
        {
            var month = GetHighlightedMonth();
            var dayCount = DateTime.DaysInMonth(month.Month.Year, month.Month.Month);
            return $"{FormatMoney(AveragePerDay(Math.Abs(GetMetric(month)), dayCount))} daily";
        }
    }

    public string ChartSelectionText =>
        $"{HighlightedMonth:MMM} \u00B7 {FormatMoney(GetHighlightedMetric())}";

    public string IncomeAmountText => FormatMoney(GetSourceMonth().IncomeMinor);
    public string ExpenseAmountText => FormatMoney(GetSourceMonth().ExpenseMinor);
    public string IncomeDailyText => FormatDaily(GetSourceMonth().IncomeMinor);
    public string ExpenseDailyText => FormatDaily(GetSourceMonth().ExpenseMinor);

    public string IncomeComparisonText => FormatCompactComparison(
        GetSourceMonth().IncomeMinor,
        GetPreviousSourceMonth().IncomeMinor);

    public string ExpenseComparisonText => FormatCompactComparison(
        GetSourceMonth().ExpenseMinor,
        GetPreviousSourceMonth().ExpenseMinor);

    public double IncomeProgress => GetSummaryProgress(GetSourceMonth().IncomeMinor);
    public double ExpenseProgress => GetSummaryProgress(GetSourceMonth().ExpenseMinor);

    public string BreakdownTitle =>
        $"{(UsesIncomeBreakdown ? "INCOME" : "EXPENSE")} BY " +
        (SelectedBreakdownDimension == TransactionStatisticsBreakdownDimension.Category
            ? "CATEGORY"
            : "PAYMENT METHOD");

    public string BreakdownTotalText => UsesIncomeBreakdown
        ? IncomeAmountText
        : ExpenseAmountText;

    public bool HasBreakdownItems => BreakdownItems.Count > 0;
    public bool HasNoBreakdownItems => !HasBreakdownItems;
    public bool HeroIsPositive => SelectedMode == TransactionStatisticsMode.Income ||
        (SelectedMode == TransactionStatisticsMode.All && GetHighlightedMetric() > 0);
    public bool HeroIsNegative => SelectedMode == TransactionStatisticsMode.Expense ||
        (SelectedMode == TransactionStatisticsMode.All && GetHighlightedMetric() < 0);

    private bool UsesIncomeBreakdown => SelectedMode == TransactionStatisticsMode.Income;

    public async Task LoadAsync(
        DateTime sourceMonth,
        CurrencyOption currency,
        bool includeInvestment)
    {
        data = await statisticsService.GetAsync(
            sourceMonth,
            currency,
            includeInvestment);
        SelectedMode = TransactionStatisticsMode.All;
        SelectedBreakdownDimension = TransactionStatisticsBreakdownDimension.Category;
        SelectedChartKind = TransactionStatisticsChartKind.Bar;
        SelectedBreakdownKey = null;
        HighlightedMonth = data.SourceMonth;
        RebuildBreakdown();
        NotifyAllStateChanged();
    }

    public async Task ReloadAsync(
        CurrencyOption currency,
        bool includeInvestment)
    {
        if (data is null)
        {
            return;
        }

        var retainedMode = SelectedMode;
        var retainedDimension = SelectedBreakdownDimension;
        var retainedChartKind = SelectedChartKind;
        var retainedBreakdownKey = SelectedBreakdownKey;
        data = await statisticsService.GetAsync(
            data.SourceMonth,
            currency,
            includeInvestment);
        SelectedMode = retainedMode;
        SelectedBreakdownDimension = retainedDimension;
        SelectedChartKind = retainedChartKind;
        SelectedBreakdownKey = retainedBreakdownKey;
        HighlightedMonth = data.SourceMonth;
        RebuildBreakdown();
        NotifyAllStateChanged();
    }

    public void SelectMode(TransactionStatisticsMode mode)
    {
        if (SelectedMode == mode)
        {
            return;
        }

        SelectedMode = mode;
        SelectedChartKind = TransactionStatisticsChartKind.Bar;
        SelectedBreakdownKey = null;
        if (data is not null)
        {
            HighlightedMonth = data.SourceMonth;
        }
        RebuildBreakdown();
        NotifyMetricStateChanged();
        NotifyBreakdownStateChanged();
    }

    public void SelectBreakdownDimension(TransactionStatisticsBreakdownDimension dimension)
    {
        if (SelectedBreakdownDimension == dimension)
        {
            return;
        }

        SelectedBreakdownDimension = dimension;
        SelectedBreakdownKey = null;
        RebuildBreakdown();
        NotifyBreakdownStateChanged();
        NotifyChartStateChanged();
    }

    public void SelectHighlightedMonth(DateTime month)
    {
        if (data is null ||
            data.DisplayMonths.All(item => item.Month != month) ||
            HighlightedMonth == month)
        {
            return;
        }

        HighlightedMonth = month;
        NotifyHeroStateChanged();
    }

    private long GetHighlightedMetric() => GetMetric(GetHighlightedMonth());

    private long GetMetric(TransactionStatisticsMonth month) => SelectedMode switch
    {
        TransactionStatisticsMode.Expense => month.ExpenseMinor,
        TransactionStatisticsMode.Income => month.IncomeMinor,
        _ => month.NetMinor
    };

    private TransactionStatisticsMonth GetHighlightedMonth() =>
        GetMonth(HighlightedMonth) ?? new TransactionStatisticsMonth(HighlightedMonth, 0, 0);

    private TransactionStatisticsMonth GetSourceMonth() =>
        data?.Timeline.LastOrDefault() ?? new TransactionStatisticsMonth(DateTime.Today, 0, 0);

    private TransactionStatisticsMonth GetPreviousSourceMonth() =>
        data?.Timeline.SkipLast(1).LastOrDefault() ??
        new TransactionStatisticsMonth(DateTime.Today.AddMonths(-1), 0, 0);

    private TransactionStatisticsMonth? GetMonth(DateTime month) =>
        data?.Timeline.FirstOrDefault(item => item.Month == month);

    private double GetSummaryProgress(long amountMinor)
    {
        var source = GetSourceMonth();
        var maximum = Math.Max(source.IncomeMinor, source.ExpenseMinor);
        return maximum <= 0 ? 0 : Math.Clamp((double)amountMinor / maximum, 0, 1);
    }

    private void NotifyAllStateChanged()
    {
        OnPropertyChanged(nameof(SourceMonthLabel));
        NotifyMetricStateChanged();
        NotifyBreakdownStateChanged();
    }

    private void NotifyMetricStateChanged()
    {
        OnPropertyChanged(nameof(ChartPoints));
        OnPropertyChanged(nameof(IncomeAmountText));
        OnPropertyChanged(nameof(ExpenseAmountText));
        OnPropertyChanged(nameof(IncomeDailyText));
        OnPropertyChanged(nameof(ExpenseDailyText));
        OnPropertyChanged(nameof(IncomeComparisonText));
        OnPropertyChanged(nameof(ExpenseComparisonText));
        OnPropertyChanged(nameof(IncomeProgress));
        OnPropertyChanged(nameof(ExpenseProgress));
        NotifyChartStateChanged();
        NotifyHeroStateChanged();
    }

    private void NotifyHeroStateChanged()
    {
        OnPropertyChanged(nameof(HighlightedMonthKey));
        OnPropertyChanged(nameof(HeroTitle));
        OnPropertyChanged(nameof(HeroAmountText));
        OnPropertyChanged(nameof(HeroComparisonText));
        OnPropertyChanged(nameof(HeroDailyText));
        OnPropertyChanged(nameof(ChartSelectionText));
        OnPropertyChanged(nameof(HeroIsPositive));
        OnPropertyChanged(nameof(HeroIsNegative));
    }

    private void NotifyBreakdownStateChanged()
    {
        OnPropertyChanged(nameof(BreakdownTitle));
        OnPropertyChanged(nameof(BreakdownTotalText));
        OnPropertyChanged(nameof(BreakdownItems));
        OnPropertyChanged(nameof(HasBreakdownItems));
        OnPropertyChanged(nameof(HasNoBreakdownItems));
    }
}
