using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WidgetSnapshot))]
[JsonSerializable(typeof(WidgetSummary))]
[JsonSerializable(typeof(WidgetMonthSnapshot))]
[JsonSerializable(typeof(AssetWidgetSnapshot))]
[JsonSerializable(typeof(AssetWidgetMonthSnapshot))]
[JsonSerializable(typeof(AssetWidgetSummary))]
[JsonSerializable(typeof(AssetWidgetItem))]
internal sealed partial class WidgetJsonSerializerContext : JsonSerializerContext
{
}
