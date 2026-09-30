using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class TransactionStatisticsView
{
    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseAsync();
        await feedback;
    }

    private async void OnCloseButtonClicked(object? sender, EventArgs e) =>
        await CloseAsync();

    private async void OnModeTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null ||
            e.Parameter is not string parameter ||
            !Enum.TryParse<TransactionStatisticsMode>(parameter, out var mode))
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        ViewModel.SelectMode(mode);
        RenderStatistics(animate: true);
        await feedback;
    }

    private async void OnBreakdownTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null ||
            e.Parameter is not string parameter ||
            !Enum.TryParse<TransactionStatisticsBreakdownDimension>(parameter, out var dimension))
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        ViewModel.SelectBreakdownDimension(dimension);
        UpdateBreakdownTabs();
        if (ViewModel.SelectedChartKind == TransactionStatisticsChartKind.Composition)
        {
            RenderCompositionChart(animate: true);
        }
        await feedback;
    }

    private async void OnChartKindTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null ||
            e.Parameter is not string parameter ||
            !Enum.TryParse<TransactionStatisticsChartKind>(parameter, out var chartKind))
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        ViewModel.SelectChartKind(chartKind);
        UpdateHeroVisuals();
        RenderChart(animate: true);
        await feedback;
    }

    private async void OnBreakdownItemTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null ||
            e.Parameter is not TransactionStatisticsBreakdownItem item)
        {
            return;
        }

        var request = ViewModel.CreateDetailRequest(item);
        if (request is null)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        await StatisticsDetailView.OpenAsync(request);
        await feedback;
    }

    private void OnChartInteraction(object? sender, TouchEventArgs e)
    {
        if (ViewModel is null ||
            e.Touches.Length == 0 ||
            !chartDrawable.Select(
                e.Touches[0],
                (float)MonthlyChart.Width,
                (float)MonthlyChart.Height))
        {
            return;
        }

        var selectedPoint = chartDrawable.SourcePoints.FirstOrDefault(
            point => MonthKeyConverter.FromDate(point.Month) == chartDrawable.SelectedMonthKey);
        if (selectedPoint is null)
        {
            return;
        }

        ViewModel.SelectHighlightedMonth(selectedPoint.Month);
        chartDrawable.AnimationProgress = 1f;
        MonthlyChart.Invalidate();
        UpdateHeroVisuals();
    }

    private void OnCompositionChartInteraction(object? sender, TouchEventArgs e)
    {
        if (ViewModel is null ||
            e.Touches.Length == 0 ||
            !compositionDrawable.Select(
                e.Touches[0],
                (float)CompositionChart.Width,
                (float)CompositionChart.Height))
        {
            return;
        }

        ViewModel.SelectCompositionItem(compositionDrawable.SelectedKey);
        RenderCompositionChart(animate: false);
        AnimateCompositionSelection();
    }

    private void OnStatisticsRootSizeChanged(object? sender, EventArgs e)
    {
        if (StatisticsRoot.Width > 0)
        {
            StatisticsContent.WidthRequest = Math.Min(
                940,
                Math.Max(320, StatisticsRoot.Width));
        }
    }
}
