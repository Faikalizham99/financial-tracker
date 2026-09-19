namespace FinancialTracker.Views;

using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using Microsoft.Maui;

public partial class ExpensesView : ContentView
{
    public event Action<int>? EditTransactionRequested;
    public event Action<int>? DeleteTransactionRequested;
    public event Action? SearchRequested;

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
    private DateTime? selectedStartDate;
    private DateTime? selectedEndDate;
    private IReadOnlyList<SelectableTransactionOption> filterSelectorOptions = [];
    private readonly HashSet<DateTime> collapsedActivityGroupDates = [];
    private readonly HashSet<DateTime> animatingActivityGroupDates = [];
    private readonly HashSet<int> expandedTransactionDescriptionIds = [];
    private readonly HashSet<int> animatingTransactionDescriptionIds = [];
    private FilterSelectorKind filterSelectorKind;
    private bool isFilterSelectorOpen;
    private bool isFilterSelectorAnimating;
    private bool isDateRangeFilterOpen;
    private bool isDateRangeFilterAnimating;
    private CancellationTokenSource? transactionFocusCancellation;
    private CancellationTokenSource? descriptionMeasurementCancellation;

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
        var validTransactionIds = records.Select(record => record.Id).ToHashSet();
        expandedTransactionDescriptionIds.RemoveWhere(
            id => !validTransactionIds.Contains(id));
        this.selectedCurrency = selectedCurrency;
        RenderDisplayedMonth();
    }

    public async Task FocusTransactionAsync(int transactionId)
    {
        CancelTransactionFocusAnimation();
        var focusCancellation = new CancellationTokenSource();
        transactionFocusCancellation = focusCancellation;
        var cancellationToken = focusCancellation.Token;
        var transaction = transactionRecords.FirstOrDefault(record => record.Id == transactionId);
        if (transaction is null)
        {
            transactionFocusCancellation = null;
            focusCancellation.Dispose();
            return;
        }

        displayedMonth = new DateTime(
            transaction.TransactionDate.Year,
            transaction.TransactionDate.Month,
            1);
        selectedPaymentFilter = null;
        selectedCategoryFilter = null;
        selectedStartDate = null;
        selectedEndDate = null;
        collapsedActivityGroupDates.Remove(transaction.TransactionDate.Date);
        RenderDisplayedMonth(cancelPendingFocus: false);

        BoxView? highlight = null;
        for (var attempt = 0; attempt < 20 && highlight is null; attempt++)
        {
            await Task.Delay(50);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            highlight = ActivityGroupsLayout
                .GetVisualTreeDescendants()
                .OfType<BoxView>()
                .FirstOrDefault(view =>
                    view.ClassId == "TransactionSearchHighlight" &&
                    view.BindingContext is TransactionActivityItem item &&
                    item.Id == transactionId);
        }

        if (highlight is null)
        {
            if (ReferenceEquals(transactionFocusCancellation, focusCancellation))
            {
                transactionFocusCancellation = null;
            }

            focusCancellation.Dispose();

            return;
        }

        try
        {
            await Task.Delay(80, cancellationToken);
            var targetOffset = GetVerticalOffsetWithinScrollContent(highlight);
            if (targetOffset >= 0)
            {
                var visibleTop = TransactionsScrollView.ScrollY;
                var visibleBottom = visibleTop + TransactionsScrollView.Height - 90;
                var targetBottom = targetOffset + highlight.Height;
                var isAlreadyVisible = targetOffset >= visibleTop && targetBottom <= visibleBottom;
                if (!isAlreadyVisible)
                {
                    var centeredOffset = Math.Max(
                        0,
                        targetOffset - ((TransactionsScrollView.Height - highlight.Height) / 2));
                    var scrollTask = TransactionsScrollView.ScrollToAsync(
                        0,
                        centeredOffset,
                        animated: true);
                    await Task.WhenAny(
                        scrollTask,
                        Task.Delay(650, cancellationToken));
                }
            }
            else
            {
                var scrollTask = TransactionsScrollView.ScrollToAsync(
                    highlight,
                    ScrollToPosition.Center,
                    animated: true);
                await Task.WhenAny(
                    scrollTask,
                    Task.Delay(650, cancellationToken));
            }

            cancellationToken.ThrowIfCancellationRequested();
            highlight.CancelAnimations();
            highlight.Opacity = 0;
            for (var pulse = 0; pulse < 3; pulse++)
            {
                await highlight.FadeToAsync(0.9, 250, Easing.CubicOut);
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken);
                await highlight.FadeToAsync(0, 250, Easing.CubicIn);
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Delay(250, cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            highlight.CancelAnimations();
            highlight.Opacity = 0;
            if (ReferenceEquals(transactionFocusCancellation, focusCancellation))
            {
                transactionFocusCancellation = null;
            }

            focusCancellation.Dispose();
        }
    }

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
        var groupView = ActivityGroupsLayout.Children
            .OfType<VerticalStackLayout>()
            .FirstOrDefault(view => ReferenceEquals(view.BindingContext, group));
        return groupView?.Children
            .OfType<VisualElement>()
            .FirstOrDefault(view => view.ClassId == "TransactionActivityGroupBody");
    }

    private void OnTransactionsScrolled(object? sender, ScrolledEventArgs e) =>
        UpdateStickyActivityHeader(e.ScrollY);

    private void OnTransactionDescriptionSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Label label)
        {
            UpdateDescriptionExpandability(label);
        }
    }

    private void UpdateDescriptionExpandability(Label label)
    {
        if (label.BindingContext is not TransactionActivityItem item ||
            label.Width <= 0)
        {
            return;
        }

        var measuredTextWidth = label
            .Measure(double.PositiveInfinity, double.PositiveInfinity)
            .Width;
        var estimatedTextWidth = EstimateSingleLineTextWidth(
            item.Description,
            label.FontSize);
        var fullTextWidth = Math.Max(measuredTextWidth, estimatedTextWidth);
        var canExpand = fullTextWidth > label.Width + 1;
        item.SetDescriptionExpandable(canExpand);
        if (!canExpand)
        {
            expandedTransactionDescriptionIds.Remove(item.Id);
        }
    }

    private void ScheduleDescriptionMeasurements()
    {
        CancelDescriptionMeasurements();
        var cancellation = new CancellationTokenSource();
        descriptionMeasurementCancellation = cancellation;
        _ = MeasureVisibleDescriptionsAfterLayoutAsync(cancellation);
    }

    private async Task MeasureVisibleDescriptionsAfterLayoutAsync(
        CancellationTokenSource cancellation)
    {
        try
        {
            // iOS can assign final BindableLayout child widths over multiple layout passes.
            foreach (var delay in new[] { 0, 50, 150 })
            {
                if (delay > 0)
                {
                    await Task.Delay(delay, cancellation.Token);
                }

                cancellation.Token.ThrowIfCancellationRequested();
                Dispatcher.Dispatch(RecalculateVisibleDescriptionExpandability);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer transaction refresh owns the current visual tree.
        }
        finally
        {
            if (ReferenceEquals(descriptionMeasurementCancellation, cancellation))
            {
                descriptionMeasurementCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void RecalculateVisibleDescriptionExpandability()
    {
        foreach (var label in ActivityGroupsLayout
            .GetVisualTreeDescendants()
            .OfType<Label>()
            .Where(candidate =>
                candidate.ClassId == "ExpandableTransactionDescription"))
        {
            UpdateDescriptionExpandability(label);
        }
    }

    private void CancelDescriptionMeasurements()
    {
        var pendingCancellation = descriptionMeasurementCancellation;
        descriptionMeasurementCancellation = null;
        if (pendingCancellation is null)
        {
            return;
        }

        pendingCancellation.Cancel();
    }

    private static double EstimateSingleLineTextWidth(
        string text,
        double fontSize)
    {
        var emWidth = 0d;
        foreach (var character in text)
        {
            emWidth += character switch
            {
                _ when char.IsWhiteSpace(character) => 0.33,
                'i' or 'l' or 'I' or '1' or '|' or '!' or '.' or ',' or ':' or ';' or '\'' => 0.3,
                'm' or 'w' or 'M' or 'W' or '@' or '#' or '%' or '&' => 0.85,
                _ when char.IsUpper(character) => 0.64,
                _ => 0.55
            };
        }

        return emWidth * fontSize;
    }

    private async void OnTransactionDescriptionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not TransactionActivityItem item ||
            !item.CanExpandDescription ||
            !animatingTransactionDescriptionIds.Add(item.Id))
        {
            return;
        }

        var descriptionContainer = (sender as TapGestureRecognizer)?.Parent as Grid;
        try
        {
            if (descriptionContainer is not null)
            {
                await descriptionContainer.FadeToAsync(0.58, 55, Easing.CubicIn);
            }

            var shouldExpand = !item.IsDescriptionExpanded;
            item.SetDescriptionExpanded(shouldExpand);
            if (shouldExpand)
            {
                expandedTransactionDescriptionIds.Add(item.Id);
            }
            else
            {
                expandedTransactionDescriptionIds.Remove(item.Id);
            }

            if (descriptionContainer is not null)
            {
                await descriptionContainer.FadeToAsync(1, 115, Easing.CubicOut);
            }
        }
        finally
        {
            if (descriptionContainer is not null)
            {
                descriptionContainer.Opacity = 1;
            }

            animatingTransactionDescriptionIds.Remove(item.Id);
            Dispatcher.Dispatch(UpdateStickyActivityHeader);
        }
    }

    private void OnActivityGroupsLayoutSizeChanged(object? sender, EventArgs e)
    {
        UpdateStickyActivityHeader();
        ScheduleDescriptionMeasurements();
    }

    private void UpdateStickyActivityHeader() =>
        UpdateStickyActivityHeader(TransactionsScrollView.ScrollY);

    private void UpdateStickyActivityHeader(double scrollY)
    {
        if (!ActivityGroupsLayout.IsVisible)
        {
            HideStickyActivityHeader();
            return;
        }

        var groupViews = ActivityGroupsLayout.Children
            .OfType<VerticalStackLayout>()
            .Where(view => view.BindingContext is TransactionActivityGroup)
            .ToList();
        var stickyTopInset = StickyActivityHeader.Margin.Top;
        var activeIndex = -1;
        var groupOffsets = new double[groupViews.Count];

        for (var index = 0; index < groupViews.Count; index++)
        {
            var offset = GetVerticalOffsetWithinScrollContent(groupViews[index]);
            groupOffsets[index] = offset;
            if (offset >= 0 && offset <= scrollY + stickyTopInset)
            {
                activeIndex = index;
            }
        }

        if (groupOffsets.Length == 0 || groupOffsets[0] <= 1)
        {
            HideStickyActivityHeader();
            return;
        }

        if (activeIndex < 0 ||
            groupViews[activeIndex].BindingContext is not TransactionActivityGroup activeGroup)
        {
            HideStickyActivityHeader();
            return;
        }

        StickyActivityHeader.BindingContext = activeGroup;
        StickyActivityHeader.IsVisible = true;
        var stickyHeight = Math.Max(StickyActivityHeader.Height, 42);
        var translationY = 0d;
        if (activeIndex + 1 < groupOffsets.Length)
        {
            var nextHeaderTop = groupOffsets[activeIndex + 1] - scrollY - stickyTopInset;
            if (nextHeaderTop < stickyHeight)
            {
                translationY = Math.Min(0, nextHeaderTop - stickyHeight);
            }
        }

        StickyActivityHeader.TranslationY = translationY;
    }

    private void HideStickyActivityHeader()
    {
        StickyActivityHeader.IsVisible = false;
        StickyActivityHeader.TranslationY = 0;
        StickyActivityHeader.BindingContext = null;
    }

    private async Task ChangeDisplayedMonthAsync(int monthOffset, object? sender)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        displayedMonth = displayedMonth.AddMonths(monthOffset);
        selectedStartDate = null;
        selectedEndDate = null;
        RenderDisplayedMonth();
        await feedback;
    }

    private async void OnAllFilterTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedPaymentFilter = null;
        selectedCategoryFilter = null;
        selectedStartDate = null;
        selectedEndDate = null;
        RenderDisplayedMonth();
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
        ConfigureDatePicker(
            StartDatePicker,
            selectedStartDate ?? monthStart,
            monthStart,
            monthEnd);
        ConfigureDatePicker(
            EndDatePicker,
            selectedEndDate ?? monthEnd,
            monthStart,
            monthEnd);
        DateRangeMonthLabel.Text = $"Choose dates in {displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture)}";
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

    private static void ConfigureDatePicker(
        DatePicker picker,
        DateTime date,
        DateTime minimumDate,
        DateTime maximumDate)
    {
        picker.MinimumDate = new DateTime(2000, 1, 1);
        picker.MaximumDate = DateTime.Today.AddYears(10);
        picker.Date = date;
        picker.MinimumDate = minimumDate;
        picker.MaximumDate = maximumDate;
    }

    private async void OnStartDateTapped(object? sender, TappedEventArgs e) =>
        await OpenNativeDatePickerAsync(StartDatePicker, sender);

    private async void OnEndDateTapped(object? sender, TappedEventArgs e) =>
        await OpenNativeDatePickerAsync(EndDatePicker, sender);

    private static async Task OpenNativeDatePickerAsync(DatePicker picker, object? sender)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        picker.IsOpen = true;

        await feedback;
    }

    private void OnStartDateSelected(object? sender, DateChangedEventArgs e)
    {
        var selectedDate = (e.NewDate ?? displayedMonth).Date;
        if ((EndDatePicker.Date ?? selectedDate).Date < selectedDate)
        {
            EndDatePicker.Date = selectedDate;
        }

        UpdateDateRangeLabels();
    }

    private void OnEndDateSelected(object? sender, DateChangedEventArgs e)
    {
        var selectedDate = (e.NewDate ?? displayedMonth).Date;
        if ((StartDatePicker.Date ?? selectedDate).Date > selectedDate)
        {
            StartDatePicker.Date = selectedDate;
        }

        UpdateDateRangeLabels();
    }

    private void UpdateDateRangeLabels()
    {
        StartDateLabel.Text = (StartDatePicker.Date ?? displayedMonth).ToString(
            "dddd, d MMMM yyyy",
            CultureInfo.CurrentCulture);
        EndDateLabel.Text = (EndDatePicker.Date ?? displayedMonth).ToString(
            "dddd, d MMMM yyyy",
            CultureInfo.CurrentCulture);
    }

    private async void OnDateRangeApplyTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedStartDate = (StartDatePicker.Date ?? displayedMonth).Date;
        selectedEndDate = (EndDatePicker.Date ?? selectedStartDate.Value).Date;
        RenderDisplayedMonth();
        await CloseDateRangeFilterAsync();
        await feedback;
    }

    private async void OnDateRangeClearTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedStartDate = null;
        selectedEndDate = null;
        RenderDisplayedMonth();
        await CloseDateRangeFilterAsync();
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
        StartDatePicker.Unfocus();
        EndDatePicker.Unfocus();
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

    private double GetVerticalOffsetWithinScrollContent(VisualElement target)
    {
        var scrollContent = TransactionsScrollView.Content;
        if (scrollContent is null)
        {
            return -1;
        }

        double offset = 0;
        Element? current = target;
        while (current is VisualElement visual && !ReferenceEquals(current, scrollContent))
        {
            offset += visual.Y;
            current = visual.Parent;
        }

        return ReferenceEquals(current, scrollContent) ? offset : -1;
    }

    private void CancelTransactionFocusAnimation()
    {
        var cancellation = transactionFocusCancellation;
        transactionFocusCancellation = null;
        cancellation?.Cancel();
        foreach (var highlight in ActivityGroupsLayout
                     .GetVisualTreeDescendants()
                     .OfType<BoxView>()
                     .Where(view => view.ClassId == "TransactionSearchHighlight"))
        {
            highlight.CancelAnimations();
            highlight.Opacity = 0;
        }
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
            .Where(record =>
                selectedStartDate is null ||
                record.TransactionDate.Date >= selectedStartDate.Value)
            .Where(record =>
                selectedEndDate is null ||
                record.TransactionDate.Date <= selectedEndDate.Value)
            .ToList();
        var groups = filteredRecords
            .GroupBy(record => record.TransactionDate.Date)
            .Select(group =>
            {
                var groupRecords = group.ToList();
                var items = groupRecords
                    .Select((record, index) => TransactionActivityItem.FromRecord(
                        record,
                        index < groupRecords.Count - 1,
                        expandedTransactionDescriptionIds.Contains(record.Id)))
                    .ToList();
                var netAmountMinor = groupRecords.Sum(record =>
                    record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase)
                        ? record.AmountMinor
                        : -record.AmountMinor);

                return new TransactionActivityGroup(
                    group.Key,
                    GetDateGroupTitle(group.Key),
                    items,
                    netAmountMinor,
                    selectedCurrency.Symbol,
                    !collapsedActivityGroupDates.Contains(group.Key));
            })
            .ToList();

        HideStickyActivityHeader();
        BindableLayout.SetItemsSource(ActivityGroupsLayout, groups);
        ActivityGroupsLayout.IsVisible = groups.Count > 0;
        EmptyActivityState.IsVisible = groups.Count == 0;
        EmptyActivityTitle.Text = selectedPaymentFilter is not null ||
            selectedCategoryFilter is not null ||
            selectedStartDate is not null
            ? "No transactions match these filters"
            : $"No transactions in {displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture)}";
        TransactionsMonthlySummary.Refresh(
            transactionRecords,
            selectedCurrency,
            displayedMonth);
        UpdateMonthSwitcherLabel();
        UpdateFilterChips();
        ScheduleDescriptionMeasurements();
        Dispatcher.Dispatch(UpdateStickyActivityHeader);
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
        var resources = Application.Current!.Resources;
        var isDark = Application.Current.RequestedTheme == AppTheme.Dark;
        CalendarFilterButton.BackgroundColor = (Color)resources[
            isActive ? "AccentTint" : isDark ? "CardBackgroundDark" : "CardBackgroundLight"];
        CalendarFilterButton.Stroke = new SolidColorBrush((Color)resources[
            isActive ? "Accent" : isDark ? "BorderDark" : "BorderLight"]);
        CalendarFilterIcon.Stroke = new SolidColorBrush((Color)resources[
            isActive ? "Accent" : isDark ? "PrimaryTextDark" : "PrimaryTextLight"]);
        CalendarFilterActiveDot.IsVisible = isActive;
    }

    private static void ApplyFilterRowStyle(
        Grid row,
        Label label,
        bool isActive)
    {
        var resources = Application.Current!.Resources;
        var isDark = Application.Current.RequestedTheme == AppTheme.Dark;

        row.BackgroundColor = isActive
            ? (Color)resources["AccentTint"]
            : Colors.Transparent;
        label.TextColor = (Color)resources[
            isActive ? "Accent" : isDark ? "PrimaryTextDark" : "PrimaryTextLight"];
    }

    private void UpdateMonthSwitcherLabel()
    {
        MonthSwitcherLabel.Text = displayedMonth.ToString(
            "MMMM yyyy",
            CultureInfo.CurrentCulture);
    }

    private static string GetDateGroupTitle(DateTime date)
    {
        var dayLabel = date.Date switch
        {
            var value when value == DateTime.Today => "TODAY",
            var value when value == DateTime.Today.AddDays(-1) => "YESTERDAY",
            _ => date.ToString("dddd", CultureInfo.CurrentCulture).ToUpperInvariant()
        };

        var dateLabel = date.ToString("d MMM yyyy", CultureInfo.CurrentCulture).ToUpperInvariant();
        return $"{dayLabel} · {dateLabel}";
    }
}
