using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using FinancialTracker.Helpers;
using FinancialTracker.Models;

namespace FinancialTracker.Views;

public partial class TransactionSearchView : ContentView
{
    private const string RecentSearchesPreferenceKey = "transaction_recent_searches";
    private const int MaximumRecentSearches = 8;
    private const int SearchPageSize = 30;
    private static readonly TimeSpan SearchDebounceDelay = TimeSpan.FromMilliseconds(120);

    private readonly List<string> recentSearches = [];
    private readonly ObservableCollection<TransactionActivityItem> searchResults = [];
    private string currencySymbol = "RM";
    private string activeQuery = string.Empty;
    private int searchOffset;
    private bool hasMoreSearchResults;
    private CancellationTokenSource? searchCancellation;
    private bool isAnimating;
    private bool isSelectingResult;
    private bool isClearingRecentSearches;
    private bool isLoadingMoreResults;

    public TransactionSearchView()
    {
        InitializeComponent();
        SearchResultsCollection.ItemsSource = searchResults;
        Unloaded += OnUnloaded;
    }

    public event Func<int, DateTime, Task>? TransactionSelected;
    public Func<string, int, int, CancellationToken, Task<TransactionSearchPage>>?
        SearchPageRequested
    { get; set; }

    public void SetCurrency(CurrencyOption selectedCurrency)
    {
        currencySymbol = selectedCurrency.Symbol;

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

    public async Task CloseAsync()
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

    public void CloseImmediately()
    {
        CancelPendingSearch();
        SearchEntry.Unfocus();
        this.CancelAnimations();
        IsVisible = false;
        Opacity = 0;
        isAnimating = false;
    }

    private async void OnCancelTapped(object? sender, TappedEventArgs e)
    {
        if (isSelectingResult)
        {
            return;
        }

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

            var provider = SearchPageRequested;
            var page = provider is null
                ? new TransactionSearchPage([], 0, HasMore: false)
                : await provider(
                        query,
                        0,
                        SearchPageSize,
                        cancellation.Token)
                    .ConfigureAwait(false);
            var results = BuildSearchResults(
                page.Records,
                currencySymbol,
                page.HasMore);

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

                ApplySearchResults(query, results, page, replaceExisting: true);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (!IsVisible ||
                    !string.Equals(
                        SearchEntry.Text?.Trim(),
                        query,
                        StringComparison.Ordinal))
                {
                    return;
                }

                searchResults.Clear();
                activeQuery = string.Empty;
                hasMoreSearchResults = false;
                SearchResultsCountLabel.Text = "SEARCH UNAVAILABLE";
                SearchResultsLoadingPanel.IsVisible = false;
                SearchResultsCard.IsVisible = false;
                SearchNoResultsLabel.Text = "Search could not be completed. Try again.";
                SearchNoResultsLabel.IsVisible = true;
            });
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

    private async void OnSearchResultsThresholdReached(object? sender, EventArgs e)
    {
        if (isLoadingMoreResults ||
            !hasMoreSearchResults ||
            string.IsNullOrWhiteSpace(activeQuery))
        {
            return;
        }

        CancelPendingSearch();
        var cancellation = new CancellationTokenSource();
        searchCancellation = cancellation;
        isLoadingMoreResults = true;
        var query = activeQuery;

        try
        {
            var provider = SearchPageRequested;
            if (provider is null)
            {
                hasMoreSearchResults = false;
                return;
            }

            var page = await provider(
                query,
                searchOffset,
                SearchPageSize,
                cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!string.Equals(
                    SearchEntry.Text?.Trim(),
                    query,
                    StringComparison.Ordinal))
            {
                return;
            }

            var results = BuildSearchResults(
                page.Records,
                currencySymbol,
                page.HasMore);
            ApplySearchResults(query, results, page, replaceExisting: false);
        }
        catch (OperationCanceledException)
        {
        }
        catch
        {
            hasMoreSearchResults = false;
            SearchResultsCountLabel.Text =
                $"TRANSACTIONS · {searchResults.Count.ToString(CultureInfo.InvariantCulture)}";
        }
        finally
        {
            if (ReferenceEquals(searchCancellation, cancellation))
            {
                searchCancellation = null;
            }

            cancellation.Dispose();
            isLoadingMoreResults = false;
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

        SearchEntry.Unfocus();
        SearchEntry.Text = query;
        SearchEntry.CursorPosition = query.Length;
        QueueSearch(query, useDebounce: false);
        AddRecentSearch(query);
    }

    private async void OnClearRecentSearchesTapped(object? sender, TappedEventArgs e)
    {
        if (isClearingRecentSearches || recentSearches.Count == 0)
        {
            return;
        }

        isClearingRecentSearches = true;
        SearchEntry.Unfocus();
        ClearRecentSearchesButton.InputTransparent = true;
        RecentSearchesScrollView.IsVisible = false;
        RecentSearchesLoadingPanel.IsVisible = true;

        try
        {
            // Yield a frame so the local skeleton is visible before the
            // preference and bound-list updates are applied.
            await Task.Yield();
            recentSearches.Clear();
            SaveRecentSearches();
            UpdateRecentSearchesView();
            await Task.Yield();
        }
        finally
        {
            RecentSearchesLoadingPanel.IsVisible = false;
            RecentSearchesScrollView.IsVisible = true;
            ClearRecentSearchesButton.InputTransparent = false;
            isClearingRecentSearches = false;
        }
    }

    private async void OnResultTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not TransactionActivityItem transaction ||
            isSelectingResult)
        {
            return;
        }

