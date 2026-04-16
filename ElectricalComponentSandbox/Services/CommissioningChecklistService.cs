using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Commissioning and acceptance testing checklists per NETA ATS,
/// NFPA 70B, and IEEE Std 3007.1.
/// </summary>
public static class CommissioningChecklistService
{
    public enum EquipmentType
    {
        Transformer,
        Switchgear,
        MotorControlCenter,
        PanelBoard,
        Generator,
        UPS,
        ATS,
        CableRun,
        GroundingSystem,
        ProtectiveRelay,
    }

    public enum TestCategory
    {
        Visual,
        Mechanical,
        Electrical,
        Functional,
        Performance,
    }

    public enum Priority
    {
        Required,
        Recommended,
        Optional,
    }

    public enum CheckStatus
    {
        NotStarted,
        InProgress,
        Passed,
        Failed,
        NotApplicable,
    }

    public record ChecklistItem
    {
        public string Id { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public TestCategory Category { get; init; }
        public Priority Priority { get; init; }
        public string NETAReference { get; init; } = string.Empty;
        public string AcceptanceCriteria { get; init; } = string.Empty;
        public CheckStatus Status { get; init; } = CheckStatus.NotStarted;
    }

    public record Checklist
    {
        public EquipmentType Equipment { get; init; }
        public string EquipmentTag { get; init; } = string.Empty;
        public List<ChecklistItem> Items { get; init; } = new();
        public int TotalItems => Items.Count;
        public int CompletedItems => Items.Count(i => i.Status == CheckStatus.Passed || i.Status == CheckStatus.NotApplicable);
        public int FailedItems => Items.Count(i => i.Status == CheckStatus.Failed);
        public double CompletionPercent => TotalItems > 0 ? Math.Round((double)CompletedItems / TotalItems * 100, 1) : 0;
        public bool IsComplete => CompletedItems + FailedItems == TotalItems;
        public bool AllPassed => FailedItems == 0 && IsComplete;
    }

    public record CommissioningPlan
    {
        public List<Checklist> Checklists { get; init; } = new();
        public int TotalItems => Checklists.Sum(c => c.TotalItems);
        public int CompletedItems => Checklists.Sum(c => c.CompletedItems);
        public double OverallCompletionPercent => TotalItems > 0 ? Math.Round((double)CompletedItems / TotalItems * 100, 1) : 0;
        public bool AllPassed => Checklists.All(c => c.AllPassed);
        public List<string> OutstandingIssues { get; init; } = new();
    }

    /// <summary>
    /// Generate a NETA ATS-based commissioning checklist for the given equipment type.
    /// </summary>
    public static Checklist GenerateChecklist(EquipmentType equipment, string equipmentTag = "")
    {
        var items = equipment switch
        {
            EquipmentType.Transformer => GetTransformerChecklist(),
            EquipmentType.Switchgear => GetSwitchgearChecklist(),
            EquipmentType.MotorControlCenter => GetMccChecklist(),
            EquipmentType.PanelBoard => GetPanelBoardChecklist(),
            EquipmentType.Generator => GetGeneratorChecklist(),
            EquipmentType.UPS => GetUpsChecklist(),
            EquipmentType.ATS => GetAtsChecklist(),
            EquipmentType.CableRun => GetCableRunChecklist(),
            EquipmentType.GroundingSystem => GetGroundingChecklist(),
            EquipmentType.ProtectiveRelay => GetRelayChecklist(),
            _ => new List<ChecklistItem>(),
        };

        return new Checklist
        {
            Equipment = equipment,
            EquipmentTag = equipmentTag,
            Items = items,
        };
    }

    /// <summary>
    /// Generate a commissioning plan for multiple pieces of equipment.
    /// </summary>
    public static CommissioningPlan GeneratePlan(IEnumerable<(EquipmentType type, string tag)> equipment)
    {
        var checklists = equipment.Select(e => GenerateChecklist(e.type, e.tag)).ToList();
        return new CommissioningPlan
        {
            Checklists = checklists,
            OutstandingIssues = new List<string>(),
        };
    }

    /// <summary>
    /// Evaluate a completed checklist and return a list of outstanding issues.
    /// </summary>
    public static List<string> GetOutstandingIssues(Checklist checklist)
    {
        var issues = new List<string>();

        var failed = checklist.Items.Where(i => i.Status == CheckStatus.Failed).ToList();
        foreach (var item in failed)
            issues.Add($"FAIL: {item.Description} [{item.NETAReference}]");

        var notStarted = checklist.Items
            .Where(i => i.Status == CheckStatus.NotStarted && i.Priority == Priority.Required)
            .ToList();
        foreach (var item in notStarted)
            issues.Add($"INCOMPLETE: {item.Description} (required)");

        return issues;
    }

