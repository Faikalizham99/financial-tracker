using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

public sealed record AssetWidgetSnapshot(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("currentMonthKey")] int CurrentMonthKey,
    [property: JsonPropertyName("months")] IReadOnlyList<AssetWidgetMonthSnapshot> Months,
    [property: JsonPropertyName("updatedAtUnixSeconds")] long UpdatedAtUnixSeconds);

public sealed record AssetWidgetMonthSnapshot(
    [property: JsonPropertyName("monthKey")] int MonthKey,
    [property: JsonPropertyName("monthText")] string MonthText,
    [property: JsonPropertyName("hasSnapshot")] bool HasSnapshot,
    [property: JsonPropertyName("comparisonDateText")] string ComparisonDateText,
    [property: JsonPropertyName("withKwsp")] AssetWidgetSummary WithKwsp,
    [property: JsonPropertyName("withoutKwsp")] AssetWidgetSummary WithoutKwsp,
    [property: JsonPropertyName("assets")] IReadOnlyList<AssetWidgetItem> Assets);

public sealed record AssetWidgetSummary(
    [property: JsonPropertyName("totalText")] string TotalText,
    [property: JsonPropertyName("changeText")] string ChangeText,
    [property: JsonPropertyName("accessibleText")] string AccessibleText,
    [property: JsonPropertyName("accessibleChangeText")] string AccessibleChangeText,
    [property: JsonPropertyName("changeDirection")] int ChangeDirection,
    [property: JsonPropertyName("accessibleChangeDirection")] int AccessibleChangeDirection);

public sealed record AssetWidgetItem(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("previousText")] string PreviousText,
    [property: JsonPropertyName("currentText")] string CurrentText,
    [property: JsonPropertyName("changeText")] string ChangeText,
    [property: JsonPropertyName("changeDirection")] int ChangeDirection,
    [property: JsonPropertyName("isKwsp")] bool IsKwsp);
