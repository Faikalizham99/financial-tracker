namespace FinancialTracker;

using System.ComponentModel;
using FinancialTracker.ViewModels;
using FinancialTracker.Data;
using FinancialTracker.Services;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

public partial class MainPage : ContentPage
{
    private readonly SettingsViewModel settingsViewModel;
    private readonly LocalDatabase localDatabase;
    private readonly SemaphoreSlim transactionRefreshLock = new(1, 1);
    private int selectedSectionIndex = 1;
    private int navigationTransitionVersion;
    private TransactionRecord? pendingDeleteTransaction;
    private bool isDeleteConfirmationAnimating;
    private bool isDeletingTransaction;

    public MainPage()
        : this(new LocalDatabase())
    {
    }

    private MainPage(LocalDatabase localDatabase)
        : this(
            new SettingsViewModel(new SettingsService(localDatabase)),
            localDatabase)
    {
    }

    public MainPage(SettingsViewModel settingsViewModel)
        : this(settingsViewModel, new LocalDatabase())
    {
    }

    private MainPage(
        SettingsViewModel settingsViewModel,
        LocalDatabase localDatabase)
    {
        InitializeComponent();
        this.settingsViewModel = settingsViewModel;
        this.localDatabase = localDatabase;
        BindingContext = settingsViewModel;
        AddTransactionOverlay.TransactionSaved += OnTransactionSaved;
        ExpensesView.EditTransactionRequested += OnTransactionEditRequested;
        ExpensesView.DeleteTransactionRequested += OnTransactionDeleteRequested;
        settingsViewModel.PropertyChanged += OnSettingsPropertyChanged;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e)
    {
        await settingsViewModel.InitializeAsync();
        await RefreshTransactionViewsAsync();
    }

    private async void OnTransactionSaved(object? sender, EventArgs e) =>
        await RefreshTransactionViewsAsync();

