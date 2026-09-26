namespace FinancialTracker.Models;

public sealed record TransactionDataSnapshot(
    IReadOnlyList<TransactionRecord> DashboardRecords,
    IReadOnlyList<TransactionRecord> PeriodRecords,
    IReadOnlyList<TransactionRecord> DescriptionHistoryRecords);
