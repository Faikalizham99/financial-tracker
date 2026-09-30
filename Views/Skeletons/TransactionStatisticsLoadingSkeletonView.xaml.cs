namespace FinancialTracker.Views.Skeletons;

public partial class TransactionStatisticsLoadingSkeletonView : ContentView
{
    public TransactionStatisticsLoadingSkeletonView()
    {
        InitializeComponent();
    }

    private void OnSkeletonRootSizeChanged(object? sender, EventArgs e)
    {
        if (SkeletonRoot.Width > 0)
        {
            SkeletonContent.WidthRequest = Math.Min(
                940,
                Math.Max(320, SkeletonRoot.Width));
        }
    }
}
