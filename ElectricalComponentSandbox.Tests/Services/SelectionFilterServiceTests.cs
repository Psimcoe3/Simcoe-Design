using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class SelectionFilterServiceTests
{
    private readonly SelectionFilterService _svc = new();

    private static List<ElectricalComponent> CreateTestComponents() => new()
    {
        new BoxComponent { Name = "JB-1", LayerId = "power" },
        new BoxComponent { Name = "JB-2", LayerId = "lighting" },
        new PanelComponent { Name = "LP-1", LayerId = "power" },
        new ConduitComponent { Name = "C-1", LayerId = "power" },
        new ConduitComponent { Name = "C-2", LayerId = "lighting" },
    };

    [Fact]
    public void SelectByLayer_ReturnsOnlyMatchingLayer()
    {
        var components = CreateTestComponents();
        var result = _svc.SelectByLayer(components, "power");

        Assert.Equal(3, result.Count);
        Assert.All(result, c => Assert.Equal("power", c.LayerId));
    }

    [Fact]
    public void SelectByType_ReturnsOnlyMatchingType()
    {
        var components = CreateTestComponents();
        var result = _svc.SelectByType(components, ComponentType.Conduit);

        Assert.Equal(2, result.Count);
        Assert.All(result, c => Assert.Equal(ComponentType.Conduit, c.Type));
    }

    [Fact]
    public void SelectSimilar_MatchesTypeAndLayer()
    {
        var components = CreateTestComponents();
        var reference = components[0]; // JB-1 on power layer

        var result = _svc.SelectSimilar(components, reference);

        // Should not include the reference itself
        Assert.DoesNotContain(reference, result);
        // No other Box on "power" layer
        Assert.Empty(result);
    }

    [Fact]
    public void QuickSelect_MultiCriteria_FiltersCorrectly()
    {
        var components = CreateTestComponents();

        var result = _svc.QuickSelect(components, new SelectionCriteria
        {
            ComponentType = ComponentType.Conduit,
            LayerId = "lighting"
        });

        Assert.Single(result);
        Assert.Equal("C-2", result[0].Name);
    }

    [Fact]
    public void QuickSelect_NameContains_CaseInsensitive()
    {
        var components = CreateTestComponents();

        var result = _svc.QuickSelect(components, new SelectionCriteria
        {
            NameContains = "jb"
        });

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void BulkPropertyChange_UpdatesAllComponents()
    {
        var components = CreateTestComponents();
        var targets = _svc.SelectByLayer(components, "power");

        var changeResult = _svc.ApplyBulkPropertyChange(targets, new BulkPropertyChange
        {
            LayerId = "relocated"
        });

        Assert.Equal(3, changeResult.AffectedCount);
        Assert.All(targets, c => Assert.Equal("relocated", c.LayerId));
    }

    [Fact]
    public void BulkPropertyChange_Revert_RestoresOriginal()
    {
        var components = CreateTestComponents();
        var targets = _svc.SelectByLayer(components, "power");

        var changeResult = _svc.ApplyBulkPropertyChange(targets, new BulkPropertyChange
        {
            Elevation = 10.0
        });

        // Verify changed
        Assert.All(targets, c => Assert.Equal(10.0, c.Parameters.Elevation));

        // Revert
        _svc.RevertBulkPropertyChange(changeResult);

        // Verify restored
        Assert.All(targets, c => Assert.Equal(0.0, c.Parameters.Elevation));
    }
}
