namespace FinancialTracker;

public partial class MainPage
{
    private readonly SemaphoreSlim widgetAddTransactionLock = new(1, 1);
    private readonly SemaphoreSlim widgetAssetSnapshotLock = new(1, 1);
    private bool pendingWidgetAddTransaction;
    private decimal? pendingWidgetTransactionAmount;
    private bool pendingWidgetOpenAssets;
    private DateTime? pendingWidgetAssetSnapshotMonth;

    internal void RequestAddTransactionFromWidget(decimal? amount = null)
    {
        pendingWidgetAddTransaction = true;
        pendingWidgetTransactionAmount = amount is > 0
            ? amount
            : null;
        if (IsLoaded)
        {
            _ = OpenPendingWidgetAddTransactionAsync();
        }
    }

    internal void RequestAddAssetSnapshotFromWidget(DateTime? month)
    {
        var requestedMonth = month ?? DateTime.Today;
        pendingWidgetAssetSnapshotMonth = new DateTime(
            requestedMonth.Year,
            requestedMonth.Month,
            1);
        if (IsLoaded)
        {
            _ = OpenPendingWidgetAssetSnapshotAsync();
        }
    }

    internal void RequestOpenAssetsFromWidget()
    {
        pendingWidgetOpenAssets = true;
        if (IsLoaded)
        {
            _ = OpenPendingWidgetAssetSnapshotAsync();
        }
    }

    private async Task OpenPendingWidgetAssetSnapshotAsync()
    {
        await widgetAssetSnapshotLock.WaitAsync();
        try
        {
            if ((!pendingWidgetOpenAssets && pendingWidgetAssetSnapshotMonth is null) ||
                !IsLoaded)
            {
                return;
            }

            await EnsureInitialDataLoadedAsync();
            if (selectedSectionIndex != 2)
            {
                await NavigateToSectionAsync(2);
            }

            pendingWidgetOpenAssets = false;
            if (pendingWidgetAssetSnapshotMonth is DateTime month)
            {
                await AssetsView.OpenEditorForMonthAsync(month);
                pendingWidgetAssetSnapshotMonth = null;
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Widget asset snapshot link failed: {exception}");
        }
        finally
        {
            widgetAssetSnapshotLock.Release();
        }
    }

    private void OnAssetSnapshotChanged(DateTime _) =>
        assetWidgetSnapshotCoordinator.QueuePublish(
            settingsViewModel.SelectedCurrency,
            DateTime.Today);

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
                transactionDataStore.DescriptionHistoryRecords,
                initialAmount: pendingWidgetTransactionAmount);
            pendingWidgetAddTransaction = false;
            pendingWidgetTransactionAmount = null;
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
