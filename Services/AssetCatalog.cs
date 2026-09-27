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
            0,
            IsAccessible: true,
            IsActive: true),
        new(
            "asb",
            "ASB",
            "asset_asb.jpeg",
            1,
            IsAccessible: true,
            IsActive: true),
        new(
            "bank_islam",
            "Bank Islam",
            "asset_bank_islam.jpeg",
            2,
            IsAccessible: true,
            IsActive: true),
        new(
            "cash",
            "Cash",
            "payment_cash.png",
            3,
            IsAccessible: true,
            IsActive: true),
        new(
            "cimb",
            "CIMB",
            "asset_cimb.jpeg",
            4,
            IsAccessible: true,
            IsActive: true),
        new(
            "gxbank",
            "GXBank",
            "asset_gxbank.jpeg",
            5,
            IsAccessible: true,
            IsActive: true),
        new(
            KwspKey,
            "KWSP",
            "asset_kwsp.jpeg",
            6,
            IsAccessible: false,
            IsActive: true),
        new(
            "luno",
            "Luno",
            "asset_luno.jpeg",
            7,
            IsAccessible: true,
            IsActive: true),
        new(
            "maybank",
            "Maybank",
            "asset_maybank.png",
            8,
            IsAccessible: true,
            IsActive: true),
        new(
            "moomoo",
            "Moomoo",
            "asset_moomoo.jpeg",
            9,
            IsAccessible: true,
            IsActive: true),
        new(
            "ryt_bank",
            "Ryt Bank",
            "asset_ryt_bank.jpeg",
            10,
            IsAccessible: true,
            IsActive: true),
        new(
            "standard_chartered",
            "Standard Chartered",
            "asset_standard_chartered.png",
            11,
            IsAccessible: true,
            IsActive: true),
        new(
            "touch_n_go_ewallet",
            "Touch N Go eWallet",
            "asset_touch_n_go_ewallet.jpeg",
            12,
            IsAccessible: true,
            IsActive: true),
        new(
            "versa",
            "Versa",
            "asset_versa.jpeg",
            13,
            IsAccessible: true,
            IsActive: true),
        new(
            "wahed",
            "Wahed",
            "asset_wahed.jpeg",
            14,
            IsAccessible: true,
            IsActive: true)
    ];

    public static IReadOnlyList<AssetCatalogItem> ActiveItems { get; } = Items
        .Where(item => item.IsActive)
        .OrderBy(item => item.DisplayOrder)
        .ToList();

    private static IReadOnlyDictionary<string, AssetCatalogItem> ItemsByKey { get; } =
        Items.ToDictionary(item => item.Key, StringComparer.Ordinal);

    private static IReadOnlySet<string> ActiveKeys { get; } = ActiveItems
        .Select(item => item.Key)
        .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlySet<string> AccessibleKeys { get; } = Items
        .Where(item => item.IsAccessible)
        .Select(item => item.Key)
        .ToHashSet(StringComparer.Ordinal);

    public static AssetCatalogItem GetHistoricalItem(string key) =>
        ItemsByKey.GetValueOrDefault(key)
        ?? new AssetCatalogItem(key, key, string.Empty, int.MaxValue, false, false);

    public static bool IsActive(string key) => ActiveKeys.Contains(key);

    public static bool IsAccessible(string key) => AccessibleKeys.Contains(key);
}
