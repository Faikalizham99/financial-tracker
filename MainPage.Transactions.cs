using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Views;
using Microsoft.Maui.Storage;

namespace FinancialTracker;

public partial class MainPage
{
    private async void OnTransactionEditRequested(int transactionId)
    {
        if (!isTransactionEditingLocked)
        {
            await OpenTransactionForEditAsync(transactionId);
        }
    }

    private async void OnTransactionQuickEditRequested(int transactionId) =>
        await OpenTransactionForEditAsync(transactionId, allowWhileLocked: true);

    private async void OnTransactionDeleteRequested(int transactionId)
    {
        if (!isTransactionEditingLocked)
        {
            await OpenDeleteConfirmationAsync(transactionId);
        }
    }

    private void OnTransactionEditingLockToggleRequested(object? sender, EventArgs e) =>
        SetTransactionEditingLocked(!isTransactionEditingLocked);

    private void OnInvestmentInclusionToggleRequested(object? sender, EventArgs e)
    {
        includeInvestmentInTotals = !includeInvestmentInTotals;
        Preferences.Default.Set(
            IncludeInvestmentInTotalsPreferenceKey,
            includeInvestmentInTotals);
        UpdateInvestmentInclusionState();
    }

    private void UpdateInvestmentInclusionState()
    {
        DashboardMonthlySummary.IncludeInvestmentInTotals = includeInvestmentInTotals;
        ExpensesView.SetIncludeInvestmentInTotals(includeInvestmentInTotals);
        if (transactionDataStore.IsLoaded)
        {
            RefreshDashboard(
                transactionDataStore.DashboardRecords,
                settingsViewModel.SelectedCurrency);
        }
    }

    private void SetTransactionEditingLocked(bool isLocked)
    {
        if (isTransactionEditingLocked == isLocked)
        {
            return;
        }

        isTransactionEditingLocked = isLocked;
        DashboardMonthlySummary.IsTransactionEditingLocked = isLocked;
        ExpensesView.SetTransactionEditingLocked(isLocked);

        if (transactionDataStore.IsLoaded)
        {
            RefreshDashboard(
                transactionDataStore.DashboardRecords,
                settingsViewModel.SelectedCurrency);
            Dispatcher.Dispatch(UpdateDashboardTransactionContextMenus);
        }

        _ = ShowTransactionLockToastAsync(isLocked);
    }

    private async Task ShowTransactionLockToastAsync(bool isLocked)
    {
        var previousToast = transactionLockToastCancellation;
        transactionLockToastCancellation = null;
        if (previousToast is not null)
        {
            previousToast.Cancel();
            previousToast.Dispose();
        }

        var cancellation = new CancellationTokenSource();
        transactionLockToastCancellation = cancellation;
        var message = isLocked
            ? "Transactions locked"
            : "Edit and delete enabled";

        TransactionLockToast.CancelAnimations();
        TransactionLockedToastIcon.IsVisible = isLocked;
        TransactionEditingToastIcon.IsVisible = !isLocked;
        TransactionLockToastLabel.Text = message;
        TransactionLockToast.Opacity = 0;
        TransactionLockToast.TranslationY = 10;
        TransactionLockToast.IsVisible = true;
        SemanticScreenReader.Default.Announce(message);

        try
        {
            await Task.WhenAll(
                TransactionLockToast.FadeToAsync(1, 140, Easing.CubicOut),
                TransactionLockToast.TranslateToAsync(0, 0, 170, Easing.CubicOut));
            await Task.Delay(1700, cancellation.Token);
            await Task.WhenAll(
                TransactionLockToast.FadeToAsync(0, 170, Easing.CubicIn),
                TransactionLockToast.TranslateToAsync(0, 8, 170, Easing.CubicIn));
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(transactionLockToastCancellation, cancellation))
            {
                TransactionLockToast.CancelAnimations();
                TransactionLockToast.IsVisible = false;
                TransactionLockToast.Opacity = 0;
                TransactionLockToast.TranslationY = 10;
                transactionLockToastCancellation = null;
                cancellation.Dispose();
            }
        }
    }

    private async void OnDashboardEditTransactionInvoked(object? sender, EventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (!isTransactionEditingLocked &&
            sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            await OpenTransactionForEditAsync(item.Id);
        }

        await feedback;
    }

    private async void OnDashboardDeleteTransactionInvoked(object? sender, EventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (!isTransactionEditingLocked &&
            sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            await OpenDeleteConfirmationAsync(item.Id);
        }

        await feedback;
    }

    private void OnDashboardTransactionDescriptionToggled(
        object? sender,
        TransactionDescriptionToggledEventArgs e)
    {
        if (e.IsExpanded)
        {
            foreach (var item in dashboardRecentActivity)
            {
                if (item.Id != e.Item.Id)
                {
                    item.SetDescriptionExpanded(false);
                }
            }

            expandedDashboardTransactionDescriptionId = e.Item.Id;
        }
        else if (expandedDashboardTransactionDescriptionId == e.Item.Id)
        {
            expandedDashboardTransactionDescriptionId = null;
        }
    }

    private void OnDashboardTransactionRowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Grid row)
        {
            ConfigureDashboardTransactionContextMenu(row);
        }
    }

    private void UpdateDashboardTransactionContextMenus()
    {
        foreach (var row in DashboardActivityLayout
            .GetVisualTreeDescendants()
            .OfType<Grid>()
            .Where(view => view.ClassId == "TransactionContextMenuTarget"))
        {
            ConfigureDashboardTransactionContextMenu(row);
        }
    }

    private void ConfigureDashboardTransactionContextMenu(Grid row)
    {
#if WINDOWS
        if (row.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement nativeRow)
        {
            return;
        }

        nativeRow.ContextFlyout = null;
        if (isTransactionEditingLocked)
        {
            return;
        }

        var editItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Edit" };
        editItem.Click += async (_, _) =>
        {
            if (!isTransactionEditingLocked &&
                row.BindingContext is TransactionActivityItem item)
            {
                await OpenTransactionForEditAsync(item.Id);
            }
        };

        var deleteItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += async (_, _) =>
        {
            if (!isTransactionEditingLocked &&
                row.BindingContext is TransactionActivityItem item)
            {
                await OpenDeleteConfirmationAsync(item.Id);
            }
        };

        var flyout = new Microsoft.UI.Xaml.Controls.MenuFlyout();
        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);
        nativeRow.ContextFlyout = flyout;
