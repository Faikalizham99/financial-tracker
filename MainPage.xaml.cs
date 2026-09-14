namespace FinancialTracker;

using System.ComponentModel;
using System.Globalization;
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
        var today = DateTime.Today;
        var currentMonthRecords = records
            .Where(record =>
                record.CurrencyCode.Equals(selectedCurrency.Code, StringComparison.OrdinalIgnoreCase) &&
                record.TransactionDate.Year == today.Year &&
                record.TransactionDate.Month == today.Month)
            .ToList();
        var incomeMinor = currentMonthRecords
            .Where(record => record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase))
            .Sum(record => record.AmountMinor);
        var spentMinor = currentMonthRecords
            .Where(record => record.Type.Equals("Expense", StringComparison.OrdinalIgnoreCase))
            .Sum(record => record.AmountMinor);

        DashboardAvailableAmountLabel.Text = FormatMoney(
            selectedCurrency,
            incomeMinor - spentMinor);
        DashboardIncomeAmountLabel.Text = FormatMoney(selectedCurrency, incomeMinor);
        DashboardSpentAmountLabel.Text = FormatMoney(selectedCurrency, spentMinor);
        DashboardMonthLabel.Text = today.ToString("MMM", CultureInfo.CurrentCulture).ToUpperInvariant();

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

    private static string FormatMoney(CurrencyOption currency, long amountMinor)
    {
        var sign = amountMinor < 0 ? "− " : string.Empty;
        var amount = Math.Abs(amountMinor) / 100m;
        return $"{sign}{currency.Symbol} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
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
