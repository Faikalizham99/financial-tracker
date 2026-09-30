using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed class TransactionStatisticsBarChartDrawable : IDrawable
{
    private const float HorizontalPadding = 5f;
    private const float TopPadding = 12f;
    private const float LabelHeight = 34f;
    private const float ColumnGap = 7f;
    private const float MinimumBarHeight = 5f;

    private IReadOnlyList<ChartPoint> points = [];

    public Color PositiveColor { get; set; } = Color.FromArgb("#16856B");
    public Color NegativeColor { get; set; } = Color.FromArgb("#C2415A");
    public Color LabelColor { get; set; } = Color.FromArgb("#686273");
    public Color AxisColor { get; set; } = Color.FromArgb("#E9E3DB");
    public Color SelectionColor { get; set; } = Color.FromArgb("#64D8E4");
    public Color SelectionBackgroundColor { get; set; } = Color.FromArgb("#E4F8FA");
    public float AnimationProgress { get; set; } = 1f;
    public int? SelectedMonthKey { get; set; }

    public IReadOnlyList<TransactionStatisticsChartPoint> SourcePoints { get; private set; } = [];

    public void SetPoints(
        IReadOnlyList<TransactionStatisticsChartPoint> source,
        bool animate)
    {
        SourcePoints = source;
        points = source
            .Select(point => new ChartPoint(
                MonthKeyConverter.FromDate(point.Month),
                point.Month.ToString("MMM", CultureInfo.CurrentCulture),
                point.ValueMinor))
            .ToList();
        AnimationProgress = animate ? 0f : 1f;
    }

    public bool Select(PointF touch, float width, float height)
    {
        if (points.Count == 0 || width <= 0 || height <= LabelHeight ||
            touch.X < HorizontalPadding || touch.X > width - HorizontalPadding)
        {
            return false;
        }

        var columnWidth = GetColumnWidth(width);
        var stride = columnWidth + ColumnGap;
        var index = (int)Math.Floor((touch.X - HorizontalPadding) / stride);
        if (index < 0 || index >= points.Count)
        {
            return false;
        }

        SelectedMonthKey = points[index].MonthKey;
        return true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (points.Count == 0 || dirtyRect.Width <= 0 || dirtyRect.Height <= LabelHeight)
        {
            return;
        }

        var plotTop = dirtyRect.Top + TopPadding;
        var plotBottom = dirtyRect.Bottom - LabelHeight;
        var plotHeight = Math.Max(1f, plotBottom - plotTop);
        var minimum = Math.Min(0L, points.Min(point => point.ValueMinor));
        var maximum = Math.Max(0L, points.Max(point => point.ValueMinor));
        var valueRange = Math.Max(1d, maximum - (double)minimum);
        var zeroY = maximum == 0 && minimum == 0
            ? plotBottom
            : plotTop + ((maximum / valueRange) * plotHeight);
        var columnWidth = GetColumnWidth(dirtyRect.Width);
        var progress = Math.Clamp(AnimationProgress, 0f, 1f);

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = dirtyRect.Left + HorizontalPadding +
                (index * (columnWidth + ColumnGap));
            if (SelectedMonthKey == point.MonthKey)
            {
                canvas.FillColor = SelectionBackgroundColor;
                canvas.FillRoundedRectangle(
                    x - 3f,
                    plotTop - 5f,
                    columnWidth + 6f,
                    plotHeight + LabelHeight + 3f,
                    10f);
            }
        }

        canvas.StrokeColor = AxisColor;
        canvas.StrokeSize = 1f;
        canvas.DrawLine(dirtyRect.Left, (float)zeroY, dirtyRect.Right, (float)zeroY);

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = dirtyRect.Left + HorizontalPadding +
                (index * (columnWidth + ColumnGap));
            var targetY = plotTop + (((maximum - point.ValueMinor) / valueRange) * plotHeight);
            var animatedY = zeroY + ((targetY - zeroY) * progress);
            var rawHeight = Math.Abs(animatedY - zeroY);
            var barHeight = point.ValueMinor == 0
                ? MinimumBarHeight
                : Math.Max(MinimumBarHeight, (float)rawHeight);
            var barY = point.ValueMinor >= 0
                ? (float)zeroY - barHeight
                : (float)zeroY;
            var isSelected = SelectedMonthKey == point.MonthKey;
            var baseColor = point.ValueMinor < 0 ? NegativeColor : PositiveColor;
            canvas.FillColor = isSelected ? baseColor : baseColor.WithAlpha(0.55f);
            canvas.FillRoundedRectangle(x, barY, columnWidth, barHeight, 6f);

            canvas.FontColor = isSelected ? SelectionColor : LabelColor;
            canvas.FontSize = 11f;
            canvas.DrawString(
                point.Label,
                x,
                plotBottom + 3f,
                columnWidth,
                LabelHeight - 3f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private float GetColumnWidth(float width)
    {
        var spacing = ColumnGap * Math.Max(0, points.Count - 1);
        var availableWidth = Math.Max(1f, width - (HorizontalPadding * 2f) - spacing);
        return Math.Max(30f, availableWidth / Math.Max(1, points.Count));
    }

    private sealed record ChartPoint(int MonthKey, string Label, long ValueMinor);
}
