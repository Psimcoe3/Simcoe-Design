using ElectricalComponentSandbox.Models;
using Newtonsoft.Json;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Reconciles imported components against existing components by stable source identity.
/// </summary>
public class InteropImportMergeService
{
    private static readonly JsonSerializerSettings CloneSettings = new()
    {
        TypeNameHandling = TypeNameHandling.Auto,
    };

    public InteropImportMergeResult MergeInto(
        IList<ElectricalComponent> existingComponents,
        IReadOnlyList<ElectricalComponent> importedComponents,
        InteropImportMergeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(existingComponents);
        ArgumentNullException.ThrowIfNull(importedComponents);

        var mergeOptions = options ?? InteropImportMergeOptions.Default;
        var importStampUtc = (mergeOptions.ImportedUtc ?? DateTime.UtcNow).ToUniversalTime();

        var existingIds = existingComponents
            .Select(component => component.Id)
            .ToHashSet(StringComparer.Ordinal);

        var existingByIdentity = BuildExistingIdentityMap(existingComponents);
        var existingIdentityByComponentId = BuildExistingComponentIdentityMap(existingComponents);

        var matchedExistingComponentIds = new HashSet<string>(StringComparer.Ordinal);
        var addedComponentIds = new List<string>();
        var updatedComponentIds = new List<string>();

        foreach (var imported in importedComponents)
        {
            var importedClone = DeepClone(imported);
            PrepareImportedMetadata(importedClone.InteropMetadata, mergeOptions, importStampUtc);

            var identity = BuildSourceIdentityKey(importedClone.InteropMetadata);
            if (TryUpdateMatchedComponent(existingComponents, existingByIdentity, identity, importedClone, mergeOptions))
            {
                matchedExistingComponentIds.Add(importedClone.Id);
                updatedComponentIds.Add(importedClone.Id);
                existingIds.Add(importedClone.Id);
                continue;
            }

            AddNewImportedComponent(existingComponents, existingByIdentity, existingIds, identity, importedClone, mergeOptions);
            addedComponentIds.Add(importedClone.Id);
        }

        var unmatchedExistingComponentIds = existingIdentityByComponentId
            .Where(entry => !matchedExistingComponentIds.Contains(entry.Key))
            .Select(entry => entry.Key)
            .ToList();

        return new InteropImportMergeResult(
            importedComponents.Count,
            addedComponentIds,
            updatedComponentIds,
            unmatchedExistingComponentIds);
    }

    internal static string? BuildSourceIdentityKey(ComponentInteropMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        var sourceSystem = NormalizeToken(metadata.SourceSystem);
        var sourceElementId = NormalizeToken(metadata.SourceElementId);
        var sourceDocument = NormalizeToken(metadata.SourceDocumentId) ?? NormalizeToken(metadata.SourceDocumentName);

        if (sourceSystem == null || sourceDocument == null || sourceElementId == null)
            return null;

        return $"{sourceSystem}|{sourceDocument}|{sourceElementId}";
    }

    private static string? NormalizeToken(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrWhiteSpace(trimmed)
            ? null
            : trimmed;
    }

