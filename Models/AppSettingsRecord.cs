using SQLite;

namespace FinancialTracker.Models;

[Table("AppSettings")]
public sealed class AppSettingsRecord
{
    public const string DefaultName = "Ryomen Sukuna";

    [PrimaryKey]
    public int Id { get; set; } = 1;

    [NotNull]
    public string Name { get; set; } = DefaultName;

    [NotNull]
    public string CurrencyCode { get; set; } = "MYR";

    [NotNull]
    public string Theme { get; set; } = "Light";

    public string AccentColorHex { get; set; } = "#5044E4";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
