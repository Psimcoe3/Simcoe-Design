using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Conduit.Core.Routing;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class RoutingPreferenceManagerTests
{
    // ── Angle Settings ──────────────────────────────────────────

    [Fact]
    public void DefaultPermittedAngles_AreNecUlStandard()
    {
        var s = new ConduitFittingAngleSettings();
        Assert.Equal(new[] { 22.5, 30, 45, 60, 90 }, s.PermittedAngles);
    }

    [Theory]
    [InlineData(22.5, true)]
    [InlineData(30, true)]
    [InlineData(45, true)]
    [InlineData(60, true)]
    [InlineData(90, true)]
    [InlineData(15, false)]
    [InlineData(75, false)]
    [InlineData(0, false)]
    public void IsPermitted_DefaultAngles(double angle, bool expected)
    {
        var s = new ConduitFittingAngleSettings();
        Assert.Equal(expected, s.IsPermitted(angle));
    }

    [Fact]
    public void IsPermitted_WhenEnforcementDisabled_AlwaysTrue()
    {
        var s = new ConduitFittingAngleSettings { EnforceAngles = false };
        Assert.True(s.IsPermitted(17));
    }

    [Theory]
    [InlineData(20, 22.5)]
    [InlineData(25, 22.5)]
    [InlineData(27, 30)]
    [InlineData(50, 45)]
    [InlineData(55, 60)]
    [InlineData(80, 90)]
    [InlineData(10, 22.5)]
    public void SnapToNearest_DefaultAngles(double input, double expected)
    {
        var s = new ConduitFittingAngleSettings();
        Assert.Equal(expected, s.SnapToNearest(input));
    }

    [Fact]
    public void SnapToNearest_WhenEnforcementDisabled_ReturnsInput()
    {
        var s = new ConduitFittingAngleSettings { EnforceAngles = false };
        Assert.Equal(17, s.SnapToNearest(17));
    }

    [Fact]
    public void CreateCustom_SortsAngles()
    {
        var s = ConduitFittingAngleSettings.CreateCustom(new[] { 90.0, 30, 45 });
        Assert.Equal(new[] { 30.0, 45, 90 }, s.PermittedAngles);
    }

    [Fact]
    public void CustomAngleSet_EnforcesNewValues()
    {
        var s = ConduitFittingAngleSettings.CreateCustom(new[] { 15.0, 30.0, 90.0 });
        Assert.True(s.IsPermitted(15));
        Assert.False(s.IsPermitted(45));
        Assert.Equal(30, s.SnapToNearest(25));
    }

    // ── SmartBendService angle helpers ──────────────────────────

    [Fact]
    public void ValidateAngle_PermittedAngle_ReturnsUnchanged()
    {
        var s = new ConduitFittingAngleSettings();
        Assert.Equal(45, SmartBendService.ValidateAngle(45, s));
    }

    [Fact]
    public void ValidateAngle_NonPermitted_SnapsToNearest()
    {
        var s = new ConduitFittingAngleSettings();
        Assert.Equal(45, SmartBendService.ValidateAngle(42, s));
    }

    [Fact]
    public void SnapAngle_DelegatesToSettings()
    {
        var s = new ConduitFittingAngleSettings();
        Assert.Equal(90, SmartBendService.SnapAngle(85, s));
    }

    // ── RoutingPreferenceRuleGroup ──────────────────────────────

    [Fact]
    public void RoutingPreferenceRule_DefaultGroup_IsElbows()
    {
        var rule = new RoutingPreferenceRule();
        Assert.Equal(RoutingPreferenceRuleGroup.Elbows, rule.Group);
    }

    [Fact]
    public void RoutingPreferenceRule_FamilyTypeId_NullByDefault()
    {
        var rule = new RoutingPreferenceRule();
        Assert.Null(rule.FamilyTypeId);
    }

    [Fact]
    public void GetRulesForGroup_FiltersCorrectly()
    {
        var ct = new ConduitType();
        // Tag the 90° elbow as Elbows (default), add a Transition rule
        ct.RoutingPreferences.Add(new RoutingPreferenceRule
        {
            MinAngleDegrees = 0,
            MaxAngleDegrees = 180,
            FittingType = FittingType.Transition,
            Group = RoutingPreferenceRuleGroup.Transitions
        });

        var mgr = CreateManager(ct);
        var elbows = mgr.GetRulesForGroup(RoutingPreferenceRuleGroup.Elbows);
        var transitions = mgr.GetRulesForGroup(RoutingPreferenceRuleGroup.Transitions);

        // Default conduit type has Elbow90, Elbow45, 2×Coupling — all default Elbows group
        Assert.Equal(4, elbows.Count);
        Assert.Single(transitions);
    }

    // ── SelectFitting ───────────────────────────────────────────

    [Fact]
    public void SelectFitting_ExactAngle_MatchesRule()
    {
        var mgr = CreateManager();
        Assert.Equal(FittingType.Elbow90, mgr.SelectFitting(90));
    }

    [Fact]
    public void SelectFitting_SnapsAngleThenMatches()
    {
        // 85° is not permitted; snaps to 90° → Elbow90 rule (80-100)
        var mgr = CreateManager();
        Assert.Equal(FittingType.Elbow90, mgr.SelectFitting(85));
    }

    [Fact]
    public void SelectFitting_AngleInGap_FallbackUsed()
    {
        // 25° snaps to 22.5° (default NEC); no rule covers 22.5° so fallback applies → Elbow45
        var mgr = CreateManager();
        Assert.Equal(FittingType.Elbow45, mgr.SelectFitting(25));
    }

    [Fact]
    public void SelectFitting_WithEnforcementOff_UsesRawAngle()
    {
        var settings = new ConduitFittingAngleSettings { EnforceAngles = false };
        var mgr = CreateManager(settings: settings);
        // 90° matches the Elbow90 rule directly
        Assert.Equal(FittingType.Elbow90, mgr.SelectFitting(90));
    }

    // ── GetFittingForAngle (family type resolution) ─────────────

    [Fact]
    public void GetFittingForAngle_NoFamilyTypeLinked_ReturnsNull()
    {
        var mgr = CreateManager();
        Assert.Null(mgr.GetFittingForAngle(90));
    }

    [Fact]
    public void GetFittingForAngle_ResolvesFamilyType()
    {
        var familyType = new ComponentFamilyType
        {
            Id = "elbow90-type",
            Name = "EMT Elbow 90"
        };
        var family = new ComponentFamily
        {
            Name = "Conduit Fittings",
            Types = { familyType }
        };

        var ct = new ConduitType();
        // Wire the 80-100 rule to our family type
        ct.RoutingPreferences[0].FamilyTypeId = familyType.Id;

        var settings = new ConduitFittingAngleSettings();
        var mgr = new RoutingPreferenceManager(ct, new[] { family }, settings);

        var resolved = mgr.GetFittingForAngle(90);
        Assert.NotNull(resolved);
        Assert.Equal("EMT Elbow 90", resolved.Name);
    }

    [Fact]
    public void GetFittingForAngle_SnapsToPermittedThenResolves()
    {
        var familyType = new ComponentFamilyType
        {
            Id = "elbow90-type",
            Name = "EMT Elbow 90"
        };
        var family = new ComponentFamily
        {
            Name = "Conduit Fittings",
            Types = { familyType }
        };

        var ct = new ConduitType();
        ct.RoutingPreferences[0].FamilyTypeId = familyType.Id;

        var settings = new ConduitFittingAngleSettings();
        var mgr = new RoutingPreferenceManager(ct, new[] { family }, settings);

        // 85° snaps to 90° → resolved from catalog
        var resolved = mgr.GetFittingForAngle(85);
        Assert.NotNull(resolved);
        Assert.Equal("EMT Elbow 90", resolved.Name);
    }

    [Fact]
    public void GetFittingForAngle_FallbackRule_ResolvesFamilyType()
    {
        var familyType = new ComponentFamilyType
        {
            Id = "elbow45-type",
            Name = "EMT Elbow 45"
        };
        var family = new ComponentFamily
        {
            Name = "Conduit Fittings",
            Types = { familyType }
        };

        var ct = new ConduitType();
        // Wire the 35-55 rule (Elbow45) to our family type
        ct.RoutingPreferences[1].FamilyTypeId = familyType.Id;

        var settings = new ConduitFittingAngleSettings();
        var mgr = new RoutingPreferenceManager(ct, new[] { family }, settings);

        // 25° snaps to 22.5° → no rule → fallback Elbow45 → find first rule with Elbow45 → resolve
        var resolved = mgr.GetFittingForAngle(25);
        Assert.NotNull(resolved);
        Assert.Equal("EMT Elbow 45", resolved.Name);
    }

    // ── ProjectModel integration ────────────────────────────────

    [Fact]
    public void ProjectModel_HasFittingAngleSettings()
    {
        var pm = new ProjectModel();
        Assert.NotNull(pm.FittingAngleSettings);
        Assert.Equal(5, pm.FittingAngleSettings.PermittedAngles.Count);
    }

    [Fact]
    public void AngleSettings_PropertyExposedOnManager()
    {
        var settings = ConduitFittingAngleSettings.CreateCustom(new[] { 30.0, 60.0 });
        var mgr = CreateManager(settings: settings);
        Assert.Equal(new[] { 30.0, 60.0 }, mgr.AngleSettings.PermittedAngles);
    }

    // ── helpers ──────────────────────────────────────────────────

    private static RoutingPreferenceManager CreateManager(
        ConduitType? conduitType = null,
        ConduitFittingAngleSettings? settings = null)
    {
        return new RoutingPreferenceManager(
            conduitType ?? new ConduitType(),
            Array.Empty<ComponentFamily>(),
            settings ?? new ConduitFittingAngleSettings());
    }
}
