using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Models;

public class SwitchboardModelTests
{
    // ── PanelSubtype enum ────────────────────────────────────────

    [Theory]
    [InlineData(PanelSubtype.LoadCenter)]
    [InlineData(PanelSubtype.Panelboard)]
    [InlineData(PanelSubtype.Switchboard)]
    [InlineData(PanelSubtype.MCCSection)]
    [InlineData(PanelSubtype.TransferSwitch)]
    public void PanelSubtype_AllValues_Exist(PanelSubtype subtype)
    {
        Assert.True(Enum.IsDefined(subtype));
    }

    // ── PanelComponent defaults ──────────────────────────────────

    [Fact]
    public void PanelComponent_Subtype_DefaultsToLoadCenter()
    {
        var panel = new PanelComponent();
        Assert.Equal(PanelSubtype.LoadCenter, panel.Subtype);
    }

    [Fact]
    public void PanelComponent_HasFeedThruLugs_DefaultsFalse()
    {
        var panel = new PanelComponent();
        Assert.False(panel.HasFeedThruLugs);
    }

    [Fact]
    public void PanelComponent_MainLugOnly_DefaultsFalse()
    {
        var panel = new PanelComponent();
        Assert.False(panel.MainLugOnly);
    }

    [Fact]
    public void PanelComponent_AICRatingKA_DefaultsTen()
    {
        var panel = new PanelComponent();
        Assert.Equal(10.0, panel.AICRatingKA);
    }

    [Fact]
    public void PanelComponent_BusAmpacity_DefaultsTwoHundred()
    {
        var panel = new PanelComponent();
        Assert.Equal(200.0, panel.BusAmpacity);
    }

    // ── Switchboard configuration ────────────────────────────────

    [Fact]
    public void Switchboard_CanBeConfigured()
    {
        var swbd = new PanelComponent
        {
            Subtype = PanelSubtype.Switchboard,
            PanelType = "Switchboard",
            CircuitCount = 84,
            Amperage = 4000,
            BusAmpacity = 4000,
            AICRatingKA = 65.0,
            HasFeedThruLugs = true,
            MainLugOnly = false
        };

        Assert.Equal(PanelSubtype.Switchboard, swbd.Subtype);
        Assert.Equal(4000, swbd.BusAmpacity);
        Assert.Equal(65.0, swbd.AICRatingKA);
        Assert.True(swbd.HasFeedThruLugs);
    }

    [Fact]
    public void MLO_Panel_MainLugOnly_Replaces_StringCheck()
    {
        var mlo = new PanelComponent
        {
            MainLugOnly = true,
            Subtype = PanelSubtype.Panelboard
        };

        Assert.True(mlo.MainLugOnly);
        Assert.Equal(PanelSubtype.Panelboard, mlo.Subtype);
    }

    [Fact]
    public void MCCSection_CanBeModeled()
    {
        var mcc = new PanelComponent
        {
            Subtype = PanelSubtype.MCCSection,
            PanelType = "Motor Control Center",
            BusAmpacity = 800,
            AICRatingKA = 42.0
        };

        Assert.Equal(PanelSubtype.MCCSection, mcc.Subtype);
        Assert.Equal(800.0, mcc.BusAmpacity);
    }

    // ── Feed-through lugs ────────────────────────────────────────

    [Fact]
    public void FeedThruLugs_AllowsDownstreamFeed()
    {
        var upstream = new PanelComponent
        {
            Subtype = PanelSubtype.Switchboard,
            HasFeedThruLugs = true,
            BusAmpacity = 2000
        };
        var downstream = new PanelComponent
        {
            Subtype = PanelSubtype.Panelboard,
            FeederId = upstream.Id,
            BusAmpacity = 400
        };

        Assert.True(upstream.HasFeedThruLugs);
        Assert.Equal(upstream.Id, downstream.FeederId);
    }

    // ── AIC Rating values ────────────────────────────────────────

    [Theory]
    [InlineData(10.0)]
    [InlineData(14.0)]
    [InlineData(22.0)]
    [InlineData(25.0)]
    [InlineData(42.0)]
    [InlineData(65.0)]
    [InlineData(100.0)]
    public void AICRating_StandardValues(double kA)
    {
        var panel = new PanelComponent { AICRatingKA = kA };
        Assert.Equal(kA, panel.AICRatingKA);
    }

