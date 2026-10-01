using System.Text.Json.Serialization;

namespace FinancialTracker.Models;

public sealed record WidgetSnapshot(
    [property: JsonPropertyName("version")] int Version,
    [property: JsonPropertyName("monthText")] string MonthText,
    [property: JsonPropertyName("availableText")] string AvailableText,
    [property: JsonPropertyName("incomeText")] string IncomeText,
    [property: JsonPropertyName("expenseText")] string ExpenseText,
    [property: JsonPropertyName("transactionCount")] int TransactionCount,
    [property: JsonPropertyName("hasBudget")] bool HasBudget,
    [property: JsonPropertyName("budgetSpentText")] string BudgetSpentText,
    [property: JsonPropertyName("budgetLimitText")] string BudgetLimitText,
    [property: JsonPropertyName("budgetUsageText")] string BudgetUsageText,
    [property: JsonPropertyName("budgetRemainingText")] string BudgetRemainingText,
    [property: JsonPropertyName("budgetProgress")] double BudgetProgress,
    [property: JsonPropertyName("includeInvestment")] bool IncludeInvestment,
    [property: JsonPropertyName("withInvestment")] WidgetSummary WithInvestment,
    [property: JsonPropertyName("withoutInvestment")] WidgetSummary WithoutInvestment,
    [property: JsonPropertyName("currentMonthKey")] int CurrentMonthKey,
    [property: JsonPropertyName("months")] IReadOnlyList<WidgetMonthSnapshot> Months,
    [property: JsonPropertyName("updatedAtUnixSeconds")] long UpdatedAtUnixSeconds);
