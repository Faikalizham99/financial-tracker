using System.ComponentModel;
using FinancialTracker.ViewModels;

namespace FinancialTracker.Views;

public partial class SettingsView : ContentView
{
    private SettingsViewModel? subscribedViewModel;

    public SettingsView()
    {
        InitializeComponent();
    }

    protected override void OnBindingContextChanged()
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        base.OnBindingContextChanged();
        subscribedViewModel = BindingContext as SettingsViewModel;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            UpdateSelectionVisuals();
        }
    }

    private async void OnCurrencyTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SettingsViewModel viewModel &&
            e.Parameter is string currencyCode)
        {
            await viewModel.SelectCurrencyAsync(currencyCode);
            UpdateSelectionVisuals();
            SetCurrencySelectorExpanded(false);
        }
    }

    private void OnCurrencySelectorTapped(object? sender, TappedEventArgs e) =>
        SetCurrencySelectorExpanded(!CurrencyOptionsPanel.IsVisible);

    private async void OnThemeTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SettingsViewModel viewModel &&
            e.Parameter is string theme)
        {
            await viewModel.SelectThemeAsync(theme);
            UpdateSelectionVisuals();
        }
    }

    private async void OnAccentTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is SettingsViewModel viewModel &&
            e.Parameter is string accentColorHex)
        {
            await viewModel.SelectAccentColorAsync(accentColorHex);
            UpdateSelectionVisuals();
        }
    }

    private async void OnApplyCustomAccent(object? sender, EventArgs e)
    {
        if (BindingContext is SettingsViewModel viewModel && viewModel.CanApplyCustomAccent)
        {
            await viewModel.ApplyCustomAccentColorAsync();
            UpdateSelectionVisuals();
            AccentHexEntry.Unfocus();
        }
    }

    private async void OnSaveProfileClicked(object? sender, EventArgs e)
    {
        await SaveNameAsync();
        NameEntry.Unfocus();
    }

    private async Task SaveNameAsync()
    {
        if (BindingContext is SettingsViewModel viewModel)
        {
            await viewModel.SaveNameAsync();
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.IsNullOrEmpty(e.PropertyName) ||
            e.PropertyName is nameof(SettingsViewModel.SelectedCurrency) or
                nameof(SettingsViewModel.SelectedTheme) or
                nameof(SettingsViewModel.SelectedAccentColorHex))
        {
            UpdateSelectionVisuals();
        }
    }

    private void UpdateSelectionVisuals()
    {
        if (BindingContext is not SettingsViewModel viewModel)
        {
            return;
        }

        var currencyOptions = new Dictionary<string, (Border Card, Label Check)>
        {
            ["MYR"] = (MyrOption, MyrCheck),
            ["USD"] = (UsdOption, UsdCheck),
            ["SGD"] = (SgdOption, SgdCheck),
            ["KRW"] = (KrwOption, KrwCheck)
        };

        foreach (var (code, controls) in currencyOptions)
        {
            var isSelected = code == viewModel.SelectedCurrency.Code;
            controls.Card.Style = GetStyle(isSelected);
            controls.Check.IsVisible = isSelected;
        }

        var themeOptions = new Dictionary<string, Border>
        {
            ["System"] = SystemThemeOption,
            ["Light"] = LightThemeOption,
            ["Dark"] = DarkThemeOption
        };

        foreach (var (theme, card) in themeOptions)
        {
            card.Style = GetStyle(theme == viewModel.SelectedTheme);
            card.Padding = new Thickness(10, 9);
        }

        var accentOptions = new Dictionary<string, (Border Card, Label Check)>(StringComparer.OrdinalIgnoreCase)
        {
            ["#5044E4"] = (PurpleAccentOption, PurpleAccentCheck),
            ["#1477D4"] = (OceanAccentOption, OceanAccentCheck),
            ["#0F766E"] = (TealAccentOption, TealAccentCheck),
            ["#C2416C"] = (RoseAccentOption, RoseAccentCheck),
            ["#C65D16"] = (OrangeAccentOption, OrangeAccentCheck),
            ["#475569"] = (SlateAccentOption, SlateAccentCheck)
        };

        foreach (var (hex, controls) in accentOptions)
        {
            var isSelected = hex.Equals(
                viewModel.SelectedAccentColorHex,
                StringComparison.OrdinalIgnoreCase);
            controls.Card.Style = GetStyle(isSelected);
            controls.Card.WidthRequest = 44;
            controls.Card.HeightRequest = 44;
            controls.Card.Padding = 6;
            controls.Check.IsVisible = isSelected;
        }
    }

    private static Style GetStyle(bool isSelected) =>
        (Style)Application.Current!.Resources[
            isSelected ? "SelectedOptionCard" : "OptionCard"];

    private void SetCurrencySelectorExpanded(bool isExpanded)
    {
        CurrencyOptionsPanel.IsVisible = isExpanded;
        CurrencyChevron.Text = isExpanded ? "⌃" : "⌄";
    }
}
