using System.Globalization;
using System.Text;

namespace FinancialTracker.Helpers;

public static class MoneyInputParser
{
    private const int DecimalPlaces = 2;
    private const NumberStyles InputStyles =
        NumberStyles.AllowDecimalPoint |
        NumberStyles.AllowThousands |
        NumberStyles.AllowLeadingWhite |
        NumberStyles.AllowTrailingWhite;

    public static bool TryParseMinor(
        string? value,
        bool allowZero,
        out long amountMinor)
    {
        amountMinor = 0;
        if ((!decimal.TryParse(value, InputStyles, CultureInfo.CurrentCulture, out var amount) &&
             !decimal.TryParse(value, InputStyles, CultureInfo.InvariantCulture, out amount)) ||
            amount < 0 ||
            (!allowZero && amount == 0) ||
            amount > long.MaxValue / 100m)
        {
            return false;
        }

        amountMinor = decimal.ToInt64(
            decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
        return allowZero || amountMinor > 0;
    }

    public static string SanitizeDecimal(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (IsAlreadySanitized(value))
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        var hasDecimalPoint = false;
        var decimalDigits = 0;

        foreach (var character in value)
        {
            if (character is >= '0' and <= '9')
            {
                if (!hasDecimalPoint || decimalDigits < DecimalPlaces)
                {
                    result.Append(character);
                    if (hasDecimalPoint)
                    {
                        decimalDigits++;
                    }
                }

                continue;
            }

            if (character == '.' && !hasDecimalPoint)
            {
                if (result.Length == 0)
                {
                    result.Append('0');
                }

                result.Append('.');
                hasDecimalPoint = true;
            }
        }

        return result.ToString();
    }

    private static bool IsAlreadySanitized(string value)
    {
        var decimalIndex = -1;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is >= '0' and <= '9')
            {
                if (decimalIndex >= 0 && index - decimalIndex > DecimalPlaces)
                {
                    return false;
                }

                continue;
            }

            if (character != '.' || decimalIndex >= 0 || index == 0)
            {
                return false;
            }

            decimalIndex = index;
        }

        return true;
    }
}
