using System;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Electrical heat tracing design per IEEE 515 and NEC 427.
/// Covers freeze protection, process temperature maintenance,
/// cable selection, and circuit sizing.
/// </summary>
public static class HeatTracingService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    public enum HeatTraceType
    {
        SelfRegulating,    // Self-regulating polymer cable
        PowerLimiting,     // Power-limiting cable
        MineralInsulated,  // MI cable — high temperature
        SeriesResistance,  // Constant-wattage series cable
    }

    public enum PipeInsulation
    {
        None,
        Fiberglass1Inch,
        Fiberglass2Inch,
        Foam1Inch,
        Foam2Inch,
    }

    public record HeatLossResult
    {
        public double HeatLossWattPerFt { get; init; }
        public double TotalHeatLossWatt { get; init; }
        public double PipeLengthFt { get; init; }
        public double DeltaTempF { get; init; }
    }

    public record CableSelectionResult
    {
        public HeatTraceType CableType { get; init; }
        public double RatedWattPerFt { get; init; }
        public double CableLengthFt { get; init; }
        public double SafetyFactor { get; init; }
        public double MaxExposureTemperatureF { get; init; }
    }

    public record CircuitResult
    {
        public double TotalWattage { get; init; }
        public double CircuitAmps { get; init; }
        public int BreakerAmps { get; init; }
        public double MaxCircuitLengthFt { get; init; }
        public string WireSize { get; init; } = "";
    }

    // ── Heat Loss Calculation (IEEE 515) ─────────────────────────────────────

    /// <summary>
    /// Calculates pipe heat loss in watts per foot.
    /// Q = (TmaintainF − TambientF) / (R_insulation)
    /// </summary>
    public static HeatLossResult CalculateHeatLoss(
        double pipeDiameterInches,
        double pipeLengthFt,
        double maintainTempF,
        double ambientTempF,
        PipeInsulation insulation = PipeInsulation.Fiberglass1Inch)
    {
        if (pipeDiameterInches <= 0 || pipeLengthFt <= 0)
            throw new ArgumentException("Pipe dimensions must be positive.");

        double deltaT = maintainTempF - ambientTempF;
        if (deltaT <= 0)
            throw new ArgumentException("Maintain temperature must exceed ambient.");

        // Thermal resistance (R-value per foot) depends on insulation
        // Higher R = less heat loss. Units: ft·°F/W
        double rPerFt = insulation switch
        {
            PipeInsulation.None => 0.5 + pipeDiameterInches * 0.05,
            PipeInsulation.Fiberglass1Inch => 3.0 + pipeDiameterInches * 0.15,
            PipeInsulation.Fiberglass2Inch => 5.5 + pipeDiameterInches * 0.25,
            PipeInsulation.Foam1Inch => 4.0 + pipeDiameterInches * 0.18,
            PipeInsulation.Foam2Inch => 7.0 + pipeDiameterInches * 0.30,
            _ => 3.0,
        };

        double heatLossPerFt = deltaT / rPerFt;
        double totalLoss = heatLossPerFt * pipeLengthFt;

        return new HeatLossResult
        {
            HeatLossWattPerFt = Math.Round(heatLossPerFt, 2),
            TotalHeatLossWatt = Math.Round(totalLoss, 1),
            PipeLengthFt = pipeLengthFt,
            DeltaTempF = deltaT,
        };
    }

    // ── Cable Selection ──────────────────────────────────────────────────────

    /// <summary>
    /// Selects heat trace cable type and calculates required length.
    /// Cable must supply at least the heat loss with a safety factor.
    /// For spiraling, cable length > pipe length.
    /// </summary>
    public static CableSelectionResult SelectCable(
        double heatLossWattPerFt,
        double pipeLengthFt,
        double maxMaintainTempF,
        double safetyFactor = 1.2)
    {
        if (heatLossWattPerFt <= 0 || pipeLengthFt <= 0)
            throw new ArgumentException("Heat loss and pipe length must be positive.");

        // Select cable type by temperature requirement
        HeatTraceType type;
        double ratedWattPerFt;
        double maxExposure;

        if (maxMaintainTempF <= 150)
        {
            type = HeatTraceType.SelfRegulating;
            ratedWattPerFt = 5; // Typical 5 W/ft self-reg for freeze protection
            maxExposure = 185;
        }
        else if (maxMaintainTempF <= 300)
        {
            type = HeatTraceType.SelfRegulating;
            ratedWattPerFt = 10; // Higher-output self-reg
            maxExposure = 420;
        }
        else if (maxMaintainTempF <= 500)
        {
            type = HeatTraceType.PowerLimiting;
            ratedWattPerFt = 15;
            maxExposure = 500;
        }
        else
        {
            type = HeatTraceType.MineralInsulated;
            ratedWattPerFt = 25;
            maxExposure = 1200;
        }

        // Required wattage per foot with safety factor
        double requiredWpf = heatLossWattPerFt * safetyFactor;

        // If cable output < required, must spiral → cable longer than pipe
        double spiralFactor = requiredWpf > ratedWattPerFt
            ? requiredWpf / ratedWattPerFt
            : 1.0;

        double cableLength = pipeLengthFt * spiralFactor;

        return new CableSelectionResult
        {
            CableType = type,
            RatedWattPerFt = ratedWattPerFt,
            CableLengthFt = Math.Round(cableLength, 1),
            SafetyFactor = safetyFactor,
            MaxExposureTemperatureF = maxExposure,
        };
    }

    // ── Circuit Sizing (NEC 427) ─────────────────────────────────────────────

    /// <summary>
    /// Sizes the branch circuit for a heat tracing run per NEC 427.4.
    /// Heat tracing is continuous load → 125% for breaker.
    /// </summary>
    public static CircuitResult SizeCircuit(
        double totalWattage,
        double voltageV,
        double maxCircuitLengthFt = 0)
    {
        if (totalWattage <= 0 || voltageV <= 0)
            throw new ArgumentException("Wattage and voltage must be positive.");

        double amps = totalWattage / voltageV;

        // Continuous load: 125% for breaker sizing
        double continuousAmps = amps * 1.25;

        // Standard breaker sizes
        int[] standardBreakers = { 15, 20, 25, 30, 40, 50 };
        int breakerAmps = standardBreakers.FirstOrDefault(b => b >= continuousAmps);
        if (breakerAmps == 0) breakerAmps = 50;

        // Wire size by breaker
        string wireSize = breakerAmps switch
        {
            <= 15 => "14",
            <= 20 => "12",
            <= 30 => "10",
            <= 40 => "8",
            <= 50 => "6",
            _ => "6",
        };

        // Maximum circuit length depends on cable resistance and voltage
        // Self-reg: ~20 ohm/1000ft at startup. Limit voltage drop to 10%.
        double maxLen = maxCircuitLengthFt > 0
            ? maxCircuitLengthFt
            : voltageV switch
            {
                <= 120 => 200,
                <= 240 => 450,
                _ => 700,
            };

        return new CircuitResult
        {
            TotalWattage = Math.Round(totalWattage, 1),
            CircuitAmps = Math.Round(amps, 2),
            BreakerAmps = breakerAmps,
            MaxCircuitLengthFt = maxLen,
            WireSize = wireSize,
        };
    }
}
