using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Reads and writes the eVolve <c>ParameterPushData</c> XML schema used by
/// the Project Conduit Run Schedule. Roundtripping is lossless: a file
/// exported by Simcoe imports cleanly into eVolve-equipped Revit and back.
/// </summary>
public static class RunScheduleXmlService
{
    /// <summary>XML root element name.</summary>
    public const string RootElement = "ParameterPushData";

    private static readonly XNamespace Xsd = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";

    // ── Public API ───────────────────────────────────────────────────────

    /// <summary>
    /// Serializes a configuration to UTF-16 XML using the same encoding,
    /// element order, and casing the eVolve template uses.
    /// </summary>
    public static string Serialize(RunScheduleConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var root = new XElement(RootElement,
            new XAttribute(XNamespace.Xmlns + "xsd", Xsd.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", Xsi.NamespaceName));

        // RunData is always written as an empty element in the eVolve template.
        root.Add(new XElement("RunData"));
        root.Add(BoolElement("SystemColorsEnabled", config.SystemColorsEnabled));
        root.Add(SystemColorsElement(config));

        root.Add(HighlightElement("RunIdNotDefined", config.RunIdNotDefined));
        root.Add(HighlightElement("RunIdNotAssigned", config.RunIdNotAssigned));
        root.Add(HighlightElement("SizeMismatch", config.SizeMismatch));
        root.Add(HighlightElement("StartMismatch", config.StartMismatch));
        root.Add(HighlightElement("FinishMismatch", config.FinishMismatch));
        root.Add(HighlightElement("TypeMismatch", config.TypeMismatch));
        root.Add(HighlightElement("SystemMismatch", config.SystemMismatch));

        root.Add(BoolElement("AutomaticallyPushRunInfo", config.AutomaticallyPushRunInfo));
        root.Add(DoubleElement("MaximumWireFill", config.MaximumWireFill));
        root.Add(new XElement("WireDescriptionFormat", config.WireDescriptionFormat.ToString()));
        root.Add(RunStatusesElement(config));

        var specs = new XElement("WireSpecifications");
        foreach (var spec in config.WireSpecifications)
            specs.Add(WireSpecElement(spec));
        root.Add(specs);

        var sizes = new XElement("WireSizes");
        foreach (var size in config.WireSizes)
            sizes.Add(WireSizeElement(size));
        root.Add(sizes);

        root.Add(new XElement("StatusSetOnAssignRun", config.StatusSetOnAssignRun));

        var doc = new XDocument(new XDeclaration("1.0", "utf-16", null), root);
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Encoding = new UnicodeEncoding(bigEndian: false, byteOrderMark: false),
            Indent = true,
            IndentChars = "  ",
            OmitXmlDeclaration = false,
        };
        using (var writer = XmlWriter.Create(sb, settings))
        {
            doc.Save(writer);
        }
        return sb.ToString();
    }

    /// <summary>
    /// Serializes a configuration to a file with the eVolve-default UTF-16
    /// (little-endian, with BOM) encoding. The BOM lets <see cref="DeserializeFromFile"/>
    /// and the .NET XML stack auto-detect the encoding on read.
    /// </summary>
    public static void SerializeToFile(RunScheduleConfiguration config, string path)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var xml = Serialize(config);
        File.WriteAllText(path, xml, new UnicodeEncoding(bigEndian: false, byteOrderMark: true));
    }

    /// <summary>
    /// Parses an eVolve-format XML string into a <see cref="RunScheduleConfiguration"/>.
    /// </summary>
    public static RunScheduleConfiguration Deserialize(string xml)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(xml);

        XDocument doc;
        try
        {
            doc = XDocument.Parse(xml);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("Document is not well-formed XML.", ex);
        }

