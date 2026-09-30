using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class TransactionStatisticsDetailView
{
    private void RenderChart(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        chartDrawable.LineColor = ViewModel.IsIncome
            ? ThemeResourceBindings.GetThemeColor(
                "PositiveLight",
                "PositiveDark",
                "#16856B")
            : ThemeResourceBindings.GetThemeColor(
                "NegativeLight",
                "NegativeDark",
                "#C2415A");
        chartDrawable.LabelColor = ThemeResourceBindings.GetThemeColor(
            "SecondaryTextLight",
            "SecondaryTextDark",
            "#686273");
        chartDrawable.GridColor = ThemeResourceBindings.GetThemeColor(
            "DividerLight",
            "DividerDark",
            "#E9E3DB");
        chartDrawable.SetPoints(ViewModel.ChartPoints, animate);
        DetailChart.AbortAnimation(ChartAnimationName);

        if (!animate)
        {
            DetailChart.Invalidate();
            return;
        }

        DetailChart.Animate(
            ChartAnimationName,
            progress =>
            {
                chartDrawable.AnimationProgress = (float)progress;
                DetailChart.Invalidate();
            },
            0,
            1,
            16,
            500,
            Easing.CubicOut);
    }

    private void UpdateRangeTabs()
    {
        if (ViewModel is null)
        {
            return;
        }

        SetTabVisual(
            DailyRangeButton,
            DailyRangeLabel,
            ViewModel.SelectedRange == TransactionStatisticsDetailRange.Daily);
        SetTabVisual(
            MonthlyRangeButton,
            MonthlyRangeLabel,
            ViewModel.SelectedRange == TransactionStatisticsDetailRange.Monthly);
    }

    private static void SetTabVisual(Border border, Label label, bool isSelected)
    {
        border.Style = (Style)Application.Current!.Resources[
            isSelected ? "SelectedCategorySegmentTab" : "CategorySegmentTab"];
        label.Style = (Style)Application.Current.Resources[
            isSelected ? "SelectedCategorySegmentLabel" : "CategorySegmentLabel"];
    }
}
