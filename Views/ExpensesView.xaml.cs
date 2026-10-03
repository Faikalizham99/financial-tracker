using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.ViewModels;

namespace FinancialTracker.Views;

public partial class ExpensesView : ContentView
{
    public event Action<int>? EditTransactionRequested;
    public event Action<int>? QuickEditTransactionRequested;
    public event Action<int>? DeleteTransactionRequested;
    public event Action? SearchRequested;
    public event Action<DateTime>? StatisticsRequested;
    public event EventHandler? TransactionEditingLockToggleRequested;
    public event EventHandler? InvestmentInclusionToggleRequested;
    public Func<DateTime, DateTime, DateTime, Task<DateTime?>>? DatePickerRequested { get; set; }
    public Func<Func<Task>, Task>? RunWithTransactionLoadingAsync { get; set; }
    public Func<DateTime, DateTime, Task<IReadOnlyList<TransactionRecord>>>?
        TransactionsRequested
    { get; set; }

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

    private readonly ExpensesViewModel viewModel = new();
    private DateTime displayedMonth = new(
        DateTime.Today.Year,
        DateTime.Today.Month,
        1);
    private IReadOnlyList<TransactionRecord> transactionRecords = [];
    private CurrencyOption? selectedCurrency;
    private TransactionOption? selectedPaymentFilter;
    private TransactionOption? selectedCategoryFilter;
    private DateTime? selectedStartDate;
    private DateTime? selectedEndDate;
    private DateTime dateRangeStartDraft;
    private DateTime dateRangeEndDraft;
    private IReadOnlyList<SelectableTransactionOption> filterSelectorOptions = [];
    private readonly HashSet<DateTime> collapsedActivityGroupDates = [];
    private readonly HashSet<DateTime> animatingActivityGroupDates = [];
    private readonly HashSet<int> expandedTransactionDescriptionIds = [];
    private FilterSelectorKind filterSelectorKind;
    private bool isFilterSelectorOpen;
    private bool isFilterSelectorAnimating;
    private bool isDateRangeFilterOpen;
    private bool isDateRangeFilterAnimating;
    private int periodLoadVersion;
    private CancellationTokenSource? transactionFocusCancellation;
    private TransactionActivityGroup? stickyActivityGroup;
    private bool isTransactionEditingLocked = true;
    private readonly bool useVirtualizedTransactionList;

    public ExpensesView()
    {
        InitializeComponent();
        useVirtualizedTransactionList =
            DeviceInfo.Current.Platform == DevicePlatform.WinUI;
        ConfigureTransactionList();
        TransactionsMonthlySummary.TransactionEditingLockToggleRequested +=
            OnTransactionEditingLockToggleRequested;
        TransactionsMonthlySummary.InvestmentInclusionToggleRequested +=
            OnInvestmentInclusionToggleRequested;
        TransactionsMonthlySummary.StatisticsRequested += OnStatisticsRequested;
        UpdateMonthSwitcherLabel();
        UpdateFilterChips();
    }

    private void ConfigureTransactionList()
    {
        if (!useVirtualizedTransactionList)
        {
            TransactionsScrollView.IsVisible = true;
            TransactionsCollectionView.IsVisible = false;
            return;
        }

        TransactionScrollContent.Children.Remove(TransactionHeaderContent);
        TransactionsCollectionView.Header = TransactionHeaderContent;
        TransactionsScrollView.IsVisible = false;
        TransactionsCollectionView.IsVisible = true;
    }

    public void Refresh(
        IReadOnlyList<TransactionRecord> records,
        CurrencyOption selectedCurrency)
    {
        periodLoadVersion++;
        transactionRecords = records;
        var validTransactionIds = records.Select(record => record.Id).ToHashSet();
        expandedTransactionDescriptionIds.RemoveWhere(
            id => !validTransactionIds.Contains(id));
        this.selectedCurrency = selectedCurrency;
        RenderDisplayedMonth();
    }

    public void SetCurrency(CurrencyOption currency)
    {
        selectedCurrency = currency;
        RenderDisplayedMonth();
    }

    public void SetTransactionEditingLocked(bool isLocked)
    {
        if (isTransactionEditingLocked == isLocked)
        {
            return;
        }

        isTransactionEditingLocked = isLocked;
        TransactionsMonthlySummary.IsTransactionEditingLocked = isLocked;
        RenderDisplayedMonth();
        Dispatcher.Dispatch(UpdateTransactionContextMenus);
    }

