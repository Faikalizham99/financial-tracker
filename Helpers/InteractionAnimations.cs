namespace FinancialTracker.Helpers;

public static class InteractionAnimations
{
    public static Task PressAsync(object? sender) =>
        ScaleAsync(ResolveVisualElement(sender), 0.96, 55, Easing.CubicOut);

    public static Task ReleaseAsync(object? sender) =>
        ScaleAsync(ResolveVisualElement(sender), 1, 105, Easing.CubicOut);

    public static async Task PulseAsync(object? sender)
    {
        var element = ResolveVisualElement(sender);
        if (element is null || !element.IsEnabled)
        {
            return;
        }

        element.CancelAnimations();
        await element.ScaleToAsync(0.96, 55, Easing.CubicOut);
        await element.ScaleToAsync(1, 105, Easing.CubicOut);
    }

    private static async Task ScaleAsync(
        VisualElement? element,
        double scale,
        uint duration,
        Easing easing)
    {
        if (element is null || !element.IsEnabled)
        {
            return;
        }

        element.CancelAnimations();
        await element.ScaleToAsync(scale, duration, easing);
    }

    private static VisualElement? ResolveVisualElement(object? sender) =>
        sender switch
        {
            VisualElement element => element,
            GestureRecognizer gesture => gesture.Parent as VisualElement,
            _ => null
        };
}
