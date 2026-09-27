namespace FinancialTracker.Helpers;

public static class ThemeResourceBindings
{
    public static Color GetColor(string resourceKey, string fallback) =>
        Application.Current?.Resources.TryGetValue(resourceKey, out var value) == true &&
        value is Color color
            ? color
            : Color.FromArgb(fallback);

    public static Color GetThemeColor(
        string lightResourceKey,
        string darkResourceKey,
        string fallback)
    {
        var resourceKey = Application.Current?.RequestedTheme == AppTheme.Dark
            ? darkResourceKey
            : lightResourceKey;
        return GetColor(resourceKey, fallback);
    }

    public static void SetColor(
        Element target,
        BindableProperty property,
        string lightResourceKey,
        string darkResourceKey)
    {
        var resources = Application.Current?.Resources ??
            throw new InvalidOperationException("Application resources are not available.");

        Reset(target, property);
        target.SetAppThemeColor(
            property,
            (Color)resources[lightResourceKey],
            (Color)resources[darkResourceKey]);
    }

    public static void SetBrush(
        Element target,
        BindableProperty property,
        string lightResourceKey,
        string darkResourceKey)
    {
        var resources = Application.Current?.Resources ??
            throw new InvalidOperationException("Application resources are not available.");

        Reset(target, property);
        target.SetAppTheme(
            property,
            new SolidColorBrush((Color)resources[lightResourceKey]),
            new SolidColorBrush((Color)resources[darkResourceKey]));
    }

    public static void SetDynamic(
        Element target,
        BindableProperty property,
        string resourceKey)
    {
        Reset(target, property);
        target.SetDynamicResource(property, resourceKey);
    }

    public static void SetStatic(
        Element target,
        BindableProperty property,
        object? value)
    {
        Reset(target, property);
        target.SetValue(property, value);
    }

    private static void Reset(Element target, BindableProperty property)
    {
        target.RemoveDynamicResource(property);
        target.ClearValue(property);
    }
}
