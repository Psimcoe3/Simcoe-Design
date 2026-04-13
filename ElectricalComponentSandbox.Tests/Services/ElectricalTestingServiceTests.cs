using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ElectricalTestingServiceTests
{
    // ── Insulation Resistance ────────────────────────────────────────────────

    [Fact]
    public void InsulationResistance_HighReading_Good()
    {
        var result = ElectricalTestingService.EvaluateInsulationResistance(
            new ElectricalTestingService.InsulationResistanceInput
            {
                Equipment = ElectricalTestingService.EquipmentType.Cable,
                RatedVoltageKV = 15,
                MeasuredMegaohms = 5000,
                TemperatureCelsius = 20,
            });

        Assert.Equal(ElectricalTestingService.TestVerdict.Good, result.Verdict);
        Assert.Equal(5000, result.MeasuredMegaohms);
        Assert.Equal(5000, result.CorrectedMegaohms); // No correction at 20°C
        Assert.Equal(5000, result.TestVoltageVdc);     // 15kV → 5000V test
    }

    [Fact]
    public void InsulationResistance_LowReading_Bad()
    {
        var result = ElectricalTestingService.EvaluateInsulationResistance(
            new ElectricalTestingService.InsulationResistanceInput
            {
                Equipment = ElectricalTestingService.EquipmentType.Cable,
                RatedVoltageKV = 15,
                MeasuredMegaohms = 5,
                TemperatureCelsius = 20,
            });

        Assert.Equal(ElectricalTestingService.TestVerdict.Bad, result.Verdict);
    }

    [Fact]
    public void InsulationResistance_TemperatureCorrection_IncreasesAtLowTemp()
    {
        var cold = ElectricalTestingService.EvaluateInsulationResistance(
            new ElectricalTestingService.InsulationResistanceInput
            {
                Equipment = ElectricalTestingService.EquipmentType.Motor,
                RatedVoltageKV = 5,
                MeasuredMegaohms = 100,
                TemperatureCelsius = 10, // 10°C below base
            });

        // Correction factor = 2^(-10/10) = 0.5 → corrected = 50 MΩ... wait,
        // IR increases with lower temp, correction halves it to normalize to 20°C
        Assert.True(cold.CorrectedMegaohms < cold.MeasuredMegaohms);
    }

    [Fact]
    public void InsulationResistance_HighTemp_IncreasesCorrection()
    {
        var hot = ElectricalTestingService.EvaluateInsulationResistance(
            new ElectricalTestingService.InsulationResistanceInput
            {
                Equipment = ElectricalTestingService.EquipmentType.Cable,
                RatedVoltageKV = 15,
                MeasuredMegaohms = 100,
                TemperatureCelsius = 40, // 20°C above base → correction = 4×
            });

        // At higher temp, IR drops; correction boosts it back up
        Assert.True(hot.CorrectedMegaohms > hot.MeasuredMegaohms);
    }

    [Fact]
    public void InsulationResistance_Generator_HigherMinimum()
    {
        var cable = ElectricalTestingService.EvaluateInsulationResistance(
            new ElectricalTestingService.InsulationResistanceInput
            {
                Equipment = ElectricalTestingService.EquipmentType.Cable,
                RatedVoltageKV = 15,
                MeasuredMegaohms = 50,
                TemperatureCelsius = 20,
            });

        var generator = ElectricalTestingService.EvaluateInsulationResistance(
            new ElectricalTestingService.InsulationResistanceInput
            {
                Equipment = ElectricalTestingService.EquipmentType.Generator,
                RatedVoltageKV = 15,
                MeasuredMegaohms = 50,
                TemperatureCelsius = 20,
            });

        // Same measurement: cable may pass, generator needs 5× higher minimum
        Assert.True(generator.MinimumMegaohms > cable.MinimumMegaohms);
    }

    [Theory]
    [InlineData(0.6, 1000)]
    [InlineData(4.16, 2500)]
    [InlineData(13.8, 5000)]
    [InlineData(34.5, 15000)]
    public void InsulationResistance_TestVoltageSelection(double ratedKV, double expectedTestV)
    {
        var result = ElectricalTestingService.EvaluateInsulationResistance(
            new ElectricalTestingService.InsulationResistanceInput
            {
                Equipment = ElectricalTestingService.EquipmentType.Cable,
                RatedVoltageKV = ratedKV,
                MeasuredMegaohms = 10000,
                TemperatureCelsius = 20,
            });

        Assert.Equal(expectedTestV, result.TestVoltageVdc);
    }

    // ── Contact Resistance ───────────────────────────────────────────────────

    [Fact]
    public void ContactResistance_LowReading_Good()
    {
        var result = ElectricalTestingService.EvaluateContactResistance(
            ElectricalTestingService.EquipmentType.Switchgear, 30);

        Assert.Equal(ElectricalTestingService.TestVerdict.Good, result.Verdict);
        Assert.Equal(50, result.MaxAllowableMicroohms);
    }

    [Fact]
    public void ContactResistance_HighReading_Bad()
    {
        var result = ElectricalTestingService.EvaluateContactResistance(
            ElectricalTestingService.EquipmentType.Switchgear, 100);

        Assert.Equal(ElectricalTestingService.TestVerdict.Bad, result.Verdict);
    }

    [Fact]
    public void ContactResistance_Marginal_Investigate()
    {
        var result = ElectricalTestingService.EvaluateContactResistance(
            ElectricalTestingService.EquipmentType.Switchgear, 60);

        Assert.Equal(ElectricalTestingService.TestVerdict.Investigate, result.Verdict);
    }

    // ── Power Factor ─────────────────────────────────────────────────────────

    [Fact]
    public void PowerFactor_Transformer_LowPF_Good()
    {
        var result = ElectricalTestingService.EvaluatePowerFactor(
            ElectricalTestingService.EquipmentType.Transformer, 0.3);

        Assert.Equal(ElectricalTestingService.TestVerdict.Good, result.Verdict);
        Assert.Equal(0.5, result.MaxAcceptablePercent);
    }

    [Fact]
    public void PowerFactor_HighPF_Bad()
    {
        var result = ElectricalTestingService.EvaluatePowerFactor(
            ElectricalTestingService.EquipmentType.Transformer, 2.0);

        Assert.Equal(ElectricalTestingService.TestVerdict.Bad, result.Verdict);
    }

    [Fact]
    public void PowerFactor_TemperatureCorrection_ReducesAtHighTemp()
    {
        var result = ElectricalTestingService.EvaluatePowerFactor(
            ElectricalTestingService.EquipmentType.Cable, 1.5, 30);

        // Corrected = 1.5 - (30-20)×0.1 = 0.5
        Assert.Equal(0.5, result.CorrectedPercent, 2);
    }

    // ── Hipot Testing ────────────────────────────────────────────────────────

    [Fact]
    public void Hipot_LowLeakage_Good()
    {
        var result = ElectricalTestingService.EvaluateHipot(
            15, 40, 5, 20);

        Assert.Equal(ElectricalTestingService.TestVerdict.Good, result.Verdict);
    }

    [Fact]
    public void Hipot_HighLeakage_Bad()
    {
        var result = ElectricalTestingService.EvaluateHipot(
            15, 40, 5, 200);

        Assert.Equal(ElectricalTestingService.TestVerdict.Bad, result.Verdict);
    }

    [Fact]
    public void Hipot_MarginalLeakage_Investigate()
    {
        var result = ElectricalTestingService.EvaluateHipot(
            15, 40, 5, 50);

        // Max = 40 µA; 50 is between 40 and 80
        Assert.Equal(ElectricalTestingService.TestVerdict.Investigate, result.Verdict);
    }
}
