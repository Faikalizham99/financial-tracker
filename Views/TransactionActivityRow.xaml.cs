using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class TransactionActivityRow : ContentView
{
    public static readonly BindableProperty ShowDateProperty = BindableProperty.Create(
        nameof(ShowDate),
        typeof(bool),
        typeof(TransactionActivityRow),
        false,
        propertyChanged: static (bindable, _, newValue) =>
            ((TransactionActivityRow)bindable).DateLabel.IsVisible = (bool)newValue);

    public static readonly BindableProperty ShowSearchHighlightProperty = BindableProperty.Create(
        nameof(ShowSearchHighlight),
        typeof(bool),
        typeof(TransactionActivityRow),
        false,
        propertyChanged: static (bindable, _, newValue) =>
            ((TransactionActivityRow)bindable).SearchHighlight.IsVisible = (bool)newValue);

    private CancellationTokenSource? measurementCancellation;
    private bool isToggleAnimating;

    public TransactionActivityRow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    public event EventHandler<TransactionDescriptionToggledEventArgs>? DescriptionToggled;

    public bool ShowDate
    {
        get => (bool)GetValue(ShowDateProperty);
        set => SetValue(ShowDateProperty, value);
    }

    public bool ShowSearchHighlight
    {
        get => (bool)GetValue(ShowSearchHighlightProperty);
        set => SetValue(ShowSearchHighlightProperty, value);
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();
        ScheduleDescriptionMeasurement();
    }

    private void OnLoaded(object? sender, EventArgs e) => ScheduleDescriptionMeasurement();

    private void OnUnloaded(object? sender, EventArgs e) => CancelDescriptionMeasurement();

    private void OnDescriptionSizeChanged(object? sender, EventArgs e) =>
        UpdateDescriptionExpandability();

    private async void OnDescriptionTapped(object? sender, TappedEventArgs e)
    {
        if (BindingContext is not TransactionActivityItem item ||
            !item.CanExpandDescription ||
            isToggleAnimating)
        {
            return;
        }

        isToggleAnimating = true;
        try
        {
            await DescriptionHeader.FadeToAsync(0.58, 55, Easing.CubicIn);
            var isExpanded = !item.IsDescriptionExpanded;
            item.SetDescriptionExpanded(isExpanded);
            SemanticProperties.SetDescription(
                DescriptionToggleButton,
                isExpanded ? "Collapse description" : "Expand description");
            DescriptionToggled?.Invoke(
                this,
                new TransactionDescriptionToggledEventArgs(item, isExpanded));
            await DescriptionHeader.FadeToAsync(1, 115, Easing.CubicOut);
        }
        finally
        {
            DescriptionHeader.Opacity = 1;
            isToggleAnimating = false;
        }
    }

    private void ScheduleDescriptionMeasurement()
    {
        CancelDescriptionMeasurement();
        var cancellation = new CancellationTokenSource();
        measurementCancellation = cancellation;
        _ = MeasureAfterLayoutAsync(cancellation);
    }

    private async Task MeasureAfterLayoutAsync(CancellationTokenSource cancellation)
    {
        try
        {
            foreach (var delay in new[] { 0, 50, 150 })
            {
                if (delay > 0)
                {
                    await Task.Delay(delay, cancellation.Token);
                }

                cancellation.Token.ThrowIfCancellationRequested();
                Dispatcher.Dispatch(UpdateDescriptionExpandability);
            }
        }
        catch (OperationCanceledException)
        {
            // A newer binding or layout pass owns the current measurement.
        }
        finally
        {
            if (ReferenceEquals(measurementCancellation, cancellation))
            {
                measurementCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void UpdateDescriptionExpandability()
    {
        if (BindingContext is not TransactionActivityItem item || DescriptionLabel.Width <= 0)
        {
            return;
        }

        var measuredTextWidth = DescriptionLabel
            .Measure(double.PositiveInfinity, double.PositiveInfinity)
            .Width;
        var estimatedTextWidth = EstimateSingleLineTextWidth(
            item.Description,
            DescriptionLabel.FontSize);
        var wasExpanded = item.IsDescriptionExpanded;
        item.SetDescriptionExpandable(
            Math.Max(measuredTextWidth, estimatedTextWidth) > DescriptionLabel.Width + 1);
        if (wasExpanded && !item.IsDescriptionExpanded)
        {
            DescriptionToggled?.Invoke(
                this,
                new TransactionDescriptionToggledEventArgs(item, isExpanded: false));
        }
    }

    private void CancelDescriptionMeasurement()
    {
        var cancellation = measurementCancellation;
        measurementCancellation = null;
        cancellation?.Cancel();
    }

    private static double EstimateSingleLineTextWidth(string text, double fontSize)
    {
        var emWidth = 0d;
        foreach (var character in text)
        {
            emWidth += character switch
            {
                _ when char.IsWhiteSpace(character) => 0.33,
                'i' or 'l' or 'I' or '1' or '|' or '!' or '.' or ',' or ':' or ';' or '\'' => 0.3,
                'm' or 'w' or 'M' or 'W' or '@' or '#' or '%' or '&' => 0.85,
                _ when char.IsUpper(character) => 0.64,
                _ => 0.55
            };
        }

        return emWidth * fontSize;
    }
}

public sealed class TransactionDescriptionToggledEventArgs(
    TransactionActivityItem item,
    bool isExpanded) : EventArgs
{
    public TransactionActivityItem Item { get; } = item;
    public bool IsExpanded { get; } = isExpanded;
}
