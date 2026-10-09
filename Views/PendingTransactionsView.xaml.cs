using FinancialTracker.Data;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.Views;

public partial class PendingTransactionsView : ContentView
{
    private PendingTransactionInboxService? inbox;
    private LocalDatabase? database;
    private Func<CurrencyOption>? currencyProvider;
    private readonly HashSet<long> activeRecords = [];
    private bool isAnimating;

    public event Action<bool>? VisibilityChanged;
    public Func<PendingTransactionRecord, Task>? EditRequested { get; set; }
    public Func<Task>? TransactionsChanged { get; set; }
    public Func<int, Task>? CountChanged { get; set; }

    public bool IsOpen => IsVisible;

    public PendingTransactionsView()
    {
        InitializeComponent();
    }

    public void Configure(
        PendingTransactionInboxService pendingInbox,
        LocalDatabase localDatabase,
        Func<CurrencyOption> selectedCurrencyProvider)
    {
        inbox = pendingInbox;
        database = localDatabase;
        currencyProvider = selectedCurrencyProvider;
    }

    public async Task OpenAsync()
    {
        if (IsVisible || isAnimating)
        {
            return;
        }

        isAnimating = true;
        IsVisible = true;
        Root.Opacity = 0;
        VisibilityChanged?.Invoke(true);
        try
        {
            await RefreshAsync();
            await Root.FadeToAsync(1, 170, Easing.CubicOut);
        }
        finally
        {
            Root.Opacity = 1;
            isAnimating = false;
        }
    }

    public async Task CloseAsync()
    {
        if (!IsVisible || isAnimating)
        {
            return;
        }

        isAnimating = true;
        try
        {
            await Root.FadeToAsync(0, 135, Easing.CubicIn);
        }
        finally
        {
            IsVisible = false;
            Root.Opacity = 0;
            isAnimating = false;
            VisibilityChanged?.Invoke(false);
        }
    }

    public Task HandleBackAsync() => CloseAsync();

    public async Task RefreshAsync()
    {
        if (inbox is null)
        {
            return;
        }

        LoadingOverlay.IsVisible = true;
        ErrorCard.IsVisible = false;
        try
        {
            var records = await inbox.GetPendingAsync();
            BindingContext = records;
            EmptyState.IsVisible = records.Count == 0;
            PendingList.IsVisible = records.Count > 0;
            InboxCountLabel.Text = records.Count.ToString();
            InboxSummaryLabel.Text = records.Count switch
            {
                0 => "Nothing waiting",
                1 => "1 transaction needs review",
                _ => $"{records.Count} transactions need review"
            };
            if (CountChanged is not null)
            {
                await CountChanged(records.Count);
            }
        }
        catch (Exception exception)
        {
            ShowError(
                "The pending inbox could not be opened. Check the App Group " +
                $"signing and try again. ({exception.Message})");
        }
        finally
        {
            LoadingOverlay.IsVisible = false;
        }
    }

    private async void OnApproveClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: PendingTransactionRecord record } ||
            !activeRecords.Add(record.Id))
        {
            return;
        }

        try
        {
            if (!record.HasDetectedAmount)
            {
                await EditRecordAsync(record);
                return;
            }

            if (database is null || inbox is null || currencyProvider is null)
            {
                return;
            }

            var existing = await database.GetTransactionByImportKeyAsync(
                record.CaptureKey);
            if (existing is null)
            {
                await database.SaveTransactionAsync(record.CreateTransaction(
                    currencyProvider().Code));
            }

            await inbox.DeleteAsync(record.Id);
            if (TransactionsChanged is not null)
            {
                await TransactionsChanged();
            }

            await RefreshAsync();
        }
        catch (Exception exception)
        {
            ShowError($"This transaction could not be approved. {exception.Message}");
        }
        finally
        {
            activeRecords.Remove(record.Id);
        }
    }

    private async void OnEditClicked(object? sender, EventArgs e)
    {
        if (sender is Button { CommandParameter: PendingTransactionRecord record })
        {
            await EditRecordAsync(record);
        }
    }

    private async Task EditRecordAsync(PendingTransactionRecord record)
    {
        var editRequested = EditRequested;
        if (editRequested is null)
        {
            return;
        }

        await CloseAsync();
        await editRequested(record);
    }

    private async void OnRejectClicked(object? sender, EventArgs e)
    {
        if (sender is not Button { CommandParameter: PendingTransactionRecord record } ||
            !activeRecords.Add(record.Id))
        {
            return;
        }

        try
        {
            var page = Window?.Page;
            var confirmed = page is not null && await page.DisplayAlertAsync(
                "Reject captured transaction?",
                "This removes the notification capture without changing your financial records.",
                "Reject",
                "Keep");
            if (!confirmed || inbox is null)
            {
                return;
            }

            await inbox.DeleteAsync(record.Id);
            await RefreshAsync();
        }
        catch (Exception exception)
        {
            ShowError($"This capture could not be rejected. {exception.Message}");
        }
        finally
        {
            activeRecords.Remove(record.Id);
        }
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorCard.IsVisible = true;
    }

    private async void OnCloseTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseAsync();
        await feedback;
    }
}
