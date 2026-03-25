using System;
using System.Linq;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    // ── Callout ───────────────────────────────────────────────────────────────

    private void AddCallout_Click(object sender, RoutedEventArgs e)
    {
        var text = PromptInput("Add Callout", "Enter callout text:", "Note");
        if (string.IsNullOrWhiteSpace(text)) return;

        var posStr = PromptInput("Add Callout", "Position (X, Y in document units):", "100, 100");
        if (posStr == null) return;

        if (!TryParsePoint(posStr, out double px, out double py))
        {
            MessageBox.Show("Invalid position. Use format: X, Y", "Add Callout",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var markup = new MarkupRecord
        {
            Type = MarkupType.Callout,
            TextContent = text,
            Vertices = { new System.Windows.Point(px, py), new System.Windows.Point(px + 40, py - 30) },
            Appearance = { StrokeColor = "#FF0000", StrokeWidth = 1.5, FontSize = 11 },
            Metadata = { Author = Environment.UserName, Label = "Callout", Subject = text }
        };
        markup.UpdateBoundingRect();

        InsertGeneratedMarkups(
            new[] { markup },
            "Add callout",
            "Callout added",
            $"Text: {text}");
    }

    // ── Leader Note ──────────────────────────────────────────────────────────

    private void AddLeaderNote_Click(object sender, RoutedEventArgs e)
    {
        var text = PromptInput("Add Leader Note", "Enter note text:", "");
        if (string.IsNullOrWhiteSpace(text)) return;

        var startStr = PromptInput("Add Leader Note", "Leader start point (X, Y):", "50, 50");
        if (startStr == null) return;
        if (!TryParsePoint(startStr, out double sx, out double sy))
        {
            MessageBox.Show("Invalid position.", "Leader Note",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var endStr = PromptInput("Add Leader Note", "Text anchor point (X, Y):", $"{sx + 60}, {sy - 20}");
        if (endStr == null) return;
        if (!TryParsePoint(endStr, out double ex, out double ey))
        {
            MessageBox.Show("Invalid position.", "Leader Note",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var markup = new MarkupRecord
        {
            Type = MarkupType.LeaderNote,
            TextContent = text,
            Vertices = { new System.Windows.Point(sx, sy), new System.Windows.Point(ex, ey) },
            Appearance = { StrokeColor = "#0000FF", StrokeWidth = 1.0, FontSize = 10 },
            Metadata = { Author = Environment.UserName, Label = "Leader", Subject = text }
        };
        markup.UpdateBoundingRect();

        InsertGeneratedMarkups(
            new[] { markup },
            "Add leader note",
            "Leader note added",
            $"Text: {text}");
    }

    // ── Revision Cloud ───────────────────────────────────────────────────────

    private void AddRevisionCloud_Click(object sender, RoutedEventArgs e)
    {
        var rectStr = PromptInput("Add Revision Cloud",
            "Enter bounding rectangle (X, Y, Width, Height):", "50, 50, 200, 150");
        if (rectStr == null) return;

        var parts = rectStr.Split(',');
        if (parts.Length != 4 ||
            !double.TryParse(parts[0].Trim(), out double x) ||
            !double.TryParse(parts[1].Trim(), out double y) ||
            !double.TryParse(parts[2].Trim(), out double w) ||
            !double.TryParse(parts[3].Trim(), out double h))
        {
            MessageBox.Show("Invalid rectangle. Use format: X, Y, Width, Height",
                "Revision Cloud", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var label = PromptInput("Add Revision Cloud", "Revision label:", "Rev A");

        // Generate cloud path using RevisionCloudService
        var cloudService = new RevisionCloudService();
        var cloudPoints = cloudService.GenerateFromRect(new Rect(x, y, w, h));

        var markup = new MarkupRecord
        {
            Type = MarkupType.RevisionCloud,
            Vertices = cloudPoints,
            Appearance = { StrokeColor = "#FF0000", StrokeWidth = 2.0, FillColor = "#10FF0000" },
            Metadata = { Author = Environment.UserName, Label = label ?? "Rev", Subject = "Revision Cloud" }
        };
        markup.UpdateBoundingRect();

        InsertGeneratedMarkups(
            new[] { markup },
            "Add revision cloud",
            "Revision cloud added",
            $"Label: {label}, Rect: ({x},{y},{w},{h})");
    }

    // ── Stamp ────────────────────────────────────────────────────────────────

    private void AddStamp_Click(object sender, RoutedEventArgs e)
    {
        var stampTypes = new[] { "APPROVED", "REJECTED", "REVIEWED", "NOT FOR CONSTRUCTION", "PRELIMINARY", "FINAL", "VOID" };
        var list = string.Join("\n", stampTypes.Select((s, i) => $"{i + 1}. {s}"));
        var input = PromptInput("Add Stamp", $"Select stamp type:\n\n{list}\n\nOr enter custom text:", "1");
        if (string.IsNullOrWhiteSpace(input)) return;

        string stampText;
        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= stampTypes.Length)
            stampText = stampTypes[idx - 1];
        else
            stampText = input.Trim().ToUpperInvariant();

        var posStr = PromptInput("Add Stamp", "Position (X, Y):", "200, 200");
        if (posStr == null) return;
        if (!TryParsePoint(posStr, out double px, out double py))
        {
            MessageBox.Show("Invalid position.", "Add Stamp",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var markup = new MarkupRecord
        {
            Type = MarkupType.Stamp,
            TextContent = stampText,
            Vertices = { new System.Windows.Point(px, py) },
            BoundingRect = new Rect(px - 60, py - 15, 120, 30),
            Appearance =
            {
                StrokeColor = stampText is "APPROVED" or "FINAL" ? "#008000" : "#FF0000",
                StrokeWidth = 2.5,
                FontSize = 16,
                FillColor = "#20FF0000"
            },
            Metadata = { Author = Environment.UserName, Label = stampText, Subject = "Stamp" }
        };

        InsertGeneratedMarkups(
            new[] { markup },
            "Add stamp",
            "Stamp added",
            $"Text: {stampText}");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static bool TryParsePoint(string input, out double x, out double y)
    {
        x = y = 0;
        var parts = input.Split(',');
        return parts.Length >= 2 &&
               double.TryParse(parts[0].Trim(), out x) &&
               double.TryParse(parts[1].Trim(), out y);
    }
}
