using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml;
using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Services.Export;

/// <summary>
/// Exports and imports markup annotations in XFDF (XML Forms Data Format) for
/// interoperability with Bluebeam Revu, Adobe Acrobat, and other PDF annotation tools.
///
/// XFDF spec: ISO 19444-1 / Adobe XFDF 3.0
///
/// Markup types are mapped to standard PDF annotation types:
///   Polyline → Ink, Polygon → Polygon, Rectangle → Square,
///   Circle → Circle, Text → FreeText, Callout → FreeText+Callout,
///   Dimension → Line (with measurement dict), RevisionCloud → Polygon (cloud style).
/// </summary>
public class XfdfExportService
{
    /// <summary>Source PDF filename (written into XFDF &lt;f&gt; element)</summary>
    public string SourcePdfFilename { get; set; } = "drawing.pdf";

    /// <summary>Page index (0-based) for all markups</summary>
    public int PageIndex { get; set; } = 0;

    // ── Export ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exports markup records to an XFDF string.
    /// </summary>
    public string Export(IEnumerable<MarkupRecord> markups)
    {
        var sb = new StringBuilder();
        using var writer = XmlWriter.Create(sb, new XmlWriterSettings
        {
            Indent = true,
            Encoding = Encoding.UTF8,
            OmitXmlDeclaration = false
        });

        writer.WriteStartDocument();
        writer.WriteStartElement("xfdf", "http://ns.adobe.com/xfdf/");
        writer.WriteAttributeString("xml", "space", null, "preserve");

        // Source PDF reference
        writer.WriteStartElement("f");
        writer.WriteAttributeString("href", SourcePdfFilename);
        writer.WriteEndElement();

        writer.WriteStartElement("annots");

        foreach (var markup in markups)
            WriteAnnotation(writer, markup);

        writer.WriteEndElement(); // annots
        writer.WriteEndElement(); // xfdf
        writer.WriteEndDocument();
        writer.Flush();

        return sb.ToString();
    }

    /// <summary>
    /// Exports markup records to an XFDF file.
    /// </summary>
    public void ExportToFile(IEnumerable<MarkupRecord> markups, string outputPath)
    {
        var xfdf = Export(markups);
        File.WriteAllText(outputPath, xfdf, Encoding.UTF8);
    }

    // ── Import ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Imports markup records from an XFDF file.
    /// </summary>
    public List<MarkupRecord> ImportFromFile(string inputPath)
    {
        var xml = File.ReadAllText(inputPath, Encoding.UTF8);
        return Import(xml);
    }

    /// <summary>
    /// Imports markup records from an XFDF string.
    /// </summary>
    public List<MarkupRecord> Import(string xfdf)
    {
        var result = new List<MarkupRecord>();
        var doc = new XmlDocument();
        doc.LoadXml(xfdf);

        var ns = new XmlNamespaceManager(doc.NameTable);
        ns.AddNamespace("x", "http://ns.adobe.com/xfdf/");

        var annots = doc.SelectSingleNode("//x:annots", ns);
        if (annots == null) return result;

        foreach (XmlNode node in annots.ChildNodes)
        {
            if (node is not XmlElement element) continue;
            var record = ReadAnnotation(element);
            if (record != null)
                result.Add(record);
        }

        return result;
    }

    // ── Write individual annotation types ────────────────────────────────────

