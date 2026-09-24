using SQLite;

namespace FinancialTracker.Models;

[Table("MonthlyBudgets")]
public sealed class MonthlyBudgetRecord
{
    [PrimaryKey]
    public int MonthKey { get; set; }

    public long BudgetIncludingInvestmentMinor { get; set; }

    public long BudgetExcludingInvestmentMinor { get; set; }

    [NotNull]
    public string CurrencyCode { get; set; } = "MYR";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
