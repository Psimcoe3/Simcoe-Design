using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ElectricalComponentSandbox.Models;

using ElectricalComponentSandbox.Markup.Models;

/// <summary>
/// Represents a persisted drawing sheet with its own document and review state.
/// Components remain project-global for now; sheet-scoped state covers the
/// underlay, markups, saved views, and page setup.
/// </summary>
public enum DrawingSheetStatus
{
    Draft,
    InReview,
    Approved,
    Archived
}

public class DrawingSheet : INotifyPropertyChanged
{
    private string _number = "S001";
    private string _name = "Sheet 1";

    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string Number
    {
        get => _number;
        set
        {
            _number = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayName));
        }
    }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = Environment.UserName;

    public DateTime ModifiedUtc { get; set; } = DateTime.UtcNow;

    public string ModifiedBy { get; set; } = Environment.UserName;

    public DrawingSheetStatus Status { get; set; } = DrawingSheetStatus.Draft;

    public DateTime? ApprovedUtc { get; set; }

    public string? ApprovedBy { get; set; }

    public PdfUnderlay? PdfUnderlay { get; set; }

    public List<MarkupRecord> Markups { get; set; } = new();

    public List<LiveScheduleInstance> LiveSchedules { get; set; } = new();

    public List<LiveTitleBlockInstance> LiveTitleBlocks { get; set; } = new();

    public List<RevisionEntry> RevisionEntries { get; set; } = new();

    public List<NamedView> NamedViews { get; set; } = new();

    public PlotLayout? PlotLayout { get; set; }

    public string DisplayName => string.IsNullOrWhiteSpace(Number)
        ? Name
        : $"{Number} - {Name}";

    public static DrawingSheet CreateDefault(int index)
    {
        var clampedIndex = Math.Max(1, index);
        return new DrawingSheet
        {
            Number = $"S{clampedIndex:000}",
            Name = $"Sheet {clampedIndex}"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}