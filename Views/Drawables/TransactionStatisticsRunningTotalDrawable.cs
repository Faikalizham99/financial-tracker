using FinancialTracker.Models;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed class TransactionStatisticsRunningTotalDrawable : IDrawable
{
    private const float LeftPadding = 42f;
    private const float RightPadding = 10f;
    private const float TopPadding = 14f;
    private const float BottomPadding = 28f;
    private IReadOnlyList<TransactionStatisticsRunningTotalPoint> points = [];

    public Color CurrentColor { get; set; } = Color.FromArgb("#C2415A");
    public Color PreviousColor { get; set; } = Color.FromArgb("#777381");
    public Color LabelColor { get; set; } = Color.FromArgb("#686273");
    public Color GridColor { get; set; } = Color.FromArgb("#E9E3DB");
    public float AnimationProgress { get; set; } = 1f;

    public void SetPoints(
        IReadOnlyList<TransactionStatisticsRunningTotalPoint> source,
        bool animate)
    {
        points = source;
        AnimationProgress = animate ? 0f : 1f;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (points.Count == 0 || dirtyRect.Width <= LeftPadding + RightPadding)
        {
            return;
        }

        var plotLeft = dirtyRect.Left + LeftPadding;
        var plotRight = dirtyRect.Right - RightPadding;
        var plotTop = dirtyRect.Top + TopPadding;
        var plotBottom = dirtyRect.Bottom - BottomPadding;
        var plotWidth = Math.Max(1f, plotRight - plotLeft);
        var plotHeight = Math.Max(1f, plotBottom - plotTop);
        var maximum = Math.Max(
            1L,
            points.Max(point => Math.Max(
                point.CurrentValueMinor,
                point.PreviousValueMinor)));
        var roundedMaximum = RoundMaximum(maximum);

        canvas.FontColor = LabelColor;
        canvas.FontSize = 10f;
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1f;
        canvas.StrokeDashPattern = [3f, 4f];
        for (var index = 0; index <= 3; index++)
        {
            var ratio = index / 3f;
            var y = plotBottom - (ratio * plotHeight);
            canvas.DrawLine(plotLeft, y, plotRight, y);
            canvas.DrawString(
                FormatAxisValue((long)(roundedMaximum * ratio)),
                dirtyRect.Left,
                y - 8f,
                LeftPadding - 6f,
                16f,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        var progress = Math.Clamp(AnimationProgress, 0f, 1f);
        DrawSeries(
            canvas,
            points.Select(point => point.PreviousValueMinor).ToList(),
            plotLeft,
            plotTop,
            plotWidth,
            plotHeight,
            roundedMaximum,
            progress,
            PreviousColor,
            dashed: true);
        DrawSeries(
            canvas,
            points.Select(point => point.CurrentValueMinor).ToList(),
            plotLeft,
            plotTop,
            plotWidth,
            plotHeight,
            roundedMaximum,
            progress,
            CurrentColor,
            dashed: false);

        canvas.StrokeDashPattern = null;
        canvas.FontColor = LabelColor;
        canvas.FontSize = 10f;
        foreach (var day in new[] { 1, 5, 10, 15, 20, 25, points.Count })
        {
            if (day < 1 || day > points.Count)
            {
                continue;
            }

            var x = points.Count == 1
                ? plotLeft + (plotWidth / 2f)
                : plotLeft + ((day - 1) / (float)(points.Count - 1) * plotWidth);
            canvas.DrawString(
                day.ToString(),
                x - 15f,
                plotBottom + 4f,
                30f,
                BottomPadding - 4f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private static void DrawSeries(
        ICanvas canvas,
        IReadOnlyList<long> values,
        float plotLeft,
        float plotTop,
        float plotWidth,
        float plotHeight,
        long maximum,
        float progress,
        Color color,
        bool dashed)
    {
        canvas.StrokeColor = color;
        canvas.StrokeSize = dashed ? 1.8f : 2.7f;
        canvas.StrokeDashPattern = dashed ? [6f, 5f] : null;
        canvas.StrokeLineCap = LineCap.Round;
        var coordinates = new PointF[values.Count];
        for (var index = 0; index < values.Count; index++)
        {
            var x = values.Count == 1
                ? plotLeft + (plotWidth / 2f)
                : plotLeft + (index / (float)(values.Count - 1) * plotWidth);
            var targetY = plotTop +
                ((1f - (values[index] / (float)maximum)) * plotHeight);
            var y = plotTop + plotHeight +
                ((targetY - (plotTop + plotHeight)) * progress);
            coordinates[index] = new PointF(x, y);
        }

        for (var index = 1; index < coordinates.Length; index++)
        {
            canvas.DrawLine(
                coordinates[index - 1].X,
                coordinates[index - 1].Y,
                coordinates[index].X,
                coordinates[index].Y);
        }

        if (!dashed && coordinates.Length > 0)
        {
            canvas.FillColor = color;
            canvas.FillCircle(coordinates[^1], 4f);
        }
    }

    private static long RoundMaximum(long maximum)
    {
        var magnitude = (long)Math.Pow(10, Math.Max(0, maximum.ToString().Length - 1));
        return Math.Max(magnitude, (long)Math.Ceiling(maximum / (double)magnitude) * magnitude);
    }

    private static string FormatAxisValue(long amountMinor)
    {
        var amount = amountMinor / 100d;
        return amount switch
        {
            >= 1_000_000 => $"{amount / 1_000_000:0.#}M",
            >= 1_000 => $"{amount / 1_000:0.#}K",
            _ => $"{amount:0.#}"
        };
    }
}
