using FinancialTracker.Models;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed class TransactionStatisticsCompositionDrawable : IDrawable
{
    private const float StartAngle = -90f;
    private const float MaximumSeamOverlap = 0.2f;
    private IReadOnlyList<TransactionStatisticsCompositionSlice> slices = [];

    public Color TrackColor { get; set; } = Color.FromArgb("#E9E3DB");
    public Color SurfaceColor { get; set; } = Color.FromArgb("#FFFEFC");
    public Color PrimaryTextColor { get; set; } = Color.FromArgb("#17152C");
    public Color SecondaryTextColor { get; set; } = Color.FromArgb("#686273");
    public float AnimationProgress { get; set; } = 1f;
    public float SelectionProgress { get; set; } = 1f;
    public string TotalText { get; set; } = string.Empty;
    public string? SelectedKey { get; private set; }

    public void SetSlices(
        IReadOnlyList<TransactionStatisticsCompositionSlice> source,
        string? selectedKey,
        bool animate)
    {
        slices = source.Where(item => item.AmountMinor > 0).ToList();
        SelectedKey = selectedKey is not null && slices.Any(item =>
            item.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase))
                ? selectedKey
                : null;
        AnimationProgress = animate ? 0f : 1f;
    }

    public bool Select(PointF touch, float width, float height)
    {
        if (slices.Count == 0 || width <= 0 || height <= 0)
        {
            return false;
        }

        var size = Math.Min(width, height);
        var center = new PointF(width / 2f, height / 2f);
        var outerRadius = (size / 2f) - 8f;
        var thickness = Math.Max(24f, size * 0.115f);
        var distance = MathF.Sqrt(
            MathF.Pow(touch.X - center.X, 2) +
            MathF.Pow(touch.Y - center.Y, 2));
        if (distance < outerRadius - thickness - 8f || distance > outerRadius + 4f)
        {
            return false;
        }

        var angle = MathF.Atan2(touch.Y - center.Y, touch.X - center.X) *
            180f / MathF.PI;
        angle = (angle - StartAngle + 360f) % 360f;
        var total = slices.Sum(item => item.AmountMinor);
        var accumulated = 0f;
        foreach (var slice in slices)
        {
            var sweep = total <= 0 ? 0f : 360f * slice.AmountMinor / total;
            if (angle >= accumulated && angle <= accumulated + sweep)
            {
                SelectedKey = string.Equals(
                    SelectedKey,
                    slice.Key,
                    StringComparison.OrdinalIgnoreCase)
                        ? null
                        : slice.Key;
                return true;
            }

            accumulated += sweep;
        }

        return false;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            return;
        }

        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        var center = dirtyRect.Center;
        var outerRadius = (size / 2f) - 8f;
        var thickness = Math.Max(24f, size * 0.115f);
        var innerRadius = Math.Max(1f, outerRadius - thickness);
        canvas.FillColor = slices.Count == 0 ? TrackColor : SurfaceColor;
        canvas.FillEllipse(
            center.X - outerRadius,
            center.Y - outerRadius,
            outerRadius * 2f,
            outerRadius * 2f);
        canvas.FillColor = SurfaceColor;
        canvas.FillEllipse(
            center.X - innerRadius,
            center.Y - innerRadius,
            innerRadius * 2f,
            innerRadius * 2f);

        var total = slices.Sum(item => item.AmountMinor);
        var progress = Math.Clamp(AnimationProgress, 0f, 1f);
        var currentAngle = StartAngle;
        foreach (var slice in slices)
        {
            var fullSweep = total <= 0 ? 0f : 360f * slice.AmountMinor / total;
            var visibleSweep = fullSweep * progress;
            if (visibleSweep > 0)
            {
                var overlap = Math.Min(MaximumSeamOverlap, fullSweep * 0.1f) * progress;
                var expansion = string.Equals(
                    SelectedKey,
                    slice.Key,
                    StringComparison.OrdinalIgnoreCase)
                        ? 5f * Math.Clamp(SelectionProgress, 0f, 1f)
                        : 0f;
                canvas.FillColor = slice.Color;
                canvas.FillPath(CreateRingSegment(
                    center,
                    Math.Max(1f, innerRadius - expansion),
                    outerRadius + expansion,
                    currentAngle - overlap,
                    visibleSweep + (overlap * 2f)));

                var share = slice.AmountMinor / (double)total;
                if (share >= 0.07d && progress >= 0.72f)
                {
                    var labelPoint = PointOnCircle(
                        center,
                        (innerRadius + outerRadius) / 2f,
                        currentAngle + (fullSweep / 2f));
                    canvas.Font = new Microsoft.Maui.Graphics.Font("OpenSansSemibold");
                    canvas.FontSize = 9.5f;
                    canvas.FontColor = Colors.White;
                    canvas.DrawString(
                        $"{share * 100d:0.#}%",
                        labelPoint.X - 24f,
                        labelPoint.Y - 9f,
                        48f,
                        18f,
                        HorizontalAlignment.Center,
                        VerticalAlignment.Center);
                }
            }

            currentAngle += fullSweep;
        }

        var selected = slices.FirstOrDefault(item => string.Equals(
            item.Key,
            SelectedKey,
            StringComparison.OrdinalIgnoreCase));
        var mainText = selected?.Label ?? TotalText;
        canvas.Font = new Microsoft.Maui.Graphics.Font("OpenSansSemibold");
        canvas.FontColor = PrimaryTextColor;
        canvas.FontSize = mainText.Length switch
        {
            > 18 => 12f,
            > 14 => 14f,
            > 10 => 16f,
            _ => 19f
        };
        canvas.DrawString(
            mainText,
            center.X - (size * 0.29f),
            center.Y - 22f,
            size * 0.58f,
            30f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 11f;
        canvas.FontColor = SecondaryTextColor;
        canvas.DrawString(
            selected is null ? "Total" : "Selected",
            center.X - (size * 0.29f),
            center.Y + 5f,
            size * 0.58f,
            22f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
    }

    private static PathF CreateRingSegment(
        PointF center,
        float innerRadius,
        float outerRadius,
        float startAngle,
        float sweepAngle)
    {
        var path = new PathF();
        var segmentCount = Math.Max(2, (int)Math.Ceiling(sweepAngle / 3f));
        path.MoveTo(PointOnCircle(center, outerRadius, startAngle));
        for (var index = 1; index <= segmentCount; index++)
        {
            path.LineTo(PointOnCircle(
                center,
                outerRadius,
                startAngle + (sweepAngle * index / segmentCount)));
        }

        for (var index = segmentCount; index >= 0; index--)
        {
            path.LineTo(PointOnCircle(
                center,
                innerRadius,
                startAngle + (sweepAngle * index / segmentCount)));
        }

        path.Close();
        return path;
    }

    private static PointF PointOnCircle(PointF center, float radius, float angle)
    {
        var radians = angle * MathF.PI / 180f;
        return new PointF(
            center.X + (MathF.Cos(radians) * radius),
            center.Y + (MathF.Sin(radians) * radius));
    }
}
