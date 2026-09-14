using System.ComponentModel;
using FinancialTracker.Controls;
using FinancialTracker.Helpers;
using FinancialTracker.ViewModels;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views;

public partial class SettingsView : ContentView
{
    private readonly ColorWheelDrawable accentColorWheelDrawable = new();
    private SettingsViewModel? subscribedViewModel;
    private bool isSynchronizingColorWheel;
    private bool isCurrencySelectorExpanded;
    private bool isCurrencySelectorAnimating;

    public SettingsView()
    {
        InitializeComponent();
        AccentColorWheel.Drawable = accentColorWheelDrawable;
        SynchronizeColorWheel("#5044E4");
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
            SynchronizeColorWheel(subscribedViewModel.SelectedAccentColorHex);
        }
    }

    private async void OnCurrencyTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (BindingContext is SettingsViewModel viewModel &&
            e.Parameter is string currencyCode)
        {
            await viewModel.SelectCurrencyAsync(currencyCode);
            UpdateSelectionVisuals();
            await SetCurrencySelectorExpandedAsync(false);
        }

        await feedback;
    }

    private async void OnCurrencySelectorTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await SetCurrencySelectorExpandedAsync(!isCurrencySelectorExpanded);
        await feedback;
    }

    private async void OnThemeTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (BindingContext is SettingsViewModel viewModel &&
            e.Parameter is string theme)
        {
            await viewModel.SelectThemeAsync(theme);
            UpdateSelectionVisuals();
        }

        await feedback;
    }

    private async void OnAccentTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (BindingContext is SettingsViewModel viewModel &&
            e.Parameter is string accentColorHex)
        {
            await viewModel.SelectAccentColorAsync(accentColorHex);
            UpdateSelectionVisuals();
            SynchronizeColorWheel(viewModel.SelectedAccentColorHex);
        }

        await feedback;
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

    private async void OnButtonPressed(object? sender, EventArgs e) =>
        await InteractionAnimations.PressAsync(sender);

    private async void OnButtonReleased(object? sender, EventArgs e) =>
        await InteractionAnimations.ReleaseAsync(sender);

    private void OnAccentWheelInteraction(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0 ||
            !accentColorWheelDrawable.Select(
                e.Touches[0],
                (float)AccentColorWheel.Width,
                (float)AccentColorWheel.Height))
        {
            return;
        }

        UpdateWheelCandidate();
    }

    private void OnAccentBrightnessChanged(object? sender, ValueChangedEventArgs e)
    {
        if (isSynchronizingColorWheel ||
            AccentColorWheel is null ||
            WheelColorHexLabel is null ||
            WheelColorPreview is null)
        {
            return;
        }

        accentColorWheelDrawable.Brightness = (float)e.NewValue;
        UpdateWheelCandidate();
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
        if (sender is SettingsViewModel colorViewModel &&
            !isSynchronizingColorWheel &&
            (string.IsNullOrEmpty(e.PropertyName) ||
             e.PropertyName is nameof(SettingsViewModel.CustomAccentColorHex) or
                 nameof(SettingsViewModel.SelectedAccentColorHex)))
        {
            var colorHex = e.PropertyName == nameof(SettingsViewModel.CustomAccentColorHex)
                ? colorViewModel.CustomAccentColorHex
                : colorViewModel.SelectedAccentColorHex;
            SynchronizeColorWheel(colorHex);
        }

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

    private async Task SetCurrencySelectorExpandedAsync(bool isExpanded)
    {
        if (isCurrencySelectorAnimating || isCurrencySelectorExpanded == isExpanded)
        {
            return;
        }

        isCurrencySelectorAnimating = true;
        isCurrencySelectorExpanded = isExpanded;

        try
        {
            if (isExpanded)
            {
                CurrencyOptionsPanel.IsVisible = true;
                CurrencyOptionsPanel.Opacity = 0;
                CurrencyOptionsPanel.TranslationY = -8;
                CurrencyOptionsPanel.HeightRequest = -1;

                var widthConstraint = CurrencyOptionsPanel.Width > 0
                    ? CurrencyOptionsPanel.Width
                    : Math.Max(Width - 80, 320);
                var targetHeight = CurrencyOptionsPanel
                    .Measure(widthConstraint, double.PositiveInfinity)
                    .Height;

                CurrencyOptionsPanel.HeightRequest = 0;
                await Task.WhenAll(
                    AnimateHeightAsync(CurrencyOptionsPanel, 0, targetHeight, 240, Easing.CubicOut),
                    CurrencyOptionsPanel.FadeToAsync(1, 190, Easing.CubicOut),
                    CurrencyOptionsPanel.TranslateToAsync(0, 0, 220, Easing.CubicOut),
                    CurrencyChevron.RotateToAsync(180, 220, Easing.CubicOut));

                CurrencyOptionsPanel.HeightRequest = -1;
            }
            else
            {
                var startHeight = CurrencyOptionsPanel.Height > 0
                    ? CurrencyOptionsPanel.Height
                    : CurrencyOptionsPanel.DesiredSize.Height;
                CurrencyOptionsPanel.HeightRequest = startHeight;

                await Task.WhenAll(
                    AnimateHeightAsync(CurrencyOptionsPanel, startHeight, 0, 190, Easing.CubicIn),
                    CurrencyOptionsPanel.FadeToAsync(0, 140, Easing.CubicIn),
                    CurrencyOptionsPanel.TranslateToAsync(0, -6, 170, Easing.CubicIn),
                    CurrencyChevron.RotateToAsync(0, 190, Easing.CubicIn));

                CurrencyOptionsPanel.IsVisible = false;
                CurrencyOptionsPanel.HeightRequest = -1;
                CurrencyOptionsPanel.Opacity = 1;
                CurrencyOptionsPanel.TranslationY = 0;
            }
        }
        finally
        {
            isCurrencySelectorAnimating = false;
        }
    }

    private static Task AnimateHeightAsync(
        VisualElement element,
        double startHeight,
        double endHeight,
        uint duration,
        Easing easing)
    {
        var completion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var animation = new Animation(
            value => element.HeightRequest = value,
            startHeight,
            endHeight,
            easing);

        animation.Commit(
            element,
            "CurrencySelectorHeight",
            length: duration,
            finished: (_, _) => completion.TrySetResult(true));

        return completion.Task;
    }

    private void SynchronizeColorWheel(string colorHex)
    {
        if (!accentColorWheelDrawable.SetColor(colorHex))
        {
            return;
        }

        isSynchronizingColorWheel = true;
        try
        {
            AccentBrightnessSlider.Value = accentColorWheelDrawable.Brightness;
            WheelColorHexLabel.Text = accentColorWheelDrawable.SelectedHex;
            WheelColorPreview.BackgroundColor = Color.FromArgb(accentColorWheelDrawable.SelectedHex);
            AccentColorWheel.Invalidate();
        }
        finally
        {
            isSynchronizingColorWheel = false;
        }
    }

    private void UpdateWheelCandidate()
    {
        var colorHex = accentColorWheelDrawable.SelectedHex;
        isSynchronizingColorWheel = true;
        try
        {
            if (BindingContext is SettingsViewModel viewModel)
            {
                viewModel.CustomAccentColorHex = colorHex;
            }
        }
        finally
        {
            isSynchronizingColorWheel = false;
        }

        WheelColorHexLabel.Text = colorHex;
        WheelColorPreview.BackgroundColor = Color.FromArgb(colorHex);
        AccentColorWheel.Invalidate();
    }
}
