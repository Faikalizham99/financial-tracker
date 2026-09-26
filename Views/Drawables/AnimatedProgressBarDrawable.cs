using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

public sealed class AnimatedProgressBarDrawable : IDrawable
{
    private const float MinimumVisibleProgress = 0.001f;

    private float startingProgress;
    private float targetProgress;
    private float animationProgress = 1f;

    public Color ProgressColor { get; set; } = Color.FromArgb("#5044E4");

    public Color TrackColor { get; set; } = Color.FromArgb("#E9E3DB");

    public float AnimationProgress
    {
        get => animationProgress;
        set => animationProgress = Math.Clamp(value, 0f, 1f);
    }

    public bool SetProgress(double value, bool animate)
    {
        var displayedProgress = Interpolate(startingProgress, targetProgress);
        var nextProgress = Math.Clamp((float)value, 0f, 1f);
        var hasTransition = animate &&
            Math.Abs(nextProgress - displayedProgress) >= MinimumVisibleProgress;

        targetProgress = nextProgress;
        startingProgress = hasTransition ? displayedProgress : targetProgress;
        AnimationProgress = hasTransition ? 0f : 1f;
        return hasTransition;
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        if (dirtyRect.Width <= 0 || dirtyRect.Height <= 0)
        {
            return;
        }

        var radius = dirtyRect.Height / 2f;
        canvas.FillColor = TrackColor;
        canvas.FillRoundedRectangle(dirtyRect, radius);

        var progress = Interpolate(startingProgress, targetProgress);
        var progressWidth = dirtyRect.Width * progress;
        if (progressWidth > 0)
        {
            canvas.FillColor = ProgressColor;
            canvas.FillRoundedRectangle(
                dirtyRect.Left,
                dirtyRect.Top,
                Math.Max(dirtyRect.Height, progressWidth),
                dirtyRect.Height,
                radius);
        }
    }

    private float Interpolate(float start, float target) =>
        start + ((target - start) * AnimationProgress);
}
