using System.Globalization;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides unit conversion between feet/inches and decimal feet
/// </summary>
public class UnitConversionService
{
    public const double InchesPerFoot = 12.0;
    
    /// <summary>
    /// Current unit system
    /// </summary>
    public UnitSystem CurrentSystem { get; set; } = UnitSystem.Imperial;
    
    /// <summary>
    /// Converts inches to feet
    /// </summary>
    public static double InchesToFeet(double inches) => inches / InchesPerFoot;
    
    /// <summary>
    /// Converts feet to inches
    /// </summary>
    public static double FeetToInches(double feet) => feet * InchesPerFoot;
    
    /// <summary>
    /// Formats a value in decimal feet as feet and inches string (e.g., "3'-6 1/2"").
    /// </summary>
    public static string FormatFeetInches(double decimalFeet, int inchFractionDenominator = 16)
    {
        var denominator = NormalizeInchFractionDenominator(inchFractionDenominator);
        var sign = decimalFeet < 0 ? "-" : string.Empty;
        var absFeet = Math.Abs(decimalFeet);

        var roundedTotalInches = Math.Round(absFeet * InchesPerFoot * denominator, MidpointRounding.AwayFromZero) / denominator;
        var wholeFeet = (int)Math.Floor(roundedTotalInches / InchesPerFoot);
        var remainingInches = roundedTotalInches - (wholeFeet * InchesPerFoot);
        if (remainingInches >= InchesPerFoot - 1e-9)
        {
            wholeFeet++;
            remainingInches = 0;
        }

        var wholeInches = (int)Math.Floor(remainingInches);
        var fractionalInches = remainingInches - wholeInches;
        var numerator = (int)Math.Round(fractionalInches * denominator, MidpointRounding.AwayFromZero);
        if (numerator >= denominator)
        {
            wholeInches++;
            numerator = 0;
        }

        if (wholeInches >= 12)
        {
            wholeFeet++;
            wholeInches = 0;
        }

        if (numerator == 0)
            return $"{sign}{wholeFeet}'-{wholeInches}\"";

        var divisor = GreatestCommonDivisor(Math.Abs(numerator), denominator);
        var reducedNumerator = numerator / divisor;
        var reducedDenominator = denominator / divisor;
        var inchesPart = wholeInches > 0
            ? $"{wholeInches} {reducedNumerator}/{reducedDenominator}"
            : $"{reducedNumerator}/{reducedDenominator}";

        return $"{sign}{wholeFeet}'-{inchesPart}\"";
    }
    
    /// <summary>
    /// Parses a feet-inches string (e.g., "3'-6\"" or "3.5") to decimal feet
    /// </summary>
    public static double ParseFeetInches(string input)
    {
        return TryParseLength(input, out var value) ? value : 0.0;
    }

    /// <summary>
    /// Parses feet-inches or decimal feet input.
    /// Supports examples like 3.5, 3'-6", 3' 6 1/2", 6 1/2".
    /// </summary>
    public static bool TryParseLength(string? input, out double decimalFeet)
    {
        decimalFeet = 0.0;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var normalized = NormalizeLengthInput(input);

        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var simpleValue) ||
            double.TryParse(normalized, NumberStyles.Float, CultureInfo.CurrentCulture, out simpleValue))
        {
            decimalFeet = simpleValue;
            return true;
        }

        var isNegative = normalized.StartsWith("-", StringComparison.Ordinal);
        if (isNegative)
            normalized = normalized[1..].Trim();

        var feet = 0.0;
        string inchesText;
        if (normalized.Contains('\''))
        {
            var split = normalized.Split('\'', 2, StringSplitOptions.TrimEntries);
            if (!TryParseDoubleFlexible(split[0], out feet))
                return false;

            inchesText = split.Length > 1 ? split[1] : string.Empty;
        }
        else
        {
            inchesText = normalized;
            if (!normalized.Contains('"') && !ContainsFraction(inchesText))
                return false;
        }

        inchesText = inchesText.Replace("\"", string.Empty, StringComparison.Ordinal)
                               .Replace("in", string.Empty, StringComparison.OrdinalIgnoreCase)
                               .Replace("inch", string.Empty, StringComparison.OrdinalIgnoreCase)
                               .Replace("inches", string.Empty, StringComparison.OrdinalIgnoreCase)
                               .Trim();

        if (inchesText.StartsWith("-", StringComparison.Ordinal))
            inchesText = inchesText[1..].Trim();

        var inches = 0.0;
        if (!string.IsNullOrWhiteSpace(inchesText) && !TryParseInchesValue(inchesText, out inches))
            return false;

        decimalFeet = feet + (inches / InchesPerFoot);
        if (isNegative)
            decimalFeet = -decimalFeet;

        return true;
    }

    private static bool TryParseInchesValue(string value, out double inches)
    {
        inches = 0.0;
        var normalized = value.Replace("-", " ", StringComparison.Ordinal)
                              .Replace("  ", " ", StringComparison.Ordinal)
                              .Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return true;

        var parts = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return TryParseSingleInchesToken(parts[0], out inches);

        if (parts.Length == 2 &&
            TryParseDoubleFlexible(parts[0], out var wholeInches) &&
            TryParseFraction(parts[1], out var fractionInches))
        {
            inches = wholeInches + fractionInches;
            return true;
        }

        return false;
    }

    private static bool TryParseSingleInchesToken(string token, out double inches)
    {
        inches = 0.0;
        if (TryParseDoubleFlexible(token, out var numeric))
        {
            inches = numeric;
            return true;
        }

        return TryParseFraction(token, out inches);
    }

    private static bool TryParseFraction(string token, out double value)
    {
        value = 0.0;
        var fractionParts = token.Split('/', 2, StringSplitOptions.TrimEntries);
        if (fractionParts.Length != 2)
            return false;

        if (!double.TryParse(fractionParts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var numerator))
            return false;
        if (!double.TryParse(fractionParts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var denominator))
            return false;
        if (Math.Abs(denominator) < 1e-9)
            return false;

        value = numerator / denominator;
        return true;
    }

    private static bool TryParseDoubleFlexible(string input, out double value)
    {
        return double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
               double.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
    }

    private static bool ContainsFraction(string value)
    {
        return value.Contains('/', StringComparison.Ordinal);
    }

    private static int NormalizeInchFractionDenominator(int denominator)
    {
        if (denominator <= 0)
            return 16;

        return denominator;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        while (b != 0)
        {
            var temp = b;
            b = a % b;
            a = temp;
        }

        return a == 0 ? 1 : a;
    }

    private static string NormalizeLengthInput(string input)
    {
        return input.Trim()
            .Replace('’', '\'')
            .Replace('′', '\'')
            .Replace('“', '"')
            .Replace('”', '"')
            .Replace('″', '"');
    }
}

public enum UnitSystem
{
    Imperial,
    Metric
}
