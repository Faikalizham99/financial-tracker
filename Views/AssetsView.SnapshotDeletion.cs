namespace FinancialTracker.Views;

public partial class AssetsView
{
    private bool isClearingSnapshot;

    private async void OnClearMonthTapped(object? sender, TappedEventArgs e)
    {
        if (portfolioService is null || isLoadingEditor || isClearingSnapshot)
        {
            return;
        }

        var hostPage = FindHostPage();
        if (hostPage is null)
        {
            ShowEditorError("The confirmation could not be opened. Close this page and try again.");
            return;
        }

        var monthLabel = selectedMonth.ToString("MMMM yyyy");
        bool shouldClear;
        try
        {
            shouldClear = await hostPage.DisplayAlertAsync(
                $"Clear {monthLabel}?",
                "This removes the saved snapshot for this month. The Assets page will show that no snapshot has been added yet.",
                "Clear month",
                "Cancel");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Asset snapshot confirmation failed: {exception}");
            ShowEditorError("The confirmation could not be opened. Please try again.");
            return;
        }

        if (!shouldClear)
        {
            return;
        }

        isClearingSnapshot = true;
        ClearMonthAction.IsEnabled = false;
        SaveButton.IsEnabled = false;
        ShowEditorProgress("Clearing snapshot…");

        try
        {
            await portfolioService.DeleteAsync(selectedMonth);
            SnapshotChanged?.Invoke(selectedMonth);
            SetEditorVisibility(false);
            hasLoaded = false;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Asset snapshot deletion failed: {exception}");
            ShowEditorError("The snapshot could not be cleared. Please try again.");
        }
        finally
        {
            isClearingSnapshot = false;
            ClearMonthAction.IsEnabled = true;
            SaveButton.IsEnabled = true;
        }
    }

    private Page? FindHostPage()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is Page page)
            {
                return page;
            }

            current = current.Parent;
        }

        return null;
    }
}
