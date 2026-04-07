using ElectricalComponentSandbox.Markup.Models;

namespace ElectricalComponentSandbox.Markup.Services;

public readonly record struct MarkupThreadEntry(MarkupReply Reply, int Depth)
{
    public bool IsThreadRoot => Depth == 0;
}

public static class MarkupThreadingService
{
    public static IReadOnlyList<MarkupThreadEntry> BuildThread(IReadOnlyList<MarkupReply>? replies)
    {
        if (replies == null || replies.Count == 0)
            return Array.Empty<MarkupThreadEntry>();

        var orderedReplies = replies
            .OrderBy(reply => reply.CreatedUtc)
            .ThenBy(reply => reply.ModifiedUtc)
            .ThenBy(reply => reply.Id, StringComparer.Ordinal)
            .ToList();

        var knownIds = orderedReplies
            .Select(reply => reply.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        var roots = new List<MarkupReply>();
        var childrenByParentId = new Dictionary<string, List<MarkupReply>>(StringComparer.Ordinal);

        foreach (var reply in orderedReplies)
        {
            var parentReplyId = NormalizeParentReplyId(reply.ParentReplyId);
            if (string.IsNullOrWhiteSpace(parentReplyId) ||
                string.Equals(parentReplyId, reply.Id, StringComparison.Ordinal) ||
                !knownIds.Contains(parentReplyId))
            {
                roots.Add(reply);
                continue;
            }

            if (!childrenByParentId.TryGetValue(parentReplyId, out var children))
            {
                children = new List<MarkupReply>();
                childrenByParentId[parentReplyId] = children;
            }

            children.Add(reply);
        }

        foreach (var children in childrenByParentId.Values)
            children.Sort(ReplyComparer.Instance);

        roots.Sort(ReplyComparer.Instance);

        var result = new List<MarkupThreadEntry>(orderedReplies.Count);
        var visited = new HashSet<string>(StringComparer.Ordinal);

        foreach (var root in roots)
            AppendReplyAndChildren(root, depth: 0, childrenByParentId, visited, result);

        foreach (var reply in orderedReplies)
        {
            if (!visited.Contains(reply.Id))
                AppendReplyAndChildren(reply, depth: 0, childrenByParentId, visited, result);
        }

        return result;
    }

    public static string? GetLatestReplyId(IReadOnlyList<MarkupReply>? replies)
    {
        if (replies == null || replies.Count == 0)
            return null;

        return replies
            .OrderByDescending(reply => reply.ModifiedUtc)
            .ThenByDescending(reply => reply.CreatedUtc)
            .ThenByDescending(reply => reply.Id, StringComparer.Ordinal)
            .Select(reply => reply.Id)
            .FirstOrDefault(id => !string.IsNullOrWhiteSpace(id));
    }

    private static void AppendReplyAndChildren(
        MarkupReply reply,
        int depth,
        IReadOnlyDictionary<string, List<MarkupReply>> childrenByParentId,
        HashSet<string> visited,
        ICollection<MarkupThreadEntry> ordered)
    {
        if (!visited.Add(reply.Id))
            return;

        ordered.Add(new MarkupThreadEntry(reply, depth));

        if (!childrenByParentId.TryGetValue(reply.Id, out var children))
            return;

        foreach (var child in children)
            AppendReplyAndChildren(child, depth + 1, childrenByParentId, visited, ordered);
    }

    private static string? NormalizeParentReplyId(string? parentReplyId)
    {
        var normalized = parentReplyId?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private sealed class ReplyComparer : IComparer<MarkupReply>
    {
        public static ReplyComparer Instance { get; } = new();

        public int Compare(MarkupReply? x, MarkupReply? y)
        {
            if (ReferenceEquals(x, y))
                return 0;

            if (x is null)
                return -1;

            if (y is null)
                return 1;

            var createdComparison = x.CreatedUtc.CompareTo(y.CreatedUtc);
            if (createdComparison != 0)
                return createdComparison;

            var modifiedComparison = x.ModifiedUtc.CompareTo(y.ModifiedUtc);
            if (modifiedComparison != 0)
                return modifiedComparison;

            return StringComparer.Ordinal.Compare(x.Id, y.Id);
        }
    }
}