    // ── BusAmpacity separate from breaker ────────────────────────

    [Fact]
    public void BusAmpacity_IndependentOfAmperage()
    {
        var panel = new PanelComponent
        {
            Amperage = 200,      // main breaker trip amps
            BusAmpacity = 225    // bus rated higher
        };

        Assert.NotEqual(panel.Amperage, panel.BusAmpacity);
        Assert.Equal(225.0, panel.BusAmpacity);
    }

    // ── Distribution graph — switchboard awareness ───────────────

    [Fact]
    public void BuildGraph_SwitchboardNode_CarriesPanelSubtype()
    {
        var source = new PowerSourceComponent { Name = "Utility" };
        var swbd = new PanelComponent
        {
            Subtype = PanelSubtype.Switchboard,
            Name = "SWBD-A",
            FeederId = source.Id
        };

        var svc = new DistributionGraphService();
        var roots = svc.BuildGraph(new ElectricalComponent[] { source, swbd });

        Assert.Single(roots);
        Assert.Single(roots[0].Children);

        var swbdNode = roots[0].Children[0];
        Assert.Equal(PanelSubtype.Switchboard, swbdNode.PanelSubtype);
        Assert.Equal(ComponentType.Panel, swbdNode.NodeType);
    }

    [Fact]
    public void BuildGraph_NonPanelNode_PanelSubtypeNull()
    {
        var source = new PowerSourceComponent { Name = "Gen" };

        var svc = new DistributionGraphService();
        var roots = svc.BuildGraph(new ElectricalComponent[] { source });

        Assert.Single(roots);
        Assert.Null(roots[0].PanelSubtype);
    }

    [Fact]
    public void FeederSchedule_SwitchboardEntry_HasSubtype()
    {
        var source = new PowerSourceComponent { Name = "Utility" };
        var swbd = new PanelComponent
        {
            Subtype = PanelSubtype.Switchboard,
            Name = "SWBD-A",
            FeederId = source.Id
        };

        var svc = new DistributionGraphService();
        var roots = svc.BuildGraph(new ElectricalComponent[] { source, swbd });
        var entries = svc.GenerateFeederSchedule(roots);

        Assert.Equal(2, entries.Count);
        var swbdEntry = entries[1];
        Assert.Equal(PanelSubtype.Switchboard, swbdEntry.PanelSubtype);
        Assert.Equal("SWBD-A", swbdEntry.Name);
    }

    [Fact]
    public void FeederSchedule_LoadCenter_HasSubtype()
    {
        var source = new PowerSourceComponent { Name = "Utility" };
        var panel = new PanelComponent
        {
            Subtype = PanelSubtype.LoadCenter,
            Name = "LP-1",
            FeederId = source.Id
        };

        var svc = new DistributionGraphService();
        var roots = svc.BuildGraph(new ElectricalComponent[] { source, panel });
        var entries = svc.GenerateFeederSchedule(roots);

        Assert.Equal(PanelSubtype.LoadCenter, entries[1].PanelSubtype);
    }

    // ── Full hierarchy: source → switchboard → panelboards ───────

    [Fact]
    public void FullHierarchy_SwitchboardWithFeedThru_DownstreamPanels()
    {
        var source = new PowerSourceComponent
        {
            Name = "Utility",
            AvailableFaultCurrentKA = 65.0
        };
        var swbd = new PanelComponent
        {
            Subtype = PanelSubtype.Switchboard,
            Name = "SWBD-A",
            FeederId = source.Id,
            HasFeedThruLugs = true,
            BusAmpacity = 2000,
            AICRatingKA = 65.0
        };
        var panel1 = new PanelComponent
        {
            Subtype = PanelSubtype.Panelboard,
            Name = "LP-1",
            FeederId = swbd.Id,
            BusAmpacity = 225
        };
        var panel2 = new PanelComponent
        {
            Subtype = PanelSubtype.Panelboard,
            Name = "LP-2",
            FeederId = swbd.Id,
            BusAmpacity = 225,
            MainLugOnly = true
        };

        var svc = new DistributionGraphService();
        var roots = svc.BuildGraph(new ElectricalComponent[] { source, swbd, panel1, panel2 });

        // Source is root, switchboard is child, two panels under switchboard
        Assert.Single(roots);
        var sourceNode = roots[0];
        Assert.Single(sourceNode.Children);

        var swbdNode = sourceNode.Children[0];
        Assert.Equal(PanelSubtype.Switchboard, swbdNode.PanelSubtype);
        Assert.Equal(2, swbdNode.Children.Count);

        // Panels carry their subtypes
        Assert.All(swbdNode.Children, child =>
            Assert.Equal(PanelSubtype.Panelboard, child.PanelSubtype));

        // Fault current propagates through
        svc.PropagateFaultCurrent(roots);
        Assert.Equal(65.0, swbdNode.FaultCurrentKA);
        Assert.All(swbdNode.Children, child =>
            Assert.Equal(65.0, child.FaultCurrentKA));
    }

