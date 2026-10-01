using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WidgetSnapshot))]
internal sealed partial class WidgetJsonSerializerContext : JsonSerializerContext
{
}
