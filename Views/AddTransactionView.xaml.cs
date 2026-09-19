using System.Globalization;
using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.ViewModels;

namespace FinancialTracker.Views;

public partial class AddTransactionView : ContentView
{
    public event EventHandler? TransactionSaved;

    private enum SelectorKind
    {
        PaymentMethod,
        Category
    }

    private LocalDatabase? database;
    private CurrencyOption currency = SettingsViewModelDefaults.Currency;
    private TransactionOption selectedType = TransactionCatalog.TransactionTypes[0];
    private TransactionOption selectedPayment = TransactionCatalog.PaymentMethods.First(item => item.Key == "Cash");
    private TransactionOption selectedCategory = TransactionCatalog.ExpenseCategories[^1];
    private SelectorKind selectorKind;
    private string currentInput = "0";
    private decimal accumulator;
    private string? pendingOperator;
    private bool startNewInput = true;
    private bool isOpen;
    private bool isAnimating;
    private bool isSelectorOpen;
    private bool isSelectorAnimating;
    private bool isTypeAnimating;
    private bool isSaving;
    private bool isApplyingDescriptionSuggestion;
    private IReadOnlyList<string> descriptionHistory = [];
    private TransactionRecord? editingTransaction;
    private IReadOnlyList<SelectableTransactionOption> selectorOptions = [];

    public AddTransactionView()
    {
        InitializeComponent();
        TransactionDatePicker.MaximumDate = DateTime.Today.AddYears(10);
        TransactionDatePicker.MinimumDate = new DateTime(2000, 1, 1);
    }

    public async Task OpenAsync(
        LocalDatabase localDatabase,
        CurrencyOption selectedCurrency,
        TransactionRecord? transactionToEdit = null)
    {
        if (isOpen || isAnimating)
        {
            return;
        }

        database = localDatabase;
        editingTransaction = transactionToEdit;
        currency = transactionToEdit is null
            ? selectedCurrency
            : SettingsViewModel.SupportedCurrencies.FirstOrDefault(option =>
                option.Code.Equals(
                    transactionToEdit.CurrencyCode,
                    StringComparison.OrdinalIgnoreCase))
              ?? selectedCurrency;
        var descriptionHistoryTask = LoadDescriptionHistoryAsync(localDatabase);
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
        if (editingTransaction is null)
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
        var categories = selectedType.Key == "Income"
            ? TransactionCatalog.IncomeCategories
            : TransactionCatalog.ExpenseCategories;
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
                selectedPayment = selectedOption.Option;
                UpdatePaymentMethod();
                break;
            case SelectorKind.Category:
                selectedCategory = selectedOption.Option;
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

            selectedType = selectedType.Key == "Expense"
                ? TransactionCatalog.TransactionTypes.First(item => item.Key == "Income")
                : TransactionCatalog.TransactionTypes.First(item => item.Key == "Expense");
            selectedCategory = selectedType.Key == "Income"
                ? TransactionCatalog.IncomeCategories[^1]
                : TransactionCatalog.ExpenseCategories[^1];
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
            ? selectedPayment.Key
            : selectedCategory.Key;
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

        if (key == "back")
        {
            RemoveLastCharacter();
        }
        else if (key is "+" or "-" or "*" or "/")
        {
            SelectOperator(key);
        }
        else
        {
            AppendInput(key);
        }

        UpdateAmountAndSaveState();
        await feedback;
    }

    private void AppendInput(string key)
    {
        if (startNewInput)
        {
            currentInput = key == "." ? "0." : key;
            startNewInput = false;
            return;
        }

        if (key == ".")
        {
            if (!currentInput.Contains('.'))
            {
                currentInput += ".";
            }

            return;
        }

        var decimalIndex = currentInput.IndexOf('.');
        if (decimalIndex >= 0 && currentInput.Length - decimalIndex > 2)
        {
            return;
        }

        if (currentInput.Replace(".", string.Empty, StringComparison.Ordinal).Length >= 10)
        {
            return;
        }

        currentInput = currentInput == "0" ? key : currentInput + key;
    }

