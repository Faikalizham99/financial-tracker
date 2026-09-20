namespace FinancialTracker.Helpers;

public static class AppearanceValueNormalizer
{
    public const string DefaultTheme = "Light";
    public const string DefaultAccentColor = "#5044E4";

    public static string NormalizeTheme(string? theme) =>
        theme?.Trim().ToLowerInvariant() switch
        {
            "system" => "System",
            "dark" => "Dark",
            _ => DefaultTheme
        };

    public static string NormalizeAccentColor(string? value) =>
        TryNormalizeAccentColor(value, out var normalized)
            ? normalized
            : DefaultAccentColor;

    public static bool TryNormalizeAccentColor(
        string? value,
        out string normalized)
    {
        var hex = value?.Trim().TrimStart('#') ?? string.Empty;
        if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
        {
            hex = string.Concat(hex.Select(character => $"{character}{character}"));
        }

        if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
        {
            normalized = $"#{hex.ToUpperInvariant()}";
            return true;
        }

        normalized = string.Empty;
        return false;
    }
}
