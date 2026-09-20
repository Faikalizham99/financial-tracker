using FinancialTracker.Data;
using FinancialTracker.Models;
using SQLite;

namespace FinancialTracker.Services;

public static class AppearanceService
{
    private const string ThemePreferenceKey = "startup_theme";
    private const string AccentPreferenceKey = "startup_accent_color";
    private const string DefaultTheme = "Light";
    private const string DefaultAccentColor = "#5044E4";
    private static string? appliedTheme;
    private static string? appliedAccentColor;

    public static void ApplyStartupAppearance(Application application)
    {
        var cachedTheme = Preferences.Default.ContainsKey(ThemePreferenceKey)
            ? Preferences.Default.Get(ThemePreferenceKey, DefaultTheme)
            : null;
        var cachedAccent = Preferences.Default.ContainsKey(AccentPreferenceKey)
            ? Preferences.Default.Get(AccentPreferenceKey, DefaultAccentColor)
            : null;

        if (cachedTheme is null || cachedAccent is null)
        {
            var storedAppearance = TryReadStoredAppearance();
            cachedTheme ??= storedAppearance?.Theme ?? DefaultTheme;
            cachedAccent ??= storedAppearance?.AccentColorHex ?? DefaultAccentColor;
        }

        var theme = NormalizeTheme(cachedTheme);
        var accent = NormalizeAccentColor(cachedAccent);
        Cache(theme, accent);
        Apply(application, theme, accent);
    }

    public static void ApplyAndCache(string theme, string accentColorHex)
    {
        var normalizedTheme = NormalizeTheme(theme);
        var normalizedAccent = NormalizeAccentColor(accentColorHex);
        Cache(normalizedTheme, normalizedAccent);

        if (Application.Current is not null)
        {
            Apply(Application.Current, normalizedTheme, normalizedAccent);
        }
    }

    private static AppSettingsRecord? TryReadStoredAppearance()
    {
        try
        {
            if (!File.Exists(LocalDatabase.DatabasePath))
            {
                return null;
            }

            using var connection = new SQLiteConnection(
                LocalDatabase.DatabasePath,
                SQLiteOpenFlags.ReadOnly);
            return connection.Find<AppSettingsRecord>(1);
        }
        catch (SQLiteException)
        {
            return null;
        }
    }

    private static void Cache(string theme, string accentColorHex)
    {
        Preferences.Default.Set(ThemePreferenceKey, theme);
        Preferences.Default.Set(AccentPreferenceKey, accentColorHex);
    }

    private static void Apply(
        Application application,
        string theme,
        string accentColorHex)
    {
        if (string.Equals(appliedTheme, theme, StringComparison.Ordinal) &&
            string.Equals(
                appliedAccentColor,
                accentColorHex,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        application.UserAppTheme = theme switch
        {
            "System" => AppTheme.Unspecified,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Light
        };

        var useDarkTint = theme == "Dark" ||
            (theme == "System" && application.RequestedTheme == AppTheme.Dark);
        var tintHex = MixColors(
            accentColorHex,
            useDarkTint ? "#000000" : "#FFFFFF",
            useDarkTint ? 0.68 : 0.86);
        var resources = application.Resources;

        resources["Accent"] = Color.FromArgb(accentColorHex);
        resources["AccentDark"] = Color.FromArgb(accentColorHex);
        resources["AccentTint"] = Color.FromArgb(tintHex);
        resources["AccentTintLight"] = Color.FromArgb(tintHex);
        resources["AccentTintDark"] = Color.FromArgb(tintHex);
        resources["AccentForeground"] = Color.FromArgb(
            GetContrastColor(accentColorHex));
        resources["Primary"] = Color.FromArgb(accentColorHex);
        resources["PrimaryDark"] = Color.FromArgb(accentColorHex);
        resources["Secondary"] = Color.FromArgb(tintHex);
        appliedTheme = theme;
        appliedAccentColor = accentColorHex;
    }

    private static string NormalizeTheme(string? theme) =>
        theme?.Trim().ToLowerInvariant() switch
        {
            "system" => "System",
            "dark" => "Dark",
            _ => DefaultTheme
        };

    private static string NormalizeAccentColor(string? value)
    {
        var hex = value?.Trim().TrimStart('#') ?? string.Empty;
        if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
        {
            hex = string.Concat(hex.Select(character => $"{character}{character}"));
        }

        return hex.Length == 6 && hex.All(Uri.IsHexDigit)
            ? $"#{hex.ToUpperInvariant()}"
            : DefaultAccentColor;
    }

    private static string MixColors(
        string foreground,
        string background,
        double backgroundWeight)
    {
        var foregroundValue = Convert.ToInt32(foreground.TrimStart('#'), 16);
        var backgroundValue = Convert.ToInt32(background.TrimStart('#'), 16);
        var red = MixChannel(
            (foregroundValue >> 16) & 0xFF,
            (backgroundValue >> 16) & 0xFF,
            backgroundWeight);
        var green = MixChannel(
            (foregroundValue >> 8) & 0xFF,
            (backgroundValue >> 8) & 0xFF,
            backgroundWeight);
        var blue = MixChannel(
            foregroundValue & 0xFF,
            backgroundValue & 0xFF,
            backgroundWeight);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static int MixChannel(
        int foreground,
        int background,
        double backgroundWeight) =>
        (int)Math.Round(
            (foreground * (1 - backgroundWeight)) +
            (background * backgroundWeight));

    private static string GetContrastColor(string colorHex)
    {
        var value = Convert.ToInt32(colorHex.TrimStart('#'), 16);
        var red = (value >> 16) & 0xFF;
        var green = (value >> 8) & 0xFF;
        var blue = value & 0xFF;
        var luminance = ((0.299 * red) + (0.587 * green) + (0.114 * blue)) / 255;
        return luminance > 0.62 ? "#17142D" : "#FFFFFF";
    }
}
