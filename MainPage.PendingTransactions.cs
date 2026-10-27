using FinancialTracker.Models;

namespace FinancialTracker;

public partial class MainPage
{
    private readonly SemaphoreSlim pendingNotificationNavigationLock = new(1, 1);
    private bool pendingOpenPendingTransactionsFromNotification;

    internal void RequestOpenPendingTransactionsFromNotification()
    {
        pendingOpenPendingTransactionsFromNotification = true;
        if (IsLoaded)
        {
            _ = OpenPendingTransactionsFromNotificationAsync();
        }
    }

    private async Task OpenPendingTransactionsFromNotificationAsync()
    {
        await pendingNotificationNavigationLock.WaitAsync();
        try
        {
            if (!pendingOpenPendingTransactionsFromNotification || !IsLoaded)
            {
                return;
            }

            await EnsureInitialDataLoadedAsync();
            if (selectedSectionIndex != 1)
            {
                await NavigateToSectionAsync(1);
            }
            await RefreshPendingTransactionInboxAsync();
            await PendingTransactionsOverlay.OpenAsync();
            pendingOpenPendingTransactionsFromNotification = false;
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Pending capture notification link failed: {exception}");
        }
        finally
        {
            pendingNotificationNavigationLock.Release();
        }
    }

    private async void OnPendingTransactionsRequested()
    {
        await PendingTransactionsOverlay.OpenAsync();
    }

    private void OnPendingTransactionsVisibilityChanged(bool isVisible)
    {
        BottomNavigationDock.Opacity = isVisible ? 0 : 1;
        BottomNavigationDock.InputTransparent = isVisible;
        BottomNavigationDock.IsVisible = !isVisible;
    }

    private async Task RefreshPendingTransactionInboxAsync()
    {
        try
        {
            var count = await pendingTransactionInbox.GetPendingCountAsync();
            ExpensesView.SetPendingTransactionCount(count);
            if (PendingTransactionsOverlay.IsOpen)
            {
                await PendingTransactionsOverlay.RefreshAsync();
            }
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Pending transaction inbox refresh failed: {exception}");
            ExpensesView.SetPendingTransactionInboxError(exception.Message);
        }
    }

    private Task OnPendingTransactionCountChangedAsync(int count)
    {
        ExpensesView.SetPendingTransactionCount(count);
        return Task.CompletedTask;
    }

    private async Task OnPendingTransactionApprovedAsync()
    {
        await OnTransactionSavedAsync();
    }

    private async Task OpenPendingTransactionForEditAsync(
        PendingTransactionRecord pending)
    {
        var existing = await localDatabase.GetTransactionByImportKeyAsync(
            pending.CaptureKey);
        if (existing is not null)
        {
            await pendingTransactionInbox.DeleteAsync(pending.Id);
            await RefreshPendingTransactionInboxAsync();
            await OpenTransactionForEditAsync(existing.Id, allowWhileLocked: true);
            return;
        }

        var draft = pending.CreateTransaction(settingsViewModel.SelectedCurrency.Code);
        await AddTransactionOverlay.OpenAsync(
            localDatabase,
            settingsViewModel.SelectedCurrency,
            transactionDataStore.DescriptionHistoryRecords,
            transactionDraft: draft,
            afterSaved: async _ =>
            {
                await pendingTransactionInbox.DeleteAsync(pending.Id);
                await RefreshPendingTransactionInboxAsync();
            });
    }
}
