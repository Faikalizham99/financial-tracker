using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WidgetSnapshot))]
[JsonSerializable(typeof(WidgetSummary))]
internal sealed partial class WidgetJsonSerializerContext : JsonSerializerContext
{
}
