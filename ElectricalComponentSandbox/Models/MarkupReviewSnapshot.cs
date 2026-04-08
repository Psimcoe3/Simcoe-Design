using System.Globalization;
using ElectricalComponentSandbox.Markup.Models;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Models;

public enum MarkupReviewSnapshotScope
{
    CurrentSheet,
    AllSheets
}

public class MarkupReviewSnapshot
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string PublishedBy { get; set; } = string.Empty;
    public DateTime PublishedUtc { get; set; } = DateTime.UtcNow;
    public MarkupReviewSnapshotScope Scope { get; set; } = MarkupReviewSnapshotScope.CurrentSheet;
    public string SourceSheetId { get; set; } = string.Empty;
    public string SourceSheetDisplayName { get; set; } = string.Empty;
    public string FilterSummary { get; set; } = string.Empty;
    public List<MarkupRecord> Markups { get; set; } = new();

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name)
        ? $"Review Set {PublishedUtc.ToLocalTime():g}"
        : Name;

    [JsonIgnore]
    public int IssueCount => Markups.Count;

    [JsonIgnore]
    public int OpenCount => Markups.Count(markup => markup.Status is MarkupStatus.Open or MarkupStatus.InProgress);

    [JsonIgnore]
    public string ScopeDisplayText => Scope == MarkupReviewSnapshotScope.AllSheets
        ? "All Sheets"
        : $"Current Sheet ({(string.IsNullOrWhiteSpace(SourceSheetDisplayName) ? "(none)" : SourceSheetDisplayName)})";

    [JsonIgnore]
    public string PublishedDisplayText => PublishedUtc == default
        ? string.Empty
        : PublishedUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    [JsonIgnore]
    public string SecondaryText => string.IsNullOrWhiteSpace(PublishedBy)
        ? $"{ScopeDisplayText}  |  {PublishedDisplayText}"
        : $"{ScopeDisplayText}  |  {PublishedDisplayText}  |  {PublishedBy}";

    [JsonIgnore]
    public string BreakdownText => $"{IssueCount} issue(s)  |  {OpenCount} open/in progress";

    public MarkupReviewSnapshot Clone()
    {
        return new MarkupReviewSnapshot
        {
            Id = Id,
            Name = Name,
            PublishedBy = PublishedBy,
            PublishedUtc = PublishedUtc,
            Scope = Scope,
            SourceSheetId = SourceSheetId,
            SourceSheetDisplayName = SourceSheetDisplayName,
            FilterSummary = FilterSummary,
            Markups = Markups.Select(markup => markup.Clone()).ToList()
        };
    }
}