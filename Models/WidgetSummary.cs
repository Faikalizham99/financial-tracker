using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

public sealed record WidgetSummary(
    [property: JsonPropertyName("availableText")] string AvailableText,
    [property: JsonPropertyName("incomeText")] string IncomeText,
    [property: JsonPropertyName("expenseText")] string ExpenseText,
    [property: JsonPropertyName("hasBudget")] bool HasBudget,
    [property: JsonPropertyName("budgetSpentText")] string BudgetSpentText,
    [property: JsonPropertyName("budgetLimitText")] string BudgetLimitText,
    [property: JsonPropertyName("budgetUsageText")] string BudgetUsageText,
    [property: JsonPropertyName("budgetRemainingText")] string BudgetRemainingText,
    [property: JsonPropertyName("budgetProgress")] double BudgetProgress);
