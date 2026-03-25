using System;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox;

public partial class MainWindow
{
    private static bool IsFinitePositiveLength(double value)
    {
        return !double.IsNaN(value) && !double.IsInfinity(value) && value > 0.0;
    }

    private static string? ResolveConduitTradeSize(ConduitComponent conduit)
    {
        return StratusImperialDefaultsLoader.ResolveTradeSizeFromConduitTypeText(conduit.ConduitType)
            ?? StratusImperialDefaultsLoader.ResolveTradeSizeFromOuterDiameterFeet(conduit.Diameter);
    }

    private StratusImperialBendSetting? TryGetConduitImperialBendSetting(ConduitComponent conduit)
    {
        if (!_stratusImperialDefaults.HasData)
            return null;

        var tradeSize = ResolveConduitTradeSize(conduit);
        if (string.IsNullOrWhiteSpace(tradeSize))
            return null;

        return _stratusImperialDefaults.FindPreferredBendSetting(conduit.ConduitType ?? string.Empty, tradeSize);
    }

    private double GetConduitMinimumSegmentSpacingFeet(ConduitComponent conduit)
    {
        return TryGetConduitImperialBendSetting(conduit)?.MinimumDistanceBetweenBendsFeet ?? 0.0;
    }

    private bool TryValidateConduitMinimumSegmentSpacing(
        ConduitComponent conduit,
        out double minimumSpacingFeet,
        out double shortestSegmentFeet,
        out int shortestSegmentIndex)
    {
        minimumSpacingFeet = GetConduitMinimumSegmentSpacingFeet(conduit);
        shortestSegmentFeet = double.PositiveInfinity;
        shortestSegmentIndex = -1;

        if (minimumSpacingFeet <= InViewDimensionMinSpan)
            return true;

        var path = conduit.GetPathPoints();
        if (path.Count < 2)
            return true;

        for (int i = 0; i < path.Count - 1; i++)
        {
            var length = (path[i + 1] - path[i]).Length;
            if (length < shortestSegmentFeet)
            {
                shortestSegmentFeet = length;
                shortestSegmentIndex = i;
            }
        }

        return shortestSegmentFeet + 1e-6 >= minimumSpacingFeet;
    }

    private static void SyncConduitDimensionalParameters(ConduitComponent conduit)
    {
        conduit.Parameters.Width = conduit.Diameter;
        conduit.Parameters.Height = conduit.Diameter;
        conduit.Parameters.Depth = conduit.Length;
    }

    private void ApplyImperialDefaultsToConduit(ConduitComponent conduit)
    {
        SyncConduitDimensionalParameters(conduit);

        var bendSetting = TryGetConduitImperialBendSetting(conduit);
        if (bendSetting != null)
            conduit.BendRadius = Math.Max(conduit.BendRadius, bendSetting.RadiusFeet);
    }
}
