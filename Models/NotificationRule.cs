using System.Text.Json.Serialization;
using FinancialTracker.Services;

namespace FinancialTracker.Models;

public sealed class NotificationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string MatchText { get; set; } = string.Empty;
    public string SourceApp { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public string TransactionType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string DescriptionOverride { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    [JsonIgnore]
    public string NameDisplay => string.IsNullOrWhiteSpace(Name)
        ? MatchTextDisplay
        : Name.Trim();

    [JsonIgnore]
    public string MatchTextDisplay => string.IsNullOrWhiteSpace(MatchText)
        ? "Match text needed"
        : $"Contains “{MatchText.Trim()}”";

    [JsonIgnore]
    public string SourceDisplay => string.IsNullOrWhiteSpace(SourceApp)
        ? "Any notification source"
        : $"Source: {SourceApp.Trim()}";

    [JsonIgnore]
    public string ActionDisplay
    {
        get
        {
            var actions = new List<string>();
            if (!string.IsNullOrWhiteSpace(TransactionType))
            {
                actions.Add($"Type → {(TransactionCatalog.IsIncomeType(TransactionType) ? "Income" : "Expense")}");
            }
            if (!string.IsNullOrWhiteSpace(Category))
            {
                actions.Add($"Category → {TransactionCatalog.GetCategory(Category, TransactionCatalog.IsIncomeType(TransactionType)).Title}");
            }
            if (!string.IsNullOrWhiteSpace(PaymentMethod))
            {
                actions.Add($"Payment → {TransactionCatalog.GetPaymentMethod(PaymentMethod).Title}");
            }
            if (!string.IsNullOrWhiteSpace(DescriptionOverride))
            {
                actions.Add($"Description → {DescriptionOverride.Trim()}");
            }
            return actions.Count == 0
                ? "No overrides configured"
                : string.Join(" · ", actions);
        }
    }

    [JsonIgnore]
    public bool HasOverrides =>
        !string.IsNullOrWhiteSpace(TransactionType) ||
        !string.IsNullOrWhiteSpace(Category) ||
        !string.IsNullOrWhiteSpace(PaymentMethod) ||
        !string.IsNullOrWhiteSpace(DescriptionOverride);

    [JsonIgnore]
    public string StatusText => IsEnabled ? "Enabled" : "Paused";

    [JsonIgnore]
    public string ToggleText => IsEnabled ? "Pause" : "Enable";

    [JsonIgnore]
    public bool CanMoveUp { get; set; }

    [JsonIgnore]
    public bool CanMoveDown { get; set; }

    public NotificationRule Clone() => new()
    {
        Id = Id,
        Name = Name,
        MatchText = MatchText,
        SourceApp = SourceApp,
        IsEnabled = IsEnabled,
        TransactionType = TransactionType,
        Category = Category,
        PaymentMethod = PaymentMethod,
        DescriptionOverride = DescriptionOverride,
        SortOrder = SortOrder
    };
}

public sealed class NotificationRulesDocument
{
    public int Version { get; set; } = 2;
    public List<NotificationRule> Rules { get; set; } = [];
}
