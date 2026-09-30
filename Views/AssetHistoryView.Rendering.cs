using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.Views.Drawables;
using Microsoft.Maui.Controls.Shapes;

namespace FinancialTracker.Views;

public partial class AssetHistoryView
{
    private readonly Dictionary<string, LegendRowVisuals> compositionLegendRows =
        new(StringComparer.Ordinal);

    private void RenderHistory(bool animateCharts)
    {
        if (history is null)
        {
            return;
        }

        UpdateKwspVisuals();
        UpdateRangeVisuals();
        ApplyChartTheme();

        var displayedMonths = BuildDisplayedMonths(history);
        var historyByMonth = history.Points.ToDictionary(
            item => MonthKeyConverter.FromDate(item.Month));
        var chartPoints = displayedMonths
            .Select(month =>
            {
                historyByMonth.TryGetValue(MonthKeyConverter.FromDate(month), out var point);
                return new AssetHistoryChartPoint(
                    month,
                    point is null
                        ? null
                        : includeKwsp ? point.TotalMinor : point.TotalWithoutKwspMinor);
            })
            .ToList();

        SetHistoryChartPoints(chartPoints, animateCharts);
        HistoryRangeCaption.Text = chartPoints.Count == 0
            ? string.Empty
            : $"{chartPoints[0].Month:MMM yyyy} – {chartPoints[^1].Month:MMM yyyy}";
        UpdateChartWidth(chartPoints.Count);
        RenderSelectedMonth(animateCharts);
        AnimateCharts(animateCharts, animatePortfolio: true, animateDonut: true);
        QueueChartScrollToSelection();
    }

    private IReadOnlyList<DateTime> BuildDisplayedMonths(AssetHistoryData value)
    {
        var endMonth = value.ThroughMonth;
        var startMonth = selectedRange switch
        {
            HistoryRange.SixMonths => endMonth.AddMonths(-5),
            HistoryRange.TwelveMonths => endMonth.AddMonths(-11),
            _ => value.Points.Count == 0
                ? endMonth
                : value.Points[0].Month
        };
        var count = Math.Max(
            1,
            ((endMonth.Year - startMonth.Year) * 12) + endMonth.Month - startMonth.Month + 1);
        return Enumerable.Range(0, count)
            .Select(startMonth.AddMonths)
            .ToList();
    }

    private void RenderSelectedMonth(bool animateDonut)
    {
        if (history is null)
        {
            return;
        }

        SelectedMonthLabel.Text = selectedMonth.ToString("MMMM yyyy").ToUpperInvariant();
        var point = history.Points.FirstOrDefault(
            item => MonthKeyConverter.FromDate(item.Month) == MonthKeyConverter.FromDate(selectedMonth));
        NoSnapshotLabel.IsVisible = point is null;
        SelectedSnapshotDetails.IsVisible = point is not null;
        CompositionCard.IsVisible = point is not null;
        SelectedSnapshotDateLabel.Text = point is null
            ? "No snapshot"
            : $"Snapshot · {point.EntryDate:dd MMMM yyyy}";

        if (point is null)
        {
            SelectedChangeLabel.Text = string.Empty;
            donutChartDrawable.SetSlices([], animate: false);
            DonutChart.Invalidate();
            return;
        }

        var previous = history.Points.LastOrDefault(item => item.Month < point.Month);
        var total = GetDisplayedTotal(point);
        var previousTotal = previous is null ? (long?)null : GetDisplayedTotal(previous);
        var change = previousTotal.HasValue ? total - previousTotal.Value : (long?)null;
        var percentage = change.HasValue && previousTotal is > 0
            ? change.Value * 100m / previousTotal.Value
            : (decimal?)null;
        var changeColor = GetChangeColor(change);

        SelectedTotalLabel.Text = MoneyFormatter.FormatMinor(total, history.CurrencySymbol);
        SelectedTotalLabel.FontSize = GetResponsiveTotalFontSize(SelectedTotalLabel.Text);
        SelectedAccessibleLabel.Text = MoneyFormatter.FormatMinor(
            point.AccessibleTotalMinor,
            history.CurrencySymbol);
        SelectedKwspLabel.Text = includeKwsp
            ? MoneyFormatter.FormatMinor(point.KwspTotalMinor, history.CurrencySymbol)
            : "Excluded";
        SelectedChangeAmountLabel.Text = change.HasValue
            ? MoneyFormatter.FormatMinor(change.Value, history.CurrencySymbol, showPositiveSign: true)
            : "Baseline";
        SelectedChangeAmountLabel.TextColor = changeColor;
        SelectedChangePercentageLabel.Text = percentage.HasValue
            ? $"{percentage.Value:+0.0;-0.0;0.0}%"
            : "—";
        SelectedChangePercentageLabel.TextColor = changeColor;
        SelectedChangeLabel.Text = percentage.HasValue
            ? $"{percentage.Value:+0.0;-0.0;0.0}%"
            : "Baseline";
        SelectedChangeLabel.TextColor = changeColor;

        var slices = BuildDonutSlices(point);
        donutChartDrawable.TotalText = MoneyFormatter.FormatMinor(total, history.CurrencySymbol);
        donutChartDrawable.SetSlices(slices, animateDonut);
        BuildCompositionLegend(slices, total);
        UpdateCompositionLegendSelection();
    }

