using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class TransactionCatalog
{
    public const string ExpenseTypeKey = "Expense";
    public const string IncomeTypeKey = "Income";
    public const string InvestmentCategoryKey = "Investment";
    public const string BonusCategoryKey = "Bonus";
    public const string SalaryCategoryKey = "Salary";

    public static IReadOnlyList<TransactionOption> TransactionTypes { get; } =
    [
        new(ExpenseTypeKey, "Expenses", "category_expense_others.png"),
        new(IncomeTypeKey, "Income", "category_salary.png")
    ];

    public static IReadOnlyList<TransactionOption> ExpenseCategories { get; } =
    [
        new("Beauty", "Beauty", "category_beauty.png"),
        new("Bills & Utilities", "Bills & Utilities", "category_bills_utilities.png"),
        new("Entertainment", "Entertainment", "category_entertainment.png"),
        new("Food & Drinks", "Food & Drinks", "category_food_drinks.png"),
        new("Gift", "Gift", "category_gift.png"),
        new("Groceries", "Groceries", "category_groceries.png"),
        new("Health", "Health", "category_health.png"),
        new(InvestmentCategoryKey, "Investment", "category_investment.png"),
        new("Shopping", "Shopping", "category_shopping.png"),
        new("Subscription", "Subscription", "category_subscription.png"),
        new("Transportation", "Transportation", "category_transportation.png"),
        new("Others", "Others", "category_expense_others.png")
    ];

    public static IReadOnlyList<TransactionOption> IncomeCategories { get; } =
    [
        new(BonusCategoryKey, "Bonus", "category_bonus.png"),
        new("Cashback", "Cashback", "category_cashback.png"),
        new("Refund", "Refund", "category_refund.png"),
        new(SalaryCategoryKey, "Salary", "category_salary.png"),
        new("Others", "Others", "category_others.png")
    ];

    public static bool IsIncomeExcludedFromBudgetUsage(string categoryKey) =>
        categoryKey.Equals(BonusCategoryKey, StringComparison.OrdinalIgnoreCase) ||
        categoryKey.Equals(SalaryCategoryKey, StringComparison.OrdinalIgnoreCase);

    public static bool IsIncomeType(string typeKey) =>
        typeKey.Equals(IncomeTypeKey, StringComparison.OrdinalIgnoreCase);

    public static bool IsExpenseType(string typeKey) =>
        typeKey.Equals(ExpenseTypeKey, StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<TransactionOption> PaymentMethods { get; } =
    [
        new("AmBank", "AmBank", "payment_ambank.png"),
        new("AMEX Maybank", "AMEX Maybank", "payment_amex_maybank.png"),
        new("Bank Islam", "Bank Islam", "payment_bank_islam.png"),
        new("Cash", "Cash", "payment_cash.png"),
        new("CIMB Bank", "CIMB Bank", "payment_cimb_bank.png"),
        new("GXBank", "GXBank", "payment_gxbank.png"),
        new("Maybank", "Maybank", "payment_maybank.png"),
        new("Ryt Bank", "Ryt Bank", "payment_ryt_bank.png"),
        new("Standard Chartered", "Standard Chartered", "payment_standard_chartered.png"),
        new("Touch N Go eWallet", "Touch N Go eWallet", "payment_touch_n_go_ewallet.png"),
        new("Touch N Go NFC Card", "Touch N Go NFC Card", "payment_touch_n_go_nfc_card.png"),
        new("VISA Maybank", "VISA Maybank", "payment_visa_maybank.png"),
        new("Others", "Others", "payment_others.png")
    ];

    public static TransactionOption DefaultPaymentMethod { get; } =
        PaymentMethods.First(option => option.Key == "Cash");

    private static readonly IReadOnlyDictionary<string, TransactionOption> TransactionTypesByKey =
        TransactionTypes.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, TransactionOption> ExpenseCategoriesByKey =
        ExpenseCategories.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, TransactionOption> IncomeCategoriesByKey =
        IncomeCategories.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, TransactionOption> PaymentMethodsByKey =
        PaymentMethods.ToDictionary(option => option.Key, StringComparer.OrdinalIgnoreCase);

    public static TransactionOption GetTransactionType(string key) =>
        TransactionTypesByKey.GetValueOrDefault(key) ?? TransactionTypes[0];

    public static TransactionOption GetCategory(string key, bool isIncome)
    {
        var categories = isIncome ? IncomeCategories : ExpenseCategories;
        var categoriesByKey = isIncome ? IncomeCategoriesByKey : ExpenseCategoriesByKey;
        return categoriesByKey.GetValueOrDefault(key) ?? categories[^1];
    }

    public static TransactionOption GetPaymentMethod(string key) =>
        PaymentMethodsByKey.GetValueOrDefault(key) ?? PaymentMethods[^1];
}
