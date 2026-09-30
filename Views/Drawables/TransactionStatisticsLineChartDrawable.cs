using FinancialTracker.Models;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed class TransactionStatisticsLineChartDrawable : IDrawable
{
    private const float LeftPadding = 46f;
    private const float RightPadding = 10f;
    private const float TopPadding = 13f;
    private const float BottomPadding = 30f;
    private const float PointRadius = 3.5f;
    private IReadOnlyList<TransactionStatisticsDetailChartPoint> points = [];

    public Color LineColor { get; set; } = Color.FromArgb("#C2415A");
    public Color LabelColor { get; set; } = Color.FromArgb("#686273");
    public Color GridColor { get; set; } = Color.FromArgb("#E9E3DB");
    public float AnimationProgress { get; set; } = 1f;

    public void SetPoints(
        IReadOnlyList<TransactionStatisticsDetailChartPoint> source,
        bool animate)
    {
        points = source;
        AnimationProgress = animate ? 0f : 1f;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (points.Count == 0 ||
            dirtyRect.Width <= LeftPadding + RightPadding ||
            dirtyRect.Height <= TopPadding + BottomPadding)
        {
            return;
        }

        var plotLeft = dirtyRect.Left + LeftPadding;
        var plotRight = dirtyRect.Right - RightPadding;
        var plotTop = dirtyRect.Top + TopPadding;
        var plotBottom = dirtyRect.Bottom - BottomPadding;
        var plotWidth = Math.Max(1f, plotRight - plotLeft);
        var plotHeight = Math.Max(1f, plotBottom - plotTop);
        var maximum = Math.Max(1L, points.Max(point => point.ValueMinor));
        var roundedMaximum = RoundMaximum(maximum);
        const int gridLineCount = 4;

        canvas.FontColor = LabelColor;
        canvas.FontSize = 10f;
        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1f;
        canvas.StrokeDashPattern = [3f, 4f];

        for (var index = 0; index <= gridLineCount; index++)
        {
            var ratio = index / (float)gridLineCount;
            var y = plotBottom - (ratio * plotHeight);
            canvas.DrawLine(plotLeft, y, plotRight, y);
            canvas.DrawString(
                FormatAxisValue((long)(roundedMaximum * ratio)),
                dirtyRect.Left,
                y - 8f,
                LeftPadding - 7f,
                16f,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        canvas.StrokeDashPattern = null;
        var progress = Math.Clamp(AnimationProgress, 0f, 1f);
        var coordinates = new PointF[points.Count];
        for (var index = 0; index < points.Count; index++)
        {
            var x = points.Count == 1
                ? plotLeft + (plotWidth / 2f)
                : plotLeft + (index / (float)(points.Count - 1) * plotWidth);
            var targetY = plotBottom -
                ((points[index].ValueMinor / (float)roundedMaximum) * plotHeight);
            var y = plotBottom + ((targetY - plotBottom) * progress);
            coordinates[index] = new PointF(x, y);
        }

        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = 2.5f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        for (var index = 1; index < coordinates.Length; index++)
        {
            canvas.DrawLine(
                coordinates[index - 1].X,
                coordinates[index - 1].Y,
                coordinates[index].X,
                coordinates[index].Y);
        }

        canvas.FillColor = LineColor;
        var labelEvery = points.Count > 12 ? 5 : 1;
        for (var index = 0; index < coordinates.Length; index++)
        {
            var coordinate = coordinates[index];
            canvas.FillCircle(coordinate.X, coordinate.Y, PointRadius);

            if (index % labelEvery != 0 && index != points.Count - 1)
            {
                continue;
            }

            canvas.FontColor = LabelColor;
            canvas.FontSize = 10f;
            canvas.DrawString(
                points[index].Label,
                coordinate.X - 18f,
                plotBottom + 5f,
                36f,
                BottomPadding - 5f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
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
