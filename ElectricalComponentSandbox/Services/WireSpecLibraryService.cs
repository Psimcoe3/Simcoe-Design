using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Provides lookup, autosizing, and conduit-run binding against a project's
/// <see cref="RunScheduleConfiguration"/>. Mirrors the eVolve "Assign Run"
/// workflow: pick a feeder by amperage or feeder-id, then push that spec's
/// trade-size and parallel count down to the bound <see cref="ConduitRun"/>.
/// </summary>
public sealed class WireSpecLibraryService
{
    private readonly RunScheduleConfiguration _config;

    public WireSpecLibraryService(RunScheduleConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public RunScheduleConfiguration Configuration => _config;

    // ── Lookup ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the wire specification with the given feeder id, or null if
    /// the library has no matching entry.
    /// </summary>
    public WireSpecification? FindByFeederId(string feederId)
    {
        if (string.IsNullOrWhiteSpace(feederId)) return null;
        return _config.WireSpecifications
            .FirstOrDefault(s => string.Equals(s.FeederId, feederId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the smallest wire specification (by amperage) whose amperage
    /// is at least <paramref name="loadAmps"/> for the given configuration
    /// family (e.g. "1P3W-CU"). Useful for sizing a feeder from a calculated load.
    /// </summary>
    public WireSpecification? FindByLoadAmps(string configurationName, double loadAmps)
    {
        if (string.IsNullOrWhiteSpace(configurationName)) return null;
        if (loadAmps <= 0) return null;
        return _config.WireSpecifications
            .Where(s => string.Equals(s.Name, configurationName, StringComparison.OrdinalIgnoreCase))
            .Where(s => s.Amperage >= loadAmps)
            .OrderBy(s => s.Amperage)
            .FirstOrDefault();
    }

    /// <summary>
    /// Returns all wire specifications belonging to a configuration family,
    /// ordered by ascending amperage. Useful for populating a feeder dropdown.
    /// </summary>
    public IReadOnlyList<WireSpecification> ListConfiguration(string configurationName)
    {
        if (string.IsNullOrWhiteSpace(configurationName)) return Array.Empty<WireSpecification>();
        return _config.WireSpecifications
            .Where(s => string.Equals(s.Name, configurationName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Amperage)
            .ToList();
    }

    /// <summary>
    /// Returns all distinct configuration names present in the library
    /// (e.g. "1P3W-CU", "3P4W-CU", "3P4W-AL").
    /// </summary>
    public IReadOnlyList<string> ListConfigurations()
    {
        return _config.WireSpecifications
            .Select(s => s.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Returns the NEC ampacity for a specific gauge / material / insulation
    /// combination, or null when the library has no entry.
    /// </summary>
    public WireSizeEntry? FindWireSize(string materialName, string insulation, string gauge)
    {
        return _config.WireSizes.FirstOrDefault(s =>
            string.Equals(s.MaterialName, materialName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Insulation, insulation, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.Gauge, gauge, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns the total cross-sectional area in square feet for all
    /// conductors in a specification. Drives a fill-percent check against
    /// the bound conduit's inner cross-section.
    /// </summary>
    public double ComputeConductorAreaSquareFeet(WireSpecification spec, string insulation = "THHN")
    {
        ArgumentNullException.ThrowIfNull(spec);

        double total = 0;
        AddArea(spec.MaterialName, insulation, spec.PhaseSize, spec.PhaseQuantity, ref total);
        AddArea(spec.MaterialName, insulation, spec.NeutralSize, spec.NeutralQuantity, ref total);
        AddArea(spec.MaterialName, insulation, spec.GroundSize, spec.GroundQuantity, ref total);
        AddArea(spec.MaterialName, insulation, spec.IsoGroundSize, spec.IsoGroundQuantity, ref total);
        return total * Math.Max(1, spec.ParallelQuantity);
    }

    private void AddArea(string material, string insulation, string gauge, int quantity, ref double total)
    {
        if (string.IsNullOrWhiteSpace(gauge) || quantity <= 0) return;
        var size = FindWireSize(material, insulation, gauge);
        if (size == null) return;
        double radius = size.DiameterFeet / 2.0;
        total += Math.PI * radius * radius * quantity;
    }

    // ── Binding ──────────────────────────────────────────────────────────

    /// <summary>
    /// Result of binding a wire specification to a conduit run.
    /// </summary>
    public sealed record BindRunResult(
        WireSpecification Spec,
        string TradeSize,
        int ParallelQuantity,
        string Description);

    /// <summary>
    /// Pushes a wire specification's trade size, parallel count, and feeder
    /// metadata down onto a conduit run — the eVolve "Assign Run" operation.
    /// </summary>
    public BindRunResult BindToRun(ConduitRun run, WireSpecification spec, ConduitSizeSettings? sizeTable = null)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(spec);

        string tradeSize = ResolveTradeSize(spec.ConduitSizeFeet, sizeTable);
        run.TradeSize = tradeSize;
        run.Voltage = ExtractVoltageHint(spec.Name);
        run.Metadata["FeederId"] = spec.FeederId;
        run.Metadata["FeederConfiguration"] = spec.Name;
        run.Metadata["FeederMaterial"] = spec.MaterialName;
        run.Metadata["WireDescription"] = BuildDescription(spec, _config.WireDescriptionFormat);
        run.ConductorFillPercent = 0; // recompute downstream

        // The conduit-run domain model exposes ParallelQty via metadata only;
        // it doesn't model parallel sets natively. Surface it for downstream
        // exporters that do (eVolve eE_ConduitRun_ParallelQty / ParallelRun).
        run.Metadata["ParallelQty"] = spec.ParallelQuantity.ToString();
        run.Metadata["ParallelRun"] = (spec.ParallelQuantity > 1).ToString();

        return new BindRunResult(spec, tradeSize, spec.ParallelQuantity, BuildDescription(spec, _config.WireDescriptionFormat));
    }

    /// <summary>
    /// Selects and binds a feeder by configuration + load amperage in one step.
    /// </summary>
    public BindRunResult? AutosizeAndBind(ConduitRun run, string configurationName, double loadAmps, ConduitSizeSettings? sizeTable = null)
    {
        var spec = FindByLoadAmps(configurationName, loadAmps);
        if (spec == null) return null;
        return BindToRun(run, spec, sizeTable);
    }

    /// <summary>
    /// Builds the standard eVolve wire description (e.g. "2#1+1#1N+1#6G")
    /// using the configured <see cref="WireDescriptionFormat"/>.
    /// </summary>
    public static string BuildDescription(WireSpecification spec, WireDescriptionFormat format = WireDescriptionFormat.Hyphen)
    {
        ArgumentNullException.ThrowIfNull(spec);
        var parts = new List<string>();
        if (spec.PhaseQuantity > 0 && !string.IsNullOrWhiteSpace(spec.PhaseSize))
            parts.Add($"{spec.PhaseQuantity}#{spec.PhaseSize}");
        if (spec.NeutralQuantity > 0 && !string.IsNullOrWhiteSpace(spec.NeutralSize))
            parts.Add($"{spec.NeutralQuantity}#{spec.NeutralSize}N");
        if (spec.GroundQuantity > 0 && !string.IsNullOrWhiteSpace(spec.GroundSize))
            parts.Add($"{spec.GroundQuantity}#{spec.GroundSize}G");
        if (spec.IsoGroundQuantity > 0 && !string.IsNullOrWhiteSpace(spec.IsoGroundSize))
            parts.Add($"{spec.IsoGroundQuantity}#{spec.IsoGroundSize}IG");

        string sep = format switch
        {
            WireDescriptionFormat.Comma => ", ",
            WireDescriptionFormat.Plus => "+",
            _ => "-",
        };
        return string.Join(sep, parts);
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    /// <summary>
    /// Converts the eVolve <c>ConduitSize</c> (in feet) to the conduit-run
    /// model's trade-size string ("1/2", "3/4", ..., "4"). Picks the
    /// closest matching standard size from the supplied size table when
    /// one is given; otherwise uses the canonical EMT defaults.
    /// </summary>
    public static string ResolveTradeSize(double conduitSizeFeet, ConduitSizeSettings? sizeTable = null)
    {
        // eVolve encodes conduit size as nominal diameter / 12 (e.g. 1.5"/12 = 0.125 ft).
        double nominalInches = conduitSizeFeet * 12.0;
        var lookup = sizeTable?.Sizes
            .Where(s => s.NominalDiameter > 0)
            .OrderBy(s => Math.Abs(s.NominalDiameter - nominalInches))
            .FirstOrDefault();
        if (lookup != null) return lookup.TradeSize;

        // Canonical EMT mapping when no size table is provided.
        return nominalInches switch
        {
            <= 0.625 => "1/2",
            <= 0.875 => "3/4",
            <= 1.125 => "1",
            <= 1.375 => "1-1/4",
            <= 1.75  => "1-1/2",
            <= 2.25  => "2",
            <= 2.75  => "2-1/2",
            <= 3.5   => "3",
            <= 3.75  => "3-1/2",
            _ => "4",
        };
    }

    private static string ExtractVoltageHint(string configurationName)
    {
        // Names like "3P4W-CU-480V" → "480V"; otherwise blank.
        if (string.IsNullOrWhiteSpace(configurationName)) return string.Empty;
        var parts = configurationName.Split('-');
        var voltagePart = parts.FirstOrDefault(p => p.EndsWith("V", StringComparison.OrdinalIgnoreCase) && p.Length <= 5);
        return voltagePart ?? string.Empty;
    }
}
