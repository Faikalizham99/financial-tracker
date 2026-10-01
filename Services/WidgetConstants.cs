namespace FinancialTracker.Services;

public static class WidgetConstants
{
    public const string PrimaryAppGroupIdentifier =
        "group.com.faikalizham.financial-tracker";

    public static IReadOnlyList<string> AppGroupIdentifiers { get; } =
    [
        PrimaryAppGroupIdentifier,
        "group.f4c6f25ba5674ecb.1",
        "group.f4c6f25ba5674ecb.2",
        "group.f4c6f25ba5674ecb.3",
        "group.f4c6f25ba5674ecb.4",
        "group.f4c6f25ba5674ecb.5"
    ];

    public const string SnapshotFileName = "financial_tracker_widget.json";
}
