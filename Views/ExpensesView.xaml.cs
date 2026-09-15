namespace FinancialTracker.Views;

using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

public partial class ExpensesView : ContentView
{
    public event Action<int>? EditTransactionRequested;
    public event Action<int>? DeleteTransactionRequested;

    private enum FilterSelectorKind
    {
        PaymentMethod,
        Category
    }

    private static readonly TransactionOption ClearPaymentFilterOption =
        new("", "All payment methods", "settings_payment_methods.png");
    private static readonly TransactionOption ClearCategoryFilterOption =
        new("", "All categories", "settings_categories.png");
    private static readonly IReadOnlyList<TransactionOption> PaymentFilterOptions =
        [ClearPaymentFilterOption, .. TransactionCatalog.PaymentMethods];
    private static readonly IReadOnlyList<TransactionOption> CategoryFilterOptions =
    [
        ClearCategoryFilterOption,
        .. TransactionCatalog.ExpenseCategories
            .Concat(TransactionCatalog.IncomeCategories)
            .GroupBy(option => option.Title, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(option => option.Title.Equals("Others", StringComparison.OrdinalIgnoreCase))
            .ThenBy(option => option.Title, StringComparer.OrdinalIgnoreCase)
    ];

    private DateTime displayedMonth = new(
        DateTime.Today.Year,
        DateTime.Today.Month,
        1);
    private IReadOnlyList<TransactionRecord> transactionRecords = [];
    private CurrencyOption? selectedCurrency;
    private TransactionOption? selectedPaymentFilter;
    private TransactionOption? selectedCategoryFilter;
    private IReadOnlyList<SelectableTransactionOption> filterSelectorOptions = [];
    private FilterSelectorKind filterSelectorKind;
    private bool isFilterSelectorOpen;
    private bool isFilterSelectorAnimating;

    public ExpensesView()
    {
        InitializeComponent();
        UpdateMonthSwitcherLabel();
        UpdateFilterChips();
    }

    public void Refresh(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency)
    {
        transactionRecords = records;
        this.selectedCurrency = selectedCurrency;
        RenderDisplayedMonth();
    }

    private async void OnPreviousMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeDisplayedMonthAsync(-1, sender);

    private async void OnNextMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeDisplayedMonthAsync(1, sender);

    private async Task ChangeDisplayedMonthAsync(int monthOffset, object? sender)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        displayedMonth = displayedMonth.AddMonths(monthOffset);
        RenderDisplayedMonth();
        await feedback;
    }

