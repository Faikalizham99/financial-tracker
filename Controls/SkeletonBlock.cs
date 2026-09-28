using Microsoft.Maui.Controls.Shapes;
using FinancialTracker.Helpers;

namespace FinancialTracker.Controls;

public sealed class SkeletonBlock : Border
{
    public static readonly BindableProperty SkeletonRadiusProperty = BindableProperty.Create(
        nameof(SkeletonRadius),
        typeof(double),
        typeof(SkeletonBlock),
        999d,
        propertyChanged: static (bindable, _, value) =>
            ((SkeletonBlock)bindable).ApplyRadius((double)value));

    public SkeletonBlock()
    {
        InputTransparent = true;
        StrokeThickness = 0;
        ThemeResourceBindings.SetColor(
            this,
            BackgroundColorProperty,
            "SurfaceMutedLight",
            "SurfaceMutedDark");
        ApplyRadius(SkeletonRadius);
    }

    public double SkeletonRadius
    {
        get => (double)GetValue(SkeletonRadiusProperty);
        set => SetValue(SkeletonRadiusProperty, value);
    }

    private void ApplyRadius(double radius) =>
        StrokeShape = new RoundRectangle
        {
            CornerRadius = new CornerRadius(Math.Max(0, radius))
        };
}