    private void OnTransactionEditingLockToggleRequested(object? sender, EventArgs e) =>
        TransactionEditingLockToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnInvestmentInclusionToggleRequested(object? sender, EventArgs e) =>
        InvestmentInclusionToggleRequested?.Invoke(this, EventArgs.Empty);

    private void OnStatisticsRequested(object? sender, EventArgs e) =>
        StatisticsRequested?.Invoke(displayedMonth);

    public void SetIncludeInvestmentInTotals(bool includeInvestment) =>
        TransactionsMonthlySummary.IncludeInvestmentInTotals = includeInvestment;

    public void SetBudgetProvider(
        Func<DateTime, Task<MonthlyBudgetRecord?>> provider) =>
        TransactionsMonthlySummary.BudgetProvider = provider;

    public Task ReloadBudgetAsync() => TransactionsMonthlySummary.ReloadBudgetAsync();

    public Task ReloadTransactionsAsync() => LoadDisplayedPeriodAsync();

    public Task RefreshTransactionsAsync(
        IReadOnlyList<TransactionRecord> currentMonthRecords,
        DateTime currentMonth,
        CurrencyOption currency)
    {
        selectedCurrency = currency;
        if (selectedStartDate is null &&
            selectedEndDate is null &&
            displayedMonth.Year == currentMonth.Year &&
            displayedMonth.Month == currentMonth.Month)
        {
            Refresh(currentMonthRecords, currency);

            return Task.CompletedTask;
        }

        return LoadDisplayedPeriodAsync();
    }

    public Task FocusTransactionAsync(
        int transactionId,
        DateTime transactionDate,
        Func<Task>? revealTargetAsync = null) =>
        useVirtualizedTransactionList
            ? FocusVirtualizedTransactionAsync(
                transactionId,
                transactionDate,
                revealTargetAsync)
            : FocusStackedTransactionAsync(
                transactionId,
                transactionDate,
                revealTargetAsync);

    private async void OnPreviousMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeDisplayedMonthAsync(-1, sender);

    private async void OnNextMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeDisplayedMonthAsync(1, sender);

    private async void OnActivityGroupHeaderTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not TransactionActivityGroup group ||
            !animatingActivityGroupDates.Add(group.Date))
        {
            return;
        }

        var body = FindActivityGroupBody(group);
        try
        {
            if (group.IsExpanded)
            {
                if (body is not null)
                {
                    await body.FadeToAsync(0.25, 85, Easing.CubicIn);
                }

                collapsedActivityGroupDates.Add(group.Date);
                group.IsExpanded = false;
            }
            else
            {
                collapsedActivityGroupDates.Remove(group.Date);
                group.IsExpanded = true;
                if (body is not null)
                {
                    body.Opacity = 0;
                    body.TranslationY = -5;
                    await Task.WhenAll(
                        body.FadeToAsync(1, 145, Easing.CubicOut),
                        body.TranslateToAsync(0, 0, 155, Easing.CubicOut));
                }
            }
        }
        finally
        {
            if (body is not null)
            {
                body.Opacity = 1;
                body.TranslationY = 0;
            }

            animatingActivityGroupDates.Remove(group.Date);
            Dispatcher.Dispatch(UpdateStickyActivityHeader);
        }
    }

    private VisualElement? FindActivityGroupBody(TransactionActivityGroup group)
    {
        var groupView = FindRealizedActivityGroupView(group);
        return groupView?.Children
            .OfType<VisualElement>()
            .FirstOrDefault(view => view.ClassId == "TransactionActivityGroupBody");
    }

    private void OnTransactionDescriptionToggled(
        object? sender,
        TransactionDescriptionToggledEventArgs e)
    {
        if (e.IsExpanded)
        {
            expandedTransactionDescriptionIds.Add(e.Item.Id);
        }
        else
        {
            expandedTransactionDescriptionIds.Remove(e.Item.Id);
        }

        Dispatcher.Dispatch(UpdateStickyActivityHeader);
    }


    private async Task ChangeDisplayedMonthAsync(int monthOffset, object? sender)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        displayedMonth = displayedMonth.AddMonths(monthOffset);
        selectedStartDate = null;
        selectedEndDate = null;
        await ReloadDisplayedPeriodWithLoadingAsync();
        await feedback;
    }

    private async void OnAllFilterTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedPaymentFilter = null;
        selectedCategoryFilter = null;
        selectedStartDate = null;
        selectedEndDate = null;
        await ReloadDisplayedPeriodWithLoadingAsync();
        await feedback;
    }

    private async void OnSearchTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        SearchRequested?.Invoke();
        await feedback;
    }

    private async void OnCalendarFilterTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await OpenDateRangeFilterAsync();
        await feedback;
    }

    private async Task OpenDateRangeFilterAsync()
    {
        if (isDateRangeFilterOpen || isDateRangeFilterAnimating || isFilterSelectorOpen)
        {
            return;
        }

        var monthStart = displayedMonth.Date;
        var monthEnd = displayedMonth.AddMonths(1).AddDays(-1).Date;
        dateRangeStartDraft = selectedStartDate ?? monthStart;
        dateRangeEndDraft = selectedEndDate ?? monthEnd;
        DateRangeMonthLabel.Text = "Choose any start and end date";
        UpdateDateRangeLabels();

        isDateRangeFilterOpen = true;
        isDateRangeFilterAnimating = true;
        DateRangeFilterOverlay.IsVisible = true;
        DateRangeFilterOverlay.Opacity = 0;
        DateRangeFilterCard.Opacity = 0;
        DateRangeFilterCard.TranslationY = 24;

        try
        {
            await Task.WhenAll(
                DateRangeFilterOverlay.FadeToAsync(1, 150, Easing.CubicOut),
                DateRangeFilterCard.FadeToAsync(1, 180, Easing.CubicOut),
                DateRangeFilterCard.TranslateToAsync(0, 0, 210, Easing.CubicOut));
        }
        finally
        {
            isDateRangeFilterAnimating = false;
        }
    }

    private async void OnStartDateTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        var selectedDate = DatePickerRequested is null
            ? null
            : await DatePickerRequested(
                dateRangeStartDraft,
                DateRangeLimits.MinimumDate,
                DateRangeLimits.MaximumDate);
        if (selectedDate is not null)
        {
            dateRangeStartDraft = selectedDate.Value.Date;
            if (dateRangeEndDraft < dateRangeStartDraft)
            {
                dateRangeEndDraft = dateRangeStartDraft;
            }

            UpdateDateRangeLabels();
        }

        await feedback;
    }

    private async void OnEndDateTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        var selectedDate = DatePickerRequested is null
            ? null
            : await DatePickerRequested(
                dateRangeEndDraft,
                DateRangeLimits.MinimumDate,
                DateRangeLimits.MaximumDate);
        if (selectedDate is not null)
        {
            dateRangeEndDraft = selectedDate.Value.Date;
            if (dateRangeStartDraft > dateRangeEndDraft)
            {
                dateRangeStartDraft = dateRangeEndDraft;
            }

            UpdateDateRangeLabels();
        }

        await feedback;
    }

    private void UpdateDateRangeLabels()
    {
        StartDateLabel.Text = dateRangeStartDraft.ToString(
            "dddd, d MMMM yyyy",
            CultureInfo.CurrentCulture);
        EndDateLabel.Text = dateRangeEndDraft.ToString(
            "dddd, d MMMM yyyy",
            CultureInfo.CurrentCulture);
    }

    private async void OnDateRangeApplyTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedStartDate = dateRangeStartDraft;
        selectedEndDate = dateRangeEndDraft;
        await CloseDateRangeFilterAsync();
        await ReloadDisplayedPeriodWithLoadingAsync();
        await feedback;
    }

    private async void OnDateRangeClearTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedStartDate = null;
        selectedEndDate = null;
        await CloseDateRangeFilterAsync();
        await ReloadDisplayedPeriodWithLoadingAsync();
        await feedback;
    }

    private async void OnDateRangeBackdropTapped(object? sender, TappedEventArgs e) =>
        await CloseDateRangeFilterAsync();

    private async void OnDateRangeCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseDateRangeFilterAsync();
        await feedback;
    }

    private async Task CloseDateRangeFilterAsync()
    {
        if (!isDateRangeFilterOpen || isDateRangeFilterAnimating)
        {
            return;
        }

        isDateRangeFilterAnimating = true;
        try
        {
            await Task.WhenAll(
                DateRangeFilterOverlay.FadeToAsync(0, 130, Easing.CubicIn),
                DateRangeFilterCard.TranslateToAsync(0, 20, 150, Easing.CubicIn));
        }
        finally
        {
            DateRangeFilterOverlay.IsVisible = false;
            DateRangeFilterOverlay.Opacity = 0;
            DateRangeFilterCard.Opacity = 1;
            DateRangeFilterCard.TranslationY = 0;
            isDateRangeFilterOpen = false;
            isDateRangeFilterAnimating = false;
        }
    }

    private void OnEditTransactionInvoked(object? sender, EventArgs e)
    {
        _ = InteractionAnimations.PulseAsync(sender);
        if (!isTransactionEditingLocked &&
            sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            EditTransactionRequested?.Invoke(item.Id);
        }
    }

    private void OnTransactionDoubleTapEditRequested(
        object? sender,
        TransactionEditRequestedEventArgs e) =>
        QuickEditTransactionRequested?.Invoke(e.Item.Id);

    private void OnDeleteTransactionInvoked(object? sender, EventArgs e)
    {
        _ = InteractionAnimations.PulseAsync(sender);
        if (!isTransactionEditingLocked &&
            sender is SwipeItemView { BindingContext: TransactionActivityItem item })
        {
            DeleteTransactionRequested?.Invoke(item.Id);
        }
    }

    private void OnTransactionRowHandlerChanged(object? sender, EventArgs e)
    {
        if (sender is Grid row)
        {
            ConfigureTransactionContextMenu(row);
        }
    }

    private void UpdateTransactionContextMenus()
    {
        var transactionList = useVirtualizedTransactionList
            ? (VisualElement)TransactionsCollectionView
            : ActivityGroupsLayout;
        foreach (var row in transactionList
            .GetVisualTreeDescendants()
            .OfType<Grid>()
            .Where(view => view.ClassId == "TransactionContextMenuTarget"))
        {
            ConfigureTransactionContextMenu(row);
        }
    }

    private void ConfigureTransactionContextMenu(Grid row)
    {
#if WINDOWS
        if (row.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement nativeRow)
        {
            return;
        }

        nativeRow.ContextFlyout = null;
        if (isTransactionEditingLocked)
        {
            return;
        }

        var editItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Edit" };
        editItem.Click += (_, _) =>
        {
            if (!isTransactionEditingLocked &&
                row.BindingContext is TransactionActivityItem item)
            {
                EditTransactionRequested?.Invoke(item.Id);
            }
        };

        var deleteItem = new Microsoft.UI.Xaml.Controls.MenuFlyoutItem { Text = "Delete" };
        deleteItem.Click += (_, _) =>
        {
            if (!isTransactionEditingLocked &&
                row.BindingContext is TransactionActivityItem item)
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

    private async void OnFilterSelectorOptionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not SelectableTransactionOption selectedOption)
        {
            return;
        }

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

        await CloseFilterSelectorAsync();
        await RenderDisplayedMonthWithLoadingAsync();
    }

    private Task RenderDisplayedMonthWithLoadingAsync()
    {
        var loadingHandler = RunWithTransactionLoadingAsync;
        if (loadingHandler is null)
        {
            RenderDisplayedMonth();
            return Task.CompletedTask;
        }

        return loadingHandler(() =>
        {
            RenderDisplayedMonth();
            return Task.CompletedTask;
        });
    }

    private Task ReloadDisplayedPeriodWithLoadingAsync()
    {
        var loadingHandler = RunWithTransactionLoadingAsync;
        return loadingHandler is null
            ? LoadDisplayedPeriodAsync()
            : loadingHandler(() => LoadDisplayedPeriodAsync());
    }

    private async Task LoadDisplayedPeriodAsync(bool cancelPendingFocus = true)
    {
        var provider = TransactionsRequested;
        if (provider is null)
        {
            RenderDisplayedMonth(cancelPendingFocus);
            return;
        }

        var startDate = selectedStartDate?.Date ?? displayedMonth.Date;
        var endDateExclusive = selectedEndDate?.Date.AddDays(1) ??
            displayedMonth.AddMonths(1).Date;
        var requestVersion = ++periodLoadVersion;
        var records = await provider(startDate, endDateExclusive);
        if (requestVersion != periodLoadVersion)
        {
            return;
        }

        transactionRecords = records;
        var validTransactionIds = transactionRecords
            .Select(record => record.Id)
            .ToHashSet();
        expandedTransactionDescriptionIds.RemoveWhere(
            id => !validTransactionIds.Contains(id));
        RenderDisplayedMonth(cancelPendingFocus);
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

    private void RenderDisplayedMonth(bool cancelPendingFocus = true)
    {
        if (cancelPendingFocus)
        {
            CancelTransactionFocusAnimation();
        }

        if (selectedCurrency is null)
        {
            return;
        }

        var presentation = viewModel.BuildPresentation(
            transactionRecords,
            selectedCurrency,
            displayedMonth,
            selectedPaymentFilter,
            selectedCategoryFilter,
            selectedStartDate,
            selectedEndDate,
            expandedTransactionDescriptionIds,
            collapsedActivityGroupDates,
            canModifyTransactions: !isTransactionEditingLocked);

        HideStickyActivityHeader();
        SetActivityGroups(presentation.Groups);
        EmptyActivityState.IsVisible = presentation.Groups.Count == 0;
        EmptyActivityTitle.Text = presentation.EmptyTitle;
        if (presentation.HasDateRange)
        {
            TransactionsMonthlySummary.RefreshRange(
                presentation.FilteredRecords,
                selectedCurrency,
                selectedStartDate!.Value,
                selectedEndDate!.Value,
                presentation.HasSummaryScopeFilters);
        }
        else
        {
            TransactionsMonthlySummary.Refresh(
                presentation.FilteredRecords,
                selectedCurrency,
                displayedMonth,
                presentation.HasSummaryScopeFilters);
        }
        MonthSwitcherLabel.Text = presentation.PeriodLabel;
        UpdateFilterChips();
        Dispatcher.Dispatch(UpdateStickyActivityHeader);
        Dispatcher.Dispatch(UpdateTransactionContextMenus);
    }

    private void UpdateFilterChips()
    {
        var hasPaymentFilter = selectedPaymentFilter is not null;
        var hasCategoryFilter = selectedCategoryFilter is not null;
        var hasDateFilter = selectedStartDate is not null && selectedEndDate is not null;

        PaymentFilterValueLabel.Text = selectedPaymentFilter?.Title ?? "Any payment method";
        PaymentFilterIcon.Source = selectedPaymentFilter?.IconAsset ?? "settings_payment_methods.png";
        CategoryFilterValueLabel.Text = selectedCategoryFilter?.Title ?? "Any category";
        CategoryFilterIcon.Source = selectedCategoryFilter?.IconAsset ?? "settings_categories.png";
        ClearAllFiltersButton.IsVisible = hasPaymentFilter || hasCategoryFilter || hasDateFilter;

        ApplyFilterRowStyle(PaymentFilterRow, PaymentFilterValueLabel, hasPaymentFilter);
        ApplyFilterRowStyle(CategoryFilterRow, CategoryFilterValueLabel, hasCategoryFilter);
        ApplyCalendarFilterStyle(hasDateFilter);
    }

    private void ApplyCalendarFilterStyle(bool isActive)
    {
        if (isActive)
        {
            ThemeResourceBindings.SetDynamic(
                CalendarFilterButton,
                Border.BackgroundColorProperty,
                "AccentTint");
            ThemeResourceBindings.SetDynamic(
                CalendarFilterButton,
                Border.StrokeProperty,
                "Accent");
            ThemeResourceBindings.SetDynamic(
                CalendarFilterIcon,
                Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty,
                "Accent");
        }
        else
        {
            ThemeResourceBindings.SetColor(
                CalendarFilterButton,
                Border.BackgroundColorProperty,
                "CardBackgroundLight",
                "CardBackgroundDark");
            ThemeResourceBindings.SetBrush(
                CalendarFilterButton,
                Border.StrokeProperty,
                "BorderLight",
                "BorderDark");
            ThemeResourceBindings.SetBrush(
                CalendarFilterIcon,
                Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty,
                "PrimaryTextLight",
                "PrimaryTextDark");
        }

        CalendarFilterActiveDot.IsVisible = isActive;
    }

    private static void ApplyFilterRowStyle(
        Grid row,
        Label label,
        bool isActive)
    {
        if (isActive)
        {
            ThemeResourceBindings.SetDynamic(
                row,
                BackgroundColorProperty,
                "AccentTint");
            ThemeResourceBindings.SetDynamic(
                label,
                Label.TextColorProperty,
                "Accent");
            return;
        }

        ThemeResourceBindings.SetStatic(
            row,
            BackgroundColorProperty,
            Colors.Transparent);
        ThemeResourceBindings.SetColor(
            label,
            Label.TextColorProperty,
            "PrimaryTextLight",
            "PrimaryTextDark");
    }

    private void UpdateMonthSwitcherLabel()
    {
        MonthSwitcherLabel.Text = selectedStartDate is not null && selectedEndDate is not null
            ? CompactDateRangeFormatter.Format(selectedStartDate.Value, selectedEndDate.Value)
            : displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    }

}