        return DeserializeDocument(doc);
    }

    private static RunScheduleConfiguration DeserializeDocument(XDocument doc)
    {
        var root = doc.Root;
        if (root == null || root.Name.LocalName != RootElement)
            throw new InvalidDataException($"Expected root element '{RootElement}' but found '{root?.Name.LocalName ?? "<null>"}'.");

        var config = new RunScheduleConfiguration
        {
            SystemColorsEnabled = ReadBool(root, "SystemColorsEnabled", defaultValue: true),
            AutomaticallyPushRunInfo = ReadBool(root, "AutomaticallyPushRunInfo", defaultValue: true),
            MaximumWireFill = ReadDouble(root, "MaximumWireFill", defaultValue: 0.4),
            WireDescriptionFormat = ParseFormat(root.Element("WireDescriptionFormat")?.Value),
            StatusSetOnAssignRun = root.Element("StatusSetOnAssignRun")?.Value ?? "Not Used",
        };

        var colors = root.Element("SystemColors");
        if (colors != null)
        {
            foreach (var item in colors.Elements())
                config.SystemColors.Add(item.Value);
        }

        config.RunIdNotDefined = ReadHighlight(root, "RunIdNotDefined", "Red", enabledDefault: true);
        config.RunIdNotAssigned = ReadHighlight(root, "RunIdNotAssigned", "Orange", enabledDefault: true);
        config.SizeMismatch = ReadHighlight(root, "SizeMismatch", "Green", enabledDefault: true);
        config.StartMismatch = ReadHighlight(root, "StartMismatch", "Blue", enabledDefault: false);
        config.FinishMismatch = ReadHighlight(root, "FinishMismatch", "Purple", enabledDefault: false);
        config.TypeMismatch = ReadHighlight(root, "TypeMismatch", "Cyan", enabledDefault: false);
        config.SystemMismatch = ReadHighlight(root, "SystemMismatch", "Fuchsia", enabledDefault: false);

        var statuses = root.Element("RunStatuses");
        if (statuses != null)
        {
            foreach (var item in statuses.Elements())
                config.RunStatuses.Add(item.Value);
        }

        var specs = root.Element("WireSpecifications");
        if (specs != null)
        {
            foreach (var elem in specs.Elements("WireSpecificationData"))
                config.WireSpecifications.Add(ReadWireSpec(elem));
        }

        var sizes = root.Element("WireSizes");
        if (sizes != null)
        {
            foreach (var elem in sizes.Elements("WireSizeData"))
                config.WireSizes.Add(ReadWireSize(elem));
        }

        return config;
    }

    /// <summary>
    /// Reads an XML file and deserializes it. Uses an underlying stream so the
    /// XML stack can auto-detect the file's encoding via the BOM or the XML
    /// declaration (UTF-8 or UTF-16, with or without BOM).
    /// </summary>
    public static RunScheduleConfiguration DeserializeFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!File.Exists(path))
            throw new FileNotFoundException("Run-schedule XML file not found.", path);

        try
        {
            using var stream = File.OpenRead(path);
            var doc = XDocument.Load(stream);
            return DeserializeDocument(doc);
        }
        catch (XmlException ex)
        {
            throw new InvalidDataException("Document is not well-formed XML.", ex);
        }
    }

    // ── Element factories ────────────────────────────────────────────────

    private static XElement BoolElement(string name, bool value) =>
        new(name, value ? "true" : "false");

    private static XElement DoubleElement(string name, double value) =>
        new(name, value.ToString("R", CultureInfo.InvariantCulture));

    private static XElement HighlightElement(string name, RunScheduleHighlightRule rule) =>
        new(name,
            BoolElement("Enabled", rule.Enabled),
            new XElement("HighlightColorValue", rule.HighlightColorValue));

    private static XElement SystemColorsElement(RunScheduleConfiguration config)
    {
        var elem = new XElement("SystemColors");
        foreach (var c in config.SystemColors)
            elem.Add(new XElement("string", c));
        return elem;
    }

    private static XElement RunStatusesElement(RunScheduleConfiguration config)
    {
        var elem = new XElement("RunStatuses");
        foreach (var s in config.RunStatuses)
            elem.Add(new XElement("string", s));
        return elem;
    }

    private static XElement WireSpecElement(WireSpecification spec) =>
        new("WireSpecificationData",
            new XElement("Name", spec.Name),
            new XElement("MaterialName", spec.MaterialName),
            new XElement("FeederId", spec.FeederId),
            DoubleElement("Amperage", spec.Amperage),
            new XElement("PhaseSize", spec.PhaseSize),
            new XElement("PhaseQuantity", spec.PhaseQuantity),
            new XElement("NeutralSize", spec.NeutralSize),
            new XElement("NeutralQuantity", spec.NeutralQuantity),
            new XElement("GroundSize", spec.GroundSize),
            new XElement("GroundQuantity", spec.GroundQuantity),
            new XElement("IsoGroundSize", spec.IsoGroundSize),
            new XElement("IsoGroundQuantity", spec.IsoGroundQuantity),
            new XElement("ParallelQuantity", spec.ParallelQuantity),
            DoubleElement("ConduitSize", spec.ConduitSizeFeet));

    private static XElement WireSizeElement(WireSizeEntry size) =>
        new("WireSizeData",
            new XElement("MaterialName", size.MaterialName),
            new XElement("Insulation", size.Insulation),
            DoubleElement("Ampacity", size.Ampacity),
            new XElement("Gauge", size.Gauge),
            DoubleElement("Diameter", size.DiameterFeet));

    // ── Element readers ──────────────────────────────────────────────────

    private static bool ReadBool(XElement parent, string name, bool defaultValue)
    {
        var elem = parent.Element(name);
        if (elem == null || string.IsNullOrWhiteSpace(elem.Value)) return defaultValue;
        return bool.TryParse(elem.Value, out var b) ? b : defaultValue;
    }

    private static double ReadDouble(XElement parent, string name, double defaultValue)
    {
        var elem = parent.Element(name);
        if (elem == null || string.IsNullOrWhiteSpace(elem.Value)) return defaultValue;
        return double.TryParse(elem.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : defaultValue;
    }

    private static int ReadInt(XElement parent, string name, int defaultValue = 0)
    {
        var elem = parent.Element(name);
        if (elem == null || string.IsNullOrWhiteSpace(elem.Value)) return defaultValue;
        return int.TryParse(elem.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i)
            ? i
            : defaultValue;
    }

    private static string ReadString(XElement parent, string name) =>
        parent.Element(name)?.Value ?? string.Empty;

    private static RunScheduleHighlightRule ReadHighlight(
        XElement root, string name, string defaultColor, bool enabledDefault)
    {
        var elem = root.Element(name);
        if (elem == null)
        {
            return new RunScheduleHighlightRule { Enabled = enabledDefault, HighlightColorValue = defaultColor };
        }
        return new RunScheduleHighlightRule
        {
            Enabled = ReadBool(elem, "Enabled", enabledDefault),
            HighlightColorValue = elem.Element("HighlightColorValue")?.Value ?? defaultColor,
        };
    }

    private static WireSpecification ReadWireSpec(XElement elem) => new()
    {
        Name = ReadString(elem, "Name"),
        MaterialName = ReadString(elem, "MaterialName"),
        FeederId = ReadString(elem, "FeederId"),
        Amperage = ReadDouble(elem, "Amperage", 0),
        PhaseSize = ReadString(elem, "PhaseSize"),
        PhaseQuantity = ReadInt(elem, "PhaseQuantity"),
        NeutralSize = ReadString(elem, "NeutralSize"),
        NeutralQuantity = ReadInt(elem, "NeutralQuantity"),
        GroundSize = ReadString(elem, "GroundSize"),
        GroundQuantity = ReadInt(elem, "GroundQuantity"),
        IsoGroundSize = ReadString(elem, "IsoGroundSize"),
        IsoGroundQuantity = ReadInt(elem, "IsoGroundQuantity"),
        ParallelQuantity = ReadInt(elem, "ParallelQuantity", defaultValue: 1),
        ConduitSizeFeet = ReadDouble(elem, "ConduitSize", 0),
    };

    private static WireSizeEntry ReadWireSize(XElement elem) => new()
    {
        MaterialName = ReadString(elem, "MaterialName"),
        Insulation = ReadString(elem, "Insulation"),
        Ampacity = ReadDouble(elem, "Ampacity", 0),
        Gauge = ReadString(elem, "Gauge"),
        DiameterFeet = ReadDouble(elem, "Diameter", 0),
    };

    private static WireDescriptionFormat ParseFormat(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return WireDescriptionFormat.Hyphen;
        return Enum.TryParse<WireDescriptionFormat>(value, ignoreCase: true, out var f)
            ? f
            : WireDescriptionFormat.Hyphen;
    }
}
