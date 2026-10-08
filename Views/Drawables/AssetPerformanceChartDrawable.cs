using System.Globalization;
using FinancialTracker.Models;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

internal enum AssetPerformanceChartMode
{
    Total,
    InvestedVersusTotal,
    ProfitLoss
}

internal sealed class AssetPerformanceChartDrawable : IDrawable
{
    private const float HorizontalPadding = 20f;
    private const float TopPadding = 12f;
    private const float BottomPadding = 30f;

    public IReadOnlyList<AssetPerformancePoint> Points { get; private set; } = [];

    public AssetPerformanceChartMode Mode { get; private set; }

    public Color AccentColor { get; set; } = Color.FromArgb("#5044E4");

    public Color SecondaryColor { get; set; } = Color.FromArgb("#777381");

    public Color GridColor { get; set; } = Color.FromArgb("#E9E3DB");

    public Color PositiveColor { get; set; } = Color.FromArgb("#168A67");

    public Color NegativeColor { get; set; } = Color.FromArgb("#C2415A");

    public float AnimationProgress { get; set; } = 1f;

    public void SetData(
        IReadOnlyList<AssetPerformancePoint> points,
        AssetPerformanceChartMode mode,
        bool animate)
    {
        Points = points;
        Mode = mode;
        AnimationProgress = animate ? 0f : 1f;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (Points.Count == 0 || dirtyRect.Width <= HorizontalPadding * 2)
        {
            return;
        }

        var plotLeft = dirtyRect.Left + HorizontalPadding;
        var plotRight = dirtyRect.Right - HorizontalPadding;
        var plotTop = dirtyRect.Top + TopPadding;
        var plotBottom = dirtyRect.Bottom - BottomPadding;
        var plotHeight = Math.Max(1f, plotBottom - plotTop);
        var series = BuildSeries();
        var allValues = series.SelectMany(item => item.Values).ToList();
        if (allValues.Count == 0)
        {
            return;
        }

        var minimum = allValues.Min();
        var maximum = allValues.Max();
        if (Mode == AssetPerformanceChartMode.ProfitLoss)
        {
            minimum = Math.Min(0, minimum);
            maximum = Math.Max(0, maximum);
        }
        else if (minimum > 0)
        {
            var padding = Math.Max(1d, (maximum - minimum) * 0.15d);
            minimum = Math.Max(0, minimum - padding);
            maximum += padding;
        }

        if (Math.Abs(maximum - minimum) < 1d)
        {
            minimum -= 1d;
            maximum += 1d;
        }

        DrawGrid(canvas, plotLeft, plotRight, plotTop, plotBottom, minimum, maximum);
        foreach (var item in series)
        {
            DrawSeries(
                canvas,
                item,
                plotLeft,
                plotRight,
                plotTop,
                plotHeight,
                minimum,
                maximum);
        }
        DrawDateLabels(canvas, plotLeft, plotRight, plotBottom);
    }

    private IReadOnlyList<ChartSeries> BuildSeries() => Mode switch
    {
        AssetPerformanceChartMode.InvestedVersusTotal =>
        [
            new ChartSeries(
                Points.Select(item => (double)item.InvestedMinor).ToList(),
                SecondaryColor,
                2f,
                DrawPoints: false),
            new ChartSeries(
                Points.Select(item => (double)item.TotalMinor).ToList(),
                AccentColor,
                3f,
                DrawPoints: true)
        ],
        AssetPerformanceChartMode.ProfitLoss =>
        [
            new ChartSeries(
                Points.Select(item => (double)item.ProfitLossMinor).ToList(),
                AccentColor,
                3f,
                DrawPoints: true,
                UseSignColors: true)
        ],
        _ =>
        [
            new ChartSeries(
                Points.Select(item => (double)item.TotalMinor).ToList(),
                AccentColor,
                3f,
                DrawPoints: true)
        ]
    };

    private void DrawGrid(
        ICanvas canvas,
        float left,
        float right,
        float top,
        float bottom,
        double minimum,
        double maximum)
    {
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1f;
        for (var index = 0; index < 4; index++)
        {
            var y = top + ((bottom - top) * index / 3f);
            canvas.DrawLine(left, y, right, y);
        }

        if (Mode != AssetPerformanceChartMode.ProfitLoss ||
            minimum > 0 || maximum < 0)
        {
            return;
        }

        var zeroY = ValueToY(0, top, bottom - top, minimum, maximum);
        canvas.StrokeColor = SecondaryColor.WithAlpha(0.7f);
        canvas.StrokeSize = 1.5f;
        canvas.DrawLine(left, zeroY, right, zeroY);
    }

    private void DrawSeries(
        ICanvas canvas,
        ChartSeries series,
        float left,
        float right,
        float top,
        float height,
        double minimum,
        double maximum)
    {
        var progress = Math.Clamp(AnimationProgress, 0f, 1f);
        var coordinates = series.Values
            .Select((value, index) => new PointF(
                PointX(index, left, right),
                (float)(top + height -
                    (((value - minimum) / (maximum - minimum)) * height * progress))))
            .ToList();

        canvas.StrokeSize = series.StrokeSize;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        for (var index = 1; index < coordinates.Count; index++)
        {
            canvas.StrokeColor = series.UseSignColors
                ? SignColor(series.Values[index])
                : series.Color;
            canvas.DrawLine(
                coordinates[index - 1].X,
                coordinates[index - 1].Y,
                coordinates[index].X,
                coordinates[index].Y);
        }

        if (!series.DrawPoints)
        {
            return;
        }

        for (var index = 0; index < coordinates.Count; index++)
        {
            canvas.FillColor = series.UseSignColors
                ? SignColor(series.Values[index])
                : series.Color;
            canvas.FillCircle(coordinates[index], 4.2f);
        }
    }

    private void DrawDateLabels(
        ICanvas canvas,
        float left,
        float right,
        float plotBottom)
    {
        var indices = Points.Count switch
        {
            <= 3 => Enumerable.Range(0, Points.Count),
            _ => new[] { 0, Points.Count / 2, Points.Count - 1 }
        };
        canvas.FontSize = 10f;
        canvas.FontColor = SecondaryColor;
        foreach (var index in indices.Distinct())
        {
            var x = PointX(index, left, right);
            canvas.DrawString(
                Points[index].EntryDate.ToString("dd MMM", CultureInfo.CurrentCulture),
                x - 34,
                plotBottom + 5,
                68,
                BottomPadding - 5,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private float PointX(int index, float left, float right) =>
        Points.Count == 1
            ? (left + right) / 2f
            : left + ((right - left) * index / (Points.Count - 1f));

    private static float ValueToY(
        double value,
        float top,
        float height,
        double minimum,
        double maximum) =>
        (float)(top + height - (((value - minimum) / (maximum - minimum)) * height));

    private Color SignColor(double value) => value switch
    {
        > 0 => PositiveColor,
        < 0 => NegativeColor,
        _ => SecondaryColor
    };

    private sealed record ChartSeries(
        IReadOnlyList<double> Values,
        Color Color,
        float StrokeSize,
        bool DrawPoints,
        bool UseSignColors = false);
}
