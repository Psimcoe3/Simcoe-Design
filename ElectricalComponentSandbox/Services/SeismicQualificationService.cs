using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Seismic qualification of electrical equipment per IEEE 693,
/// ASCE 7, and IBC seismic provisions.
/// </summary>
public static class SeismicQualificationService
{
    /// <summary>IEEE 693 qualification level.</summary>
    public enum QualificationLevel
    {
        Low,      // ≤0.1g
        Moderate, // 0.1g–0.5g
        High,     // >0.5g
    }

    /// <summary>ASCE 7 site class.</summary>
    public enum SiteClass
    {
        A, // Hard rock
        B, // Rock
        C, // Very dense soil / soft rock
        D, // Stiff soil (default)
        E, // Soft clay
    }

    /// <summary>Equipment importance category.</summary>
    public enum ImportanceCategory
    {
        Standard,  // Ip = 1.0
        Essential, // Ip = 1.5 (emergency, life safety)
    }

    public enum EquipmentType
    {
        Transformer,
        Switchgear,
        MotorControlCenter,
        PanelBoard,
        BatteryRack,
        Generator,
        UPS,
        CableTray,
        Conduit,
    }

    public record SeismicInput
    {
        public double Sds { get; init; }
        public double Sd1 { get; init; }
        public SiteClass Site { get; init; } = SiteClass.D;
        public ImportanceCategory Importance { get; init; } = ImportanceCategory.Standard;
        public EquipmentType Equipment { get; init; }
        public double EquipmentWeightLbs { get; init; }
        public double MountingHeightFeet { get; init; }
        public double BuildingHeightFeet { get; init; } = 40;
        public int NumberOfAnchors { get; init; } = 4;
    }

    public record SeismicForceResult
    {
        public double Fp { get; init; }
        public double FpMin { get; init; }
        public double FpMax { get; init; }
        public double DesignForce { get; init; }
        public double Ip { get; init; }
        public double Ap { get; init; }
        public double Rp { get; init; }
    }

    public record AnchorageResult
    {
        public double ShearPerAnchorLbs { get; init; }
        public double TensionPerAnchorLbs { get; init; }
        public double OverturnMomentLbFt { get; init; }
        public int NumberOfAnchors { get; init; }
        public string MinAnchorDiameter { get; init; } = string.Empty;
    }

    public record QualificationResult
    {
        public QualificationLevel Level { get; init; }
        public SeismicForceResult Forces { get; init; } = new();
        public AnchorageResult Anchorage { get; init; } = new();
        public List<string> Requirements { get; init; } = new();
    }

    /// <summary>
    /// ASCE 7 component amplification factor (ap) by equipment type.
    /// </summary>
    public static double GetAp(EquipmentType equipment)
    {
        return equipment switch
        {
            EquipmentType.Generator => 1.0,
            EquipmentType.Transformer => 1.0,
            EquipmentType.BatteryRack => 2.5,
            EquipmentType.CableTray => 2.5,
            EquipmentType.Conduit => 2.5,
            EquipmentType.Switchgear => 2.5,
            EquipmentType.MotorControlCenter => 2.5,
            EquipmentType.PanelBoard => 2.5,
            EquipmentType.UPS => 2.5,
            _ => 2.5,
        };
    }

    /// <summary>
    /// ASCE 7 component response modification factor (Rp) by equipment type.
    /// </summary>
    public static double GetRp(EquipmentType equipment)
    {
        return equipment switch
        {
            EquipmentType.Generator => 2.5,
            EquipmentType.Transformer => 2.5,
            EquipmentType.BatteryRack => 6.0,
            EquipmentType.CableTray => 6.0,
            EquipmentType.Conduit => 6.0,
            EquipmentType.Switchgear => 2.5,
            EquipmentType.MotorControlCenter => 2.5,
            EquipmentType.PanelBoard => 2.5,
            EquipmentType.UPS => 2.5,
            _ => 2.5,
        };
    }

    /// <summary>
    /// Importance factor per ASCE 7-22 Table 13.1-1.
    /// </summary>
    public static double GetIp(ImportanceCategory category)
    {
        return category switch
        {
            ImportanceCategory.Essential => 1.5,
            _ => 1.0,
        };
    }

    /// <summary>
    /// IEEE 693 qualification level from spectral acceleration.
    /// </summary>
    public static QualificationLevel DetermineLevel(double sds)
    {
        if (sds <= 0.1) return QualificationLevel.Low;
        if (sds <= 0.5) return QualificationLevel.Moderate;
        return QualificationLevel.High;
    }

