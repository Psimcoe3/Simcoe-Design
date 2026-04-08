using System.Globalization;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows;

namespace ElectricalComponentSandbox.Markup.Models;

/// <summary>
/// Enumerates the types of markup annotations
/// </summary>
public enum MarkupType
{
    // ── Basic drawing primitives ──────────────────────────────────────────────
    Polyline,
    Polygon,
    Rectangle,
    Circle,
    Arc,
    Text,
    // ── Electrical plan overlays ──────────────────────────────────────────────
    Box,
    Panel,
    ConduitRun,
    Dimension,
    // ── Extended annotation types (Bluebeam / Autodesk parity) ──────────────
    Callout,        // Note callout with leader line
    RevisionCloud,  // Revision cloud around changed area
    LeaderNote,     // Leader with text note
    Stamp,          // Approval / status stamp
    Hatch,          // Filled hatch region
    Measurement,    // Point-to-point measurement (read-only, not stored as dim)
    Hyperlink       // Clickable region linking to URL or page
}

/// <summary>
/// Lifecycle status for punch-list / review workflows (Bluebeam-style).
/// Covers full electrical construction review cycle: open → in-progress → resolved → approved/rejected/void.
/// </summary>
public enum MarkupStatus
{
    /// <summary>New markup, not yet assigned</summary>
    Open,
    /// <summary>Assigned and work has begun</summary>
    InProgress,
    /// <summary>Fix applied, pending verification</summary>
    Resolved,
    /// <summary>Verified and accepted by reviewer</summary>
    Approved,
    /// <summary>Reviewer returned the item for rework</summary>
    Rejected,
    /// <summary>Cancelled / no longer applicable</summary>
    Void
}

/// <summary>
/// Visual appearance settings for a markup
/// </summary>
public class MarkupAppearance
{
    public string StrokeColor { get; set; } = "#FF0000";
    public double StrokeWidth { get; set; } = 2.0;
    public string FillColor { get; set; } = "#40FF0000";
    public double Opacity { get; set; } = 1.0;
    public string FontFamily { get; set; } = "Arial";
    public double FontSize { get; set; } = 12.0;
    /// <summary>Hatch pattern for Hatch/Polygon fill.  Empty = solid fill.</summary>
    public string HatchPattern { get; set; } = string.Empty;
    /// <summary>Dash array for line types (CSV of lengths, e.g., &quot;5,3&quot;).  Empty = solid.</summary>
    public string DashArray { get; set; } = string.Empty;
}