    private static Dictionary<string, int> BuildExistingIdentityMap(IList<ElectricalComponent> existingComponents)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < existingComponents.Count; index++)
        {
            var identity = BuildSourceIdentityKey(existingComponents[index].InteropMetadata);
            if (identity == null || map.ContainsKey(identity))
                continue;

            map[identity] = index;
        }

        return map;
    }

    private static Dictionary<string, string> BuildExistingComponentIdentityMap(IList<ElectricalComponent> existingComponents)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var component in existingComponents)
        {
            var identity = BuildSourceIdentityKey(component.InteropMetadata);
            if (identity != null)
                map[component.Id] = identity;
        }

        return map;
    }

    private static bool TryUpdateMatchedComponent(
        IList<ElectricalComponent> existingComponents,
        IDictionary<string, int> existingByIdentity,
        string? identity,
        ElectricalComponent importedClone,
        InteropImportMergeOptions options)
    {
        if (identity == null || !existingByIdentity.TryGetValue(identity, out var existingIndex))
            return false;

        var existing = existingComponents[existingIndex];
        importedClone.Id = existing.Id;
        EnsureReviewState(importedClone.InteropMetadata, options.ResetReviewStateOnUpdate);

        existingComponents[existingIndex] = importedClone;
        existingByIdentity[identity] = existingIndex;
        return true;
    }

    private static void AddNewImportedComponent(
        IList<ElectricalComponent> existingComponents,
        IDictionary<string, int> existingByIdentity,
        ISet<string> existingIds,
        string? identity,
        ElectricalComponent importedClone,
        InteropImportMergeOptions options)
    {
        importedClone.Id = EnsureUniqueLocalId(importedClone.Id, existingIds);
        EnsureReviewState(importedClone.InteropMetadata, options.ResetReviewStateOnInsert);

        existingComponents.Add(importedClone);
        var addedIndex = existingComponents.Count - 1;
        existingIds.Add(importedClone.Id);

        if (identity != null && !existingByIdentity.ContainsKey(identity))
            existingByIdentity[identity] = addedIndex;
    }

    private static ElectricalComponent DeepClone(ElectricalComponent component)
    {
        var runtimeType = component.GetType();
        var json = JsonConvert.SerializeObject(component, runtimeType, CloneSettings);
        var clone = JsonConvert.DeserializeObject(json, runtimeType, CloneSettings) as ElectricalComponent;
        return clone ?? throw new InvalidOperationException("Failed to clone imported component.");
    }

    private static void PrepareImportedMetadata(
        ComponentInteropMetadata metadata,
        InteropImportMergeOptions options,
        DateTime importStampUtc)
    {
        NormalizeMetadataTokens(metadata);
        metadata.LastImportedUtc = importStampUtc;
        ApplyInterchangeFormat(metadata, options.InterchangeFormat);
    }

    private static void NormalizeMetadataTokens(ComponentInteropMetadata metadata)
    {
        metadata.SourceSystem = NormalizeOrEmpty(metadata.SourceSystem);
        metadata.SourceDocumentId = NormalizeOrEmpty(metadata.SourceDocumentId);
        metadata.SourceDocumentName = NormalizeOrEmpty(metadata.SourceDocumentName);
        metadata.SourceElementId = NormalizeOrEmpty(metadata.SourceElementId);
        metadata.SourceFamilyName = NormalizeOrEmpty(metadata.SourceFamilyName);
        metadata.SourceTypeName = NormalizeOrEmpty(metadata.SourceTypeName);
    }

    private static string NormalizeOrEmpty(string? value) => value?.Trim() ?? string.Empty;

    private static void ApplyInterchangeFormat(ComponentInteropMetadata metadata, string? interchangeFormat)
    {
        var normalizedFormat = NormalizeToken(interchangeFormat);
        if (normalizedFormat != null)
            metadata.LastInterchangeFormat = normalizedFormat;
    }

    private static void EnsureReviewState(ComponentInteropMetadata metadata, bool resetReviewState)
    {
        if (!resetReviewState)
            return;

        metadata.ReviewStatus = ComponentInteropReviewStatus.Unreviewed;
        metadata.ReviewedBy = string.Empty;
        metadata.ReviewNote = string.Empty;
        metadata.LastReviewedUtc = null;
    }

    private static string EnsureUniqueLocalId(string currentId, ISet<string> existingIds)
    {
        if (!string.IsNullOrWhiteSpace(currentId) && !existingIds.Contains(currentId))
            return currentId;

        string nextId;
        do
        {
            nextId = Guid.NewGuid().ToString();
        }
        while (existingIds.Contains(nextId));

        return nextId;
    }
}

public sealed record InteropImportMergeResult(
    int ImportedCount,
    IReadOnlyList<string> AddedComponentIds,
    IReadOnlyList<string> UpdatedComponentIds,
    IReadOnlyList<string> UnmatchedExistingComponentIds)
{
    public int AddedCount => AddedComponentIds.Count;

    public int UpdatedCount => UpdatedComponentIds.Count;

    public int UnmatchedExistingCount => UnmatchedExistingComponentIds.Count;
}

public sealed record InteropImportMergeOptions(
    DateTime? ImportedUtc,
    string? InterchangeFormat,
    bool ResetReviewStateOnUpdate,
    bool ResetReviewStateOnInsert)
{
    public static InteropImportMergeOptions Default { get; } = new(
        ImportedUtc: null,
        InterchangeFormat: null,
        ResetReviewStateOnUpdate: true,
        ResetReviewStateOnInsert: true);
}