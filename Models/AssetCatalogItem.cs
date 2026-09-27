namespace FinancialTracker.Models;

public sealed record AssetCatalogItem(
    string Key,
    string DisplayName,
    string IconAsset,
    int DisplayOrder,
    bool IsAccessible,
    bool IsActive);