    private void RemoveLastCharacter()
    {
        if (pendingOperator is not null && startNewInput)
        {
            currentInput = FormatAmount(accumulator);
            accumulator = 0;
            pendingOperator = null;
            startNewInput = false;
            return;
        }

        if (startNewInput)
        {
            currentInput = "0";
            startNewInput = false;
            return;
        }

        currentInput = currentInput.Length <= 1
            ? "0"
            : currentInput[..^1];
    }

    private void SelectOperator(string operation)
    {
        var currentValue = ParseCurrentInput();
        if (pendingOperator is not null && !startNewInput)
        {
            accumulator = Calculate(accumulator, currentValue, pendingOperator);
            currentInput = FormatAmount(accumulator);
        }
        else
        {
            accumulator = currentValue;
        }

        pendingOperator = operation;
        startNewInput = true;
    }

    private decimal GetEffectiveAmount()
    {
        if (pendingOperator is null || startNewInput)
        {
            return ParseCurrentInput();
        }

        return Calculate(accumulator, ParseCurrentInput(), pendingOperator);
    }

    private void CompleteCalculation()
    {
        if (pendingOperator is null || startNewInput)
        {
            return;
        }

        currentInput = FormatAmount(
            Calculate(accumulator, ParseCurrentInput(), pendingOperator));
        accumulator = 0;
        pendingOperator = null;
        startNewInput = false;
    }

