namespace FinancialTracker;

using FinancialTracker.ViewModels;
using FinancialTracker.Data;
using FinancialTracker.Services;
using FinancialTracker.Helpers;

public partial class MainPage : ContentPage
{
    private readonly SettingsViewModel settingsViewModel;
    private int selectedSectionIndex = 1;
    private int navigationTransitionVersion;

    public MainPage()
        : this(new SettingsViewModel(new SettingsService(new LocalDatabase())))
    {
    }

    public MainPage(SettingsViewModel settingsViewModel)
    {
        InitializeComponent();
        this.settingsViewModel = settingsViewModel;
        BindingContext = settingsViewModel;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, EventArgs e) =>
        await settingsViewModel.InitializeAsync();

    private async void OnDashboardTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(0);

    private async void OnExpensesTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(1);

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(2);

    private async void OnAddTapped(object? sender, TappedEventArgs e) =>
        await InteractionAnimations.PulseAsync(sender);

    private void OnNavigationTabsSizeChanged(object? sender, EventArgs e)
    {
        NavigationSelectionPill.TranslationX = GetSelectionPillOffset(selectedSectionIndex);
    }

    private async Task NavigateToSectionAsync(int selectedIndex)
    {
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
