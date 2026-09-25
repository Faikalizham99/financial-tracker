using System.Globalization;
using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.ViewModels;

namespace FinancialTracker.Views;

public partial class AddTransactionView : ContentView
{
    public Func<Task>? TransactionSaved { get; set; }
    public Func<DateTime, DateTime, DateTime, Task<DateTime?>>? DatePickerRequested { get; set; }
    public Func<Func<Task>, Task>? RunWithTransactionLoadingAsync { get; set; }

    private enum SelectorKind
    {
        PaymentMethod,
        Category
    }

    private readonly TransactionEntryViewModel viewModel = new();
    private LocalDatabase? database;
    private SelectorKind selectorKind;
    private bool isOpen;
    private bool isAnimating;
    private bool isSelectorOpen;
    private bool isSelectorAnimating;
    private bool isTypeAnimating;
    private bool isSaving;
    private bool isApplyingDescriptionSuggestion;
    private IReadOnlyList<SelectableTransactionOption> selectorOptions = [];

    public AddTransactionView()
    {
        InitializeComponent();
    }

    public async Task OpenAsync(
        LocalDatabase localDatabase,
        CurrencyOption selectedCurrency,
        IReadOnlyList<TransactionRecord> transactionHistory,
        TransactionRecord? transactionToEdit = null)
    {
        if (isOpen || isAnimating)
        {
            return;
        }

        database = localDatabase;
        viewModel.Initialize(selectedCurrency, transactionToEdit);
        var descriptionHistoryTask = viewModel.LoadDescriptionHistoryAsync(
            transactionHistory);
        ResetForm();
        isOpen = true;
        isAnimating = true;
        IsVisible = true;
        OverlayRoot.Opacity = 0;
        TransactionSheet.Opacity = 0;
        TransactionSheet.TranslationY = 28;
        TransactionSheet.Scale = 0.985;

        try
        {
            await Task.WhenAll(
                OverlayRoot.FadeToAsync(1, 170, Easing.CubicOut),
                TransactionSheet.FadeToAsync(1, 190, Easing.CubicOut),
                TransactionSheet.TranslateToAsync(0, 0, 260, Easing.CubicOut),
                TransactionSheet.ScaleToAsync(1, 260, Easing.CubicOut));
        }
        finally
        {
            isAnimating = false;
        }

        await descriptionHistoryTask;
        if (!viewModel.IsEditing)
        {
            UpdateDescriptionSuggestions(DescriptionEntry.Text);
        }
        else
        {
            HideDescriptionSuggestions();
        }
    }

    public async Task CloseAsync()
    {
        if (!isOpen || isAnimating)
        {
            return;
        }

        if (isSelectorOpen)
        {
            await CloseSelectorAsync();
        }

        isAnimating = true;
        HideDescriptionSuggestions();
        DescriptionEntry.Unfocus();

        try
        {
            await Task.WhenAll(
                OverlayRoot.FadeToAsync(0, 150, Easing.CubicIn),
                TransactionSheet.TranslateToAsync(0, 24, 190, Easing.CubicIn),
                TransactionSheet.ScaleToAsync(0.99, 190, Easing.CubicIn));
        }
        finally
        {
            IsVisible = false;
            OverlayRoot.Opacity = 0;
            TransactionSheet.Opacity = 1;
            TransactionSheet.TranslationY = 0;
            TransactionSheet.Scale = 1;
            isOpen = false;
            isAnimating = false;
        }
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseAsync();
        await feedback;
    }

    private async void OnTypeTapped(object? sender, TappedEventArgs e)
    {
        if (isTypeAnimating || isSaving)
        {
            return;
        }

        HideDescriptionSuggestions();
        var feedback = InteractionAnimations.PulseAsync(sender);
        await ToggleTransactionTypeAsync();
        await feedback;
    }

    private async void OnPaymentTapped(object? sender, TappedEventArgs e)
    {
        HideDescriptionSuggestions();
        var feedback = InteractionAnimations.PulseAsync(sender);
        await OpenSelectorAsync(
            SelectorKind.PaymentMethod,
            "Choose payment method",
            TransactionCatalog.PaymentMethods);
        await feedback;
    }

    private async void OnCategoryTapped(object? sender, TappedEventArgs e)
    {
        HideDescriptionSuggestions();
        var feedback = InteractionAnimations.PulseAsync(sender);
        var categories = viewModel.GetCategoriesForSelectedType();
        await OpenSelectorAsync(SelectorKind.Category, "Choose category", categories);
        await feedback;
    }

