using FinancialTracker.Helpers;

namespace FinancialTracker.Views;

public partial class BudgetSettingsView
{
    private async void OnClearMonthTapped(object? sender, TappedEventArgs e)
    {
        if (ViewModel is null || !ViewModel.CanClearMonth || isOperationRunning)
        {
            return;
        }

        var hostPage = FindHostPage();
        if (hostPage is null)
        {
            return;
        }

        var monthLabel = ViewModel.SelectedMonthLabel;
        bool shouldClear;
        try
        {
            shouldClear = await hostPage.DisplayAlertAsync(
                $"Clear {monthLabel}?",
                "This removes both saved budgets for this month. Other months will not be changed.",
                "Clear month",
                "Cancel");
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Budget clear confirmation failed: {exception}");
            await ShowClearErrorAsync(hostPage);
            return;
        }

        if (!shouldClear)
        {
            return;
        }

        var feedback = InteractionAnimations.PulseAsync(sender);
        IncludingInvestmentEntry.Unfocus();
        ExcludingInvestmentEntry.Unfocus();

        try
        {
            await RunOperationAsync(ViewModel.ClearSelectedMonthAsync);
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Budget clear failed: {exception}");
            await ShowClearErrorAsync(hostPage);
        }

        await feedback;
    }

    private static Task ShowClearErrorAsync(Page hostPage) =>
        hostPage.DisplayAlertAsync(
            "Budget not cleared",
            "The saved budget could not be cleared. Please try again.",
            "OK");

    private Page? FindHostPage()
    {
        Element? current = this;
        while (current is not null)
        {
            if (current is Page page)
            {
                return page;
            }

            current = current.Parent;
        }

        return null;
    }
}
