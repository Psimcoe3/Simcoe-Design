using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// NEC 110.26 working space and 110.26(E) dedicated equipment space calculations.
/// Validates clearances around electrical equipment based on voltage and conditions.
/// </summary>
public static class ElectricalRoomClearanceService
{
    // ── Enums & Records ──────────────────────────────────────────────────────

    /// <summary>
    /// NEC 110.26(A)(1) Condition of installation.
    /// Condition 1: Exposed live parts on one side, no live or grounded parts on the other.
    /// Condition 2: Exposed live parts on one side, grounded parts on the other.
    /// Condition 3: Exposed live parts on both sides.
    /// </summary>
    public enum ClearanceCondition
    {
        Condition1,
        Condition2,
        Condition3,
    }

    /// <summary>Equipment requiring clearance analysis.</summary>
    public record EquipmentClearance
    {
        public string Id { get; init; } = "";
        public string Name { get; init; } = "";
        public double NominalVoltage { get; init; } = 208;
        public ClearanceCondition Condition { get; init; } = ClearanceCondition.Condition1;
        public double EquipmentWidthInches { get; init; } = 20;
        public double EquipmentHeightInches { get; init; } = 72;
        public double EquipmentDepthInches { get; init; } = 12;
        public double ProvidedClearanceDepthFeet { get; init; }
        public double ProvidedClearanceWidthInches { get; init; }
        public double ProvidedClearanceHeightFeet { get; init; }
    }

    /// <summary>Result of a clearance check.</summary>
    public record ClearanceResult
    {
        public string EquipmentId { get; init; } = "";
        public string EquipmentName { get; init; } = "";
        public double RequiredDepthFeet { get; init; }
        public double ProvidedDepthFeet { get; init; }
        public bool DepthCompliant { get; init; }
        public double RequiredWidthInches { get; init; }
        public double ProvidedWidthInches { get; init; }
        public bool WidthCompliant { get; init; }
        public double RequiredHeightFeet { get; init; }
        public double ProvidedHeightFeet { get; init; }
        public bool HeightCompliant { get; init; }
        public bool FullyCompliant => DepthCompliant && WidthCompliant && HeightCompliant;
        public List<string> Violations { get; init; } = new();
    }

    /// <summary>NEC 110.26(E) dedicated equipment space result.</summary>
    public record DedicatedSpaceResult
    {
        public string EquipmentId { get; init; } = "";
        public double RequiredWidthInches { get; init; }
        public double RequiredDepthInches { get; init; }
        public double RequiredHeightFeet { get; init; }
        public bool Compliant { get; init; }
        public List<string> Violations { get; init; } = new();
    }

    // ── NEC 110.26(A)(1) Table — Working Space Depth (feet) ──────────────────
    // Voltage-to-ground → Condition 1, 2, 3

    private static readonly (double MaxVoltage, double Cond1, double Cond2, double Cond3)[] DepthTable =
    {
        (150,   3.0, 3.0, 3.0),
        (600,   3.0, 3.5, 4.0),
        (2500,  3.0, 4.0, 5.0),
        (9000,  4.0, 5.0, 6.0),
        (25000, 5.0, 6.0, 9.0),
        (75000, 6.0, 8.0, 12.0),
    };

    // ── Public Methods ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the NEC 110.26(A)(1) minimum working space depth in feet.
    /// </summary>
    public static double GetRequiredDepth(double nominalVoltage, ClearanceCondition condition)
    {
        foreach (var (maxV, c1, c2, c3) in DepthTable)
        {
            if (nominalVoltage <= maxV)
            {
                return condition switch
                {
                    ClearanceCondition.Condition1 => c1,
                    ClearanceCondition.Condition2 => c2,
                    ClearanceCondition.Condition3 => c3,
                    _ => c1,
                };
            }
        }
        // Above 75kV — use maximum row
        return condition switch
        {
            ClearanceCondition.Condition1 => 6.0,
            ClearanceCondition.Condition2 => 8.0,
            ClearanceCondition.Condition3 => 12.0,
            _ => 6.0,
        };
    }

    /// <summary>
    /// Returns the NEC 110.26(A)(2) minimum working space width.
    /// Width must be at least 30 inches OR the width of the equipment, whichever is greater.
    /// </summary>
    public static double GetRequiredWidth(double equipmentWidthInches)
    {
        return Math.Max(30.0, equipmentWidthInches);
    }

