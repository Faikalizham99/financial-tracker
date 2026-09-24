using FinancialTracker.Helpers;

namespace FinancialTracker.Views;

public partial class MonthYearPickerSheet : ContentView
{
    private readonly Border[] monthButtons;
    private readonly Border[] yearButtons;
    private readonly Label[] yearLabels;
    private TaskCompletionSource<DateTime?>? completion;
    private DateTime selectedMonth;
    private int displayedYear;
    private int displayedDecadeStart;
    private int minimumYear;
    private int maximumYear;
    private bool isOpen;
    private bool isAnimating;
    private bool isYearSelectorOpen;
    private bool isYearSelectorAnimating;

    public MonthYearPickerSheet()
    {
        InitializeComponent();
        monthButtons =
        [
            Month1, Month2, Month3, Month4, Month5, Month6,
            Month7, Month8, Month9, Month10, Month11, Month12
        ];
        yearButtons =
        [
            YearOption0, YearOption1, YearOption2, YearOption3,
            YearOption4, YearOption5, YearOption6, YearOption7,
            YearOption8, YearOption9, YearOption10, YearOption11
        ];
        yearLabels =
        [
            YearOptionLabel0, YearOptionLabel1, YearOptionLabel2,
            YearOptionLabel3, YearOptionLabel4, YearOptionLabel5,
            YearOptionLabel6, YearOptionLabel7, YearOptionLabel8,
            YearOptionLabel9, YearOptionLabel10, YearOptionLabel11
        ];
    }

    public bool IsOpen => isOpen;

    public async Task DismissAsync()
    {
        if (isYearSelectorOpen)
        {
            await CloseYearSelectorAsync();
            return;
        }

        await CompleteAsync(null);
    }

    public async Task<DateTime?> PickAsync(
        DateTime initialMonth,
        int minimumYear,
        int maximumYear)
    {
        if (isOpen)
        {
            return null;
        }

        this.minimumYear = minimumYear;
        this.maximumYear = maximumYear;
        selectedMonth = new DateTime(
            Math.Clamp(initialMonth.Year, minimumYear, maximumYear),
            initialMonth.Month,
            1);
        displayedYear = selectedMonth.Year;
        completion = new TaskCompletionSource<DateTime?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        isOpen = true;
        displayedDecadeStart = StartOfDecade(displayedYear);
        Render();

        IsVisible = true;
        OverlayRoot.Opacity = 0;
        PickerCard.Opacity = 0.92;
        PickerCard.TranslationY = 28;
        isAnimating = true;
        try
        {
            await Task.WhenAll(
                OverlayRoot.FadeToAsync(1, 150, Easing.CubicOut),
                PickerCard.FadeToAsync(1, 180, Easing.CubicOut),
                PickerCard.TranslateToAsync(0, 0, 210, Easing.CubicOut));
        }
        finally
        {
            isAnimating = false;
        }

        return await completion.Task;
    }

    private void Render()
    {
        YearLabel.Text = displayedYear.ToString();
        PreviousYearButton.IsEnabled = displayedYear > minimumYear;
        PreviousYearButton.Opacity = PreviousYearButton.IsEnabled ? 1 : 0.3;
        NextYearButton.IsEnabled = displayedYear < maximumYear;
        NextYearButton.Opacity = NextYearButton.IsEnabled ? 1 : 0.3;

        for (var index = 0; index < monthButtons.Length; index++)
        {
            var isSelected = displayedYear == selectedMonth.Year &&
                index + 1 == selectedMonth.Month;
            var button = monthButtons[index];
            if (isSelected)
            {
                ThemeResourceBindings.SetDynamic(button, Border.BackgroundColorProperty, "AccentTint");
                ThemeResourceBindings.SetDynamic(button, Border.StrokeProperty, "Accent");
                button.StrokeThickness = 2;
            }
            else
            {
                ThemeResourceBindings.SetColor(
                    button,
                    Border.BackgroundColorProperty,
                    "CardBackgroundLight",
                    "CardBackgroundDark");
                ThemeResourceBindings.SetBrush(
                    button,
                    Border.StrokeProperty,
                    "DividerLight",
                    "DividerDark");
                button.StrokeThickness = 1;
            }
        }
    }

    private async void OnPreviousYearTapped(object? sender, TappedEventArgs e)
    {
        if (!isAnimating && displayedYear > minimumYear)
        {
            displayedYear--;
            Render();
            await InteractionAnimations.PulseAsync(sender);
        }
    }

    private async void OnNextYearTapped(object? sender, TappedEventArgs e)
    {
        if (!isAnimating && displayedYear < maximumYear)
        {
            displayedYear++;
            Render();
            await InteractionAnimations.PulseAsync(sender);
        }
    }

    private async void OnYearSelectorTapped(object? sender, TappedEventArgs e)
    {
        if (isAnimating || isYearSelectorOpen || isYearSelectorAnimating)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        displayedDecadeStart = StartOfDecade(displayedYear);
        RenderYearSelector();
        isYearSelectorOpen = true;
        isYearSelectorAnimating = true;
        YearSelectorOverlay.IsVisible = true;
        YearSelectorOverlay.Opacity = 0;
        YearSelectorCard.Scale = 0.96;
        try
        {
            await Task.WhenAll(
                YearSelectorOverlay.FadeToAsync(1, 130, Easing.CubicOut),
                YearSelectorCard.ScaleToAsync(1, 170, Easing.CubicOut));
        }
        finally
        {
            isYearSelectorAnimating = false;
        }

        await feedback;
    }

