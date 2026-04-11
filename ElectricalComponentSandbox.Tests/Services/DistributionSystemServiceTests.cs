using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class DistributionSystemServiceTests
{
    private readonly DistributionSystemService _sut = new();

    // ── Built-In Defaults ─────────────────────────────────────────────────────

    [Fact]
    public void GetBuiltInDefaults_ReturnsFourSystems()
    {
        var defaults = DistributionSystemType.GetBuiltInDefaults();

        Assert.Equal(4, defaults.Count);
        Assert.All(defaults, d => Assert.True(d.IsBuiltIn));
    }

    [Fact]
    public void EnsureDefaults_EmptyProject_PopulatesFourSystems()
    {
        var project = new ProjectModel();

        _sut.EnsureDefaults(project);

        Assert.Equal(4, project.DistributionSystems.Count);
    }

    [Fact]
    public void EnsureDefaults_CalledTwice_DoesNotDuplicate()
    {
        var project = new ProjectModel();
        _sut.EnsureDefaults(project);
        _sut.EnsureDefaults(project);

        Assert.Equal(4, project.DistributionSystems.Count);
    }

    [Fact]
    public void EnsureDefaults_PreservesUserSystems()
    {
        var project = new ProjectModel();
        project.DistributionSystems.Add(new DistributionSystemType
        {
            Id = "custom-600v", Name = "600V Custom", LineVoltage = 600, PhaseVoltage = 347
        });

        _sut.EnsureDefaults(project);

        Assert.Equal(5, project.DistributionSystems.Count);
        Assert.Contains(project.DistributionSystems, d => d.Id == "custom-600v");
    }

    // ── Voltage Label ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("builtin-120-208-3ph",   "120/208V 3×4W")]
    [InlineData("builtin-277-480-3ph",   "277/480V 3×4W")]
    [InlineData("builtin-120-240-1ph",   "120/240V 1×3W")]
    [InlineData("builtin-240-delta-3ph", "240V 3×3W")]
    public void VoltageLabel_BuiltInSystems_MatchExpected(string id, string expectedLabel)
    {
        var system = DistributionSystemType.GetBuiltInDefaults().First(d => d.Id == id);

        Assert.Equal(expectedLabel, system.VoltageLabel);
    }

    // ── IsThreePhase ──────────────────────────────────────────────────────────

    [Fact]
    public void IsThreePhase_WyeIsTrue()
    {
        var sys = new DistributionSystemType { Phase = PhaseConfiguration.Wye };
        Assert.True(sys.IsThreePhase);
    }

    [Fact]
    public void IsThreePhase_DeltaIsTrue()
    {
        var sys = new DistributionSystemType { Phase = PhaseConfiguration.Delta };
        Assert.True(sys.IsThreePhase);
    }

    [Fact]
    public void IsThreePhase_SinglePhaseIsFalse()
    {
        var sys = new DistributionSystemType { Phase = PhaseConfiguration.SinglePhase };
        Assert.False(sys.IsThreePhase);
    }

    // ── CalculateCurrent ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateCurrent_ThreePhaseWye_UsesRootThree()
    {
        var sys = DistributionSystemType.GetBuiltInDefaults()
            .First(d => d.Id == DistributionSystemType.BuiltIn_120_208_3Ph_Id);
        double va = 36_000;

        double current = sys.CalculateCurrent(va);

        double expected = va / (208 * Math.Sqrt(3));
        Assert.Equal(expected, current, precision: 4);
    }

    [Fact]
    public void CalculateCurrent_SinglePhase_UsesLineVoltage()
    {
        var sys = DistributionSystemType.GetBuiltInDefaults()
            .First(d => d.Id == DistributionSystemType.BuiltIn_120_240_1Ph_Id);
        double va = 24_000;

        double current = sys.CalculateCurrent(va);

        Assert.Equal(va / 240.0, current, precision: 4);
    }

    // ── Migration ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(PanelVoltageConfig.V120_208_3Ph, "builtin-120-208-3ph")]
    [InlineData(PanelVoltageConfig.V277_480_3Ph, "builtin-277-480-3ph")]
    [InlineData(PanelVoltageConfig.V120_240_1Ph, "builtin-120-240-1ph")]
    [InlineData(PanelVoltageConfig.V240_3Ph,     "builtin-240-delta-3ph")]
    public void MigrateFromVoltageConfig_MapsAllEnumValues(PanelVoltageConfig config, string expectedId)
    {
        Assert.Equal(expectedId, DistributionSystemType.MigrateFromVoltageConfig(config));
    }

    [Fact]
    public void MigrateFromLegacy_SetsDistributionSystemId()
    {
        var schedules = new[]
        {
            new PanelSchedule { PanelName = "A", VoltageConfig = PanelVoltageConfig.V120_208_3Ph },
            new PanelSchedule { PanelName = "B", VoltageConfig = PanelVoltageConfig.V277_480_3Ph }
        };

        int count = _sut.MigrateFromLegacy(schedules);

        Assert.Equal(2, count);
        Assert.Equal("builtin-120-208-3ph", schedules[0].DistributionSystemId);
        Assert.Equal("builtin-277-480-3ph", schedules[1].DistributionSystemId);
    }

    [Fact]
    public void MigrateFromLegacy_SkipsAlreadyMigrated()
    {
        var schedules = new[]
        {
            new PanelSchedule
            {
                PanelName = "Already",
                VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
                DistributionSystemId = "custom-id"
            }
        };

        int count = _sut.MigrateFromLegacy(schedules);

        Assert.Equal(0, count);
        Assert.Equal("custom-id", schedules[0].DistributionSystemId);
    }

    // ── Resolve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_WithDistributionSystemId_ReturnsNamedSystem()
    {
        var systems = DistributionSystemType.GetBuiltInDefaults();
        var schedule = new PanelSchedule
        {
            DistributionSystemId = DistributionSystemType.BuiltIn_277_480_3Ph_Id,
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph // should be ignored
        };

        var resolved = _sut.Resolve(schedule, systems);

        Assert.Equal(DistributionSystemType.BuiltIn_277_480_3Ph_Id, resolved.Id);
        Assert.Equal(480, resolved.LineVoltage);
    }

    [Fact]
    public void Resolve_WithoutDistributionSystemId_FallsBackToVoltageConfig()
    {
        var systems = DistributionSystemType.GetBuiltInDefaults();
        var schedule = new PanelSchedule
        {
            VoltageConfig = PanelVoltageConfig.V277_480_3Ph
        };

        var resolved = _sut.Resolve(schedule, systems);

        Assert.Equal(DistributionSystemType.BuiltIn_277_480_3Ph_Id, resolved.Id);
    }

    // ── In-Use Guard ──────────────────────────────────────────────────────────

    [Fact]
    public void IsInUse_ReferencedSystem_ReturnsTrue()
    {
        var schedules = new[]
        {
            new PanelSchedule { DistributionSystemId = "sys-1" }
        };

        Assert.True(_sut.IsInUse("sys-1", schedules));
    }

    [Fact]
    public void IsInUse_UnreferencedSystem_ReturnsFalse()
    {
        var schedules = new[]
        {
            new PanelSchedule { DistributionSystemId = "sys-1" }
        };

        Assert.False(_sut.IsInUse("sys-2", schedules));
    }

    // ── AnalyzePanelLoad with DistributionSystemType ──────────────────────────

    [Fact]
    public void AnalyzePanelLoad_WithDistributionSystem_UsesSystemVoltage()
    {
        var calcService = new ElectricalCalculationService();
        var sys = new DistributionSystemType
        {
            Phase = PhaseConfiguration.Wye,
            LineVoltage = 480,
            PhaseVoltage = 277
        };
        var schedule = new PanelSchedule
        {
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph, // should be overridden
            Circuits = { new Circuit { Phase = "A", ConnectedLoadVA = 10_000 } }
        };

        var summary = calcService.AnalyzePanelLoad(schedule, sys);

        double expected = 10_000 / (480 * Math.Sqrt(3));
        Assert.Equal(expected, summary.TotalCurrentAmps, precision: 2);
    }

    // ── GeneratePanelSchedule with DistributionSystemType ────────────────────

    [Fact]
    public void GeneratePanelSchedule_WithDistributionSystem_UsesVoltageLabelFromSystem()
    {
        var tableService = new ScheduleTableService();
        var sys = new DistributionSystemType
        {
            Phase = PhaseConfiguration.Wye,
            LineVoltage = 480,
            PhaseVoltage = 277,
            Wires = 4
        };
        var schedule = new PanelSchedule
        {
            PanelName = "LP-1",
            VoltageConfig = PanelVoltageConfig.V120_208_3Ph, // should be overridden
            Circuits =
            {
                new Circuit { CircuitNumber = "1", Phase = "A", ConnectedLoadVA = 1200, Breaker = new CircuitBreaker { Poles = 1 } }
            }
        };

        var table = tableService.GeneratePanelSchedule(schedule, sys);

        // The subheader row (first row) should contain the distribution system voltage label
        Assert.Contains("277/480V", table.Rows[0][1]);
    }
}