    /// <summary>
    /// Returns the NEC 110.26(A)(3) minimum headroom in feet.
    /// At least 6.5 feet or the equipment height, whichever is greater.
    /// </summary>
    public static double GetRequiredHeight(double equipmentHeightInches)
    {
        double equipHeightFt = equipmentHeightInches / 12.0;
        return Math.Max(6.5, equipHeightFt);
    }

    /// <summary>
    /// Validates all clearance requirements for a piece of equipment.
    /// </summary>
    public static ClearanceResult CheckClearance(EquipmentClearance equipment)
    {
        double reqDepth = GetRequiredDepth(equipment.NominalVoltage, equipment.Condition);
        double reqWidth = GetRequiredWidth(equipment.EquipmentWidthInches);
        double reqHeight = GetRequiredHeight(equipment.EquipmentHeightInches);

        bool depthOk = equipment.ProvidedClearanceDepthFeet >= reqDepth;
        bool widthOk = equipment.ProvidedClearanceWidthInches >= reqWidth;
        bool heightOk = equipment.ProvidedClearanceHeightFeet >= reqHeight;

        var violations = new List<string>();
        if (!depthOk)
            violations.Add($"NEC 110.26(A)(1): Working space depth {equipment.ProvidedClearanceDepthFeet:F1} ft < required {reqDepth:F1} ft");
        if (!widthOk)
            violations.Add($"NEC 110.26(A)(2): Width {equipment.ProvidedClearanceWidthInches:F0} in < required {reqWidth:F0} in");
        if (!heightOk)
            violations.Add($"NEC 110.26(A)(3): Headroom {equipment.ProvidedClearanceHeightFeet:F1} ft < required {reqHeight:F1} ft");

        return new ClearanceResult
        {
            EquipmentId = equipment.Id,
            EquipmentName = equipment.Name,
            RequiredDepthFeet = reqDepth,
            ProvidedDepthFeet = equipment.ProvidedClearanceDepthFeet,
            DepthCompliant = depthOk,
            RequiredWidthInches = reqWidth,
            ProvidedWidthInches = equipment.ProvidedClearanceWidthInches,
            WidthCompliant = widthOk,
            RequiredHeightFeet = reqHeight,
            ProvidedHeightFeet = equipment.ProvidedClearanceHeightFeet,
            HeightCompliant = heightOk,
            Violations = violations,
        };
    }

    /// <summary>
    /// Checks NEC 110.26(E) dedicated equipment space.
    /// The space must extend from floor to 6 ft above equipment (or structural ceiling)
    /// and be the full width and depth of the equipment.
    /// </summary>
    public static DedicatedSpaceResult CheckDedicatedSpace(
        EquipmentClearance equipment,
        double providedWidthInches,
        double providedDepthInches,
        double providedHeightFeet)
    {
        double reqHeight = (equipment.EquipmentHeightInches / 12.0) + 6.0; // 6 ft above equipment top
        if (reqHeight < 6.5) reqHeight = 6.5; // Practical minimum

        var violations = new List<string>();
        bool widthOk = providedWidthInches >= equipment.EquipmentWidthInches;
        bool depthOk = providedDepthInches >= equipment.EquipmentDepthInches;
        bool heightOk = providedHeightFeet >= reqHeight;

        if (!widthOk) violations.Add($"NEC 110.26(E): Dedicated width {providedWidthInches:F0} in < equipment width {equipment.EquipmentWidthInches:F0} in");
        if (!depthOk) violations.Add($"NEC 110.26(E): Dedicated depth {providedDepthInches:F0} in < equipment depth {equipment.EquipmentDepthInches:F0} in");
        if (!heightOk) violations.Add($"NEC 110.26(E): Dedicated height {providedHeightFeet:F1} ft < required {reqHeight:F1} ft");

        return new DedicatedSpaceResult
        {
            EquipmentId = equipment.Id,
            RequiredWidthInches = equipment.EquipmentWidthInches,
            RequiredDepthInches = equipment.EquipmentDepthInches,
            RequiredHeightFeet = reqHeight,
            Compliant = widthOk && depthOk && heightOk,
            Violations = violations,
        };
    }

    /// <summary>
    /// Batch checks multiple equipment items and returns all violations.
    /// </summary>
    public static List<ClearanceResult> CheckAll(IEnumerable<EquipmentClearance> equipment)
    {
        return equipment.Select(CheckClearance).ToList();
    }
}
