namespace FinancialTracker.Views;

public partial class SettingsView
{
    public event Func<Task>? DatabaseResetRequested;

    private async void OnResetAllDataClicked(object? sender, EventArgs e)
    {
        if (isDatabaseTransferInProgress)
        {
            return;
        }

        await RunDatabaseTransferAsync(
            ResetAllDataButton,
            "Resetting…",
            DatabaseResetRequested);
    }
}
