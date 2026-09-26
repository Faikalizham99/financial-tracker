using System.Globalization;
using FinancialTracker.Models;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed class AssetTrendChartDrawable : IDrawable
{
    private const int MaximumVisiblePoints = 6;
    private const float LabelHeight = 20f;
    private const float BarSpacing = 5f;
    private const float MinimumBarHeight = 7f;

    private IReadOnlyList<ChartPoint> points = [];
    private IReadOnlyDictionary<int, float> startingHeights = new Dictionary<int, float>();
    private float animationProgress = 1f;

    public Color BarColor { get; set; } = Color.FromArgb("#5044E4");

    public Color LabelColor { get; set; } = Color.FromArgb("#686273");

    public float AnimationProgress
    {
        get => animationProgress;
        set => animationProgress = Math.Clamp(value, 0f, 1f);
    }

    public void SetPoints(IReadOnlyList<AssetTrendPoint> source, bool animate)
    {
        var displayedHeights = points.ToDictionary(
            point => point.MonthKey,
            point => Interpolate(GetStartingHeight(point.MonthKey), point.RelativeHeight));

        points = source
            .TakeLast(MaximumVisiblePoints)
            .Select(point => new ChartPoint(
                (point.Month.Year * 100) + point.Month.Month,
                point.Month.ToString("MMM", CultureInfo.CurrentCulture),
                Math.Clamp((float)point.RelativeHeight, 0f, 1f)))
            .ToList();

        startingHeights = animate
            ? points.ToDictionary(
                point => point.MonthKey,
                point => displayedHeights.GetValueOrDefault(point.MonthKey))
            : points.ToDictionary(point => point.MonthKey, point => point.RelativeHeight);
        AnimationProgress = animate ? 0f : 1f;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (points.Count == 0 || dirtyRect.Width <= 0 || dirtyRect.Height <= LabelHeight)
        {
            return;
        }

        var plotHeight = dirtyRect.Height - LabelHeight;
        var totalSpacing = BarSpacing * Math.Max(0, points.Count - 1);
        var columnWidth = Math.Max(1f, (dirtyRect.Width - totalSpacing) / points.Count);

        canvas.FillColor = BarColor;
        canvas.FontColor = LabelColor;
        canvas.FontSize = 10f;

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var relativeHeight = Interpolate(GetStartingHeight(point.MonthKey), point.RelativeHeight);
            var barHeight = Math.Max(MinimumBarHeight, plotHeight * relativeHeight);
            var x = dirtyRect.Left + (index * (columnWidth + BarSpacing));
            var y = dirtyRect.Top + plotHeight - barHeight;

            canvas.FillRoundedRectangle(x, y, columnWidth, barHeight, 5f);
            canvas.DrawString(
                point.Label,
                x,
                dirtyRect.Top + plotHeight,
                columnWidth,
                LabelHeight,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private float GetStartingHeight(int monthKey) =>
        startingHeights.TryGetValue(monthKey, out var value) ? value : 0f;

    private float Interpolate(float start, float target) =>
        start + ((target - start) * AnimationProgress);

    private sealed record ChartPoint(int MonthKey, string Label, float RelativeHeight);
}
