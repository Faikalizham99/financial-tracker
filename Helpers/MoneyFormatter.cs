using System.Globalization;

namespace FinancialTracker.Helpers;

public static class MoneyFormatter
{
    public static string GetCurrencySymbol(string currencyCode) =>
        currencyCode.ToUpperInvariant() switch
        {
            "USD" => "$",
            "SGD" => "S$",
            "KRW" => "₩",
            _ => "RM"
        };

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
        var amount = Math.Abs(amountMinor) / 100m;
        return $"{sign}{signSpacing}{currencySymbol} {amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }
}
