using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using FinancialTracker.Helpers;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed class SettingsViewModel(SettingsService settingsService) : INotifyPropertyChanged
{
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private AppSettingsRecord settings = new();
    private bool isInitialized;
    private string name = "Faikal";
    private string savedName = "Faikal";
    private CurrencyOption selectedCurrency = SupportedCurrencies[0];
    private string selectedTheme = AppearanceValueNormalizer.DefaultTheme;
    private string selectedAccentColorHex = AppearanceValueNormalizer.DefaultAccentColor;
    private string customAccentColorHex = AppearanceValueNormalizer.DefaultAccentColor;

    public static IReadOnlyList<CurrencyOption> SupportedCurrencies { get; } =
    [
        new("flag_myr.png", "MYR", "Malaysian Ringgit", "RM"),
        new("flag_usd.png", "USD", "United States Dollar", "$"),
        new("flag_sgd.png", "SGD", "Singapore Dollar", "S$"),
        new("flag_krw.png", "KRW", "South Korean Won", "₩")
    ];

    public static IReadOnlyList<AccentColorOption> SupportedAccentColors { get; } =
    [
        new("Purple", "#5044E4"),
        new("Ocean", "#1477D4"),
        new("Teal", "#0F766E"),
        new("Rose", "#C2416C"),
        new("Orange", "#C65D16"),
        new("Slate", "#475569")
    ];

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name
    {
        get => name;
        set
        {
            var cleanedValue = value ?? string.Empty;
            if (SetProperty(ref name, cleanedValue))
            {
                OnPropertyChanged(nameof(GreetingName));
                OnPropertyChanged(nameof(ProfileInitial));
                OnPropertyChanged(nameof(IsNameDirty));
                OnPropertyChanged(nameof(SaveButtonText));
            }
        }
    }

    public string GreetingName =>
        string.IsNullOrWhiteSpace(Name) ? "Hello World" : Name.Trim();

    public string ProfileInitial =>
        string.IsNullOrWhiteSpace(Name) ? "F" : Name.Trim()[..1].ToUpperInvariant();

    public bool IsNameDirty =>
        !string.Equals(Name.Trim(), savedName, StringComparison.Ordinal);

    public string SaveButtonText => IsNameDirty ? "Save profile" : "Saved";

    public CurrencyOption SelectedCurrency
    {
        get => selectedCurrency;
        private set
        {
            if (SetProperty(ref selectedCurrency, value))
            {
                NotifyCurrencyFormattingChanged();
            }
        }
    }

    public string SelectedCurrencySummary =>
        $"{SelectedCurrency.Code} - {SelectedCurrency.Name}";

    public string SelectedCurrencySymbol =>
        $"Shown as {SelectedCurrency.Symbol}";

    public string SelectedTheme
    {
        get => selectedTheme;
        private set => SetProperty(ref selectedTheme, value);
    }

    public string SelectedAccentColorHex
    {
        get => selectedAccentColorHex;
        private set
        {
            if (SetProperty(ref selectedAccentColorHex, value))
            {
                OnPropertyChanged(nameof(SelectedAccentLabel));
                OnPropertyChanged(nameof(CanApplyCustomAccent));
                OnPropertyChanged(nameof(ApplyAccentButtonText));
            }
        }
    }

    public string SelectedAccentLabel
    {
        get
        {
            var preset = SupportedAccentColors.FirstOrDefault(
                option => option.Hex.Equals(SelectedAccentColorHex, StringComparison.OrdinalIgnoreCase));
            return $"{preset?.Name ?? "Custom"} · {SelectedAccentColorHex}";
        }
    }

    public string CustomAccentColorHex
    {
        get => customAccentColorHex;
        set
        {
            if (SetProperty(ref customAccentColorHex, value ?? string.Empty))
            {
                OnPropertyChanged(nameof(CanApplyCustomAccent));
                OnPropertyChanged(nameof(ApplyAccentButtonText));
                OnPropertyChanged(nameof(AccentValidationMessage));
                OnPropertyChanged(nameof(HasAccentValidationError));
            }
        }
    }

    public bool CanApplyCustomAccent =>
        AppearanceValueNormalizer.TryNormalizeAccentColor(
            CustomAccentColorHex,
            out var normalized) &&
        !normalized.Equals(SelectedAccentColorHex, StringComparison.OrdinalIgnoreCase);

    public string ApplyAccentButtonText =>
        AppearanceValueNormalizer.TryNormalizeAccentColor(
            CustomAccentColorHex,
            out var normalized) &&
        normalized.Equals(SelectedAccentColorHex, StringComparison.OrdinalIgnoreCase)
            ? "Applied"
            : "Apply";

    public bool HasAccentValidationError =>
        !string.IsNullOrWhiteSpace(CustomAccentColorHex) &&
        !AppearanceValueNormalizer.TryNormalizeAccentColor(CustomAccentColorHex, out _);

    public string AccentValidationMessage =>
        HasAccentValidationError ? "Enter a HEX colour such as #5044E4." : string.Empty;

    public string AppVersionText =>
        $"Version {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})";

    public string BuildRevisionText
    {
        get
        {
            var informationalVersion = typeof(SettingsViewModel).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            var metadataSeparator = informationalVersion?.IndexOf('+') ?? -1;
            var revision = metadataSeparator >= 0
                ? informationalVersion![(metadataSeparator + 1)..]
                : null;

            return string.IsNullOrWhiteSpace(revision)
                ? "Development build"
                : $"Commit {revision[..Math.Min(7, revision.Length)]}";
        }
    }

    public async Task InitializeAsync()
    {
        if (isInitialized)
        {
            return;
        }

        await initializationLock.WaitAsync();
        try
        {
            if (isInitialized)
            {
                return;
            }

            settings = await settingsService.GetAsync();
            name = settings.Name;
            savedName = settings.Name.Trim();
            selectedCurrency = SupportedCurrencies.FirstOrDefault(
                option => option.Code.Equals(settings.CurrencyCode, StringComparison.OrdinalIgnoreCase))
                ?? SupportedCurrencies[0];
            selectedTheme = AppearanceValueNormalizer.NormalizeTheme(settings.Theme);
            selectedAccentColorHex = AppearanceValueNormalizer.NormalizeAccentColor(
                settings.AccentColorHex);
            customAccentColorHex = selectedAccentColorHex;
            AppearanceService.ApplyAndCache(
                selectedTheme,
                selectedAccentColorHex);
            isInitialized = true;
            OnPropertyChanged(string.Empty);
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public async Task SaveNameAsync()
    {
        var requestedName = Name.Trim();
        await InitializeAsync();
        Name = requestedName;
        if (!IsNameDirty)
        {
            return;
        }

        settings.Name = Name;
        await SaveAsync();
        savedName = Name;
        OnPropertyChanged(nameof(IsNameDirty));
        OnPropertyChanged(nameof(SaveButtonText));
    }

    public async Task SelectCurrencyAsync(string currencyCode)
    {
        await InitializeAsync();
        var currency = SupportedCurrencies.FirstOrDefault(
            option => option.Code.Equals(currencyCode, StringComparison.OrdinalIgnoreCase));

        if (currency is null || currency == SelectedCurrency)
        {
            return;
        }

        SelectedCurrency = currency;
        settings.CurrencyCode = currency.Code;
        await SaveAsync();
    }

    public async Task SelectThemeAsync(string theme)
    {
        await InitializeAsync();
        var normalizedTheme = AppearanceValueNormalizer.NormalizeTheme(theme);
        if (normalizedTheme == SelectedTheme)
        {
            return;
        }

        SelectedTheme = normalizedTheme;
        settings.Theme = normalizedTheme;
        AppearanceService.ApplyAndCache(
            normalizedTheme,
            SelectedAccentColorHex);
        await SaveAsync();
    }

    public async Task SelectAccentColorAsync(string accentColorHex)
    {
        await InitializeAsync();
        var normalized = AppearanceValueNormalizer.NormalizeAccentColor(accentColorHex);
        if (normalized.Equals(SelectedAccentColorHex, StringComparison.OrdinalIgnoreCase))
        {
            CustomAccentColorHex = normalized;
            return;
        }

        SelectedAccentColorHex = normalized;
        CustomAccentColorHex = normalized;
        settings.AccentColorHex = normalized;
        AppearanceService.ApplyAndCache(SelectedTheme, normalized);
        await SaveAsync();
    }

    public Task ApplyCustomAccentColorAsync() =>
        AppearanceValueNormalizer.TryNormalizeAccentColor(
            CustomAccentColorHex,
            out var normalized)
            ? SelectAccentColorAsync(normalized)
            : Task.CompletedTask;

    private async Task SaveAsync()
    {
        await InitializeAsync();

        await saveLock.WaitAsync();
        try
        {
            await settingsService.SaveAsync(settings);
        }
        finally
        {
            saveLock.Release();
        }
    }

    private void NotifyCurrencyFormattingChanged()
    {
        OnPropertyChanged(nameof(SelectedCurrencySummary));
        OnPropertyChanged(nameof(SelectedCurrencySymbol));
    }

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
