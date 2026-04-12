using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class GeneratorSizingServiceTests
{
    // ── Basic Sizing ─────────────────────────────────────────────────────────

    [Fact]
    public void SizeGenerator_EmptyLoads_ReturnsDefault()
    {
        var result = GeneratorSizingService.SizeGenerator(Array.Empty<GeneratorLoad>());
        Assert.Equal(0, result.LoadCount);
        Assert.Equal(0, result.TotalDemandKVA);
    }

    [Fact]
    public void SizeGenerator_SingleLifeSafetyLoad()
    {
        var loads = new[]
        {
            new GeneratorLoad
            {
                Id = "LS1", Description = "Exit Signs",
                LoadClass = EmergencyLoadClass.LifeSafety,
                ConnectedKVA = 10, DemandFactor = 1.0, PowerFactor = 0.9,
            }
        };
        var result = GeneratorSizingService.SizeGenerator(loads);

        Assert.Equal(10.0, result.LifeSafetyKVA);
        Assert.Equal(10.0, result.TotalDemandKVA);
        Assert.Equal(0, result.LargestMotorStartingKVA);
        Assert.Equal(1, result.LoadCount);
    }

    [Fact]
    public void SizeGenerator_DemandFactor_Applied()
    {
        var loads = new[]
        {
            new GeneratorLoad
            {
                ConnectedKVA = 100, DemandFactor = 0.8,
                LoadClass = EmergencyLoadClass.OptionalStandby,
            }
        };
        var result = GeneratorSizingService.SizeGenerator(loads);
        Assert.Equal(80.0, result.TotalDemandKVA);
    }

    [Fact]
    public void SizeGenerator_BreakdownByClass()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 50, LoadClass = EmergencyLoadClass.LifeSafety },
            new GeneratorLoad { ConnectedKVA = 80, LoadClass = EmergencyLoadClass.LegallyRequired },
            new GeneratorLoad { ConnectedKVA = 120, LoadClass = EmergencyLoadClass.OptionalStandby },
        };
        var result = GeneratorSizingService.SizeGenerator(loads);

        Assert.Equal(50.0, result.LifeSafetyKVA);
        Assert.Equal(80.0, result.LegallyRequiredKVA);
        Assert.Equal(120.0, result.OptionalStandbyKVA);
        Assert.Equal(250.0, result.TotalDemandKVA);
    }

    [Fact]
    public void SizeGenerator_WithMotor_AddsStartingInrush()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 50, LoadClass = EmergencyLoadClass.LifeSafety },
            new GeneratorLoad
            {
                ConnectedKVA = 30, LoadClass = EmergencyLoadClass.LegallyRequired,
                IsMotor = true, MotorStartingKVA = 80,
            },
        };
        var result = GeneratorSizingService.SizeGenerator(loads);

        Assert.Equal(80.0, result.LargestMotorStartingKVA);
        // Peak = 80 (total running) + 80 (largest starting) = 160
        Assert.Equal(160.0, result.PeakDemandKVA);
    }

    [Fact]
    public void SizeGenerator_MultipleMotors_LargestStartingUsed()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 20, IsMotor = true, MotorStartingKVA = 40 },
            new GeneratorLoad { ConnectedKVA = 40, IsMotor = true, MotorStartingKVA = 100 },
            new GeneratorLoad { ConnectedKVA = 10, IsMotor = false },
        };
        var result = GeneratorSizingService.SizeGenerator(loads);

        Assert.Equal(100.0, result.LargestMotorStartingKVA);
    }

    [Fact]
    public void SizeGenerator_125PercentFactor()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 100, PowerFactor = 1.0, LoadClass = EmergencyLoadClass.LifeSafety }
        };
        var result = GeneratorSizingService.SizeGenerator(loads);

        // Peak = 100, 125% = 125 kVA, PF=1.0 → 125 kW → nearest std = 125kW
        Assert.Equal(125, result.RecommendedGeneratorKW);
    }

    [Fact]
    public void SizeGenerator_RoundsToStandardSize()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 90, PowerFactor = 1.0 }
        };
        var result = GeneratorSizingService.SizeGenerator(loads);

        // Peak = 90, × 1.25 = 112.5 kW → next std = 125 kW
        Assert.Equal(125, result.RecommendedGeneratorKW);
    }

    [Fact]
    public void SizeGenerator_AverageWeightedPF()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 60, PowerFactor = 0.9 },
            new GeneratorLoad { ConnectedKVA = 40, PowerFactor = 0.7 },
        };
        var result = GeneratorSizingService.SizeGenerator(loads);

        // Weighted PF = (60*0.9 + 40*0.7) / 100 = (54+28)/100 = 0.82
        Assert.Equal(0.82, result.AverageWeightedPowerFactor);
    }

    // ── Emergency System Validation ──────────────────────────────────────────

    [Fact]
    public void ValidateEmergencySystem_Adequate_NoIssues()
    {
        var loads = new[]
        {
            new GeneratorLoad
            {
                ConnectedKVA = 50, LoadClass = EmergencyLoadClass.LifeSafety,
                PowerFactor = 0.8,
            }
        };
        // 200 kVA generator, load is 50 kVA → well within
        var issues = GeneratorSizingService.ValidateEmergencySystem(loads, 200);
        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateEmergencySystem_LifeSafetyExceedsCapacity()
    {
        var loads = new[]
        {
            new GeneratorLoad
            {
                ConnectedKVA = 150, LoadClass = EmergencyLoadClass.LifeSafety,
            }
        };
        var issues = GeneratorSizingService.ValidateEmergencySystem(loads, 100);
        Assert.Contains(issues, i => i.Contains("NEC 700") && i.Contains("Life safety"));
    }

    [Fact]
    public void ValidateEmergencySystem_Tier2ExceedsCapacity()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 40, LoadClass = EmergencyLoadClass.LifeSafety },
            new GeneratorLoad { ConnectedKVA = 70, LoadClass = EmergencyLoadClass.LegallyRequired },
        };
        // Total = 110 > 100 kVA
        var issues = GeneratorSizingService.ValidateEmergencySystem(loads, 100);
        Assert.Contains(issues, i => i.Contains("NEC 700+701"));
    }

    [Fact]
    public void ValidateEmergencySystem_PeakExceedsCapacity()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 50 },
            new GeneratorLoad { ConnectedKVA = 30, IsMotor = true, MotorStartingKVA = 80 },
        };
        // Peak = 80 + 80 = 160 kVA
        var issues = GeneratorSizingService.ValidateEmergencySystem(loads, 100);
        Assert.Contains(issues, i => i.Contains("peak demand"));
    }

    [Fact]
    public void ValidateEmergencySystem_Undersized_After125Percent()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 80, PowerFactor = 1.0 }
        };
        // Peak = 80, × 1.25 = 100kVA, reqKW = 100, stdGen = 100kW → kVA=100
        // Generator is 90 kVA → too small
        var issues = GeneratorSizingService.ValidateEmergencySystem(loads, 90);
        Assert.Contains(issues, i => i.Contains("undersized"));
    }

    // ── Load Sequence ────────────────────────────────────────────────────────

    [Fact]
    public void GetLoadSequence_OrderedByPriority()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 50, LoadClass = EmergencyLoadClass.OptionalStandby },
            new GeneratorLoad { ConnectedKVA = 80, LoadClass = EmergencyLoadClass.LifeSafety },
            new GeneratorLoad { ConnectedKVA = 30, LoadClass = EmergencyLoadClass.LegallyRequired },
        };
        var seq = GeneratorSizingService.GetLoadSequence(loads);

        Assert.Equal(3, seq.Count);
        Assert.Equal(EmergencyLoadClass.LifeSafety, seq[0].Class);
        Assert.Equal(EmergencyLoadClass.LegallyRequired, seq[1].Class);
        Assert.Equal(EmergencyLoadClass.OptionalStandby, seq[2].Class);
    }

    [Fact]
    public void GetLoadSequence_LifeSafety_Priority1()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 10, LoadClass = EmergencyLoadClass.LifeSafety },
        };
        var seq = GeneratorSizingService.GetLoadSequence(loads);
        Assert.Equal(1, seq[0].SequencePriority);
    }

    [Fact]
    public void GetLoadSequence_AggregatesWithinClass()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 10, LoadClass = EmergencyLoadClass.LifeSafety },
            new GeneratorLoad { ConnectedKVA = 20, LoadClass = EmergencyLoadClass.LifeSafety },
        };
        var seq = GeneratorSizingService.GetLoadSequence(loads);
        Assert.Single(seq);
        Assert.Equal(30.0, seq[0].DemandKVA);
    }

    // ── Large system with motor ──────────────────────────────────────────────

    [Fact]
    public void SizeGenerator_RealWorldExample()
    {
        var loads = new[]
        {
            // Life safety: exit signs + egress lights
            new GeneratorLoad { ConnectedKVA = 15, LoadClass = EmergencyLoadClass.LifeSafety, PowerFactor = 0.95 },
            // Legally required: fire alarm + smoke control
            new GeneratorLoad { ConnectedKVA = 25, LoadClass = EmergencyLoadClass.LegallyRequired, PowerFactor = 0.9 },
            // Optional: HVAC
            new GeneratorLoad
            {
                ConnectedKVA = 100, LoadClass = EmergencyLoadClass.OptionalStandby,
                PowerFactor = 0.85, IsMotor = true, MotorStartingKVA = 150,
            },
            // Optional: elevators
            new GeneratorLoad
            {
                ConnectedKVA = 50, LoadClass = EmergencyLoadClass.OptionalStandby,
                PowerFactor = 0.8, IsMotor = true, MotorStartingKVA = 80,
            },
        };

        var result = GeneratorSizingService.SizeGenerator(loads);

        Assert.Equal(15.0, result.LifeSafetyKVA);
        Assert.Equal(25.0, result.LegallyRequiredKVA);
        Assert.Equal(150.0, result.OptionalStandbyKVA);
        Assert.Equal(190.0, result.TotalDemandKVA);
        Assert.Equal(150.0, result.LargestMotorStartingKVA);
        // Peak = 190 + 150 = 340 kVA
        Assert.Equal(340.0, result.PeakDemandKVA);
        Assert.True(result.RecommendedGeneratorKW > 0);
        Assert.Equal(4, result.LoadCount);
    }

    [Fact]
    public void SizeGenerator_NoMotors_ZeroStarting()
    {
        var loads = new[]
        {
            new GeneratorLoad { ConnectedKVA = 100, IsMotor = false },
        };
        var result = GeneratorSizingService.SizeGenerator(loads);
        Assert.Equal(0, result.LargestMotorStartingKVA);
        Assert.Equal(result.TotalDemandKVA, result.PeakDemandKVA);
    }
}
