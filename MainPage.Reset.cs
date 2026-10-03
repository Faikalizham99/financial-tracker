using FinancialTracker.Models;
using Microsoft.Maui.Storage;

namespace FinancialTracker;

public partial class MainPage
{
    private async Task OnDatabaseResetRequested()
    {
        var shouldContinue = await DisplayAlertAsync(
            "Erase all Financial Tracker data?",
            "This will remove every transaction, budget, asset snapshot, profile setting and local preference. Create a backup first if you may need this data again.",
            "Continue",
            "Cancel");
        if (!shouldContinue)
        {
            return;
        }

        var shouldReset = await DisplayAlertAsync(
            "This cannot be undone",
            $"Everything will be permanently erased and a fresh profile named {AppSettingsRecord.DefaultName} will be created.",
            "Erase everything",
            "Cancel");
        if (!shouldReset)
        {
            return;
        }

        var databaseReset = false;
        try
        {
            await RunWithDataLoadingSkeletonAsync(async () =>
            {
                await localDatabase.ResetAllDataAsync();
                databaseReset = true;

                Preferences.Default.Clear();
                monthlyBudgetService.InvalidateCache();
                includeInvestmentInTotals = true;
                widgetSettingsStore.TryWriteIncludeInvestment(
                    includeInvestmentInTotals);
                expandedDashboardTransactionDescriptionId = null;
                TransactionSearchView.ResetState();
                LoadHomeSectionOrder();
                ApplyHomeSectionOrder();

                await settingsViewModel.ReloadAsync();
                await RefreshTransactionViewsAsync();
                UpdateInvestmentInclusionState();
                await ReloadMonthlyBudgetCardsAsync();
                await AssetsView.RefreshAsync();
            });

            await DisplayAlertAsync(
                "Reset complete",
                $"Financial Tracker is ready for a fresh start. Welcome, {AppSettingsRecord.DefaultName}.",
                "OK");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Database reset failed: {exception}");
            await DisplayAlertAsync(
                databaseReset ? "Restart required" : "Reset failed",
                databaseReset
                    ? "Your data was erased, but the screen could not fully refresh. Close and reopen Financial Tracker to finish resetting the app."
                    : "Financial Tracker could not erase your data. Nothing was changed.",
                "OK");
        }
    }
}
