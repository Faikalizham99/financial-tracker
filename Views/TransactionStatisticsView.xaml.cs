using FinancialTracker.Models;
using FinancialTracker.ViewModels;
using FinancialTracker.Views.Drawables;

namespace FinancialTracker.Views;

public partial class TransactionStatisticsView : ContentView
{
    private const string ChartAnimationName = "TransactionStatisticsChartAnimation";
    private const string CompositionSelectionAnimationName =
        "TransactionStatisticsCompositionSelectionAnimation";
    private const string ProgressAnimationName = "TransactionStatisticsProgressAnimation";
    private const uint ChartAnimationLength = 500;

    private readonly TransactionStatisticsBarChartDrawable chartDrawable = new();
    private readonly TransactionStatisticsRunningTotalDrawable runningTotalDrawable = new();
    private readonly TransactionStatisticsCompositionDrawable compositionDrawable = new();
    private readonly AnimatedProgressBarDrawable incomeProgressDrawable = new();
    private readonly AnimatedProgressBarDrawable expenseProgressDrawable = new();
    private bool isOpen;
    private bool isAnimating;
    private int loadVersion;

    public TransactionStatisticsView()
    {
        InitializeComponent();
        MonthlyChart.Drawable = chartDrawable;
        RunningTotalChart.Drawable = runningTotalDrawable;
        CompositionChart.Drawable = compositionDrawable;
        IncomeProgressBar.Drawable = incomeProgressDrawable;
        ExpenseProgressBar.Drawable = expenseProgressDrawable;
    }

    public bool IsOpen => isOpen;

    public event Action<bool>? VisibilityChanged;
    public event Action<int>? TransactionEditRequested;

    private TransactionStatisticsViewModel? ViewModel =>
        BindingContext as TransactionStatisticsViewModel;

    public void ConfigureDetailView(TransactionStatisticsDetailViewModel viewModel)
    {
        StatisticsDetailView.BindingContext = viewModel;
        StatisticsDetailView.TransactionEditRequested += transactionId =>
            TransactionEditRequested?.Invoke(transactionId);
    }

    public async Task HandleBackAsync()
    {
        if (StatisticsDetailView.IsOpen)
        {
            await StatisticsDetailView.CloseAsync();
            return;
        }

        await CloseAsync();
    }

    public async Task RefreshAsync(CurrencyOption currency, bool includeInvestment)
    {
        if (!isOpen || ViewModel is null)
        {
            return;
        }

        var currentDetail = StatisticsDetailView.CurrentRequest;
        await ViewModel.ReloadAsync(currency, includeInvestment);
        RenderStatistics(animate: true);

        if (currentDetail is null || !StatisticsDetailView.IsOpen)
        {
            return;
        }

        var refreshedItem = ViewModel.BreakdownItems.FirstOrDefault(item =>
            item.Key.Equals(currentDetail.OptionKey, StringComparison.OrdinalIgnoreCase));
        var refreshedRequest = refreshedItem is null
            ? null
            : ViewModel.CreateDetailRequest(refreshedItem);
        if (refreshedRequest is null)
        {
            await StatisticsDetailView.CloseAsync();
        }
        else
        {
            StatisticsDetailView.Refresh(refreshedRequest);
        }
    }

    public async Task OpenAsync(
        DateTime sourceMonth,
        CurrencyOption currency,
        bool includeInvestment)
    {
        if (isOpen || isAnimating || ViewModel is null)
        {
            return;
        }

        isOpen = true;
        isAnimating = true;
        var requestVersion = ++loadVersion;
        StatisticsErrorOverlay.IsVisible = false;
        StatisticsLoadingOverlay.IsVisible = true;
        IsVisible = true;
        Opacity = 0;
        StatisticsContent.TranslationX = 30;
        VisibilityChanged?.Invoke(true);
        QueueScrollReset();

        try
        {
            var loadTask = ViewModel.LoadAsync(
                sourceMonth,
                currency,
                includeInvestment);
            await Task.WhenAll(
                this.FadeToAsync(1, 170, Easing.CubicOut),
                StatisticsContent.TranslateToAsync(0, 0, 230, Easing.CubicOut),
                loadTask);

            if (requestVersion != loadVersion || !isOpen)
            {
                return;
            }

            RenderStatistics(animate: true);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Transaction statistics load failed: {exception}");
            if (requestVersion == loadVersion && isOpen)
            {
                StatisticsErrorOverlay.IsVisible = true;
            }
        }
        finally
        {
            if (requestVersion == loadVersion)
            {
                StatisticsLoadingOverlay.IsVisible = false;
            }

            isAnimating = false;
        }
    }

    public async Task CloseAsync()
    {
        if (!isOpen || isAnimating)
        {
            return;
        }

        isAnimating = true;
        ++loadVersion;
        MonthlyChart.AbortAnimation(ChartAnimationName);
        RunningTotalChart.AbortAnimation(ChartAnimationName);
        CompositionChart.AbortAnimation(ChartAnimationName);
        CompositionChart.AbortAnimation(CompositionSelectionAnimationName);
        IncomeProgressBar.AbortAnimation(ProgressAnimationName);
        ExpenseProgressBar.AbortAnimation(ProgressAnimationName);
        try
        {
            await Task.WhenAll(
                this.FadeToAsync(0, 145, Easing.CubicIn),
                StatisticsContent.TranslateToAsync(26, 0, 175, Easing.CubicIn));
        }
        finally
        {
            StatisticsLoadingOverlay.IsVisible = false;
            StatisticsErrorOverlay.IsVisible = false;
            IsVisible = false;
            Opacity = 1;
            StatisticsContent.TranslationX = 0;
            isOpen = false;
            isAnimating = false;
            VisibilityChanged?.Invoke(false);
        }
    }

    private void QueueScrollReset()
    {
        Dispatcher.Dispatch(async () =>
        {
            try
            {
                await StatisticsScrollView.ScrollToAsync(0, 0, false);
            }
            catch (ObjectDisposedException)
            {
            }
        });
    }
}
