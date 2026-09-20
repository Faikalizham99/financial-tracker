using System.ComponentModel;
using FinancialTracker.Controls;
using FinancialTracker.Helpers;
using FinancialTracker.ViewModels;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views;

public partial class SettingsView : ContentView
{
    private readonly ColorWheelDrawable accentColorWheelDrawable = new();
    private readonly (string Code, Border Card, Label Check)[] currencyOptionControls;
    private readonly (string Theme, Border Card)[] themeOptionControls;
    private readonly (string Hex, Border Card, Label Check)[] accentOptionControls;
    private SettingsViewModel? subscribedViewModel;
    private bool isSynchronizingColorWheel;
    private bool isCurrencySelectorExpanded;
    private bool isCurrencySelectorAnimating;
    private bool isDataDrawerOpen;
    private bool isDataDrawerAnimating;
    private bool isCategoryTypeAnimating;
    private string selectedCategoryType = "Expense";

    public SettingsView()
    {
        InitializeComponent();
        currencyOptionControls =
        [
            ("MYR", MyrOption, MyrCheck),
            ("USD", UsdOption, UsdCheck),
            ("SGD", SgdOption, SgdCheck),
            ("KRW", KrwOption, KrwCheck)
        ];
        themeOptionControls =
        [
            ("System", SystemThemeOption),
            ("Light", LightThemeOption),
            ("Dark", DarkThemeOption)
        ];
        accentOptionControls =
        [
            ("#5044E4", PurpleAccentOption, PurpleAccentCheck),
            ("#1477D4", OceanAccentOption, OceanAccentCheck),
            ("#0F766E", TealAccentOption, TealAccentCheck),
            ("#C2416C", RoseAccentOption, RoseAccentCheck),
            ("#C65D16", OrangeAccentOption, OrangeAccentCheck),
            ("#475569", SlateAccentOption, SlateAccentCheck)
        ];
        AccentColorWheel.Drawable = accentColorWheelDrawable;
        SynchronizeColorWheel(AppearanceValueNormalizer.DefaultAccentColor);
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

    private async void OnCategoriesTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        ConfigureDataDrawer(showCategories: true);
        await OpenDataDrawerAsync();
        await feedback;
    }

