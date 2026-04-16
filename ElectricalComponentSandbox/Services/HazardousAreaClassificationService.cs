using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Hazardous area classification per NEC Article 500 (Division system) and
/// Article 505 (Zone system), with equipment protection requirements.
/// </summary>
public static class HazardousAreaClassificationService
{
    /// <summary>NEC 500 Division system.</summary>
    public enum Division
    {
        Unclassified,
        Division1,
        Division2,
    }

    /// <summary>NEC 505/506 Zone system (IEC harmonized).</summary>
    public enum Zone
    {
        Unclassified,
        Zone0,
        Zone1,
        Zone2,
        Zone20,
        Zone21,
        Zone22,
    }

    /// <summary>NEC 500 Class of hazardous material.</summary>
    public enum HazardClass
    {
        ClassI,   // Flammable gases or vapors
        ClassII,  // Combustible dust
        ClassIII, // Ignitable fibers/flyings
    }

    /// <summary>NEC 500 Group within a Class.</summary>
    public enum MaterialGroup
    {
        GroupA, // Acetylene
        GroupB, // Hydrogen, fuel gases
        GroupC, // Ethylene, ethyl ether
        GroupD, // Propane, gasoline, natural gas
        GroupE, // Metal dust
        GroupF, // Carbon black, coal dust
        GroupG, // Grain, wood, plastic dust
    }

    /// <summary>Temperature class per NEC Table 500.8(C).</summary>
    public enum TemperatureClass
    {
        T1,  // 450°C
        T2,  // 300°C
        T2A, // 280°C
        T2B, // 260°C
        T2C, // 230°C
        T2D, // 215°C
        T3,  // 200°C
        T3A, // 180°C
        T3B, // 165°C
        T3C, // 160°C
        T4,  // 135°C
        T4A, // 120°C
        T5,  // 100°C
        T6,  // 85°C
    }

    /// <summary>Equipment protection method.</summary>
    public enum ProtectionMethod
    {
        Explosionproof,         // NEC 501, "XP" enclosure
        DustIgnitionproof,      // NEC 502
        IntrinsicallySafe,      // NEC 504
        Purged,                 // NFPA 496
        Hermetically_Sealed,
        NonIncendive,
        DustTight,
        GeneralPurpose,
    }

    public record AreaClassification
    {
        public HazardClass Class { get; init; }
        public Division Division { get; init; }
        public Zone Zone { get; init; }
        public MaterialGroup Group { get; init; }
        public TemperatureClass TempClass { get; init; }
        public string Description { get; init; } = string.Empty;
    }

    public record EquipmentRequirement
    {
        public ProtectionMethod Method { get; init; }
        public string NecArticle { get; init; } = string.Empty;
        public TemperatureClass MinTempClass { get; init; }
        public string EnclosureMarking { get; init; } = string.Empty;
        public bool IntrinsicSafetyPermitted { get; init; }
    }

    public record ClassificationResult
    {
        public AreaClassification Area { get; init; } = new();
        public EquipmentRequirement Equipment { get; init; } = new();
        public List<string> Warnings { get; init; } = new();
    }

    /// <summary>
    /// Maximum surface temperature (°C) for a given temperature class.
    /// NEC Table 500.8(C).
    /// </summary>
    public static double GetMaxSurfaceTemp(TemperatureClass tClass)
    {
        return tClass switch
        {
            TemperatureClass.T1 => 450,
            TemperatureClass.T2 => 300,
            TemperatureClass.T2A => 280,
            TemperatureClass.T2B => 260,
            TemperatureClass.T2C => 230,
            TemperatureClass.T2D => 215,
            TemperatureClass.T3 => 200,
            TemperatureClass.T3A => 180,
            TemperatureClass.T3B => 165,
            TemperatureClass.T3C => 160,
            TemperatureClass.T4 => 135,
            TemperatureClass.T4A => 120,
            TemperatureClass.T5 => 100,
            TemperatureClass.T6 => 85,
            _ => 450,
        };
    }

    /// <summary>
    /// Map a Division classification to its equivalent Zone classification.
    /// NEC 505.5(A).
    /// </summary>
    public static Zone DivisionToZone(HazardClass hazClass, Division division)
    {
        if (division == Division.Unclassified) return Zone.Unclassified;

        return hazClass switch
        {
            HazardClass.ClassI => division == Division.Division1 ? Zone.Zone1 : Zone.Zone2,
            HazardClass.ClassII => division == Division.Division1 ? Zone.Zone21 : Zone.Zone22,
            HazardClass.ClassIII => division == Division.Division1 ? Zone.Zone21 : Zone.Zone22,
            _ => Zone.Unclassified,
        };
    }

    /// <summary>
    /// Determine the required equipment protection method based on class and division.
    /// NEC Articles 501, 502, 503.
    /// </summary>
    public static EquipmentRequirement GetEquipmentRequirement(HazardClass hazClass,
        Division division, MaterialGroup group, TemperatureClass tempClass)
    {
        if (division == Division.Unclassified)
        {
            return new EquipmentRequirement
            {
                Method = ProtectionMethod.GeneralPurpose,
                NecArticle = "General",
                MinTempClass = TemperatureClass.T1,
                EnclosureMarking = "General Purpose",
                IntrinsicSafetyPermitted = false,
            };
        }

        return hazClass switch
        {
            HazardClass.ClassI => GetClassIRequirement(division, group, tempClass),
            HazardClass.ClassII => GetClassIIRequirement(division, group, tempClass),
            HazardClass.ClassIII => GetClassIIIRequirement(division, tempClass),
            _ => new EquipmentRequirement
            {
                Method = ProtectionMethod.GeneralPurpose,
                NecArticle = "General",
                MinTempClass = tempClass,
                EnclosureMarking = "General Purpose",
            },
        };
    }

