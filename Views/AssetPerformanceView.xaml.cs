using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.Views.Drawables;
using Microsoft.Maui.Controls.Shapes;

namespace FinancialTracker.Views;

public partial class AssetPerformanceView : ContentView
{
    private const string ChartAnimationName = "AssetPerformanceChartAnimation";
    private const uint ChartAnimationLength = 450;

    private readonly AssetPerformanceChartDrawable chartDrawable = new();
    private AssetPortfolioService? portfolioService;
    private Func<CurrencyOption>? currencyProvider;
    private AssetPerformanceData? performance;
    private string? selectedAssetKey;
    private DateTime selectedCalendarMonth = new(
        DateTime.Today.Year,
        DateTime.Today.Month,
        1);
    private DateTime selectedRecordDate = DateTime.Today;
    private int? editingRecordId;
    private bool isLoss;
    private bool isNormalizingMoney;
    private bool isSaving;
    private int loadVersion;
    private AssetPerformanceChartMode chartMode = AssetPerformanceChartMode.Total;

    public AssetPerformanceView()
    {
        InitializeComponent();
        PerformanceChart.Drawable = chartDrawable;
    }

    public Func<DateTime, DateTime, DateTime, Task<DateTime?>>? DatePickerRequested
    {
        get;
        set;
    }

    public bool IsOpen => IsVisible;

    public event Action<bool>? VisibilityChanged;

    public event Action<DateTime>? RecordChanged;

    public void Configure(
        AssetPortfolioService service,
        Func<CurrencyOption> selectedCurrencyProvider)
    {
        portfolioService = service;
        currencyProvider = selectedCurrencyProvider;
    }

    public async Task OpenAsync(string assetKey)
    {
        if (portfolioService is null || currencyProvider is null)
        {
            return;
        }

        selectedAssetKey = assetKey;
        chartMode = AssetPerformanceChartMode.Total;
        SetVisibility(true);
        await LoadAsync(resetCalendarMonth: true);
    }

    public async Task HandleBackAsync()
    {
        if (EditorOverlay.IsVisible)
        {
            CloseEditor();
            return;
        }

        await Task.CompletedTask;
        Close();
    }

    public void Close()
    {
        ++loadVersion;
        PerformanceChart.AbortAnimation(ChartAnimationName);
        EditorOverlay.IsVisible = false;
        LoadingOverlay.IsVisible = false;
        performance = null;
        selectedAssetKey = null;
        SetVisibility(false);
    }

    private async Task LoadAsync(bool resetCalendarMonth = false)
    {
        if (portfolioService is null ||
            currencyProvider is null ||
            string.IsNullOrWhiteSpace(selectedAssetKey))
        {
            return;
        }

        var requestVersion = ++loadVersion;
        LoadingOverlay.IsVisible = true;
        try
        {
            var loaded = await portfolioService.GetPerformanceAsync(
                selectedAssetKey,
                currencyProvider());
            if (requestVersion != loadVersion || !IsVisible)
            {
                return;
            }

            performance = loaded;
            if (resetCalendarMonth)
            {
                var focusDate = loaded.Latest?.EntryDate ?? DateTime.Today;
                selectedCalendarMonth = new DateTime(
                    focusDate.Year,
                    focusDate.Month,
                    1);
            }
            Render(animateChart: true);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Asset performance load failed: {exception}");
            performance = null;
            SummaryCard.IsVisible = false;
            ComparisonCard.IsVisible = false;
            ChartCard.IsVisible = false;
            CalendarCard.IsVisible = false;
            RecordsSection.IsVisible = false;
            EmptyCard.IsVisible = true;
        }
        finally
        {
            if (requestVersion == loadVersion)
            {
                LoadingOverlay.IsVisible = false;
            }
        }
    }

