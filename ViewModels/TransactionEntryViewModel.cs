using System.Globalization;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed class TransactionEntryViewModel
{
    private string currentInput = "0";
    private decimal accumulator;
    private string? pendingOperator;
    private bool startNewInput = true;
    private IReadOnlyList<TransactionHistorySuggestion> descriptionHistory = [];

    public CurrencyOption Currency { get; private set; } =
        SettingsViewModel.SupportedCurrencies[0];
    public TransactionOption SelectedType { get; private set; } =
        TransactionCatalog.TransactionTypes[0];
    public TransactionOption SelectedPayment { get; private set; } =
        TransactionCatalog.DefaultPaymentMethod;
    public TransactionOption SelectedCategory { get; private set; } =
        TransactionCatalog.ExpenseCategories[^1];
    public DateTime TransactionDate { get; private set; } = DateTime.Today;
    public TransactionRecord? EditingTransaction { get; private set; }
    public bool IsEditing => EditingTransaction is not null;
    public bool HasPendingCalculation => pendingOperator is not null;
    public bool CanCompleteCalculation => pendingOperator is not null && !startNewInput;
    public decimal EffectiveAmount => pendingOperator is null || startNewInput
        ? ParseCurrentInput()
        : Calculate(accumulator, ParseCurrentInput(), pendingOperator);

    public string AmountDisplayText
    {
        get
        {
            if (pendingOperator is null)
            {
                return currentInput;
            }

            var operation = pendingOperator switch
            {
                "*" => "×",
                "/" => "÷",
                "-" => "−",
                _ => "+"
            };
            var left = FormatAmount(accumulator);
            return startNewInput
                ? $"{left} {operation}"
                : $"{left} {operation} {currentInput}";
        }
    }

    public void Initialize(
        CurrencyOption currency,
        TransactionRecord? transaction)
    {
        Currency = currency;
        EditingTransaction = transaction;
        SelectedType = transaction is null
            ? TransactionCatalog.TransactionTypes[0]
            : TransactionCatalog.GetTransactionType(transaction.Type);
        SelectedPayment = transaction is null
            ? TransactionCatalog.DefaultPaymentMethod
            : TransactionCatalog.PaymentMethods.FirstOrDefault(option =>
                option.Key.Equals(
                    transaction.PaymentMethod,
                    StringComparison.OrdinalIgnoreCase))
              ?? TransactionCatalog.DefaultPaymentMethod;
        var categories = GetCategoriesForSelectedType();
        SelectedCategory = transaction is null
            ? categories[^1]
            : TransactionCatalog.GetCategory(
                transaction.Category,
                TransactionCatalog.IsIncomeType(SelectedType.Key));
        currentInput = transaction is null
            ? "0"
            : FormatAmount(transaction.AmountMinor / 100m);
        accumulator = 0;
        pendingOperator = null;
        startNewInput = true;
        TransactionDate = transaction?.TransactionDate.Date ?? DateTime.Today;
    }

    public void ToggleTransactionType()
    {
        SelectedType = TransactionCatalog.IsExpenseType(SelectedType.Key)
            ? TransactionCatalog.TransactionTypes[1]
            : TransactionCatalog.TransactionTypes[0];
        SelectedCategory = GetCategoriesForSelectedType()[^1];
    }

    public IReadOnlyList<TransactionOption> GetCategoriesForSelectedType() =>
        TransactionCatalog.IsIncomeType(SelectedType.Key)
            ? TransactionCatalog.IncomeCategories
            : TransactionCatalog.ExpenseCategories;

    public void SelectPayment(TransactionOption payment) =>
        SelectedPayment = payment;

    public void SelectCategory(TransactionOption category) =>
        SelectedCategory = category;

    public void SetTransactionDate(DateTime date) =>
        TransactionDate = date.Date;

    public void ApplySuggestion(TransactionHistorySuggestion suggestion)
    {
        SelectedType = suggestion.TransactionType;
        SelectedCategory = suggestion.Category;
        SelectedPayment = suggestion.PaymentMethod;
    }

    public void ApplyKey(string key)
    {
        if (key == "back")
        {
            RemoveLastCharacter();
        }
        else if (key is "+" or "-" or "*" or "/")
        {
            SelectOperator(key);
        }
        else
        {
            AppendInput(key);
        }
    }

    public void CompleteCalculation()
    {
        if (!CanCompleteCalculation)
        {
            return;
        }

        currentInput = FormatAmount(
            Calculate(accumulator, ParseCurrentInput(), pendingOperator!));
        accumulator = 0;
        pendingOperator = null;
        startNewInput = false;
    }

    public async Task LoadDescriptionHistoryAsync(
        IReadOnlyList<TransactionRecord> records)
    {
        try
        {
            descriptionHistory = await Task.Run(() => records
                    .Where(record => !string.IsNullOrWhiteSpace(record.Description))
                    .GroupBy(
                        record => record.Description.Trim(),
                        StringComparer.OrdinalIgnoreCase)
                    .Select(group => CreateHistorySuggestion(group.First()))
                    .ToList())
                .ConfigureAwait(false);
        }
        catch
        {
            descriptionHistory = [];
        }
    }

    public IReadOnlyList<TransactionHistorySuggestion> FindDescriptionSuggestions(
        string? input,
        int maximumCount = 4)
    {
        var query = input?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        return descriptionHistory
            .Where(suggestion => suggestion.Description.Contains(
                query,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(suggestion => suggestion.Description.StartsWith(
                query,
                StringComparison.OrdinalIgnoreCase))
            .Take(maximumCount)
            .ToList();
    }

    public TransactionRecord CreateTransaction(string description) => new()
    {
        Id = EditingTransaction?.Id ?? 0,
        Type = SelectedType.Key,
        Category = SelectedCategory.Key,
        PaymentMethod = SelectedPayment.Key,
        Description = description.Trim(),
        AmountMinor = decimal.ToInt64(decimal.Round(
            EffectiveAmount * 100,
            0,
            MidpointRounding.AwayFromZero)),
        CurrencyCode = EditingTransaction?.CurrencyCode ?? Currency.Code,
        TransactionDate = TransactionDate,
        CreatedAtUtc = EditingTransaction?.CreatedAtUtc ?? DateTime.UtcNow
    };

    private void AppendInput(string key)
    {
        if (startNewInput)
        {
            currentInput = key == "." ? "0." : key;
            startNewInput = false;
            return;
        }

        if (key == ".")
        {
            if (!currentInput.Contains('.'))
            {
                currentInput += ".";
            }

            return;
        }

        var decimalIndex = currentInput.IndexOf('.');
        if (decimalIndex >= 0 && currentInput.Length - decimalIndex > 2)
        {
            return;
        }

        if (currentInput.Replace(".", string.Empty, StringComparison.Ordinal).Length >= 10)
        {
            return;
        }

        currentInput = currentInput == "0" ? key : currentInput + key;
    }

    private void RemoveLastCharacter()
    {
        if (pendingOperator is not null && startNewInput)
        {
            currentInput = FormatAmount(accumulator);
            accumulator = 0;
            pendingOperator = null;
            startNewInput = false;
            return;
        }

        if (startNewInput)
        {
            currentInput = "0";
            startNewInput = false;
            return;
        }

        currentInput = currentInput.Length <= 1
            ? "0"
            : currentInput[..^1];
    }

    private void SelectOperator(string operation)
    {
        var currentValue = ParseCurrentInput();
        if (pendingOperator is not null && !startNewInput)
        {
            accumulator = Calculate(accumulator, currentValue, pendingOperator);
            currentInput = FormatAmount(accumulator);
        }
        else
        {
            accumulator = currentValue;
        }

        pendingOperator = operation;
        startNewInput = true;
    }

    private decimal ParseCurrentInput() =>
        decimal.TryParse(
            currentInput.TrimEnd('.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out var amount)
            ? amount
            : 0;

    private static decimal Calculate(decimal left, decimal right, string operation) =>
        operation switch
        {
            "+" => left + right,
            "-" => left - right,
            "*" => left * right,
            "/" when right != 0 => left / right,
            _ => left
        };

    private static string FormatAmount(decimal amount) =>
        amount.ToString("0.##", CultureInfo.InvariantCulture);

    private static TransactionHistorySuggestion CreateHistorySuggestion(
        TransactionRecord record)
    {
        var transactionType = TransactionCatalog.GetTransactionType(record.Type);
        var isIncome = TransactionCatalog.IsIncomeType(transactionType.Key);
        return new TransactionHistorySuggestion(
            record.Description.Trim(),
            transactionType,
            TransactionCatalog.GetCategory(record.Category, isIncome),
            TransactionCatalog.GetPaymentMethod(record.PaymentMethod));
    }
}
