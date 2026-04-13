using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Electrical acceptance and maintenance testing calculations
/// per NETA ATS/MTS and NFPA 70B standards.
/// Covers insulation resistance, contact resistance, power factor,
/// and hipot testing.
/// </summary>
public static class ElectricalTestingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum EquipmentType
    {
        Transformer,
        Cable,
        Motor,
        Switchgear,
        Generator,
    }

    public enum TestType
    {
        InsulationResistance,   // Megohmmeter / megger
        ContactResistance,      // DLRO
        PowerFactor,            // Doble / power factor
        HighPotential,          // Hipot DC/AC
    }

    public enum TestVerdict
    {
        Good,
        Investigate,
        Bad,
    }

    public record InsulationResistanceInput
    {
        public EquipmentType Equipment { get; init; } = EquipmentType.Cable;
        public double RatedVoltageKV { get; init; } = 15;
        public double MeasuredMegaohms { get; init; }
        public double TemperatureCelsius { get; init; } = 20;
    }

    public record InsulationResistanceResult
    {
        public double MeasuredMegaohms { get; init; }
        public double CorrectedMegaohms { get; init; }
        public double MinimumMegaohms { get; init; }
        public double TestVoltageVdc { get; init; }
        public TestVerdict Verdict { get; init; }
    }

    public record ContactResistanceResult
    {
        public double MeasuredMicroohms { get; init; }
        public double MaxAllowableMicroohms { get; init; }
        public TestVerdict Verdict { get; init; }
    }

    public record PowerFactorResult
    {
        public double MeasuredPercent { get; init; }
        public double CorrectedPercent { get; init; }
        public double MaxAcceptablePercent { get; init; }
        public TestVerdict Verdict { get; init; }
    }

    public record HipotResult
    {
        public double TestVoltageKV { get; init; }
        public double DurationMinutes { get; init; }
        public double LeakageMicroamps { get; init; }
        public double MaxLeakageMicroamps { get; init; }
        public TestVerdict Verdict { get; init; }
    }

    // ── Insulation Resistance (Megger) ───────────────────────────────────────

    /// <summary>
    /// Evaluates insulation resistance per NETA ATS §7.
    /// Applies temperature correction to 20°C base, checks minimum per 1 MΩ/kV rule.
    /// </summary>
    public static InsulationResistanceResult EvaluateInsulationResistance(InsulationResistanceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        // Test voltage selection per NETA ATS
        double testVoltage = input.RatedVoltageKV switch
        {
            <= 1.0 => 1000,
            <= 2.5 => 2500,
            <= 5.0 => 2500,
            <= 15.0 => 5000,
            <= 25.0 => 5000,
            _ => 15000,
        };

        // Temperature correction to 20°C per IEEE 43
        // Resistance halves for every 10°C rise above 20°C
        double deltaT = input.TemperatureCelsius - 20.0;
        double correctionFactor = Math.Pow(2.0, deltaT / 10.0);
        double corrected = input.MeasuredMegaohms * correctionFactor;

        // Minimum per NETA: 1 MΩ per kV + 1 MΩ minimum
        double minimum = input.Equipment switch
        {
            EquipmentType.Transformer => input.RatedVoltageKV + 1,
            EquipmentType.Cable => input.RatedVoltageKV + 1,
            EquipmentType.Motor => input.RatedVoltageKV + 1,
            EquipmentType.Switchgear => input.RatedVoltageKV + 1,
            EquipmentType.Generator => (input.RatedVoltageKV + 1) * 5, // Generators need higher
            _ => input.RatedVoltageKV + 1,
        };

        var verdict = corrected >= minimum * 2 ? TestVerdict.Good
                    : corrected >= minimum ? TestVerdict.Investigate
                    : TestVerdict.Bad;

        return new InsulationResistanceResult
        {
            MeasuredMegaohms = Math.Round(input.MeasuredMegaohms, 2),
            CorrectedMegaohms = Math.Round(corrected, 2),
            MinimumMegaohms = Math.Round(minimum, 2),
            TestVoltageVdc = testVoltage,
            Verdict = verdict,
        };
    }

    // ── Contact Resistance (DLRO) ────────────────────────────────────────────

    /// <summary>
    /// Evaluates contact resistance of breaker/switch contacts per NETA ATS §7.
    /// </summary>
    public static ContactResistanceResult EvaluateContactResistance(
        EquipmentType equipment,
        double measuredMicroohms)
    {
        // Max allowable per NETA ATS §7 (manufacturer data typical values)
        double maxAllowable = equipment switch
        {
            EquipmentType.Switchgear => 50,    // MV breaker contacts
            EquipmentType.Transformer => 100,  // Transformer tap changer
            _ => 200,                          // General bolted connections
        };

        var verdict = measuredMicroohms <= maxAllowable ? TestVerdict.Good
                    : measuredMicroohms <= maxAllowable * 1.5 ? TestVerdict.Investigate
                    : TestVerdict.Bad;

        return new ContactResistanceResult
        {
            MeasuredMicroohms = Math.Round(measuredMicroohms, 1),
            MaxAllowableMicroohms = maxAllowable,
            Verdict = verdict,
        };
    }

    // ── Power Factor Testing ─────────────────────────────────────────────────

    /// <summary>
    /// Evaluates power factor (dissipation factor) of insulation per NETA ATS §7.
    /// Temperature-corrected to 20°C.
    /// </summary>
    public static PowerFactorResult EvaluatePowerFactor(
        EquipmentType equipment,
        double measuredPercent,
        double temperatureCelsius = 20)
    {
        // Temperature correction per IEEE C57.12.90
        // PF increases ~0.1% per °C above 20
        double corrected = measuredPercent - (temperatureCelsius - 20) * 0.1;
        corrected = Math.Max(corrected, 0);

        double maxAcceptable = equipment switch
        {
            EquipmentType.Transformer => 0.5,   // Oil-filled
            EquipmentType.Cable => 1.0,          // XLPE cable
            EquipmentType.Switchgear => 2.0,     // Bus insulation
            EquipmentType.Motor => 3.0,
            EquipmentType.Generator => 2.0,
            _ => 2.0,
        };

        var verdict = corrected <= maxAcceptable ? TestVerdict.Good
                    : corrected <= maxAcceptable * 2 ? TestVerdict.Investigate
                    : TestVerdict.Bad;

        return new PowerFactorResult
        {
            MeasuredPercent = Math.Round(measuredPercent, 2),
            CorrectedPercent = Math.Round(corrected, 2),
            MaxAcceptablePercent = maxAcceptable,
            Verdict = verdict,
        };
    }

    // ── High-Potential (Hipot) Testing ───────────────────────────────────────

    /// <summary>
    /// Evaluates DC hipot test per NETA ATS §7 / IEEE 400.
    /// Test voltage = rated × multiplier. Leakage must stay below threshold.
    /// </summary>
    public static HipotResult EvaluateHipot(
        double ratedVoltageKV,
        double testVoltageKV,
        double durationMinutes,
        double leakageMicroamps)
    {
        if (ratedVoltageKV <= 0) throw new ArgumentException("Rated voltage must be positive.");
        if (testVoltageKV <= 0) throw new ArgumentException("Test voltage must be positive.");

        // IEEE 400.1 DC hipot max leakage guideline: ~1 µA per kV of test voltage for cables
        double maxLeakage = testVoltageKV * 1.0; // µA per kV test voltage

        var verdict = leakageMicroamps <= maxLeakage ? TestVerdict.Good
                    : leakageMicroamps <= maxLeakage * 2 ? TestVerdict.Investigate
                    : TestVerdict.Bad;

        return new HipotResult
        {
            TestVoltageKV = Math.Round(testVoltageKV, 2),
            DurationMinutes = durationMinutes,
            LeakageMicroamps = Math.Round(leakageMicroamps, 1),
            MaxLeakageMicroamps = Math.Round(maxLeakage, 1),
            Verdict = verdict,
        };
    }
}
