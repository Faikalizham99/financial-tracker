using FinancialTracker.Helpers;
using FinancialTracker.Views.Drawables;
using Microsoft.Maui.Controls.Shapes;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;

namespace FinancialTracker.Views;

public partial class AssetHistoryView
{
    private const string LineAnimationName = "AssetHistoryLineAnimation";

    private enum HistoryChartKind
    {
        Bar,
        Line
    }

    private readonly AssetHistoryLineChartDrawable lineChartDrawable = new();
    private HistoryChartKind selectedChartKind = HistoryChartKind.Bar;

    private void InitializeChartKinds()
    {
        HistoryLineChart.Drawable = lineChartDrawable;
        UpdateHistoryChartKindVisuals();
    }

    private void ResetHistoryChartKind()
    {
        selectedChartKind = HistoryChartKind.Bar;
        UpdateHistoryChartKindVisuals();
    }

    private void SetHistoryChartPoints(
        IReadOnlyList<AssetHistoryChartPoint> points,
        bool animate)
    {
        var selectedKey = MonthKeyConverter.FromDate(selectedMonth);
        barChartDrawable.SelectedMonthKey = selectedKey;
        lineChartDrawable.SelectedMonthKey = selectedKey;
        barChartDrawable.SetPoints(
            points,
            animate && selectedChartKind == HistoryChartKind.Bar);
        lineChartDrawable.SetPoints(
            points,
            animate && selectedChartKind == HistoryChartKind.Line);
        UpdateHistoryChartKindVisuals();
    }

    private async void OnHistoryChartKindTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string parameter ||
            !Enum.TryParse<HistoryChartKind>(parameter, out var chartKind) ||
            selectedChartKind == chartKind)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        selectedChartKind = chartKind;
        var points = barChartDrawable.SourcePoints;
        if (chartKind == HistoryChartKind.Bar)
        {
            barChartDrawable.SetPoints(points, animate: true);
            barChartDrawable.SelectedMonthKey = MonthKeyConverter.FromDate(selectedMonth);
        }
        else
        {
            lineChartDrawable.SetPoints(points, animate: true);
            lineChartDrawable.SelectedMonthKey = MonthKeyConverter.FromDate(selectedMonth);
        }

        UpdateHistoryChartKindVisuals();
        AnimatePortfolioChart(animate: true);
        await feedback;
    }

    private void UpdateHistoryChartKindVisuals()
    {
        var showBar = selectedChartKind == HistoryChartKind.Bar;
        HistoryBarChart.IsVisible = showBar;
        HistoryLineChart.IsVisible = !showBar;
        SetHistoryChartKindTab(
            HistoryBarChartKindButton,
            HistoryBarChartKindIcon,
            showBar,
            usesFill: true);
        SetHistoryChartKindTab(
            HistoryLineChartKindButton,
            HistoryLineChartKindIcon,
            !showBar,
            usesFill: false);
    }

    private static void SetHistoryChartKindTab(
        Border border,
        MauiPath icon,
        bool isSelected,
        bool usesFill)
    {
        border.Style = (Style)Application.Current!.Resources[
            isSelected ? "FlatSelectedCategorySegmentTab" : "CategorySegmentTab"];
        var colorProperty = usesFill
            ? Shape.FillProperty
            : Shape.StrokeProperty;
        if (isSelected)
        {
            ThemeResourceBindings.SetDynamic(icon, colorProperty, "Accent");
            return;
        }

        ThemeResourceBindings.SetColor(
            icon,
            colorProperty,
            "SecondaryTextLight",
            "SecondaryTextDark");
    }

    private void ApplyLineChartTheme(
        bool isDark,
        Color labelColor,
        Color gridColor,
        Color accentColor)
    {
        lineChartDrawable.UseDarkPalette = isDark;
        lineChartDrawable.LineColor = accentColor;
        lineChartDrawable.LabelColor = labelColor;
        lineChartDrawable.GridColor = gridColor;
        lineChartDrawable.SelectionColor = accentColor;
        lineChartDrawable.SelectionBackgroundColor =
            ThemeResourceBindings.GetColor("AccentTint", "#E4F8FA");
    }

    private void AnimatePortfolioChart(bool animate)
    {
        HistoryBarChart.AbortAnimation(BarAnimationName);
        HistoryLineChart.AbortAnimation(LineAnimationName);
        if (!animate)
        {
            barChartDrawable.AnimationProgress = 1f;
            lineChartDrawable.AnimationProgress = 1f;
            HistoryBarChart.Invalidate();
            HistoryLineChart.Invalidate();
            return;
        }

        if (selectedChartKind == HistoryChartKind.Bar)
        {
            lineChartDrawable.AnimationProgress = 1f;
            HistoryLineChart.Invalidate();
            HistoryBarChart.Animate(
                BarAnimationName,
                progress =>
                {
                    barChartDrawable.AnimationProgress = (float)progress;
                    HistoryBarChart.Invalidate();
                },
                length: ChartAnimationLength,
                easing: Easing.CubicOut);
            return;
        }

        barChartDrawable.AnimationProgress = 1f;
        HistoryBarChart.Invalidate();
        HistoryLineChart.Animate(
            LineAnimationName,
            progress =>
            {
                lineChartDrawable.AnimationProgress = (float)progress;
                HistoryLineChart.Invalidate();
            },
            length: ChartAnimationLength,
            easing: Easing.CubicOut);
    }

    private void AbortHistoryChartAnimations()
    {
        HistoryBarChart.AbortAnimation(BarAnimationName);
        HistoryLineChart.AbortAnimation(LineAnimationName);
    }
}
