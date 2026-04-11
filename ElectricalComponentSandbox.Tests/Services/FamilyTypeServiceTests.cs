using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class FamilyTypeServiceTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>Test-only component that leaves all parameters at their defaults.</summary>
    private class TestComponent : ElectricalComponent
    {
        public TestComponent() { Type = ComponentType.Box; }
    }

    private static ComponentFamily MakeBoxFamily(out ComponentFamilyType typeA, out ComponentFamilyType typeB)
    {
        typeA = new ComponentFamilyType
        {
            Id = "type-a",
            Name = "4\" Square",
            FamilyId = "fam-box",
            ParameterOverrides = new()
            {
                ["Width"] = "4",
                ["Height"] = "4",
                ["Depth"] = "1.5",
                ["Material"] = "Steel",
                ["Manufacturer"] = "Raco"
            },
            Connectors = new()
            {
                new ConnectorDefinition
                {
                    Name = "Top",
                    Domain = ConnectorDomain.Conduit,
                    Flow = ConnectorFlow.In,
                    OffsetY = 2
                }
            }
        };

        typeB = new ComponentFamilyType
        {
            Id = "type-b",
            Name = "4-11/16\" Square",
            FamilyId = "fam-box",
            ParameterOverrides = new()
            {
                ["Width"] = "4.6875",
                ["Height"] = "4.6875",
                ["Depth"] = "2.125",
                ["Manufacturer"] = "Eaton"
            }
        };

        return new ComponentFamily
        {
            Id = "fam-box",
            Name = "Junction Box",
            Category = ComponentType.Box,
            DefaultTypeId = "type-a",
            IsBuiltIn = true,
            Types = { typeA, typeB }
        };
    }

    private static TestComponent MakeInstance(string familyTypeId)
    {
        return new TestComponent
        {
            Id = "inst-1",
            FamilyTypeId = familyTypeId
        };
    }

    // ── Type parameter inheritance ───────────────────────────────────────

    [Fact]
    public void ApplyTypeDefaults_SetsParametersFromType()
    {
        var family = MakeBoxFamily(out var typeA, out _);
        var instance = MakeInstance("type-a");

        FamilyTypeService.ApplyTypeDefaults(instance, new[] { family });

        Assert.Equal(4.0, instance.Parameters.Width);
        Assert.Equal(4.0, instance.Parameters.Height);
        Assert.Equal(1.5, instance.Parameters.Depth);
        Assert.Equal("Raco", instance.Parameters.Manufacturer);
    }

    [Fact]
    public void ApplyTypeDefaults_DifferentType_DifferentValues()
    {
        var family = MakeBoxFamily(out _, out var typeB);
        var instance = MakeInstance("type-b");

        FamilyTypeService.ApplyTypeDefaults(instance, new[] { family });

        Assert.Equal(4.6875, instance.Parameters.Width);
        Assert.Equal(4.6875, instance.Parameters.Height);
        Assert.Equal(2.125, instance.Parameters.Depth);
        Assert.Equal("Eaton", instance.Parameters.Manufacturer);
    }

    [Fact]
    public void ApplyTypeDefaults_NoFamilyTypeId_DoesNothing()
    {
        var family = MakeBoxFamily(out _, out _);
        var instance = new TestComponent(); // no FamilyTypeId

        FamilyTypeService.ApplyTypeDefaults(instance, new[] { family });

        // Defaults unchanged
        Assert.Equal(1.0, instance.Parameters.Width);
    }

    [Fact]
    public void ApplyTypeDefaults_UnknownTypeId_DoesNothing()
    {
        var family = MakeBoxFamily(out _, out _);
        var instance = MakeInstance("nonexistent");

        FamilyTypeService.ApplyTypeDefaults(instance, new[] { family });

        Assert.Equal(1.0, instance.Parameters.Width);
    }

    // ── Per-instance override ────────────────────────────────────────────

    [Fact]
    public void ApplyTypeDefaults_DoesNotOverrideInstanceValues()
    {
        var family = MakeBoxFamily(out _, out _);
        var instance = MakeInstance("type-a");

        // Set instance-specific width before applying type defaults
        instance.Parameters.Width = 6.0;

        FamilyTypeService.ApplyTypeDefaults(instance, new[] { family });

        // Width stays at instance value; other params get type values
        Assert.Equal(6.0, instance.Parameters.Width);
        Assert.Equal(4.0, instance.Parameters.Height);
    }

    [Fact]
    public void ResolveParameter_InstanceOverrideTakesPrecedence()
    {
        var family = MakeBoxFamily(out _, out _);
        var instance = MakeInstance("type-a");
        instance.Parameters.Width = 6.0;

        var result = FamilyTypeService.ResolveParameter(instance, "Width", new[] { family });

        Assert.Equal("6", result);
    }

    [Fact]
    public void ResolveParameter_FallsBackToTypeOverride()
    {
        var family = MakeBoxFamily(out _, out _);
        var instance = MakeInstance("type-a");
        // Width is default (1.0), so type override applies

        var result = FamilyTypeService.ResolveParameter(instance, "Manufacturer", new[] { family });

        Assert.Equal("Raco", result);
    }

    [Fact]
    public void ResolveParameter_NoTypeNoInstance_ReturnsEmpty()
    {
        var instance = new TestComponent(); // no FamilyTypeId
        var result = FamilyTypeService.ResolveParameter(instance, "Manufacturer", Array.Empty<ComponentFamily>());
        Assert.Equal(string.Empty, result);
    }

    // ── Connector definitions ────────────────────────────────────────────

    [Fact]
    public void GetConnectors_ReturnsTypeConnectors()
    {
        var family = MakeBoxFamily(out _, out _);
        var instance = MakeInstance("type-a");

        var connectors = FamilyTypeService.GetConnectors(instance, new[] { family });

        Assert.Single(connectors);
        Assert.Equal("Top", connectors[0].Name);
        Assert.Equal(ConnectorDomain.Conduit, connectors[0].Domain);
        Assert.Equal(ConnectorFlow.In, connectors[0].Flow);
        Assert.Equal(2, connectors[0].OffsetY);
    }

    [Fact]
    public void GetConnectors_TypeWithNoConnectors_ReturnsEmpty()
    {
        var family = MakeBoxFamily(out _, out var typeB);
        var instance = MakeInstance("type-b");

        var connectors = FamilyTypeService.GetConnectors(instance, new[] { family });

        Assert.Empty(connectors);
    }

    [Fact]
    public void GetConnectors_NoFamilyTypeId_ReturnsEmpty()
    {
        var instance = new TestComponent();
        var connectors = FamilyTypeService.GetConnectors(instance, Array.Empty<ComponentFamily>());
        Assert.Empty(connectors);
    }

    [Fact]
    public void ConnectorDefinition_Defaults()
    {
        var cd = new ConnectorDefinition();
        Assert.Equal(ConnectorDomain.Electrical, cd.Domain);
        Assert.Equal(ConnectorFlow.Bidirectional, cd.Flow);
        Assert.Equal(0, cd.OffsetX);
        Assert.Equal(0, cd.OffsetY);
        Assert.Equal(0, cd.OffsetZ);
    }

    // ── In-use type guard ────────────────────────────────────────────────

    [Fact]
    public void IsTypeInUse_TypeUsedByComponent_ReturnsTrue()
    {
        var instance = MakeInstance("type-a");
        Assert.True(FamilyTypeService.IsTypeInUse("type-a", new[] { instance }));
    }

    [Fact]
    public void IsTypeInUse_TypeNotUsed_ReturnsFalse()
    {
        var instance = MakeInstance("type-a");
        Assert.False(FamilyTypeService.IsTypeInUse("type-b", new[] { instance }));
    }

    [Fact]
    public void IsTypeInUse_EmptyComponents_ReturnsFalse()
    {
        Assert.False(FamilyTypeService.IsTypeInUse("type-a", Array.Empty<ElectricalComponent>()));
    }

    // ── FindType / FindFamilyForType ─────────────────────────────────────

    [Fact]
    public void FindType_ReturnsCorrectType()
    {
        var family = MakeBoxFamily(out var typeA, out _);
        var found = FamilyTypeService.FindType("type-a", new[] { family });
        Assert.NotNull(found);
        Assert.Equal("4\" Square", found!.Name);
    }

    [Fact]
    public void FindType_NotFound_ReturnsNull()
    {
        var family = MakeBoxFamily(out _, out _);
        Assert.Null(FamilyTypeService.FindType("nonexistent", new[] { family }));
    }

    [Fact]
    public void FindFamilyForType_ReturnsCorrectFamily()
    {
        var family = MakeBoxFamily(out _, out _);
        var found = FamilyTypeService.FindFamilyForType("type-b", new[] { family });
        Assert.NotNull(found);
        Assert.Equal("Junction Box", found!.Name);
    }

    [Fact]
    public void FindFamilyForType_NotFound_ReturnsNull()
    {
        var family = MakeBoxFamily(out _, out _);
        Assert.Null(FamilyTypeService.FindFamilyForType("nonexistent", new[] { family }));
    }

    // ── ComponentFamily model defaults ───────────────────────────────────

    [Fact]
    public void ComponentFamily_Defaults()
    {
        var family = new ComponentFamily();
        Assert.NotEmpty(family.Id);
        Assert.Equal(string.Empty, family.Name);
        Assert.Equal(ComponentType.Conduit, family.Category); // default enum value
        Assert.Empty(family.Types);
        Assert.False(family.IsBuiltIn);
    }

    [Fact]
    public void ComponentFamilyType_Defaults()
    {
        var type = new ComponentFamilyType();
        Assert.NotEmpty(type.Id);
        Assert.Equal(string.Empty, type.Name);
        Assert.Empty(type.ParameterOverrides);
        Assert.Empty(type.Connectors);
    }

    // ── FamilyTypeId on ElectricalComponent ──────────────────────────────

    [Fact]
    public void ElectricalComponent_FamilyTypeId_DefaultsToNull()
    {
        var comp = new TestComponent();
        Assert.Null(comp.FamilyTypeId);
    }

    [Fact]
    public void ElectricalComponent_FamilyTypeId_RoundTrip()
    {
        var comp = new TestComponent { FamilyTypeId = "type-a" };
        Assert.Equal("type-a", comp.FamilyTypeId);
    }

    // ── Multiple families across different categories ────────────────────

    [Fact]
    public void FindType_SearchesAcrossMultipleFamilies()
    {
        var boxFamily = MakeBoxFamily(out _, out _);
        var panelType = new ComponentFamilyType
        {
            Id = "panel-type-1",
            Name = "200A Panel",
            FamilyId = "fam-panel",
            ParameterOverrides = new() { ["Width"] = "20" }
        };
        var panelFamily = new ComponentFamily
        {
            Id = "fam-panel",
            Name = "Electrical Panel",
            Category = ComponentType.Panel,
            Types = { panelType }
        };

        var families = new[] { boxFamily, panelFamily };
        var found = FamilyTypeService.FindType("panel-type-1", families);
        Assert.NotNull(found);
        Assert.Equal("200A Panel", found!.Name);
    }
}