    private async void OnYearOptionTapped(object? sender, TappedEventArgs e)
    {
        if (isYearSelectorAnimating ||
            !int.TryParse(e.Parameter?.ToString(), out var index) ||
            index < 0 || index >= yearButtons.Length)
        {
            return;
        }

        var year = displayedDecadeStart - 1 + index;
        if (year < minimumYear || year > maximumYear)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        displayedYear = year;
        Render();
        await Task.Delay(45);
        await CloseYearSelectorAsync();
        await feedback;
    }

    private async void OnPreviousYearPageTapped(object? sender, TappedEventArgs e)
    {
        var minimumDecade = StartOfDecade(minimumYear);
        if (isYearSelectorAnimating || displayedDecadeStart <= minimumDecade)
        {
            return;
        }

        displayedDecadeStart -= 10;
        RenderYearSelector();
        await InteractionAnimations.PulseAsync(sender);
    }

    private async void OnNextYearPageTapped(object? sender, TappedEventArgs e)
    {
        var maximumDecade = StartOfDecade(maximumYear);
        if (isYearSelectorAnimating || displayedDecadeStart >= maximumDecade)
        {
            return;
        }

        displayedDecadeStart += 10;
        RenderYearSelector();
        await InteractionAnimations.PulseAsync(sender);
    }

    private void RenderYearSelector()
    {
        YearRangeLabel.Text =
            $"{displayedDecadeStart} \u2013 {displayedDecadeStart + 9}";
        var minimumDecade = StartOfDecade(minimumYear);
        var maximumDecade = StartOfDecade(maximumYear);
        PreviousYearPageButton.IsEnabled = displayedDecadeStart > minimumDecade;
        PreviousYearPageButton.Opacity = PreviousYearPageButton.IsEnabled ? 1 : 0.3;
        NextYearPageButton.IsEnabled = displayedDecadeStart < maximumDecade;
        NextYearPageButton.Opacity = NextYearPageButton.IsEnabled ? 1 : 0.3;

        for (var index = 0; index < yearButtons.Length; index++)
        {
            var year = displayedDecadeStart - 1 + index;
            var button = yearButtons[index];
            yearLabels[index].Text = year.ToString();
            button.IsEnabled = year >= minimumYear && year <= maximumYear;
            button.Opacity = button.IsEnabled ? 1 : 0.3;

            if (year == displayedYear)
            {
                ThemeResourceBindings.SetDynamic(
                    button,
                    Border.BackgroundColorProperty,
                    "AccentTint");
                ThemeResourceBindings.SetDynamic(
                    button,
                    Border.StrokeProperty,
                    "Accent");
                button.StrokeThickness = 2;
            }
            else
            {
                ThemeResourceBindings.SetColor(
                    button,
                    Border.BackgroundColorProperty,
                    "CardBackgroundLight",
                    "CardBackgroundDark");
                ThemeResourceBindings.SetBrush(
                    button,
                    Border.StrokeProperty,
                    "DividerLight",
                    "DividerDark");
                button.StrokeThickness = 1;
            }
        }
    }

    private async void OnYearSelectorDismissTapped(object? sender, TappedEventArgs e)
    {
        await CloseYearSelectorAsync();
    }

    private async Task CloseYearSelectorAsync()
    {
        if (!isYearSelectorOpen || isYearSelectorAnimating)
        {
            return;
        }

        isYearSelectorAnimating = true;
        try
        {
            await Task.WhenAll(
                YearSelectorOverlay.FadeToAsync(0, 110, Easing.CubicIn),
                YearSelectorCard.ScaleToAsync(0.96, 120, Easing.CubicIn));
        }
        finally
        {
            YearSelectorOverlay.IsVisible = false;
            YearSelectorOverlay.Opacity = 0;
            YearSelectorCard.Scale = 1;
            isYearSelectorOpen = false;
            isYearSelectorAnimating = false;
        }
    }

    private async void OnMonthTapped(object? sender, TappedEventArgs e)
    {
        if (isAnimating ||
            !int.TryParse(e.Parameter?.ToString(), out var month) ||
            month is < 1 or > 12)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedMonth = new DateTime(displayedYear, month, 1);
        Render();
        await Task.Delay(55);
        await CompleteAsync(selectedMonth);
        await feedback;
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
        var pendingCompletion = completion;
        completion = null;
        try
        {
            await Task.WhenAll(
                OverlayRoot.FadeToAsync(0, 120, Easing.CubicIn),
                PickerCard.FadeToAsync(0.92, 130, Easing.CubicIn),
                PickerCard.TranslateToAsync(0, 22, 145, Easing.CubicIn));
        }
        finally
        {
            YearSelectorOverlay.IsVisible = false;
            YearSelectorOverlay.Opacity = 0;
            YearSelectorCard.Scale = 1;
            IsVisible = false;
            OverlayRoot.Opacity = 0;
            PickerCard.Opacity = 1;
            PickerCard.TranslationY = 0;
            isOpen = false;
            isAnimating = false;
            isYearSelectorOpen = false;
            isYearSelectorAnimating = false;
            pendingCompletion?.TrySetResult(result);
        }
    }

    private static int StartOfDecade(int year) => (year / 10) * 10;
}