    /// <summary>
    /// Classify an area and return full requirements.
    /// </summary>
    public static ClassificationResult ClassifyArea(HazardClass hazClass, Division division,
        MaterialGroup group, TemperatureClass tempClass, double autoIgnitionTempC = 0)
    {
        var zone = DivisionToZone(hazClass, division);
        string description = BuildDescription(hazClass, division, group);

        var area = new AreaClassification
        {
            Class = hazClass,
            Division = division,
            Zone = zone,
            Group = group,
            TempClass = tempClass,
            Description = description,
        };

        var equipment = GetEquipmentRequirement(hazClass, division, group, tempClass);

        var warnings = new List<string>();

        if (autoIgnitionTempC > 0)
        {
            double maxSurface = GetMaxSurfaceTemp(tempClass);
            if (maxSurface >= autoIgnitionTempC)
                warnings.Add($"T-class max {maxSurface}°C meets or exceeds AIT {autoIgnitionTempC}°C — select a lower T-class");
        }

        if (hazClass == HazardClass.ClassI && (group == MaterialGroup.GroupA || group == MaterialGroup.GroupB))
            warnings.Add("Group A/B materials require heightened protection — verify fitting ratings");

        if (division == Division.Division1)
            warnings.Add("Division 1 — hazardous concentrations expected during normal operation");

        return new ClassificationResult
        {
            Area = area,
            Equipment = equipment,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Check whether a given temperature class is adequate for a material's auto-ignition temperature.
    /// Equipment T-class max must be below 80% of AIT per good practice.
    /// </summary>
    public static bool IsTempClassAdequate(TemperatureClass tClass, double autoIgnitionTempC)
    {
        if (autoIgnitionTempC <= 0) return true;
        double maxSurface = GetMaxSurfaceTemp(tClass);
        return maxSurface < autoIgnitionTempC;
    }

    /// <summary>
    /// Recommend the minimum temperature class for a given auto-ignition temperature.
    /// Returns the highest T-class whose max surface temp is below the AIT.
    /// </summary>
    public static TemperatureClass RecommendTempClass(double autoIgnitionTempC)
    {
        // Walk from highest T (lowest max temp) to find the safest class below AIT
        var classes = new[]
        {
            TemperatureClass.T6, TemperatureClass.T5, TemperatureClass.T4A, TemperatureClass.T4,
            TemperatureClass.T3C, TemperatureClass.T3B, TemperatureClass.T3A, TemperatureClass.T3,
            TemperatureClass.T2D, TemperatureClass.T2C, TemperatureClass.T2B, TemperatureClass.T2A,
            TemperatureClass.T2, TemperatureClass.T1,
        };

        foreach (var tc in classes)
        {
            double max = GetMaxSurfaceTemp(tc);
            if (max < autoIgnitionTempC) return tc;
        }

        return TemperatureClass.T6; // Most restrictive fallback
    }

    private static EquipmentRequirement GetClassIRequirement(Division division,
        MaterialGroup group, TemperatureClass tempClass)
    {
        if (division == Division.Division1)
        {
            return new EquipmentRequirement
            {
                Method = ProtectionMethod.Explosionproof,
                NecArticle = "NEC 501.10(A)",
                MinTempClass = tempClass,
                EnclosureMarking = $"Class I, Div 1, Group {group}",
                IntrinsicSafetyPermitted = true,
            };
        }

        return new EquipmentRequirement
        {
            Method = ProtectionMethod.NonIncendive,
            NecArticle = "NEC 501.10(B)",
            MinTempClass = tempClass,
            EnclosureMarking = $"Class I, Div 2, Group {group}",
            IntrinsicSafetyPermitted = true,
        };
    }

    private static EquipmentRequirement GetClassIIRequirement(Division division,
        MaterialGroup group, TemperatureClass tempClass)
    {
        if (division == Division.Division1)
        {
            return new EquipmentRequirement
            {
                Method = ProtectionMethod.DustIgnitionproof,
                NecArticle = "NEC 502.10(A)",
                MinTempClass = tempClass,
                EnclosureMarking = $"Class II, Div 1, Group {group}",
                IntrinsicSafetyPermitted = true,
            };
        }

        return new EquipmentRequirement
        {
            Method = ProtectionMethod.DustTight,
            NecArticle = "NEC 502.10(B)",
            MinTempClass = tempClass,
            EnclosureMarking = $"Class II, Div 2, Group {group}",
            IntrinsicSafetyPermitted = true,
        };
    }

    private static EquipmentRequirement GetClassIIIRequirement(Division division,
        TemperatureClass tempClass)
    {
        var method = division == Division.Division1
            ? ProtectionMethod.DustTight
            : ProtectionMethod.GeneralPurpose;

        return new EquipmentRequirement
        {
            Method = method,
            NecArticle = division == Division.Division1 ? "NEC 503.10(A)" : "NEC 503.10(B)",
            MinTempClass = tempClass,
            EnclosureMarking = $"Class III, Div {(division == Division.Division1 ? "1" : "2")}",
            IntrinsicSafetyPermitted = false,
        };
    }

    private static string BuildDescription(HazardClass hazClass, Division division, MaterialGroup group)
    {
        string className = hazClass switch
        {
            HazardClass.ClassI => "Flammable gases/vapors",
            HazardClass.ClassII => "Combustible dust",
            HazardClass.ClassIII => "Ignitable fibers/flyings",
            _ => "Unknown",
        };

        string divDesc = division switch
        {
            Division.Division1 => "normally present",
            Division.Division2 => "abnormal conditions only",
            Division.Unclassified => "unclassified",
            _ => "unknown",
        };

        return $"{className} ({group}) — {divDesc}";
    }
}
