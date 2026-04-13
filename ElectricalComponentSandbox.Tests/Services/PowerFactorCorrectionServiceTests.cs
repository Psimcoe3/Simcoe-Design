using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class PowerFactorCorrectionServiceTests
{
    // ── kVAR Calculation ─────────────────────────────────────────────────────

    [Fact]
    public void CalculateRequiredKVAR_0_80_To_0_95()
    {
        // 100 kW at 0.80 PF → target 0.95
        double kvar = PowerFactorCorrectionService.CalculateRequiredKVAR(100, 0.80, 0.95);
        // tan(acos(0.80)) = 0.75, tan(acos(0.95)) ≈ 0.3287
        // 100 × (0.75 - 0.3287) ≈ 42.13
        Assert.InRange(kvar, 40, 45);
    }

    [Fact]
    public void CalculateRequiredKVAR_0_70_To_0_95()
    {
        double kvar = PowerFactorCorrectionService.CalculateRequiredKVAR(200, 0.70, 0.95);
        Assert.True(kvar > 100, "Severely low PF should require substantial correction");
    }

    [Fact]
    public void CalculateRequiredKVAR_AlreadyAtTarget_Zero()
    {
        double kvar = PowerFactorCorrectionService.CalculateRequiredKVAR(100, 0.95, 0.95);
        Assert.Equal(0, kvar);
    }

    [Fact]
    public void CalculateRequiredKVAR_AboveTarget_Zero()
    {
        double kvar = PowerFactorCorrectionService.CalculateRequiredKVAR(100, 0.98, 0.95);
        Assert.Equal(0, kvar);
    }

    [Fact]
    public void CalculateRequiredKVAR_ZeroPower_Zero()
    {
        double kvar = PowerFactorCorrectionService.CalculateRequiredKVAR(0, 0.80, 0.95);
        Assert.Equal(0, kvar);
    }

    [Fact]
    public void CalculateRequiredKVAR_UnityExisting_Zero()
    {
        double kvar = PowerFactorCorrectionService.CalculateRequiredKVAR(100, 1.0, 0.95);
        Assert.Equal(0, kvar);
    }

    // ── Bank Size Selection ──────────────────────────────────────────────────

    [Fact]
    public void SelectBankSize_RoundsUpToStandard()
    {
        double bank = PowerFactorCorrectionService.SelectBankSize(42);
        Assert.Equal(50, bank);
    }

    [Fact]
    public void SelectBankSize_ExactMatch()
    {
        double bank = PowerFactorCorrectionService.SelectBankSize(50);
        Assert.Equal(50, bank);
    }

    [Fact]
    public void SelectBankSize_Zero_Zero()
    {
        double bank = PowerFactorCorrectionService.SelectBankSize(0);
        Assert.Equal(0, bank);
    }

    [Fact]
    public void SelectBankSize_Small()
    {
        double bank = PowerFactorCorrectionService.SelectBankSize(3);
        Assert.Equal(5, bank);
    }

    // ── Step Recommendations ─────────────────────────────────────────────────

    [Fact]
    public void RecommendSteps_Small_SingleStep()
    {
        var (steps, size) = PowerFactorCorrectionService.RecommendSteps(15);
        Assert.Equal(1, steps);
        Assert.Equal(15, size);
    }

    [Fact]
    public void RecommendSteps_Medium_ThreeSteps()
    {
        var (steps, size) = PowerFactorCorrectionService.RecommendSteps(50);
        Assert.Equal(3, steps);
        Assert.True(size > 0);
        Assert.True(steps * size >= 50);
    }

    [Fact]
    public void RecommendSteps_Large_MultipleSteps()
    {
        var (steps, _) = PowerFactorCorrectionService.RecommendSteps(400);
        Assert.True(steps >= 4);
    }

    [Fact]
    public void RecommendSteps_Zero_NoSteps()
    {
        var (steps, size) = PowerFactorCorrectionService.RecommendSteps(0);
        Assert.Equal(0, steps);
        Assert.Equal(0, size);
    }

    // ── System Analysis ──────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeSystem_LowPF_RecommendsCorrection()
    {
        // 100 kW real, 125 kVA apparent → PF = 0.80
        var result = PowerFactorCorrectionService.AnalyzeSystem(100, 125, 0.95);

        Assert.Equal(0.80, result.ExistingPowerFactor, 0.01);
        Assert.True(result.RequiredCorrectionKVAR > 0);
        Assert.True(result.RecommendedBankSizeKVAR > 0);
        Assert.True(result.CorrectedPowerFactor > result.ExistingPowerFactor);
        Assert.True(result.CurrentReductionPercent > 0);
        Assert.True(result.CapacityReleasedKVA > 0);
    }

    [Fact]
    public void AnalyzeSystem_HighPF_MinimalCorrection()
    {
        // 95 kW / 100 kVA → PF = 0.95
        var result = PowerFactorCorrectionService.AnalyzeSystem(95, 100, 0.95);

        Assert.True(result.ExistingPowerFactor >= 0.95);
        Assert.Equal(0, result.RequiredCorrectionKVAR, 0.1);
    }

    [Fact]
    public void AnalyzeSystem_Zero_NoError()
    {
        var result = PowerFactorCorrectionService.AnalyzeSystem(0, 0, 0.95);
        Assert.Equal(1.0, result.CorrectedPowerFactor);
    }

    [Fact]
    public void AnalyzeSystem_CorrectedPF_MeetsTarget()
    {
        var result = PowerFactorCorrectionService.AnalyzeSystem(100, 125, 0.95);

        // Corrected PF should meet or exceed target
        Assert.True(result.CorrectedPowerFactor >= 0.95,
            $"Corrected PF {result.CorrectedPowerFactor} should reach target 0.95");
    }

    [Fact]
    public void AnalyzeSystem_CriticallyLow_Warning()
    {
        // 60 kW / 100 kVA → PF = 0.60
        var result = PowerFactorCorrectionService.AnalyzeSystem(60, 100, 0.95);

        Assert.Contains(result.Warnings, w => w.Contains("critically low"));
    }

    // ── Panel Analysis ───────────────────────────────────────────────────────

    [Fact]
    public void AnalyzePanel_MixedLoads_CorrectPF()
    {
        var schedule = new PanelSchedule
        {
            PanelId = "MCC", PanelName = "Motor Control Center",
            BusAmps = 400, VoltageConfig = PanelVoltageConfig.V277_480_3Ph,
            Circuits = new List<Circuit>
            {
                new() { Id = "1", Phase = "ABC", Poles = 3, ConnectedLoadVA = 50000, DemandFactor = 1.0, PowerFactor = 0.85 },
                new() { Id = "2", Phase = "ABC", Poles = 3, ConnectedLoadVA = 30000, DemandFactor = 1.0, PowerFactor = 0.80 },
                new() { Id = "3", Phase = "ABC", Poles = 3, ConnectedLoadVA = 20000, DemandFactor = 1.0, PowerFactor = 0.75 },
            },
        };

        var result = PowerFactorCorrectionService.AnalyzePanel(schedule, 0.95);

        Assert.True(result.ExistingPowerFactor < 0.90);
        Assert.True(result.RequiredCorrectionKVAR > 0);
        Assert.True(result.RecommendedBankSizeKVAR > 0);
    }

    [Fact]
    public void AnalyzePanel_UnityPF_NoCorrection()
    {
        var schedule = new PanelSchedule
        {
            PanelId = "LP1", PanelName = "Lighting Panel",
            BusAmps = 200, VoltageConfig = PanelVoltageConfig.V120_208_3Ph,
            Circuits = new List<Circuit>
            {
                new() { Id = "1", Phase = "A", Poles = 1, ConnectedLoadVA = 5000, DemandFactor = 1.0, PowerFactor = 1.0 },
                new() { Id = "2", Phase = "B", Poles = 1, ConnectedLoadVA = 5000, DemandFactor = 1.0, PowerFactor = 1.0 },
            },
        };

        var result = PowerFactorCorrectionService.AnalyzePanel(schedule, 0.95);

        Assert.True(result.ExistingPowerFactor >= 0.99);
        Assert.Equal(0, result.RecommendedBankSizeKVAR);
    }

    // ── Capacity Release ─────────────────────────────────────────────────────

    [Fact]
    public void AnalyzeSystem_CapacityRelease_Positive()
    {
        var result = PowerFactorCorrectionService.AnalyzeSystem(100, 150, 0.95);

        Assert.True(result.CapacityReleasedKVA > 0,
            "Correcting low PF should release system capacity");
    }

    [Fact]
    public void AnalyzeSystem_CurrentReduction_Proportional()
    {
        var low = PowerFactorCorrectionService.AnalyzeSystem(100, 140, 0.95);
        var veryLow = PowerFactorCorrectionService.AnalyzeSystem(100, 170, 0.95);

        Assert.True(veryLow.CurrentReductionPercent > low.CurrentReductionPercent,
            "Lower initial PF should yield greater current reduction");
    }

    // ── Real-World ───────────────────────────────────────────────────────────

    [Fact]
    public void RealWorld_IndustrialPlant_500kW()
    {
        // Typical industrial: 500 kW at 0.75 PF
        double apparentKVA = 500 / 0.75; // ≈ 666.7 kVA
        var result = PowerFactorCorrectionService.AnalyzeSystem(500, apparentKVA, 0.95);

        Assert.Equal(0.75, result.ExistingPowerFactor, 0.01);
        Assert.True(result.RequiredCorrectionKVAR > 200);
        Assert.True(result.RecommendedBankSizeKVAR >= 200);
        Assert.True(result.CorrectedPowerFactor >= 0.95);
        Assert.True(result.NumberOfSteps >= 3, "Large bank should have multiple steps");
    }
}
