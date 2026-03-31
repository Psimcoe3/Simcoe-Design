namespace ElectricalComponentSandbox.Models;

public sealed class RevisionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    public string RevisionNumber { get; set; } = "1";

    public string Date { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    public string Description { get; set; } = string.Empty;

    public string Author { get; set; } = string.Empty;

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public string DisplayText => BuildDisplayText();

    public string BuildDisplayText()
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(Date))
            parts.Add(Date.Trim());

        if (!string.IsNullOrWhiteSpace(Description))
            parts.Add(Description.Trim());

        var summary = string.Join(" - ", parts);
        if (string.IsNullOrWhiteSpace(Author))
            return summary;

        var authorText = Author.Trim();
        return string.IsNullOrWhiteSpace(summary)
            ? $"({authorText})"
            : $"{summary} ({authorText})";
    }

    public override string ToString() => DisplayText;
}