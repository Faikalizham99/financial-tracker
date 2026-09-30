using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class TransactionStatisticsView
{
    private void RenderStatistics(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        UpdateModeTabs();
        UpdateBreakdownTabs();
        UpdateHeroVisuals();
        RenderChart(animate);
        RenderSummaryProgress(animate);
    }

    private void RenderBarChart(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        chartDrawable.PositiveColor = ViewModel.SelectedMode ==
            TransactionStatisticsMode.Expense
                ? ThemeResourceBindings.GetThemeColor(
                    "NegativeLight",
                    "NegativeDark",
                    "#C2415A")
                : ThemeResourceBindings.GetThemeColor(
                    "PositiveLight",
                    "PositiveDark",
                    "#16856B");
        chartDrawable.NegativeColor = ThemeResourceBindings.GetThemeColor(
            "NegativeLight",
            "NegativeDark",
            "#C2415A");
        chartDrawable.LabelColor = ThemeResourceBindings.GetThemeColor(
            "SecondaryTextLight",
            "SecondaryTextDark",
            "#686273");
        chartDrawable.AxisColor = ThemeResourceBindings.GetThemeColor(
            "DividerLight",
            "DividerDark",
            "#E9E3DB");
        chartDrawable.SelectionColor = ThemeResourceBindings.GetColor(
            "Accent",
            "#5044E4");
        chartDrawable.SelectionBackgroundColor = ThemeResourceBindings.GetColor(
            "AccentTint",
            "#E4F8FA");
        chartDrawable.SelectedMonthKey = ViewModel.HighlightedMonthKey;
        chartDrawable.SetPoints(ViewModel.ChartPoints, animate);
        MonthlyChart.AbortAnimation(ChartAnimationName);

        if (!animate)
        {
            MonthlyChart.Invalidate();
            return;
        }

        MonthlyChart.Animate(
            ChartAnimationName,
            progress =>
            {
                chartDrawable.AnimationProgress = (float)progress;
                MonthlyChart.Invalidate();
            },
            0,
            1,
            16,
            ChartAnimationLength,
            Easing.CubicOut);
    }

    private void RenderSummaryProgress(bool animate)
    {
        if (ViewModel is null)
        {
            return;
        }

        var trackColor = ThemeResourceBindings.GetThemeColor(
            "SurfaceMutedLight",
            "SurfaceMutedDark",
            "#F1EDE7");
        incomeProgressDrawable.TrackColor = trackColor;
        expenseProgressDrawable.TrackColor = trackColor;
        incomeProgressDrawable.ProgressColor = ThemeResourceBindings.GetThemeColor(
            "PositiveLight",
            "PositiveDark",
            "#16856B");
        expenseProgressDrawable.ProgressColor = ThemeResourceBindings.GetThemeColor(
            "NegativeLight",
            "NegativeDark",
            "#C2415A");

        var animateIncome = incomeProgressDrawable.SetProgress(
            ViewModel.IncomeProgress,
            animate);
        var animateExpense = expenseProgressDrawable.SetProgress(
            ViewModel.ExpenseProgress,
            animate);
        IncomeProgressBar.AbortAnimation(ProgressAnimationName);
        ExpenseProgressBar.AbortAnimation(ProgressAnimationName);

        if (!animateIncome)
        {
            IncomeProgressBar.Invalidate();
        }
        else
        {
            IncomeProgressBar.Animate(
                ProgressAnimationName,
                progress =>
                {
                    incomeProgressDrawable.AnimationProgress = (float)progress;
                    IncomeProgressBar.Invalidate();
                },
                0,
                1,
                16,
                ChartAnimationLength,
                Easing.CubicOut);
        }

        if (!animateExpense)
        {
            ExpenseProgressBar.Invalidate();
        }
        else
        {
            ExpenseProgressBar.Animate(
                ProgressAnimationName,
                progress =>
                {
                    expenseProgressDrawable.AnimationProgress = (float)progress;
                    ExpenseProgressBar.Invalidate();
                },
                0,
                1,
                16,
                ChartAnimationLength,
                Easing.CubicOut);
        }
    }

    private void UpdateHeroVisuals()
    {
        if (ViewModel is null)
        {
            return;
        }

        if (ViewModel.HeroIsPositive)
        {
            ThemeResourceBindings.SetColor(
                HeroAmountLabel,
                Label.TextColorProperty,
                "PositiveLight",
                "PositiveDark");
        }
        else if (ViewModel.HeroIsNegative)
        {
            ThemeResourceBindings.SetColor(
                HeroAmountLabel,
                Label.TextColorProperty,
                "NegativeLight",
                "NegativeDark");
        }
        else
        {
            ThemeResourceBindings.SetColor(
                HeroAmountLabel,
                Label.TextColorProperty,
                "PrimaryTextLight",
                "PrimaryTextDark");
        }

        HeroAmountLabel.FontSize = DeviceInfo.Idiom == DeviceIdiom.Phone &&
            ViewModel.HeroAmountText.Length > 15
                ? 27
                : DeviceInfo.Idiom == DeviceIdiom.Phone ? 32 : 38;
    }

    private void UpdateModeTabs()
    {
        if (ViewModel is null)
        {
            return;
        }

        SetTabVisual(
            AllModeButton,
            AllModeLabel,
            ViewModel.SelectedMode == TransactionStatisticsMode.All);
        SetTabVisual(
            ExpenseModeButton,
            ExpenseModeLabel,
            ViewModel.SelectedMode == TransactionStatisticsMode.Expense);
        SetTabVisual(
            IncomeModeButton,
            IncomeModeLabel,
            ViewModel.SelectedMode == TransactionStatisticsMode.Income);
    }

    private void UpdateBreakdownTabs()
    {
        if (ViewModel is null)
        {
            return;
        }

        SetTabVisual(
            CategoryBreakdownButton,
            CategoryBreakdownLabel,
            ViewModel.SelectedBreakdownDimension ==
                TransactionStatisticsBreakdownDimension.Category);
        SetTabVisual(
            PaymentBreakdownButton,
            PaymentBreakdownLabel,
            ViewModel.SelectedBreakdownDimension ==
                TransactionStatisticsBreakdownDimension.PaymentMethod);
    }

    private static void SetTabVisual(Border border, Label label, bool isSelected)
    {
        border.Style = (Style)Application.Current!.Resources[
            isSelected ? "FlatSelectedCategorySegmentTab" : "CategorySegmentTab"];
        label.Style = (Style)Application.Current.Resources[
            isSelected ? "SelectedCategorySegmentLabel" : "CategorySegmentLabel"];
    }
}
