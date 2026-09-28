using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed record AssetDonutSlice(
    string Key,
    string Label,
    long AmountMinor,
    Color Color);

public sealed class AssetDonutChartDrawable : IDrawable
{
    private const float StartAngle = -90f;
    private const float MaximumSeamOverlap = 0.2f;

    private IReadOnlyList<AssetDonutSlice> slices = [];

    public Color TrackColor { get; set; } = Color.FromArgb("#E9E3DB");

    public Color SurfaceColor { get; set; } = Color.FromArgb("#FFFEFC");

    public Color PrimaryTextColor { get; set; } = Color.FromArgb("#17152C");

    public Color SecondaryTextColor { get; set; } = Color.FromArgb("#686273");

    public float AnimationProgress { get; set; } = 1f;

    public float SelectionProgress { get; set; } = 1f;

    public string TotalText { get; set; } = string.Empty;

    public string? SelectedKey { get; private set; }

    public AssetDonutSlice? SelectedSlice => slices.FirstOrDefault(
        item => item.Key.Equals(SelectedKey, StringComparison.Ordinal));

    public void SetSlices(IReadOnlyList<AssetDonutSlice> source, bool animate)
    {
        slices = source.Where(item => item.AmountMinor > 0).ToList();
        if (SelectedKey is not null &&
            slices.All(item => !item.Key.Equals(SelectedKey, StringComparison.Ordinal)))
        {
            SelectedKey = null;
        }

        AnimationProgress = animate ? 0f : 1f;
    }

    public bool Select(PointF touch, float width, float height)
    {
        if (slices.Count == 0 || width <= 0 || height <= 0)
        {
            return false;
        }

        var center = new PointF(width / 2f, height / 2f);
        var radius = Math.Min(width, height) / 2f;
        var thickness = Math.Max(24f, radius * 0.23f);
        var distance = MathF.Sqrt(
            MathF.Pow(touch.X - center.X, 2) +
            MathF.Pow(touch.Y - center.Y, 2));
        if (distance < radius - thickness - 8f || distance > radius + 4f)
        {
            return false;
        }

        var angle = MathF.Atan2(touch.Y - center.Y, touch.X - center.X) * 180f / MathF.PI;
        angle = (angle - StartAngle + 360f) % 360f;
        var total = slices.Sum(item => item.AmountMinor);
        var accumulated = 0f;
        foreach (var slice in slices)
        {
            var sweep = total <= 0 ? 0f : 360f * slice.AmountMinor / total;
            if (angle >= accumulated && angle <= accumulated + sweep)
            {
                SelectedKey = SelectedKey == slice.Key ? null : slice.Key;
                return true;
            }

            accumulated += sweep;
        }

        return false;
    }

    public void ClearSelection() => SelectedKey = null;

    public void SelectKey(string key)
    {
        if (slices.Any(item => item.Key.Equals(key, StringComparison.Ordinal)))
        {
            SelectedKey = SelectedKey == key ? null : key;
        }
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            return;
        }

        var size = Math.Min(dirtyRect.Width, dirtyRect.Height);
        var thickness = Math.Max(24f, size * 0.115f);
        var center = dirtyRect.Center;
        var outerRadius = (size / 2f) - 8f;
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
        if (total > 0 && progress > 0)
        {
            var currentAngle = StartAngle;
            foreach (var slice in slices)
            {
                var fullSweep = 360f * slice.AmountMinor / total;
                var visibleSweep = fullSweep * progress;
                if (visibleSweep > 0)
                {
                    // Slightly overlap neighbouring paths to avoid anti-aliased
                    // hairline seams without introducing visible separators.
                    var overlap = Math.Min(MaximumSeamOverlap, fullSweep * 0.1f) * progress;
                    var selectionExpansion = SelectedKey == slice.Key
                        ? 5f * Math.Clamp(SelectionProgress, 0f, 1f)
                        : 0f;
                    canvas.FillColor = slice.Color;
                    canvas.FillPath(CreateRingSegment(
                        center,
                        Math.Max(1f, innerRadius - selectionExpansion),
                        outerRadius + selectionExpansion,
                        currentAngle - overlap,
                        visibleSweep + (overlap * 2f)));
                }

                currentAngle += fullSweep;
            }
        }

        var selected = SelectedSlice;
        var mainText = selected?.Label ?? TotalText;
        var caption = selected is null ? "Portfolio" : "Selected asset";
        canvas.FontColor = PrimaryTextColor;
        canvas.FontSize = mainText.Length switch
        {
            > 18 => 13f,
            > 14 => 15f,
            > 10 => 17f,
            _ => 20f
        };
        canvas.Font = new Microsoft.Maui.Graphics.Font("OpenSansSemibold");
        canvas.DrawString(
            mainText,
            dirtyRect.Left + (size * 0.21f),
            dirtyRect.Center.Y - 23f,
            size * 0.58f,
            30f,
            HorizontalAlignment.Center,
            VerticalAlignment.Center);
        canvas.Font = Microsoft.Maui.Graphics.Font.Default;
        canvas.FontSize = 11f;
        canvas.FontColor = SecondaryTextColor;
        canvas.DrawString(
            caption,
            dirtyRect.Left + (size * 0.21f),
            dirtyRect.Center.Y + 5f,
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
        var outerStart = PointOnCircle(center, outerRadius, startAngle);
        path.MoveTo(outerStart);

        for (var index = 1; index <= segmentCount; index++)
        {
            var angle = startAngle + (sweepAngle * index / segmentCount);
            path.LineTo(PointOnCircle(center, outerRadius, angle));
        }

        for (var index = segmentCount; index >= 0; index--)
        {
            var angle = startAngle + (sweepAngle * index / segmentCount);
            path.LineTo(PointOnCircle(center, innerRadius, angle));
        }

        path.Close();
        return path;
    }

    private static PointF OffsetPoint(PointF origin, float angle, float distance)
    {
        var radians = angle * MathF.PI / 180f;
        return new PointF(
            origin.X + (MathF.Cos(radians) * distance),
            origin.Y + (MathF.Sin(radians) * distance));
    }

    private static PointF PointOnCircle(PointF center, float radius, float angle) =>
        OffsetPoint(center, angle, radius);
}
