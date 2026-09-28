using Microsoft.Maui.Graphics;

namespace FinancialTracker.Views.Drawables;

internal static class AssetChartPalette
{
    private static readonly IReadOnlyDictionary<string, Color> AssetColors =
        new Dictionary<string, Color>(StringComparer.Ordinal)
        {
            ["ambank"] = Color.FromArgb("#ED1C24"),
            ["asb"] = Color.FromArgb("#2E4F9E"),
            ["bank_islam"] = Color.FromArgb("#D4145A"),
            ["cash"] = Color.FromArgb("#8B6B3E"),
            ["cimb"] = Color.FromArgb("#79001C"),
            ["gxbank"] = Color.FromArgb("#7B2CBF"),
            ["kwsp"] = Color.FromArgb("#A77B00"),
            ["luno"] = Color.FromArgb("#102A56"),
            ["maybank"] = Color.FromArgb("#F4C300"),
            ["moomoo"] = Color.FromArgb("#FF6B00"),
            ["ryt_bank"] = Color.FromArgb("#5267E8"),
            ["standard_chartered"] = Color.FromArgb("#2EAD00"),
            ["touch_n_go_ewallet"] = Color.FromArgb("#0072CE"),
            ["versa"] = Color.FromArgb("#159A9C"),
            ["wahed"] = Color.FromArgb("#E3B341")
        };

    private static readonly Color HistoricalAssetColor = Color.FromArgb("#777381");

    public static Color GetAssetColor(string assetKey) =>
        AssetColors.GetValueOrDefault(assetKey, HistoricalAssetColor);
}
