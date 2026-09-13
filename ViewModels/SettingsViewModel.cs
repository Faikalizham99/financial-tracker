using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using FinancialTracker.Models;
using FinancialTracker.Services;

namespace FinancialTracker.ViewModels;

public sealed class SettingsViewModel(SettingsService settingsService) : INotifyPropertyChanged
{
    private readonly SemaphoreSlim saveLock = new(1, 1);
    private AppSettingsRecord settings = new();
    private bool isInitialized;
    private string name = "Faikal";
    private string savedName = "Faikal";
    private CurrencyOption selectedCurrency = SupportedCurrencies[0];
    private string selectedTheme = "Light";
    private string selectedAccentColorHex = "#5044E4";
    private string customAccentColorHex = "#5044E4";

    public static IReadOnlyList<CurrencyOption> SupportedCurrencies { get; } =
    [
        new("🇲🇾", "flag_myr.png", "MYR", "Malaysian Ringgit", "RM"),
        new("🇺🇸", "flag_usd.png", "USD", "United States Dollar", "$"),
        new("🇸🇬", "flag_sgd.png", "SGD", "Singapore Dollar", "S$"),
        new("🇰🇷", "flag_krw.png", "KRW", "South Korean Won", "₩")
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
                OnPropertyChanged(nameof(AccentValidationMessage));
                OnPropertyChanged(nameof(HasAccentValidationError));
            }
        }
    }

    public bool CanApplyCustomAccent =>
        TryNormalizeAccentColor(CustomAccentColorHex, out var normalized) &&
        !normalized.Equals(SelectedAccentColorHex, StringComparison.OrdinalIgnoreCase);

    public bool HasAccentValidationError =>
        !string.IsNullOrWhiteSpace(CustomAccentColorHex) &&
        !TryNormalizeAccentColor(CustomAccentColorHex, out _);

    public string AccentValidationMessage =>
        HasAccentValidationError ? "Enter a HEX colour such as #5044E4." : string.Empty;

    public string AvailableAmountText => FormatMoney(3240.50m);
    public string IncomeAmountText => FormatMoney(6800m);
    public string SpentAmountText => FormatMoney(3559.50m);
    public string TotalSpendingText => FormatMoney(1687.50m);
    public string MedicalAmountText => FormatMoney(-172m);
    public string GroceryAmountText => FormatMoney(-38.90m);
    public string FamilyAmountText => FormatMoney(22m, showPositiveSign: true);
    public string FuelAmountText => FormatMoney(-8m);
    public string MarketAmountText => FormatMoney(-50m);
    public string PharmacyAmountText => FormatMoney(-19.80m);
    public string BreakfastAmountText => FormatMoney(-18.50m);

    public async Task InitializeAsync()
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
        selectedTheme = NormalizeTheme(settings.Theme);
        selectedAccentColorHex = NormalizeAccentColor(settings.AccentColorHex);
        customAccentColorHex = selectedAccentColorHex;
        ApplyTheme(selectedTheme);
        ApplyAccentColor(selectedAccentColorHex, selectedTheme);
        isInitialized = true;
        OnPropertyChanged(string.Empty);
    }

    public async Task SaveNameAsync()
    {
        Name = Name.Trim();
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
        var normalizedTheme = NormalizeTheme(theme);
        if (normalizedTheme == SelectedTheme)
        {
            return;
        }

        SelectedTheme = normalizedTheme;
        settings.Theme = normalizedTheme;
        ApplyTheme(normalizedTheme);
        ApplyAccentColor(SelectedAccentColorHex, normalizedTheme);
        await SaveAsync();
    }

    public async Task SelectAccentColorAsync(string accentColorHex)
    {
        var normalized = NormalizeAccentColor(accentColorHex);
        if (normalized.Equals(SelectedAccentColorHex, StringComparison.OrdinalIgnoreCase))
        {
            CustomAccentColorHex = normalized;
            return;
        }

        SelectedAccentColorHex = normalized;
        CustomAccentColorHex = normalized;
        settings.AccentColorHex = normalized;
        ApplyAccentColor(normalized, SelectedTheme);
        await SaveAsync();
    }

    public Task ApplyCustomAccentColorAsync() =>
        TryNormalizeAccentColor(CustomAccentColorHex, out var normalized)
            ? SelectAccentColorAsync(normalized)
            : Task.CompletedTask;

    private string FormatMoney(decimal amount, bool showPositiveSign = false)
    {
        var sign = amount < 0 ? "−" : showPositiveSign ? "+" : string.Empty;
        return $"{sign}{SelectedCurrency.Symbol} {Math.Abs(amount).ToString("N2", CultureInfo.InvariantCulture)}";
    }

    private async Task SaveAsync()
    {
        if (!isInitialized)
        {
            return;
        }

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

    private static string NormalizeTheme(string? theme) =>
        theme?.Trim().ToLowerInvariant() switch
        {
            "system" => "System",
            "dark" => "Dark",
            _ => "Light"
        };

    private static void ApplyTheme(string theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.UserAppTheme = theme switch
        {
            "System" => AppTheme.Unspecified,
            "Dark" => AppTheme.Dark,
            _ => AppTheme.Light
        };
    }

    private static string NormalizeAccentColor(string? value) =>
        TryNormalizeAccentColor(value, out var normalized) ? normalized : "#5044E4";

    private static bool TryNormalizeAccentColor(string? value, out string normalized)
    {
        var hex = value?.Trim().TrimStart('#') ?? string.Empty;
        if (hex.Length == 3 && hex.All(Uri.IsHexDigit))
        {
            hex = string.Concat(hex.Select(character => $"{character}{character}"));
        }

        if (hex.Length == 6 && hex.All(Uri.IsHexDigit))
        {
            normalized = $"#{hex.ToUpperInvariant()}";
            return true;
        }

        normalized = string.Empty;
        return false;
    }

    private static void ApplyAccentColor(string accentColorHex, string theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        var useDarkTint = theme == "Dark" ||
            (theme == "System" && Application.Current.RequestedTheme == AppTheme.Dark);
        var tintHex = MixColors(
            accentColorHex,
            useDarkTint ? "#17142D" : "#FFFFFF",
            useDarkTint ? 0.68 : 0.86);
        var foregroundHex = GetContrastColor(accentColorHex);
        var resources = Application.Current.Resources;

        resources["Accent"] = Color.FromArgb(accentColorHex);
        resources["AccentDark"] = Color.FromArgb(accentColorHex);
        resources["AccentTint"] = Color.FromArgb(tintHex);
        resources["AccentTintLight"] = Color.FromArgb(tintHex);
        resources["AccentTintDark"] = Color.FromArgb(tintHex);
        resources["AccentForeground"] = Color.FromArgb(foregroundHex);
        resources["Primary"] = Color.FromArgb(accentColorHex);
        resources["PrimaryDark"] = Color.FromArgb(accentColorHex);
        resources["Secondary"] = Color.FromArgb(tintHex);
    }

    private static string MixColors(string foreground, string background, double backgroundWeight)
    {
        var foregroundValue = Convert.ToInt32(foreground.TrimStart('#'), 16);
        var backgroundValue = Convert.ToInt32(background.TrimStart('#'), 16);
        var red = MixChannel((foregroundValue >> 16) & 0xFF, (backgroundValue >> 16) & 0xFF, backgroundWeight);
        var green = MixChannel((foregroundValue >> 8) & 0xFF, (backgroundValue >> 8) & 0xFF, backgroundWeight);
        var blue = MixChannel(foregroundValue & 0xFF, backgroundValue & 0xFF, backgroundWeight);
        return $"#{red:X2}{green:X2}{blue:X2}";
    }

    private static int MixChannel(int foreground, int background, double backgroundWeight) =>
        (int)Math.Round((foreground * (1 - backgroundWeight)) + (background * backgroundWeight));

    private static string GetContrastColor(string colorHex)
    {
        var value = Convert.ToInt32(colorHex.TrimStart('#'), 16);
        var red = (value >> 16) & 0xFF;
        var green = (value >> 8) & 0xFF;
        var blue = value & 0xFF;
        var luminance = ((0.299 * red) + (0.587 * green) + (0.114 * blue)) / 255;
        return luminance > 0.62 ? "#17142D" : "#FFFFFF";
    }

    private void NotifyCurrencyFormattingChanged()
    {
        OnPropertyChanged(nameof(SelectedCurrencySummary));
        OnPropertyChanged(nameof(SelectedCurrencySymbol));
        OnPropertyChanged(nameof(AvailableAmountText));
        OnPropertyChanged(nameof(IncomeAmountText));
        OnPropertyChanged(nameof(SpentAmountText));
        OnPropertyChanged(nameof(TotalSpendingText));
        OnPropertyChanged(nameof(MedicalAmountText));
        OnPropertyChanged(nameof(GroceryAmountText));
        OnPropertyChanged(nameof(FamilyAmountText));
        OnPropertyChanged(nameof(FuelAmountText));
        OnPropertyChanged(nameof(MarketAmountText));
        OnPropertyChanged(nameof(PharmacyAmountText));
        OnPropertyChanged(nameof(BreakfastAmountText));
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
