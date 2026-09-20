using System.Globalization;
using FinancialTracker.Helpers;
using Microsoft.Maui.Controls.Shapes;

namespace FinancialTracker.Views;

public partial class CalendarDatePickerSheet : ContentView
{
    private const int CalendarCellCount = 42;
    private const double MaximumSheetWidth = 500;
    private const double MaximumSheetHeight = 520;
    private const double DefaultPhoneBottomInset = 94;
    private readonly Border[] dayCells = new Border[CalendarCellCount];
    private readonly Label[] dayLabels = new Label[CalendarCellCount];
    private readonly DateTime[] dayDates = new DateTime[CalendarCellCount];
    private TaskCompletionSource<DateTime?>? pendingSelection;
    private DateTime selectedDate;
    private DateTime displayedMonth;
    private DateTime minimumDate;
    private DateTime maximumDate;
    private int renderedMinimumYear;
    private int renderedMaximumYear;
    private bool isOpen;
    private bool isAnimating;
    private bool isMonthYearVisible;

    public static readonly BindableProperty BottomInsetProperty = BindableProperty.Create(
        nameof(BottomInset),
        typeof(double),
        typeof(CalendarDatePickerSheet),
        DefaultPhoneBottomInset,
        propertyChanged: static (bindable, _, _) =>
            ((CalendarDatePickerSheet)bindable).UpdateSheetBounds());

    public double BottomInset
    {
        get => (double)GetValue(BottomInsetProperty);
        set => SetValue(BottomInsetProperty, value);
    }

    public CalendarDatePickerSheet()
    {
        InitializeComponent();
        CreateDayCells();
        CreateMonthOptions();
    }

    public async Task<DateTime?> PickAsync(
        DateTime initialDate,
        DateTime minimumDate,
        DateTime maximumDate)
    {
        if (isOpen)
        {
            return null;
        }

        if (minimumDate.Date > maximumDate.Date)
        {
            throw new ArgumentException("The minimum date cannot be after the maximum date.");
        }

        this.minimumDate = minimumDate.Date;
        this.maximumDate = maximumDate.Date;
        selectedDate = Clamp(initialDate.Date, this.minimumDate, this.maximumDate);
        displayedMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        pendingSelection = new TaskCompletionSource<DateTime?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        isOpen = true;
        isMonthYearVisible = false;
        CalendarPanel.IsVisible = true;
        MonthYearPanel.IsVisible = false;
        MonthYearChevron.Rotation = 0;
        CreateYearOptions();
        Render();

        IsVisible = true;
        OverlayRoot.Opacity = 0;
        PickerSheet.Opacity = 0.9;
        PickerSheet.TranslationY = 36;
        isAnimating = true;
        try
        {
            await Task.WhenAll(
                OverlayRoot.FadeToAsync(1, 155, Easing.CubicOut),
                PickerSheet.FadeToAsync(1, 190, Easing.CubicOut),
                PickerSheet.TranslateToAsync(0, 0, 230, Easing.CubicOut));
        }
        finally
        {
            isAnimating = false;
        }

        return await pendingSelection.Task;
    }

    public Task DismissAsync() => CompleteAsync(null);

