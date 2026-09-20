namespace FinancialTracker.Helpers;

internal static class PlatformRotationAnimation
{
    private const string AnimationKey = "FinancialTrackerContinuousRotation";

    public static bool TryStart(VisualElement element, TimeSpan revolutionDuration)
    {
#if WINDOWS
        if (element.Handler?.PlatformView is not Microsoft.UI.Xaml.FrameworkElement nativeView)
        {
            return false;
        }

        var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview
            .GetElementVisual(nativeView);
        visual.CenterPoint = new System.Numerics.Vector3(
            (float)(nativeView.ActualWidth / 2),
            (float)(nativeView.ActualHeight / 2),
            0);

        var animation = visual.Compositor.CreateScalarKeyFrameAnimation();
        animation.InsertKeyFrame(
            1,
            360,
            visual.Compositor.CreateLinearEasingFunction());
        animation.Duration = revolutionDuration;
        animation.IterationBehavior =
            Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
        visual.StartAnimation("RotationAngleInDegrees", animation);
        return true;
#elif ANDROID
        if (element.Handler?.PlatformView is not Android.Views.View nativeView)
        {
            return false;
        }

        nativeView.Animate().Cancel();
        nativeView.Rotation = 0;
        nativeView
            .Animate()
            .Rotation(36_000)
            .SetDuration((long)revolutionDuration.TotalMilliseconds * 100)
            .SetInterpolator(new Android.Views.Animations.LinearInterpolator())
            .Start();
        return true;
#elif IOS || MACCATALYST
        if (element.Handler?.PlatformView is not UIKit.UIView nativeView)
        {
            return false;
        }

        nativeView.Layer.RemoveAnimation(AnimationKey);
        var animation = CoreAnimation.CABasicAnimation.FromKeyPath(
            "transform.rotation.z");
        animation.From = Foundation.NSNumber.FromDouble(0);
        animation.To = Foundation.NSNumber.FromDouble(Math.PI * 2);
        animation.Duration = revolutionDuration.TotalSeconds;
        animation.RepeatCount = float.PositiveInfinity;
        animation.TimingFunction = CoreAnimation.CAMediaTimingFunction.FromName(
            CoreAnimation.CAMediaTimingFunction.Linear);
        animation.RemovedOnCompletion = false;
        nativeView.Layer.AddAnimation(animation, AnimationKey);
        return true;
#else
        return false;
#endif
    }

    public static void Stop(VisualElement element)
    {
#if WINDOWS
        if (element.Handler?.PlatformView is Microsoft.UI.Xaml.FrameworkElement nativeView)
        {
            var visual = Microsoft.UI.Xaml.Hosting.ElementCompositionPreview
                .GetElementVisual(nativeView);
            visual.StopAnimation("RotationAngleInDegrees");
            visual.RotationAngleInDegrees = 0;
        }
#elif ANDROID
        if (element.Handler?.PlatformView is Android.Views.View nativeView)
        {
            nativeView.Animate().Cancel();
            nativeView.Rotation = 0;
        }
#elif IOS || MACCATALYST
        if (element.Handler?.PlatformView is UIKit.UIView nativeView)
        {
            nativeView.Layer.RemoveAnimation(AnimationKey);
        }
#endif
    }
}