    private static decimal Calculate(decimal left, decimal right, string operation) =>
        operation switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" when right != 0 => left / right,
            _ => left
        };

    private decimal ParseCurrentInput() =>
        decimal.TryParse(
            currentInput.TrimEnd('.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : 0;

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);

    private async void OnDateChipTapped(object? sender, TappedEventArgs e)
    {
        HideDescriptionSuggestions();
        DescriptionEntry.Unfocus();
        var feedback = InteractionAnimations.PulseAsync(sender);

        Dispatcher.Dispatch(() =>
        {
            TransactionDatePicker.IsOpen = true;
        });

        await feedback;
    }

    private void OnTransactionDateSelected(object? sender, DateChangedEventArgs e) =>
        UpdateDateLabel(e.NewDate ?? DateTime.Today);

    private void OnTransactionDatePickerFocused(object? sender, FocusEventArgs e)
    {
        HideDescriptionSuggestions();
        DescriptionEntry.Unfocus();
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
        if (e.Parameter is not string description)
        {
            return;
        }

        isApplyingDescriptionSuggestion = true;
        DescriptionEntry.Text = description;
        DescriptionEntry.CursorPosition = description.Length;
        isApplyingDescriptionSuggestion = false;
        HideDescriptionSuggestions();
        DescriptionEntry.Focus();
    }

    private async Task LoadDescriptionHistoryAsync(LocalDatabase localDatabase)
    {
        try
        {
            var records = await localDatabase.GetTransactionsAsync();
            descriptionHistory = records
                .Select(record => record.Description.Trim())
                .Where(description => !string.IsNullOrWhiteSpace(description))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            descriptionHistory = [];
        }
    }

    private void UpdateDescriptionSuggestions(string? input)
    {
        var query = input?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            HideDescriptionSuggestions();
            return;
        }

        var matches = descriptionHistory
            .Where(description => description.Contains(
                query,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(description => description.StartsWith(
                query,
                StringComparison.OrdinalIgnoreCase))
            .Take(4)
            .ToList();

        DescriptionSuggestionsView.ItemsSource = matches;
        DescriptionSuggestionsPanel.HeightRequest = matches.Count * 46 + 8;
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

        if (pendingOperator is not null)
        {
            if (isSaving || startNewInput)
            {
                return;
            }

            var equalsFeedback = InteractionAnimations.PulseAsync(sender);
            CompleteCalculation();
            UpdateAmountAndSaveState();
            await equalsFeedback;
            return;
        }

        var amount = GetEffectiveAmount();
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
            var transaction = new TransactionRecord
            {
                Id = editingTransaction?.Id ?? 0,
                Type = selectedType.Key,
                Category = selectedCategory.Key,
                PaymentMethod = selectedPayment.Key,
                Description = DescriptionEntry.Text?.Trim() ?? string.Empty,
                AmountMinor = decimal.ToInt64(decimal.Round(amount * 100, 0, MidpointRounding.AwayFromZero)),
                CurrencyCode = currency.Code,
                TransactionDate = (TransactionDatePicker.Date ?? DateTime.Today).Date,
                CreatedAtUtc = editingTransaction?.CreatedAtUtc ?? DateTime.UtcNow
            };

            if (editingTransaction is null)
            {
                await database.SaveTransactionAsync(transaction);
            }
            else
            {
                await database.UpdateTransactionAsync(transaction);
            }

            TransactionSaved?.Invoke(this, EventArgs.Empty);
            SaveLabel.Text = editingTransaction is null ? "Saved" : "Updated";
            await Task.Delay(420);
            await CloseAsync();
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
        var transaction = editingTransaction;
        selectedType = transaction is null
            ? TransactionCatalog.TransactionTypes[0]
            : TransactionCatalog.TransactionTypes.FirstOrDefault(item =>
                item.Key.Equals(transaction.Type, StringComparison.OrdinalIgnoreCase))
              ?? TransactionCatalog.TransactionTypes[0];
        selectedPayment = transaction is null
            ? TransactionCatalog.PaymentMethods.First(item => item.Key == "Cash")
            : TransactionCatalog.PaymentMethods.FirstOrDefault(item =>
                item.Key.Equals(transaction.PaymentMethod, StringComparison.OrdinalIgnoreCase))
              ?? TransactionCatalog.PaymentMethods.First(item => item.Key == "Cash");
        var categories = selectedType.Key == "Income"
            ? TransactionCatalog.IncomeCategories
            : TransactionCatalog.ExpenseCategories;
        selectedCategory = transaction is null
            ? categories[^1]
            : categories.FirstOrDefault(item =>
                item.Key.Equals(transaction.Category, StringComparison.OrdinalIgnoreCase))
              ?? categories[^1];
        currentInput = transaction is null
            ? "0"
            : FormatAmount(transaction.AmountMinor / 100m);
        accumulator = 0;
        pendingOperator = null;
        startNewInput = true;
        isSaving = false;
        isApplyingDescriptionSuggestion = false;
        HideDescriptionSuggestions();
        isApplyingDescriptionSuggestion = true;
        DescriptionEntry.Text = transaction?.Description ?? string.Empty;
        isApplyingDescriptionSuggestion = false;
        var transactionDate = transaction?.TransactionDate.Date ?? DateTime.Today;
        TransactionDatePicker.Date = transactionDate;
        CurrencySymbolLabel.Text = currency.Symbol;
        UpdateDateLabel(transactionDate);
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
        TypeLabel.Text = selectedType.Title;
        var isIncome = selectedType.Key == "Income";
        IncomeArrowPath.IsVisible = isIncome;
        ExpenseArrowPath.IsVisible = !isIncome;
        UpdateCategory();
    }

    private void UpdatePaymentMethod()
    {
        PaymentLabel.Text = selectedPayment.Title;
        PaymentIcon.Source = selectedPayment.IconAsset;
    }

    private void UpdateCategory()
    {
        CategoryLabel.Text = selectedCategory.Title;
        CategoryIcon.Source = selectedCategory.IconAsset;
    }

    private void UpdateAmountAndSaveState()
    {
        var amountText = GetAmountDisplayText();
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

    private string GetAmountDisplayText()
    {
        if (pendingOperator is null)
        {
            return currentInput;
        }

        var operation = pendingOperator switch
        {
            "*" => "×",
            "/" => "÷",
            "-" => "−",
            _ => "+"
        };
        var left = FormatAmount(accumulator);
        return startNewInput
            ? $"{left} {operation}"
            : $"{left} {operation} {currentInput}";
    }

    private void UpdateSaveButton()
    {
        var isEqualsAction = pendingOperator is not null;
        var hasDescription = !string.IsNullOrWhiteSpace(DescriptionEntry.Text);
        var canSave = GetEffectiveAmount() > 0 && hasDescription;
        var canCalculate = isEqualsAction && !startNewInput;
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
            : isEqualsAction ? "=" : editingTransaction is null ? "Save" : "Update";
        SaveLabel.FontSize = isEqualsAction ? 23 : 14;
        SaveButton.Opacity = isSaving ? 0.7 : 1;
    }

    private static class SettingsViewModelDefaults
    {
        public static CurrencyOption Currency { get; } =
            new("", "flag_myr.png", "MYR", "Malaysian Ringgit", "RM");
    }
}