    private void Render(bool animateChart)
    {
        if (performance is null)
        {
            return;
        }

        AssetNameLabel.Text = performance.Asset.DisplayName;
        AssetIcon.Source = performance.Asset.IconAsset;
        EditorAssetLabel.Text = performance.Asset.DisplayName;
        var hasRecords = performance.Points.Count > 0;
        EmptyCard.IsVisible = !hasRecords;
        SummaryCard.IsVisible = hasRecords;
        ChartCard.IsVisible = hasRecords;
        CalendarCard.IsVisible = hasRecords;
        RecordsSection.IsVisible = hasRecords;

        if (!hasRecords || performance.Latest is not { } latest)
        {
            ComparisonCard.IsVisible = false;
            return;
        }

        CurrentTotalLabel.Text = MoneyFormatter.FormatMinor(
            latest.TotalMinor,
            performance.CurrencySymbol);
        CurrentTotalLabel.FontSize = CurrentTotalLabel.Text.Length switch
        {
            > 19 => 23,
            > 15 => 27,
            _ => 32
        };
        LastRecordedLabel.Text = $"Recorded {latest.EntryDate:dd MMMM yyyy}";
        InvestedLabel.Text = MoneyFormatter.FormatMinor(
            latest.InvestedMinor,
            performance.CurrencySymbol);
        ProfitLabel.Text = MoneyFormatter.FormatMinor(
            latest.ProfitLossMinor,
            performance.CurrencySymbol,
            showPositiveSign: true,
            separateSign: true);
        ProfitLabel.TextColor = ChangeColor(latest.ProfitLossMinor);
        ReturnLabel.Text = latest.ReturnPercentage.HasValue
            ? $"{latest.ReturnPercentage.Value:+0.0;-0.0;0.0}% return"
            : "No return yet";
        ReturnLabel.TextColor = ChangeColor(latest.ProfitLossMinor);
        ReturnPill.BackgroundColor = ChangeTint(latest.ProfitLossMinor);

        var previous = performance.Points.Count > 1
            ? performance.Points[^2]
            : null;
        ComparisonCard.IsVisible = previous is not null;
        if (previous is not null)
        {
            PreviousDateLabel.Text = latest.DaysSincePrevious == 1
                ? "1 day later"
                : $"{latest.DaysSincePrevious ?? 0} days later";
            SetChangeLabel(InvestedChangeLabel, latest.InvestedChangeMinor);
            SetChangeLabel(ProfitChangeLabel, latest.ProfitChangeMinor);
            SetChangeLabel(TotalChangeLabel, latest.TotalChangeMinor);
        }

        RenderChart(animateChart);
        RenderCalendar();
        RenderRecordHistory();
    }

    private void RenderChart(bool animate)
    {
        if (performance is null)
        {
            return;
        }

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        chartDrawable.AccentColor = ThemeResourceBindings.GetColor("Accent", "#5044E4");
        chartDrawable.SecondaryColor = ThemeResourceBindings.GetColor(
            isDark ? "SecondaryTextDark" : "SecondaryTextLight",
            isDark ? "#BBB4C7" : "#686273");
        chartDrawable.GridColor = ThemeResourceBindings.GetColor(
            isDark ? "DividerDark" : "DividerLight",
            isDark ? "#29292D" : "#E9E3DB");
        chartDrawable.PositiveColor = PositiveColor;
        chartDrawable.NegativeColor = NegativeColor;
        chartDrawable.SetData(performance.Points, chartMode, animate);
        ChartCaptionLabel.Text = chartMode switch
        {
            AssetPerformanceChartMode.InvestedVersusTotal =>
                "See the gap between principal and total value.",
            AssetPerformanceChartMode.ProfitLoss =>
                "Track profit above zero and loss below zero.",
            _ => "Follow the recorded total value over time."
        };
        CompareLegend.IsVisible =
            chartMode == AssetPerformanceChartMode.InvestedVersusTotal;
        UpdateChartButtons();

        PerformanceChart.AbortAnimation(ChartAnimationName);
        if (!animate)
        {
            PerformanceChart.Invalidate();
            return;
        }

        PerformanceChart.Animate(
            ChartAnimationName,
            progress =>
            {
                chartDrawable.AnimationProgress = (float)progress;
                PerformanceChart.Invalidate();
            },
            length: ChartAnimationLength,
            easing: Easing.CubicOut);
    }

