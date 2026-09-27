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

    // Birthstone-inspired colours, tuned separately for light and dark surfaces.
    // The month label remains the accessible identifier; colour is supplementary.
    private static readonly Color[] LightMonthColors =
    [
        Color.FromArgb("#B23A48"), // January — garnet
        Color.FromArgb("#7652B5"), // February — amethyst
        Color.FromArgb("#2787A8"), // March — aquamarine
        Color.FromArgb("#607D9B"), // April — diamond / crystal
        Color.FromArgb("#268653"), // May — emerald
        Color.FromArgb("#8A68A5"), // June — alexandrite / pearl
        Color.FromArgb("#CF3654"), // July — ruby
        Color.FromArgb("#78952B"), // August — peridot
        Color.FromArgb("#3159AD"), // September — sapphire
        Color.FromArgb("#CF6292"), // October — pink tourmaline
        Color.FromArgb("#BC7117"), // November — citrine / topaz
        Color.FromArgb("#14878D")  // December — turquoise
    ];

    private static readonly Color[] DarkMonthColors =
    [
        Color.FromArgb("#F06A78"), // January — garnet
        Color.FromArgb("#A98AE8"), // February — amethyst
        Color.FromArgb("#63C7E6"), // March — aquamarine
        Color.FromArgb("#AFC7DD"), // April — diamond / crystal
        Color.FromArgb("#5FC58B"), // May — emerald
        Color.FromArgb("#C0A4DB"), // June — alexandrite / pearl
        Color.FromArgb("#FF6B88"), // July — ruby
        Color.FromArgb("#A8CA58"), // August — peridot
        Color.FromArgb("#6F96F5"), // September — sapphire
        Color.FromArgb("#F199C0"), // October — pink tourmaline
        Color.FromArgb("#F2B85A"), // November — citrine / topaz
        Color.FromArgb("#50CFD0")  // December — turquoise
    ];

    private IReadOnlyList<ChartPoint> points = [];
    private IReadOnlyDictionary<int, float> startingHeights = new Dictionary<int, float>();
    private float animationProgress = 1f;

    public bool UseDarkPalette { get; set; }

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
                point.Month.Month,
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

        canvas.FontColor = LabelColor;
        canvas.FontSize = 10f;

        for (var index = 0; index < points.Count; index++)
        {
            var point = points[index];
            var relativeHeight = Interpolate(GetStartingHeight(point.MonthKey), point.RelativeHeight);
            var barHeight = Math.Max(MinimumBarHeight, plotHeight * relativeHeight);
            var x = dirtyRect.Left + (index * (columnWidth + BarSpacing));
            var y = dirtyRect.Top + plotHeight - barHeight;

            canvas.FillColor = GetMonthColor(point.MonthNumber);
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

    private Color GetMonthColor(int monthNumber)
    {
        var palette = UseDarkPalette ? DarkMonthColors : LightMonthColors;
        return palette[Math.Clamp(monthNumber, 1, 12) - 1];
    }

    private sealed record ChartPoint(
        int MonthKey,
        int MonthNumber,
        string Label,
        float RelativeHeight);
}
