using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Resolves routing preference rules to <see cref="ComponentFamilyType"/> from
/// the project's family catalog, mirroring Revit's <c>RoutingPreferenceManager</c>.
/// </summary>
public class RoutingPreferenceManager
{
    private readonly ConduitType _conduitType;
    private readonly IReadOnlyList<ComponentFamily> _families;
    private readonly ConduitFittingAngleSettings _angleSettings;

    public RoutingPreferenceManager(
        ConduitType conduitType,
        IReadOnlyList<ComponentFamily> families,
        ConduitFittingAngleSettings angleSettings)
    {
        _conduitType = conduitType;
        _families = families;
        _angleSettings = angleSettings;
    }

    /// <summary>
    /// Selects the fitting type for a given angle, snapping to the nearest
    /// permitted angle first when enforcement is enabled.
    /// </summary>
    public FittingType? SelectFitting(double angleDegrees)
    {
        double resolved = _angleSettings.EnforceAngles
            ? _angleSettings.SnapToNearest(angleDegrees)
            : angleDegrees;

        return _conduitType.SelectFitting(resolved)
            ?? FallbackFitting(resolved);
    }

    /// <summary>
    /// Returns the <see cref="ComponentFamilyType"/> associated with the best
    /// matching routing preference rule for the given angle, or <c>null</c> if
    /// no catalog entry is linked.
    /// </summary>
    public ComponentFamilyType? GetFittingForAngle(double angleDegrees)
    {
        double resolved = _angleSettings.EnforceAngles
            ? _angleSettings.SnapToNearest(angleDegrees)
            : angleDegrees;

        var rule = _conduitType.RoutingPreferences
            .FirstOrDefault(r => r.Matches(resolved));

        if (rule == null)
        {
            // Try fallback angle
            var fallbackType = FallbackFitting(resolved);
            if (fallbackType == null)
                return null;

            rule = _conduitType.RoutingPreferences
                .FirstOrDefault(r => r.FittingType == fallbackType);
        }

        if (rule?.FamilyTypeId == null)
            return null;

        return ResolveFamilyType(rule.FamilyTypeId);
    }

    /// <summary>
    /// Returns all routing preference rules for a given <see cref="RoutingPreferenceRuleGroup"/>.
    /// </summary>
    public IReadOnlyList<RoutingPreferenceRule> GetRulesForGroup(RoutingPreferenceRuleGroup group) =>
        _conduitType.RoutingPreferences.Where(r => r.Group == group).ToList();

    /// <summary>
    /// Returns the <see cref="ConduitFittingAngleSettings"/> used by this manager.
    /// </summary>
    public ConduitFittingAngleSettings AngleSettings => _angleSettings;

    // ----- private helpers -----

    private static FittingType? FallbackFitting(double angleDeg)
    {
        if (angleDeg <= 5 || angleDeg >= 170)
            return FittingType.Coupling;
        if (angleDeg > 60)
            return FittingType.Elbow90;
        if (angleDeg > 5)
            return FittingType.Elbow45;
        return null;
    }

    private ComponentFamilyType? ResolveFamilyType(string familyTypeId)
    {
        foreach (var family in _families)
        {
            var ft = family.Types.FirstOrDefault(t => t.Id == familyTypeId);
            if (ft != null)
                return ft;
        }

        return null;
    }
}