    private void UpdateChartButtons()
    {
        UpdateChartButton(
            TotalChartButton,
            TotalChartButtonLabel,
            chartMode == AssetPerformanceChartMode.Total);
        UpdateChartButton(
            InvestedChartButton,
            InvestedChartButtonLabel,
            chartMode == AssetPerformanceChartMode.InvestedVersusTotal);
        UpdateChartButton(
            ProfitChartButton,
            ProfitChartButtonLabel,
            chartMode == AssetPerformanceChartMode.ProfitLoss);
    }

    private static void UpdateChartButton(Border button, Label label, bool selected)
    {
        if (selected)
        {
            ThemeResourceBindings.SetDynamic(button, BackgroundColorProperty, "Accent");
            ThemeResourceBindings.SetDynamic(label, Label.TextColorProperty, "AccentForeground");
            return;
        }

        ThemeResourceBindings.SetColor(
            button,
            BackgroundColorProperty,
            "SurfaceMutedLight",
            "SurfaceMutedDark");
        ThemeResourceBindings.SetColor(
            label,
            Label.TextColorProperty,
            "SecondaryTextLight",
            "SecondaryTextDark");
    }

    private void RenderCalendar()
    {
        if (performance is null)
        {
            return;
        }

        CalendarMonthLabel.Text = selectedCalendarMonth.ToString(
            "MMMM yyyy",
            CultureInfo.CurrentCulture);
        CalendarDaysGrid.Children.Clear();
        CalendarDaysGrid.RowDefinitions.Clear();
        CalendarDaysGrid.ColumnDefinitions.Clear();
        for (var column = 0; column < 7; column++)
        {
            CalendarDaysGrid.ColumnDefinitions.Add(
                new ColumnDefinition(GridLength.Star));
        }

        var daysInMonth = DateTime.DaysInMonth(
            selectedCalendarMonth.Year,
            selectedCalendarMonth.Month);
        var leadingDays = ((int)selectedCalendarMonth.DayOfWeek + 6) % 7;
        var rowCount = Math.Max(4, (int)Math.Ceiling((leadingDays + daysInMonth) / 7d));
        var cellHeight = rowCount switch
        {
            4 => 58d,
            5 => 50d,
            _ => 44d
        };
        for (var row = 0; row < rowCount; row++)
        {
            CalendarDaysGrid.RowDefinitions.Add(new RowDefinition(cellHeight));
        }

        var recordsByDay = performance.Points
            .Where(item =>
                item.EntryDate.Year == selectedCalendarMonth.Year &&
                item.EntryDate.Month == selectedCalendarMonth.Month)
            .ToDictionary(item => item.EntryDate.Day);
        var cellCount = rowCount * 7;
        for (var index = 0; index < cellCount; index++)
        {
            var day = index - leadingDays + 1;
            if (day < 1 || day > daysInMonth)
            {
                continue;
            }

            recordsByDay.TryGetValue(day, out var point);
            var date = new DateTime(
                selectedCalendarMonth.Year,
                selectedCalendarMonth.Month,
                day);
            var cell = CreateCalendarCell(date, point, cellHeight);
            CalendarDaysGrid.Add(cell, index % 7, index / 7);
        }
    }

