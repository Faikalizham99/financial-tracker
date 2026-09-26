namespace FinancialTracker.Models;

public sealed record TransactionSearchPage(
    IReadOnlyList<TransactionRecord> Records,
    int NextOffset,
    bool HasMore);
