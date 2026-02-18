namespace ElectricalComponentSandbox.Conduit.Core.Model;

/// <summary>
/// Conduit material/standard enumeration.
/// </summary>
public enum ConduitMaterialType
{
    EMT,  // Electrical Metallic Tubing
    RMC,  // Rigid Metal Conduit
    IMC,  // Intermediate Metal Conduit
    PVC,  // Polyvinyl Chloride
    LFMC, // Liquidtight Flexible Metal Conduit
    LFNC, // Liquidtight Flexible Nonmetallic Conduit
    FMC,  // Flexible Metal Conduit
    ENT   // Electrical Nonmetallic Tubing
}

/// <summary>
/// A single trade size entry with nominal and actual dimensions.
/// </summary>
public class ConduitSize
{
    /// <summary>Trade size designation, e.g. "1/2", "3/4", "1"</summary>
    public string TradeSize { get; set; } = string.Empty;

    /// <summary>Nominal diameter in inches</summary>
    public double NominalDiameter { get; set; }

    /// <summary>Outer diameter in inches</summary>
    public double OuterDiameter { get; set; }

    /// <summary>Inner diameter in inches</summary>
    public double InnerDiameter { get; set; }

    /// <summary>Weight per foot in lbs</summary>
    public double WeightPerFoot { get; set; }
}

/// <summary>
/// Manages the set of available conduit sizes for a given standard.
/// Provides iterator support and min-length validation.
/// </summary>
public class ConduitSizeSettings
{
    public ConduitMaterialType Standard { get; set; } = ConduitMaterialType.EMT;

    private readonly List<ConduitSize> _sizes = new();

    /// <summary>Minimum allowed conduit segment length in inches (default 0.1).</summary>
    public double MinLengthInches { get; set; } = 0.1;

    public IReadOnlyList<ConduitSize> Sizes => _sizes;

    public void AddSize(ConduitSize size) => _sizes.Add(size);
    public void RemoveSize(string tradeSize) =>
        _sizes.RemoveAll(s => s.TradeSize == tradeSize);

    public ConduitSize? GetSize(string tradeSize) =>
        _sizes.FirstOrDefault(s => s.TradeSize == tradeSize);

    public IEnumerator<ConduitSize> GetEnumerator() => _sizes.GetEnumerator();

    /// <summary>
    /// Validates that a segment length meets minimum requirements.
    /// </summary>
    public bool IsValidLength(double lengthInches)
    {
        return lengthInches >= MinLengthInches;
    }

    /// <summary>
    /// Creates default EMT size table.
    /// </summary>
    public static ConduitSizeSettings CreateDefaultEMT()
    {
        var settings = new ConduitSizeSettings { Standard = ConduitMaterialType.EMT };
        settings.AddSize(new ConduitSize { TradeSize = "1/2", NominalDiameter = 0.5, OuterDiameter = 0.706, InnerDiameter = 0.622, WeightPerFoot = 0.29 });
        settings.AddSize(new ConduitSize { TradeSize = "3/4", NominalDiameter = 0.75, OuterDiameter = 0.922, InnerDiameter = 0.824, WeightPerFoot = 0.45 });
        settings.AddSize(new ConduitSize { TradeSize = "1", NominalDiameter = 1.0, OuterDiameter = 1.163, InnerDiameter = 1.049, WeightPerFoot = 0.65 });
        settings.AddSize(new ConduitSize { TradeSize = "1-1/4", NominalDiameter = 1.25, OuterDiameter = 1.510, InnerDiameter = 1.380, WeightPerFoot = 0.96 });
        settings.AddSize(new ConduitSize { TradeSize = "1-1/2", NominalDiameter = 1.5, OuterDiameter = 1.740, InnerDiameter = 1.610, WeightPerFoot = 1.11 });
        settings.AddSize(new ConduitSize { TradeSize = "2", NominalDiameter = 2.0, OuterDiameter = 2.197, InnerDiameter = 2.067, WeightPerFoot = 1.43 });
        settings.AddSize(new ConduitSize { TradeSize = "2-1/2", NominalDiameter = 2.5, OuterDiameter = 2.875, InnerDiameter = 2.731, WeightPerFoot = 2.68 });
        settings.AddSize(new ConduitSize { TradeSize = "3", NominalDiameter = 3.0, OuterDiameter = 3.500, InnerDiameter = 3.356, WeightPerFoot = 3.23 });
        settings.AddSize(new ConduitSize { TradeSize = "4", NominalDiameter = 4.0, OuterDiameter = 4.500, InnerDiameter = 4.334, WeightPerFoot = 4.65 });
        return settings;
    }
}
