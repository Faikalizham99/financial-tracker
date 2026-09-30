using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed partial class TransactionStatisticsViewModel
{
    public bool ShowsChartKindSelector => SelectedMode != TransactionStatisticsMode.All;

    public IReadOnlyList<TransactionStatisticsRunningTotalPoint> RunningTotalPoints
    {
        get
        {
            if (data is null || SelectedMode == TransactionStatisticsMode.All)
            {
                return [];
            }

            var sourceDayCount = DateTime.DaysInMonth(
                data.SourceMonth.Year,
                data.SourceMonth.Month);
            var previousMonth = data.SourceMonth.AddMonths(-1);
            var previousDayCount = DateTime.DaysInMonth(
                previousMonth.Year,
                previousMonth.Month);
            var currentDaily = BuildDailyTotals(data.SourceMonthRecords, sourceDayCount);
            var previousDaily = BuildDailyTotals(data.PreviousMonthRecords, previousDayCount);
            var points = new List<TransactionStatisticsRunningTotalPoint>(sourceDayCount);
            long currentRunning = 0;
            long previousRunning = 0;

            for (var day = 1; day <= sourceDayCount; day++)
            {
                currentRunning += currentDaily[day];
                if (day <= previousDayCount)
                {
                    previousRunning += previousDaily[day];
                }

                points.Add(new TransactionStatisticsRunningTotalPoint(
                    day,
                    currentRunning,
                    previousRunning));
            }

            return points;
        }
    }

    public IReadOnlyList<TransactionStatisticsCompositionSlice> CompositionSlices =>
        BreakdownItems
            .Select(item => new TransactionStatisticsCompositionSlice(
                item.Key,
                item.Title,
                item.AmountMinor,
                item.ChartColor))
            .ToList();

    public string ChartHeading => SelectedChartKind switch
    {
        TransactionStatisticsChartKind.RunningTotal => "RUNNING TOTAL",
        TransactionStatisticsChartKind.Composition =>
            $"COMPOSITION BY {(SelectedBreakdownDimension == TransactionStatisticsBreakdownDimension.Category ? "CATEGORY" : "PAYMENT METHOD")}",
        _ => "LAST 6 MONTHS"
    };

    public string ChartHint => SelectedChartKind switch
    {
        TransactionStatisticsChartKind.RunningTotal =>
            $"Compared with {data?.SourceMonth.AddMonths(-1):MMMM yyyy}",
        TransactionStatisticsChartKind.Composition =>
            "Tap a segment to highlight its breakdown",
        _ => "Tap a bar to preview that month's value"
    };

    public string ChartContextText => SelectedChartKind switch
    {
        TransactionStatisticsChartKind.RunningTotal when data is not null =>
            $"{data.SourceMonth:MMM} {FormatMoney(GetMetric(GetSourceMonth()))} \u00B7 " +
            $"{data.SourceMonth.AddMonths(-1):MMM} {FormatMoney(GetMetric(GetPreviousSourceMonth()))}",
        TransactionStatisticsChartKind.Composition => BreakdownTotalText,
        _ => ChartSelectionText
    };

    public void SelectChartKind(TransactionStatisticsChartKind chartKind)
    {
        var normalizedKind = SelectedMode == TransactionStatisticsMode.All
            ? TransactionStatisticsChartKind.Bar
            : chartKind;
        if (SelectedChartKind == normalizedKind)
        {
            return;
        }

        SelectedChartKind = normalizedKind;
        SelectedBreakdownKey = null;
        if (data is not null)
        {
            HighlightedMonth = data.SourceMonth;
        }

        RebuildBreakdown();
        NotifyChartStateChanged();
        NotifyBreakdownStateChanged();
        NotifyHeroStateChanged();
    }

    public void SelectCompositionItem(string? key)
    {
        if (SelectedChartKind != TransactionStatisticsChartKind.Composition)
        {
            return;
        }

        SelectedBreakdownKey = string.Equals(
            SelectedBreakdownKey,
            key,
            StringComparison.OrdinalIgnoreCase)
                ? null
                : key;
        RebuildBreakdown();
        NotifyBreakdownStateChanged();
        OnPropertyChanged(nameof(CompositionSlices));
    }

    private long[] BuildDailyTotals(
        IReadOnlyList<TransactionRecord> records,
        int dayCount)
    {
        var totals = new long[dayCount + 1];
        foreach (var record in records)
        {
            var matchesType = UsesIncomeBreakdown
                ? TransactionCatalog.IsIncomeType(record.Type)
                : TransactionCatalog.IsExpenseType(record.Type);
            if (matchesType && record.TransactionDate.Day <= dayCount)
            {
                totals[record.TransactionDate.Day] += record.AmountMinor;
            }
        }

        return totals;
    }

    private void NotifyChartStateChanged()
    {
        OnPropertyChanged(nameof(SelectedChartKind));
        OnPropertyChanged(nameof(ShowsChartKindSelector));
        OnPropertyChanged(nameof(RunningTotalPoints));
        OnPropertyChanged(nameof(CompositionSlices));
        OnPropertyChanged(nameof(ChartHeading));
        OnPropertyChanged(nameof(ChartHint));
        OnPropertyChanged(nameof(ChartContextText));
    }
}
