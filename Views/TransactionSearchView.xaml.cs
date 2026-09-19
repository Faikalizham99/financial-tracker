using System.Globalization;
using System.Text.Json;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class TransactionSearchView : ContentView
{
    private const string RecentSearchesPreferenceKey = "transaction_recent_searches";
    private const int MaximumRecentSearches = 8;
    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(120);

    private readonly List<string> recentSearches = [];
    private IReadOnlyList<SearchDocument> searchDocuments = [];
    private CancellationTokenSource? searchCancellation;
    private bool isAnimating;

    public TransactionSearchView()
    {
        InitializeComponent();
        Unloaded += OnUnloaded;
    }

    public event Action<int>? TransactionSelected;

    public void SetTransactions(IReadOnlyList<TransactionRecord> transactions)
    {
        searchDocuments = transactions
            .Select(transaction => new SearchDocument(
                transaction,
                BuildSearchText(transaction)))
            .ToList();

        if (IsVisible && !string.IsNullOrWhiteSpace(SearchEntry.Text))
        {
            QueueSearch(SearchEntry.Text, useDebounce: false);
        }
    }

    public async Task OpenAsync()
    {
        if (IsVisible || isAnimating)
        {
            return;
        }

        LoadRecentSearches();
        SearchEntry.Text = string.Empty;
        ShowEmptySearchState();
        IsVisible = true;
        Opacity = 0;
        isAnimating = true;

        try
        {
            await this.FadeToAsync(1, 180, Easing.CubicOut);
        }
        finally
        {
            isAnimating = false;
        }

        await Task.Delay(80);
        SearchEntry.Focus();
    }

    private async Task CloseAsync()
    {
        if (!IsVisible || isAnimating)
        {
            return;
        }

        CancelPendingSearch();
        isAnimating = true;
        SearchEntry.Unfocus();
        try
        {
            await this.FadeToAsync(0, 140, Easing.CubicIn);
        }
        finally
        {
            IsVisible = false;
            Opacity = 0;
            isAnimating = false;
        }
    }

    private async void OnCancelTapped(object? sender, TappedEventArgs e)
    {
        var feedback = InteractionAnimations.PulseAsync(sender);
        await CloseAsync();
        await feedback;
    }

    private void OnSearchTextChanged(object? sender, TextChangedEventArgs e) =>
        QueueSearch(e.NewTextValue, useDebounce: true);

    private void QueueSearch(string? searchText, bool useDebounce)
    {
        CancelPendingSearch();
        var query = searchText?.Trim() ?? string.Empty;
        UpdateSearchPresentation(query);

        if (query.Length == 0)
        {
            ShowEmptySearchState();
            return;
        }

        var cancellation = new CancellationTokenSource();
        searchCancellation = cancellation;
        _ = SearchAsync(query, useDebounce, cancellation);
    }

    private async Task SearchAsync(
        string query,
        bool useDebounce,
        CancellationTokenSource cancellation)
    {
        try
        {
            if (useDebounce)
            {
                await Task.Delay(SearchDebounceDelay, cancellation.Token)
                    .ConfigureAwait(false);
            }

            var documents = searchDocuments;
            var results = await Task.Run(
                    () => BuildSearchResults(documents, query, cancellation.Token),
                    cancellation.Token)
                .ConfigureAwait(false);

            cancellation.Token.ThrowIfCancellationRequested();
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!ReferenceEquals(searchCancellation, cancellation) ||
                    !IsVisible ||
                    !string.Equals(
                        SearchEntry.Text?.Trim(),
                        query,
                        StringComparison.Ordinal))
                {
                    return;
                }

                ApplySearchResults(results);
            });
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (ReferenceEquals(searchCancellation, cancellation))
            {
                searchCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private void OnSearchCompleted(object? sender, EventArgs e)
    {
        var query = SearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            AddRecentSearch(query);
        }
    }

    private void OnClearTapped(object? sender, TappedEventArgs e)
    {
        SearchEntry.Text = string.Empty;
        ShowEmptySearchState();
        SearchEntry.Focus();
    }

    private void OnRecentSearchTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not string query)
        {
            return;
        }

        SearchEntry.Text = query;
        SearchEntry.CursorPosition = query.Length;
        QueueSearch(query, useDebounce: false);
        SearchEntry.Focus();
        AddRecentSearch(query);
    }

    private void OnClearRecentSearchesTapped(object? sender, TappedEventArgs e)
    {
        recentSearches.Clear();
        SaveRecentSearches();
        UpdateRecentSearchesView();
    }

    private async void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not TransactionActivityItem transaction)
        {
            return;
        }

        var query = SearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            AddRecentSearch(query);
        }

        await CloseAsync();
        TransactionSelected?.Invoke(transaction.Id);
    }

    private void UpdateSearchPresentation(string query)
    {
        ClearButton.IsVisible = query.Length > 0;
        RecentSearchesScrollView.IsVisible = query.Length == 0;
        SearchResultsPanel.IsVisible = query.Length > 0;

        if (query.Length > 0)
        {
            SearchNoResultsLabel.IsVisible = false;
            SearchResultsCountLabel.Text = "SEARCHING...";
            SearchResultsCard.InputTransparent = true;
        }
    }

    private void ShowEmptySearchState()
    {
        CancelPendingSearch();
        ClearButton.IsVisible = false;
        RecentSearchesScrollView.IsVisible = true;
        SearchResultsPanel.IsVisible = false;
        SearchResultsCollection.ItemsSource = null;
        SearchResultsCard.IsVisible = false;
        SearchResultsCard.InputTransparent = false;
        SearchNoResultsLabel.IsVisible = false;
        UpdateRecentSearchesView();
    }

    private static IReadOnlyList<TransactionActivityItem> BuildSearchResults(
        IReadOnlyList<SearchDocument> documents,
        string query,
        CancellationToken cancellationToken)
    {
        var matchingRecords = new List<TransactionRecord>();
        foreach (var document in documents)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (document.SearchText.Contains(
                    query,
                    StringComparison.CurrentCultureIgnoreCase))
            {
                matchingRecords.Add(document.Transaction);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var results = matchingRecords
            .Select((record, index) => TransactionActivityItem.FromRecord(
                record,
                index < matchingRecords.Count - 1))
            .ToList();
        cancellationToken.ThrowIfCancellationRequested();
        return results;
    }

    private void ApplySearchResults(IReadOnlyList<TransactionActivityItem> results)
    {
        SearchResultsCollection.ItemsSource = results;
        SearchResultsCountLabel.Text =
            $"TRANSACTIONS \u00B7 {results.Count.ToString(CultureInfo.InvariantCulture)}";
        SearchResultsCard.IsVisible = results.Count > 0;
        SearchResultsCard.InputTransparent = false;
        SearchNoResultsLabel.IsVisible = results.Count == 0;
    }

    private static string BuildSearchText(TransactionRecord transaction) =>
        string.Join(
            ' ',
            transaction.Description,
            transaction.Category,
            transaction.PaymentMethod,
            transaction.Type,
            transaction.TransactionDate.ToString("dddd d MMMM yyyy", CultureInfo.CurrentCulture),
            transaction.TransactionDate.ToString("d MMM yyyy", CultureInfo.CurrentCulture),
            (transaction.AmountMinor / 100m).ToString("N2", CultureInfo.InvariantCulture));

    private void LoadRecentSearches()
    {
        recentSearches.Clear();
        try
        {
            var serializedSearches = Preferences.Default.Get(
                RecentSearchesPreferenceKey,
                string.Empty);
            var savedSearches = JsonSerializer.Deserialize<List<string>>(serializedSearches);
            if (savedSearches is not null)
            {
                recentSearches.AddRange(savedSearches
                    .Where(search => !string.IsNullOrWhiteSpace(search))
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .Take(MaximumRecentSearches));
            }
        }
        catch (JsonException)
        {
            Preferences.Default.Remove(RecentSearchesPreferenceKey);
        }

        UpdateRecentSearchesView();
    }

    private void AddRecentSearch(string query)
    {
        recentSearches.RemoveAll(search =>
            search.Equals(query, StringComparison.CurrentCultureIgnoreCase));
        recentSearches.Insert(0, query);
        if (recentSearches.Count > MaximumRecentSearches)
        {
            recentSearches.RemoveRange(
                MaximumRecentSearches,
                recentSearches.Count - MaximumRecentSearches);
        }

        SaveRecentSearches();
        UpdateRecentSearchesView();
    }

    private void SaveRecentSearches() =>
        Preferences.Default.Set(
            RecentSearchesPreferenceKey,
            JsonSerializer.Serialize(recentSearches));

    private void UpdateRecentSearchesView()
    {
        BindableLayout.SetItemsSource(RecentSearchesLayout, recentSearches.ToList());
        RecentSearchesCard.IsVisible = recentSearches.Count > 0;
        NoRecentSearchesLabel.IsVisible = recentSearches.Count == 0;
    }

    private void CancelPendingSearch()
    {
        searchCancellation?.Cancel();
        searchCancellation = null;
    }

    private void OnUnloaded(object? sender, EventArgs e) => CancelPendingSearch();

    private sealed record SearchDocument(
        TransactionRecord Transaction,
        string SearchText);
}