    /// <summary>
    /// ASCE 7-22 Eq. 13.3-1 through 13.3-3: horizontal seismic design force (Fp).
    /// Fp = (0.4 × ap × Sds × Wp / (Rp/Ip)) × (1 + 2 × z/h)
    /// Bounded by Fp_min = 0.3 × Sds × Ip × Wp and Fp_max = 1.6 × Sds × Ip × Wp.
    /// </summary>
    public static SeismicForceResult CalculateSeismicForce(SeismicInput input)
    {
        double ap = GetAp(input.Equipment);
        double rp = GetRp(input.Equipment);
        double ip = GetIp(input.Importance);
        double wp = input.EquipmentWeightLbs;

        double z = input.MountingHeightFeet;
        double h = input.BuildingHeightFeet > 0 ? input.BuildingHeightFeet : 1.0;
        double zOverH = Math.Min(z / h, 1.0);

        double fp = (0.4 * ap * input.Sds * wp / (rp / ip)) * (1.0 + 2.0 * zOverH);
        double fpMin = 0.3 * input.Sds * ip * wp;
        double fpMax = 1.6 * input.Sds * ip * wp;

        double design = Math.Max(fpMin, Math.Min(fpMax, fp));

        return new SeismicForceResult
        {
            Fp = Math.Round(fp, 1),
            FpMin = Math.Round(fpMin, 1),
            FpMax = Math.Round(fpMax, 1),
            DesignForce = Math.Round(design, 1),
            Ip = ip,
            Ap = ap,
            Rp = rp,
        };
    }

    /// <summary>
    /// Anchorage design from seismic forces.
    /// Distributes shear equally across anchors; tension from overturning moment.
    /// </summary>
    public static AnchorageResult CalculateAnchorage(SeismicForceResult forces,
        double equipmentWeightLbs, double equipmentHeightFeet, int numberOfAnchors,
        double anchorSpreadFeet)
    {
        if (numberOfAnchors <= 0) numberOfAnchors = 4;
        if (anchorSpreadFeet <= 0) anchorSpreadFeet = 2.0;

        double shearPerAnchor = forces.DesignForce / numberOfAnchors;

        // Overturning moment: force at equipment CG (height/2)
        double cgHeight = equipmentHeightFeet / 2.0;
        double overturnMoment = forces.DesignForce * cgHeight;

        // Tension from overturning: T = M / (spread × n_anchors/2) - W/(n_anchors)
        double tensionFromMoment = overturnMoment / (anchorSpreadFeet * (numberOfAnchors / 2.0));
        double gravityRelief = equipmentWeightLbs / numberOfAnchors;
        double netTension = Math.Max(0, tensionFromMoment - gravityRelief);

        // Recommend anchor diameter based on tension
        string anchorDia;
        if (netTension <= 500)
            anchorDia = "3/8\"";
        else if (netTension <= 1500)
            anchorDia = "1/2\"";
        else if (netTension <= 3000)
            anchorDia = "5/8\"";
        else if (netTension <= 6000)
            anchorDia = "3/4\"";
        else
            anchorDia = "1\"";

        return new AnchorageResult
        {
            ShearPerAnchorLbs = Math.Round(shearPerAnchor, 1),
            TensionPerAnchorLbs = Math.Round(netTension, 1),
            OverturnMomentLbFt = Math.Round(overturnMoment, 1),
            NumberOfAnchors = numberOfAnchors,
            MinAnchorDiameter = anchorDia,
        };
    }

    /// <summary>
    /// Full seismic qualification: level, forces, anchorage, and requirements.
    /// </summary>
    public static QualificationResult Qualify(SeismicInput input, double equipmentHeightFeet = 6,
        double anchorSpreadFeet = 2.5)
    {
        var level = DetermineLevel(input.Sds);
        var forces = CalculateSeismicForce(input);
        var anchorage = CalculateAnchorage(forces, input.EquipmentWeightLbs,
            equipmentHeightFeet, input.NumberOfAnchors, anchorSpreadFeet);

        var reqs = new List<string>();

        if (level == QualificationLevel.High)
            reqs.Add("IEEE 693 High-level shake table test or analysis required");
        else if (level == QualificationLevel.Moderate)
            reqs.Add("IEEE 693 Moderate-level qualification by analysis or experience");

        if (input.Importance == ImportanceCategory.Essential)
            reqs.Add("Ip = 1.5 — equipment must remain operational after design earthquake");

        if (input.Equipment == EquipmentType.BatteryRack)
            reqs.Add("Battery racks require seismic restraint per IBC 2308 and IEEE 484");

        if (input.Equipment == EquipmentType.Generator)
            reqs.Add("Vibration isolators must include seismic snubbers or restraints");

        if (anchorage.TensionPerAnchorLbs > 0)
            reqs.Add($"Anchor bolts subject to combined shear + tension — verify interaction per ACI 318 Appendix D");

        return new QualificationResult
        {
            Level = level,
            Forces = forces,
            Anchorage = anchorage,
            Requirements = reqs,
        };
    }
}