    private Border CreateCalendarCell(
        DateTime date,
        AssetPerformancePoint? point,
        double height)
    {
        var isFuture = date > DateTime.Today;
        var background = point is null
            ? SurfaceMutedColor
            : point.ProfitChangeMinor switch
            {
                > 0 => PositiveColor.WithAlpha(0.14f),
                < 0 => NegativeColor.WithAlpha(0.14f),
                null => AccentColor.WithAlpha(0.16f),
                _ => SurfaceMutedColor
            };
        var valueColor = point?.ProfitChangeMinor switch
        {
            > 0 => PositiveColor,
            < 0 => NegativeColor,
            null when point is not null => AccentColor,
            _ => SecondaryTextColor
        };
        var content = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = date.Day.ToString(CultureInfo.InvariantCulture),
                    FontFamily = "OpenSansSemibold",
                    FontSize = 11,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new Label
                {
                    Text = point is null
                        ? string.Empty
                        : CompactMoney(point.ProfitLossMinor),
                    FontFamily = "OpenSansSemibold",
                    FontSize = 8,
                    TextColor = valueColor,
                    HorizontalTextAlignment = TextAlignment.Center,
                    LineBreakMode = LineBreakMode.TailTruncation,
                    MaxLines = 1
                }
            }
        };
        var cell = new Border
        {
            HeightRequest = height,
            Padding = new Thickness(2),
            BackgroundColor = background,
            Stroke = date == DateTime.Today
                ? AccentColor
                : Colors.Transparent,
            StrokeThickness = date == DateTime.Today ? 1.6 : 0,
            StrokeShape = new RoundRectangle { CornerRadius = 11 },
            Content = content,
            Opacity = isFuture ? 0.42 : 1
        };
        if (!isFuture)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OpenEditorAsync(point, date);
            cell.GestureRecognizers.Add(tap);
            InteractionAnimations.SetIsPressFeedbackEnabled(cell, true);
        }
        return cell;
    }

    private void RenderRecordHistory()
    {
        if (performance is null)
        {
            return;
        }

        RecordCountLabel.Text = performance.Points.Count == 1
            ? "1 record"
            : $"{performance.Points.Count} records";
        BindingContext = new
        {
            Records = performance.Points
                .OrderByDescending(item => item.EntryDate)
                .Select(item => new RecordRowViewModel(
                    item.Id,
                    item.EntryDate.Day.ToString("00", CultureInfo.InvariantCulture),
                    item.EntryDate.ToString("MMM", CultureInfo.CurrentCulture).ToUpperInvariant(),
                    MoneyFormatter.FormatMinor(item.TotalMinor, performance.CurrencySymbol),
                    string.IsNullOrWhiteSpace(item.Note)
                        ? $"Invested {MoneyFormatter.FormatMinor(item.InvestedMinor, performance.CurrencySymbol)}"
                        : item.Note,
                    MoneyFormatter.FormatMinor(
                        item.ProfitLossMinor,
                        performance.CurrencySymbol,
                        showPositiveSign: true,
                        separateSign: true),
                    item.TotalChangeMinor.HasValue
                        ? $"{MoneyFormatter.FormatMinor(item.TotalChangeMinor.Value, performance.CurrencySymbol, true)} vs previous"
                        : "Baseline record",
                    ChangeColor(item.ProfitLossMinor),
                    ChangeColor(item.TotalChangeMinor),
                    item.ProfitChangeMinor switch
                    {
                        > 0 => PositiveColor.WithAlpha(0.14f),
                        < 0 => NegativeColor.WithAlpha(0.14f),
                        _ => AccentColor.WithAlpha(0.14f)
                    }))
                .ToList()
        };
    }

    private async Task OpenEditorAsync(
        AssetPerformancePoint? record = null,
        DateTime? requestedDate = null)
    {
        if (performance is null || currencyProvider is null)
        {
            return;
        }

        selectedRecordDate = (requestedDate ?? record?.EntryDate ?? DateTime.Today).Date;
        PopulateEditor(record ?? performance.Points.FirstOrDefault(
            item => item.EntryDate == selectedRecordDate));
        EditorOverlay.IsVisible = true;
        await Task.Yield();
    }

    private void PopulateEditor(AssetPerformancePoint? record)
    {
        if (performance is null)
        {
            return;
        }

        var previous = record is null
            ? performance.Points.LastOrDefault(item => item.EntryDate < selectedRecordDate)
            : null;
        editingRecordId = record?.Id;
        EditorTitleLabel.Text = record is null
            ? "Add performance record"
            : "Update performance record";
        DeleteRecordButton.IsVisible = record is not null;
        RecordDateActionLabel.Text = record is null ? "Change" : "Fixed";
        RecordDateActionLabel.TextColor = record is null
            ? AccentColor
            : SecondaryTextColor;
        selectedRecordDate = record?.EntryDate ?? selectedRecordDate;
        RecordDateLabel.Text = selectedRecordDate.ToString(
            "dd MMMM yyyy",
            CultureInfo.CurrentCulture);
        InvestedCurrencyLabel.Text = performance.CurrencySymbol;
        ProfitCurrencyLabel.Text = performance.CurrencySymbol;
        InvestedEntry.Text = MoneyFormatter.FormatMinorValue(
            record?.InvestedMinor ?? previous?.InvestedMinor ?? 0)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        var profitLoss = record?.ProfitLossMinor ?? previous?.ProfitLossMinor ?? 0;
        isLoss = profitLoss < 0;
        ProfitEntry.Text = MoneyFormatter.FormatMinorValue(profitLoss)
            .Replace(",", string.Empty, StringComparison.Ordinal);
        NoteEditor.Text = record?.Note ?? string.Empty;
        EditorErrorLabel.IsVisible = false;
        UpdateProfitModeButtons();
        UpdateCalculatedTotal();
    }

    private void CloseEditor()
    {
        InvestedEntry.Unfocus();
        ProfitEntry.Unfocus();
        NoteEditor.Unfocus();
        EditorOverlay.IsVisible = false;
        editingRecordId = null;
        EditorErrorLabel.IsVisible = false;
    }

    private async void OnSaveRecordClicked(object? sender, EventArgs e)
    {
        if (portfolioService is null ||
            currencyProvider is null ||
            performance is null ||
            string.IsNullOrWhiteSpace(selectedAssetKey) ||
            isSaving)
        {
            return;
        }

        if (!MoneyInputParser.TryParseMinor(
                InvestedEntry.Text,
                allowZero: true,
                out var investedMinor) ||
            !MoneyInputParser.TryParseMinor(
                ProfitEntry.Text,
                allowZero: true,
                out var profitMagnitudeMinor))
        {
            ShowEditorError("Enter valid invested and profit or loss amounts.");
            return;
        }

        var profitLossMinor = isLoss ? -profitMagnitudeMinor : profitMagnitudeMinor;
        if (investedMinor + profitLossMinor < 0)
        {
            ShowEditorError("The loss cannot be greater than the amount invested.");
            return;
        }

        isSaving = true;
        SaveRecordButton.IsEnabled = false;
        SaveRecordButton.Text = "Saving…";
        try
        {
            await portfolioService.SavePerformanceRecordAsync(
                selectedAssetKey,
                selectedRecordDate,
                investedMinor,
                profitLossMinor,
                NoteEditor.Text,
                currencyProvider());
            CloseEditor();
            RecordChanged?.Invoke(selectedRecordDate);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Asset performance save failed: {exception}");
            ShowEditorError(string.IsNullOrWhiteSpace(exception.Message)
                ? "The record could not be saved. Please try again."
                : exception.Message);
        }
        finally
        {
            isSaving = false;
            SaveRecordButton.IsEnabled = true;
            SaveRecordButton.Text = "Save record";
        }
    }

    private async void OnDeleteRecordTapped(object? sender, TappedEventArgs e)
    {
        if (portfolioService is null ||
            currencyProvider is null ||
            string.IsNullOrWhiteSpace(selectedAssetKey) ||
            !editingRecordId.HasValue ||
            isSaving)
        {
            return;
        }

        var hostPage = FindHostPage();
        if (hostPage is null)
        {
            ShowEditorError("The confirmation could not be opened. Please try again.");
            return;
        }

        var confirmed = await hostPage.DisplayAlertAsync(
            "Delete this record?",
            $"The {selectedRecordDate:dd MMMM yyyy} performance check-in will be removed.",
            "Delete",
            "Cancel");
        if (!confirmed)
        {
            return;
        }

        isSaving = true;
        SaveRecordButton.IsEnabled = false;
        try
        {
            await portfolioService.DeletePerformanceRecordAsync(
                selectedAssetKey,
                editingRecordId.Value,
                selectedRecordDate,
                currencyProvider());
            CloseEditor();
            RecordChanged?.Invoke(selectedRecordDate);
            await LoadAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Asset performance deletion failed: {exception}");
            ShowEditorError("The record could not be deleted. Please try again.");
        }
        finally
        {
            isSaving = false;
            SaveRecordButton.IsEnabled = true;
        }
    }

    private async void OnRecordDateTapped(object? sender, TappedEventArgs e)
    {
        if (DatePickerRequested is null || performance is null)
        {
            return;
        }

        if (editingRecordId.HasValue)
        {
            ShowEditorError(
                "The date is fixed for an existing record. Add another record for a different day.");
            return;
        }

        var picked = await DatePickerRequested(
            selectedRecordDate,
            DateRangeLimits.MinimumDate,
            DateTime.Today);
        if (!picked.HasValue)
        {
            return;
        }

        selectedRecordDate = picked.Value.Date;
        PopulateEditor(performance.Points.FirstOrDefault(
            item => item.EntryDate == selectedRecordDate));
    }

    private void OnMoneyEntryTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!isNormalizingMoney && sender is Entry entry)
        {
            var sanitized = MoneyInputParser.SanitizeDecimal(e.NewTextValue);
            if (!string.Equals(sanitized, e.NewTextValue, StringComparison.Ordinal))
            {
                isNormalizingMoney = true;
                entry.Text = sanitized;
                entry.CursorPosition = sanitized.Length;
                isNormalizingMoney = false;
            }
        }
        UpdateCalculatedTotal();
    }

    private void UpdateCalculatedTotal()
    {
        if (performance is null)
        {
            return;
        }

        MoneyInputParser.TryParseMinor(
            InvestedEntry.Text,
            allowZero: true,
            out var investedMinor);
        MoneyInputParser.TryParseMinor(
            ProfitEntry.Text,
            allowZero: true,
            out var profitMagnitudeMinor);
        var total = investedMinor + (isLoss ? -profitMagnitudeMinor : profitMagnitudeMinor);
        CalculatedTotalLabel.Text = MoneyFormatter.FormatMinor(
            Math.Max(0, total),
            performance.CurrencySymbol);
        CalculatedTotalLabel.TextColor = total < 0 ? NegativeColor : AccentColor;
    }

    private void UpdateProfitModeButtons()
    {
        UpdateModeButton(ProfitModeButton, ProfitModeLabel, !isLoss, PositiveColor);
        UpdateModeButton(LossModeButton, LossModeLabel, isLoss, NegativeColor);
    }

    private static void UpdateModeButton(
        Border border,
        Label label,
        bool selected,
        Color selectedColor)
    {
        border.BackgroundColor = selected
            ? selectedColor.WithAlpha(0.16f)
            : Colors.Transparent;
        label.TextColor = selected ? selectedColor : SecondaryTextColor;
    }

    private void ShowEditorError(string message)
    {
        EditorErrorLabel.Text = message;
        EditorErrorLabel.IsVisible = true;
    }

    private void SetChangeLabel(Label label, long? change)
    {
        label.Text = change.HasValue && performance is not null
            ? MoneyFormatter.FormatMinor(
                change.Value,
                performance.CurrencySymbol,
                showPositiveSign: true,
                separateSign: true)
            : "Baseline";
        label.TextColor = ChangeColor(change);
    }

    private void SetVisibility(bool isVisible)
    {
        if (IsVisible == isVisible)
        {
            return;
        }
        IsVisible = isVisible;
        VisibilityChanged?.Invoke(isVisible);
    }

    private async void OnAddRecordTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await OpenEditorAsync(requestedDate: DateTime.Today);
        await feedback;
    }

    private async void OnAddRecordClicked(object? sender, EventArgs e) =>
        await OpenEditorAsync(requestedDate: DateTime.Today);

    private void OnCloseTapped(object? sender, TappedEventArgs e) => Close();

    private void OnCloseEditorTapped(object? sender, TappedEventArgs e) => CloseEditor();

    private void OnProfitModeTapped(object? sender, TappedEventArgs e)
    {
        isLoss = false;
        UpdateProfitModeButtons();
        UpdateCalculatedTotal();
    }

    private void OnLossModeTapped(object? sender, TappedEventArgs e)
    {
        isLoss = true;
        UpdateProfitModeButtons();
        UpdateCalculatedTotal();
    }

    private void OnTotalChartTapped(object? sender, TappedEventArgs e) =>
        SetChartMode(AssetPerformanceChartMode.Total);

    private void OnInvestedChartTapped(object? sender, TappedEventArgs e) =>
        SetChartMode(AssetPerformanceChartMode.InvestedVersusTotal);

    private void OnProfitChartTapped(object? sender, TappedEventArgs e) =>
        SetChartMode(AssetPerformanceChartMode.ProfitLoss);

    private void SetChartMode(AssetPerformanceChartMode mode)
    {
        if (chartMode == mode)
        {
            return;
        }
        chartMode = mode;
        RenderChart(animate: true);
    }

    private void OnPreviousCalendarMonthTapped(object? sender, TappedEventArgs e)
    {
        selectedCalendarMonth = selectedCalendarMonth.AddMonths(-1);
        RenderCalendar();
    }

    private void OnNextCalendarMonthTapped(object? sender, TappedEventArgs e)
    {
        var next = selectedCalendarMonth.AddMonths(1);
        var currentMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (next > currentMonth)
        {
            return;
        }
        selectedCalendarMonth = next;
        RenderCalendar();
    }

    private async void OnRecordTapped(object? sender, TappedEventArgs e)
    {
        if (performance is null ||
            !int.TryParse(e.Parameter?.ToString(), out var recordId))
        {
            return;
        }
        var record = performance.Points.FirstOrDefault(item => item.Id == recordId);
        if (record is not null)
        {
            await OpenEditorAsync(record);
        }
    }

    private void OnRootSizeChanged(object? sender, EventArgs e)
    {
        if (PerformanceRoot.Width <= 0)
        {
            return;
        }
        PerformanceContent.WidthRequest = Math.Min(
            940,
            Math.Max(300, PerformanceRoot.Width));
        EditorContent.WidthRequest = Math.Min(
            680,
            Math.Max(300, PerformanceRoot.Width));
    }

    private string CompactMoney(long amountMinor)
    {
        var amount = Math.Abs(amountMinor) / 100d;
        var sign = amountMinor switch
        {
            > 0 => "+",
            < 0 => "−",
            _ => string.Empty
        };
        var value = amount >= 1000
            ? $"{amount / 1000:0.#}k"
            : $"{amount:0}";
        return $"{sign}{value}";
    }

    private Page? FindHostPage()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is Page page)
            {
                return page;
            }
            current = current.Parent;
        }
        return null;
    }

    private Color ChangeColor(long? value) => value switch
    {
        > 0 => PositiveColor,
        < 0 => NegativeColor,
        _ => SecondaryTextColor
    };

    private Color ChangeTint(long value) => value switch
    {
        > 0 => PositiveColor.WithAlpha(0.14f),
        < 0 => NegativeColor.WithAlpha(0.14f),
        _ => SurfaceMutedColor
    };

    private static Color AccentColor =>
        ThemeResourceBindings.GetColor("Accent", "#5044E4");

    private static Color PositiveColor => ThemeResourceBindings.GetColor(
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? "PositiveDark"
            : "PositiveLight",
        "#168A67");

    private static Color NegativeColor => ThemeResourceBindings.GetColor(
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? "NegativeDark"
            : "NegativeLight",
        "#C2415A");

    private static Color SecondaryTextColor => ThemeResourceBindings.GetColor(
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? "SecondaryTextDark"
            : "SecondaryTextLight",
        "#686273");

    private static Color SurfaceMutedColor => ThemeResourceBindings.GetColor(
        Application.Current?.RequestedTheme == AppTheme.Dark
            ? "SurfaceMutedDark"
            : "SurfaceMutedLight",
        "#F2EFEB");

    private sealed record RecordRowViewModel(
        int Id,
        string DayText,
        string MonthText,
        string TotalText,
        string DetailText,
        string ProfitText,
        string ChangeText,
        Color ProfitColor,
        Color ChangeColor,
        Color TintColor);
}
