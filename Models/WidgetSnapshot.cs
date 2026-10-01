using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

public sealed record WidgetSnapshot(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("monthText")] string MonthText,
    [property: JsonPropertyName("availableText")] string AvailableText,
    [property: JsonPropertyName("incomeText")] string IncomeText,
    [property: JsonPropertyName("expenseText")] string ExpenseText,
    [property: JsonPropertyName("transactionCount")] int TransactionCount,
    [property: JsonPropertyName("updatedAtUnixSeconds")] long UpdatedAtUnixSeconds);