    private async void OnPaymentMethodsTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        ConfigureDataDrawer(showCategories: false);
        await OpenDataDrawerAsync();
        await feedback;
    }

    private async void OnDataDrawerCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseDataDrawerAsync();
        await feedback;
    }

    private async void OnCategoryTypeTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        if (e.Parameter is string categoryType)
        {
            await SelectCategoryTypeAsync(categoryType);
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

        foreach (var (code, card, check) in currencyOptionControls)
        {
            var isSelected = code == viewModel.SelectedCurrency.Code;
            card.Style = GetStyle(isSelected);
            check.IsVisible = isSelected;
        }

        foreach (var (theme, card) in themeOptionControls)
        {
            card.Style = GetStyle(theme == viewModel.SelectedTheme);
            card.Padding = new Thickness(10, 9);
        }

        foreach (var (hex, card, check) in accentOptionControls)
        {
            var isSelected = hex.Equals(
                viewModel.SelectedAccentColorHex,
                StringComparison.OrdinalIgnoreCase);
            card.Style = GetStyle(isSelected);
            card.WidthRequest = 44;
            card.HeightRequest = 44;
            card.Padding = 6;
            check.IsVisible = isSelected;
        }
    }

    private static Style GetStyle(bool isSelected) =>
        (Style)Application.Current!.Resources[
            isSelected ? "SelectedOptionCard" : "OptionCard"];

    private void ConfigureDataDrawer(bool showCategories)
    {
        DataDrawerTitle.Text = showCategories ? "Categories" : "Payment methods";
        CategoriesDrawerContent.IsVisible = showCategories;
        PaymentMethodsDrawerContent.IsVisible = !showCategories;

        if (showCategories)
        {
            selectedCategoryType = "Expense";
            ExpenseCategoriesPanel.IsVisible = true;
            IncomeCategoriesPanel.IsVisible = false;
            UpdateCategoryTypeVisuals();
        }
    }

    private async Task OpenDataDrawerAsync()
    {
        if (isDataDrawerOpen || isDataDrawerAnimating)
        {
            return;
        }

        isDataDrawerAnimating = true;
        isDataDrawerOpen = true;
        ZIndex = 30;
        DataDrawerOverlay.IsVisible = true;
        DataDrawerOverlay.Opacity = 0;
        DataDrawerCard.Opacity = 0;
        DataDrawerCard.TranslationX = 44;

        try
        {
            await Task.WhenAll(
                DataDrawerOverlay.FadeToAsync(1, 190, Easing.CubicOut),
                DataDrawerCard.FadeToAsync(1, 210, Easing.CubicOut),
                DataDrawerCard.TranslateToAsync(0, 0, 285, Easing.CubicOut));
        }
        finally
        {
            isDataDrawerAnimating = false;
        }
    }

    private async Task CloseDataDrawerAsync()
    {
        if (!isDataDrawerOpen || isDataDrawerAnimating)
        {
            return;
        }

        isDataDrawerAnimating = true;
        isDataDrawerOpen = false;

        try
        {
            await Task.WhenAll(
                DataDrawerOverlay.FadeToAsync(0, 170, Easing.CubicIn),
                DataDrawerCard.FadeToAsync(0, 150, Easing.CubicIn),
                DataDrawerCard.TranslateToAsync(44, 0, 210, Easing.CubicIn));
        }
        finally
        {
            DataDrawerOverlay.IsVisible = false;
            DataDrawerOverlay.Opacity = 0;
            DataDrawerCard.Opacity = 1;
            DataDrawerCard.TranslationX = 0;
            ZIndex = 0;
            isDataDrawerAnimating = false;
        }
    }

    private async Task SelectCategoryTypeAsync(string categoryType)
    {
        if (isCategoryTypeAnimating ||
            categoryType == selectedCategoryType ||
            categoryType is not ("Expense" or "Income"))
        {
            return;
        }

        isCategoryTypeAnimating = true;
        var outgoingPanel = selectedCategoryType == "Expense"
            ? ExpenseCategoriesPanel
            : IncomeCategoriesPanel;
        var incomingPanel = categoryType == "Expense"
            ? ExpenseCategoriesPanel
            : IncomeCategoriesPanel;

        selectedCategoryType = categoryType;
        UpdateCategoryTypeVisuals();

        try
        {
            outgoingPanel.CancelAnimations();
            incomingPanel.CancelAnimations();
            await outgoingPanel.FadeToAsync(0, 90, Easing.CubicIn);
            outgoingPanel.IsVisible = false;
            outgoingPanel.Opacity = 1;

            incomingPanel.IsVisible = true;
            incomingPanel.Opacity = 0;
            incomingPanel.TranslationY = 6;
            await Task.WhenAll(
                incomingPanel.FadeToAsync(1, 150, Easing.CubicOut),
                incomingPanel.TranslateToAsync(0, 0, 170, Easing.CubicOut));
        }
        finally
        {
            incomingPanel.Opacity = 1;
            incomingPanel.TranslationY = 0;
            isCategoryTypeAnimating = false;
        }
    }

    private void UpdateCategoryTypeVisuals()
    {
        var expenseSelected = selectedCategoryType == "Expense";
        ExpenseCategoryTab.Style = GetResourceStyle(
            expenseSelected ? "SelectedCategorySegmentTab" : "CategorySegmentTab");
        IncomeCategoryTab.Style = GetResourceStyle(
            expenseSelected ? "CategorySegmentTab" : "SelectedCategorySegmentTab");
        ExpenseCategoryLabel.Style = GetResourceStyle(
            expenseSelected ? "SelectedCategorySegmentLabel" : "CategorySegmentLabel");
        IncomeCategoryLabel.Style = GetResourceStyle(
            expenseSelected ? "CategorySegmentLabel" : "SelectedCategorySegmentLabel");
    }

    private static Style GetResourceStyle(string key) =>
        (Style)Application.Current!.Resources[key];

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
