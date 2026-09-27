using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.Views.Drawables;
using Microsoft.Maui.Controls.Shapes;

namespace FinancialTracker.Views;

public partial class AssetsView : ContentView
{
    private const string TrendAnimationName = "AssetTrendAnimation";
    private const string ProgressAnimationName = "AssetProgressAnimation";
    private const uint ChartAnimationLength = 500;

    private enum AssetSortMode
    {
        Alphabetical,
        HighestFirst,
        LowestFirst
    }

    private readonly Dictionary<string, Entry> amountEntries = new(StringComparer.Ordinal);
    private readonly AssetTrendChartDrawable trendChartDrawable = new();
    private readonly AnimatedProgressBarDrawable accessibleShareDrawable = new();
    private AssetPortfolioService? portfolioService;
    private Func<CurrencyOption>? currencyProvider;
    private DateTime selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private AssetPortfolio? portfolio;
    private string editorCurrencyCode = "MYR";
    private string editorCurrencySymbol = "RM";
    private DateTime selectedEntryDate = DateTime.Today;
    private bool hasLoaded;
    private bool includeKwsp = true;
    private bool isNormalizingAmountText;
    private AssetSortMode assetSortMode = AssetSortMode.Alphabetical;
    private bool isLoading;
    private bool isLoadingEditor;

    public AssetsView()
    {
        InitializeComponent();
        TrendChart.Drawable = trendChartDrawable;
        AccessibleShareChart.Drawable = accessibleShareDrawable;
    }

    public Func<DateTime, DateTime, DateTime, Task<DateTime?>>? DatePickerRequested { get; set; }

    public event Action<bool>? EditorVisibilityChanged;

    public void Configure(
        AssetPortfolioService service,
        Func<CurrencyOption> selectedCurrencyProvider)
    {
        portfolioService = service;
        currencyProvider = selectedCurrencyProvider;
    }

    public async Task RefreshAsync()
    {
        hasLoaded = false;
        await LoadAsync();
    }

    public Task EnsureLoadedAsync() => hasLoaded ? Task.CompletedTask : LoadAsync();

    public void InvalidateCurrency() => hasLoaded = false;

    public bool HasOpenOverlay => EditorOverlay.IsVisible || MonthPicker.IsOpen;

    public async Task HandleBackAsync()
    {
        if (MonthPicker.IsOpen)
        {
            await MonthPicker.DismissAsync();
            return;
        }

        if (EditorOverlay.IsVisible)
        {
            CloseEditor();
        }
    }

    private async Task LoadAsync()
    {
        if (portfolioService is null || currencyProvider is null || isLoading)
        {
            return;
        }

        isLoading = true;
        LoadingOverlay.IsVisible = true;
        try
        {
            MonthLabel.Text = selectedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            portfolio = await portfolioService.GetPortfolioAsync(selectedMonth, currencyProvider());
            RenderPortfolio(portfolio, animateCharts: true);
            hasLoaded = true;
        }
        catch
        {
            EmptyCard.IsVisible = true;
            PortfolioContent.IsVisible = false;
            EmptyMessageLabel.Text = "Asset data could not be loaded. Please try again.";
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
            isLoading = false;
        }
    }