    private void WriteAnnotation(XmlWriter w, MarkupRecord m)
    {
        string elementName = m.Type switch
        {
            MarkupType.Rectangle => "square",
            MarkupType.Circle => "circle",
            MarkupType.Text or MarkupType.Callout or MarkupType.LeaderNote => "freetext",
            MarkupType.Polyline or MarkupType.ConduitRun => "ink",
            MarkupType.Polygon or MarkupType.RevisionCloud or MarkupType.Hatch => "polygon",
            MarkupType.Dimension or MarkupType.Measurement => "line",
            MarkupType.Stamp => "stamp",
            MarkupType.Arc => "ink",
            _ => "ink"
        };

        w.WriteStartElement(elementName);

        // Common attributes
        w.WriteAttributeString("page", PageIndex.ToString());
        w.WriteAttributeString("name", m.Id);
        w.WriteAttributeString("title", m.Metadata.Author);
        w.WriteAttributeString("subject", m.Metadata.Subject);
        w.WriteAttributeString("date", m.Metadata.ModifiedUtc.ToString("D:yyyyMMddHHmmss+00'00'"));
        w.WriteAttributeString("creationdate", m.Metadata.CreatedUtc.ToString("D:yyyyMMddHHmmss+00'00'"));
        w.WriteAttributeString("color", m.Appearance.StrokeColor);
        w.WriteAttributeString("width", F(m.Appearance.StrokeWidth));
        w.WriteAttributeString("opacity", F(m.Appearance.Opacity));

        // Rect attribute (bounding box)
        var r = m.BoundingRect;
        w.WriteAttributeString("rect",
            $"{F(r.Left)},{F(r.Top)},{F(r.Right)},{F(r.Bottom)}");

        // Status as custom attribute
        w.WriteAttributeString("customstatus", m.StatusDisplayText);

        if (!string.IsNullOrWhiteSpace(m.AssignedTo))
            w.WriteAttributeString("assignedto", m.AssignedTo);

        // Contents (label / text)
        if (!string.IsNullOrEmpty(m.Metadata.Label) || !string.IsNullOrEmpty(m.TextContent))
        {
            w.WriteStartElement("contents-richtext");
            w.WriteCData(m.TextContent ?? m.Metadata.Label);
            w.WriteEndElement();

            w.WriteElementString("contents", m.TextContent ?? m.Metadata.Label);
        }

        if (m.Replies.Count > 0)
        {
            w.WriteStartElement("replies");
            foreach (var reply in m.Replies)
            {
                w.WriteStartElement("reply");
                w.WriteAttributeString("id", reply.Id);
                w.WriteAttributeString("author", reply.Author);
                w.WriteAttributeString("created", reply.CreatedUtc.ToString("O"));
                w.WriteAttributeString("modified", reply.ModifiedUtc.ToString("O"));
                w.WriteCData(reply.Text);
                w.WriteEndElement();
            }

            w.WriteEndElement();
        }

        // Vertices for polyline/polygon/line
        if (m.Vertices.Count > 0 && (elementName is "ink" or "polygon" or "line"))
        {
            if (elementName == "ink")
            {
                w.WriteStartElement("inklist");
                w.WriteStartElement("gesture");
                w.WriteString(string.Join(";",
                    m.Vertices.Select(v => $"{F(v.X)},{F(v.Y)}")));
                w.WriteEndElement(); // gesture
                w.WriteEndElement(); // inklist
            }
            else if (elementName == "polygon")
            {
                w.WriteAttributeString("vertices",
                    string.Join(",", m.Vertices.SelectMany(v => new[] { F(v.X), F(v.Y) })));
            }
            else if (elementName == "line" && m.Vertices.Count >= 2)
            {
                var start = m.Vertices[0];
                var end = m.Vertices[1];
                w.WriteAttributeString("start", $"{F(start.X)},{F(start.Y)}");
                w.WriteAttributeString("end", $"{F(end.X)},{F(end.Y)}");
            }
        }

        // Popup for status note
        if (!string.IsNullOrEmpty(m.StatusNote))
        {
            w.WriteStartElement("popup");
            w.WriteAttributeString("open", "no");
            w.WriteEndElement();
        }

        w.WriteEndElement(); // annotation element
    }

    // ── Read individual annotation ───────────────────────────────────────────

