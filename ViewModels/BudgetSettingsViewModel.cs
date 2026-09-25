using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed class BudgetSettingsViewModel(
    MonthlyBudgetService monthlyBudgetService,
    SettingsViewModel settingsViewModel) : INotifyPropertyChanged
{
    public const int MinimumBudgetYear = DateRangeLimits.MinimumYear;
    public static int MaximumBudgetYear => DateRangeLimits.MaximumYear;
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private DateTime selectedMonth = StartOfMonth(DateTime.Today);
    private int historyYear = DateTime.Today.Year;
    private string includingInvestmentText = string.Empty;
    private string excludingInvestmentText = string.Empty;
    private long? savedIncludingInvestmentMinor;
    private long? savedExcludingInvestmentMinor;
    private bool isLoading;
    private bool isSaving;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? BudgetSaved;

    public ObservableCollection<BudgetHistoryMonthItem> HistoryMonths { get; } = [];

    public DateTime SelectedMonth
    {
        get => selectedMonth;
        private set
        {
            if (SetProperty(ref selectedMonth, StartOfMonth(value)))
            {
                OnPropertyChanged(nameof(SelectedMonthLabel));
            }
        }
    }

    public string SelectedMonthLabel =>
        SelectedMonth.ToString("MMMM yyyy", CultureInfo.CurrentCulture);

    public int HistoryYear
    {
        get => historyYear;
        private set
        {
            if (SetProperty(ref historyYear, value))
            {
                OnPropertyChanged(nameof(HistoryYearLabel));
            }
        }
    }

    public string HistoryYearLabel => HistoryYear.ToString(CultureInfo.InvariantCulture);
    public string CurrencySymbol => settingsViewModel.SelectedCurrency.Symbol;

    public string IncludingInvestmentText
    {
        get => includingInvestmentText;
        set
        {
            if (SetProperty(ref includingInvestmentText, value ?? string.Empty))
            {
                NotifyEditingStateChanged();
            }
        }
    }

    public string ExcludingInvestmentText
    {
        get => excludingInvestmentText;
        set
        {
            if (SetProperty(ref excludingInvestmentText, value ?? string.Empty))
            {
                NotifyEditingStateChanged();
            }
        }
    }

    public bool HasSavedBudget =>
        savedIncludingInvestmentMinor.HasValue &&
        savedExcludingInvestmentMinor.HasValue;

    public bool HasAmountValidationError =>
        (!string.IsNullOrWhiteSpace(IncludingInvestmentText) &&
         !TryParseAmount(IncludingInvestmentText, out _)) ||
        (!string.IsNullOrWhiteSpace(ExcludingInvestmentText) &&
         !TryParseAmount(ExcludingInvestmentText, out _));

    public bool HasBudgetOrderError =>
        TryParseAmount(IncludingInvestmentText, out var includingMinor) &&
        TryParseAmount(ExcludingInvestmentText, out var excludingMinor) &&
        includingMinor < excludingMinor;

    public string ValidationMessage => HasAmountValidationError
        ? "Enter valid amounts greater than zero."
        : HasBudgetOrderError
            ? "The budget including investment cannot be lower than the budget without it."
            : string.Empty;

    public bool HasValidationError =>
        HasAmountValidationError || HasBudgetOrderError;

    public bool CanSave
    {
        get
        {
            if (IsLoading || IsSaving ||
                !TryParseAmount(IncludingInvestmentText, out var includingMinor) ||
                !TryParseAmount(ExcludingInvestmentText, out var excludingMinor) ||
                includingMinor < excludingMinor)
            {
                return false;
            }

            return includingMinor != savedIncludingInvestmentMinor ||
                excludingMinor != savedExcludingInvestmentMinor;
        }
    }

    public string SaveButtonText => IsSaving
        ? "Saving…"
        : HasSavedBudget
            ? CanSave ? "Update budget" : "Saved"
            : "Save budget";

    public string SelectedMonthStatus => HasSavedBudget
        ? "Both monthly budgets are saved"
        : "No budget saved for this month";

    public bool IsLoading
    {
        get => isLoading;
        private set
        {
            if (SetProperty(ref isLoading, value))
            {
                NotifyEditingStateChanged();
            }
        }
    }

    public bool IsSaving
    {
        get => isSaving;
        private set
        {
            if (SetProperty(ref isSaving, value))
            {
                NotifyEditingStateChanged();
            }
        }
    }

    public async Task OpenAsync(DateTime month)
    {
        IsLoading = true;
        try
        {
            SelectedMonth = month;
            HistoryYear = SelectedMonth.Year;
            OnPropertyChanged(nameof(CurrencySymbol));
            await LoadSelectedMonthAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task SelectMonthAsync(DateTime month)
    {
        SelectedMonth = month;
        await LoadSelectedMonthAsync();
    }

    public Task ChangeMonthAsync(int offset) =>
        SelectMonthAsync(SelectedMonth.AddMonths(offset));

    public async Task LoadHistoryYearAsync(int year)
    {
        IsLoading = true;
        try
        {
            var normalizedYear = Math.Clamp(year, MinimumBudgetYear, MaximumBudgetYear);
            HistoryYear = normalizedYear;
            var budgets = await monthlyBudgetService.GetYearAsync(normalizedYear);
            var byMonth = budgets.ToDictionary(item => item.MonthKey);
            HistoryMonths.Clear();
            for (var monthNumber = 1; monthNumber <= 12; monthNumber++)
            {
                var month = new DateTime(normalizedYear, monthNumber, 1);
                byMonth.TryGetValue((normalizedYear * 100) + monthNumber, out var budget);
                HistoryMonths.Add(BudgetHistoryMonthItem.Create(
                    month,
                    budget,
                    CurrencySymbol));
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task ChangeHistoryYearAsync(int offset) =>
        LoadHistoryYearAsync(HistoryYear + offset);

    public async Task SaveAsync()
    {
        if (!CanSave ||
            !TryParseAmount(IncludingInvestmentText, out var includingMinor) ||
            !TryParseAmount(ExcludingInvestmentText, out var excludingMinor))
        {
            NotifyEditingStateChanged();
            return;
        }

        await operationLock.WaitAsync();
        try
        {
            IsSaving = true;
            await monthlyBudgetService.SaveAsync(
                SelectedMonth,
                includingMinor,
                excludingMinor,
                settingsViewModel.SelectedCurrency.Code);
            savedIncludingInvestmentMinor = includingMinor;
            savedExcludingInvestmentMinor = excludingMinor;
            includingInvestmentText = FormatInput(includingMinor);
            excludingInvestmentText = FormatInput(excludingMinor);
            OnPropertyChanged(nameof(IncludingInvestmentText));
            OnPropertyChanged(nameof(ExcludingInvestmentText));

            if (HistoryYear == SelectedMonth.Year && HistoryMonths.Count > 0)
            {
                await LoadHistoryYearAsync(HistoryYear);
            }

            BudgetSaved?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            IsSaving = false;
            operationLock.Release();
            NotifyEditingStateChanged();
        }
    }

    private async Task LoadSelectedMonthAsync()
    {
        var budget = await monthlyBudgetService.GetAsync(SelectedMonth);
        savedIncludingInvestmentMinor = budget?.BudgetIncludingInvestmentMinor;
        savedExcludingInvestmentMinor = budget?.BudgetExcludingInvestmentMinor;
        includingInvestmentText = budget is null
            ? string.Empty
            : FormatInput(budget.BudgetIncludingInvestmentMinor);
        excludingInvestmentText = budget is null
            ? string.Empty
            : FormatInput(budget.BudgetExcludingInvestmentMinor);
        OnPropertyChanged(nameof(IncludingInvestmentText));
        OnPropertyChanged(nameof(ExcludingInvestmentText));
        NotifyEditingStateChanged();
    }

    private void NotifyEditingStateChanged()
    {
        OnPropertyChanged(nameof(HasSavedBudget));
        OnPropertyChanged(nameof(HasAmountValidationError));
        OnPropertyChanged(nameof(HasBudgetOrderError));
        OnPropertyChanged(nameof(HasValidationError));
        OnPropertyChanged(nameof(ValidationMessage));
        OnPropertyChanged(nameof(CanSave));
        OnPropertyChanged(nameof(SaveButtonText));
        OnPropertyChanged(nameof(SelectedMonthStatus));
    }

    private static bool TryParseAmount(string value, out long amountMinor)
    {
        amountMinor = 0;
        var styles = NumberStyles.AllowDecimalPoint |
            NumberStyles.AllowThousands |
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite;
        if ((!decimal.TryParse(value, styles, CultureInfo.CurrentCulture, out var amount) &&
             !decimal.TryParse(value, styles, CultureInfo.InvariantCulture, out amount)) ||
            amount <= 0 ||
            amount > long.MaxValue / 100m)
        {
            return false;
        }

        amountMinor = decimal.ToInt64(
            decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        return amountMinor > 0;
    }

    private static string FormatInput(long amountMinor) =>
        (amountMinor / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    private static DateTime StartOfMonth(DateTime value) =>
        new(value.Year, value.Month, 1);

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
