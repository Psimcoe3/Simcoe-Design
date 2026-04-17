using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Sweeps relay coordination over multiple fault levels and summarizes worst-case margins.
/// </summary>
public static class CoordinationSweepService
{
    public record SweepPoint
    {
        public double FaultCurrentAmps { get; init; }
        public double DownstreamTripSec { get; init; }
        public double UpstreamTripSec { get; init; }
        public double MarginSec { get; init; }
        public bool IsCoordinated { get; init; }
    }

    public record SweepSummary
    {
        public string UpstreamId { get; init; } = string.Empty;
        public string DownstreamId { get; init; } = string.Empty;
        public double MinimumMarginSec { get; init; }
        public double MaximumMarginSec { get; init; }
        public bool IsFullyCoordinated { get; init; }
        public int EvaluatedPointCount { get; init; }
        public SweepPoint WorstPoint { get; init; } = new();
        public List<SweepPoint> EvaluatedPoints { get; init; } = new();
        public List<SweepPoint> Violations { get; init; } = new();
    }

    public static List<double> BuildFaultCurrentSeries(double minimumFaultCurrentAmps, double maximumFaultCurrentAmps, int pointCount)
    {
        if (minimumFaultCurrentAmps <= 0)
            throw new ArgumentException("Minimum fault current must be positive.");
        if (maximumFaultCurrentAmps < minimumFaultCurrentAmps)
            throw new ArgumentException("Maximum fault current must be greater than or equal to minimum fault current.");

        pointCount = Math.Max(pointCount, 2);
        if (Math.Abs(maximumFaultCurrentAmps - minimumFaultCurrentAmps) < 0.001)
            return new List<double> { Math.Round(minimumFaultCurrentAmps, 2), Math.Round(maximumFaultCurrentAmps, 2) };

        double step = (maximumFaultCurrentAmps - minimumFaultCurrentAmps) / (pointCount - 1);
        var values = new List<double>(pointCount);
        for (int index = 0; index < pointCount; index++)
        {
            double value = minimumFaultCurrentAmps + (step * index);
            values.Add(Math.Round(index == pointCount - 1 ? maximumFaultCurrentAmps : value, 2));
        }

        return values;
    }

    public static SweepSummary EvaluateFaultLevels(
        ProtectiveRelayService.RelaySettings upstream,
        ProtectiveRelayService.RelaySettings downstream,
        IEnumerable<double> faultCurrentsAmps,
        double minimumCtiSec = 0.3)
    {
        ArgumentNullException.ThrowIfNull(upstream);
        ArgumentNullException.ThrowIfNull(downstream);

        var points = (faultCurrentsAmps ?? Array.Empty<double>())
            .Where(current => current > 0)
            .Distinct()
            .OrderBy(current => current)
            .Select(current => ToSweepPoint(ProtectiveRelayService.CheckCoordination(upstream, downstream, current, minimumCtiSec)))
            .ToList();

        if (points.Count == 0)
            throw new ArgumentException("At least one positive fault current is required.");

        var worstPoint = points.OrderBy(point => point.MarginSec).First();
        var violations = points.Where(point => !point.IsCoordinated).ToList();

        return new SweepSummary
        {
            UpstreamId = upstream.Id,
            DownstreamId = downstream.Id,
            MinimumMarginSec = Math.Round(points.Min(point => point.MarginSec), 4),
            MaximumMarginSec = Math.Round(points.Max(point => point.MarginSec), 4),
            IsFullyCoordinated = violations.Count == 0,
            EvaluatedPointCount = points.Count,
            WorstPoint = worstPoint,
            EvaluatedPoints = points,
            Violations = violations,
        };
    }

    public static SweepSummary SweepRange(
        ProtectiveRelayService.RelaySettings upstream,
        ProtectiveRelayService.RelaySettings downstream,
        double minimumFaultCurrentAmps,
        double maximumFaultCurrentAmps,
        int pointCount = 10,
        double minimumCtiSec = 0.3)
    {
        var currents = BuildFaultCurrentSeries(minimumFaultCurrentAmps, maximumFaultCurrentAmps, pointCount);
        return EvaluateFaultLevels(upstream, downstream, currents, minimumCtiSec);
    }

    private static SweepPoint ToSweepPoint(ProtectiveRelayService.CoordinationResult result)
    {
        return new SweepPoint
        {
            FaultCurrentAmps = result.FaultCurrentAmps,
            DownstreamTripSec = result.DownstreamTripSec,
            UpstreamTripSec = result.UpstreamTripSec,
            MarginSec = result.CoordinationMarginSec,
            IsCoordinated = result.IsCoordinated,
        };
    }
}