    private static MarkupRecord? ReadAnnotation(XmlElement el)
    {
        var type = el.LocalName.ToLowerInvariant() switch
        {
            "square" => MarkupType.Rectangle,
            "circle" => MarkupType.Circle,
            "freetext" => MarkupType.Text,
            "ink" => MarkupType.Polyline,
            "polygon" => MarkupType.Polygon,
            "line" => MarkupType.Dimension,
            "stamp" => MarkupType.Stamp,
            _ => (MarkupType?)null
        };

        if (type == null) return null;

        var record = new MarkupRecord
        {
            Type = type.Value,
            Id = el.GetAttribute("name"),
        };

        if (string.IsNullOrEmpty(record.Id))
            record.Id = Guid.NewGuid().ToString();

        record.Metadata.Author = el.GetAttribute("title") ?? string.Empty;
        record.Metadata.Subject = el.GetAttribute("subject") ?? string.Empty;
        record.AssignedTo = el.GetAttribute("assignedto");
        record.Appearance.StrokeColor = el.GetAttribute("color") ?? "#FF0000";

        if (double.TryParse(el.GetAttribute("width"), NumberStyles.Float, CultureInfo.InvariantCulture, out double w))
            record.Appearance.StrokeWidth = w;

        if (double.TryParse(el.GetAttribute("opacity"), NumberStyles.Float, CultureInfo.InvariantCulture, out double op))
            record.Appearance.Opacity = op;

        // Parse rect
        var rectStr = el.GetAttribute("rect");
        if (!string.IsNullOrEmpty(rectStr))
        {
            var parts = rectStr.Split(',');
            if (parts.Length == 4 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double l) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double t) &&
                double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double r2) &&
                double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double b))
            {
                record.BoundingRect = new Rect(l, t, r2 - l, b - t);
            }
        }

        // Parse vertices
        var verticesStr = el.GetAttribute("vertices");
        if (!string.IsNullOrEmpty(verticesStr))
        {
            var nums = verticesStr.Split(',');
            for (int i = 0; i + 1 < nums.Length; i += 2)
            {
                if (double.TryParse(nums[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double x) &&
                    double.TryParse(nums[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out double y))
                {
                    record.Vertices.Add(new Point(x, y));
                }
            }
        }

        // Parse ink gesture
        var gesture = el.SelectSingleNode("inklist/gesture") ?? el.SelectSingleNode("*[local-name()='inklist']/*[local-name()='gesture']");
        if (gesture != null && !string.IsNullOrEmpty(gesture.InnerText))
        {
            foreach (var ptStr in gesture.InnerText.Split(';'))
            {
                var coords = ptStr.Split(',');
                if (coords.Length == 2 &&
                    double.TryParse(coords[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double gx) &&
                    double.TryParse(coords[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double gy))
                {
                    record.Vertices.Add(new Point(gx, gy));
                }
            }
        }

        // Parse line start/end
        var startStr = el.GetAttribute("start");
        var endStr = el.GetAttribute("end");
        if (!string.IsNullOrEmpty(startStr) && !string.IsNullOrEmpty(endStr))
        {
            var sp = startStr.Split(',');
            var ep = endStr.Split(',');
            if (sp.Length == 2 && ep.Length == 2 &&
                double.TryParse(sp[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double sx) &&
                double.TryParse(sp[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double sy) &&
                double.TryParse(ep[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double ex) &&
                double.TryParse(ep[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double ey))
            {
                record.Vertices.Add(new Point(sx, sy));
                record.Vertices.Add(new Point(ex, ey));
            }
        }

        // Parse contents
        var contents = el.SelectSingleNode("contents") ?? el.SelectSingleNode("*[local-name()='contents']");
        if (contents != null)
            record.TextContent = contents.InnerText;

        var replies = el.SelectNodes("./*[local-name()='replies']/*[local-name()='reply']");
        if (replies != null)
        {
            foreach (XmlNode replyNode in replies)
            {
                if (replyNode is not XmlElement replyElement)
                    continue;

                var reply = new MarkupReply
                {
                    Id = replyElement.GetAttribute("id"),
                    Author = replyElement.GetAttribute("author") ?? string.Empty,
                    Text = replyElement.InnerText ?? string.Empty
                };

                if (string.IsNullOrEmpty(reply.Id))
                    reply.Id = Guid.NewGuid().ToString();

                if (DateTime.TryParse(replyElement.GetAttribute("created"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var createdUtc))
                    reply.CreatedUtc = createdUtc;

                if (DateTime.TryParse(replyElement.GetAttribute("modified"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var modifiedUtc))
                    reply.ModifiedUtc = modifiedUtc;

                record.Replies.Add(reply);
            }
        }

        record.UpdateBoundingRect();
        return record;
    }

    private static string F(double v) =>
        v.ToString("0.####", CultureInfo.InvariantCulture);
}
