using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.Views;

public partial class NotificationRulesView : ContentView
{
    private static readonly TransactionOption KeepDetectedPayment =
        new(string.Empty, "Keep detected payment", "payment_others.png");

    private NotificationRuleStore? store;
    private List<NotificationRule> rules = [];
    private string selectedType = TransactionCatalog.ExpenseTypeKey;
    private string? editingRuleId;
    private bool isAnimating;
    private bool isSaving;

    public event Action<bool>? VisibilityChanged;
    public bool IsOpen => IsVisible;

    public NotificationRulesView()
    {
        InitializeComponent();
        CategoryPicker.ItemDisplayBinding = new Binding(nameof(TransactionOption.Title));
        PaymentPicker.ItemDisplayBinding = new Binding(nameof(TransactionOption.Title));
        PaymentPicker.ItemsSource = new[] { KeepDetectedPayment }
            .Concat(TransactionCatalog.PaymentMethods)
            .ToList();
        UpdateTypeControls();
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
            TestResultLabel.Text = "Enter a sample notification first.";
            TestResultLabel.IsVisible = true;
            return;
        }

        var match = rules
            .OrderBy(rule => rule.SortOrder)
            .FirstOrDefault(rule => RuleMatches(rule, notification, source));
        TestResultLabel.Text = match is null
            ? "No enabled rule matched. The built-in suggestions will be used."
            : $"Matches {match.NameDisplay}: {match.ActionDisplay}";
        TestResultLabel.IsVisible = true;
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
            : TransactionCatalog.ExpenseTypeKey;
        UpdateTypeControls(rule?.Category);
        PaymentPicker.SelectedItem = string.IsNullOrWhiteSpace(rule?.PaymentMethod)
            ? KeepDetectedPayment
            : TransactionCatalog.GetPaymentMethod(rule.PaymentMethod);
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
        var isIncome = TransactionCatalog.IsIncomeType(selectedType);
        ExpenseTypeOption.Style = GetStyle(!isIncome);
        IncomeTypeOption.Style = GetStyle(isIncome);
        ExpenseTypeLabel.TextColor = ResolveTextColor(!isIncome);
        IncomeTypeLabel.TextColor = ResolveTextColor(isIncome);
        var categories = (isIncome
            ? TransactionCatalog.IncomeCategories
            : TransactionCatalog.ExpenseCategories).ToList();
        CategoryPicker.ItemsSource = categories;
        CategoryPicker.SelectedItem = categories.FirstOrDefault(category =>
            category.Key.Equals(preferredCategory, StringComparison.OrdinalIgnoreCase))
            ?? categories[^1];
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
        if (e.Parameter is not string type || type == selectedType)
        {
            return;
        }
        selectedType = type;
        UpdateTypeControls();
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
        if (CategoryPicker.SelectedItem is not TransactionOption category)
        {
            ShowEditorError("Select a category.");
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
            target.Category = category.Key;
            target.PaymentMethod = PaymentPicker.SelectedItem is TransactionOption payment
                ? payment.Key
                : string.Empty;
            target.DescriptionOverride = DescriptionEntry.Text?.Trim() ?? string.Empty;
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