    /// <summary>
    /// Get the minimum required test instruments for a given equipment type.
    /// </summary>
    public static List<string> GetRequiredInstruments(EquipmentType equipment)
    {
        var common = new List<string> { "Digital Multimeter", "Torque Wrench" };

        var specific = equipment switch
        {
            EquipmentType.Transformer => new List<string>
            {
                "Insulation Resistance Tester (Megger)",
                "Turns Ratio Tester",
                "Winding Resistance Meter",
                "Power Factor Test Set",
            },
            EquipmentType.Switchgear => new List<string>
            {
                "Insulation Resistance Tester (Megger)",
                "Contact Resistance Tester (DLRO)",
                "Circuit Breaker Timer/Analyzer",
                "Hi-Pot Test Set",
            },
            EquipmentType.Generator => new List<string>
            {
                "Insulation Resistance Tester (Megger)",
                "Power Analyzer",
                "Vibration Analyzer",
                "Load Bank (if available)",
            },
            EquipmentType.CableRun => new List<string>
            {
                "Insulation Resistance Tester (Megger)",
                "Time Domain Reflectometer (TDR)",
                "Hi-Pot Test Set (DC or VLF)",
            },
            EquipmentType.GroundingSystem => new List<string>
            {
                "Ground Resistance Tester (Fall-of-Potential)",
                "Earth Continuity Tester",
            },
            EquipmentType.ProtectiveRelay => new List<string>
            {
                "Relay Test Set (secondary injection)",
                "CT/PT Analyzer",
            },
            _ => new List<string> { "Insulation Resistance Tester (Megger)" },
        };

        common.AddRange(specific);
        return common;
    }

