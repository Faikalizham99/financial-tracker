using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WidgetSnapshot))]
[JsonSerializable(typeof(WidgetSummary))]
[JsonSerializable(typeof(WidgetMonthSnapshot))]
internal sealed partial class WidgetJsonSerializerContext : JsonSerializerContext
{
}
