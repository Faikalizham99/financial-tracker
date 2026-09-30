using FinancialTracker.Models;
using FinancialTracker.ViewModels;
using FinancialTracker.Views.Drawables;

namespace FinancialTracker.Views;

public partial class TransactionStatisticsDetailView : ContentView
{
    private const string ChartAnimationName = "TransactionStatisticsDetailChartAnimation";
    private readonly TransactionStatisticsLineChartDrawable chartDrawable = new();
    private bool isOpen;
    private bool isAnimating;

    public TransactionStatisticsDetailView()
    {
        InitializeComponent();
        DetailChart.Drawable = chartDrawable;
    }

    public bool IsOpen => isOpen;
    public TransactionStatisticsDetailRequest? CurrentRequest { get; private set; }
    public event Action<int>? TransactionEditRequested;

    private TransactionStatisticsDetailViewModel? ViewModel =>
        BindingContext as TransactionStatisticsDetailViewModel;

    public async Task OpenAsync(TransactionStatisticsDetailRequest request)
    {
        if (isOpen || isAnimating || ViewModel is null)
        {
            return;
        }

        CurrentRequest = request;
        ViewModel.Load(request);
        UpdateRangeTabs();
        RenderChart(animate: false);
        isOpen = true;
        isAnimating = true;
        IsVisible = true;
        Opacity = 0;
        DetailContent.TranslationX = 28;
        QueueScrollReset();

        try
        {
            await Task.WhenAll(
                this.FadeToAsync(1, 170, Easing.CubicOut),
                DetailContent.TranslateToAsync(0, 0, 220, Easing.CubicOut));
            RenderChart(animate: true);
        }
        finally
        {
            isAnimating = false;
        }
    }

    public void Refresh(TransactionStatisticsDetailRequest request)
    {
        if (!isOpen || ViewModel is null)
        {
            return;
        }

        var selectedRange = ViewModel.SelectedRange;
        CurrentRequest = request;
        ViewModel.Load(request);
        ViewModel.SelectRange(selectedRange);
        UpdateRangeTabs();
        RenderChart(animate: true);
    }

    public async Task CloseAsync()
    {
        if (!isOpen || isAnimating)
        {
            return;
        }

        isAnimating = true;
        DetailChart.AbortAnimation(ChartAnimationName);
        try
        {
            await Task.WhenAll(
                this.FadeToAsync(0, 145, Easing.CubicIn),
                DetailContent.TranslateToAsync(24, 0, 170, Easing.CubicIn));
        }
        finally
        {
            IsVisible = false;
            Opacity = 1;
            DetailContent.TranslationX = 0;
            CurrentRequest = null;
            isOpen = false;
            isAnimating = false;
        }
    }

    private void QueueScrollReset() => Dispatcher.Dispatch(async () =>
    {
        try
        {
            await DetailScrollView.ScrollToAsync(0, 0, false);
        }
        catch (ObjectDisposedException)
        {
        }
    });
}
