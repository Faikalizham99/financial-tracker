using FinancialTracker.Helpers;

namespace FinancialTracker;

public partial class MainPage
{
    private async void OnExpensesTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(1);

    private async void OnSettingsTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(3);

    private async void OnAssetsTapped(object? sender, TappedEventArgs e)
        => await NavigateToSectionAsync(2);

    private async void OnAddTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await AddTransactionOverlay.OpenAsync(
            localDatabase,
            settingsViewModel.SelectedCurrency,
            transactionDataStore.DescriptionHistoryRecords);
        await feedback;
    }

    private void OnNavigationTabsSizeChanged(object? sender, EventArgs e)
    {
        NavigationSelectionPill.TranslationX = GetSelectionPillOffset(selectedSectionIndex);
    }

    private async Task NavigateToSectionAsync(int selectedIndex)
    {
        if (selectedIndex == 0)
        {
            UpdateDashboardGreeting();
        }

        var selectedIcon = navigationIcons[selectedIndex];

        if (selectedIndex == selectedSectionIndex)
        {
            await AnimateNavigationIconAsync(selectedIcon);
            return;
        }

        var transitionVersion = ++navigationTransitionVersion;
        var previousIndex = selectedSectionIndex;
        var previousPage = navigationPages[previousIndex];
        var selectedPage = navigationPages[selectedIndex];

        foreach (var page in navigationPages)
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
        previousPage.IsVisible = false;
        previousPage.Opacity = 1;
        previousPage.TranslationY = 0;
        selectedPage.IsVisible = true;
        selectedPage.Opacity = 1;
        selectedPage.TranslationY = 0;
        if (selectedIndex == 2)
        {
            await AssetsView.EnsureLoadedAsync();
        }
        UpdateNavigationStyles(selectedIndex);
        UpdateLoadingSkeletonForSelectedSection();

        NavigationSelectionPill.CancelAnimations();
        var pillOffset = GetSelectionPillOffset(selectedIndex);

        await Task.WhenAll(
            NavigationSelectionPill.TranslateToAsync(
                pillOffset,
                0,
                190,
                Easing.CubicOut),
            AnimateNavigationIconAsync(selectedIcon));

        if (transitionVersion != navigationTransitionVersion)
        {
            return;
        }

        selectedPage.Opacity = 1;
        selectedPage.TranslationY = 0;
    }

    private double GetSelectionPillOffset(int selectedIndex)
    {
        return navigationTabs[selectedIndex].X - ExpensesTab.X;
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
        var resources = Application.Current!.Resources;

        for (var index = 0; index < navigationTabs.Length; index++)
        {
            var isSelected = index == selectedIndex;
            navigationTabs[index].Style = (Style)resources["NavTab"];
            navigationLabels[index].Style = (Style)resources[
                isSelected ? "SelectedNavLabel" : "NavLabel"];

            navigationIcons[index].Style = (Style)resources[
                isSelected ? "SelectedNavIcon" : "NavIcon"];
        }
    }
}
