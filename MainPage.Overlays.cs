namespace FinancialTracker;

public partial class MainPage
{
    private async void OnTransactionSearchRequested() =>
        await TransactionSearchView.OpenAsync();

    private async Task OnTransactionSearchResultSelected(
        int transactionId,
        DateTime transactionDate)
    {
        // Replace the search surface and the transaction skeleton in one UI
        // frame. Fading the search view over the skeleton causes both layouts
        // to be composited together, which is especially visible on iOS.
        TransactionSearchView.CloseImmediately();
        ShowTransactionLoadingSkeleton();
        await Task.Yield();

        try
        {
            if (selectedSectionIndex != 1)
            {
                await NavigateToSectionAsync(1);
            }

            await ExpensesView.FocusTransactionAsync(
                transactionId,
                transactionDate,
                HideLoadingSkeletonAsync);
        }
        finally
        {
            // FocusTransactionAsync also reveals the page before scrolling.
            // This fallback guarantees that a missing or cancelled target can
            // never leave the transition skeleton on screen.
            await HideLoadingSkeletonAsync();
        }
    }

    private void OnSettingsDataDrawerVisibilityChanged(bool isVisible)
    {
        // Keep the dock in its existing native layer to avoid reordering the
        // visual tree while a pointer gesture is completing. The drawer can
        // then cover the same space without the dock drawing or handling input.
        BottomNavigationDock.Opacity = isVisible ? 0 : 1;
        BottomNavigationDock.InputTransparent = isVisible;
    }

    private void OnAssetsEditorVisibilityChanged(bool isVisible)
    {
        BottomNavigationDock.InputTransparent = isVisible;
        BottomNavigationDock.Opacity = isVisible ? 0 : 1;
        BottomNavigationDock.IsVisible = !isVisible;
    }

    private async void OnBudgetSettingsRequested(DateTime month)
    {
        try
        {
            await BudgetSettingsOverlay.OpenAsync(month);
        }
        catch
        {
            await DisplayAlertAsync(
                "Budget unavailable",
                "Financial Tracker could not load the budget settings. Please try again.",
                "OK");
        }
    }

    private void OnBudgetSettingsVisibilityChanged(bool isVisible)
    {
        BottomNavigationDock.Opacity = isVisible ? 0 : 1;
        BottomNavigationDock.InputTransparent = isVisible;
    }

    private async void OnBudgetSaved(object? sender, EventArgs e)
    {
        await settingsViewModel.RefreshCurrentBudgetStatusAsync();
        await ReloadMonthlyBudgetCardsAsync();
    }

    private Task ReloadMonthlyBudgetCardsAsync() => Task.WhenAll(
        DashboardMonthlySummary.ReloadBudgetAsync(),
        ExpensesView.ReloadBudgetAsync());
}