    private async void OnAllFilterTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedPaymentFilter = null;
        selectedCategoryFilter = null;
        RenderDisplayedMonth();
        await feedback;
    }

    private void OnEditTransactionInvoked(object? sender, EventArgs e)
    {
        if (sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            EditTransactionRequested?.Invoke(item.Id);
        }
    }

    private void OnDeleteTransactionInvoked(object? sender, EventArgs e)
    {
        if (sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            DeleteTransactionRequested?.Invoke(item.Id);
        }
    }

    private void OnTransactionRowHandlerChanged(object? sender, EventArgs e)
    {
#if WINDOWS
        if (sender is not Grid row ||
            row.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement nativeRow)
        {
            return;
        }

        var editItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Edit" };
        editItem.Click += (_, _) =>
        {
            if (row.BindingContext is TransactionActivityItem item)
            {
                EditTransactionRequested?.Invoke(item.Id);
            }
        };

        var deleteItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += (_, _) =>
        {
            if (row.BindingContext is TransactionActivityItem item)
            {
                DeleteTransactionRequested?.Invoke(item.Id);
            }
        };

        var flyout = new Microsoft.UI.Xaml.Controls.MenuFlyout();
        flyout.Items.Add(editItem);
        flyout.Items.Add(deleteItem);
        nativeRow.ContextFlyout = flyout;
#endif
    }

    private async void OnPaymentFilterTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await OpenFilterSelectorAsync(
            FilterSelectorKind.PaymentMethod,
            "Filter by payment method",
            PaymentFilterOptions);
        await feedback;
    }

    private async void OnCategoryFilterTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await OpenFilterSelectorAsync(
            FilterSelectorKind.Category,
            "Filter by category",
            CategoryFilterOptions);
        await feedback;
    }

    private async void OnFilterSelectorBackdropTapped(object? sender, TappedEventArgs e) =>
        await CloseFilterSelectorAsync();

    private async void OnFilterSelectorCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseFilterSelectorAsync();
        await feedback;
    }

    private async void OnFilterSelectorSelectionChanged(
        object? sender,
        SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SelectableTransactionOption selectedOption)
        {
            return;
        }

        // Clear the platform selection immediately so iOS does not draw its
        // rectangular selected-cell overlay over the custom rounded state.
        FilterSelectorCollection.SelectedItem = null;

        foreach (var selectorOption in filterSelectorOptions)
        {
            selectorOption.IsSelected = ReferenceEquals(selectorOption, selectedOption);
        }

        var option = selectedOption.Option;

        if (filterSelectorKind == FilterSelectorKind.PaymentMethod)
        {
            selectedPaymentFilter = string.IsNullOrEmpty(option.Key) ? null : option;
        }
        else
        {
            selectedCategoryFilter = string.IsNullOrEmpty(option.Key) ? null : option;
        }

        FilterSelectorCollection.SelectedItem = null;
        RenderDisplayedMonth();
        await CloseFilterSelectorAsync();
    }

    private async Task OpenFilterSelectorAsync(
        FilterSelectorKind kind,
        string title,
        IReadOnlyList<TransactionOption> options)
    {
        if (isFilterSelectorOpen || isFilterSelectorAnimating)
        {
            return;
        }

        filterSelectorKind = kind;
        FilterSelectorTitle.Text = title;
        var selectedKey = kind == FilterSelectorKind.PaymentMethod
            ? selectedPaymentFilter?.Key ?? string.Empty
            : selectedCategoryFilter?.Key ?? string.Empty;
        filterSelectorOptions = options
            .Select(option => new SelectableTransactionOption(
                option,
                option.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        FilterSelectorCollection.ItemsSource = filterSelectorOptions;
        FilterSelectorCollection.SelectedItem = null;
        UpdateFilterSelectorCardBounds();
        isFilterSelectorOpen = true;
        isFilterSelectorAnimating = true;
        FilterSelectorOverlay.IsVisible = true;
        FilterSelectorOverlay.Opacity = 0;
        FilterSelectorCard.Opacity = 0;
        FilterSelectorCard.TranslationY = 24;

        try
        {
            await Task.WhenAll(
                FilterSelectorOverlay.FadeToAsync(1, 150, Easing.CubicOut),
                FilterSelectorCard.FadeToAsync(1, 180, Easing.CubicOut),
                FilterSelectorCard.TranslateToAsync(0, 0, 210, Easing.CubicOut));
        }
        finally
        {
            isFilterSelectorAnimating = false;
        }

        var selectedOption = filterSelectorOptions.FirstOrDefault(option => option.IsSelected);
        if (selectedOption is not null)
        {
            await Task.Delay(40);
            FilterSelectorCollection.ScrollTo(
                selectedOption,
                position: ScrollToPosition.Center,
                animate: false);
        }
    }

    private void OnFilterSelectorOverlaySizeChanged(object? sender, EventArgs e) =>
        UpdateFilterSelectorCardBounds();

    private void UpdateFilterSelectorCardBounds()
    {
        var availableWidth = FilterSelectorOverlay.Width > 0
            ? FilterSelectorOverlay.Width
            : Width;
        var availableHeight = FilterSelectorOverlay.Height > 0
            ? FilterSelectorOverlay.Height
            : Height;

        if (availableWidth > 0)
        {
            FilterSelectorCard.WidthRequest = Math.Min(
                500,
                Math.Max(280, availableWidth - 36));
        }

        if (availableHeight > 0)
        {
            FilterSelectorCard.HeightRequest = Math.Min(
                520,
                Math.Max(320, availableHeight - 36));
        }
    }

    private async Task CloseFilterSelectorAsync()
    {
        if (!isFilterSelectorOpen || isFilterSelectorAnimating)
        {
            return;
        }

        isFilterSelectorAnimating = true;
        try
        {
            await Task.WhenAll(
                FilterSelectorOverlay.FadeToAsync(0, 130, Easing.CubicIn),
                FilterSelectorCard.TranslateToAsync(0, 20, 150, Easing.CubicIn));
        }
        finally
        {
            FilterSelectorOverlay.IsVisible = false;
            FilterSelectorOverlay.Opacity = 0;
            FilterSelectorCard.Opacity = 1;
            FilterSelectorCard.TranslationY = 0;
            isFilterSelectorOpen = false;
            isFilterSelectorAnimating = false;
        }
    }

    private void RenderDisplayedMonth()
    {
        if (selectedCurrency is null)
        {
            return;
        }

        var monthRecords = transactionRecords
            .Where(record =>
                record.TransactionDate.Year == displayedMonth.Year &&
                record.TransactionDate.Month == displayedMonth.Month)
            .ToList();
        var filteredRecords = monthRecords
            .Where(record =>
                selectedPaymentFilter is null ||
                record.PaymentMethod.Equals(
                    selectedPaymentFilter.Key,
                    StringComparison.OrdinalIgnoreCase))
            .Where(record =>
                selectedCategoryFilter is null ||
                record.Category.Equals(
                    selectedCategoryFilter.Key,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        var groups = filteredRecords
            .GroupBy(record => record.TransactionDate.Date)
            .Select(group =>
            {
                var groupRecords = group.ToList();
                var items = groupRecords
                    .Select((record, index) => TransactionActivityItem.FromRecord(
                        record,
                        index < groupRecords.Count - 1))
                    .ToList();
                var netAmountMinor = groupRecords.Sum(record =>
                    record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase)
                        ? record.AmountMinor
                        : -record.AmountMinor);

                return new TransactionActivityGroup(
                    GetDateGroupTitle(group.Key),
                    items,
                    netAmountMinor,
                    selectedCurrency.Symbol);
            })
            .ToList();

        BindableLayout.SetItemsSource(ActivityGroupsLayout, groups);
        ActivityGroupsLayout.IsVisible = groups.Count > 0;
        EmptyActivityState.IsVisible = groups.Count == 0;
        EmptyActivityTitle.Text = selectedPaymentFilter is not null || selectedCategoryFilter is not null
            ? "No transactions match these filters"
            : $"No transactions in {displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture)}";
        TransactionsMonthlySummary.Refresh(
            transactionRecords,
            selectedCurrency,
            displayedMonth);
        UpdateMonthSwitcherLabel();
        UpdateFilterChips();
    }

    private void UpdateFilterChips()
    {
        var filtersAreClear = selectedPaymentFilter is null && selectedCategoryFilter is null;

        PaymentFilterLabel.Text = selectedPaymentFilter?.Title ?? "Payment method";
        PaymentFilterIcon.Source = selectedPaymentFilter?.IconAsset ?? "settings_payment_methods.png";
        CategoryFilterLabel.Text = selectedCategoryFilter?.Title ?? "Category";
        CategoryFilterIcon.Source = selectedCategoryFilter?.IconAsset ?? "settings_categories.png";

        ApplyFilterChipStyle(AllFilterChip, AllFilterLabel, filtersAreClear, true);
        ApplyFilterChipStyle(
            PaymentFilterChip,
            PaymentFilterLabel,
            selectedPaymentFilter is not null,
            false);
        ApplyFilterChipStyle(
            CategoryFilterChip,
            CategoryFilterLabel,
            selectedCategoryFilter is not null,
            false);
    }

    private static void ApplyFilterChipStyle(
        Border chip,
        Label label,
        bool isActive,
        bool useSolidAccent)
    {
        var resources = Application.Current!.Resources;
        var isDark = Application.Current.RequestedTheme == AppTheme.Dark;

        if (isActive && useSolidAccent)
        {
            chip.BackgroundColor = (Color)resources["Accent"];
            chip.Stroke = Brush.Transparent;
            chip.StrokeThickness = 0;
            label.TextColor = (Color)resources["AccentForeground"];
            return;
        }

        chip.BackgroundColor = isActive
            ? (Color)resources["AccentTint"]
            : (Color)resources[isDark ? "CardBackgroundDark" : "CardBackgroundLight"];
        chip.Stroke = new SolidColorBrush((Color)resources[
            isActive ? "Accent" : isDark ? "BorderDark" : "BorderLight"]);
        chip.StrokeThickness = 1.5;
        label.TextColor = (Color)resources[
            isActive ? "Accent" : isDark ? "SecondaryTextDark" : "SecondaryTextLight"];
    }

    private void UpdateMonthSwitcherLabel()
    {
        MonthSwitcherLabel.Text = displayedMonth.ToString(
            "MMMM yyyy",
            CultureInfo.CurrentCulture);
    }

    private static string GetDateGroupTitle(DateTime date) =>
        date.Date switch
        {
            var value when value == DateTime.Today => "TODAY",
            var value when value == DateTime.Today.AddDays(-1) => "YESTERDAY",
            var value => value.ToString("dddd, d MMMM yyyy", CultureInfo.CurrentCulture).ToUpperInvariant()
        };
}
