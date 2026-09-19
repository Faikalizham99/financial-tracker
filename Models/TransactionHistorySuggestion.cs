namespace FinancialTracker.Models;

public sealed record TransactionHistorySuggestion(
    string Description,
    TransactionOption TransactionType,
    TransactionOption Category,
    TransactionOption PaymentMethod)
{
    public string DetailText =>
        $"{TransactionType.Title} \u00B7 {Category.Title} \u00B7 {PaymentMethod.Title}";
}
