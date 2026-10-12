using System.Text.Json;
using FinancialTracker.Models;

namespace FinancialTracker.Services;

public sealed class NotificationRuleStore
{
    private const string FileName = "notification-rules.json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<IReadOnlyList<NotificationRule>> GetRulesAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var path = ResolvePath();
            if (!File.Exists(path))
            {
                return [];
            }

            await using var stream = File.OpenRead(path);
            var document = await JsonSerializer.DeserializeAsync<NotificationRulesDocument>(
                    stream,
                    JsonOptions)
                .ConfigureAwait(false);
            return Normalize(document?.Rules ?? []);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task SaveRulesAsync(IEnumerable<NotificationRule> rules)
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            var normalized = Normalize(rules).ToList();
            var document = new NotificationRulesDocument { Rules = normalized };
            var path = ResolvePath();
            var temporaryPath = path + ".tmp";
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, document, JsonOptions)
                    .ConfigureAwait(false);
                await stream.FlushAsync().ConfigureAwait(false);
            }

            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    private static string ResolvePath() => Path.Combine(
        PendingTransactionInboxService.ResolveSharedDirectory(),
        FileName);

    private static IReadOnlyList<NotificationRule> Normalize(
        IEnumerable<NotificationRule> rules)
    {
        var normalized = rules
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Id))
            .OrderBy(rule => rule.SortOrder)
            .ThenBy(rule => rule.Name, StringComparer.OrdinalIgnoreCase)
            .Select(rule => rule.Clone())
            .ToList();

        for (var index = 0; index < normalized.Count; index++)
        {
            var rule = normalized[index];
            rule.SortOrder = index;
            rule.Name = rule.Name.Trim();
            rule.MatchText = rule.MatchText.Trim();
            rule.SourceApp = rule.SourceApp.Trim();
            rule.DescriptionOverride = rule.DescriptionOverride.Trim();
            var hasExplicitType =
                TransactionCatalog.IsIncomeType(rule.TransactionType) ||
                TransactionCatalog.IsExpenseType(rule.TransactionType);
            var isIncome = hasExplicitType &&
                TransactionCatalog.IsIncomeType(rule.TransactionType);
            rule.TransactionType = !hasExplicitType
                ? string.Empty
                : isIncome
                    ? TransactionCatalog.IncomeTypeKey
                    : TransactionCatalog.ExpenseTypeKey;
            rule.Category = hasExplicitType && !string.IsNullOrWhiteSpace(rule.Category)
                ? TransactionCatalog.GetCategory(rule.Category, isIncome).Key
                : string.Empty;
            rule.PaymentMethod = string.IsNullOrWhiteSpace(rule.PaymentMethod)
                ? string.Empty
                : TransactionCatalog.GetPaymentMethod(rule.PaymentMethod).Key;
            rule.CanMoveUp = index > 0;
            rule.CanMoveDown = index < normalized.Count - 1;
        }

        return normalized;
    }
}
