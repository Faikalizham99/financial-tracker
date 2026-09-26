using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class AssetCatalog
{
    public const string KwspKey = "kwsp";

    public static IReadOnlyList<AssetCatalogItem> Items { get; } =
    [
        new(
            "ambank",
            "AmBank",
            "asset_ambank.png",
            "Bank",
            0,
            IsAccessible: true,
            IsActive: true),
        new(
            "asb",
            "ASB",
            "asset_asb.jpeg",
            "Investment",
            1,
            IsAccessible: true,
            IsActive: true),
        new(
            "bank_islam",
            "Bank Islam",
            "asset_bank_islam.jpeg",
            "Bank",
            2,
            IsAccessible: true,
            IsActive: true),
        new(
            "cash",
            "Cash",
            "payment_cash.png",
            "Cash",
            3,
            IsAccessible: true,
            IsActive: true),
        new(
            "cimb",
            "CIMB",
            "asset_cimb.jpeg",
            "Bank",
            4,
            IsAccessible: true,
            IsActive: true),
        new(
            "gxbank",
            "GXBank",
            "asset_gxbank.jpeg",
            "Bank",
            5,
            IsAccessible: true,
            IsActive: true),
        new(
            KwspKey,
            "KWSP",
            "asset_kwsp.jpeg",
            "Retirement",
            6,
            IsAccessible: false,
            IsActive: true),
        new(
            "luno",
            "Luno",
            "asset_luno.jpeg",
            "Cryptocurrency",
            7,
            IsAccessible: true,
            IsActive: true),
        new(
            "maybank",
            "Maybank",
            "asset_maybank.png",
            "Bank",
            8,
            IsAccessible: true,
            IsActive: true),
        new(
            "moomoo",
            "Moomoo",
            "asset_moomoo.jpeg",
            "Investment",
            9,
            IsAccessible: true,
            IsActive: true),
        new(
            "ryt_bank",
            "Ryt Bank",
            "asset_ryt_bank.jpeg",
            "Bank",
            10,
            IsAccessible: true,
            IsActive: true),
        new(
            "standard_chartered",
            "Standard Chartered",
            "asset_standard_chartered.png",
            "Bank",
            11,
            IsAccessible: true,
            IsActive: true),
        new(
            "touch_n_go_ewallet",
            "Touch N Go eWallet",
            "asset_touch_n_go_ewallet.jpeg",
            "E-wallet",
            12,
            IsAccessible: true,
            IsActive: true),
        new(
            "versa",
            "Versa",
            "asset_versa.jpeg",
            "Investment",
            13,
            IsAccessible: true,
            IsActive: true),
        new(
            "wahed",
            "Wahed",
            "asset_wahed.jpeg",
            "Investment",
            14,
            IsAccessible: true,
            IsActive: true)
    ];

    public static IReadOnlyList<AssetCatalogItem> ActiveItems { get; } = Items
        .Where(item => item.IsActive)
        .OrderBy(item => item.DisplayOrder)
        .ToList();

    public static AssetCatalogItem GetHistoricalItem(string key) =>
        Items.FirstOrDefault(item => item.Key.Equals(key, StringComparison.Ordinal))
        ?? new AssetCatalogItem(key, key, string.Empty, "Other", int.MaxValue, false, false);
}
