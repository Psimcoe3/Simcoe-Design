using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Xml;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Markup.Services;

namespace ElectricalComponentSandbox.Services.Export;

public enum XfdfImportMergeMode
{
    PreferImported,
    PreferExisting,
    AddAsNew
}

[Flags]
public enum XfdfMergeConflictKind
{
    None = 0,
    TypeMismatch = 1,
    GeometryMismatch = 2,
    ReviewStateMismatch = 4
}

public sealed class XfdfMergeConflict
{
    public required string MarkupId { get; init; }
    public required XfdfMergeConflictKind Kind { get; init; }
    public required string Summary { get; init; }
}

public sealed class XfdfImportMergeResult
{
    public int ImportedCount { get; init; }
    public int AddedCount { get; internal set; }
    public int UpdatedCount { get; internal set; }
    public int DuplicatedCount { get; internal set; }
    public int RepliesAddedCount { get; internal set; }
    public int ManualRepliesAddedCount { get; internal set; }
    public int AuditRepliesAddedCount { get; internal set; }
    public int StatusNotesAppliedCount { get; internal set; }
    public List<XfdfMergeConflict> Conflicts { get; } = new();
    public HashSet<string> ParticipantNames { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> AddedMarkupIds { get; } = new();
    public List<string> UpdatedMarkupIds { get; } = new();
    public List<string> DuplicatedMarkupIds { get; } = new();
    public int ConflictCount => Conflicts.Count;
    public int TypeConflictCount => CountConflicts(XfdfMergeConflictKind.TypeMismatch);
    public int GeometryConflictCount => CountConflicts(XfdfMergeConflictKind.GeometryMismatch);
    public int ReviewStateConflictCount => CountConflicts(XfdfMergeConflictKind.ReviewStateMismatch);

    private int CountConflicts(XfdfMergeConflictKind flag)
        => Conflicts.Count(conflict => conflict.Kind.HasFlag(flag));
}

internal readonly record struct ReplyMergeTelemetry(
    int AddedReplyCount,
    int AddedManualReplyCount,
    int AddedAuditReplyCount);

internal readonly record struct MarkupMergeTelemetry(
    int AddedReplyCount,
    int AddedManualReplyCount,
    int AddedAuditReplyCount,
    bool AppliedImportedStatusNote);

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
    /// Imports markup records from an XFDF file and merges them into the existing markup set.
    /// </summary>
    public XfdfImportMergeResult ImportAndMergeFromFile(string inputPath, IList<MarkupRecord> existingMarkups, XfdfImportMergeMode mode = XfdfImportMergeMode.PreferImported)
    {
        var xml = File.ReadAllText(inputPath, Encoding.UTF8);
        return ImportAndMerge(xml, existingMarkups, mode);
    }

