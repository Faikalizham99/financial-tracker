namespace FinancialTracker;

using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
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
    private const string RecentSearchesPreferenceKey = "transaction_recent_searches";
    private readonly List<string> recentTransactionSearches = [];
    private IReadOnlyList<TransactionRecord> transactionRecords = [];
    private int selectedSectionIndex = 1;
    private int navigationTransitionVersion;
    private TransactionRecord? pendingDeleteTransaction;
    private bool isDeleteConfirmationAnimating;
    private bool isDeletingTransaction;
    private bool isTransactionSearchAnimating;

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
        ExpensesView.SearchRequested += OnTransactionSearchRequested;
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

    private async void OnTransactionSearchRequested() =>
        await OpenTransactionSearchAsync();

    private async Task OpenTransactionSearchAsync()
    {
        if (TransactionSearchOverlay.IsVisible || isTransactionSearchAnimating)
        {
            return;
        }

        LoadRecentTransactionSearches();
        TransactionSearchEntry.Text = string.Empty;
        UpdateTransactionSearchResults();
        TransactionSearchOverlay.IsVisible = true;
        TransactionSearchOverlay.Opacity = 0;
        isTransactionSearchAnimating = true;

        try
        {
            await TransactionSearchOverlay.FadeToAsync(1, 180, Easing.CubicOut);
        }
        finally
        {
            isTransactionSearchAnimating = false;
        }

        await Task.Delay(80);
        TransactionSearchEntry.Focus();
    }

    private async Task CloseTransactionSearchAsync()
    {
        if (!TransactionSearchOverlay.IsVisible || isTransactionSearchAnimating)
        {
            return;
        }

        isTransactionSearchAnimating = true;
        TransactionSearchEntry.Unfocus();
        try
        {
            await TransactionSearchOverlay.FadeToAsync(0, 140, Easing.CubicIn);
        }
        finally
        {
            TransactionSearchOverlay.IsVisible = false;
            TransactionSearchOverlay.Opacity = 0;
            isTransactionSearchAnimating = false;
        }
    }

    private async void OnTransactionSearchCancelTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseTransactionSearchAsync();
        await feedback;
    }

    private void OnTransactionSearchTextChanged(object? sender, TextChangedEventArgs e) =>
        UpdateTransactionSearchResults();

    private void OnTransactionSearchCompleted(object? sender, EventArgs e)
    {
        var query = TransactionSearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            AddRecentTransactionSearch(query);
        }
    }

    private void OnTransactionSearchClearTapped(object? sender, TappedEventArgs e)
    {
        TransactionSearchEntry.Text = string.Empty;
        TransactionSearchEntry.Focus();
    }

    private void OnRecentSearchTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string query)
        {
            return;
        }

        TransactionSearchEntry.Text = query;
        TransactionSearchEntry.CursorPosition = query.Length;
        TransactionSearchEntry.Focus();
        AddRecentTransactionSearch(query);
    }

    private void OnClearRecentSearchesTapped(object? sender, TappedEventArgs e)
    {
        recentTransactionSearches.Clear();
        SaveRecentTransactionSearches();
        UpdateRecentSearchesView();
    }

    private async void OnTransactionSearchResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not TransactionActivityItem transaction)
        {
            return;
        }

        var query = TransactionSearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            AddRecentTransactionSearch(query);
        }

        await CloseTransactionSearchAsync();
        if (selectedSectionIndex != 1)
        {
            await NavigateToSectionAsync(1);
        }

        await ExpensesView.FocusTransactionAsync(transaction.Id);
    }

    private void UpdateTransactionSearchResults()
    {
        var query = TransactionSearchEntry.Text?.Trim() ?? string.Empty;
        TransactionSearchClearButton.IsVisible = query.Length > 0;
        RecentSearchesPanel.IsVisible = query.Length == 0;
        SearchResultsPanel.IsVisible = query.Length > 0;

        if (query.Length == 0)
        {
            BindableLayout.SetItemsSource(SearchResultsLayout, null);
            SearchResultsCard.IsVisible = false;
            SearchNoResultsLabel.IsVisible = false;
            UpdateRecentSearchesView();
            return;
        }

        var matchingRecords = transactionRecords
            .Where(record => TransactionMatchesSearch(record, query))
            .ToList();
        var results = matchingRecords
            .Select((record, index) => TransactionActivityItem.FromRecord(
                record,
                index < matchingRecords.Count - 1))
            .ToList();

        BindableLayout.SetItemsSource(SearchResultsLayout, results);
        SearchResultsCountLabel.Text = $"TRANSACTIONS · {results.Count.ToString(CultureInfo.InvariantCulture)}";
        SearchResultsCard.IsVisible = results.Count > 0;
        SearchNoResultsLabel.IsVisible = results.Count == 0;
    }

    private static bool TransactionMatchesSearch(TransactionRecord record, string query)
    {
        var searchableText = string.Join(
            ' ',
            record.Description,
            record.Category,
            record.PaymentMethod,
            record.Type,
            record.TransactionDate.ToString("dddd d MMMM yyyy", CultureInfo.CurrentCulture),
            record.TransactionDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture),
            (record.AmountMinor / 100m).ToString("N2", CultureInfo.InvariantCulture));
        return searchableText.Contains(query, StringComparison.CurrentCultureIgnoreCase);
    }

    private void LoadRecentTransactionSearches()
    {
        recentTransactionSearches.Clear();
        try
        {
            var serializedSearches = Preferences.Default.Get(
                RecentSearchesPreferenceKey,
                string.Empty);
            var savedSearches = JsonSerializer.Deserialize<List<string>>(serializedSearches);
            if (savedSearches is not null)
            {
                recentTransactionSearches.AddRange(savedSearches
                    .Where(search => !string.IsNullOrWhiteSpace(search))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .Take(8));
            }
        }
        catch
        {
            recentTransactionSearches.Clear();
        }

        UpdateRecentSearchesView();
    }

    private void AddRecentTransactionSearch(string query)
    {
        recentTransactionSearches.RemoveAll(search =>
            search.Equals(query, StringComparison.CurrentCultureIgnoreCase));
        recentTransactionSearches.Insert(0, query);
        if (recentTransactionSearches.Count > 8)
        {
            recentTransactionSearches.RemoveRange(
                8,
                recentTransactionSearches.Count - 8);
        }

        SaveRecentTransactionSearches();
        UpdateRecentSearchesView();
    }

    private void SaveRecentTransactionSearches() =>
        Preferences.Default.Set(
            RecentSearchesPreferenceKey,
            JsonSerializer.Serialize(recentTransactionSearches));

    private void UpdateRecentSearchesView()
    {
        BindableLayout.SetItemsSource(
            RecentSearchesLayout,
            recentTransactionSearches.ToList());
        RecentSearchesCard.IsVisible = recentTransactionSearches.Count > 0;
        NoRecentSearchesLabel.IsVisible = recentTransactionSearches.Count == 0;
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

    private void OnDashboardTransactionRowHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        if (sender is not Grid row ||
            row.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement nativeRow)
        {
            return;
        }

        var editItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Edit" };
        editItem.Click += async (_, _) =>
        {
            if (row.BindingContext is TransactionActivityItem item)
            {
                await OpenTransactionForEditAsync(item.Id);
            }
        };

        var deleteItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += async (_, _) =>
        {
            if (row.BindingContext is TransactionActivityItem item)
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
        var transactionActivity = TransactionActivityItem.FromRecord(transaction);
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
            transactionRecords = records;

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

        var icons = new[] { DashboardIcon, ExpensesIcon, SettingsIcon };
        var labels = new[] { DashboardLabel, ExpensesLabel, SettingsLabel };
        var resources = Application.Current!.Resources;

        for (var index = 0; index < tabs.Length; index++)
        {
            var isSelected = index == selectedIndex;
            tabs[index].Style = (Style)resources["NavTab"];
            labels[index].Style = (Style)resources[
                isSelected ? "SelectedNavLabel" : "NavLabel"];

            icons[index].Style = (Style)resources[
                isSelected ? "SelectedNavIcon" : "NavIcon"];
        }
    }
}
