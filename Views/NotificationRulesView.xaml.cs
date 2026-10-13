using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.Views;

public partial class NotificationRulesView : ContentView
{
    private enum SelectorKind
    {
        Category,
        PaymentMethod
    }

    private static readonly TransactionOption KeepDetectedCategory =
        new(string.Empty, "Keep detected category", "category_expense_others.png");
    private static readonly TransactionOption KeepDetectedPayment =
        new(string.Empty, "Keep detected payment", "payment_others.png");

    private NotificationRuleStore? store;
    private List<NotificationRule> rules = [];
    private string selectedType = string.Empty;
    private TransactionOption selectedCategory = KeepDetectedCategory;
    private TransactionOption selectedPayment = KeepDetectedPayment;
    private SelectorKind selectorKind;
    private IReadOnlyList<SelectableTransactionOption> selectorOptions = [];
    private string? editingRuleId;
    private bool isAnimating;
    private bool isSaving;
    private bool isSelectorOpen;
    private bool isSelectorAnimating;

    public event Action<bool>? VisibilityChanged;
    public bool IsOpen => IsVisible;

    public NotificationRulesView()
    {
        InitializeComponent();
        UpdateTypeControls();
        UpdatePaymentSelector();
    }

    public void Configure(NotificationRuleStore notificationRuleStore) =>
        store = notificationRuleStore;

    public async Task OpenAsync()
    {
        if (IsVisible || isAnimating)
        {
            return;
        }

        isAnimating = true;
        IsVisible = true;
        Root.Opacity = 0;
        VisibilityChanged?.Invoke(true);
        try
        {
            await RefreshAsync();
            await Root.FadeToAsync(1, 170, Easing.CubicOut);
        }
        finally
        {
            Root.Opacity = 1;
            isAnimating = false;
        }
    }

    public async Task CloseAsync()
    {
        if (!IsVisible || isAnimating)
        {
            return;
        }

        if (isSelectorOpen)
        {
            await CloseSelectorAsync();
            return;
        }

        if (EditorOverlay.IsVisible)
        {
            await CloseEditorAsync();
            return;
        }

        isAnimating = true;
        try
        {
            await Root.FadeToAsync(0, 135, Easing.CubicIn);
        }
        finally
        {
            IsVisible = false;
            Root.Opacity = 0;
            isAnimating = false;
            VisibilityChanged?.Invoke(false);
        }
    }

    public Task HandleBackAsync() => CloseAsync();

    private async Task RefreshAsync()
    {
        if (store is null)
        {
            return;
        }

        LoadingOverlay.IsVisible = true;
        ErrorCard.IsVisible = false;
        try
        {
            rules = (await store.GetRulesAsync()).Select(rule => rule.Clone()).ToList();
            RefreshRulePresentation();
        }
        catch (Exception exception)
        {
            ShowError($"Notification rules could not be loaded. {exception.Message}");
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }

    private void RefreshRulePresentation()
    {
        for (var index = 0; index < rules.Count; index++)
        {
            rules[index].SortOrder = index;
            rules[index].CanMoveUp = index > 0;
            rules[index].CanMoveDown = index < rules.Count - 1;
        }
        BindableLayout.SetItemsSource(RulesList, rules.Select(rule => rule.Clone()).ToList());
        RulesList.IsVisible = rules.Count > 0;
        EmptyState.IsVisible = rules.Count == 0;
    }

    private async Task PersistAsync()
    {
        if (store is null)
        {
            return;
        }

        for (var index = 0; index < rules.Count; index++)
        {
            rules[index].SortOrder = index;
        }
        await store.SaveRulesAsync(rules);
        rules = (await store.GetRulesAsync()).Select(rule => rule.Clone()).ToList();
        RefreshRulePresentation();
        ErrorCard.IsVisible = false;
    }

    private async void OnAddRuleClicked(object? sender, EventArgs e) =>
        await OpenEditorAsync(null);

    private void OnTestRulesClicked(object? sender, EventArgs e)
    {
        var notification = TestNotificationEditor.Text?.Trim() ?? string.Empty;
        var source = TestSourceEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(notification))
        {
            ShowTestResult("Enter a sample notification first.", false);
            return;
        }

        var matches = rules
            .OrderBy(rule => rule.SortOrder)
            .Where(rule => RuleMatches(rule, notification, source))
            .ToList();
        var result = matches.Count == 0
            ? "No enabled rule matched. The built-in suggestions will be used."
            : $"{matches.Count} rule{(matches.Count == 1 ? string.Empty : "s")} matched: " +
              $"{string.Join(", ", matches.Select(rule => rule.NameDisplay))}. " +
              $"Combined: {BuildCombinedActionDisplay(matches)}";
        ShowTestResult(result, true);
    }

