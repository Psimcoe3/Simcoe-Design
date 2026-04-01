using System.Globalization;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

public sealed class RevisionHistoryService
{
    public RevisionEntry CreateRevisionEntry(
        DrawingSheet sheet,
        string description,
        string? author = null,
        string? revisionNumber = null,
        string? revisionDate = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var entry = new RevisionEntry
        {
            RevisionNumber = string.IsNullOrWhiteSpace(revisionNumber)
                ? GetNextRevisionNumber(sheet.RevisionEntries)
                : revisionNumber.Trim(),
            Date = string.IsNullOrWhiteSpace(revisionDate)
                ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : revisionDate.Trim(),
            Description = description?.Trim() ?? string.Empty,
            Author = string.IsNullOrWhiteSpace(author) ? Environment.UserName : author.Trim(),
            CreatedUtc = DateTime.UtcNow
        };

        NormalizeRevisionEntry(entry);
        return entry;
    }

    public void AddRevision(DrawingSheet sheet, RevisionEntry entry, bool insertAtTop = true)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(entry);

        NormalizeRevisionEntry(entry);
        if (sheet.RevisionEntries.Any(existing => string.Equals(existing.Id, entry.Id, StringComparison.Ordinal)))
            return;

        if (insertAtTop)
            sheet.RevisionEntries.Insert(0, entry);
        else
            sheet.RevisionEntries.Add(entry);
    }

    public bool RemoveRevision(DrawingSheet sheet, string revisionId)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var revision = sheet.RevisionEntries.FirstOrDefault(existing => string.Equals(existing.Id, revisionId, StringComparison.Ordinal));
        if (revision == null)
            return false;

        sheet.RevisionEntries.Remove(revision);
        return true;
    }

    public bool UpdateRevision(
        DrawingSheet sheet,
        string revisionId,
        string revisionNumber,
        string revisionDate,
        string description,
        string? author = null)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var revision = sheet.RevisionEntries.FirstOrDefault(existing => string.Equals(existing.Id, revisionId, StringComparison.Ordinal));
        if (revision == null)
            return false;

        revision.RevisionNumber = revisionNumber;
        revision.Date = revisionDate;
        revision.Description = description;
        revision.Author = string.IsNullOrWhiteSpace(author) ? Environment.UserName : author.Trim();
        NormalizeRevisionEntry(revision);
        return true;
    }

    public string GetNextRevisionNumber(IEnumerable<RevisionEntry>? revisions)
    {
        var revisionNumbers = revisions?
            .Select(revision => revision.RevisionNumber?.Trim())
            .Where(number => !string.IsNullOrWhiteSpace(number))
            .Cast<string>()
            .ToList() ?? new List<string>();

        if (revisionNumbers.Count == 0)
            return "1";

        if (TryGetNextNumericRevision(revisionNumbers, out var numericRevision))
            return numericRevision;

        if (TryGetNextAlphabeticRevision(revisionNumbers, out var alphabeticRevision))
            return alphabeticRevision;

        return (revisionNumbers.Count + 1).ToString(CultureInfo.InvariantCulture);
    }

    private static bool TryGetNextNumericRevision(IEnumerable<string> revisionNumbers, out string nextRevision)
    {
        var maxValue = 0;
        var hasValue = false;
        foreach (var revisionNumber in revisionNumbers)
        {
            if (!int.TryParse(revisionNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                nextRevision = string.Empty;
                return false;
            }

            hasValue = true;
            maxValue = Math.Max(maxValue, value);
        }

        nextRevision = hasValue
            ? (maxValue + 1).ToString(CultureInfo.InvariantCulture)
            : "1";
        return true;
    }

    private static bool TryGetNextAlphabeticRevision(IEnumerable<string> revisionNumbers, out string nextRevision)
    {
        var maxValue = 'A' - 1;
        var count = 0;

        foreach (var revisionNumber in revisionNumbers)
        {
            if (revisionNumber.Length != 1 || !char.IsLetter(revisionNumber[0]))
            {
                nextRevision = string.Empty;
                return false;
            }

            count++;
            maxValue = (char)Math.Max(maxValue, char.ToUpperInvariant(revisionNumber[0]));
        }

        if (count == 0)
        {
            nextRevision = "A";
            return true;
        }

        if (maxValue >= 'Z')
        {
            nextRevision = (count + 1).ToString(CultureInfo.InvariantCulture);
            return true;
        }

        nextRevision = ((char)(maxValue + 1)).ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static void NormalizeRevisionEntry(RevisionEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Id))
            entry.Id = Guid.NewGuid().ToString();

        entry.RevisionNumber = string.IsNullOrWhiteSpace(entry.RevisionNumber) ? "1" : entry.RevisionNumber.Trim();
        entry.Date = string.IsNullOrWhiteSpace(entry.Date)
            ? DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : entry.Date.Trim();
        entry.Description = entry.Description?.Trim() ?? string.Empty;
        entry.Author = entry.Author?.Trim() ?? string.Empty;
        if (entry.CreatedUtc == default)
            entry.CreatedUtc = DateTime.UtcNow;
    }
}