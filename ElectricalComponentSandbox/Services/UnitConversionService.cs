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
    /// Formats a value in decimal feet as feet and inches string (e.g., "3'-6\"")
    /// </summary>
    public static string FormatFeetInches(double decimalFeet)
    {
        int wholeFeet = (int)Math.Floor(Math.Abs(decimalFeet));
        double remainingInches = Math.Round((Math.Abs(decimalFeet) - wholeFeet) * InchesPerFoot, 2);
        
        // Handle rounding to 12 inches
        if (remainingInches >= InchesPerFoot)
        {
            wholeFeet++;
            remainingInches = 0;
        }
        
        string sign = decimalFeet < 0 ? "-" : "";
        
        if (remainingInches == 0)
            return $"{sign}{wholeFeet}'-0\"";
        
        return $"{sign}{wholeFeet}'-{remainingInches:F1}\"";
    }
    
    /// <summary>
    /// Parses a feet-inches string (e.g., "3'-6\"" or "3.5") to decimal feet
    /// </summary>
    public static double ParseFeetInches(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return 0.0;
        
        input = input.Trim();
        
        // Try simple decimal
        if (double.TryParse(input, out double simpleValue))
            return simpleValue;
        
        // Try feet-inches format: 3'-6" or 3' 6"
        var cleaned = input.Replace("\"", "").Replace("'", " ").Replace("-", " ");
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length >= 2 && double.TryParse(parts[0], out double feet)
            && double.TryParse(parts[1], out double inches))
        {
            return feet + inches / InchesPerFoot;
        }
        
        if (parts.Length == 1 && double.TryParse(parts[0], out double singleValue))
        {
            return singleValue;
        }
        
        return 0.0;
    }
}

public enum UnitSystem
{
    Imperial,
    Metric
}