#endif
    }

    private async Task<TransactionRecord?> FindTransactionAsync(int transactionId)
    {
        var cachedTransaction = transactionDataStore.FindCached(transactionId);
        if (cachedTransaction is not null)
        {
            return cachedTransaction;
        }

        TransactionRecord? storedTransaction = null;
        await RunWithDataLoadingSkeletonAsync(async () =>
        {
            storedTransaction = await transactionDataStore.FindStoredAsync(transactionId);
            if (storedTransaction is null)
            {
                await RefreshTransactionViewsAsync();
            }
        });

        return storedTransaction;
    }

    private async Task OpenTransactionForEditAsync(
        int transactionId,
        bool allowWhileLocked = false)
    {
        if (isTransactionEditingLocked && !allowWhileLocked)
        {
            return;
        }

        var transaction = await FindTransactionAsync(transactionId);
        if (transaction is null)
        {
            return;
        }

        await AddTransactionOverlay.OpenAsync(
            localDatabase,
            settingsViewModel.SelectedCurrency,
            transactionDataStore.DescriptionHistoryRecords,
            transaction);
    }

    private async Task OpenDeleteConfirmationAsync(int transactionId)
    {
        if (isTransactionEditingLocked ||
            DeleteConfirmationOverlay.IsVisible ||
            isDeleteConfirmationAnimating)
        {
            return;
        }

        var transaction = await FindTransactionAsync(transactionId);
        if (transaction is null)
        {
            return;
        }

        pendingDeleteTransaction = transaction;
        var transactionActivity = TransactionActivityItem.FromRecord(
            transaction,
            settingsViewModel.SelectedCurrency.Symbol);
        DeleteCategoryIcon.Source = transactionActivity.IconAsset;
        DeletePaymentMethodIcon.Source = transactionActivity.PaymentMethodIconAsset;
        var transactionDate = transaction.TransactionDate.ToString(
            "dddd, d MMMM yyyy",
            CultureInfo.CurrentCulture);
        DeleteDescriptionLabel.Text =
            $"“{transaction.Description} on {transactionDate}” will be permanently removed. This cannot be undone.";
        UpdateDeleteConfirmationCardWidth();
        DeleteConfirmLabel.Text = "Delete";
        DeleteConfirmButton.IsEnabled = true;
        DeleteConfirmationOverlay.IsVisible = true;
        DeleteConfirmationOverlay.Opacity = 0;
        DeleteConfirmationCard.Opacity = 0;
        DeleteConfirmationCard.Scale = 0.96;
        DeleteConfirmationCard.TranslationY = 16;
        isDeleteConfirmationAnimating = true;

        try
        {
            await Task.WhenAll(
                DeleteConfirmationOverlay.FadeToAsync(1, 150, Easing.CubicOut),
                DeleteConfirmationCard.FadeToAsync(1, 180, Easing.CubicOut),
                DeleteConfirmationCard.ScaleToAsync(1, 210, Easing.CubicOut),
                DeleteConfirmationCard.TranslateToAsync(0, 0, 210, Easing.CubicOut));
        }
        finally
        {
            isDeleteConfirmationAnimating = false;
        }
    }

    private async void OnDeleteConfirmationBackdropTapped(object? sender, TappedEventArgs e) =>
        await CloseDeleteConfirmationAsync();

    private async void OnDeleteConfirmationCancelTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseDeleteConfirmationAsync();
        await feedback;
    }

    private async void OnDeleteConfirmationConfirmedTapped(object? sender, TappedEventArgs e)
    {
        if (isTransactionEditingLocked ||
            pendingDeleteTransaction is null ||
            isDeletingTransaction)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        isDeletingTransaction = true;
        DeleteConfirmButton.IsEnabled = false;
        DeleteConfirmButton.Opacity = 0.72;
        DeleteConfirmLabel.Text = "Deleting…";

        try
        {
            var transactionId = pendingDeleteTransaction.Id;
            await RunWithDataLoadingSkeletonAsync(async () =>
            {
                await localDatabase.DeleteTransactionAsync(transactionId);
                isDeletingTransaction = false;
                await CloseDeleteConfirmationAsync();
                await RefreshTransactionViewsAsync();
            });
        }
        catch
        {
            DeleteConfirmLabel.Text = "Try again";
            await Task.Delay(900);
        }
        finally
        {
            isDeletingTransaction = false;
            DeleteConfirmButton.IsEnabled = true;
            DeleteConfirmButton.Opacity = 1;
            if (DeleteConfirmationOverlay.IsVisible)
            {
                DeleteConfirmLabel.Text = "Delete";
            }

            await feedback;
        }
    }

    private async Task CloseDeleteConfirmationAsync()
    {
        if (!DeleteConfirmationOverlay.IsVisible ||
            isDeleteConfirmationAnimating ||
            isDeletingTransaction)
        {
            return;
        }

        isDeleteConfirmationAnimating = true;
        try
        {
            await Task.WhenAll(
                DeleteConfirmationOverlay.FadeToAsync(0, 130, Easing.CubicIn),
                DeleteConfirmationCard.ScaleToAsync(0.97, 150, Easing.CubicIn),
                DeleteConfirmationCard.TranslateToAsync(0, 12, 150, Easing.CubicIn));
        }
        finally
        {
            DeleteConfirmationOverlay.IsVisible = false;
            DeleteConfirmationOverlay.Opacity = 0;
            DeleteConfirmationCard.Opacity = 1;
            DeleteConfirmationCard.Scale = 1;
            DeleteConfirmationCard.TranslationY = 0;
            pendingDeleteTransaction = null;
            isDeleteConfirmationAnimating = false;
        }
    }

    private void OnDeleteConfirmationOverlaySizeChanged(object? sender, EventArgs e) =>
        UpdateDeleteConfirmationCardWidth();

    private void UpdateDeleteConfirmationCardWidth()
    {
        var availableWidth = DeleteConfirmationOverlay.Width > 0
            ? DeleteConfirmationOverlay.Width
            : Width;
        if (availableWidth > 0)
        {
            DeleteConfirmationCard.WidthRequest = Math.Min(
                420,
                Math.Max(280, availableWidth - 40));
        }
    }

    private async Task RefreshTransactionViewsAsync()
    {
        await RefreshTransactionViewsCoreAsync(recoverSuspiciousEmptyRead: false);
    }

    private async Task RefreshTransactionViewsAfterResumeAsync()
    {
        await RefreshTransactionViewsCoreAsync(
            recoverSuspiciousEmptyRead:
                transactionDataStore.IsLoaded &&
                transactionDataStore.DashboardRecords.Count == 0);
    }

    private async Task RefreshTransactionViewsCoreAsync(
        bool recoverSuspiciousEmptyRead)
    {
        var snapshot = await transactionDataStore.RefreshAsync(
            DateTime.Today,
            useStartupRecovery: recoverSuspiciousEmptyRead);
        if (recoverSuspiciousEmptyRead && snapshot.DashboardRecords.Count > 0)
        {
            // The database may have appeared after an early iOS or
            // LiveContainer read. Reload appearance and currency from the
            // same recovered file before rendering its transactions.
            await settingsViewModel.ReloadAsync();
        }

        var currency = settingsViewModel.SelectedCurrency;
        TransactionSearchView.SetCurrency(currency);
        RefreshDashboard(snapshot.DashboardRecords, currency);
        await ExpensesView.RefreshTransactionsAsync(
            snapshot.PeriodRecords,
            DateTime.Today,
            currency);
    }
}
