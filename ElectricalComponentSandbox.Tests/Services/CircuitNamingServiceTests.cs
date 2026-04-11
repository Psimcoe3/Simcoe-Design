using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class CircuitNamingServiceTests
{
    private static Circuit MakeCircuit(string number = "1", string phase = "A", string description = "Lighting")
        => new()
        {
            CircuitNumber = number,
            Phase = phase,
            Description = description,
            Voltage = 120,
            ConnectedLoadVA = 1800,
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 }
        };

    private static PanelSchedule MakePanel(string name = "LP-1")
        => new() { PanelName = name };

    // ── Null / empty scheme falls back to CircuitNumber ──────────────────

    [Fact]
    public void FormatCircuitName_NullScheme_ReturnsCircuitNumber()
    {
        var circuit = MakeCircuit("5");
        var result = CircuitNamingService.FormatCircuitName(circuit, MakePanel(), null);
        Assert.Equal("5", result);
    }

    [Fact]
    public void FormatCircuitName_EmptyTokens_ReturnsCircuitNumber()
    {
        var scheme = new CircuitNamingScheme { Tokens = new() };
        var result = CircuitNamingService.FormatCircuitName(MakeCircuit("7"), MakePanel(), scheme);
        Assert.Equal("7", result);
    }

    // ── Built-in: Numerical ──────────────────────────────────────────────

    [Fact]
    public void Numerical_ReturnsBareCircuitNumber()
    {
        var scheme = CircuitNamingScheme.Numerical();
        var result = CircuitNamingService.FormatCircuitName(MakeCircuit("3"), MakePanel(), scheme);
        Assert.Equal("3", result);
    }

    [Fact]
    public void Numerical_MultiPole_PreservesComma()
    {
        var scheme = CircuitNamingScheme.Numerical();
        var circuit = MakeCircuit("1,3");
        var result = CircuitNamingService.FormatCircuitName(circuit, MakePanel(), scheme);
        Assert.Equal("1,3", result);
    }

    // ── Built-in: Panel + Number ─────────────────────────────────────────

    [Fact]
    public void PanelPrefixNumber_FormatsCorrectly()
    {
        var scheme = CircuitNamingScheme.PanelPrefixNumber();
        var result = CircuitNamingService.FormatCircuitName(MakeCircuit("1"), MakePanel("LP-1"), scheme);
        Assert.Equal("LP-1-1", result);
    }

    [Fact]
    public void PanelPrefixNumber_DifferentPanel()
    {
        var scheme = CircuitNamingScheme.PanelPrefixNumber();
        var result = CircuitNamingService.FormatCircuitName(MakeCircuit("12"), MakePanel("MDP"), scheme);
        Assert.Equal("MDP-12", result);
    }

    // ── Built-in: Phase + Number ─────────────────────────────────────────

    [Fact]
    public void PhaseNumber_SinglePhase()
    {
        var scheme = CircuitNamingScheme.PhaseNumber();
        var result = CircuitNamingService.FormatCircuitName(MakeCircuit("5", phase: "B"), MakePanel(), scheme);
        Assert.Equal("B-5", result);
    }

    [Fact]
    public void PhaseNumber_MultiPhase()
    {
        var scheme = CircuitNamingScheme.PhaseNumber();
        var result = CircuitNamingService.FormatCircuitName(MakeCircuit("7,9", phase: "AB"), MakePanel(), scheme);
        Assert.Equal("AB-7,9", result);
    }

    // ── Built-in: Full ───────────────────────────────────────────────────

    [Fact]
    public void Full_AllTokens()
    {
        var scheme = CircuitNamingScheme.Full();
        var result = CircuitNamingService.FormatCircuitName(
            MakeCircuit("1", "A", "Lighting 2nd Floor"),
            MakePanel("LP1"),
            scheme);
        Assert.Equal("LP1-A-1-Lighting 2nd Floor", result);
    }

    [Fact]
    public void Full_EmptyDescription_SkipsTrailingSeparator()
    {
        var scheme = CircuitNamingScheme.Full();
        var result = CircuitNamingService.FormatCircuitName(
            MakeCircuit("1", "A", ""),
            MakePanel("LP1"),
            scheme);
        Assert.Equal("LP1-A-1", result);
    }

    // ── Custom scheme: Prefix + Number + Suffix ──────────────────────────

    [Fact]
    public void Custom_PrefixSuffix()
    {
        var scheme = new CircuitNamingScheme
        {
            Name = "Custom",
            Tokens = new()
            {
                new() { Token = NamingToken.Prefix, Literal = "CKT", Separator = "-" },
                new() { Token = NamingToken.CircuitNumber, Separator = "-" },
                new() { Token = NamingToken.Suffix, Literal = "E", Separator = "" }
            }
        };
        var result = CircuitNamingService.FormatCircuitName(MakeCircuit("14"), MakePanel(), scheme);
        Assert.Equal("CKT-14-E", result);
    }

    [Fact]
    public void Custom_ReorderedTokens()
    {
        // Load Name first, then circuit number
        var scheme = new CircuitNamingScheme
        {
            Name = "DescFirst",
            Tokens = new()
            {
                new() { Token = NamingToken.LoadName, Separator = " #" },
                new() { Token = NamingToken.CircuitNumber, Separator = "" }
            }
        };
        var result = CircuitNamingService.FormatCircuitName(
            MakeCircuit("3", description: "HVAC"),
            MakePanel(),
            scheme);
        Assert.Equal("HVAC #3", result);
    }

    // ── FormatAll batch ──────────────────────────────────────────────────

    [Fact]
    public void FormatAll_ReturnsAllCircuits()
    {
        var panel = MakePanel("LP-1");
        var c1 = MakeCircuit("1");
        c1.Id = "ckt1";
        var c2 = MakeCircuit("2");
        c2.Id = "ckt2";
        panel.Circuits.AddRange(new[] { c1, c2 });

        var scheme = CircuitNamingScheme.PanelPrefixNumber();
        var names = CircuitNamingService.FormatAll(panel, scheme);

        Assert.Equal(2, names.Count);
        Assert.Equal("LP-1-1", names["ckt1"]);
        Assert.Equal("LP-1-2", names["ckt2"]);
    }

    // ── Scheme switch preserves underlying CircuitNumber ─────────────────

    [Fact]
    public void SchemeSwitchPreservesUnderlyingNumber()
    {
        var circuit = MakeCircuit("5", "B", "Receptacles");
        var panel = MakePanel("HP-2");

        // Numerical
        Assert.Equal("5", CircuitNamingService.FormatCircuitName(circuit, panel, CircuitNamingScheme.Numerical()));
        // Panel+Number
        Assert.Equal("HP-2-5", CircuitNamingService.FormatCircuitName(circuit, panel, CircuitNamingScheme.PanelPrefixNumber()));
        // Phase+Number
        Assert.Equal("B-5", CircuitNamingService.FormatCircuitName(circuit, panel, CircuitNamingScheme.PhaseNumber()));

        // Underlying number unchanged through all scheme formats
        Assert.Equal("5", circuit.CircuitNumber);
    }

    // ── Built-in scheme metadata ─────────────────────────────────────────

    [Fact]
    public void GetBuiltInSchemes_ReturnsFour()
    {
        var schemes = CircuitNamingScheme.GetBuiltInSchemes();
        Assert.Equal(4, schemes.Count);
        Assert.All(schemes, s => Assert.True(s.IsBuiltIn));
    }

    [Fact]
    public void BuiltInSchemes_HaveUniqueIds()
    {
        var schemes = CircuitNamingScheme.GetBuiltInSchemes();
        var ids = schemes.Select(s => s.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void BuiltInSchemes_HaveNames()
    {
        var schemes = CircuitNamingScheme.GetBuiltInSchemes();
        Assert.All(schemes, s => Assert.False(string.IsNullOrWhiteSpace(s.Name)));
    }

    // ── Schedule table integration ───────────────────────────────────────

    [Fact]
    public void PanelScheduleTable_UsesNamingScheme_InLeftColumn()
    {
        var panel = new PanelSchedule
        {
            PanelName = "LP-1",
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    Phase = "A",
                    Description = "Lighting",
                    Voltage = 120,
                    ConnectedLoadVA = 1800,
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 }
                }
            }
        };

        var svc = new ScheduleTableService();
        var scheme = CircuitNamingScheme.PanelPrefixNumber();
        var table = svc.GeneratePanelSchedule(panel, namingScheme: scheme);

        // Row 0 = subheader, Row 1 = first circuit row
        // Left column CKT cell (index 0) should have the formatted name
        Assert.Equal("LP-1-1", table.Rows[1][0]);
    }

    [Fact]
    public void PanelScheduleTable_NoScheme_UsesCircuitNumber()
    {
        var panel = new PanelSchedule
        {
            PanelName = "LP-1",
            Circuits =
            {
                new Circuit
                {
                    CircuitNumber = "1",
                    Phase = "A",
                    Description = "Lighting",
                    Voltage = 120,
                    ConnectedLoadVA = 1800,
                    Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 }
                }
            }
        };

        var svc = new ScheduleTableService();
        var table = svc.GeneratePanelSchedule(panel);

        // Row 0 = subheader, Row 1 = first circuit row
        Assert.Equal("1", table.Rows[1][0]);
    }

    // ── NamingTokenEntry defaults ────────────────────────────────────────

    [Fact]
    public void NamingTokenEntry_DefaultSeparator_IsDash()
    {
        var entry = new NamingTokenEntry { Token = NamingToken.CircuitNumber };
        Assert.Equal("-", entry.Separator);
    }

    [Fact]
    public void NamingTokenEntry_PrefixLiteral_IsNull_ByDefault()
    {
        var entry = new NamingTokenEntry { Token = NamingToken.Prefix };
        Assert.Null(entry.Literal);
    }
}
