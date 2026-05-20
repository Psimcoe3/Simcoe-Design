using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class WireSpecLibraryServiceTests
{
    private static RunScheduleConfiguration BuildConfig()
    {
        var cfg = new RunScheduleConfiguration();
        // Three feeders in a single configuration family
        cfg.WireSpecifications.AddRange(new[]
        {
            new WireSpecification
            {
                Name = "1P3W-CU", FeederId = "1P3W-CU-100A", MaterialName = "Copper",
                Amperage = 100, PhaseSize = "1", PhaseQuantity = 2,
                NeutralSize = "1", NeutralQuantity = 1,
                GroundSize = "6", GroundQuantity = 1,
                ParallelQuantity = 1, ConduitSizeFeet = 0.125, // 1-1/2"
            },
            new WireSpecification
            {
                Name = "1P3W-CU", FeederId = "1P3W-CU-200A", MaterialName = "Copper",
                Amperage = 200, PhaseSize = "3/0", PhaseQuantity = 2,
                NeutralSize = "3/0", NeutralQuantity = 1,
                GroundSize = "6", GroundQuantity = 1,
                ParallelQuantity = 1, ConduitSizeFeet = 0.166666666, // 2"
            },
            new WireSpecification
            {
                Name = "3P4W-AL", FeederId = "3P4W-AL-600A", MaterialName = "Aluminum",
                Amperage = 600, PhaseSize = "350", PhaseQuantity = 3,
                NeutralSize = "350", NeutralQuantity = 1,
                GroundSize = "1", GroundQuantity = 1,
                ParallelQuantity = 2, ConduitSizeFeet = 0.25, // 3"
            },
        });
        cfg.WireSizes.AddRange(new[]
        {
            new WireSizeEntry { MaterialName = "Copper", Insulation = "THHN", Ampacity = 100, Gauge = "1", DiameterFeet = 0.040 },
            new WireSizeEntry { MaterialName = "Copper", Insulation = "THHN", Ampacity = 65, Gauge = "6", DiameterFeet = 0.025 },
        });
        return cfg;
    }

    [Fact]
    public void Constructor_NullConfig_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new WireSpecLibraryService(null!));
    }

    [Fact]
    public void FindByFeederId_Match_ReturnsSpec()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        var result = svc.FindByFeederId("1P3W-CU-200A");

        Assert.NotNull(result);
        Assert.Equal(200, result!.Amperage);
    }

    [Fact]
    public void FindByFeederId_NoMatch_ReturnsNull()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        Assert.Null(svc.FindByFeederId("does-not-exist"));
        Assert.Null(svc.FindByFeederId("  "));
    }

    [Fact]
    public void FindByLoadAmps_PicksSmallestThatCovers()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        var result = svc.FindByLoadAmps("1P3W-CU", 150);

        Assert.NotNull(result);
        Assert.Equal(200, result!.Amperage);
    }

    [Fact]
    public void FindByLoadAmps_LoadExceedsLibrary_ReturnsNull()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        Assert.Null(svc.FindByLoadAmps("1P3W-CU", 5000));
    }

    [Fact]
    public void FindByLoadAmps_BlankConfigOrZeroAmps_ReturnsNull()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        Assert.Null(svc.FindByLoadAmps("", 100));
        Assert.Null(svc.FindByLoadAmps("1P3W-CU", 0));
        Assert.Null(svc.FindByLoadAmps("1P3W-CU", -10));
    }

    [Fact]
    public void ListConfigurations_ReturnsDistinctSortedNames()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        var names = svc.ListConfigurations();

        Assert.Equal(new[] { "1P3W-CU", "3P4W-AL" }, names);
    }

    [Fact]
    public void ListConfiguration_ReturnsAmpAscending()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        var family = svc.ListConfiguration("1P3W-CU");

        Assert.Equal(2, family.Count);
        Assert.True(family[0].Amperage < family[1].Amperage);
    }

    [Fact]
    public void FindWireSize_MatchesByMaterialInsulationGauge()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        var size = svc.FindWireSize("Copper", "THHN", "1");

        Assert.NotNull(size);
        Assert.Equal(100, size!.Ampacity);
    }

    [Fact]
    public void FindWireSize_NoMatch_ReturnsNull()
    {
        var svc = new WireSpecLibraryService(BuildConfig());

        Assert.Null(svc.FindWireSize("Aluminum", "THHN", "1"));
    }

    [Fact]
    public void ComputeConductorAreaSquareFeet_AccumulatesAcrossConductors()
    {
        var svc = new WireSpecLibraryService(BuildConfig());
        var spec = svc.FindByFeederId("1P3W-CU-100A")!;

        var area = svc.ComputeConductorAreaSquareFeet(spec);

        // 2× "1" phase (D=0.040 ft) + 1× "1" neutral + 1× "6" ground (D=0.025 ft)
        // = π·(0.020)²·3 + π·(0.0125)²·1
        double expected = Math.PI * 0.020 * 0.020 * 3 + Math.PI * 0.0125 * 0.0125 * 1;
        Assert.Equal(expected, area, 9);
    }

    [Fact]
    public void ComputeConductorAreaSquareFeet_ParallelMultiplies()
    {
        var cfg = BuildConfig();
        cfg.WireSpecifications.Add(new WireSpecification
        {
            Name = "Test", FeederId = "Test-1000A", MaterialName = "Copper",
            Amperage = 1000, PhaseSize = "1", PhaseQuantity = 3,
            ParallelQuantity = 2, ConduitSizeFeet = 0.25,
        });
        var svc = new WireSpecLibraryService(cfg);
        var spec = svc.FindByFeederId("Test-1000A")!;

        var area = svc.ComputeConductorAreaSquareFeet(spec);

        double singleSet = Math.PI * 0.020 * 0.020 * 3;
        Assert.Equal(singleSet * 2, area, 9);
    }

    [Fact]
    public void BindToRun_NullArgs_Throw()
    {
        var svc = new WireSpecLibraryService(BuildConfig());
        var run = new ConduitRun();
        var spec = svc.FindByFeederId("1P3W-CU-100A")!;

        Assert.Throws<ArgumentNullException>(() => svc.BindToRun(null!, spec));
        Assert.Throws<ArgumentNullException>(() => svc.BindToRun(run, null!));
    }

    [Fact]
    public void BindToRun_PushesTradeSizeAndFeederMetadata()
    {
        var svc = new WireSpecLibraryService(BuildConfig());
        var run = new ConduitRun { RunId = "CR-001" };
        var spec = svc.FindByFeederId("1P3W-CU-200A")!;

        var result = svc.BindToRun(run, spec);

        Assert.Equal("2", run.TradeSize); // 0.16666... ft ≈ 2"
        Assert.Equal("1P3W-CU-200A", run.Metadata["FeederId"]);
        Assert.Equal("1P3W-CU", run.Metadata["FeederConfiguration"]);
        Assert.Equal("Copper", run.Metadata["FeederMaterial"]);
        Assert.Equal("1", run.Metadata["ParallelQty"]);
        Assert.Equal("False", run.Metadata["ParallelRun"]);
        Assert.Equal(spec, result.Spec);
        Assert.Equal("2", result.TradeSize);
    }

    [Fact]
    public void BindToRun_ParallelSpec_PushesParallelMetadata()
    {
        var svc = new WireSpecLibraryService(BuildConfig());
        var run = new ConduitRun();
        var spec = svc.FindByFeederId("3P4W-AL-600A")!;

        svc.BindToRun(run, spec);

        Assert.Equal("2", run.Metadata["ParallelQty"]);
        Assert.Equal("True", run.Metadata["ParallelRun"]);
    }

    [Fact]
    public void AutosizeAndBind_PicksFeederAndBindsRun()
    {
        var svc = new WireSpecLibraryService(BuildConfig());
        var run = new ConduitRun();

        var result = svc.AutosizeAndBind(run, "1P3W-CU", 150);

        Assert.NotNull(result);
        Assert.Equal(200, result!.Spec.Amperage);
        Assert.Equal("1P3W-CU-200A", run.Metadata["FeederId"]);
    }

    [Fact]
    public void AutosizeAndBind_NoMatch_ReturnsNull()
    {
        var svc = new WireSpecLibraryService(BuildConfig());
        var run = new ConduitRun();

        var result = svc.AutosizeAndBind(run, "1P3W-CU", 99999);

        Assert.Null(result);
    }

    [Fact]
    public void BuildDescription_HyphenFormat_StandardEvolveOutput()
    {
        var spec = new WireSpecification
        {
            PhaseQuantity = 2, PhaseSize = "1",
            NeutralQuantity = 1, NeutralSize = "1",
            GroundQuantity = 1, GroundSize = "6",
        };

        var desc = WireSpecLibraryService.BuildDescription(spec);

        Assert.Equal("2#1-1#1N-1#6G", desc);
    }

    [Fact]
    public void BuildDescription_PlusFormat_UsesPlusSeparator()
    {
        var spec = new WireSpecification
        {
            PhaseQuantity = 3, PhaseSize = "1/0",
            GroundQuantity = 1, GroundSize = "6",
        };

        var desc = WireSpecLibraryService.BuildDescription(spec, WireDescriptionFormat.Plus);

        Assert.Equal("3#1/0+1#6G", desc);
    }

    [Fact]
    public void BuildDescription_IncludesIsolatedGround()
    {
        var spec = new WireSpecification
        {
            PhaseQuantity = 2, PhaseSize = "1",
            IsoGroundQuantity = 1, IsoGroundSize = "10",
        };

        var desc = WireSpecLibraryService.BuildDescription(spec);

        Assert.Contains("1#10IG", desc);
    }

    [Theory]
    [InlineData(0.04, "1/2")]   // 0.5"
    [InlineData(0.0625, "3/4")] // 0.75"
    [InlineData(0.125, "1-1/2")] // 1.5"
    [InlineData(0.166666666, "2")] // 2"
    [InlineData(0.25, "3")]      // 3"
    [InlineData(0.333333333, "4")] // 4"
    public void ResolveTradeSize_CanonicalMapping(double feet, string expected)
    {
        Assert.Equal(expected, WireSpecLibraryService.ResolveTradeSize(feet));
    }

    [Fact]
    public void ResolveTradeSize_WithSizeTable_PicksClosestNominal()
    {
        var table = ConduitSizeSettings.CreateDefaultEMT();

        // 0.166... ft = 2", which exists in the EMT table
        var result = WireSpecLibraryService.ResolveTradeSize(0.16666666, table);

        Assert.Equal("2", result);
    }
}
