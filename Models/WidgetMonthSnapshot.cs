using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

public sealed record WidgetMonthSnapshot(
    [property: JsonPropertyName("monthKey")] int MonthKey,
    [property: JsonPropertyName("monthText")] string MonthText,
    [property: JsonPropertyName("transactionCount")] int TransactionCount,
    [property: JsonPropertyName("withInvestment")] WidgetSummary WithInvestment,
    [property: JsonPropertyName("withoutInvestment")] WidgetSummary WithoutInvestment,
    [property: JsonPropertyName("days")] IReadOnlyList<WidgetDaySnapshot> Days);
