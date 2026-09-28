using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;
using FinancialTracker.Views.Drawables;

namespace FinancialTracker.Views;

public partial class AssetHistoryView : ContentView
{
    private const string BarAnimationName = "AssetHistoryBarAnimation";
    private const string DonutAnimationName = "AssetHistoryDonutAnimation";
    private const string DonutSelectionAnimationName = "AssetHistoryDonutSelectionAnimation";
    private const uint ChartAnimationLength = 500;

    private enum HistoryRange
    {
        SixMonths,
        TwelveMonths,
        All
    }

    private readonly AssetHistoryBarChartDrawable barChartDrawable = new();
    private readonly AssetDonutChartDrawable donutChartDrawable = new();
    private AssetPortfolioService? portfolioService;
    private Func<CurrencyOption>? currencyProvider;
    private AssetHistoryData? history;
    private HistoryRange selectedRange = HistoryRange.SixMonths;
    private DateTime selectedMonth;
    private bool includeKwsp = true;
    private int loadVersion;

    public AssetHistoryView()
    {
        InitializeComponent();
        HistoryBarChart.Drawable = barChartDrawable;
        DonutChart.Drawable = donutChartDrawable;
    }

    public bool IsOpen => IsVisible;

    public event Action<bool>? VisibilityChanged;

    public void Configure(
        AssetPortfolioService service,
        Func<CurrencyOption> selectedCurrencyProvider)
    {
        portfolioService = service;
        currencyProvider = selectedCurrencyProvider;
    }

    public async Task OpenAsync(DateTime throughMonth, bool initiallyIncludeKwsp)
    {
        if (portfolioService is null || currencyProvider is null)
        {
            return;
        }

        selectedMonth = new DateTime(throughMonth.Year, throughMonth.Month, 1);
        includeKwsp = initiallyIncludeKwsp;
        selectedRange = HistoryRange.SixMonths;
        HistoryErrorOverlay.IsVisible = false;
        SetVisibility(true);
        HistoryLoadingOverlay.IsVisible = true;
        var requestVersion = ++loadVersion;

        try
        {
            var loadedHistory = await portfolioService.GetHistoryAsync(
                selectedMonth,
                currencyProvider());
            if (requestVersion != loadVersion || !IsVisible)
            {
                return;
            }

            history = loadedHistory;
            RenderHistory(animateCharts: true);
        }
        catch
        {
            if (requestVersion == loadVersion && IsVisible)
            {
                HistoryErrorOverlay.IsVisible = true;
                HistoryErrorLabel.Text =
                    "Your asset history could not be loaded. Close this page and try again.";
            }
        }
        finally
        {
            if (requestVersion == loadVersion)
            {
                HistoryLoadingOverlay.IsVisible = false;
            }
        }
    }

    public void Close()
    {
        ++loadVersion;
        HistoryBarChart.AbortAnimation(BarAnimationName);
        DonutChart.AbortAnimation(DonutAnimationName);
        DonutChart.AbortAnimation(DonutSelectionAnimationName);
        HistoryLoadingOverlay.IsVisible = false;
        history = null;
        SetVisibility(false);
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
}