    private static List<ChecklistItem> GetTransformerChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "TX-V01", Description = "Verify nameplate data matches specifications", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.1", AcceptanceCriteria = "Matches design documents" },
            new() { Id = "TX-V02", Description = "Inspect for shipping damage and oil leaks", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.1", AcceptanceCriteria = "No visible damage or leaks" },
            new() { Id = "TX-V03", Description = "Verify tap changer position and mechanism", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.1", AcceptanceCriteria = "Correct position per design" },
            new() { Id = "TX-M01", Description = "Verify torque on all bus connections", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.2", AcceptanceCriteria = "Per manufacturer specifications" },
            new() { Id = "TX-M02", Description = "Verify grounding connections", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.2", AcceptanceCriteria = "Bonded per NEC 250" },
            new() { Id = "TX-E01", Description = "Insulation resistance test (megger)", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.3.1", AcceptanceCriteria = "≥ values per NETA Table 100.1" },
            new() { Id = "TX-E02", Description = "Turns ratio test", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.3.2", AcceptanceCriteria = "Within 0.5% of calculated ratio" },
            new() { Id = "TX-E03", Description = "Winding resistance test", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.3.3", AcceptanceCriteria = "Within 5% between phases" },
            new() { Id = "TX-E04", Description = "Power factor / dielectric loss test", Category = TestCategory.Electrical, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.2.3.4", AcceptanceCriteria = "Per manufacturer limits" },
            new() { Id = "TX-F01", Description = "Energize and verify secondary voltage", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.2.4", AcceptanceCriteria = "Within ±5% of design voltage" },
            new() { Id = "TX-P01", Description = "Load test at rated load", Category = TestCategory.Performance, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.2.5", AcceptanceCriteria = "Temperature rise within limits" },
        };
    }

    private static List<ChecklistItem> GetSwitchgearChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "SG-V01", Description = "Verify nameplate data and ratings", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.1", AcceptanceCriteria = "Matches design documents" },
            new() { Id = "SG-V02", Description = "Inspect for physical damage and cleanliness", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.1", AcceptanceCriteria = "No damage, clean interior" },
            new() { Id = "SG-M01", Description = "Verify bus connection torque", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.2", AcceptanceCriteria = "Per manufacturer specifications" },
            new() { Id = "SG-M02", Description = "Verify breaker racking mechanism", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.2", AcceptanceCriteria = "Smooth operation, proper alignment" },
            new() { Id = "SG-E01", Description = "Insulation resistance test — bus", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.3.1", AcceptanceCriteria = "≥ values per NETA Table 100.1" },
            new() { Id = "SG-E02", Description = "Contact resistance test (DLRO)", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.3.2", AcceptanceCriteria = "≤ manufacturer limits" },
            new() { Id = "SG-E03", Description = "Breaker trip test — long-time pickup", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.3.3", AcceptanceCriteria = "Within ±10% of setting" },
            new() { Id = "SG-E04", Description = "Breaker trip test — instantaneous", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.3.3", AcceptanceCriteria = "Within ±15% of setting" },
            new() { Id = "SG-F01", Description = "Verify interlocks and key exchange", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.4", AcceptanceCriteria = "All interlocks function correctly" },
            new() { Id = "SG-F02", Description = "Verify metering and indication", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.1.4", AcceptanceCriteria = "Readings within ±2% of reference" },
        };
    }

    private static List<ChecklistItem> GetMccChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "MCC-V01", Description = "Verify nameplate data and bucket lineup", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.16.1", AcceptanceCriteria = "Matches design documents" },
            new() { Id = "MCC-M01", Description = "Verify bus and stab connection torque", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.16.2", AcceptanceCriteria = "Per manufacturer specifications" },
            new() { Id = "MCC-M02", Description = "Verify bucket insertion and withdrawal", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.16.2", AcceptanceCriteria = "Smooth, fully seated" },
            new() { Id = "MCC-E01", Description = "Insulation resistance test — bus and starters", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.16.3.1", AcceptanceCriteria = "≥ values per NETA Table 100.1" },
            new() { Id = "MCC-E02", Description = "Overload relay calibration check", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.16.3.2", AcceptanceCriteria = "Trip at 6× FLA within spec" },
            new() { Id = "MCC-F01", Description = "Motor start/stop functional test", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.16.4", AcceptanceCriteria = "Correct rotation, runs smoothly" },
            new() { Id = "MCC-F02", Description = "Control circuit verification", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.16.4", AcceptanceCriteria = "HOA, interlocks, indicators correct" },
        };
    }

    private static List<ChecklistItem> GetPanelBoardChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "PNL-V01", Description = "Verify panel schedule and directory", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.5.1", AcceptanceCriteria = "Matches as-built schedule" },
            new() { Id = "PNL-M01", Description = "Verify conductor torque at all terminals", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.5.2", AcceptanceCriteria = "Per manufacturer specifications" },
            new() { Id = "PNL-E01", Description = "Insulation resistance test — bus", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.5.3.1", AcceptanceCriteria = "≥ values per NETA Table 100.1" },
            new() { Id = "PNL-E02", Description = "Verify GFCI/AFCI breaker operation", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.5.3.2", AcceptanceCriteria = "Trip on test button, reset properly" },
            new() { Id = "PNL-F01", Description = "Verify phase balance under load", Category = TestCategory.Functional, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.5.4", AcceptanceCriteria = "Imbalance ≤ 10%" },
        };
    }

    private static List<ChecklistItem> GetGeneratorChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "GEN-V01", Description = "Verify nameplate and fuel system", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.13.1", AcceptanceCriteria = "Matches specifications" },
            new() { Id = "GEN-M01", Description = "Check engine fluid levels and belts", Category = TestCategory.Mechanical, Priority = Priority.Required, NETAReference = "NETA ATS 7.13.2", AcceptanceCriteria = "Per manufacturer specifications" },
            new() { Id = "GEN-E01", Description = "Insulation resistance test — stator", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.13.3.1", AcceptanceCriteria = "≥ values per NETA Table 100.1" },
            new() { Id = "GEN-E02", Description = "Verify voltage and frequency regulation", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.13.3.2", AcceptanceCriteria = "V: ±2%, Hz: ±0.5%" },
            new() { Id = "GEN-F01", Description = "Auto-start on signal and load transfer", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.13.4", AcceptanceCriteria = "Start ≤10 sec, transfer successful" },
            new() { Id = "GEN-P01", Description = "Full load test (1 hr minimum)", Category = TestCategory.Performance, Priority = Priority.Required, NETAReference = "NETA ATS 7.13.5", AcceptanceCriteria = "Stable V, Hz, temp within limits" },
            new() { Id = "GEN-P02", Description = "Verify vibration levels", Category = TestCategory.Performance, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.13.5", AcceptanceCriteria = "Per ISO 8528-9 limits" },
        };
    }

    private static List<ChecklistItem> GetUpsChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "UPS-V01", Description = "Verify UPS and battery nameplate data", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.24.1", AcceptanceCriteria = "Matches specifications" },
            new() { Id = "UPS-E01", Description = "Verify input/output voltage and current", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.24.3.1", AcceptanceCriteria = "Within ±5% of nominal" },
            new() { Id = "UPS-E02", Description = "Battery discharge test", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.24.3.2", AcceptanceCriteria = "Runtime meets design" },
            new() { Id = "UPS-F01", Description = "Simulate utility failure — verify transfer", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.24.4", AcceptanceCriteria = "Seamless transfer, no load drop" },
            new() { Id = "UPS-F02", Description = "Verify bypass operation (manual and auto)", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.24.4", AcceptanceCriteria = "Transfer without interruption" },
            new() { Id = "UPS-F03", Description = "Verify alarm and monitoring outputs", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.24.4", AcceptanceCriteria = "All alarms report correctly" },
        };
    }

    private static List<ChecklistItem> GetAtsChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "ATS-V01", Description = "Verify ATS nameplate and wiring", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.23.1", AcceptanceCriteria = "Matches specifications" },
            new() { Id = "ATS-E01", Description = "Verify voltage sensing thresholds", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.23.3.1", AcceptanceCriteria = "Per design settings" },
            new() { Id = "ATS-F01", Description = "Simulate normal source failure — verify transfer", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.23.4", AcceptanceCriteria = "Transfer within time delay" },
            new() { Id = "ATS-F02", Description = "Simulate normal source return — verify retransfer", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.23.4", AcceptanceCriteria = "Retransfer after time delay" },
            new() { Id = "ATS-F03", Description = "Verify exerciser timer and schedule", Category = TestCategory.Functional, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.23.4", AcceptanceCriteria = "Starts/stops per schedule" },
        };
    }

    private static List<ChecklistItem> GetCableRunChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "CBL-V01", Description = "Verify cable type, size, and routing", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.3.1", AcceptanceCriteria = "Matches design documents" },
            new() { Id = "CBL-V02", Description = "Verify cable support and bend radius", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.3.1", AcceptanceCriteria = "Meets NEC 300.34 minimums" },
            new() { Id = "CBL-E01", Description = "Insulation resistance test (megger)", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.3.3.1", AcceptanceCriteria = "≥ values per NETA Table 100.1" },
            new() { Id = "CBL-E02", Description = "Continuity and phasing verification", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.3.3.2", AcceptanceCriteria = "Correct phase sequence" },
            new() { Id = "CBL-E03", Description = "Hi-pot test (if required)", Category = TestCategory.Electrical, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.3.3.3", AcceptanceCriteria = "No breakdown at test voltage" },
        };
    }

    private static List<ChecklistItem> GetGroundingChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "GND-V01", Description = "Verify grounding electrode system", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.14.1", AcceptanceCriteria = "Per NEC 250, Part III" },
            new() { Id = "GND-E01", Description = "Ground resistance test (fall-of-potential)", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.14.3.1", AcceptanceCriteria = "≤ 5 ohms (or per design)" },
            new() { Id = "GND-E02", Description = "Bonding continuity test", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.14.3.2", AcceptanceCriteria = "< 1 ohm across bonds" },
            new() { Id = "GND-E03", Description = "Neutral-to-ground voltage measurement", Category = TestCategory.Electrical, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.14.3.3", AcceptanceCriteria = "≤ 2V under load" },
        };
    }

    private static List<ChecklistItem> GetRelayChecklist()
    {
        return new List<ChecklistItem>
        {
            new() { Id = "RLY-V01", Description = "Verify relay model and firmware version", Category = TestCategory.Visual, Priority = Priority.Required, NETAReference = "NETA ATS 7.9.1", AcceptanceCriteria = "Matches coordination study" },
            new() { Id = "RLY-E01", Description = "Verify setting group matches study", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.9.3.1", AcceptanceCriteria = "All settings per protection study" },
            new() { Id = "RLY-E02", Description = "Secondary injection test — pickup", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.9.3.2", AcceptanceCriteria = "Pickup within ±5% of setting" },
            new() { Id = "RLY-E03", Description = "Secondary injection test — timing", Category = TestCategory.Electrical, Priority = Priority.Required, NETAReference = "NETA ATS 7.9.3.3", AcceptanceCriteria = "Trip time within ±10% or 1 cycle" },
            new() { Id = "RLY-F01", Description = "Verify trip circuit and breaker operation", Category = TestCategory.Functional, Priority = Priority.Required, NETAReference = "NETA ATS 7.9.4", AcceptanceCriteria = "Relay trip opens breaker" },
            new() { Id = "RLY-F02", Description = "Verify alarm and communication outputs", Category = TestCategory.Functional, Priority = Priority.Recommended, NETAReference = "NETA ATS 7.9.4", AcceptanceCriteria = "SCADA/HMI receives alarms" },
        };
    }
}