    private async void OnSelectorBackdropTapped(object? sender, TappedEventArgs e) =>
        await CloseSelectorAsync();

    private async void OnSelectorCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseSelectorAsync();
        await feedback;
    }

    private async void OnSelectorOptionTapped(object? sender, TappedEventArgs e)
    {
        if (isSelectorAnimating ||
            e.Parameter is not SelectableTransactionOption selectedOption)
        {
            return;
        }

        foreach (var option in selectorOptions)
        {
            option.IsSelected = ReferenceEquals(option, selectedOption);
        }

        switch (selectorKind)
        {
            case SelectorKind.PaymentMethod:
                viewModel.SelectPayment(selectedOption.Option);
                UpdatePaymentMethod();
                break;
            case SelectorKind.Category:
                viewModel.SelectCategory(selectedOption.Option);
                UpdateCategory();
                break;
        }

        await Task.Delay(110);
        await CloseSelectorAsync();
    }

    private async Task ToggleTransactionTypeAsync()
    {
        isTypeAnimating = true;
        TypeArrowIcon.CancelAnimations();
        TypeLabel.CancelAnimations();

        try
        {
            await Task.WhenAll(
                TypeArrowIcon.TranslateToAsync(0, -10, 105, Easing.CubicIn),
                TypeArrowIcon.FadeToAsync(0, 90, Easing.CubicIn),
                TypeLabel.FadeToAsync(0.35, 90, Easing.CubicIn));

            viewModel.ToggleTransactionType();
            UpdateTypeAndCategory();
            TypeArrowIcon.TranslationY = 10;

            await Task.WhenAll(
                TypeArrowIcon.TranslateToAsync(0, 0, 190, Easing.SpringOut),
                TypeArrowIcon.FadeToAsync(1, 145, Easing.CubicOut),
                TypeLabel.FadeToAsync(1, 145, Easing.CubicOut));
        }
        finally
        {
            TypeArrowIcon.Opacity = 1;
            TypeArrowIcon.TranslationY = 0;
            TypeLabel.Opacity = 1;
            isTypeAnimating = false;
        }
    }

    private async Task OpenSelectorAsync(
        SelectorKind kind,
        string title,
        IReadOnlyList<TransactionOption> options)
    {
        if (isSelectorOpen || isSelectorAnimating)
        {
            return;
        }

        selectorKind = kind;
        SelectorTitle.Text = title;
        var selectedKey = kind == SelectorKind.PaymentMethod
            ? viewModel.SelectedPayment.Key
            : viewModel.SelectedCategory.Key;
        selectorOptions = options
            .Select(option => new SelectableTransactionOption(
                option,
                option.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        SelectorCollection.ItemsSource = selectorOptions;
        isSelectorOpen = true;
        isSelectorAnimating = true;
        SelectorOverlay.IsVisible = true;
        UpdateSelectorCardBounds();
        SelectorOverlay.Opacity = 0;
        SelectorCard.TranslationY = 42;
        SelectorCard.Opacity = 0.85;

        try
        {
            await Task.WhenAll(
                SelectorOverlay.FadeToAsync(1, 170, Easing.CubicOut),
                SelectorCard.TranslateToAsync(0, 0, 260, Easing.CubicOut),
                SelectorCard.FadeToAsync(1, 210, Easing.CubicOut));
        }
        finally
        {
            isSelectorAnimating = false;
        }

        var selectedOption = selectorOptions.FirstOrDefault(option => option.IsSelected);
        if (selectedOption is not null)
        {
            await Task.Delay(40);
            SelectorCollection.ScrollTo(
                selectedOption,
                position: ScrollToPosition.Center,
                animate: false);
        }
    }

    private void OnSelectorOverlaySizeChanged(object? sender, EventArgs e) =>
        UpdateSelectorCardBounds();

    private void UpdateSelectorCardBounds()
    {
        var availableWidth = SelectorOverlay.Width > 0
            ? SelectorOverlay.Width
            : Width;
        var availableHeight = SelectorOverlay.Height > 0
            ? SelectorOverlay.Height
            : Height;

        if (availableWidth > 0)
        {
            SelectorCard.WidthRequest = Math.Min(540, Math.Max(0, availableWidth - 36));
        }

        if (availableHeight > 0)
        {
            SelectorCard.HeightRequest = Math.Min(620, Math.Max(0, availableHeight - 136));
        }
    }

    private async Task CloseSelectorAsync()
    {
        if (!isSelectorOpen || isSelectorAnimating)
        {
            return;
        }

        isSelectorAnimating = true;

        try
        {
            await Task.WhenAll(
                SelectorOverlay.FadeToAsync(0, 140, Easing.CubicIn),
                SelectorCard.TranslateToAsync(0, 34, 175, Easing.CubicIn));
        }
        finally
        {
            SelectorOverlay.IsVisible = false;
            SelectorOverlay.Opacity = 0;
            SelectorCard.Opacity = 1;
            SelectorCard.TranslationY = 0;
            isSelectorOpen = false;
            isSelectorAnimating = false;
        }
    }

    private async void OnKeypadTapped(object? sender, TappedEventArgs e)
    {
        HideDescriptionSuggestions();

        if (e.Parameter is not string key || isSaving)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);

        viewModel.ApplyKey(key);
        UpdateAmountAndSaveState();
        await feedback;
    }

    private async void OnDateChipTapped(object? sender, TappedEventArgs e)
    {
        HideDescriptionSuggestions();
        DescriptionEntry.Unfocus();
        var feedback = InteractionAnimations.PulseAsync(sender);
        var selectedDate = DatePickerRequested is null
            ? null
            : await DatePickerRequested(
                viewModel.TransactionDate,
                DateRangeLimits.MinimumDate,
                DateRangeLimits.MaximumDate);
        if (selectedDate is not null)
        {
            viewModel.SetTransactionDate(selectedDate.Value);
            UpdateDateLabel(viewModel.TransactionDate);
        }

        await feedback;
    }

    private void OnDescriptionTextChanged(object? sender, TextChangedEventArgs e)
    {
        UpdateSaveButton();
        if (!isApplyingDescriptionSuggestion)
        {
            UpdateDescriptionSuggestions(e.NewTextValue);
        }
    }

    private void OnDescriptionSuggestionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not TransactionHistorySuggestion suggestion)
        {
            return;
        }

        isApplyingDescriptionSuggestion = true;
        DescriptionEntry.Text = suggestion.Description;
        DescriptionEntry.CursorPosition = suggestion.Description.Length;
        viewModel.ApplySuggestion(suggestion);
        UpdateTypeAndCategory();
        UpdatePaymentMethod();
        isApplyingDescriptionSuggestion = false;
        HideDescriptionSuggestions();
        DescriptionEntry.Focus();
    }

    private void UpdateDescriptionSuggestions(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            HideDescriptionSuggestions();
            return;
        }

        var matches = viewModel.FindDescriptionSuggestions(input);

        DescriptionSuggestionsView.ItemsSource = matches;
        DescriptionSuggestionsPanel.HeightRequest = matches.Count * 54 + 8;
        DescriptionSuggestionsPanel.TranslationY =
            DescriptionFieldContainer.Y + DescriptionFieldContainer.Height + 8;
        DescriptionSuggestionsPanel.IsVisible = matches.Count > 0;
    }

    private void HideDescriptionSuggestions()
    {
        DescriptionSuggestionsPanel.IsVisible = false;
        DescriptionSuggestionsPanel.HeightRequest = 0;
    }

    private async void OnSaveTapped(object? sender, TappedEventArgs e)
    {
        HideDescriptionSuggestions();

        if (viewModel.HasPendingCalculation)
        {
            if (isSaving || !viewModel.CanCompleteCalculation)
            {
                return;
            }

            var equalsFeedback = InteractionAnimations.PulseAsync(sender);
            viewModel.CompleteCalculation();
            UpdateAmountAndSaveState();
            await equalsFeedback;
            return;
        }

        var amount = viewModel.EffectiveAmount;
        if (isSaving ||
            amount <= 0 ||
            string.IsNullOrWhiteSpace(DescriptionEntry.Text) ||
            database is null)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        isSaving = true;
        UpdateSaveButton();

        try
        {
            var transaction = viewModel.CreateTransaction(
                DescriptionEntry.Text ?? string.Empty);

            async Task SaveAndRefreshAsync()
            {
                if (!viewModel.IsEditing)
                {
                    await database.SaveTransactionAsync(transaction);
                }
                else
                {
                    await database.UpdateTransactionAsync(transaction);
                }

                SaveLabel.Text = viewModel.IsEditing ? "Updated" : "Saved";
                await CloseAsync();

                var transactionSaved = TransactionSaved;
                if (transactionSaved is not null)
                {
                    await transactionSaved();
                }
            }

            var loadingHandler = RunWithTransactionLoadingAsync;
            if (loadingHandler is null)
            {
                await SaveAndRefreshAsync();
            }
            else
            {
                await loadingHandler(SaveAndRefreshAsync);
            }
        }
        catch
        {
            SaveLabel.Text = "Try again";
            await Task.Delay(900);
        }
        finally
        {
            isSaving = false;
            UpdateSaveButton();
            await feedback;
        }
    }

    private void ResetForm()
    {
        var transaction = viewModel.EditingTransaction;
        isSaving = false;
        isApplyingDescriptionSuggestion = false;
        HideDescriptionSuggestions();
        isApplyingDescriptionSuggestion = true;
        DescriptionEntry.Text = transaction?.Description ?? string.Empty;
        isApplyingDescriptionSuggestion = false;
        CurrencySymbolLabel.Text = viewModel.Currency.Symbol;
        UpdateDateLabel(viewModel.TransactionDate);
        UpdateTypeAndCategory();
        UpdatePaymentMethod();
        UpdateAmountAndSaveState();
    }

    private void UpdateDateLabel(DateTime date)
    {
        DateLabel.Text = date.ToString(
            "dddd, d MMMM yyyy",
            CultureInfo.CurrentCulture);
    }

    private void UpdateTypeAndCategory()
    {
        TypeLabel.Text = viewModel.SelectedType.Title;
        var isIncome = TransactionCatalog.IsIncomeType(viewModel.SelectedType.Key);
        IncomeArrowPath.IsVisible = isIncome;
        ExpenseArrowPath.IsVisible = !isIncome;
        UpdateCategory();
    }

    private void UpdatePaymentMethod()
    {
        PaymentLabel.Text = viewModel.SelectedPayment.Title;
        PaymentIcon.Source = viewModel.SelectedPayment.IconAsset;
    }

    private void UpdateCategory()
    {
        CategoryLabel.Text = viewModel.SelectedCategory.Title;
        CategoryIcon.Source = viewModel.SelectedCategory.IconAsset;
    }

    private void UpdateAmountAndSaveState()
    {
        var amountText = viewModel.AmountDisplayText;
        AmountLabel.Text = amountText;
        AmountLabel.FontSize = amountText.Length switch
        {
            > 20 => 31,
            > 15 => 37,
            > 11 => 44,
            > 7 => 54,
            _ => 70
        };
        UpdateSaveButton();
    }

    private void UpdateSaveButton()
    {
        var isEqualsAction = viewModel.HasPendingCalculation;
        var hasDescription = !string.IsNullOrWhiteSpace(DescriptionEntry.Text);
        var canSave = viewModel.EffectiveAmount > 0 && hasDescription;
        var canCalculate = viewModel.CanCompleteCalculation;
        var isActionEnabled = !isSaving &&
            (isEqualsAction ? canCalculate : canSave);
        SaveButton.IsEnabled = isActionEnabled;
        if (isActionEnabled)
        {
            ThemeResourceBindings.SetDynamic(
                SaveButton,
                Border.BackgroundColorProperty,
                "Accent");
            ThemeResourceBindings.SetStatic(
                SaveButton,
                Border.StrokeProperty,
                Brush.Transparent);
            ThemeResourceBindings.SetDynamic(
                SaveLabel,
                Label.TextColorProperty,
                "AccentForeground");
        }
        else
        {
            ThemeResourceBindings.SetColor(
                SaveButton,
                Border.BackgroundColorProperty,
                "SurfaceMutedLight",
                "SurfaceMutedDark");
            ThemeResourceBindings.SetBrush(
                SaveButton,
                Border.StrokeProperty,
                "DividerLight",
                "DividerDark");
            ThemeResourceBindings.SetColor(
                SaveLabel,
                Label.TextColorProperty,
                "SecondaryTextLight",
                "SecondaryTextDark");
        }
        SaveLabel.Text = isSaving
            ? "Saving…"
            : isEqualsAction ? "=" : viewModel.IsEditing ? "Update" : "Save";
        SaveLabel.FontSize = isEqualsAction ? 23 : 14;
        SaveButton.Opacity = isSaving ? 0.7 : 1;
    }

}