        var query = SearchEntry.Text?.Trim();
        if (!string.IsNullOrWhiteSpace(query))
        {
            AddRecentSearch(query);
        }

        isSelectingResult = true;
        CancelPendingSearch();
        SearchEntry.Unfocus();
        InputTransparent = true;
        SearchResultsCard.InputTransparent = true;

        try
        {
            var selectionHandler = TransactionSelected;
            if (selectionHandler is null)
            {
                await CloseAsync();
                return;
            }

            await selectionHandler(transaction.Id, transaction.TransactionDate);
        }
        finally
        {
            isSelectingResult = false;
            InputTransparent = false;
            SearchResultsCard.InputTransparent = false;
        }
    }

    private void UpdateSearchPresentation(string query)
    {
        ClearButton.IsVisible = query.Length > 0;
        RecentSearchesScrollView.IsVisible = query.Length == 0;
        SearchResultsPanel.IsVisible = query.Length > 0;

        if (query.Length > 0)
        {
            SearchNoResultsLabel.Text = "No matching transactions.";
            SearchNoResultsLabel.IsVisible = false;
            SearchResultsCountLabel.Text = "SEARCHING...";
            SearchResultsLoadingPanel.IsVisible = true;
            SearchResultsCard.IsVisible = false;
            SearchResultsCard.InputTransparent = true;
        }
    }

    private void ShowEmptySearchState()
    {
        CancelPendingSearch();
        ClearButton.IsVisible = false;
        RecentSearchesScrollView.IsVisible = true;
        SearchResultsPanel.IsVisible = false;
        searchResults.Clear();
        activeQuery = string.Empty;
        searchOffset = 0;
        hasMoreSearchResults = false;
        SearchResultsLoadingPanel.IsVisible = false;
        SearchResultsCard.IsVisible = false;
        SearchResultsCard.InputTransparent = false;
        SearchNoResultsLabel.IsVisible = false;
        UpdateRecentSearchesView();
    }

    private static IReadOnlyList<TransactionActivityItem> BuildSearchResults(
        IReadOnlyList<TransactionRecord> records,
        string currencySymbol,
        bool hasMore)
    {
        return records
            .Select((record, index) => TransactionActivityItem.FromRecord(
                record,
                currencySymbol,
                index < records.Count - 1 || hasMore))
            .ToList();
    }

    private void ApplySearchResults(
        string query,
        IReadOnlyList<TransactionActivityItem> results,
        TransactionSearchPage page,
        bool replaceExisting)
    {
        if (replaceExisting)
        {
            searchResults.Clear();
        }

        foreach (var result in results)
        {
            searchResults.Add(result);
        }

        activeQuery = query;
        searchOffset = page.NextOffset;
        hasMoreSearchResults = page.HasMore;
        SearchResultsCountLabel.Text =
            $"TRANSACTIONS \u00B7 {searchResults.Count.ToString(CultureInfo.InvariantCulture)}" +
            (hasMoreSearchResults ? "+" : string.Empty);
        SearchResultsLoadingPanel.IsVisible = false;
        SearchResultsCard.IsVisible = searchResults.Count > 0;
        SearchResultsCard.InputTransparent = false;
        SearchNoResultsLabel.IsVisible = searchResults.Count == 0;
    }

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

}
