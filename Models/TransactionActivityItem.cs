using System.ComponentModel;
using System.Globalization;
using FinancialTracker.Helpers;
using FinancialTracker.Services;

namespace FinancialTracker.Models;

public sealed class TransactionActivityItem : INotifyPropertyChanged
{
    private bool canExpandDescription;
    private bool isDescriptionExpanded;

    private TransactionActivityItem()
    {
    }

    public int Id { get; private init; }
    public string Description { get; private init; } = string.Empty;
    public string DetailText { get; private init; } = string.Empty;
    public string DashboardDetailText { get; private init; } = string.Empty;
    public string DashboardDateText { get; private init; } = string.Empty;
    public string AmountText { get; private init; } = string.Empty;
    public string IconAsset { get; private init; } = string.Empty;
    public string PaymentMethodIconAsset { get; private init; } = string.Empty;
    public bool IsIncome { get; private init; }
    public bool ShowDivider { get; private init; }
    public DateTime TransactionDate { get; private init; }
    public bool CanExpandDescription => canExpandDescription;
    public bool IsDescriptionExpanded => isDescriptionExpanded;
    public int DescriptionMaxLines => IsDescriptionExpanded ? -1 : 1;
    public LineBreakMode DescriptionLineBreakMode =>
        IsDescriptionExpanded ? LineBreakMode.WordWrap : LineBreakMode.TailTruncation;
    public double DescriptionHeaderHeightRequest => IsDescriptionExpanded ? -1 : 20;
    public double DescriptionRowHeightRequest => IsDescriptionExpanded ? -1 : 63;
    public double DescriptionIndicatorRotation => IsDescriptionExpanded ? 180 : 0;

    public event PropertyChangedEventHandler? PropertyChanged;

    public static TransactionActivityItem FromRecord(
        TransactionRecord record,
        bool showDivider = false,
        bool isDescriptionExpanded = false)
    {
        var isIncome = record.Type.Equals("Income", StringComparison.OrdinalIgnoreCase);
        var category = TransactionCatalog.GetCategory(record.Category, isIncome);
        var paymentMethod = TransactionCatalog.GetPaymentMethod(record.PaymentMethod);
        var symbol = MoneyFormatter.GetCurrencySymbol(record.CurrencyCode);
        var dateText = record.TransactionDate.Date switch
        {
            var date when date == DateTime.Today => "Today",
            var date when date == DateTime.Today.AddDays(-1) => "Yesterday",
            var date => date.ToString("d MMM yyyy", CultureInfo.CurrentCulture)
        };

        return new TransactionActivityItem
        {
            Id = record.Id,
            Description = record.Description,
            DetailText = $"{record.Category} · {record.PaymentMethod}",
            DashboardDetailText = $"{record.Category} · {record.PaymentMethod}",
            DashboardDateText = dateText,
            AmountText = MoneyFormatter.FormatMinor(
                isIncome ? record.AmountMinor : -record.AmountMinor,
                symbol,
                showPositiveSign: true,
                separateSign: true),
            IconAsset = category.IconAsset,
            PaymentMethodIconAsset = paymentMethod.IconAsset,
            IsIncome = isIncome,
            ShowDivider = showDivider,
            TransactionDate = record.TransactionDate.Date,
            isDescriptionExpanded = isDescriptionExpanded
        };
    }

    public void SetDescriptionExpandable(bool canExpand)
    {
        if (canExpandDescription == canExpand)
        {
            return;
        }

        canExpandDescription = canExpand;
        OnPropertyChanged(nameof(CanExpandDescription));
        if (!canExpand && isDescriptionExpanded)
        {
            SetDescriptionExpanded(false);
        }
    }

    public void SetDescriptionExpanded(bool isExpanded)
    {
        if (isDescriptionExpanded == isExpanded)
        {
            return;
        }

        isDescriptionExpanded = isExpanded;
        OnPropertyChanged(nameof(IsDescriptionExpanded));
        OnPropertyChanged(nameof(DescriptionMaxLines));
        OnPropertyChanged(nameof(DescriptionLineBreakMode));
        OnPropertyChanged(nameof(DescriptionHeaderHeightRequest));
        OnPropertyChanged(nameof(DescriptionRowHeightRequest));
        OnPropertyChanged(nameof(DescriptionIndicatorRotation));
    }

    private void OnPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

public sealed class TransactionActivityGroup : INotifyPropertyChanged
{
    private bool isExpanded;

    public TransactionActivityGroup(
        DateTime date,
        string title,
        IReadOnlyList<TransactionActivityItem> items,
        long netAmountMinor,
        string currencySymbol,
        bool isExpanded = true)
    {
        Date = date.Date;
        Title = title;
        Items = items;
        this.isExpanded = isExpanded;
        DailyTotalText = MoneyFormatter.FormatMinor(
            netAmountMinor,
            currencySymbol,
            showPositiveSign: true,
            separateSign: true);
        IsNetIncome = netAmountMinor > 0;
        IsNetExpense = netAmountMinor < 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public DateTime Date { get; }
    public string Title { get; }
    public IReadOnlyList<TransactionActivityItem> Items { get; }
    public string DailyTotalText { get; }
    public bool IsNetIncome { get; }
    public bool IsNetExpense { get; }

    public bool IsExpanded
    {
        get => isExpanded;
        set
        {
            if (isExpanded == value)
            {
                return;
            }

            isExpanded = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(IsExpanded)));
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(nameof(ChevronRotation)));
        }
    }

    public double ChevronRotation => IsExpanded ? 90 : 0;
}
