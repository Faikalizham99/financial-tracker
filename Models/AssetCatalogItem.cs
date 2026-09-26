namespace FinancialTracker.Models;

public sealed record AssetCatalogItem(
    string Key,
    string DisplayName,
    string IconAsset,
    string AssetType,
    int DisplayOrder,
    bool IsAccessible,
    bool IsActive);
