using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class LayerStateServiceTests
{
    [Fact]
    public void SaveState_CreatesSnapshot()
    {
        var sut = new LayerStateService();
        var layers = CreateLayers();
        var before = DateTime.UtcNow;

        var snapshot = sut.SaveState("Working", layers);

        var after = DateTime.UtcNow;
        Assert.Equal("Working", snapshot.Name);
        Assert.Equal(2, snapshot.LayerEntries.Count);
        Assert.InRange(snapshot.SavedAt, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public void SaveState_CapturesLayerProperties()
    {
        var sut = new LayerStateService();
        var layer = Layer.CreateDefault();
        layer.Color = "#FF0000";
        layer.IsVisible = false;
        layer.IsFrozen = true;
        layer.IsPlotted = false;

        var snapshot = sut.SaveState("Props", new[] { layer });
        var entry = Assert.Single(snapshot.LayerEntries);

        Assert.Equal(layer.Color, entry.Color);
        Assert.Equal(layer.IsVisible, entry.IsVisible);
        Assert.Equal(layer.IsFrozen, entry.IsFrozen);
        Assert.Equal(layer.IsPlotted, entry.IsPlotted);
        Assert.Equal(layer.LineType, entry.LineType);
        Assert.Equal(layer.LineWeight, entry.LineWeight);
    }

    [Fact]
    public void RestoreState_RestoresVisibility()
    {
        var sut = new LayerStateService();
        var layer = Layer.CreateDefault();

        sut.SaveState("Visible", new[] { layer });
        layer.IsVisible = false;

        var restored = sut.RestoreState("Visible", new List<Layer> { layer });

        Assert.Single(restored);
        Assert.True(layer.IsVisible);
    }

    [Fact]
    public void RestoreState_UnknownName_ReturnsEmpty()
    {
        var sut = new LayerStateService();

        var restored = sut.RestoreState("Missing", new List<Layer> { Layer.CreateDefault() });

        Assert.Empty(restored);
    }

    [Fact]
    public void DeleteState_RemovesState()
    {
        var sut = new LayerStateService();
        sut.SaveState("Temp", new[] { Layer.CreateDefault() });

        var deleted = sut.DeleteState("Temp");

        Assert.True(deleted);
        Assert.DoesNotContain("Temp", sut.GetStateNames());
    }

    [Fact]
    public void RenameState_ChangesKey()
    {
        var sut = new LayerStateService();
        sut.SaveState("Old", new[] { Layer.CreateDefault() });

        var renamed = sut.RenameState("Old", "New");

        Assert.True(renamed);
        Assert.DoesNotContain("Old", sut.GetStateNames());
        Assert.Contains("New", sut.GetStateNames());
        Assert.True(sut.SavedStates.ContainsKey("New"));
    }

    [Fact]
    public void GetStateNames_ReturnsSorted()
    {
        var sut = new LayerStateService();
        sut.SaveState("Zulu", new[] { Layer.CreateDefault() });
        sut.SaveState("Alpha", new[] { Layer.CreateDefault() });
        sut.SaveState("Mike", new[] { Layer.CreateDefault() });

        var names = sut.GetStateNames();

        Assert.Equal(new[] { "Alpha", "Mike", "Zulu" }, names);
    }

    [Fact]
    public void CreatePresets_CreatesStandardStates()
    {
        var sut = new LayerStateService();

        sut.CreatePresets(CreateLayers());

        Assert.Contains("All On", sut.GetStateNames());
        Assert.Contains("All Off", sut.GetStateNames());
        Assert.Contains("Electrical Only", sut.GetStateNames());
        Assert.Contains("Print Ready", sut.GetStateNames());
        Assert.Equal(4, sut.SavedStates.Count);
    }

    private static IReadOnlyList<Layer> CreateLayers()
    {
        var defaultLayer = Layer.CreateDefault();
        var electricalLayer = new Layer
        {
            Name = "E-Lighting",
            Color = "#FF0000",
            IsVisible = true,
            IsLocked = true,
            IsFrozen = false,
            IsPlotted = true,
            LineType = LineType.Dashed,
            LineWeight = 0.35
        };

        return new[] { defaultLayer, electricalLayer };
    }
}
