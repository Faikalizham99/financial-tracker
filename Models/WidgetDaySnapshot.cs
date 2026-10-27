using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

public sealed record WidgetDaySnapshot(
    [property: JsonPropertyName("day")] int Day,
    [property: JsonPropertyName("transactionCount")] int TransactionCount,
    [property: JsonPropertyName("withInvestmentExpenseMinor")] long WithInvestmentExpenseMinor,
    [property: JsonPropertyName("withoutInvestmentExpenseMinor")] long WithoutInvestmentExpenseMinor,
    [property: JsonPropertyName("withInvestmentBudgetIncomeMinor")] long WithInvestmentBudgetIncomeMinor,
    [property: JsonPropertyName("withoutInvestmentBudgetIncomeMinor")] long WithoutInvestmentBudgetIncomeMinor);