    /// <summary>
    /// Previews an XFDF import/merge operation without mutating the current markup set.
    /// </summary>
    public XfdfImportMergeResult PreviewImportMergeFromFile(string inputPath, IEnumerable<MarkupRecord> existingMarkups)
    {
        var xml = File.ReadAllText(inputPath, Encoding.UTF8);
        return PreviewImportMerge(xml, existingMarkups);
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

    /// <summary>
    /// Imports markup records from XFDF and merges them into the supplied markup set.
    /// </summary>
    public XfdfImportMergeResult ImportAndMerge(string xfdf, IList<MarkupRecord> existingMarkups, XfdfImportMergeMode mode = XfdfImportMergeMode.PreferImported)
    {
        ArgumentNullException.ThrowIfNull(existingMarkups);

        var imported = Import(xfdf);
        var result = BuildMergePreview(imported, existingMarkups);

        foreach (var importedMarkup in imported)
        {
            var existingMarkup = existingMarkups.FirstOrDefault(markup => string.Equals(markup.Id, importedMarkup.Id, StringComparison.Ordinal));
            if (existingMarkup == null)
            {
                EnsureMarkupIds(importedMarkup);
                existingMarkups.Add(importedMarkup);
                result.AddedCount++;
                result.AddedMarkupIds.Add(importedMarkup.Id);
                result.RepliesAddedCount += importedMarkup.Replies.Count;
                result.ManualRepliesAddedCount += importedMarkup.Replies.Count(reply => !reply.IsAuditEntry);
                result.AuditRepliesAddedCount += importedMarkup.Replies.Count(reply => reply.IsAuditEntry);
                if (!string.IsNullOrWhiteSpace(importedMarkup.StatusNote))
                    result.StatusNotesAppliedCount++;
                continue;
            }

            switch (mode)
            {
                case XfdfImportMergeMode.AddAsNew:
                    EnsureMarkupIds(importedMarkup);
                    importedMarkup.Id = Guid.NewGuid().ToString();
                    existingMarkups.Add(importedMarkup);
                    result.AddedCount++;
                    result.DuplicatedCount++;
                    result.AddedMarkupIds.Add(importedMarkup.Id);
                    result.DuplicatedMarkupIds.Add(importedMarkup.Id);
                    result.RepliesAddedCount += importedMarkup.Replies.Count;
                    result.ManualRepliesAddedCount += importedMarkup.Replies.Count(reply => !reply.IsAuditEntry);
                    result.AuditRepliesAddedCount += importedMarkup.Replies.Count(reply => reply.IsAuditEntry);
                    if (!string.IsNullOrWhiteSpace(importedMarkup.StatusNote))
                        result.StatusNotesAppliedCount++;
                    break;
                case XfdfImportMergeMode.PreferExisting:
                    var preferExistingTelemetry = MergeMarkup(existingMarkup, importedMarkup, preferImported: false);
                    result.UpdatedCount++;
                    result.UpdatedMarkupIds.Add(existingMarkup.Id);
                    result.RepliesAddedCount += preferExistingTelemetry.AddedReplyCount;
                    result.ManualRepliesAddedCount += preferExistingTelemetry.AddedManualReplyCount;
                    result.AuditRepliesAddedCount += preferExistingTelemetry.AddedAuditReplyCount;
                    if (preferExistingTelemetry.AppliedImportedStatusNote)
                        result.StatusNotesAppliedCount++;
                    break;
                default:
                    var preferImportedTelemetry = MergeMarkup(existingMarkup, importedMarkup, preferImported: true);
                    result.UpdatedCount++;
                    result.UpdatedMarkupIds.Add(existingMarkup.Id);
                    result.RepliesAddedCount += preferImportedTelemetry.AddedReplyCount;
                    result.ManualRepliesAddedCount += preferImportedTelemetry.AddedManualReplyCount;
                    result.AuditRepliesAddedCount += preferImportedTelemetry.AddedAuditReplyCount;
                    if (preferImportedTelemetry.AppliedImportedStatusNote)
                        result.StatusNotesAppliedCount++;
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Previews an XFDF import/merge operation without mutating the current markup set.
    /// </summary>
    public XfdfImportMergeResult PreviewImportMerge(string xfdf, IEnumerable<MarkupRecord> existingMarkups)
    {
        ArgumentNullException.ThrowIfNull(existingMarkups);

        var imported = Import(xfdf);
        return BuildMergePreview(imported, existingMarkups);
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
        var r = GetExportRect(m);
        w.WriteAttributeString("rect",
            $"{F(r.Left)},{F(r.Top)},{F(r.Right)},{F(r.Bottom)}");

        // Status as custom attribute
        w.WriteAttributeString("customstatus", m.StatusDisplayText);
        if (!string.IsNullOrWhiteSpace(m.StatusNote))
            w.WriteAttributeString("statusnote", m.StatusNote);

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
                w.WriteAttributeString("parentReplyId", reply.ParentReplyId ?? string.Empty);
                w.WriteAttributeString("author", reply.Author);
                w.WriteAttributeString("kind", reply.EntryKindKey);
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
        record.StatusNote = string.IsNullOrWhiteSpace(el.GetAttribute("statusnote")) ? null : el.GetAttribute("statusnote");

        if (TryParseStatusDisplayText(el.GetAttribute("customstatus"), out var status))
            record.Status = status;

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
                if (TryCreateNormalizedRect(l, t, r2, b, out var rect))
                    record.BoundingRect = rect;
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
                    ParentReplyId = string.IsNullOrWhiteSpace(replyElement.GetAttribute("parentReplyId")) ? null : replyElement.GetAttribute("parentReplyId"),
                    Author = replyElement.GetAttribute("author") ?? string.Empty,
                    Kind = MarkupReply.ParseKind(replyElement.GetAttribute("kind")),
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

        if (record.BoundingRect == Rect.Empty)
            record.UpdateBoundingRect();

        return record;
    }

    private static MarkupMergeTelemetry MergeMarkup(MarkupRecord existing, MarkupRecord imported, bool preferImported)
    {
        EnsureMarkupIds(existing);
        EnsureMarkupIds(imported);

        var replyTelemetry = MergeReplies(existing.Replies, imported.Replies, preferImported, out var mergedReplies);
        var appliedImportedStatusNote = false;

        if (preferImported)
        {
            existing.Type = imported.Type;
            ReplacePoints(existing.Vertices, imported.Vertices);
            existing.BoundingRect = imported.BoundingRect;
            existing.Radius = imported.Radius;
            existing.ArcStartDeg = imported.ArcStartDeg;
            existing.ArcSweepDeg = imported.ArcSweepDeg;
            existing.RotationDegrees = imported.RotationDegrees;
            existing.TextContent = imported.TextContent;
            existing.HyperlinkUrl = imported.HyperlinkUrl;
            existing.Status = imported.Status;
            existing.StatusNote = imported.StatusNote;
            appliedImportedStatusNote = !string.IsNullOrWhiteSpace(imported.StatusNote);
            existing.AssignedTo = imported.AssignedTo;
            existing.LayerId = imported.LayerId;
            CopyAppearance(existing.Appearance, imported.Appearance);
            CopyMetadata(existing.Metadata, imported.Metadata);
            ReplaceStrings(existing.CutoutIds, imported.CutoutIds);
            existing.Replies = mergedReplies;
        }
        else
        {
            existing.Replies = mergedReplies;
            existing.Metadata.CreatedUtc = GetEarlierUtc(existing.Metadata.CreatedUtc, imported.Metadata.CreatedUtc);
            existing.Metadata.ModifiedUtc = GetLaterUtc(existing.Metadata.ModifiedUtc, imported.Metadata.ModifiedUtc);

            foreach (var pair in imported.Metadata.CustomFields)
            {
                if (!existing.Metadata.CustomFields.ContainsKey(pair.Key))
                    existing.Metadata.CustomFields[pair.Key] = pair.Value;
            }

            if (existing.BoundingRect == Rect.Empty)
            {
                existing.UpdateBoundingRect();
                if (existing.BoundingRect == Rect.Empty && imported.BoundingRect != Rect.Empty)
                    existing.BoundingRect = imported.BoundingRect;
            }

            return new MarkupMergeTelemetry(
                replyTelemetry.AddedReplyCount,
                replyTelemetry.AddedManualReplyCount,
                replyTelemetry.AddedAuditReplyCount,
                appliedImportedStatusNote: false);
        }

        existing.UpdateBoundingRect();
        if (existing.BoundingRect == Rect.Empty && imported.BoundingRect != Rect.Empty)
            existing.BoundingRect = imported.BoundingRect;

        return new MarkupMergeTelemetry(
            replyTelemetry.AddedReplyCount,
            replyTelemetry.AddedManualReplyCount,
            replyTelemetry.AddedAuditReplyCount,
            appliedImportedStatusNote);
    }

    private static ReplyMergeTelemetry MergeReplies(IReadOnlyList<MarkupReply> existingReplies, IReadOnlyList<MarkupReply> importedReplies, bool preferImported, out List<MarkupReply> mergedReplies)
    {
        var merged = new Dictionary<string, MarkupReply>(StringComparer.Ordinal);
        var existingReplyIds = new HashSet<string>(
            existingReplies.Where(reply => !string.IsNullOrWhiteSpace(reply.Id)).Select(reply => reply.Id),
            StringComparer.Ordinal);
        var addedReplyCount = 0;
        var addedManualReplyCount = 0;
        var addedAuditReplyCount = 0;

        foreach (var reply in existingReplies)
        {
            var clone = CloneReply(reply);
            merged[clone.Id] = clone;
        }

        foreach (var reply in importedReplies)
        {
            var clone = CloneReply(reply);
            if (!existingReplyIds.Contains(clone.Id))
            {
                addedReplyCount++;
                if (clone.IsAuditEntry)
                    addedAuditReplyCount++;
                else
                    addedManualReplyCount++;
            }

            if (!merged.TryGetValue(clone.Id, out var existing))
            {
                merged[clone.Id] = clone;
                continue;
            }

            if (preferImported || clone.ModifiedUtc >= existing.ModifiedUtc)
                merged[clone.Id] = clone;
        }

        mergedReplies = MarkupThreadingService.BuildThread(merged.Values.ToList())
            .Select(entry => entry.Reply)
            .ToList();

        return new ReplyMergeTelemetry(addedReplyCount, addedManualReplyCount, addedAuditReplyCount);
    }

    private static MarkupReply CloneReply(MarkupReply reply)
    {
        var id = string.IsNullOrWhiteSpace(reply.Id) ? Guid.NewGuid().ToString() : reply.Id;
        return new MarkupReply
        {
            Id = id,
            ParentReplyId = string.IsNullOrWhiteSpace(reply.ParentReplyId) ? null : reply.ParentReplyId,
            Author = reply.Author,
            Text = reply.Text,
            Kind = reply.Kind,
            CreatedUtc = reply.CreatedUtc,
            ModifiedUtc = reply.ModifiedUtc
        };
    }

    private static void EnsureMarkupIds(MarkupRecord markup)
    {
        if (string.IsNullOrWhiteSpace(markup.Id))
            markup.Id = Guid.NewGuid().ToString();

        foreach (var reply in markup.Replies)
        {
            if (string.IsNullOrWhiteSpace(reply.Id))
                reply.Id = Guid.NewGuid().ToString();
        }
    }

    private static void ReplacePoints(ICollection<Point> target, IEnumerable<Point> source)
    {
        target.Clear();
        foreach (var point in source)
            target.Add(point);
    }

    private static void ReplaceStrings(ICollection<string> target, IEnumerable<string> source)
    {
        target.Clear();
        foreach (var value in source)
            target.Add(value);
    }

    private static void CopyAppearance(MarkupAppearance target, MarkupAppearance source)
    {
        target.StrokeColor = source.StrokeColor;
        target.StrokeWidth = source.StrokeWidth;
        target.FillColor = source.FillColor;
        target.Opacity = source.Opacity;
        target.FontFamily = source.FontFamily;
        target.FontSize = source.FontSize;
        target.HatchPattern = source.HatchPattern;
        target.DashArray = source.DashArray;
    }

    private static void CopyMetadata(MarkupMetadata target, MarkupMetadata source)
    {
        target.Label = source.Label;
        target.Depth = source.Depth;
        target.Subject = source.Subject;
        target.Author = source.Author;
        target.CreatedUtc = source.CreatedUtc;
        target.ModifiedUtc = source.ModifiedUtc;
        target.CustomFields = source.CustomFields.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
    }

    private static DateTime GetEarlierUtc(DateTime first, DateTime second)
    {
        if (first == default)
            return second;

        if (second == default)
            return first;

        return first <= second ? first : second;
    }

    private static DateTime GetLaterUtc(DateTime first, DateTime second)
    {
        if (first == default)
            return second;

        if (second == default)
            return first;

        return first >= second ? first : second;
    }

    private static XfdfImportMergeResult BuildMergePreview(IReadOnlyList<MarkupRecord> imported, IEnumerable<MarkupRecord> existingMarkups)
    {
        var result = new XfdfImportMergeResult
        {
            ImportedCount = imported.Count
        };

        foreach (var importedMarkup in imported)
        {
            AccumulateImportedParticipants(result, importedMarkup);

            var existingMarkup = existingMarkups.FirstOrDefault(markup => string.Equals(markup.Id, importedMarkup.Id, StringComparison.Ordinal));
            if (existingMarkup == null)
                continue;

            var conflictKind = BuildConflictKind(existingMarkup, importedMarkup);
            if (conflictKind == XfdfMergeConflictKind.None)
                continue;

            result.Conflicts.Add(new XfdfMergeConflict
            {
                MarkupId = importedMarkup.Id,
                Kind = conflictKind,
                Summary = BuildConflictSummary(importedMarkup.Id, conflictKind)
            });
        }

        return result;
    }

    private static void AccumulateImportedParticipants(XfdfImportMergeResult result, MarkupRecord importedMarkup)
    {
        if (!string.IsNullOrWhiteSpace(importedMarkup.Metadata.Author))
            result.ParticipantNames.Add(importedMarkup.Metadata.Author);

        foreach (var reply in importedMarkup.Replies)
        {
            if (!string.IsNullOrWhiteSpace(reply.Author))
                result.ParticipantNames.Add(reply.Author);
        }
    }

    private static XfdfMergeConflictKind BuildConflictKind(MarkupRecord existing, MarkupRecord imported)
    {
        var kind = XfdfMergeConflictKind.None;

        if (existing.Type != imported.Type)
            kind |= XfdfMergeConflictKind.TypeMismatch;

        if (HasGeometryDifferences(existing, imported))
            kind |= XfdfMergeConflictKind.GeometryMismatch;

        if (HasReviewStateDifferences(existing, imported))
            kind |= XfdfMergeConflictKind.ReviewStateMismatch;

        return kind;
    }

    private static string BuildConflictSummary(string markupId, XfdfMergeConflictKind kind)
    {
        var labels = new List<string>();
        if (kind.HasFlag(XfdfMergeConflictKind.TypeMismatch))
            labels.Add("type");
        if (kind.HasFlag(XfdfMergeConflictKind.GeometryMismatch))
            labels.Add("geometry");
        if (kind.HasFlag(XfdfMergeConflictKind.ReviewStateMismatch))
            labels.Add("review state");

        return $"Markup '{markupId}' had differing {string.Join(", ", labels)} during XFDF merge.";
    }

    private static bool HasGeometryDifferences(MarkupRecord existing, MarkupRecord imported)
    {
        return existing.Type != imported.Type ||
               !ArePointsEquivalent(existing.Vertices, imported.Vertices) ||
               !AreRectsEquivalent(existing.BoundingRect, imported.BoundingRect) ||
               !AreEqual(existing.Radius, imported.Radius) ||
               !AreEqual(existing.ArcStartDeg, imported.ArcStartDeg) ||
               !AreEqual(existing.ArcSweepDeg, imported.ArcSweepDeg) ||
               !AreEqual(existing.RotationDegrees, imported.RotationDegrees) ||
               !string.Equals(existing.TextContent, imported.TextContent, StringComparison.Ordinal) ||
               !string.Equals(existing.HyperlinkUrl, imported.HyperlinkUrl, StringComparison.Ordinal) ||
               !string.Equals(existing.LayerId, imported.LayerId, StringComparison.Ordinal) ||
               !string.Equals(existing.Appearance.StrokeColor, imported.Appearance.StrokeColor, StringComparison.Ordinal) ||
               !AreEqual(existing.Appearance.StrokeWidth, imported.Appearance.StrokeWidth) ||
               !string.Equals(existing.Appearance.FillColor, imported.Appearance.FillColor, StringComparison.Ordinal) ||
               !AreEqual(existing.Appearance.Opacity, imported.Appearance.Opacity) ||
               !string.Equals(existing.Appearance.HatchPattern, imported.Appearance.HatchPattern, StringComparison.Ordinal) ||
               !string.Equals(existing.Appearance.DashArray, imported.Appearance.DashArray, StringComparison.Ordinal);
    }

    private static bool HasReviewStateDifferences(MarkupRecord existing, MarkupRecord imported)
    {
        return existing.Status != imported.Status ||
               !string.Equals(existing.StatusNote, imported.StatusNote, StringComparison.Ordinal) ||
               !string.Equals(existing.AssignedTo, imported.AssignedTo, StringComparison.Ordinal) ||
               !string.Equals(existing.Metadata.Author, imported.Metadata.Author, StringComparison.Ordinal) ||
               !string.Equals(existing.Metadata.Subject, imported.Metadata.Subject, StringComparison.Ordinal) ||
               !string.Equals(existing.Metadata.Label, imported.Metadata.Label, StringComparison.Ordinal);
    }

    private static bool ArePointsEquivalent(IReadOnlyList<Point> first, IReadOnlyList<Point> second)
    {
        if (first.Count != second.Count)
            return false;

        for (var index = 0; index < first.Count; index++)
        {
            if (!AreEqual(first[index].X, second[index].X) || !AreEqual(first[index].Y, second[index].Y))
                return false;
        }

        return true;
    }

    private static bool AreRectsEquivalent(Rect first, Rect second)
    {
        if (first == Rect.Empty && second == Rect.Empty)
            return true;

        return AreEqual(first.Left, second.Left) &&
               AreEqual(first.Top, second.Top) &&
               AreEqual(first.Width, second.Width) &&
               AreEqual(first.Height, second.Height);
    }

    private static bool AreEqual(double first, double second)
        => Math.Abs(first - second) < 0.0001;

    private static bool TryParseStatusDisplayText(string? statusText, out MarkupStatus status)
    {
        foreach (var candidate in Enum.GetValues<MarkupStatus>())
        {
            if (string.Equals(MarkupRecord.GetStatusDisplayText(candidate), statusText, StringComparison.OrdinalIgnoreCase))
            {
                status = candidate;
                return true;
            }
        }

        status = MarkupStatus.Open;
        return false;
    }

    private static Rect GetExportRect(MarkupRecord markup)
    {
        if (markup.BoundingRect != Rect.Empty)
            return markup.BoundingRect;

        if ((markup.Type == MarkupType.Circle || markup.Type == MarkupType.Arc) && markup.Vertices.Count >= 1)
        {
            var center = markup.Vertices[0];
            return new Rect(center.X - markup.Radius, center.Y - markup.Radius, markup.Radius * 2, markup.Radius * 2);
        }

        if (markup.Vertices.Count == 0)
            return new Rect(0, 0, 0, 0);

        double minX = double.MaxValue;
        double minY = double.MaxValue;
        double maxX = double.MinValue;
        double maxY = double.MinValue;

        foreach (var point in markup.Vertices)
        {
            if (point.X < minX)
                minX = point.X;
            if (point.Y < minY)
                minY = point.Y;
            if (point.X > maxX)
                maxX = point.X;
            if (point.Y > maxY)
                maxY = point.Y;
        }

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static bool TryCreateNormalizedRect(double left, double top, double right, double bottom, out Rect rect)
    {
        rect = Rect.Empty;
        if (!double.IsFinite(left) || !double.IsFinite(top) || !double.IsFinite(right) || !double.IsFinite(bottom))
            return false;

        var minX = Math.Min(left, right);
        var minY = Math.Min(top, bottom);
        var maxX = Math.Max(left, right);
        var maxY = Math.Max(top, bottom);
        rect = new Rect(minX, minY, maxX - minX, maxY - minY);
        return true;
    }

    private static string F(double v) =>
        v.ToString("0.####", CultureInfo.InvariantCulture);
}
