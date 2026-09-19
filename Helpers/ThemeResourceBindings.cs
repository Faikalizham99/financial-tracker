namespace FinancialTracker.Helpers;

public static class ThemeResourceBindings
{
    public static void SetColor(
        BindableObject target,
        BindableProperty property,
        string lightResourceKey,
        string darkResourceKey)
    {
        var resources = Application.Current?.Resources ??
            throw new InvalidOperationException("Application resources are not available.");

        target.SetAppThemeColor(
            property,
            (Color)resources[lightResourceKey],
            (Color)resources[darkResourceKey]);
    }

    public static void SetBrush(
        BindableObject target,
        BindableProperty property,
        string lightResourceKey,
        string darkResourceKey)
    {
        var resources = Application.Current?.Resources ??
            throw new InvalidOperationException("Application resources are not available.");

        target.SetAppTheme(
            property,
            new SolidColorBrush((Color)resources[lightResourceKey]),
            new SolidColorBrush((Color)resources[darkResourceKey]));
    }
}
