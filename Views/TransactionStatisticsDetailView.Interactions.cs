using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class TransactionStatisticsDetailView
{
    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseAsync();
        await feedback;
    }

    private async void OnRangeTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null ||
            e.Parameter is not string parameter ||
            !Enum.TryParse<TransactionStatisticsDetailRange>(parameter, out var range))
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        ViewModel.SelectRange(range);
        UpdateRangeTabs();
        RenderChart(animate: true);
        await feedback;
    }

    private void OnActivityGroupHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is TransactionActivityGroup group)
        {
            group.IsExpanded = !group.IsExpanded;
        }
    }

    private void OnTransactionEditRequested(
        object? sender,
        TransactionEditRequestedEventArgs e) =>
        TransactionEditRequested?.Invoke(e.Item.Id);

    private void OnDetailRootSizeChanged(object? sender, EventArgs e)
    {
        if (DetailRoot.Width <= 0)
        {
            return;
        }

        var contentWidth = Math.Min(940, Math.Max(320, DetailRoot.Width));
        DetailHeader.WidthRequest = contentWidth;
        DetailContent.WidthRequest = contentWidth;
    }
}