    private void CreateDayCells()
    {
        for (var index = 0; index < CalendarCellCount; index++)
        {
            var label = new Label
            {
                FontFamily = "OpenSansSemibold",
                FontSize = 15,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                InputTransparent = true
            };
            var cell = new Border
            {
                WidthRequest = 42,
                HeightRequest = 42,
                Padding = 0,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 21 },
                BackgroundColor = Colors.Transparent,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Content = label
            };
            var tap = new TapGestureRecognizer { CommandParameter = index };
            tap.Tapped += OnDayTapped;
            cell.GestureRecognizers.Add(tap);

            Grid.SetColumn(cell, index % 7);
            Grid.SetRow(cell, index / 7);
            DaysGrid.Children.Add(cell);
            dayCells[index] = cell;
            dayLabels[index] = label;
        }
    }

    private void OnOverlaySizeChanged(object? sender, EventArgs e) => UpdateSheetBounds();

    private void UpdateSheetBounds()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        var isPortraitPhone = DeviceInfo.Idiom == DeviceIdiom.Phone && Height >= Width;
        var horizontalMargin = isPortraitPhone ? 12d : 18d;
        var bottomInset = isPortraitPhone ? Math.Max(0, BottomInset) : 0;
        var verticalMargin = isPortraitPhone
            ? new Thickness(horizontalMargin, 0, horizontalMargin, bottomInset + 12)
            : new Thickness(horizontalMargin, 12);
        var availableWidth = Math.Max(280, Width - (horizontalMargin * 2));
        var availableHeight = Math.Max(
            320,
            Height - verticalMargin.Top - verticalMargin.Bottom);
        var sheetHeight = Math.Min(MaximumSheetHeight, availableHeight);

        PickerSheet.Margin = verticalMargin;
        PickerSheet.WidthRequest = Math.Min(MaximumSheetWidth, availableWidth);
        PickerSheet.HeightRequest = sheetHeight;
        PickerSheet.VerticalOptions = isPortraitPhone
            ? LayoutOptions.End
            : LayoutOptions.Center;

        var dayRowHeight = Math.Clamp((sheetHeight - 208) / 6, 30, 52);
        foreach (var row in DaysGrid.RowDefinitions)
        {
            row.Height = new GridLength(dayRowHeight);
        }
    }

    private void CreateMonthOptions()
    {
        var monthNames = CultureInfo.CurrentCulture.DateTimeFormat.MonthNames;
        for (var month = 1; month <= 12; month++)
        {
            MonthOptionsLayout.Children.Add(CreateChoice(
                monthNames[month - 1],
                month,
                OnMonthOptionTapped));
        }
    }

    private void CreateYearOptions()
    {
        if (renderedMinimumYear == minimumDate.Year &&
            renderedMaximumYear == maximumDate.Year &&
            YearOptionsLayout.Children.Count > 0)
        {
            return;
        }

        YearOptionsLayout.Children.Clear();
        for (var year = minimumDate.Year; year <= maximumDate.Year; year++)
        {
            YearOptionsLayout.Children.Add(CreateChoice(
                year.ToString(CultureInfo.CurrentCulture),
                year,
                OnYearOptionTapped));
        }

        renderedMinimumYear = minimumDate.Year;
        renderedMaximumYear = maximumDate.Year;
    }

    private static Border CreateChoice(
        string text,
        int value,
        EventHandler<TappedEventArgs> tapped)
    {
        var label = new Label
        {
            Text = text,
            FontFamily = "OpenSansSemibold",
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            InputTransparent = true
        };
        var border = new Border
        {
            HeightRequest = 43,
            Margin = new Thickness(2, 0),
            Padding = new Thickness(8, 0),
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            BindingContext = value,
            Content = label
        };
        var gesture = new TapGestureRecognizer { CommandParameter = value };
        gesture.Tapped += tapped;
        border.GestureRecognizers.Add(gesture);
        return border;
    }

    private void Render()
    {
        MonthYearLabel.Text = displayedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
        SelectedDateTitleLabel.Text = selectedDate == DateTime.Today
            ? "Today"
            : selectedDate.ToString("ddd, d MMM", CultureInfo.CurrentCulture);

        var firstDayOffset = ((int)displayedMonth.DayOfWeek + 6) % 7;
        var firstCellDate = displayedMonth.AddDays(-firstDayOffset);
        for (var index = 0; index < CalendarCellCount; index++)
        {
            var date = firstCellDate.AddDays(index);
            dayDates[index] = date;
            RenderDayCell(index, date);
        }

        var previousMonth = displayedMonth.AddMonths(-1);
        var nextMonth = displayedMonth.AddMonths(1);
        SetNavigationState(
            PreviousMonthButton,
            previousMonth.Year > minimumDate.Year ||
            previousMonth.Month >= minimumDate.Month && previousMonth.Year == minimumDate.Year);
        SetNavigationState(
            NextMonthButton,
            nextMonth.Year < maximumDate.Year ||
            nextMonth.Month <= maximumDate.Month && nextMonth.Year == maximumDate.Year);

        var canSelectToday = DateTime.Today >= minimumDate && DateTime.Today <= maximumDate;
        TodayButton.IsEnabled = canSelectToday;
        TodayButton.Opacity = canSelectToday ? 1 : 0.38;
        RefreshChoiceStyles();
    }

    private void RenderDayCell(int index, DateTime date)
    {
        var cell = dayCells[index];
        var label = dayLabels[index];
        var isAvailable = date >= minimumDate && date <= maximumDate;
        var isDisplayedMonth = date.Month == displayedMonth.Month &&
            date.Year == displayedMonth.Year;
        var isSelected = date == selectedDate;
        var isToday = date == DateTime.Today;

        label.Text = date.Day.ToString(CultureInfo.CurrentCulture);
        cell.IsEnabled = isAvailable;
        cell.Opacity = isAvailable ? isDisplayedMonth ? 1 : 0.42 : 0.2;

        if (isSelected)
        {
            ThemeResourceBindings.SetDynamic(cell, Border.BackgroundColorProperty, "Accent");
            ThemeResourceBindings.SetStatic(cell, Border.StrokeProperty, Brush.Transparent);
            ThemeResourceBindings.SetDynamic(label, Label.TextColorProperty, "AccentForeground");
            return;
        }

        ThemeResourceBindings.SetStatic(cell, Border.BackgroundColorProperty, Colors.Transparent);
        if (isToday && isAvailable)
        {
            ThemeResourceBindings.SetDynamic(cell, Border.StrokeProperty, "Accent");
            cell.StrokeThickness = 1.5;
            ThemeResourceBindings.SetDynamic(label, Label.TextColorProperty, "Accent");
        }
        else
        {
            ThemeResourceBindings.SetStatic(cell, Border.StrokeProperty, Brush.Transparent);
            cell.StrokeThickness = 0;
            ThemeResourceBindings.SetColor(
                label,
                Label.TextColorProperty,
                isDisplayedMonth ? "PrimaryTextLight" : "SecondaryTextLight",
                isDisplayedMonth ? "PrimaryTextDark" : "SecondaryTextDark");
        }
    }

    private void RefreshChoiceStyles()
    {
        foreach (var child in MonthOptionsLayout.Children.OfType<Border>())
        {
            var month = (int)child.BindingContext;
            var enabled = IsMonthAvailable(displayedMonth.Year, month);
            ApplyChoiceStyle(child, month == displayedMonth.Month, enabled);
        }

        foreach (var child in YearOptionsLayout.Children.OfType<Border>())
        {
            var year = (int)child.BindingContext;
            ApplyChoiceStyle(child, year == displayedMonth.Year, true);
        }
    }

    private static void ApplyChoiceStyle(Border choice, bool isSelected, bool isEnabled)
    {
        if (choice.Content is not Label label)
        {
            return;
        }

        choice.IsEnabled = isEnabled;
        choice.Opacity = isEnabled ? 1 : 0.28;
        if (isSelected)
        {
            ThemeResourceBindings.SetDynamic(choice, Border.BackgroundColorProperty, "AccentTint");
            ThemeResourceBindings.SetDynamic(choice, Border.StrokeProperty, "Accent");
            ThemeResourceBindings.SetDynamic(
                label,
                Label.TextColorProperty,
                "Accent");
            return;
        }

        ThemeResourceBindings.SetStatic(choice, Border.BackgroundColorProperty, Colors.Transparent);
        ThemeResourceBindings.SetBrush(
            choice,
            Border.StrokeProperty,
            "DividerLight",
            "DividerDark");
        ThemeResourceBindings.SetColor(
            label,
            Label.TextColorProperty,
            "PrimaryTextLight",
            "PrimaryTextDark");
    }

    private bool IsMonthAvailable(int year, int month)
    {
        var first = new DateTime(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        return last >= minimumDate && first <= maximumDate;
    }

    private static void SetNavigationState(VisualElement button, bool isEnabled)
    {
        button.IsEnabled = isEnabled;
        button.Opacity = isEnabled ? 1 : 0.28;
    }

    private async void OnDayTapped(object? sender, TappedEventArgs e)
    {
        if (isAnimating || e.Parameter is not int index)
        {
            return;
        }

        var date = dayDates[index];
        if (date < minimumDate || date > maximumDate)
        {
            return;
        }

        selectedDate = date;
        displayedMonth = new DateTime(date.Year, date.Month, 1);
        Render();
        await Task.Delay(80);
        await CompleteAsync(date);
    }

    private async void OnTodayTapped(object? sender, TappedEventArgs e)
    {
        if (!TodayButton.IsEnabled || isAnimating)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedDate = DateTime.Today;
        displayedMonth = new DateTime(selectedDate.Year, selectedDate.Month, 1);
        Render();
        await Task.Delay(80);
        await CompleteAsync(selectedDate);
        await feedback;
    }

    private async void OnPreviousMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeMonthAsync(-1, sender);

    private async void OnNextMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeMonthAsync(1, sender);

    private async Task ChangeMonthAsync(int offset, object? sender)
    {
        if (isAnimating)
        {
            return;
        }

        var target = displayedMonth.AddMonths(offset);
        if (!IsMonthAvailable(target.Year, target.Month))
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        displayedMonth = target;
        Render();
        await feedback;
    }

    private async void OnMonthYearTapped(object? sender, TappedEventArgs e)
    {
        if (isAnimating)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        isMonthYearVisible = !isMonthYearVisible;
        CalendarPanel.IsVisible = !isMonthYearVisible;
        MonthYearPanel.IsVisible = isMonthYearVisible;
        MonthYearChevron.Rotation = isMonthYearVisible ? 180 : 0;
        if (isMonthYearVisible)
        {
            RefreshChoiceStyles();
            await Task.Delay(45);
            await ScrollChoicesIntoViewAsync(animated: false);
        }

        await feedback;
    }

    private async void OnMonthOptionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not int month || !IsMonthAvailable(displayedMonth.Year, month))
        {
            return;
        }

        displayedMonth = new DateTime(displayedMonth.Year, month, 1);
        Render();
        await ScrollChoicesIntoViewAsync(animated: true);
    }

    private async void OnYearOptionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not int year)
        {
            return;
        }

        var month = displayedMonth.Month;
        if (!IsMonthAvailable(year, month))
        {
            month = year == minimumDate.Year
                ? minimumDate.Month
                : maximumDate.Month;
        }

        displayedMonth = new DateTime(year, month, 1);
        Render();
        await ScrollChoicesIntoViewAsync(animated: true);
    }

    private async Task ScrollChoicesIntoViewAsync(bool animated)
    {
        await Task.Yield();
        var monthChoice = MonthOptionsLayout.Children
            .OfType<Border>()
            .FirstOrDefault(view => (int)view.BindingContext == displayedMonth.Month);
        var yearChoice = YearOptionsLayout.Children
            .OfType<Border>()
            .FirstOrDefault(view => (int)view.BindingContext == displayedMonth.Year);
        var tasks = new List<Task>(2);
        if (monthChoice is not null)
        {
            tasks.Add(MonthOptionsScroll.ScrollToAsync(
                monthChoice,
                ScrollToPosition.Center,
                animated));
        }

        if (yearChoice is not null)
        {
            tasks.Add(YearOptionsScroll.ScrollToAsync(
                yearChoice,
                ScrollToPosition.Center,
                animated));
        }

        await Task.WhenAll(tasks);
    }

    private async void OnDismissTapped(object? sender, TappedEventArgs e)
    {
        if (!isAnimating)
        {
            await CompleteAsync(null);
        }
    }

    private async Task CompleteAsync(DateTime? result)
    {
        if (!isOpen || isAnimating)
        {
            return;
        }

        isAnimating = true;
        var completion = pendingSelection;
        pendingSelection = null;
        try
        {
            await Task.WhenAll(
                OverlayRoot.FadeToAsync(0, 130, Easing.CubicIn),
                PickerSheet.TranslateToAsync(0, 28, 155, Easing.CubicIn),
                PickerSheet.FadeToAsync(0.9, 140, Easing.CubicIn));
        }
        finally
        {
            IsVisible = false;
            OverlayRoot.Opacity = 0;
            PickerSheet.Opacity = 1;
            PickerSheet.TranslationY = 0;
            isOpen = false;
            isAnimating = false;
            completion?.TrySetResult(result?.Date);
        }
    }

    private static DateTime Clamp(DateTime value, DateTime minimum, DateTime maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;
}