    private void RenderPortfolio(AssetPortfolio value, bool animateCharts)
    {
        EditActionLabel.Text = value.HasSnapshot ? "Edit snapshot" : "Add snapshot";
        EmptyCard.IsVisible = !value.HasSnapshot;
        PortfolioContent.IsVisible = value.HasSnapshot;
        EmptyMessageLabel.Text = $"Record your {selectedMonth:MMMM yyyy} asset values.";
        if (!value.HasSnapshot)
        {
            return;
        }

        var total = includeKwsp ? value.TotalMinor : value.TotalWithoutKwspMinor;
        var totalChange = includeKwsp ? value.TotalChangeMinor : value.TotalWithoutKwspChangeMinor;
        var totalChangePercentage = includeKwsp
            ? value.TotalChangePercentage
            : value.TotalWithoutKwspChangePercentage;
        var visibleComparisons = includeKwsp
            ? value.Comparisons
            : value.Comparisons
                .Where(item => !item.Asset.Key.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
                .ToList();
        var comparisons = assetSortMode switch
        {
            AssetSortMode.HighestFirst => visibleComparisons
                .OrderByDescending(item => item.CurrentAmountMinor)
                .ThenBy(item => item.Asset.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            AssetSortMode.LowestFirst => visibleComparisons
                .OrderBy(item => item.CurrentAmountMinor)
                .ThenBy(item => item.Asset.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList(),
            _ => visibleComparisons
                .OrderBy(item => item.Asset.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList()
        };
        var trend = includeKwsp ? value.Trend : value.TrendWithoutKwsp;

        TotalModeLabel.Text = includeKwsp ? "TOTAL ASSETS" : "TOTAL ASSETS · EXCLUDING KWSP";
        KwspModeLabel.Text = includeKwsp ? "With KWSP" : "Without KWSP";
        TotalLabel.Text = MoneyFormatter.FormatMinor(total, value.CurrencySymbol);
        TotalLabel.FontSize = GetPortfolioTotalFontSize(TotalLabel.Text);
        SnapshotDateLabel.Text = value.PreviousEntryDate.HasValue
            ? $"{value.PreviousEntryDate:dd MMM yyyy} → {value.EntryDate:dd MMM yyyy}"
            : $"Baseline · {value.EntryDate:dd MMM yyyy}";
        TotalChangeLabel.Text = FormatChange(totalChange, totalChangePercentage, value.CurrencySymbol);
        TotalChangeLabel.TextColor = GetChangeColor(totalChange);
        AccessibleLabel.Text = MoneyFormatter.FormatMinor(value.AccessibleTotalMinor, value.CurrencySymbol);
        AccessibleChangeLabel.Text = FormatChange(value.AccessibleChangeMinor, null, value.CurrencySymbol);
        AccessibleChangeLabel.TextColor = GetChangeColor(value.AccessibleChangeMinor);
        var accessibleShare = total > 0
            ? Math.Clamp((double)value.AccessibleTotalMinor / total, 0d, 1d)
            : 0d;
        AccessibleShareLabel.Text = $"{accessibleShare:P0}";
        BindingContext = new
        {
            Comparisons = comparisons.Select(item => new
            {
                Name = item.Asset.DisplayName,
                IconAsset = item.Asset.IconAsset,
                CurrentText = MoneyFormatter.FormatMinor(item.CurrentAmountMinor, value.CurrencySymbol),
                ComparisonText = item.PreviousAmountMinor.HasValue
                    ? $"Previous {MoneyFormatter.FormatMinor(item.PreviousAmountMinor.Value, value.CurrencySymbol)}"
                    : "No earlier value",
                ChangeText = item.IsNew
                    ? "New"
                    : FormatChange(item.ChangeMinor, item.ChangePercentage, value.CurrencySymbol),
                ChangeColor = GetChangeColor(item.ChangeMinor)
            }).ToList()
        };
        TrendLabel.Text = trend.Count switch
        {
            0 => "Add more snapshots to build a trend.",
            1 => $"{trend[0].Month:MMM yyyy}: {MoneyFormatter.FormatMinor(trend[0].TotalMinor, value.CurrencySymbol)}",
            _ => $"{trend[0].Month:MMM yyyy} → {trend[^1].Month:MMM yyyy}\n" +
                $"{MoneyFormatter.FormatMinor(trend[0].TotalMinor, value.CurrencySymbol)} → {MoneyFormatter.FormatMinor(trend[^1].TotalMinor, value.CurrencySymbol)}"
        };
        RenderCharts(trend, accessibleShare, animateCharts);
        InsightLabel.Text = includeKwsp ? value.InsightText : value.InsightWithoutKwspText;
    }

    private async void OnAssetSortTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        assetSortMode = assetSortMode switch
        {
            AssetSortMode.Alphabetical => AssetSortMode.HighestFirst,
            AssetSortMode.HighestFirst => AssetSortMode.LowestFirst,
            _ => AssetSortMode.Alphabetical
        };
        AssetSortLabel.Text = assetSortMode switch
        {
            AssetSortMode.HighestFirst => "Highest first",
            AssetSortMode.LowestFirst => "Lowest first",
            _ => "A–Z"
        };
        SemanticProperties.SetDescription(
            sender as BindableObject,
            assetSortMode switch
            {
                AssetSortMode.HighestFirst => "Assets sorted from highest value to lowest. Tap to change the order.",
                AssetSortMode.LowestFirst => "Assets sorted from lowest value to highest. Tap to change the order.",
                _ => "Assets sorted alphabetically. Tap to change the order."
            });

        if (portfolio is not null)
        {
            RenderPortfolio(portfolio, animateCharts: false);
        }

        await feedback;
    }

    private void OnKwspToggleTapped(object? sender, TappedEventArgs e)
    {
        includeKwsp = !includeKwsp;
        UpdateKwspToggleVisuals();
        if (portfolio is not null)
        {
            RenderPortfolio(portfolio, animateCharts: true);
        }
    }

    private void UpdateKwspToggleVisuals()
    {
        KwspExclusionSlash.IsVisible = !includeKwsp;

        if (includeKwsp)
        {
            ThemeResourceBindings.SetDynamic(
                KwspToggleButton,
                BackgroundColorProperty,
                "Accent");
            ThemeResourceBindings.SetDynamic(
                KwspToggleIcon,
                Shape.StrokeProperty,
                "Accent");
        }
        else
        {
            ThemeResourceBindings.SetColor(
                KwspToggleButton,
                BackgroundColorProperty,
                "SecondaryTextLight",
                "ControlDividerDark");
            ThemeResourceBindings.SetColor(
                KwspToggleIcon,
                Shape.StrokeProperty,
                "SecondaryTextLight",
                "SecondaryTextDark");
        }

        KwspToggleThumb.CancelAnimations();
        _ = KwspToggleThumb.TranslateToAsync(
            includeKwsp ? 22 : 0,
            0,
            150,
            Easing.CubicOut);
        SemanticProperties.SetDescription(
            KwspToggleButton,
            includeKwsp
                ? "KWSP included in portfolio totals. Tap to exclude it."
                : "KWSP excluded from portfolio totals. Tap to include it.");
    }

    private void RenderCharts(
        IReadOnlyList<AssetTrendPoint> points,
        double accessibleShare,
        bool animate)
    {
        TrendChart.AbortAnimation(TrendAnimationName);
        AccessibleShareChart.AbortAnimation(ProgressAnimationName);

        var accent = GetResourceColor("Accent", "#5044E4");
        var isDarkTheme = Application.Current?.RequestedTheme == AppTheme.Dark;
        trendChartDrawable.BarColor = accent;
        trendChartDrawable.LabelColor = GetResourceColor(
            isDarkTheme ? "SecondaryTextDark" : "SecondaryTextLight",
            isDarkTheme ? "#BBB4C7" : "#686273");
        accessibleShareDrawable.ProgressColor = accent;
        accessibleShareDrawable.TrackColor = GetResourceColor(
            isDarkTheme ? "DividerDark" : "DividerLight",
            isDarkTheme ? "#29292D" : "#E9E3DB");

        trendChartDrawable.SetPoints(points, animate);
        var animateAccessibleShare = accessibleShareDrawable.SetProgress(accessibleShare, animate);

        if (!animate)
        {
            TrendChart.Invalidate();
            AccessibleShareChart.Invalidate();
            return;
        }

        TrendChart.Animate(
            TrendAnimationName,
            progress =>
            {
                trendChartDrawable.AnimationProgress = (float)progress;
                TrendChart.Invalidate();
            },
            length: ChartAnimationLength,
            easing: Easing.CubicOut);
        if (animateAccessibleShare)
        {
            AccessibleShareChart.Animate(
                ProgressAnimationName,
                progress =>
                {
                    accessibleShareDrawable.AnimationProgress = (float)progress;
                    AccessibleShareChart.Invalidate();
                },
                length: ChartAnimationLength,
                easing: Easing.CubicOut);
        }
        else
        {
            AccessibleShareChart.Invalidate();
        }
    }

    private static Color GetResourceColor(string key, string fallback) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Color.FromArgb(fallback);

    private async void OnPreviousMonthTapped(object? sender, TappedEventArgs e)
    {
        selectedMonth = selectedMonth.AddMonths(-1);
        hasLoaded = false;
        await LoadAsync();
    }

    private async void OnNextMonthTapped(object? sender, TappedEventArgs e)
    {
        selectedMonth = selectedMonth.AddMonths(1);
        hasLoaded = false;
        await LoadAsync();
    }

    private async void OnMonthTapped(object? sender, TappedEventArgs e)
    {
        var result = await MonthPicker.PickAsync(
            selectedMonth,
            Math.Max(1900, selectedMonth.Year - 100),
            selectedMonth.Year + 100);
        if (result.HasValue)
        {
            selectedMonth = new DateTime(result.Value.Year, result.Value.Month, 1);
            hasLoaded = false;
            await LoadAsync();
        }
    }

    private async void OnEditTapped(object? sender, TappedEventArgs e) => await OpenEditorAsync();

    private async Task OpenEditorAsync()
    {
        if (portfolioService is null || currencyProvider is null || isLoadingEditor)
        {
            return;
        }

        isLoadingEditor = true;
        SaveButton.IsEnabled = false;
        try
        {
            EditorErrorLabel.IsVisible = false;
            EditorMonthLabel.Text = selectedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);
            var monthEnd = selectedMonth.AddMonths(1).AddDays(-1);
            selectedEntryDate = DateTime.Today >= selectedMonth && DateTime.Today <= monthEnd
                ? DateTime.Today
                : monthEnd;

            var existing = await portfolioService.GetSnapshotAsync(selectedMonth);
            EditorTitleLabel.Text = existing is null
                ? "Add asset snapshot"
                : "Edit asset snapshot";
            var selectedCurrency = currencyProvider();
            editorCurrencyCode = selectedCurrency.Code;
            editorCurrencySymbol = selectedCurrency.Symbol;
            if (existing is not null)
            {
                selectedEntryDate = existing.Snapshot.EntryDate;
            }
            UpdateEntryDateLabel();

            BuildEditorRows(existing);
            UpdateEditorTotal();
            SetEditorVisibility(true);
            QueueEditorScrollReset();
        }
        finally
        {
            isLoadingEditor = false;
            SaveButton.IsEnabled = true;
        }
    }

    private void BuildEditorRows(AssetSnapshotData? existing)
    {
        EditorRows.Children.Clear();
        amountEntries.Clear();
        var amountColumnWidth = DeviceInfo.Idiom == DeviceIdiom.Phone ? 168d : 190d;
        var existingValues = existing?.Values.ToDictionary(item => item.AssetKey)
            ?? new Dictionary<string, AssetSnapshotValueRecord>();

        foreach (var asset in AssetCatalog.ActiveItems)
        {
            var image = new Image { Source = asset.IconAsset, Aspect = Aspect.AspectFill };
            var imageBorder = new Border
            {
                WidthRequest = 48,
                HeightRequest = 48,
                Padding = 0,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 13 },
                Content = image
            };
            var title = new Label
            {
                Text = asset.DisplayName,
                FontFamily = "OpenSansSemibold",
                FontSize = 16,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                VerticalTextAlignment = TextAlignment.Center
            };
            var entry = new Entry
            {
                Placeholder = "0.00",
                Keyboard = Keyboard.Numeric,
                HorizontalTextAlignment = TextAlignment.End,
                ClearButtonVisibility = ClearButtonVisibility.WhileEditing,
                FontFamily = "OpenSansSemibold",
                FontSize = 15,
                MaxLength = 18,
                MinimumHeightRequest = 48
            };
            entry.SetAppThemeColor(
                Entry.TextColorProperty,
                Color.FromArgb("#17152C"),
                Color.FromArgb("#F4F0F7"));
            entry.SetAppThemeColor(
                Entry.PlaceholderColorProperty,
                Color.FromArgb("#777381"),
                Color.FromArgb("#AAA5B3"));
            entry.Text = existingValues.TryGetValue(asset.Key, out var value)
                ? MoneyFormatter.FormatMinorValue(value.AmountMinor).Replace(",", string.Empty, StringComparison.Ordinal)
                : "0.00";
            entry.Focused += OnAmountEntryFocused;
            entry.Unfocused += OnAmountEntryUnfocused;
            entry.TextChanged += OnAmountTextChanged;
            amountEntries[asset.Key] = entry;
            var currencyLabel = new Label
            {
                Text = editorCurrencySymbol,
                FontFamily = "OpenSansSemibold",
                FontSize = 14,
                VerticalTextAlignment = TextAlignment.Center
            };
            var amountGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star)
                },
                ColumnSpacing = 6
            };
            amountGrid.Add(currencyLabel, 0);
            amountGrid.Add(entry, 1);
            var amountBorder = new Border
            {
                Padding = new Thickness(11, 0),
                BackgroundColor = Application.Current?.Resources["SurfaceMutedLight"] as Color,
                StrokeThickness = 0,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 14 },
                Content = amountGrid
            };
            amountBorder.SetAppThemeColor(
                Border.BackgroundColorProperty,
                Color.FromArgb("#F2EFEB"),
                Color.FromArgb("#242426"));
            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(50)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(new GridLength(amountColumnWidth))
                },
                ColumnSpacing = 12
            };
            grid.Add(imageBorder, 0);
            grid.Add(title, 1);
            grid.Add(amountBorder, 2);
            var border = new Border
            {
                Padding = 14,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 },
                Content = grid
            };
            border.SetAppThemeColor(Border.BackgroundColorProperty, Color.FromArgb("#FFFCFA"), Color.FromArgb("#101012"));
            border.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#E8E1DA"), Color.FromArgb("#333238"));
            EditorRows.Children.Add(border);
        }
    }

    private void OnAmountTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!isNormalizingAmountText && sender is Entry entry)
        {
            var sanitized = SanitizeAmountText(e.NewTextValue);
            if (!string.Equals(sanitized, e.NewTextValue, StringComparison.Ordinal))
            {
                isNormalizingAmountText = true;
                entry.Text = sanitized;
                entry.CursorPosition = sanitized.Length;
                isNormalizingAmountText = false;
            }
        }

        UpdateEditorTotal();
    }

    private void OnAmountEntryFocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry &&
            decimal.TryParse(
                entry.Text,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount) &&
            amount == 0)
        {
            entry.Text = string.Empty;
        }
    }

    private void OnAmountEntryUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is Entry entry && string.IsNullOrWhiteSpace(entry.Text))
        {
            entry.Text = "0.00";
        }
    }

    private static string SanitizeAmountText(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        var result = new System.Text.StringBuilder(value.Length);
        var hasDecimalPoint = false;
        var decimalDigits = 0;

        foreach (var character in value)
        {
            if (character is >= '0' and <= '9')
            {
                if (!hasDecimalPoint || decimalDigits < 2)
                {
                    result.Append(character);
                    if (hasDecimalPoint)
                    {
                        decimalDigits++;
                    }
                }

                continue;
            }

            if (character == '.' && !hasDecimalPoint)
            {
                if (result.Length == 0)
                {
                    result.Append('0');
                }

                result.Append('.');
                hasDecimalPoint = true;
            }
        }

        return result.ToString();
    }

    private async void OnCopyPreviousTapped(object? sender, TappedEventArgs e)
    {
        if (portfolioService is null)
        {
            return;
        }

        var previousMonth = selectedMonth.AddMonths(-1);
        var previous = await portfolioService.GetPreviousMonthSnapshotAsync(selectedMonth);
        if (previous is null)
        {
            ShowEditorError($"There is no {previousMonth:MMMM yyyy} snapshot to copy.");
            return;
        }

        var values = previous.Values.ToDictionary(item => item.AssetKey);
        foreach (var item in amountEntries)
        {
            item.Value.Text = values.TryGetValue(item.Key, out var value)
                ? MoneyFormatter.FormatMinorValue(value.AmountMinor).Replace(",", string.Empty, StringComparison.Ordinal)
                : "0.00";
        }
        EditorErrorLabel.IsVisible = false;
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        if (portfolioService is null || currencyProvider is null)
        {
            ShowEditorError("The asset service is not ready. Close this page and try again.");
            return;
        }

        if (!TryReadAmounts(out var amounts))
        {
            return;
        }

        SaveButton.IsEnabled = false;
        SaveButton.Text = "Saving…";
        ShowEditorProgress("Saving snapshot…");
        try
        {
            await portfolioService.SaveAsync(
                selectedMonth,
                selectedEntryDate,
                editorCurrencyCode,
                amounts);
            SetEditorVisibility(false);
            hasLoaded = false;
            await LoadAsync();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Asset snapshot save failed: {exception}");
            var reason = string.IsNullOrWhiteSpace(exception.Message)
                ? string.Empty
                : $" {exception.Message}";
            ShowEditorError($"The snapshot could not be saved.{reason}");
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveButton.Text = "Save snapshot";
        }
    }

    private bool TryReadAmounts(out IReadOnlyDictionary<string, long> amounts)
    {
        var result = new Dictionary<string, long>(StringComparer.Ordinal);
        foreach (var item in amountEntries)
        {
            if (!decimal.TryParse(item.Value.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) &&
                !decimal.TryParse(item.Value.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                ShowEditorError("Enter a valid value for every asset. Use 0.00 when an asset has no value.");
                amounts = result;
                return false;
            }

            if (amount < 0 || amount > long.MaxValue / 100m)
            {
                ShowEditorError("Asset values must be zero or greater.");
                amounts = result;
                return false;
            }
            result[item.Key] = decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        }
        amounts = result;
        EditorErrorLabel.IsVisible = false;
        return true;
    }

    private void UpdateEditorTotal()
    {
        long total = 0;
        foreach (var entry in amountEntries.Values)
        {
            if (decimal.TryParse(entry.Text, NumberStyles.Number, CultureInfo.CurrentCulture, out var amount) ||
                decimal.TryParse(entry.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out amount))
            {
                total += decimal.ToInt64(decimal.Round(Math.Max(0, amount) * 100m, 0, MidpointRounding.AwayFromZero));
            }
        }
        EditorTotalLabel.Text = MoneyFormatter.FormatMinor(total, editorCurrencySymbol);
    }

    private void ShowEditorError(string message)
    {
        EditorErrorLabel.Text = message;
        ThemeResourceBindings.SetColor(
            EditorErrorLabel,
            Label.TextColorProperty,
            "NegativeLight",
            "NegativeDark");
        EditorErrorLabel.IsVisible = true;
    }

    private void ShowEditorProgress(string message)
    {
        EditorErrorLabel.Text = message;
        ThemeResourceBindings.SetDynamic(
            EditorErrorLabel,
            Label.TextColorProperty,
            "Accent");
        EditorErrorLabel.IsVisible = true;
    }

    private void OnCloseEditorTapped(object? sender, TappedEventArgs e) => CloseEditor();

    private async void OnPreviousEditorMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeEditorMonthAsync(selectedMonth.AddMonths(-1), sender);

    private async void OnNextEditorMonthTapped(object? sender, TappedEventArgs e) =>
        await ChangeEditorMonthAsync(selectedMonth.AddMonths(1), sender);

    private async void OnEditorMonthTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        var result = await MonthPicker.PickAsync(
            selectedMonth,
            DateRangeLimits.MinimumYear,
            DateRangeLimits.MaximumYear);
        if (result.HasValue)
        {
            await ChangeEditorMonthAsync(
                new DateTime(result.Value.Year, result.Value.Month, 1),
                sender: null);
        }
        await feedback;
    }

    private async Task ChangeEditorMonthAsync(DateTime month, object? sender)
    {
        if (isLoadingEditor ||
            month.Year < DateRangeLimits.MinimumYear ||
            month.Year > DateRangeLimits.MaximumYear)
        {
            return;
        }

        var feedback = sender is null
            ? Task.CompletedTask
            : InteractionAnimations.PulseAsync(sender);
        foreach (var entry in amountEntries.Values)
        {
            entry.Unfocus();
        }
        selectedMonth = new DateTime(month.Year, month.Month, 1);
        hasLoaded = false;
        await OpenEditorAsync();
        await feedback;
    }

    private async void OnEntryDateTapped(object? sender, TappedEventArgs e)
    {
        if (DatePickerRequested is null)
        {
            return;
        }

        var selectedDate = await DatePickerRequested(
            selectedEntryDate,
            DateRangeLimits.MinimumDate,
            DateRangeLimits.MaximumDate);
        if (selectedDate.HasValue)
        {
            selectedEntryDate = selectedDate.Value.Date;
            UpdateEntryDateLabel();
        }
    }

    private void UpdateEntryDateLabel() =>
        EntryDateLabel.Text = selectedEntryDate.ToString("dd MMMM yyyy", CultureInfo.CurrentCulture);

    private void OnEditorOverlaySizeChanged(object? sender, EventArgs e)
    {
        if (EditorOverlay.Width > 0)
        {
            EditorContent.WidthRequest = Math.Min(760, Math.Max(280, EditorOverlay.Width));
        }
    }

    private void QueueEditorScrollReset()
    {
        Dispatcher.Dispatch(() => _ = ResetEditorScrollAsync());
    }

    private async Task ResetEditorScrollAsync()
    {
        try
        {
            await Task.Yield();
            await EditorAssetsScrollView.ScrollToAsync(0, 0, false);
        }
        catch (ObjectDisposedException)
        {
            // The editor may close while the non-blocking reset is queued.
        }
    }

    private void CloseEditor()
    {
        foreach (var entry in amountEntries.Values)
        {
            entry.Unfocus();
        }
        SetEditorVisibility(false);
        hasLoaded = false;
        _ = LoadAsync();
    }

    private void SetEditorVisibility(bool isVisible)
    {
        if (EditorOverlay.IsVisible == isVisible)
        {
            return;
        }

        if (isVisible)
        {
            SaveButton.IsEnabled = true;
            SaveButton.InputTransparent = false;
        }

        EditorOverlay.IsVisible = isVisible;
        EditorVisibilityChanged?.Invoke(isVisible);
    }

    private static string FormatChange(long? amount, decimal? percentage, string symbol)
    {
        if (!amount.HasValue)
        {
            return "Baseline";
        }
        var text = MoneyFormatter.FormatMinor(amount.Value, symbol, showPositiveSign: true);
        return percentage.HasValue ? $"{text} ({percentage:+0.0;-0.0;0.0}%)" : text;
    }

    private static Color GetChangeColor(long? amount) => amount switch
    {
        > 0 => Color.FromArgb("#13866B"),
        < 0 => Color.FromArgb("#C2415A"),
        _ => Color.FromArgb("#777381")
    };

    private static double GetPortfolioTotalFontSize(string text)
    {
        if (DeviceInfo.Idiom != DeviceIdiom.Phone)
        {
            return 34;
        }

        return text.Length switch
        {
            > 20 => 24,
            > 16 => 28,
            > 13 => 31,
            _ => 34
        };
    }
}
