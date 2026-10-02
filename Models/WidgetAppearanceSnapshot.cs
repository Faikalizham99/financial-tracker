using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

public sealed record WidgetAppearanceSnapshot(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("theme")] string Theme,
    [property: JsonPropertyName("accentColorHex")] string AccentColorHex);
