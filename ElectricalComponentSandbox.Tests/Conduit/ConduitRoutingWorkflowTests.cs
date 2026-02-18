using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Core.Routing;

namespace ElectricalComponentSandbox.Tests.Conduit;

public class ConduitRoutingWorkflowTests
{
    [Fact]
    public void ResolveRoutingDefaults_InvalidTradeSize_FallsBackToTypeTradeSizeAndMaterial()
    {
        var store = new ConduitModelStore();
        var type = new ConduitType
        {
            Standard = ConduitMaterialType.PVC,
            SizeSettings = new ConduitSizeSettings { Standard = ConduitMaterialType.PVC }
        };
        type.SizeSettings.AddSize(new ConduitSize { TradeSize = "1" });
        store.AddType(type);
        store.Settings.DefaultConduitTypeId = type.Id;

        var defaults = store.ResolveRoutingDefaults(type.Id, "9", null);

        Assert.Equal(type.Id, defaults.ConduitTypeId);
        Assert.Equal("1", defaults.TradeSize);
        Assert.Equal(ConduitMaterialType.PVC, defaults.Material);
    }

    [Fact]
    public void CreateRunFromSegments_TypeWithoutFittings_SkipsAutomaticFittings()
    {
        var store = new ConduitModelStore();
        var type = new ConduitType
        {
            IsWithFitting = false,
            SizeSettings = ConduitSizeSettings.CreateDefaultEMT()
        };
        store.AddType(type);

        var segments = new List<ConduitSegment>
        {
            new() { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(5, 0, 0), ConduitTypeId = type.Id, TradeSize = "1/2" },
            new() { StartPoint = new XYZ(5, 0, 0), EndPoint = new XYZ(5, 5, 0), ConduitTypeId = type.Id, TradeSize = "1/2" }
        };

        var run = store.CreateRunFromSegments(segments);

        Assert.Empty(run.FittingIds);
    }

    [Fact]
    public void AutoRoute_AutoRiseDropDisabled_FlattensPathToSpecifiedElevation()
    {
        var store = new ConduitModelStore();
        var type = new ConduitType();
        store.AddType(type);
        store.Settings.DefaultConduitTypeId = type.Id;

        var service = new AutoRouteService(store);
        var pathway = new List<XYZ>
        {
            new(0, 0, 0),
            new(5, 0, 12),
            new(10, 0, 3)
        };

        var run = service.AutoRoute(pathway, new RoutingOptions
        {
            ConduitTypeId = type.Id,
            TradeSize = "1/2",
            Elevation = 8,
            AutoRiseDrop = false
        });

        var zValues = run.GetSegments(store)
            .SelectMany(s => new[] { s.StartPoint.Z, s.EndPoint.Z })
            .Distinct()
            .ToList();

        Assert.Single(zValues);
        Assert.Equal(8, zValues[0], 6);
    }
}