    private async void OnSendTestNotificationClicked(object? sender, EventArgs e)
    {
        var notification = TestNotificationEditor.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(notification))
        {
            ShowTestResult("Enter a sample notification first.", false);
            return;
        }

        SendTestNotificationButton.IsEnabled = false;
        try
        {
            var result = await LocalNotificationTestService.SendAsync(notification);
            ShowTestResult(result.Message, result.IsSuccess);
        }
        catch (Exception exception)
        {
            ShowTestResult(
                $"The test notification could not be sent. {exception.Message}",
                false);
        }
        finally
        {
            SendTestNotificationButton.IsEnabled = true;
        }
    }

    private void ShowTestResult(string message, bool isSuccess)
    {
        TestResultLabel.Text = message;
        var resourceKey = isSuccess
            ? "Accent"
            : Application.Current?.RequestedTheme == AppTheme.Dark
                ? "NegativeDark"
                : "NegativeLight";
        if (Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
            value is Color color)
        {
            TestResultLabel.TextColor = color;
        }
        TestResultLabel.IsVisible = true;
    }

    private static string BuildCombinedActionDisplay(
        IReadOnlyList<NotificationRule> matches)
    {
        var combined = new NotificationRule
        {
            TransactionType = matches
                .Select(rule => rule.TransactionType)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
            Category = matches
                .Select(rule => rule.Category)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
            PaymentMethod = matches
                .Select(rule => rule.PaymentMethod)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
            DescriptionOverride = matches
                .Select(rule => rule.DescriptionOverride)
                .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty
        };
        return combined.ActionDisplay;
    }

    private static bool RuleMatches(
        NotificationRule rule,
        string notification,
        string source)
    {
        if (!rule.IsEnabled)
        {
            return false;
        }
        var sourceMatches = string.IsNullOrWhiteSpace(rule.SourceApp) ||
            source.Contains(rule.SourceApp, StringComparison.OrdinalIgnoreCase);
        if (!sourceMatches)
        {
            return false;
        }
        return rule.MatchText
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(keyword => notification.Contains(
                keyword,
                StringComparison.OrdinalIgnoreCase));
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: NotificationRule rule })
        {
            await OpenEditorAsync(rule);
        }
    }

    private async Task OpenEditorAsync(NotificationRule? rule)
    {
        editingRuleId = rule?.Id;
        EditorTitle.Text = rule is null ? "Add notification rule" : "Edit notification rule";
        RuleNameEntry.Text = rule?.Name ?? string.Empty;
        MatchTextEntry.Text = rule?.MatchText ?? string.Empty;
        SourceAppEntry.Text = rule?.SourceApp ?? string.Empty;
        DescriptionEntry.Text = rule?.DescriptionOverride ?? string.Empty;
        EnabledSwitch.IsToggled = rule?.IsEnabled ?? true;
        selectedType = TransactionCatalog.IsIncomeType(rule?.TransactionType ?? string.Empty)
            ? TransactionCatalog.IncomeTypeKey
            : TransactionCatalog.IsExpenseType(rule?.TransactionType ?? string.Empty)
                ? TransactionCatalog.ExpenseTypeKey
                : string.Empty;
        UpdateTypeControls(rule?.Category);
        selectedPayment = string.IsNullOrWhiteSpace(rule?.PaymentMethod)
            ? KeepDetectedPayment
            : TransactionCatalog.GetPaymentMethod(rule.PaymentMethod);
        UpdatePaymentSelector();
        EditorErrorLabel.IsVisible = false;
        EditorOverlay.IsVisible = true;
        EditorOverlay.Opacity = 0;
        await EditorOverlay.FadeToAsync(1, 150, Easing.CubicOut);
    }

    private async Task CloseEditorAsync()
    {
        if (!EditorOverlay.IsVisible)
        {
            return;
        }
        if (isSelectorOpen)
        {
            await CloseSelectorAsync();
        }
        await EditorOverlay.FadeToAsync(0, 120, Easing.CubicIn);
        EditorOverlay.IsVisible = false;
        editingRuleId = null;
        RuleNameEntry.Unfocus();
        MatchTextEntry.Unfocus();
        SourceAppEntry.Unfocus();
        DescriptionEntry.Unfocus();
    }

    private void UpdateTypeControls(string? preferredCategory = null)
    {
        var keepDetected = string.IsNullOrWhiteSpace(selectedType);
        var isIncome = TransactionCatalog.IsIncomeType(selectedType);
        DetectedTypeOption.Style = GetStyle(keepDetected);
        ExpenseTypeOption.Style = GetStyle(!keepDetected && !isIncome);
        IncomeTypeOption.Style = GetStyle(!keepDetected && isIncome);
        DetectedTypeLabel.TextColor = ResolveTextColor(keepDetected);
        ExpenseTypeLabel.TextColor = ResolveTextColor(!keepDetected && !isIncome);
        IncomeTypeLabel.TextColor = ResolveTextColor(isIncome);
        var categories = isIncome
            ? TransactionCatalog.IncomeCategories
            : TransactionCatalog.ExpenseCategories;
        var categoryKey = preferredCategory ?? selectedCategory.Key;
        selectedCategory = keepDetected
            ? KeepDetectedCategory
            : categories.FirstOrDefault(category =>
                category.Key.Equals(categoryKey, StringComparison.OrdinalIgnoreCase))
                ?? KeepDetectedCategory;
        CategorySelector.IsEnabled = !keepDetected;
        CategorySelector.Opacity = keepDetected ? 0.68 : 1;
        CategoryHintLabel.IsVisible = keepDetected;
        UpdateCategorySelector();
    }

    private static Style GetStyle(bool selected) =>
        (Style)Application.Current!.Resources[
            selected ? "SelectedOptionCard" : "OptionCard"];

    private static Color ResolveTextColor(bool selected) =>
        selected
            ? (Color)Application.Current!.Resources["Accent"]
            : Application.Current!.RequestedTheme == AppTheme.Dark
                ? (Color)Application.Current.Resources["PrimaryTextDark"]
                : (Color)Application.Current.Resources["PrimaryTextLight"];

    private void OnRuleTypeTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string type)
        {
            return;
        }
        type = type.Equals("Detected", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : type;
        if (type == selectedType)
        {
            return;
        }
        selectedType = type;
        UpdateTypeControls();
    }

    private async void OnCategorySelectorTapped(object? sender, TappedEventArgs e)
    {
        if (!CategorySelector.IsEnabled)
        {
            return;
        }
        var feedback = InteractionAnimations.PulseAsync(sender);
        var categories = TransactionCatalog.IsIncomeType(selectedType)
            ? TransactionCatalog.IncomeCategories
            : TransactionCatalog.ExpenseCategories;
        await OpenSelectorAsync(
            SelectorKind.Category,
            "Choose category override",
            new[] { KeepDetectedCategory }.Concat(categories).ToList());
        await feedback;
    }

    private async void OnPaymentSelectorTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await OpenSelectorAsync(
            SelectorKind.PaymentMethod,
            "Choose payment override",
            new[] { KeepDetectedPayment }
                .Concat(TransactionCatalog.PaymentMethods)
                .ToList());
        await feedback;
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
        var selectedKey = kind == SelectorKind.Category
            ? selectedCategory.Key
            : selectedPayment.Key;
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

        if (selectorKind == SelectorKind.Category)
        {
            selectedCategory = selectedOption.Option;
            UpdateCategorySelector();
        }
        else
        {
            selectedPayment = selectedOption.Option;
            UpdatePaymentSelector();
        }

        await Task.Delay(110);
        await CloseSelectorAsync();
    }

    private async void OnSelectorBackdropTapped(object? sender, TappedEventArgs e) =>
        await CloseSelectorAsync();

    private async void OnSelectorCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseSelectorAsync();
        await feedback;
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

    private void UpdateCategorySelector()
    {
        CategorySelectorLabel.Text = selectedCategory.Title;
        CategorySelectorIcon.Source = selectedCategory.IconAsset;
    }

    private void UpdatePaymentSelector()
    {
        PaymentSelectorLabel.Text = selectedPayment.Title;
        PaymentSelectorIcon.Source = selectedPayment.IconAsset;
    }

    private async void OnSaveRuleClicked(object? sender, EventArgs e)
    {
        if (isSaving)
        {
            return;
        }

        var matchText = MatchTextEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(matchText))
        {
            ShowEditorError("Enter the text that should be found in the notification.");
            return;
        }
        var categoryKey = string.IsNullOrWhiteSpace(selectedType)
            ? string.Empty
            : selectedCategory.Key;
        var paymentKey = selectedPayment.Key;
        var descriptionOverride = DescriptionEntry.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(selectedType) &&
            string.IsNullOrWhiteSpace(categoryKey) &&
            string.IsNullOrWhiteSpace(paymentKey) &&
            string.IsNullOrWhiteSpace(descriptionOverride))
        {
            ShowEditorError("Choose at least one field for this rule to override.");
            return;
        }

        isSaving = true;
        SaveRuleButton.IsEnabled = false;
        try
        {
            var target = rules.FirstOrDefault(rule => rule.Id == editingRuleId);
            if (target is null)
            {
                target = new NotificationRule { SortOrder = rules.Count };
                rules.Add(target);
            }

            target.Name = RuleNameEntry.Text?.Trim() ?? string.Empty;
            target.MatchText = matchText;
            target.SourceApp = SourceAppEntry.Text?.Trim() ?? string.Empty;
            target.IsEnabled = EnabledSwitch.IsToggled;
            target.TransactionType = selectedType;
            target.Category = categoryKey;
            target.PaymentMethod = paymentKey;
            target.DescriptionOverride = descriptionOverride;
            await PersistAsync();
            await CloseEditorAsync();
        }
        catch (Exception exception)
        {
            ShowEditorError($"This rule could not be saved. {exception.Message}");
        }
        finally
        {
            SaveRuleButton.IsEnabled = true;
            isSaving = false;
        }
    }

    private async void OnToggleClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: NotificationRule selected })
        {
            return;
        }
        var rule = rules.FirstOrDefault(item => item.Id == selected.Id);
        if (rule is null)
        {
            return;
        }
        rule.IsEnabled = !rule.IsEnabled;
        await TryPersistAsync();
    }

    private async void OnMoveUpClicked(object? sender, EventArgs e) =>
        await MoveAsync(sender, -1);

    private async void OnMoveDownClicked(object? sender, EventArgs e) =>
        await MoveAsync(sender, 1);

    private async Task MoveAsync(object? sender, int direction)
    {
        if (sender is not Button { CommandParameter: NotificationRule selected })
        {
            return;
        }
        var index = rules.FindIndex(rule => rule.Id == selected.Id);
        var target = index + direction;
        if (index < 0 || target < 0 || target >= rules.Count)
        {
            return;
        }
        (rules[index], rules[target]) = (rules[target], rules[index]);
        await TryPersistAsync();
    }

    private async void OnDeleteClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: NotificationRule selected })
        {
            return;
        }
        var confirmed = Window?.Page is Page page && await page.DisplayAlertAsync(
            "Delete notification rule?",
            $"Delete {selected.NameDisplay}? Future captures will no longer use it.",
            "Delete",
            "Keep");
        if (!confirmed)
        {
            return;
        }
        rules.RemoveAll(rule => rule.Id == selected.Id);
        await TryPersistAsync();
    }

    private async Task TryPersistAsync()
    {
        try
        {
            await PersistAsync();
        }
        catch (Exception exception)
        {
            ShowError($"Notification rules could not be updated. {exception.Message}");
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorCard.IsVisible = true;
    }

    private void ShowEditorError(string message)
    {
        EditorErrorLabel.Text = message;
        EditorErrorLabel.IsVisible = true;
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseAsync();
        await feedback;
    }

    private async void OnEditorCloseTapped(object? sender, TappedEventArgs e) =>
        await CloseEditorAsync();

    private async void OnEditorCancelClicked(object? sender, EventArgs e) =>
        await CloseEditorAsync();
}