    private async void OnSettingsPropertyChanged(
        object? sender,
        PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.SelectedCurrency))
        {
            await RefreshTransactionViewsAsync();
        }
    }

    private async void OnDashboardTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(0);

    private async void OnExpensesTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(1);

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(2);

    private async void OnAddTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await AddTransactionOverlay.OpenAsync(
            localDatabase,
            settingsViewModel.SelectedCurrency);
        await feedback;
    }

    private async void OnTransactionEditRequested(int transactionId) =>
        await OpenTransactionForEditAsync(transactionId);

    private async void OnTransactionDeleteRequested(int transactionId) =>
        await OpenDeleteConfirmationAsync(transactionId);

    private async void OnDashboardEditTransactionInvoked(object? sender, EventArgs e)
    {
        if (sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            await OpenTransactionForEditAsync(item.Id);
        }
    }

    private async void OnDashboardDeleteTransactionInvoked(object? sender, EventArgs e)
    {
        if (sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            await OpenDeleteConfirmationAsync(item.Id);
        }
    }

    private async Task OpenTransactionForEditAsync(int transactionId)
    {
        var transaction = await localDatabase.GetTransactionAsync(transactionId);
        if (transaction is null)
        {
            await RefreshTransactionViewsAsync();
            return;
        }

        await AddTransactionOverlay.OpenAsync(
            localDatabase,
            settingsViewModel.SelectedCurrency,
            transaction);
    }

    private async Task OpenDeleteConfirmationAsync(int transactionId)
    {
        if (DeleteConfirmationOverlay.IsVisible || isDeleteConfirmationAnimating)
        {
            return;
        }

        var transaction = await localDatabase.GetTransactionAsync(transactionId);
        if (transaction is null)
        {
            await RefreshTransactionViewsAsync();
            return;
        }

        pendingDeleteTransaction = transaction;
        DeleteDescriptionLabel.Text =
            $"“{transaction.Description}” will be permanently removed. This cannot be undone.";
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
        if (pendingDeleteTransaction is null || isDeletingTransaction)
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
            await localDatabase.DeleteTransactionAsync(pendingDeleteTransaction.Id);
            await RefreshTransactionViewsAsync();
            isDeletingTransaction = false;
            await CloseDeleteConfirmationAsync();
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

    private void OnNavigationTabsSizeChanged(object? sender, EventArgs e)
    {
        NavigationSelectionPill.TranslationX = GetSelectionPillOffset(selectedSectionIndex);
    }

    private async Task NavigateToSectionAsync(int selectedIndex)
    {
        if (selectedIndex <= 1)
        {
            await RefreshTransactionViewsAsync();
        }

        var pages = new VisualElement[] { DashboardView, ExpensesView, SettingsView };
        var icons = new VisualElement[] { DashboardIcon, ExpensesIcon, SettingsIcon };
        var selectedIcon = icons[selectedIndex];

        if (selectedIndex == selectedSectionIndex)
        {
            await AnimateNavigationIconAsync(selectedIcon);
            return;
        }

        var transitionVersion = ++navigationTransitionVersion;
        var previousIndex = selectedSectionIndex;
        var previousPage = pages[previousIndex];
        var selectedPage = pages[selectedIndex];

        foreach (var page in pages)
        {
            page.CancelAnimations();
            if (page != previousPage && page != selectedPage)
            {
                page.IsVisible = false;
                page.Opacity = 1;
                page.TranslationY = 0;
            }
        }

        selectedSectionIndex = selectedIndex;
        selectedPage.IsVisible = true;
        selectedPage.Opacity = 0;
        selectedPage.TranslationY = 8;
        UpdateNavigationStyles(selectedIndex);

        NavigationSelectionPill.CancelAnimations();
        var pillOffset = GetSelectionPillOffset(selectedIndex);

        await Task.WhenAll(
            NavigationSelectionPill.TranslateToAsync(
                pillOffset,
                0,
                220,
                Easing.CubicOut),
            previousPage.FadeToAsync(0, 120, Easing.CubicIn),
            selectedPage.FadeToAsync(1, 180, Easing.CubicOut),
            selectedPage.TranslateToAsync(0, 0, 190, Easing.CubicOut),
            AnimateNavigationIconAsync(selectedIcon));

        if (transitionVersion != navigationTransitionVersion)
        {
            return;
        }

        previousPage.IsVisible = false;
        previousPage.Opacity = 1;
        previousPage.TranslationY = 0;
        selectedPage.Opacity = 1;
        selectedPage.TranslationY = 0;
    }

    private async Task RefreshTransactionViewsAsync()
    {
        await transactionRefreshLock.WaitAsync();
        try
        {
            var records = await localDatabase.GetTransactionsAsync();
            var currency = settingsViewModel.SelectedCurrency;

            ExpensesView.Refresh(records, currency);
            RefreshDashboard(records, currency);
        }
        finally
        {
            transactionRefreshLock.Release();
        }
    }

    private void RefreshDashboard(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency)
    {
        DashboardMonthlySummary.Refresh(records, selectedCurrency);

        var recentRecords = records.Take(5).ToList();
        var recentActivity = recentRecords
            .Select((record, index) => TransactionActivityItem.FromRecord(
                record,
                index < recentRecords.Count - 1))
            .ToList();
        BindableLayout.SetItemsSource(DashboardActivityLayout, recentActivity);
        DashboardActivityCard.IsVisible = recentActivity.Count > 0;
        DashboardEmptyActivityState.IsVisible = recentActivity.Count == 0;
    }

    private double GetSelectionPillOffset(int selectedIndex)
    {
        var tabs = new[] { DashboardTab, ExpensesTab, SettingsTab };
        return tabs[selectedIndex].X - ExpensesTab.X;
    }

    private static async Task AnimateNavigationIconAsync(VisualElement icon)
    {
        icon.CancelAnimations();
        icon.Scale = 1;
        icon.TranslationY = 0;

        await Task.WhenAll(
            icon.ScaleToAsync(1.1, 85, Easing.CubicOut),
            icon.TranslateToAsync(0, -2, 85, Easing.CubicOut));
        await Task.WhenAll(
            icon.ScaleToAsync(1, 135, Easing.SpringOut),
            icon.TranslateToAsync(0, 0, 135, Easing.CubicOut));
    }

    private void UpdateNavigationStyles(int selectedIndex)
    {
        var tabs = new[] { DashboardTab, ExpensesTab, SettingsTab };

        var icons = new[] { DashboardIcon, ExpensesIcon };
        var labels = new[] { DashboardLabel, ExpensesLabel, SettingsLabel };
        var resources = Application.Current!.Resources;

        for (var index = 0; index < tabs.Length; index++)
        {
            var isSelected = index == selectedIndex;
            tabs[index].Style = (Style)resources["NavTab"];
            labels[index].Style = (Style)resources[
                isSelected ? "SelectedNavLabel" : "NavLabel"];

            if (index < icons.Length)
            {
                icons[index].Style = (Style)resources[
                    isSelected ? "SelectedNavLabel" : "NavLabel"];
                icons[index].FontSize = index == 0 ? 21 : 20;
            }
        }

        SettingsIcon.Style = (Style)resources[
            selectedIndex == 2 ? "SelectedNavIcon" : "NavIcon"];
    }
}
