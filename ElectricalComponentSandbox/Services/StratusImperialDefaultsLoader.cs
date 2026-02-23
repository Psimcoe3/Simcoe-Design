using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using ElectricalComponentSandbox.Conduit.Core.Model;

namespace ElectricalComponentSandbox.Services;

public sealed class StratusImperialDefaults
{
    public static StratusImperialDefaults Empty { get; } = new();

    public string? SourceDirectory { get; init; }
    public string PreferredBenderName { get; init; } = "Greenlee 555";
    public double? MaximumWireFill { get; init; }
    public IReadOnlyList<StratusImperialBendSetting> BendSettings { get; init; } = Array.Empty<StratusImperialBendSetting>();
    public IReadOnlyList<StratusImperialRunAlignmentSpacing> RunAlignmentSpacings { get; init; } = Array.Empty<StratusImperialRunAlignmentSpacing>();
    public IReadOnlyList<StratusImperialWireSpecification> WireSpecifications { get; init; } = Array.Empty<StratusImperialWireSpecification>();

    public bool HasData =>
        BendSettings.Count > 0 ||
        RunAlignmentSpacings.Count > 0 ||
        WireSpecifications.Count > 0 ||
        MaximumWireFill.HasValue;

    public StratusImperialBendSetting? FindPreferredBendSetting(string conduitTypeText, string tradeSize)
    {
        if (string.IsNullOrWhiteSpace(tradeSize))
            return null;

        var conduitType = (conduitTypeText ?? string.Empty).ToUpperInvariant();
        var candidates = BendSettings.Where(setting =>
            string.Equals(setting.TradeSize, tradeSize, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0)
            return null;

        static bool ConduitTypeMatches(string candidateType, string targetType)
        {
            if (string.IsNullOrWhiteSpace(candidateType))
                return true;
            if (string.IsNullOrWhiteSpace(targetType))
                return true;

            var normalizedCandidate = candidateType.ToUpperInvariant();
            if (normalizedCandidate == targetType)
                return true;

            if (normalizedCandidate.Contains("EMT") && targetType.Contains("EMT"))
                return true;
            if ((normalizedCandidate.Contains("RMC") || normalizedCandidate.Contains("RIGID")) &&
                (targetType.Contains("RMC") || targetType.Contains("RIGID") || targetType.Contains("IMC")))
                return true;
            if (normalizedCandidate.Contains("PVC") && targetType.Contains("PVC"))
                return true;
            if ((normalizedCandidate.Contains("FMC") || normalizedCandidate.Contains("FLEX")) &&
                (targetType.Contains("FMC") || targetType.Contains("FLEX")))
                return true;

            return false;
        }

        var filtered = candidates
            .Where(setting => ConduitTypeMatches(setting.ConduitType, conduitType))
            .ToList();
        if (filtered.Count == 0)
            filtered = candidates;

        return filtered
            .OrderBy(setting => string.Equals(setting.BenderName, PreferredBenderName, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(setting => setting.BenderName, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    public double? FindParallelSpacingFeet(string tradeSizeA, string tradeSizeB)
    {
        if (string.IsNullOrWhiteSpace(tradeSizeA) || string.IsNullOrWhiteSpace(tradeSizeB))
            return null;

        var keyA = tradeSizeA.Trim();
        var keyB = tradeSizeB.Trim();
        var match = RunAlignmentSpacings.FirstOrDefault(spacing =>
            (string.Equals(spacing.TradeSize1, keyA, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(spacing.TradeSize2, keyB, StringComparison.OrdinalIgnoreCase)) ||
            (string.Equals(spacing.TradeSize1, keyB, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(spacing.TradeSize2, keyA, StringComparison.OrdinalIgnoreCase)));

        return match == null ? null : match.SpacingFeet;
    }
}

public sealed record StratusImperialBendSetting(
    string BenderName,
    string ConduitType,
    string TradeSize,
    double DiameterFeet,
    double RadiusFeet,
    double DeductFeet,
    double XValueFeet,
    double DefaultEndLengthStubFeet,
    double MinimumDistanceKick90Feet,
    double MinimumDistanceOffsetFeet,
    double MinimumDistanceSaddle3PointFeet,
    double MinimumDistanceSaddle4PointFeet,
    double MinimumDistanceBetweenBendsFeet,
    IReadOnlyDictionary<double, double> MinimumHeightByAngleFeet);

public sealed record StratusImperialRunAlignmentSpacing(string TradeSize1, string TradeSize2, double SpacingFeet);

public sealed record StratusImperialWireSpecification(
    string Name,
    string MaterialName,
    string FeederId,
    int Amperage,
    string? TradeSize,
    double ConduitSizeFeet);

public static class StratusImperialDefaultsLoader
{
    private const string BendSettingsFileName = "DefaultBendSettingsData.xml";
    private const string ConduitRunFileName = "DefaultConduitRunData.xml";
    private const string RunAlignmentFileName = "DefaultRunAlignmentConfiguration.xml";

    private static readonly IReadOnlyDictionary<string, string> TradeSizeTokenMap =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["OneHalfInch"] = "1/2",
            ["ThreeQuartersInch"] = "3/4",
            ["OneInch"] = "1",
            ["OneAndOneForthInch"] = "1-1/4",
            ["OneAndOneFourthInch"] = "1-1/4",
            ["OneAndOneHalfInch"] = "1-1/2",
            ["TwoInches"] = "2",
            ["TwoAndOneHalfInch"] = "2-1/2",
            ["ThreeInches"] = "3",
            ["ThreeAndOneHalfInch"] = "3-1/2",
            ["FourInches"] = "4",
            ["FiveInches"] = "5",
            ["SixInches"] = "6"
        };

    private static readonly IReadOnlyDictionary<double, string> NominalTradeSizeByInches =
        new Dictionary<double, string>
        {
            [0.5] = "1/2",
            [0.75] = "3/4",
            [1.0] = "1",
            [1.25] = "1-1/4",
            [1.5] = "1-1/2",
            [2.0] = "2",
            [2.5] = "2-1/2",
            [3.0] = "3",
            [3.5] = "3-1/2",
            [4.0] = "4",
            [5.0] = "5",
            [6.0] = "6"
        };

    private static readonly IReadOnlyList<string> KnownTradeSizesDescending =
    [
        "6",
        "5",
        "4",
        "3-1/2",
        "3",
        "2-1/2",
        "2",
        "1-1/2",
        "1-1/4",
        "1",
        "3/4",
        "1/2"
    ];

    private static readonly IReadOnlyList<ConduitSize> ImperialEmtSizes = ConduitSizeSettings.CreateDefaultEMT().Sizes;

    public static StratusImperialDefaults LoadImperialDefaults(string? directoryPath, string preferredBenderName = "Greenlee 555")
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            return StratusImperialDefaults.Empty;

        var bendSettings = LoadBendSettings(Path.Combine(directoryPath, BendSettingsFileName));
        var (maximumWireFill, wireSpecifications) = LoadRunDefaults(Path.Combine(directoryPath, ConduitRunFileName));
        var runSpacings = LoadRunAlignmentSpacings(Path.Combine(directoryPath, RunAlignmentFileName));

        return new StratusImperialDefaults
        {
            SourceDirectory = directoryPath,
            PreferredBenderName = preferredBenderName,
            MaximumWireFill = maximumWireFill,
            BendSettings = bendSettings,
            RunAlignmentSpacings = runSpacings,
            WireSpecifications = wireSpecifications
        };
    }

    private static IReadOnlyList<StratusImperialBendSetting> LoadBendSettings(string filePath)
    {
        if (!File.Exists(filePath))
            return Array.Empty<StratusImperialBendSetting>();

        var doc = LoadXmlDocument(filePath);
        if (doc == null)
            return Array.Empty<StratusImperialBendSetting>();
        var items = new List<StratusImperialBendSetting>();

        foreach (var node in doc.Descendants().Where(element => element.Name.LocalName == "BendFamilyConfigurationLookupItem"))
        {
            var diameterFeet = ParseDouble(node, "Diameter");
            var tradeSize = ResolveTradeSizeFromNominalDiameterFeet(diameterFeet);
            if (string.IsNullOrEmpty(tradeSize))
                continue;

            var conduitType = ParseString(node, "ConduitType");
            if (string.IsNullOrWhiteSpace(conduitType))
                continue;

            var minHeightByAngle = new Dictionary<double, double>
            {
                [15.0] = ParseDouble(node, "MinimumHeightAtAngle15"),
                [22.5] = ParseDouble(node, "MinimumHeightAtAngle22_5"),
                [30.0] = ParseDouble(node, "MinimumHeightAtAngle30"),
                [45.0] = ParseDouble(node, "MinimumHeightAtAngle45"),
                [60.0] = ParseDouble(node, "MinimumHeightAtAngle60"),
                [90.0] = ParseDouble(node, "MinimumHeightAtAngle90")
            };

            items.Add(new StratusImperialBendSetting(
                ParseString(node, "BenderName"),
                conduitType,
                tradeSize,
                diameterFeet,
                ParseDouble(node, "Radius"),
                ParseDouble(node, "Deduct"),
                ParseDouble(node, "XValue"),
                ParseDouble(node, "DefaultEndLengthStub"),
                ParseDouble(node, "MinimumDistanceKick90"),
                ParseDouble(node, "MinimumDistanceOffset"),
                ParseDouble(node, "MinimumDistanceSaddle3Point"),
                ParseDouble(node, "MinimumDistanceSaddle4Point"),
                ParseDouble(node, "MinimumDistanceBetweenBends"),
                minHeightByAngle));
        }

        return items;
    }

    private static (double? MaximumWireFill, IReadOnlyList<StratusImperialWireSpecification> WireSpecs) LoadRunDefaults(string filePath)
    {
        if (!File.Exists(filePath))
            return (null, Array.Empty<StratusImperialWireSpecification>());

        var doc = LoadXmlDocument(filePath);
        if (doc == null)
            return (null, Array.Empty<StratusImperialWireSpecification>());
        double? maxFill = null;
        var maxFillElement = doc.Descendants().FirstOrDefault(element => element.Name.LocalName == "MaximumWireFill");
        if (maxFillElement != null && double.TryParse(maxFillElement.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedFill))
            maxFill = parsedFill;

        var specs = new List<StratusImperialWireSpecification>();
        foreach (var node in doc.Descendants().Where(element => element.Name.LocalName == "WireSpecificationData"))
        {
            var conduitSizeFeet = ParseDouble(node, "ConduitSize");
            var tradeSize = ResolveTradeSizeFromNominalDiameterFeet(conduitSizeFeet);
            if (string.IsNullOrEmpty(tradeSize))
                continue;

            specs.Add(new StratusImperialWireSpecification(
                ParseString(node, "Name"),
                ParseString(node, "MaterialName"),
                ParseString(node, "FeederId"),
                ParseInt(node, "Amperage"),
                tradeSize,
                conduitSizeFeet));
        }

        return (maxFill, specs);
    }

    private static IReadOnlyList<StratusImperialRunAlignmentSpacing> LoadRunAlignmentSpacings(string filePath)
    {
        if (!File.Exists(filePath))
            return Array.Empty<StratusImperialRunAlignmentSpacing>();

        var doc = LoadXmlDocument(filePath);
        if (doc == null)
            return Array.Empty<StratusImperialRunAlignmentSpacing>();
        var spacings = new List<StratusImperialRunAlignmentSpacing>();

        foreach (var node in doc.Descendants().Where(element => element.Name.LocalName == "TradeSizeMapping"))
        {
            var rawA = ParseString(node, "Size1");
            var rawB = ParseString(node, "Size2");
            if (!TradeSizeTokenMap.TryGetValue(rawA, out var tradeSizeA) ||
                !TradeSizeTokenMap.TryGetValue(rawB, out var tradeSizeB))
            {
                continue;
            }

            var spacingFeet = ParseDouble(node, "Spacing");
            if (spacingFeet <= 0.0)
                continue;

            spacings.Add(new StratusImperialRunAlignmentSpacing(tradeSizeA, tradeSizeB, spacingFeet));
        }

        return spacings;
    }

    private static string? ResolveTradeSizeFromNominalDiameterFeet(double diameterFeet)
    {
        if (diameterFeet <= 0.0)
            return null;

        var nominalInches = Math.Round(diameterFeet * 12.0, 4);
        var closest = NominalTradeSizeByInches
            .OrderBy(entry => Math.Abs(entry.Key - nominalInches))
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(closest.Value))
            return null;

        return Math.Abs(closest.Key - nominalInches) <= 0.02
            ? closest.Value
            : null;
    }

    public static string? ResolveTradeSizeFromConduitTypeText(string? conduitTypeText)
    {
        if (string.IsNullOrWhiteSpace(conduitTypeText))
            return null;

        foreach (var tradeSize in KnownTradeSizesDescending)
        {
            var escapedTradeSize = Regex.Escape(tradeSize);
            if (Regex.IsMatch(conduitTypeText, $@"\btrade\s*{escapedTradeSize}\b", RegexOptions.IgnoreCase) ||
                Regex.IsMatch(conduitTypeText, $@"\b{escapedTradeSize}\s*(?:in|inch|inches)\b", RegexOptions.IgnoreCase))
            {
                return tradeSize;
            }
        }

        return null;
    }

    public static string? ResolveTradeSizeFromOuterDiameterFeet(double outerDiameterFeet)
    {
        if (outerDiameterFeet <= 0.0)
            return null;

        var outerInches = outerDiameterFeet * 12.0;
        var closest = ImperialEmtSizes
            .OrderBy(size => Math.Abs(size.OuterDiameter - outerInches))
            .FirstOrDefault();

        if (closest == null)
            return null;

        return Math.Abs(closest.OuterDiameter - outerInches) <= 0.3
            ? closest.TradeSize
            : null;
    }

    private static string ParseString(XElement parent, string childName)
    {
        return parent.Elements().FirstOrDefault(element => element.Name.LocalName == childName)?.Value?.Trim() ?? string.Empty;
    }

    private static XDocument? LoadXmlDocument(string filePath)
    {
        try
        {
            return XDocument.Load(filePath);
        }
        catch (XmlException ex) when (ex.Message.Contains("Cannot switch to Unicode", StringComparison.OrdinalIgnoreCase))
        {
            // Some STRATUS exports carry an encoding declaration that does not match file bytes.
            // Parse as text after removing the declaration so we can still use imperial defaults.
            var xml = File.ReadAllText(filePath);
            var declarationRemoved = Regex.Replace(xml, @"^\s*<\?xml[^>]*\?>", string.Empty, RegexOptions.IgnoreCase);
            return XDocument.Parse(declarationRemoved);
        }
    }

    private static int ParseInt(XElement parent, string childName)
    {
        var value = ParseString(parent, childName);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static double ParseDouble(XElement parent, string childName)
    {
        var value = ParseString(parent, childName);
        return double.TryParse(value, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0.0;
    }
}
