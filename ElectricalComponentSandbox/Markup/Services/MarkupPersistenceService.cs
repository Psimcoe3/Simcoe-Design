using System.IO;
using System.Text;
using System.Windows;
using System.Xml.Linq;
using ElectricalComponentSandbox.Markup.Models;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Markup.Services;

/// <summary>
/// Serializes/deserializes markup records to JSON and optional XFDF-like XML
/// </summary>
public class MarkupPersistenceService
{
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        Formatting = Formatting.Indented,
        NullValueHandling = NullValueHandling.Ignore
    };

    // ────────── JSON ──────────

    /// <summary>
    /// Serializes a list of markups to a JSON string
    /// </summary>
    public string SerializeToJson(IEnumerable<MarkupRecord> markups)
    {
        var dtos = markups.Select(MarkupDto.FromRecord).ToList();
        return JsonConvert.SerializeObject(dtos, JsonSettings);
    }

    /// <summary>
    /// Deserializes a JSON string to a list of markup records
    /// </summary>
    public List<MarkupRecord> DeserializeFromJson(string json)
    {
        var dtos = JsonConvert.DeserializeObject<List<MarkupDto>>(json);
        return dtos?.Select(d => d.ToRecord()).ToList() ?? new List<MarkupRecord>();
    }

    /// <summary>
    /// Saves markups to a JSON file
    /// </summary>
    public async Task SaveJsonAsync(IEnumerable<MarkupRecord> markups, string filePath)
    {
        var json = SerializeToJson(markups);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// Loads markups from a JSON file
    /// </summary>
    public async Task<List<MarkupRecord>> LoadJsonAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return DeserializeFromJson(json);
    }

    // ────────── XFDF-like XML ──────────

    /// <summary>
    /// Serializes markups to an XFDF-like XML string
    /// </summary>
    public string SerializeToXml(IEnumerable<MarkupRecord> markups)
    {
        var root = new XElement("markups");

        foreach (var m in markups)
        {
            var el = new XElement("markup",
                new XAttribute("id", m.Id),
                new XAttribute("type", m.Type.ToString()),
                new XAttribute("layer", m.LayerId));

            if (!string.IsNullOrEmpty(m.TextContent))
                el.Add(new XElement("text", m.TextContent));

            if (m.Radius > 0)
                el.Add(new XElement("radius", m.Radius));

            if (Math.Abs(m.RotationDegrees) > 1e-6)
                el.Add(new XElement("rotation", m.RotationDegrees));

            var verticesEl = new XElement("vertices");
            foreach (var v in m.Vertices)
            {
                verticesEl.Add(new XElement("point",
                    new XAttribute("x", v.X),
                    new XAttribute("y", v.Y)));
            }
            el.Add(verticesEl);

            el.Add(new XElement("appearance",
                new XAttribute("stroke", m.Appearance.StrokeColor),
                new XAttribute("strokeWidth", m.Appearance.StrokeWidth),
                new XAttribute("fill", m.Appearance.FillColor),
                new XAttribute("opacity", m.Appearance.Opacity)));

            el.Add(new XElement("metadata",
                new XAttribute("label", m.Metadata.Label),
                new XAttribute("depth", m.Metadata.Depth),
                new XAttribute("subject", m.Metadata.Subject),
                new XAttribute("author", m.Metadata.Author)));

            root.Add(el);
        }

        return root.ToString();
    }

    /// <summary>
    /// Deserializes markups from an XFDF-like XML string
    /// </summary>
    public List<MarkupRecord> DeserializeFromXml(string xml)
    {
        var results = new List<MarkupRecord>();
        var root = XElement.Parse(xml);

        foreach (var el in root.Elements("markup"))
        {
            var record = new MarkupRecord
            {
                Id = (string?)el.Attribute("id") ?? Guid.NewGuid().ToString(),
                Type = Enum.TryParse<MarkupType>((string?)el.Attribute("type"), out var t) ? t : MarkupType.Polyline,
                LayerId = (string?)el.Attribute("layer") ?? "markup-default",
                TextContent = (string?)el.Element("text") ?? string.Empty,
                Radius = (double?)el.Element("radius") ?? 0,
                RotationDegrees = (double?)el.Element("rotation") ?? 0
            };

            var verticesEl = el.Element("vertices");
            if (verticesEl != null)
            {
                foreach (var pt in verticesEl.Elements("point"))
                {
                    double x = (double?)pt.Attribute("x") ?? 0;
                    double y = (double?)pt.Attribute("y") ?? 0;
                    record.Vertices.Add(new Point(x, y));
                }
            }

            var appearance = el.Element("appearance");
            if (appearance != null)
            {
                record.Appearance.StrokeColor = (string?)appearance.Attribute("stroke") ?? "#FF0000";
                record.Appearance.StrokeWidth = (double?)appearance.Attribute("strokeWidth") ?? 2.0;
                record.Appearance.FillColor = (string?)appearance.Attribute("fill") ?? "#40FF0000";
                record.Appearance.Opacity = (double?)appearance.Attribute("opacity") ?? 1.0;
            }

            var metadata = el.Element("metadata");
            if (metadata != null)
            {
                record.Metadata.Label = (string?)metadata.Attribute("label") ?? string.Empty;
                record.Metadata.Depth = (double?)metadata.Attribute("depth") ?? 0;
                record.Metadata.Subject = (string?)metadata.Attribute("subject") ?? string.Empty;
                record.Metadata.Author = (string?)metadata.Attribute("author") ?? string.Empty;
            }

            record.UpdateBoundingRect();
            results.Add(record);
        }

        return results;
    }
}

/// <summary>
/// DTO for JSON serialization of MarkupRecord (WPF Point/Rect are not directly serializable)
/// </summary>
internal class MarkupDto
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<double[]> Vertices { get; set; } = new();
    public double Radius { get; set; }
    public double RotationDegrees { get; set; }
    public string TextContent { get; set; } = string.Empty;
    public string LayerId { get; set; } = string.Empty;
    public MarkupAppearance? Appearance { get; set; }
    public MarkupMetadata? Metadata { get; set; }
    public List<string> CutoutIds { get; set; } = new();

    public static MarkupDto FromRecord(MarkupRecord r) => new()
    {
        Id = r.Id,
        Type = r.Type.ToString(),
        Vertices = r.Vertices.Select(v => new[] { v.X, v.Y }).ToList(),
        Radius = r.Radius,
        RotationDegrees = r.RotationDegrees,
        TextContent = r.TextContent,
        LayerId = r.LayerId,
        Appearance = r.Appearance,
        Metadata = r.Metadata,
        CutoutIds = r.CutoutIds
    };

    public MarkupRecord ToRecord()
    {
        var r = new MarkupRecord
        {
            Id = Id,
            Type = Enum.TryParse<MarkupType>(Type, out var t) ? t : MarkupType.Polyline,
            Radius = Radius,
            RotationDegrees = RotationDegrees,
            TextContent = TextContent,
            LayerId = LayerId,
            Appearance = Appearance ?? new MarkupAppearance(),
            Metadata = Metadata ?? new MarkupMetadata(),
            CutoutIds = CutoutIds ?? new List<string>()
        };

        foreach (var v in Vertices)
        {
            if (v.Length >= 2)
                r.Vertices.Add(new Point(v[0], v[1]));
        }

        r.UpdateBoundingRect();
        return r;
    }
}
