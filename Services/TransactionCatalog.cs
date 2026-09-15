using FinancialTracker.Models;

namespace FinancialTracker.Services;

public static class TransactionCatalog
{
    public static IReadOnlyList<TransactionOption> TransactionTypes { get; } =
    [
        new("Expense", "Expenses", "category_expense_others.png"),
        new("Income", "Income", "category_salary.png")
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
        new("Investment", "Investment", "category_investment.png"),
        new("Shopping", "Shopping", "category_shopping.png"),
        new("Subscription", "Subscription", "category_subscription.png"),
        new("Transportation", "Transportation", "category_transportation.png"),
        new("Others", "Others", "category_expense_others.png")
    ];

    public static IReadOnlyList<TransactionOption> IncomeCategories { get; } =
    [
        new("Bonus", "Bonus", "category_bonus.png"),
        new("Cashback", "Cashback", "category_cashback.png"),
        new("Refund", "Refund", "category_refund.png"),
        new("Salary", "Salary", "category_salary.png"),
        new("Others", "Others", "category_others.png")
    ];

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
}