    [Fact]
    public void FeederSchedule_FullHierarchy_DepthCorrect()
    {
        var source = new PowerSourceComponent { Name = "Utility" };
        var swbd = new PanelComponent
        {
            Subtype = PanelSubtype.Switchboard,
            Name = "SWBD",
            FeederId = source.Id
        };
        var panel = new PanelComponent
        {
            Subtype = PanelSubtype.LoadCenter,
            Name = "LC-1",
            FeederId = swbd.Id
        };

        var svc = new DistributionGraphService();
        var roots = svc.BuildGraph(new ElectricalComponent[] { source, swbd, panel });
        var entries = svc.GenerateFeederSchedule(roots);

        Assert.Equal(3, entries.Count);
        Assert.Equal(0, entries[0].Depth); // Utility
        Assert.Equal(1, entries[1].Depth); // SWBD
        Assert.Equal(2, entries[2].Depth); // LC-1
        Assert.Equal(PanelSubtype.Switchboard, entries[1].PanelSubtype);
        Assert.Equal(PanelSubtype.LoadCenter, entries[2].PanelSubtype);
        Assert.Null(entries[0].PanelSubtype); // PowerSource
    }

    // ── Subtype persistence (round-trip) ─────────────────────────

    [Theory]
    [InlineData(PanelSubtype.LoadCenter)]
    [InlineData(PanelSubtype.Panelboard)]
    [InlineData(PanelSubtype.Switchboard)]
    [InlineData(PanelSubtype.MCCSection)]
    [InlineData(PanelSubtype.TransferSwitch)]
    public void Subtype_Persists_AllValues(PanelSubtype subtype)
    {
        var panel = new PanelComponent { Subtype = subtype };
        Assert.Equal(subtype, panel.Subtype);
    }

    [Fact]
    public void AllNewProperties_Persist()
    {
        var panel = new PanelComponent
        {
            Subtype = PanelSubtype.Switchboard,
            HasFeedThruLugs = true,
            MainLugOnly = true,
            AICRatingKA = 42.0,
            BusAmpacity = 1600.0
        };

        Assert.Equal(PanelSubtype.Switchboard, panel.Subtype);
        Assert.True(panel.HasFeedThruLugs);
        Assert.True(panel.MainLugOnly);
        Assert.Equal(42.0, panel.AICRatingKA);
        Assert.Equal(1600.0, panel.BusAmpacity);
    }

    // ── Backward compatibility ───────────────────────────────────

    [Fact]
    public void ExistingPanelType_String_StillWorks()
    {
        // The string PanelType property still coexists with the enum Subtype
        var panel = new PanelComponent
        {
            PanelType = "Switchboard",
            Subtype = PanelSubtype.Switchboard
        };

        Assert.Equal("Switchboard", panel.PanelType);
        Assert.Equal(PanelSubtype.Switchboard, panel.Subtype);
    }

    [Fact]
    public void DefaultPanel_LegacyDefaults_Unchanged()
    {
        // Verify we didn't break existing defaults
        var panel = new PanelComponent();

        Assert.Equal(ComponentType.Panel, panel.Type);
        Assert.Equal("Electrical Panel", panel.Name);
        Assert.Equal(24, panel.CircuitCount);
        Assert.Equal(200.0, panel.Amperage);
        Assert.Equal("Distribution Panel", panel.PanelType);
        Assert.Null(panel.FeederId);
    }
}
