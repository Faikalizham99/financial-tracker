using FinancialTracker.Helpers;

namespace FinancialTracker.Views;

public partial class AssetHistoryView
{
    private void OnCloseTapped(object? sender, TappedEventArgs e) => Close();

    private void OnCloseButtonClicked(object? sender, EventArgs e) => Close();

    private void OnKwspToggleTapped(object? sender, TappedEventArgs e)
    {
        includeKwsp = !includeKwsp;
        donutChartDrawable.ClearSelection();
        RenderHistory(animateCharts: true);
    }

    private void OnSixMonthsTapped(object? sender, TappedEventArgs e) =>
        SetRange(HistoryRange.SixMonths);

    private void OnTwelveMonthsTapped(object? sender, TappedEventArgs e) =>
        SetRange(HistoryRange.TwelveMonths);

    private void OnAllMonthsTapped(object? sender, TappedEventArgs e) =>
        SetRange(HistoryRange.All);

    private void SetRange(HistoryRange range)
    {
        if (selectedRange == range)
        {
            return;
        }

        selectedRange = range;
        RenderHistory(animateCharts: true);
    }

    private void OnHistoryBarInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0 ||
            !barChartDrawable.Select(
                e.Touches[0],
                (float)HistoryBarChart.Width,
                (float)HistoryBarChart.Height))
        {
            return;
        }

        var selectedPoint = barChartDrawable.SourcePoints.FirstOrDefault(
            item => MonthKeyConverter.FromDate(item.Month) == barChartDrawable.SelectedMonthKey);
        if (selectedPoint is null)
        {
            return;
        }

        selectedMonth = selectedPoint.Month;
        donutChartDrawable.ClearSelection();
        barChartDrawable.AnimationProgress = 1f;
        HistoryBarChart.Invalidate();
        RenderSelectedMonth(animateDonut: true);
        AnimateCharts(animate: true, animateBar: false, animateDonut: true);
    }

    private void OnDonutInteraction(object? sender, TouchEventArgs e)
    {
        var previousKey = donutChartDrawable.SelectedSlice?.Key;
        if (e.Touches.Length == 0 || history is null ||
            !donutChartDrawable.Select(
                e.Touches[0],
                (float)DonutChart.Width,
                (float)DonutChart.Height))
        {
            return;
        }

        AnimateDonutSelection(previousKey);
        UpdateCompositionLegendSelection();
    }

    private void OnHistoryRootSizeChanged(object? sender, EventArgs e)
    {
        if (HistoryRoot.Width <= 0)
        {
            return;
        }

        HistoryContent.WidthRequest = Math.Min(940, Math.Max(320, HistoryRoot.Width));
    }

    private void OnHistoryChartScrollSizeChanged(object? sender, EventArgs e)
    {
        if (barChartDrawable.SourcePoints.Count > 0)
        {
            UpdateChartWidth(barChartDrawable.SourcePoints.Count);
        }
    }

    private void QueueChartScrollToSelection()
    {
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await Task.Yield();
                var points = barChartDrawable.SourcePoints;
                var selectedKey = MonthKeyConverter.FromDate(selectedMonth);
                var index = points
                    .Select((point, pointIndex) => new
                    {
                        Key = MonthKeyConverter.FromDate(point.Month),
                        Index = pointIndex
                    })
                    .FirstOrDefault(item => item.Key == selectedKey)
                    ?.Index;
                if (!index.HasValue || points.Count == 0 || HistoryChartScroll.Width <= 0)
                {
                    return;
                }

                var selectedCenter = HistoryBarChart.Width * (index.Value + 0.5d) / points.Count;
                var targetX = Math.Max(0, selectedCenter - (HistoryChartScroll.Width / 2d));
                await HistoryChartScroll.ScrollToAsync(targetX, 0, false);
            }
            catch (ObjectDisposedException)
            {
                // The history surface may close while the non-blocking scroll is queued.
            }
        });
    }
}
