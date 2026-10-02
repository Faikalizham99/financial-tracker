namespace FinancialTracker;

public partial class MainPage
{
    private readonly SemaphoreSlim widgetAddTransactionLock = new(1, 1);
    private bool pendingWidgetAddTransaction;

    internal void RequestAddTransactionFromWidget()
    {
        pendingWidgetAddTransaction = true;
        if (IsLoaded)
        {
            _ = OpenPendingWidgetAddTransactionAsync();
        }
    }

    private async Task OpenPendingWidgetAddTransactionAsync()
    {
        await widgetAddTransactionLock.WaitAsync();
        try
        {
            if (!pendingWidgetAddTransaction || !IsLoaded)
            {
                return;
            }

            await EnsureInitialDataLoadedAsync();
            if (selectedSectionIndex != 1)
            {
                await NavigateToSectionAsync(1);
            }

            if (AddTransactionOverlay.IsVisible)
            {
                await AddTransactionOverlay.CloseAsync();
            }

            await AddTransactionOverlay.OpenAsync(
                localDatabase,
                settingsViewModel.SelectedCurrency,
                transactionDataStore.DescriptionHistoryRecords);
            pendingWidgetAddTransaction = false;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Widget add transaction link failed: {exception}");
        }
        finally
        {
            widgetAddTransactionLock.Release();
        }
    }
}
