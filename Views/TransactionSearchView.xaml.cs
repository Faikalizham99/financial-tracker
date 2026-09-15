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
            UpdateResults();
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
        UpdateResults();
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

    private async void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
    {
        CancelPendingSearch();
        var cancellation = new CancellationTokenSource();
        searchCancellation = cancellation;

        try
        {
            await Task.Delay(SearchDebounceDelay, cancellation.Token);
            UpdateResults();
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
        UpdateResults();
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
        UpdateResults();
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

    private void UpdateResults()
    {
        var query = SearchEntry.Text?.Trim() ?? string.Empty;
        ClearButton.IsVisible = query.Length > 0;
        RecentSearchesPanel.IsVisible = query.Length == 0;
        SearchResultsPanel.IsVisible = query.Length > 0;

        if (query.Length == 0)
        {
            BindableLayout.SetItemsSource(SearchResultsLayout, null);
            SearchResultsCard.IsVisible = false;
            SearchNoResultsLabel.IsVisible = false;
            UpdateRecentSearchesView();
            return;
        }

        var matchingRecords = searchDocuments
            .Where(document => document.SearchText.Contains(
                query,
                StringComparison.CurrentCultureIgnoreCase))
            .Select(document => document.Transaction)
            .ToList();
        var results = matchingRecords
            .Select((record, index) => TransactionActivityItem.FromRecord(
                record,
                index < matchingRecords.Count - 1))
            .ToList();

        BindableLayout.SetItemsSource(SearchResultsLayout, results);
        SearchResultsCountLabel.Text = $"TRANSACTIONS · {results.Count.ToString(CultureInfo.InvariantCulture)}";
        SearchResultsCard.IsVisible = results.Count > 0;
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
