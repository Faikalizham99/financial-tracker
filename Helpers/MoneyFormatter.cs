using System.Globalization;

namespace FinancialTracker.Helpers;

public static class MoneyFormatter
{
    public static string FormatMinorValue(long amountMinor) =>
        (Math.Abs(amountMinor) / 100m).ToString("N2", CultureInfo.InvariantCulture);

    public static string FormatMinor(
        long amountMinor,
        string currencySymbol,
        bool showPositiveSign = false,
        bool separateSign = false)
    {
        var sign = amountMinor switch
        {
            < 0 => "−",
            > 0 when showPositiveSign => "+",
            _ => string.Empty
        };
        var signSpacing = sign.Length > 0 && separateSign ? " " : string.Empty;
        return $"{sign}{signSpacing}{currencySymbol} {FormatMinorValue(amountMinor)}";
    }
}