    private IReadOnlyList<AssetDonutSlice> BuildDonutSlices(AssetHistoryPoint point)
    {
        var positiveAssets = point.Assets
            .Where(item => item.AmountMinor > 0)
            .Where(item => includeKwsp ||
                !item.Asset.Key.Equals(AssetCatalog.KwspKey, StringComparison.Ordinal))
            .OrderByDescending(item => item.AmountMinor)
            .ThenBy(item => item.Asset.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
        const int maximumNamedSlices = 6;
        var result = positiveAssets
            .Take(maximumNamedSlices)
            .Select(item => new AssetDonutSlice(
                item.Asset.Key,
                item.Asset.DisplayName,
                item.AmountMinor,
                AssetChartPalette.GetAssetColor(item.Asset.Key)))
            .ToList();
        var otherTotal = positiveAssets
            .Skip(maximumNamedSlices)
            .Sum(item => item.AmountMinor);
        if (otherTotal > 0)
        {
            result.Add(new AssetDonutSlice(
                "other",
                "Other",
                otherTotal,
                ThemeResourceBindings.GetThemeColor(
                    "SecondaryTextLight",
                    "SecondaryTextDark",
                    "#777381")));
        }

        return result;
    }

    private void BuildCompositionLegend(IReadOnlyList<AssetDonutSlice> slices, long total)
    {
        CompositionLegend.Children.Clear();
        compositionLegendRows.Clear();
        if (slices.Count == 0)
        {
            CompositionLegend.Children.Add(new Label
            {
                Text = "All assets are zero for this snapshot.",
                Style = (Style)Application.Current!.Resources["BodyMuted"]
            });
            return;
        }

        foreach (var slice in slices)
        {
            var colorDot = new Border
            {
                WidthRequest = 12,
                HeightRequest = 12,
                Padding = 0,
                BackgroundColor = slice.Color,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 6 },
                VerticalOptions = LayoutOptions.Center
            };
            var nameLabel = new Label
            {
                Text = slice.Label,
                FontFamily = "OpenSansSemibold",
                FontSize = 13,
                LineBreakMode = LineBreakMode.TailTruncation,
                MaxLines = 1,
                VerticalTextAlignment = TextAlignment.Center
            };
            var amountLabel = new Label
            {
                Text = MoneyFormatter.FormatMinor(slice.AmountMinor, history!.CurrencySymbol),
                FontFamily = "OpenSansSemibold",
                FontSize = 12,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center
            };
            var percentageLabel = new Label
            {
                Text = total > 0 ? $"{slice.AmountMinor * 100d / total:0.0}%" : "0.0%",
                FontSize = 11,
                HorizontalTextAlignment = TextAlignment.End,
                VerticalTextAlignment = TextAlignment.Center
            };
            percentageLabel.SetAppThemeColor(
                Label.TextColorProperty,
                Color.FromArgb("#686273"),
                Color.FromArgb("#BBB4C7"));
            var row = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(new GridLength(14)),
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(new GridLength(48))
                },
                ColumnSpacing = 8,
                Padding = new Thickness(8, 7)
            };
            row.Add(colorDot, 0);
            row.Add(nameLabel, 1);
            row.Add(amountLabel, 2);
            row.Add(percentageLabel, 3);
            var rowContainer = new Border
            {
                Padding = 0,
                BackgroundColor = Colors.Transparent,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 12 },
                Content = row
            };
            var gesture = new TapGestureRecognizer();
            gesture.Tapped += (_, _) => SelectDonutSlice(slice.Key);
            rowContainer.GestureRecognizers.Add(gesture);
            compositionLegendRows[slice.Key] = new LegendRowVisuals(
                rowContainer,
                nameLabel,
                amountLabel);
            CompositionLegend.Children.Add(rowContainer);
        }
    }

    private void SelectDonutSlice(string key)
    {
        var previousKey = donutChartDrawable.SelectedSlice?.Key;
        donutChartDrawable.SelectKey(key);
        AnimateDonutSelection(previousKey);
        UpdateCompositionLegendSelection();
    }

    private void AnimateDonutSelection(string? previousKey)
    {
        DonutChart.AbortAnimation(DonutSelectionAnimationName);
        var selectedKey = donutChartDrawable.SelectedSlice?.Key;
        if (selectedKey is null || selectedKey.Equals(previousKey, StringComparison.Ordinal))
        {
            donutChartDrawable.SelectionProgress = selectedKey is null ? 0f : 1f;
            DonutChart.Invalidate();
            return;
        }

        donutChartDrawable.SelectionProgress = 0f;
        DonutChart.Animate(
            DonutSelectionAnimationName,
            progress =>
            {
                donutChartDrawable.SelectionProgress = (float)progress;
                DonutChart.Invalidate();
            },
            length: 220,
            easing: Easing.CubicOut);
    }

    private void UpdateCompositionLegendSelection()
    {
        var selectedKey = donutChartDrawable.SelectedSlice?.Key;
        foreach (var (key, visuals) in compositionLegendRows)
        {
            var isSelected = key.Equals(selectedKey, StringComparison.Ordinal);
            if (isSelected)
            {
                ThemeResourceBindings.SetDynamic(
                    visuals.Container,
                    Border.BackgroundColorProperty,
                    "AccentTint");
                ThemeResourceBindings.SetDynamic(
                    visuals.NameLabel,
                    Label.TextColorProperty,
                    "Accent");
                ThemeResourceBindings.SetDynamic(
                    visuals.AmountLabel,
                    Label.TextColorProperty,
                    "Accent");
            }
            else
            {
                ThemeResourceBindings.SetStatic(
                    visuals.Container,
                    Border.BackgroundColorProperty,
                    Colors.Transparent);
                ThemeResourceBindings.SetColor(
                    visuals.NameLabel,
                    Label.TextColorProperty,
                    "PrimaryTextLight",
                    "PrimaryTextDark");
                ThemeResourceBindings.SetColor(
                    visuals.AmountLabel,
                    Label.TextColorProperty,
                    "PrimaryTextLight",
                    "PrimaryTextDark");
            }

            SemanticProperties.SetDescription(
                visuals.Container,
                isSelected
                    ? $"{visuals.NameLabel.Text}, selected"
                    : $"{visuals.NameLabel.Text}. Tap to select this asset.");
        }
    }

    private void ApplyChartTheme()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var secondary = ThemeResourceBindings.GetThemeColor(
            "SecondaryTextLight",
            "SecondaryTextDark",
            isDark ? "#BBB4C7" : "#686273");
        var divider = ThemeResourceBindings.GetThemeColor(
            "DividerLight",
            "DividerDark",
            isDark ? "#29292D" : "#E9E3DB");
        var primary = ThemeResourceBindings.GetThemeColor(
            "PrimaryTextLight",
            "PrimaryTextDark",
            isDark ? "#FBF7F0" : "#17142D");
        var cardSurface = ThemeResourceBindings.GetThemeColor(
            "CardBackgroundLight",
            "CardBackgroundDark",
            isDark ? "#0A0A0C" : "#FFFEFC");
        var accent = ThemeResourceBindings.GetColor("Accent", "#5044E4");
        barChartDrawable.UseDarkPalette = isDark;
        barChartDrawable.LabelColor = secondary;
        barChartDrawable.EmptyColor = divider;
        barChartDrawable.SelectionColor = accent;
        ApplyLineChartTheme(isDark, secondary, divider, accent);
        donutChartDrawable.TrackColor = divider;
        donutChartDrawable.SurfaceColor = cardSurface;
        donutChartDrawable.PrimaryTextColor = primary;
        donutChartDrawable.SecondaryTextColor = secondary;
    }

    private void UpdateRangeVisuals()
    {
        SetRangeVisual(SixMonthRangeButton, SixMonthRangeLabel, selectedRange == HistoryRange.SixMonths);
        SetRangeVisual(TwelveMonthRangeButton, TwelveMonthRangeLabel, selectedRange == HistoryRange.TwelveMonths);
        SetRangeVisual(AllRangeButton, AllRangeLabel, selectedRange == HistoryRange.All);
    }

    private static void SetRangeVisual(Border border, Label label, bool isSelected)
    {
        if (isSelected)
        {
            ThemeResourceBindings.SetDynamic(border, Border.BackgroundColorProperty, "Accent");
            ThemeResourceBindings.SetDynamic(label, Label.TextColorProperty, "AccentForeground");
            return;
        }

        ThemeResourceBindings.SetColor(
            border,
            Border.BackgroundColorProperty,
            "SurfaceMutedLight",
            "SurfaceMutedDark");
        ThemeResourceBindings.SetColor(
            label,
            Label.TextColorProperty,
            "PrimaryTextLight",
            "PrimaryTextDark");
    }

    private void UpdateKwspVisuals()
    {
        HistoryKwspModeLabel.Text = includeKwsp ? "With KWSP" : "Without KWSP";
        HistoryKwspExclusionSlash.IsVisible = !includeKwsp;
        if (includeKwsp)
        {
            ThemeResourceBindings.SetDynamic(
                HistoryKwspToggleButton,
                Border.BackgroundColorProperty,
                "Accent");
            ThemeResourceBindings.SetDynamic(
                HistoryKwspToggleIcon,
                Shape.StrokeProperty,
                "Accent");
        }
        else
        {
            ThemeResourceBindings.SetColor(
                HistoryKwspToggleButton,
                Border.BackgroundColorProperty,
                "SecondaryTextLight",
                "ControlDividerDark");
            ThemeResourceBindings.SetColor(
                HistoryKwspToggleIcon,
                Shape.StrokeProperty,
                "SecondaryTextLight",
                "SecondaryTextDark");
        }

        HistoryKwspToggleThumb.CancelAnimations();
        _ = HistoryKwspToggleThumb.TranslateToAsync(
            includeKwsp ? 22 : 0,
            0,
            150,
            Easing.CubicOut);
        SemanticProperties.SetDescription(
            HistoryKwspToggleButton,
            includeKwsp
                ? "KWSP included in asset history. Tap to exclude it."
                : "KWSP excluded from asset history. Tap to include it.");
    }

    private void AnimateCharts(bool animate, bool animatePortfolio, bool animateDonut)
    {
        DonutChart.AbortAnimation(DonutAnimationName);
        if (!animate)
        {
            AnimatePortfolioChart(animate: false);
            donutChartDrawable.AnimationProgress = 1f;
            DonutChart.Invalidate();
            return;
        }

        if (animatePortfolio)
        {
            AnimatePortfolioChart(animate: true);
        }
        else
        {
            AnimatePortfolioChart(animate: false);
        }

        if (animateDonut)
        {
            DonutChart.Animate(
                DonutAnimationName,
                progress =>
                {
                    donutChartDrawable.AnimationProgress = (float)progress;
                    DonutChart.Invalidate();
                },
                length: ChartAnimationLength,
                easing: Easing.CubicOut);
        }
        else
        {
            DonutChart.Invalidate();
        }
    }

    private void UpdateChartWidth(int pointCount)
    {
        var availableWidth = Math.Max(320d, HistoryChartScroll.Width);
        HistoryChartCanvas.WidthRequest = Math.Max(availableWidth, pointCount * 58d);
    }

    private long GetDisplayedTotal(AssetHistoryPoint point) =>
        includeKwsp ? point.TotalMinor : point.TotalWithoutKwspMinor;

    private static Color GetChangeColor(long? amount) => amount switch
    {
        > 0 => ThemeResourceBindings.GetThemeColor("PositiveLight", "PositiveDark", "#13866B"),
        < 0 => ThemeResourceBindings.GetThemeColor("NegativeLight", "NegativeDark", "#C2415A"),
        _ => ThemeResourceBindings.GetThemeColor("SecondaryTextLight", "SecondaryTextDark", "#777381")
    };

    private static double GetResponsiveTotalFontSize(string text)
    {
        if (DeviceInfo.Idiom != DeviceIdiom.Phone)
        {
            return 30;
        }

        return text.Length switch
        {
            > 20 => 21,
            > 16 => 24,
            > 13 => 27,
            _ => 30
        };
    }

    private sealed record LegendRowVisuals(
        Border Container,
        Label NameLabel,
        Label AmountLabel);
}
