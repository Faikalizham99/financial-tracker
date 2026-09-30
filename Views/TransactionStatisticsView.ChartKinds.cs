using FinancialTracker.Helpers;
using FinancialTracker.Models;
using Microsoft.Maui.Controls.Shapes;
using MauiPath = Microsoft.Maui.Controls.Shapes.Path;

namespace FinancialTracker.Views;

public partial class TransactionStatisticsView
{
    private void RenderChart(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        var chartKind = ViewModel.SelectedMode == TransactionStatisticsMode.All
            ? TransactionStatisticsChartKind.Bar
            : ViewModel.SelectedChartKind;
        MonthlyChart.IsVisible = chartKind == TransactionStatisticsChartKind.Bar;
        RunningTotalChart.IsVisible =
            chartKind == TransactionStatisticsChartKind.RunningTotal;
        CompositionChart.IsVisible =
            chartKind == TransactionStatisticsChartKind.Composition;
        UpdateChartKindTabs();

        switch (chartKind)
        {
            case TransactionStatisticsChartKind.RunningTotal:
                RenderRunningTotalChart(animate);
                break;
            case TransactionStatisticsChartKind.Composition:
                RenderCompositionChart(animate);
                break;
            default:
                RenderBarChart(animate);
                break;
        }
    }

    private void RenderRunningTotalChart(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        runningTotalDrawable.CurrentColor = ViewModel.SelectedMode ==
            TransactionStatisticsMode.Income
                ? ThemeResourceBindings.GetThemeColor(
                    "PositiveLight",
                    "PositiveDark",
                    "#16856B")
                : ThemeResourceBindings.GetThemeColor(
                    "NegativeLight",
                    "NegativeDark",
                    "#C2415A");
        runningTotalDrawable.PreviousColor = ThemeResourceBindings.GetThemeColor(
            "SecondaryTextLight",
            "SecondaryTextDark",
            "#777381").WithAlpha(0.72f);
        runningTotalDrawable.LabelColor = ThemeResourceBindings.GetThemeColor(
            "SecondaryTextLight",
            "SecondaryTextDark",
            "#686273");
        runningTotalDrawable.GridColor = ThemeResourceBindings.GetThemeColor(
            "DividerLight",
            "DividerDark",
            "#E9E3DB");
        runningTotalDrawable.SetPoints(ViewModel.RunningTotalPoints, animate);
        RunningTotalChart.AbortAnimation(ChartAnimationName);
        AnimateChart(
            RunningTotalChart,
            animate,
            progress => runningTotalDrawable.AnimationProgress = progress);
    }

    private void RenderCompositionChart(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        compositionDrawable.TrackColor = ThemeResourceBindings.GetThemeColor(
            "DividerLight",
            "DividerDark",
            "#E9E3DB");
        compositionDrawable.SurfaceColor = ThemeResourceBindings.GetThemeColor(
            "CardBackgroundLight",
            "CardBackgroundDark",
            "#FFFEFC");
        compositionDrawable.PrimaryTextColor = ThemeResourceBindings.GetThemeColor(
            "PrimaryTextLight",
            "PrimaryTextDark",
            "#17152C");
        compositionDrawable.SecondaryTextColor = ThemeResourceBindings.GetThemeColor(
            "SecondaryTextLight",
            "SecondaryTextDark",
            "#686273");
        compositionDrawable.TotalText = ViewModel.BreakdownTotalText;
        compositionDrawable.SetSlices(
            ViewModel.CompositionSlices,
            ViewModel.SelectedBreakdownKey,
            animate);
        CompositionChart.AbortAnimation(ChartAnimationName);
        AnimateChart(
            CompositionChart,
            animate,
            progress => compositionDrawable.AnimationProgress = progress);
    }

    private static void AnimateChart(
        GraphicsView chart,
        bool animate,
        Action<float> setProgress)
    {
        if (!animate)
        {
            setProgress(1f);
            chart.Invalidate();
            return;
        }

        chart.Animate(
            ChartAnimationName,
            progress =>
            {
                setProgress((float)progress);
                chart.Invalidate();
            },
            0,
            1,
            16,
            ChartAnimationLength,
            Easing.CubicOut);
    }

    private void AnimateCompositionSelection()
    {
        CompositionChart.AbortAnimation(CompositionSelectionAnimationName);
        compositionDrawable.SelectionProgress = 0f;
        CompositionChart.Animate(
            CompositionSelectionAnimationName,
            progress =>
            {
                compositionDrawable.SelectionProgress = (float)progress;
                CompositionChart.Invalidate();
            },
            0,
            1,
            16,
            240,
            Easing.CubicOut);
    }

    private void UpdateChartKindTabs()
    {
        if (ViewModel is null)
        {
            return;
        }

        SetChartKindTab(
            BarChartKindButton,
            BarChartKindIcon,
            ViewModel.SelectedChartKind == TransactionStatisticsChartKind.Bar,
            usesFill: true);
        SetChartKindTab(
            RunningChartKindButton,
            RunningChartKindIcon,
            ViewModel.SelectedChartKind == TransactionStatisticsChartKind.RunningTotal,
            usesFill: false);
        SetChartKindTab(
            CompositionChartKindButton,
            CompositionChartKindIcon,
            ViewModel.SelectedChartKind == TransactionStatisticsChartKind.Composition,
            usesFill: false);
    }

    private static void SetChartKindTab(
        Border border,
        MauiPath icon,
        bool isSelected,
        bool usesFill)
    {
        border.Style = (Style)Application.Current!.Resources[
            isSelected ? "FlatSelectedCategorySegmentTab" : "CategorySegmentTab"];
        var property = usesFill
            ? Shape.FillProperty
            : Shape.StrokeProperty;
        if (isSelected)
        {
            ThemeResourceBindings.SetDynamic(icon, property, "Accent");
        }
        else
        {
            ThemeResourceBindings.SetColor(
                icon,
                property,
                "SecondaryTextLight",
                "SecondaryTextDark");
        }
    }
}
