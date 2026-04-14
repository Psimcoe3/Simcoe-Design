using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Performs fast-screen DER hosting capacity checks using thermal, voltage-rise, and export constraints.
/// </summary>
public static class HostingCapacityService
{
    public record HostingNode
    {
        public string Name { get; init; } = string.Empty;
        public double ExistingPeakLoadKw { get; init; }
        public double MinimumLoadKw { get; init; }
        public double ThermalRatingKw { get; init; }
        public double UpstreamExportLimitKw { get; init; }
        public double AllowableVoltageRisePercent { get; init; } = 3.0;
        public double VoltageRisePercentPer100Kw { get; init; }
    }

    public record HostingAssessment
    {
        public string NodeName { get; init; } = string.Empty;
        public double ThermalCapacityKw { get; init; }
        public double VoltageCapacityKw { get; init; }
        public double ExportCapacityKw { get; init; }
        public double RecommendedHostingCapacityKw { get; init; }
        public string LimitingConstraint { get; init; } = string.Empty;
        public bool RequiresDetailedStudy { get; init; }
    }

    public record HostingPortfolioSummary
    {
        public int NodeCount { get; init; }
        public double MinimumHostingCapacityKw { get; init; }
        public string? MostConstrainedNode { get; init; }
        public List<HostingAssessment> Assessments { get; init; } = new();
    }

    public static double CalculateThermalCapacityKw(double thermalRatingKw, double existingPeakLoadKw)
    {
        if (thermalRatingKw < 0 || existingPeakLoadKw < 0)
            throw new ArgumentOutOfRangeException(nameof(thermalRatingKw), "Thermal inputs must be non-negative.");

        return Math.Round(Math.Max(0, thermalRatingKw - existingPeakLoadKw), 2);
    }

    public static double EstimateVoltageRisePercent(double derCapacityKw, double voltageRisePercentPer100Kw)
    {
        if (derCapacityKw < 0 || voltageRisePercentPer100Kw < 0)
            throw new ArgumentOutOfRangeException(nameof(derCapacityKw), "Voltage inputs must be non-negative.");

        return Math.Round(derCapacityKw / 100.0 * voltageRisePercentPer100Kw, 3);
    }

    public static double CalculateVoltageLimitedCapacityKw(double allowableVoltageRisePercent, double voltageRisePercentPer100Kw)
    {
        if (allowableVoltageRisePercent < 0 || voltageRisePercentPer100Kw < 0)
            throw new ArgumentOutOfRangeException(nameof(allowableVoltageRisePercent), "Voltage limit inputs must be non-negative.");

        if (voltageRisePercentPer100Kw == 0)
            return double.MaxValue;

        return Math.Round(allowableVoltageRisePercent / voltageRisePercentPer100Kw * 100.0, 2);
    }

    public static double CalculateExportCapacityKw(double minimumLoadKw, double upstreamExportLimitKw)
    {
        if (minimumLoadKw < 0 || upstreamExportLimitKw < 0)
            throw new ArgumentOutOfRangeException(nameof(minimumLoadKw), "Export inputs must be non-negative.");

        return Math.Round(minimumLoadKw + upstreamExportLimitKw, 2);
    }

    public static HostingAssessment AnalyzeNode(HostingNode node)
    {
        double thermalCapacity = CalculateThermalCapacityKw(node.ThermalRatingKw, node.ExistingPeakLoadKw);
        double voltageCapacity = CalculateVoltageLimitedCapacityKw(node.AllowableVoltageRisePercent, node.VoltageRisePercentPer100Kw);
        double exportCapacity = CalculateExportCapacityKw(node.MinimumLoadKw, node.UpstreamExportLimitKw);

        double recommended = new[] { thermalCapacity, voltageCapacity, exportCapacity }.Min();
        string limitingConstraint = recommended == thermalCapacity
            ? "Thermal headroom"
            : recommended == voltageCapacity
                ? "Voltage rise"
                : "Minimum load and export allowance";

        return new HostingAssessment
        {
            NodeName = node.Name,
            ThermalCapacityKw = thermalCapacity,
            VoltageCapacityKw = voltageCapacity,
            ExportCapacityKw = exportCapacity,
            RecommendedHostingCapacityKw = recommended == double.MaxValue ? Math.Min(thermalCapacity, exportCapacity) : recommended,
            LimitingConstraint = limitingConstraint,
            RequiresDetailedStudy = limitingConstraint != "Thermal headroom",
        };
    }

    public static HostingPortfolioSummary AnalyzePortfolio(IEnumerable<HostingNode> nodes)
    {
        var assessments = (nodes ?? Array.Empty<HostingNode>())
            .Select(AnalyzeNode)
            .ToList();

        var mostConstrained = assessments
            .OrderBy(assessment => assessment.RecommendedHostingCapacityKw)
            .FirstOrDefault();

        return new HostingPortfolioSummary
        {
            NodeCount = assessments.Count,
            MinimumHostingCapacityKw = mostConstrained?.RecommendedHostingCapacityKw ?? 0,
            MostConstrainedNode = mostConstrained?.NodeName,
            Assessments = assessments,
        };
    }
}