using FinancialTracker.Models;
using Microsoft.Maui.Graphics;

namespace FinancialTracker.Helpers;

public static class TransactionChartPalette
{
    private static readonly IReadOnlyDictionary<string, string> CategoryColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Beauty"] = "#EC4899",
            ["Bills & Utilities"] = "#7C3AED",
            ["Entertainment"] = "#A855F7",
            ["Food & Drinks"] = "#F59E0B",
            ["Gift"] = "#E11D8A",
            ["Groceries"] = "#65A30D",
            ["Health"] = "#0D9488",
            ["Investment"] = "#16856B",
            ["Shopping"] = "#F97360",
            ["Subscription"] = "#6366F1",
            ["Transportation"] = "#0284C7",
            ["Bonus"] = "#D97706",
            ["Cashback"] = "#14B8A6",
            ["Refund"] = "#3B82F6",
            ["Salary"] = "#16A34A",
            ["Others"] = "#777381"
        };

    private static readonly IReadOnlyDictionary<string, string> PaymentMethodColors =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AmBank"] = "#ED1C24",
            ["AMEX Maybank"] = "#2F6DB5",
            ["Bank Islam"] = "#D4145A",
            ["Cash"] = "#B7791F",
            ["CIMB Bank"] = "#79001C",
            ["GXBank"] = "#7B2CBF",
            ["Maybank"] = "#E0A800",
            ["Ryt Bank"] = "#5267E8",
            ["Standard Chartered"] = "#2EAD66",
            ["Touch N Go eWallet"] = "#0072CE",
            ["Touch N Go NFC Card"] = "#2458A6",
            ["VISA Maybank"] = "#1A4F9C",
            ["Others"] = "#777381"
        };

    public static Color GetColor(
        string key,
        TransactionStatisticsBreakdownDimension dimension)
    {
        var palette = dimension == TransactionStatisticsBreakdownDimension.Category
            ? CategoryColors
            : PaymentMethodColors;
        return Color.FromArgb(palette.GetValueOrDefault(key, "#777381"));
    }
}
