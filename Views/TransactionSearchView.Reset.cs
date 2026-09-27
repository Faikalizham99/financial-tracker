namespace FinancialTracker.Views;

public partial class TransactionSearchView
{
    public void ResetState()
    {
        CancelPendingSearch();
        recentSearches.Clear();
        searchResults.Clear();
        SearchEntry.Text = string.Empty;
        ShowEmptySearchState();
    }
}
