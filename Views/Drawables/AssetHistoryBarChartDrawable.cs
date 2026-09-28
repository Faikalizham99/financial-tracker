using System.Globalization;
using FinancialTracker.Helpers;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed record AssetHistoryChartPoint(DateTime Month, long? TotalMinor);

public sealed class AssetHistoryBarChartDrawable : IDrawable
{
    private const float TopPadding = 12f;
    private const float HorizontalPadding = 8f;
    private const float LabelHeight = 34f;
    private const float BarSpacing = 9f;
    private const float MinimumBarHeight = 8f;

    private IReadOnlyList<ChartPoint> points = [];

    public bool UseDarkPalette { get; set; }

    public Color LabelColor { get; set; } = Color.FromArgb("#686273");

    public Color EmptyColor { get; set; } = Color.FromArgb("#E9E3DB");

    public Color SelectionColor { get; set; } = Color.FromArgb("#64D8E4");

    public float AnimationProgress { get; set; } = 1f;

    public int? SelectedMonthKey { get; set; }

    public IReadOnlyList<AssetHistoryChartPoint> SourcePoints { get; private set; } = [];

    public void SetPoints(IReadOnlyList<AssetHistoryChartPoint> source, bool animate)
    {
        SourcePoints = source;
        var maximum = source
            .Where(item => item.TotalMinor.HasValue)
            .Select(item => Math.Max(0, item.TotalMinor!.Value))
            .DefaultIfEmpty(0)
            .Max();
        points = source
            .Select(item => new ChartPoint(
                MonthKeyConverter.FromDate(item.Month),
                item.Month.Month,
                item.Month.ToString("MMM", CultureInfo.CurrentCulture),
                item.TotalMinor.HasValue,
                maximum <= 0 || !item.TotalMinor.HasValue
                    ? 0f
                    : Math.Clamp((float)((double)Math.Max(0, item.TotalMinor.Value) / maximum), 0f, 1f)))
            .ToList();
        AnimationProgress = animate ? 0f : 1f;
    }

    public bool Select(PointF touch, float width, float height)
    {
        if (points.Count == 0 || width <= 0 || height <= LabelHeight)
        {
            return false;
        }

        if (touch.X < HorizontalPadding || touch.X > width - HorizontalPadding)
        {
            return false;
        }

        var columnWidth = GetColumnWidth(width);
        var stride = columnWidth + BarSpacing;
        var adjustedX = touch.X - HorizontalPadding;
        var index = (int)Math.Floor(adjustedX / stride);
        if (index < 0 || index >= points.Count)
        {
            return false;
        }

        var offsetWithinColumn = adjustedX - (index * stride);
        if (offsetWithinColumn > columnWidth)
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

        var plotBottom = dirtyRect.Bottom - LabelHeight;
        var plotHeight = Math.Max(1f, plotBottom - dirtyRect.Top - TopPadding);
        var columnWidth = GetColumnWidth(dirtyRect.Width);

        canvas.StrokeColor = EmptyColor;
        canvas.StrokeSize = 1f;
        canvas.DrawLine(dirtyRect.Left, plotBottom, dirtyRect.Right, plotBottom);
        canvas.FontColor = LabelColor;
        canvas.FontSize = 11f;

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = dirtyRect.Left + HorizontalPadding +
                (index * (columnWidth + BarSpacing));
            var isSelected = SelectedMonthKey == point.MonthKey;

            if (point.HasSnapshot)
            {
                var barHeight = Math.Max(
                    MinimumBarHeight,
                    plotHeight * point.RelativeHeight * Math.Clamp(AnimationProgress, 0f, 1f));
                var y = plotBottom - barHeight;
                canvas.FillColor = AssetTrendChartDrawable.GetMonthColor(
                    point.MonthNumber,
                    UseDarkPalette);
                canvas.FillRoundedRectangle(x, y, columnWidth, barHeight, 7f);

                if (isSelected)
                {
                    canvas.StrokeColor = SelectionColor;
                    canvas.StrokeSize = 3f;
                    canvas.DrawRoundedRectangle(
                        x - 2f,
                        y - 2f,
                        columnWidth + 4f,
                        barHeight + 4f,
                        9f);
                }
            }
            else
            {
                canvas.FillColor = EmptyColor;
                canvas.FillRoundedRectangle(
                    x,
                    plotBottom - MinimumBarHeight,
                    columnWidth,
                    MinimumBarHeight,
                    4f);
                if (isSelected)
                {
                    canvas.StrokeColor = SelectionColor;
                    canvas.StrokeSize = 2f;
                    canvas.DrawRoundedRectangle(
                        x - 2f,
                        plotBottom - MinimumBarHeight - 2f,
                        columnWidth + 4f,
                        MinimumBarHeight + 4f,
                        6f);
                }
            }

            canvas.FontColor = isSelected ? SelectionColor : LabelColor;
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
        var spacing = BarSpacing * Math.Max(0, points.Count - 1);
        var availableWidth = Math.Max(1f, width - (HorizontalPadding * 2f) - spacing);
        return Math.Max(28f, availableWidth / Math.Max(1, points.Count));
    }

    private sealed record ChartPoint(
        int MonthKey,
        int MonthNumber,
        string Label,
        bool HasSnapshot,
        float RelativeHeight);
}
