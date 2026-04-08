using System.IO;
using System.Windows;
using ElectricalComponentSandbox.Markup.Models;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class ProjectFileServiceTests : IDisposable
{
    private readonly ProjectFileService _service;
    private readonly string _tempDir;

    public ProjectFileServiceTests()
    {
        _service = new ProjectFileService();
        _tempDir = Path.Combine(Path.GetTempPath(), $"ECS_Proj_Tests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    [Fact]
    public async Task SaveAndLoad_EmptyProject_RoundTrips()
    {
        var project = new ProjectModel { Name = "Test Project" };
        var filePath = Path.Combine(_tempDir, "test.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal("Test Project", loaded.Name);
    }

    [Fact]
    public async Task SaveAndLoad_ProjectWithComponents_RoundTrips()
    {
        var project = new ProjectModel { Name = "Full Project" };
        project.Components.Add(new BoxComponent { Name = "Box 1" });
        project.Components.Add(new ConduitComponent { Name = "Conduit 1" });
        project.Components.Add(new CableTrayComponent { Name = "Tray 1" });
        project.Components.Add(new HangerComponent { Name = "Hanger 1" });
        project.Layers.Add(Layer.CreateDefault());
        var filePath = Path.Combine(_tempDir, "full.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(4, loaded.Components.Count);
        Assert.Single(loaded.Layers);
        Assert.IsType<BoxComponent>(loaded.Components[0]);
        Assert.IsType<CableTrayComponent>(loaded.Components[2]);
        Assert.IsType<HangerComponent>(loaded.Components[3]);
    }

    [Fact]
    public async Task SaveAndLoad_ProjectWithComponentInteropMetadata_RoundTrips()
    {
        var importedUtc = new DateTime(2026, 4, 7, 12, 30, 0, DateTimeKind.Utc);
        var reviewedUtc = new DateTime(2026, 4, 7, 16, 45, 0, DateTimeKind.Utc);
        var project = new ProjectModel { Name = "Interop Project" };
        project.Components.Add(new ConduitComponent
        {
            Name = "Imported Conduit",
            InteropMetadata = new ComponentInteropMetadata
            {
                SourceSystem = "Revit",
                SourceDocumentId = "project-guid-001",
                SourceDocumentName = "Campus Power.rvt",
                SourceElementId = "element-12345",
                SourceFamilyName = "Conduit",
                SourceTypeName = "EMT 3-4",
                LastInterchangeFormat = "IFC4",
                LastImportedUtc = importedUtc,
                ReviewStatus = ComponentInteropReviewStatus.NeedsChanges,
                ReviewedBy = "BIM Coordinator",
                ReviewNote = "Check conduit elevation offsets.",
                LastReviewedUtc = reviewedUtc
            }
        });
        var filePath = Path.Combine(_tempDir, "interop.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        var loadedConduit = Assert.IsType<ConduitComponent>(Assert.Single(loaded.Components));
        Assert.Equal("Revit", loadedConduit.InteropMetadata.SourceSystem);
        Assert.Equal("project-guid-001", loadedConduit.InteropMetadata.SourceDocumentId);
        Assert.Equal("Campus Power.rvt", loadedConduit.InteropMetadata.SourceDocumentName);
        Assert.Equal("element-12345", loadedConduit.InteropMetadata.SourceElementId);
        Assert.Equal("Conduit", loadedConduit.InteropMetadata.SourceFamilyName);
        Assert.Equal("EMT 3-4", loadedConduit.InteropMetadata.SourceTypeName);
        Assert.Equal("IFC4", loadedConduit.InteropMetadata.LastInterchangeFormat);
        Assert.Equal(importedUtc, loadedConduit.InteropMetadata.LastImportedUtc);
        Assert.Equal(ComponentInteropReviewStatus.NeedsChanges, loadedConduit.InteropMetadata.ReviewStatus);
        Assert.Equal("BIM Coordinator", loadedConduit.InteropMetadata.ReviewedBy);
        Assert.Equal("Check conduit elevation offsets.", loadedConduit.InteropMetadata.ReviewNote);
        Assert.Equal(reviewedUtc, loadedConduit.InteropMetadata.LastReviewedUtc);
    }

    [Fact]
    public async Task SaveAndLoad_ProjectSettings_Preserved()
    {
        var project = new ProjectModel
        {
            UnitSystem = "Metric",
            GridSize = 2.5,
            ShowGrid = false,
            SnapToGrid = false
        };
        var filePath = Path.Combine(_tempDir, "settings.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal("Metric", loaded.UnitSystem);
        Assert.Equal(2.5, loaded.GridSize);
        Assert.False(loaded.ShowGrid);
        Assert.False(loaded.SnapToGrid);
    }

    [Fact]
    public async Task SaveAndLoad_PdfUnderlay_Preserved()
    {
        var project = new ProjectModel();
        project.PdfUnderlay = new PdfUnderlay
        {
            FilePath = "test.pdf",
            PageNumber = 2,
            Opacity = 0.7,
            Scale = 1.5,
            IsCalibrated = true,
            PixelsPerUnit = 48.0
        };
        var filePath = Path.Combine(_tempDir, "pdf.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded?.PdfUnderlay);
        Assert.Equal("test.pdf", loaded.PdfUnderlay.FilePath);
        Assert.Equal(2, loaded.PdfUnderlay.PageNumber);
        Assert.Equal(0.7, loaded.PdfUnderlay.Opacity);
        Assert.True(loaded.PdfUnderlay.IsCalibrated);
        Assert.Equal(48.0, loaded.PdfUnderlay.PixelsPerUnit);
    }

    [Fact]
    public async Task LoadProject_InvalidFile_ThrowsException()
    {
        var filePath = Path.Combine(_tempDir, "invalid.ecproj");
        await File.WriteAllTextAsync(filePath, "not valid json");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.LoadProjectAsync(filePath));
    }

    [Fact]
    public void GetFileFilter_ReturnsExpectedFilter()
    {
        var filter = ProjectFileService.GetFileFilter();

        Assert.Contains("*.ecproj", filter);
    }

    [Fact]
    public async Task SaveAndLoad_NamedViews_RoundTrips()
    {
        var project = new ProjectModel();
        project.NamedViews.Add(new NamedView { Name = "Plan View", PanX = 100, PanY = 200, Zoom = 1.5, VisibleLayerIds = new List<string> { "layer-1" } });
        project.NamedViews.Add(new NamedView { Name = "Detail A", PanX = 50, PanY = 75, Zoom = 3.0 });
        var filePath = Path.Combine(_tempDir, "namedviews.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.NamedViews.Count);
        Assert.Equal("Plan View", loaded.NamedViews[0].Name);
        Assert.Equal(100, loaded.NamedViews[0].PanX);
        Assert.Equal(200, loaded.NamedViews[0].PanY);
        Assert.Equal(1.5, loaded.NamedViews[0].Zoom);
        var visibleLayerIds = loaded.NamedViews[0].VisibleLayerIds;
        Assert.NotNull(visibleLayerIds);
        Assert.Single(visibleLayerIds);
        Assert.Equal("layer-1", visibleLayerIds[0]);
        Assert.Equal("Detail A", loaded.NamedViews[1].Name);
        Assert.Equal(3.0, loaded.NamedViews[1].Zoom);
    }

    [Fact]
    public async Task SaveAndLoad_PlotStyleTables_RoundTrips()
    {
        var project = new ProjectModel();
        project.PlotStyleTables.Add(PlotStyleTable.CreateMonochrome());
        var filePath = Path.Combine(_tempDir, "plotstyles.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.PlotStyleTables);
        Assert.Equal("monochrome.ctb", loaded.PlotStyleTables[0].Name);
        Assert.Equal(256, loaded.PlotStyleTables[0].Pens.Count);
    }

    [Fact]
    public async Task SaveAndLoad_PlotLayout_RoundTrips()
    {
        var project = new ProjectModel();
        project.PlotLayout = new PlotLayout
        {
            PaperSize = PaperSize.ANSI_D,
            PlotScale = 24.0,
            CustomWidth = 11.0,
            CustomHeight = 8.5
        };
        var filePath = Path.Combine(_tempDir, "plotlayout.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded?.PlotLayout);
        Assert.Equal(PaperSize.ANSI_D, loaded.PlotLayout.PaperSize);
        Assert.Equal(24.0, loaded.PlotLayout.PlotScale);
    }

    [Fact]
    public async Task SaveAndLoad_SavedPageSetups_RoundTrips()
    {
        var project = new ProjectModel();
        project.SavedPageSetups.Add(new PlotLayout
        {
            Name = "Permit Set",
            PaperSize = PaperSize.ANSI_C,
            PlotScale = 12.0,
            PlotStyleTableName = "permit.ctb"
        });
        var filePath = Path.Combine(_tempDir, "saved-page-setups.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.SavedPageSetups);
        Assert.Equal("Permit Set", loaded.SavedPageSetups[0].Name);
        Assert.Equal(PaperSize.ANSI_C, loaded.SavedPageSetups[0].PaperSize);
        Assert.Equal(12.0, loaded.SavedPageSetups[0].PlotScale);
        Assert.Equal("permit.ctb", loaded.SavedPageSetups[0].PlotStyleTableName);
    }

    [Fact]
    public async Task SaveAndLoad_Sheets_RoundTrips()
    {
        var project = new ProjectModel();
        project.Sheets.Add(new DrawingSheet
        {
            Number = "A101",
            Name = "Plan",
            PdfUnderlay = new PdfUnderlay { FilePath = "plan.pdf", PageNumber = 3 },
            PlotLayout = new PlotLayout { PaperSize = PaperSize.ANSI_C, PlotScale = 12.0 },
            NamedViews =
            [
                new NamedView { Name = "Area A", PanX = 25, PanY = 50, Zoom = 1.25 }
            ],
            Markups =
            [
                new MarkupRecord
                {
                    Type = MarkupType.Rectangle,
                    Vertices = [new Point(0, 0), new Point(20, 10)]
                }
            ]
        });
        var filePath = Path.Combine(_tempDir, "sheets.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Sheets);
        Assert.Equal("A101", loaded.Sheets[0].Number);
        Assert.Equal("plan.pdf", loaded.Sheets[0].PdfUnderlay?.FilePath);
        Assert.Single(loaded.Sheets[0].NamedViews);
        Assert.Single(loaded.Sheets[0].Markups);
        Assert.Equal(PaperSize.ANSI_C, loaded.Sheets[0].PlotLayout?.PaperSize);
    }

    [Fact]
    public async Task SaveAndLoad_ActiveSheetId_RoundTrips()
    {
        var firstSheet = DrawingSheet.CreateDefault(1);
        var secondSheet = DrawingSheet.CreateDefault(2);
        var project = new ProjectModel
        {
            Sheets = [firstSheet, secondSheet],
            ActiveSheetId = secondSheet.Id
        };
        var filePath = Path.Combine(_tempDir, "active-sheet.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(secondSheet.Id, loaded.ActiveSheetId);
    }

    [Fact]
    public async Task SaveAndLoad_LiveSchedulesAndCircuits_RoundTrips()
    {
        var sheet = DrawingSheet.CreateDefault(1);
        sheet.LiveSchedules.Add(new LiveScheduleInstance
        {
            Kind = LiveScheduleKind.CircuitSummary,
            Origin = new Point(120, 140),
            GroupId = "group-1"
        });

        var project = new ProjectModel
        {
            Sheets = [sheet],
            Circuits =
            [
                new Circuit
                {
                    CircuitNumber = "1",
                    Description = "Lighting",
                    PanelId = "panel-1",
                    Phase = "A",
                    Voltage = 120,
                    ConnectedLoadVA = 1800,
                    WireLengthFeet = 75,
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 },
                    Wire = new WireSpec { Size = "12", Material = ConductorMaterial.Copper }
                }
            ]
        };
        var filePath = Path.Combine(_tempDir, "live-schedules.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Circuits);
        Assert.Single(loaded.Sheets);
        Assert.Single(loaded.Sheets[0].LiveSchedules);
        Assert.Equal(LiveScheduleKind.CircuitSummary, loaded.Sheets[0].LiveSchedules[0].Kind);
        Assert.Equal(new Point(120, 140), loaded.Sheets[0].LiveSchedules[0].Origin);
        Assert.Equal("Lighting", loaded.Circuits[0].Description);
    }

    [Fact]
    public async Task SaveAndLoad_LiveTitleBlocks_RoundTrips()
    {
        var titleBlockService = new TitleBlockService();
        var sheet = DrawingSheet.CreateDefault(1);
        sheet.LiveTitleBlocks.Add(new LiveTitleBlockInstance
        {
            Origin = new Point(72, 72),
            GroupId = "title-group",
            Template = titleBlockService.GetDefaultTemplate(PaperSizeType.ANSI_B)
        });

        var project = new ProjectModel
        {
            Name = "Tower Renovation",
            Sheets = [sheet]
        };
        var filePath = Path.Combine(_tempDir, "live-titleblocks.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal("Tower Renovation", loaded.Name);
        Assert.Single(loaded.Sheets);
        Assert.Single(loaded.Sheets[0].LiveTitleBlocks);
        Assert.Equal(new Point(72, 72), loaded.Sheets[0].LiveTitleBlocks[0].Origin);
        Assert.Equal(PaperSizeType.ANSI_B, loaded.Sheets[0].LiveTitleBlocks[0].Template.PaperSize);
    }

    [Fact]
    public async Task SaveAndLoad_SheetRevisionEntriesAndMetadata_RoundTrips()
    {
        var sheet = DrawingSheet.CreateDefault(1);
        sheet.Status = DrawingSheetStatus.Approved;
        sheet.CreatedBy = "Paul";
        sheet.ModifiedBy = "Reviewer";
        sheet.ApprovedBy = "Reviewer";
        sheet.ApprovedUtc = new DateTime(2026, 3, 31, 12, 0, 0, DateTimeKind.Utc);
        sheet.RevisionEntries.Add(new RevisionEntry
        {
            RevisionNumber = "A",
            Date = "2026-03-31",
            Description = "Initial issue",
            Author = "Paul"
        });

        var project = new ProjectModel
        {
            Sheets = [sheet]
        };
        var filePath = Path.Combine(_tempDir, "sheet-revisions.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Sheets);
        Assert.Equal(DrawingSheetStatus.Approved, loaded.Sheets[0].Status);
        Assert.Equal("Paul", loaded.Sheets[0].CreatedBy);
        Assert.Equal("Reviewer", loaded.Sheets[0].ApprovedBy);
        Assert.Single(loaded.Sheets[0].RevisionEntries);
        Assert.Equal("A", loaded.Sheets[0].RevisionEntries[0].RevisionNumber);
        Assert.Equal("Initial issue", loaded.Sheets[0].RevisionEntries[0].Description);
    }

    [Fact]
    public async Task SaveAndLoad_ProjectParametersAndBindings_RoundTrip()
    {
        var parameter = new ProjectParameterDefinition { Name = "Shared Width", Value = 4.25 };
        var box = new BoxComponent { Name = "Box 1" };
        box.Parameters.SetBinding(ProjectParameterBindingTarget.Width, parameter.Id);

        var project = new ProjectModel();
        project.ProjectParameters.Add(parameter);
        project.Components.Add(box);

        var filePath = Path.Combine(_tempDir, "project-parameters.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.ProjectParameters);
        Assert.Equal("Shared Width", loaded.ProjectParameters[0].Name);
        Assert.Equal(4.25, loaded.ProjectParameters[0].Value, 6);
        Assert.Single(loaded.Components);
        Assert.Equal(parameter.Id, loaded.Components[0].Parameters.GetBinding(ProjectParameterBindingTarget.Width));
    }

    [Fact]
    public async Task SaveAndLoad_ProjectParameterFormula_RoundTrips()
    {
        var project = new ProjectModel();
        project.ProjectParameters.Add(new ProjectParameterDefinition { Name = "Base Width", Value = 2.0 });
        project.ProjectParameters.Add(new ProjectParameterDefinition { Name = "Derived Width", Value = 4.5, Formula = "[Base Width] * 2 + 0.5" });

        var filePath = Path.Combine(_tempDir, "project-parameter-formula.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.ProjectParameters.Count);
        Assert.Equal("[Base Width] * 2 + 0.5", loaded.ProjectParameters[1].Formula);
    }

    [Fact]
    public async Task SaveAndLoad_TextProjectParameterAndBinding_RoundTrips()
    {
        var parameter = new ProjectParameterDefinition
        {
            Name = "Shared Material",
            ValueKind = ProjectParameterValueKind.Text,
            TextValue = "PVC"
        };
        var box = new BoxComponent { Name = "Box 1" };
        box.Parameters.SetBinding(ProjectParameterBindingTarget.Material, parameter.Id);
        box.Parameters.Material = "PVC";

        var project = new ProjectModel();
        project.ProjectParameters.Add(parameter);
        project.Components.Add(box);

        var filePath = Path.Combine(_tempDir, "project-parameter-text.ecproj");

        await _service.SaveProjectAsync(project, filePath);
        var loaded = await _service.LoadProjectAsync(filePath);

        Assert.NotNull(loaded);
        Assert.Single(loaded.ProjectParameters);
        Assert.Equal(ProjectParameterValueKind.Text, loaded.ProjectParameters[0].ValueKind);
        Assert.Equal("PVC", loaded.ProjectParameters[0].TextValue);
        Assert.Single(loaded.Components);
        Assert.Equal(parameter.Id, loaded.Components[0].Parameters.GetBinding(ProjectParameterBindingTarget.Material));
    }
}