/// <summary>
/// Metadata attached to a markup (label, depth, custom fields)
/// </summary>
public class MarkupMetadata
{
    public string Label { get; set; } = string.Empty;
    public double Depth { get; set; } = 0.0;
    public string Subject { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public Dictionary<string, string> CustomFields { get; set; } = new();
}

/// <summary>
/// A threaded review reply attached to a markup issue.
/// </summary>
public enum MarkupReplyKind
{
    Manual,
    Audit,
    StatusAudit,
    AssignmentAudit
}

public class MarkupReply
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? ParentReplyId { get; set; }
    public string Author { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public MarkupReplyKind Kind { get; set; } = MarkupReplyKind.Manual;
    public bool IsAuditEntry
    {
        get => Kind != MarkupReplyKind.Manual;
        set
        {
            if (!value)
            {
                Kind = MarkupReplyKind.Manual;
            }
            else if (Kind == MarkupReplyKind.Manual)
            {
                Kind = MarkupReplyKind.Audit;
            }
        }
    }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;
    public string EntryKindKey => GetKindKey(Kind);
    public string EntryTypeDisplayText => GetKindDisplayText(Kind);
    public string EntrySummaryDisplayText => GetKindSummaryDisplayText(Kind);
    public string CreatedDisplayText => FormatUtcForDisplay(CreatedUtc);
    public string ModifiedDisplayText => FormatUtcForDisplay(ModifiedUtc);

    public static string GetKindKey(MarkupReplyKind kind) => kind switch
    {
        MarkupReplyKind.Manual => "manual",
        MarkupReplyKind.StatusAudit => "status-audit",
        MarkupReplyKind.AssignmentAudit => "assignment-audit",
        _ => "audit"
    };

    public static string GetKindDisplayText(MarkupReplyKind kind) => kind switch
    {
        MarkupReplyKind.Manual => "Reply",
        MarkupReplyKind.StatusAudit => "Status",
        MarkupReplyKind.AssignmentAudit => "Assignment",
        _ => "Audit"
    };

    public static string GetKindSummaryDisplayText(MarkupReplyKind kind) => kind switch
    {
        MarkupReplyKind.Manual => "reply",
        MarkupReplyKind.StatusAudit => "status update",
        MarkupReplyKind.AssignmentAudit => "assignment update",
        _ => "audit update"
    };

    public static MarkupReplyKind ParseKind(string? serializedKind, bool legacyIsAuditEntry = false)
    {
        if (string.IsNullOrWhiteSpace(serializedKind))
            return legacyIsAuditEntry ? MarkupReplyKind.Audit : MarkupReplyKind.Manual;

        return serializedKind.Trim().ToLowerInvariant() switch
        {
            "manual" => MarkupReplyKind.Manual,
            "status-audit" => MarkupReplyKind.StatusAudit,
            "assignment-audit" => MarkupReplyKind.AssignmentAudit,
            "audit" => MarkupReplyKind.Audit,
            _ => legacyIsAuditEntry ? MarkupReplyKind.Audit : MarkupReplyKind.Manual
        };
    }

    internal static string FormatUtcForDisplay(DateTime utc)
    {
        if (utc == default)
            return string.Empty;

        return utc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    public MarkupReply Clone()
    {
        return new MarkupReply
        {
            Id = Id,
            ParentReplyId = ParentReplyId,
            Author = Author,
            Text = Text,
            Kind = Kind,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = ModifiedUtc
        };
    }
}

/// <summary>
/// A parametric markup record stored on the annotation layer.
/// Uses Document-space (PDF points) coordinates.
/// </summary>
public class MarkupRecord
{
    public static string GetTypeDisplayText(MarkupType type) => type switch
    {
        MarkupType.ConduitRun => "Conduit Run",
        MarkupType.RevisionCloud => "Revision Cloud",
        MarkupType.LeaderNote => "Leader Note",
        _ => type.ToString()
    };

    public static string GetStatusDisplayText(MarkupStatus status) => status switch
    {
        MarkupStatus.Open => "Open",
        MarkupStatus.InProgress => "In Progress",
        MarkupStatus.Resolved => "Resolved",
        MarkupStatus.Approved => "Approved",
        MarkupStatus.Rejected => "Rejected",
        MarkupStatus.Void => "Void",
        _ => status.ToString()
    };

    public string Id { get; set; } = Guid.NewGuid().ToString();
    public MarkupType Type { get; set; }

    /// <summary>
    /// Ordered vertices in Document coordinates (PDF points).
    /// For polyline/polygon: the vertex list.
    /// For rectangle: two corners (top-left, bottom-right).
    /// For circle/arc: one point (center).
    /// For text/stamp/callout: one point (anchor / leader-tail).
    /// For dimension/leader: [start, end, text-anchor].
    /// </summary>
    public List<Point> Vertices { get; set; } = new();

    /// <summary>Bounding rectangle in Document coordinates</summary>
    public Rect BoundingRect { get; set; } = Rect.Empty;

    /// <summary>Radius for circle / arc markups (Document units)</summary>
    public double Radius { get; set; }

    /// <summary>Start angle in degrees (for Arc markups)</summary>
    public double ArcStartDeg { get; set; }

    /// <summary>Sweep angle in degrees (for Arc markups, positive = counter-clockwise)</summary>
    public double ArcSweepDeg { get; set; } = 90.0;

    /// <summary>Rotation angle in degrees (for select/move/rotate)</summary>
    public double RotationDegrees { get; set; }

    /// <summary>Text content for Text / Callout / Stamp markups</summary>
    public string TextContent { get; set; } = string.Empty;

    /// <summary>
    /// Optional hyperlink URL.  Used directly by Hyperlink markups; also
    /// lets any annotation be clickable (e.g. a stamp that links to a spec sheet).
    /// </summary>
    public string? HyperlinkUrl { get; set; }

    // ── Punch-list / review workflow ──────────────────────────────────────────

    /// <summary>Review status for punch-list workflows</summary>
    public MarkupStatus Status { get; set; } = MarkupStatus.Open;

    /// <summary>
    /// Optional response / reply text (used in review workflows).
    /// e.g. "Fixed in Rev B"
    /// </summary>
    public string? StatusNote { get; set; }

    /// <summary>
    /// Discussion replies attached to this markup issue.
    /// </summary>
    public List<MarkupReply> Replies { get; set; } = new();

    /// <summary>
    /// Current assignee for the markup issue.
    /// </summary>
    public string? AssignedTo { get; set; }

    // ── Layer + style ─────────────────────────────────────────────────────────

    /// <summary>Layer this markup belongs to</summary>
    public string LayerId { get; set; } = "markup-default";

    public MarkupAppearance Appearance { get; set; } = new();
    public MarkupMetadata Metadata { get; set; } = new();
    public string TypeDisplayText => GetTypeDisplayText(Type);
    public string StatusDisplayText => GetStatusDisplayText(Status);
    public string LayerDisplayText => LayerId;
    public string AssignedToDisplayText => string.IsNullOrWhiteSpace(AssignedTo) ? "(unassigned)" : AssignedTo;
    public string CreatedDisplayText => FormatUtcForDisplay(Metadata.CreatedUtc);
    public string ModifiedDisplayText => FormatUtcForDisplay(Metadata.ModifiedUtc);
    public int ReplyCount => Replies.Count;
    public string ReplyCountDisplayText => ReplyCount.ToString(CultureInfo.CurrentCulture);
    [JsonIgnore]
    public string ReviewSheetDisplayText { get; set; } = string.Empty;

    /// <summary>For cutout calculations: IDs of inner polygon markups subtracted from this polygon</summary>
    public List<string> CutoutIds { get; set; } = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Recalculates the bounding rect from current vertices and radius</summary>
    public void UpdateBoundingRect()
    {
        if ((Type == MarkupType.Circle || Type == MarkupType.Arc) && Vertices.Count >= 1)
        {
            var c = Vertices[0];
            BoundingRect = new Rect(c.X - Radius, c.Y - Radius, Radius * 2, Radius * 2);
            return;
        }

        if (Vertices.Count == 0)
        {
            BoundingRect = Rect.Empty;
            return;
        }

        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;

        foreach (var v in Vertices)
        {
            if (v.X < minX) minX = v.X;
            if (v.Y < minY) minY = v.Y;
            if (v.X > maxX) maxX = v.X;
            if (v.Y > maxY) maxY = v.Y;
        }

        BoundingRect = new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    private static string FormatUtcForDisplay(DateTime utc)
    {
        return MarkupReply.FormatUtcForDisplay(utc);
    }

    public MarkupRecord Clone()
    {
        return new MarkupRecord
        {
            Id = Id,
            Type = Type,
            Vertices = Vertices.ToList(),
            BoundingRect = BoundingRect,
            Radius = Radius,
            ArcStartDeg = ArcStartDeg,
            ArcSweepDeg = ArcSweepDeg,
            RotationDegrees = RotationDegrees,
            TextContent = TextContent,
            HyperlinkUrl = HyperlinkUrl,
            Status = Status,
            StatusNote = StatusNote,
            Replies = Replies.Select(reply => reply.Clone()).ToList(),
            AssignedTo = AssignedTo,
            LayerId = LayerId,
            Appearance = new MarkupAppearance
            {
                StrokeColor = Appearance.StrokeColor,
                StrokeWidth = Appearance.StrokeWidth,
                FillColor = Appearance.FillColor,
                Opacity = Appearance.Opacity,
                FontFamily = Appearance.FontFamily,
                FontSize = Appearance.FontSize,
                HatchPattern = Appearance.HatchPattern,
                DashArray = Appearance.DashArray
            },
            Metadata = new MarkupMetadata
            {
                Label = Metadata.Label,
                Depth = Metadata.Depth,
                Subject = Metadata.Subject,
                Author = Metadata.Author,
                CreatedUtc = Metadata.CreatedUtc,
                ModifiedUtc = Metadata.ModifiedUtc,
                CustomFields = Metadata.CustomFields.ToDictionary(entry => entry.Key, entry => entry.Value, StringComparer.Ordinal)
            },
            ReviewSheetDisplayText = ReviewSheetDisplayText,
            CutoutIds = CutoutIds.ToList()
        };
    }
}
