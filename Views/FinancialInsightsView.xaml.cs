using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class FinancialInsightsView : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IReadOnlyList<FinancialInsightItem>),
        typeof(FinancialInsightsView),
        Array.Empty<FinancialInsightItem>());

    public FinancialInsightsView()
    {
        InitializeComponent();
    }

    public IReadOnlyList<FinancialInsightItem> ItemsSource
    {
        get => (IReadOnlyList<FinancialInsightItem>)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }
}
