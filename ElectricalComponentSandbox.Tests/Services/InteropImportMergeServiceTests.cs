using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class InteropImportMergeServiceTests
{
    [Fact]
    public void MergeInto_NewImportedComponent_AddsNewLocalComponentAndStampsImportInfo()
    {
        var service = new InteropImportMergeService();
        var stampUtc = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);
        var existing = new List<ElectricalComponent>();

        var imported = CreateConduit(
            name: "Imported Conduit",
            configureMetadata: metadata =>
            {
                metadata.SourceSystem = "Revit";
                metadata.SourceDocumentId = "doc-001";
                metadata.SourceElementId = "element-100";
                metadata.ReviewStatus = ComponentInteropReviewStatus.Reviewed;
            });

        var result = service.MergeInto(
            existing,
            new[] { imported },
            InteropImportMergeOptions.Default with
            {
                ImportedUtc = stampUtc,
                InterchangeFormat = "IFC4",
            });

        Assert.Equal(1, result.ImportedCount);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(0, result.UnmatchedExistingCount);
        Assert.Single(existing);

        var added = existing[0];
        Assert.Equal("Imported Conduit", added.Name);
        Assert.Equal(stampUtc, added.InteropMetadata.LastImportedUtc);
        Assert.Equal("IFC4", added.InteropMetadata.LastInterchangeFormat);
        Assert.Equal(ComponentInteropReviewStatus.Unreviewed, added.InteropMetadata.ReviewStatus);
        Assert.Empty(added.InteropMetadata.ReviewedBy);
        Assert.Empty(added.InteropMetadata.ReviewNote);
        Assert.Null(added.InteropMetadata.LastReviewedUtc);
    }

    [Fact]
    public void MergeInto_ReimportedComponent_UpdatesMatchedComponentAndPreservesLocalId()
    {
        var service = new InteropImportMergeService();
        var stampUtc = new DateTime(2026, 5, 2, 12, 0, 0, DateTimeKind.Utc);

        var existingComponent = CreateConduit(
            name: "Old Name",
            configureMetadata: metadata =>
            {
                metadata.SourceSystem = "Revit";
                metadata.SourceDocumentId = "doc-001";
                metadata.SourceElementId = "element-100";
            });
        existingComponent.Id = "local-component-1";

        var existing = new List<ElectricalComponent> { existingComponent };

        var imported = CreateConduit(
            name: "Updated Name",
            configureMetadata: metadata =>
            {
                metadata.SourceSystem = "revit";
                metadata.SourceDocumentId = "doc-001";
                metadata.SourceElementId = "element-100";
            });
        imported.Id = "source-id-should-not-overwrite-local";

        var result = service.MergeInto(
            existing,
            new[] { imported },
            InteropImportMergeOptions.Default with { ImportedUtc = stampUtc });

        Assert.Equal(0, result.AddedCount);
        Assert.Equal(1, result.UpdatedCount);
        Assert.Equal(0, result.UnmatchedExistingCount);
        Assert.Single(existing);

        var updated = existing[0];
        Assert.Equal("local-component-1", updated.Id);
        Assert.Equal("Updated Name", updated.Name);
        Assert.Equal(stampUtc, updated.InteropMetadata.LastImportedUtc);
    }

    [Fact]
    public void MergeInto_SourceDocumentNameFallback_TracksUnmatchedExistingWithoutRemoval()
    {
        var service = new InteropImportMergeService();

        var existingMatchedCandidate = CreateConduit(
            name: "Existing",
            configureMetadata: metadata =>
            {
                metadata.SourceSystem = "Revit";
                metadata.SourceDocumentName = "LegacyModel.rvt";
                metadata.SourceElementId = "existing-element";
            });
        existingMatchedCandidate.Id = "existing-local-id";

        var existing = new List<ElectricalComponent> { existingMatchedCandidate };

        var importedNew = CreateConduit(
            name: "New Import",
            configureMetadata: metadata =>
            {
                metadata.SourceSystem = "Revit";
                metadata.SourceDocumentName = "LegacyModel.rvt";
                metadata.SourceElementId = "new-element";
            });

        var result = service.MergeInto(existing, new[] { importedNew });

        Assert.Equal(1, result.AddedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.UnmatchedExistingCount);
        Assert.Contains("existing-local-id", result.UnmatchedExistingComponentIds);
        Assert.Equal(2, existing.Count);
        Assert.Contains(existing, component => component.Id == "existing-local-id");
    }

    [Fact]
    public void MergeInto_ReimportResetReviewState_ClearsPriorReviewMetadata()
    {
        var service = new InteropImportMergeService();

        var reviewedUtc = new DateTime(2026, 5, 3, 8, 0, 0, DateTimeKind.Utc);
        var existingComponent = CreateConduit(
            name: "Reviewed Existing",
            configureMetadata: metadata =>
            {
                metadata.SourceSystem = "Revit";
                metadata.SourceDocumentId = "doc-002";
                metadata.SourceElementId = "element-200";
                metadata.ReviewStatus = ComponentInteropReviewStatus.Reviewed;
                metadata.ReviewedBy = "QA";
                metadata.ReviewNote = "Approved";
                metadata.LastReviewedUtc = reviewedUtc;
            });

        existingComponent.Id = "local-reviewed";
        var existing = new List<ElectricalComponent> { existingComponent };

        var importedUpdate = CreateConduit(
            name: "Reviewed Existing Updated",
            configureMetadata: metadata =>
            {
                metadata.SourceSystem = "Revit";
                metadata.SourceDocumentId = "doc-002";
                metadata.SourceElementId = "element-200";
                metadata.ReviewStatus = ComponentInteropReviewStatus.Reviewed;
                metadata.ReviewedBy = "External";
                metadata.ReviewNote = "Still reviewed";
                metadata.LastReviewedUtc = reviewedUtc.AddHours(1);
            });

        service.MergeInto(existing, new[] { importedUpdate });

        var updated = existing[0];
        Assert.Equal(ComponentInteropReviewStatus.Unreviewed, updated.InteropMetadata.ReviewStatus);
        Assert.Empty(updated.InteropMetadata.ReviewedBy);
        Assert.Empty(updated.InteropMetadata.ReviewNote);
        Assert.Null(updated.InteropMetadata.LastReviewedUtc);
    }

    private static ConduitComponent CreateConduit(string name, Action<ComponentInteropMetadata>? configureMetadata = null)
    {
        var metadata = new ComponentInteropMetadata();
        configureMetadata?.Invoke(metadata);

        return new ConduitComponent
        {
            Name = name,
            InteropMetadata = metadata,
        };
    }
}