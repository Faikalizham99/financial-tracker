using System.Globalization;
using FinancialTracker.Helpers;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed class AssetHistoryLineChartDrawable : IDrawable
{
    private const float HorizontalPadding = 24f;
    private const float TopPadding = 14f;
    private const float LabelHeight = 34f;
    private const float PointRadius = 4.5f;
    private const float SelectedPointRadius = 6.5f;

    private IReadOnlyList<ChartPoint> points = [];

    public bool UseDarkPalette { get; set; }

    public Color LineColor { get; set; } = Color.FromArgb("#5044E4");

    public Color LabelColor { get; set; } = Color.FromArgb("#686273");

    public Color GridColor { get; set; } = Color.FromArgb("#E9E3DB");

    public Color SelectionColor { get; set; } = Color.FromArgb("#64D8E4");

    public Color SelectionBackgroundColor { get; set; } = Color.FromArgb("#E4F8FA");

    public float AnimationProgress { get; set; } = 1f;

    public int? SelectedMonthKey { get; set; }

    public IReadOnlyList<AssetHistoryChartPoint> SourcePoints { get; private set; } = [];

    public void SetPoints(IReadOnlyList<AssetHistoryChartPoint> source, bool animate)
    {
        SourcePoints = source;
        points = source
            .Select(item => new ChartPoint(
                MonthKeyConverter.FromDate(item.Month),
                item.Month.Month,
                item.Month.ToString("MMM", CultureInfo.CurrentCulture),
                item.TotalMinor))
            .ToList();
        AnimationProgress = animate ? 0f : 1f;
    }

    public bool Select(PointF touch, float width, float height)
    {
        if (points.Count == 0 ||
            width <= HorizontalPadding * 2f ||
            height <= TopPadding + LabelHeight ||
            touch.X < 0 ||
            touch.X > width)
        {
            return false;
        }

        var index = GetNearestPointIndex(touch.X, width);
        if (index < 0 || index >= points.Count)
        {
            return false;
        }

        SelectedMonthKey = points[index].MonthKey;
        return true;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (points.Count == 0 ||
            dirtyRect.Width <= HorizontalPadding * 2f ||
            dirtyRect.Height <= TopPadding + LabelHeight)
        {
            return;
        }

        var plotTop = dirtyRect.Top + TopPadding;
        var plotBottom = dirtyRect.Bottom - LabelHeight;
        var plotHeight = Math.Max(1f, plotBottom - plotTop);
        var maximum = Math.Max(
            1L,
            points
                .Where(point => point.TotalMinor.HasValue)
                .Select(point => Math.Max(0, point.TotalMinor!.Value))
                .DefaultIfEmpty(0)
                .Max());
        var progress = Math.Clamp(AnimationProgress, 0f, 1f);
        var coordinates = new PointF?[points.Count];

        DrawSelectionBackground(canvas, dirtyRect, plotTop, plotBottom);

        canvas.StrokeColor = GridColor;
        canvas.StrokeSize = 1f;
        canvas.DrawLine(
            dirtyRect.Left + HorizontalPadding,
            plotBottom,
            dirtyRect.Right - HorizontalPadding,
            plotBottom);

        for (var index = 0; index < points.Count; index++)
        {
            var value = points[index].TotalMinor;
            if (!value.HasValue)
            {
                continue;
            }

            var x = GetPointX(index, dirtyRect.Left, dirtyRect.Width);
            var targetY = plotBottom -
                ((Math.Max(0, value.Value) / (float)maximum) * plotHeight);
            var y = plotBottom + ((targetY - plotBottom) * progress);
            coordinates[index] = new PointF(x, y);
        }

        canvas.StrokeColor = LineColor;
        canvas.StrokeSize = 2.8f;
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        for (var index = 1; index < coordinates.Length; index++)
        {
            if (coordinates[index - 1] is not PointF previous ||
                coordinates[index] is not PointF current)
            {
                continue;
            }

            canvas.DrawLine(previous.X, previous.Y, current.X, current.Y);
        }

        canvas.FontSize = 11f;
        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var x = GetPointX(index, dirtyRect.Left, dirtyRect.Width);
            var isSelected = SelectedMonthKey == point.MonthKey;
            if (coordinates[index] is PointF coordinate)
            {
                canvas.FillColor = AssetTrendChartDrawable.GetMonthColor(
                    point.MonthNumber,
                    UseDarkPalette);
                canvas.FillCircle(
                    coordinate.X,
                    coordinate.Y,
                    isSelected ? SelectedPointRadius : PointRadius);
                if (isSelected)
                {
                    canvas.StrokeColor = SelectionColor;
                    canvas.StrokeSize = 2.5f;
                    canvas.DrawCircle(
                        coordinate.X,
                        coordinate.Y,
                        SelectedPointRadius + 2f);
                }
            }
            else
            {
                canvas.FillColor = GridColor;
                canvas.FillCircle(x, plotBottom, isSelected ? 4.5f : 3f);
            }

            canvas.FontColor = isSelected ? SelectionColor : LabelColor;
            canvas.DrawString(
                point.Label,
                x - 24f,
                plotBottom + 3f,
                48f,
                LabelHeight - 3f,
                HorizontalAlignment.Center,
                VerticalAlignment.Center);
        }
    }

    private void DrawSelectionBackground(
        ICanvas canvas,
        RectF dirtyRect,
        float plotTop,
        float plotBottom)
    {
        var selectedIndex = points
            .Select((point, index) => new { point.MonthKey, Index = index })
            .FirstOrDefault(item => item.MonthKey == SelectedMonthKey)
            ?.Index;
        if (!selectedIndex.HasValue)
        {
            return;
        }

        var x = GetPointX(selectedIndex.Value, dirtyRect.Left, dirtyRect.Width);
        canvas.FillColor = SelectionBackgroundColor;
        canvas.FillRoundedRectangle(
            x - 23f,
            plotTop - 5f,
            46f,
            plotBottom - plotTop + LabelHeight + 3f,
            11f);
    }

    private int GetNearestPointIndex(float x, float width)
    {
        if (points.Count == 1)
        {
            return 0;
        }

        var usableWidth = Math.Max(1f, width - (HorizontalPadding * 2f));
        var position = (x - HorizontalPadding) / usableWidth;
        return Math.Clamp(
            (int)Math.Round(position * (points.Count - 1)),
            0,
            points.Count - 1);
    }

    private float GetPointX(int index, float left, float width)
    {
        if (points.Count == 1)
        {
            return left + (width / 2f);
        }

        var usableWidth = Math.Max(1f, width - (HorizontalPadding * 2f));
        return left + HorizontalPadding +
            (index / (float)(points.Count - 1) * usableWidth);
    }

    private sealed record ChartPoint(
        int MonthKey,
        int MonthNumber,
        string Label,
        long? TotalMinor);
}
