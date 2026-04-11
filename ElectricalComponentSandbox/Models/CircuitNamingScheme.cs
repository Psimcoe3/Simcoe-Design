namespace ElectricalComponentSandbox.Models;

/// <summary>
/// The individual tokens that can appear in a circuit naming pattern.
/// Maps to Revit's <c>CombinedParameter</c> concept in <c>CircuitNamingScheme</c>.
/// </summary>
public enum NamingToken
{
    /// <summary>Panel-relative circuit number (e.g. "1", "3").</summary>
    CircuitNumber,

    /// <summary>Panel name / designator (e.g. "LP-1", "MDP").</summary>
    PanelName,

    /// <summary>Phase letter (e.g. "A", "B", "C", "AB").</summary>
    PhaseLetter,

    /// <summary>Circuit description / load name.</summary>
    LoadName,

    /// <summary>Fixed prefix text (stored in <see cref="NamingTokenEntry.Literal"/>).</summary>
    Prefix,

    /// <summary>Fixed suffix text (stored in <see cref="NamingTokenEntry.Literal"/>).</summary>
    Suffix
}

/// <summary>
/// A single entry in a naming scheme's token list.
/// For <see cref="NamingToken.Prefix"/> and <see cref="NamingToken.Suffix"/> tokens,
/// <see cref="Literal"/> holds the fixed text value.
/// </summary>
public class NamingTokenEntry
{
    public NamingToken Token { get; set; }

    /// <summary>
    /// Fixed text value for <see cref="NamingToken.Prefix"/> / <see cref="NamingToken.Suffix"/> tokens.
    /// Ignored for dynamic tokens.
    /// </summary>
    public string? Literal { get; set; }

    /// <summary>Separator placed after this token (default "-").</summary>
    public string Separator { get; set; } = "-";
}

/// <summary>
/// A configurable circuit naming scheme analogous to Revit's <c>CircuitNamingScheme</c>.
/// Defines how the formatted name for a circuit is constructed from panel/circuit metadata.
/// </summary>
public class CircuitNamingScheme
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public bool IsBuiltIn { get; set; }

    /// <summary>
    /// Ordered list of tokens that compose the formatted name.
    /// Tokens are concatenated left-to-right with their separators.
    /// The last token's separator is not appended.
    /// </summary>
    public List<NamingTokenEntry> Tokens { get; set; } = new();

    // ── Built-in schemes ─────────────────────────────────────────────────

    /// <summary>Plain circuit number only: "1", "3", "1,3".</summary>
    public static CircuitNamingScheme Numerical() => new()
    {
        Id = "builtin-numerical",
        Name = "Numerical",
        IsBuiltIn = true,
        Tokens = new()
        {
            new() { Token = NamingToken.CircuitNumber, Separator = "" }
        }
    };

    /// <summary>Panel prefix + number: "LP1-1", "MDP-3".</summary>
    public static CircuitNamingScheme PanelPrefixNumber() => new()
    {
        Id = "builtin-panel-prefix",
        Name = "Panel + Number",
        IsBuiltIn = true,
        Tokens = new()
        {
            new() { Token = NamingToken.PanelName, Separator = "-" },
            new() { Token = NamingToken.CircuitNumber, Separator = "" }
        }
    };

    /// <summary>Phase + number: "A-1", "B-3", "AB-5".</summary>
    public static CircuitNamingScheme PhaseNumber() => new()
    {
        Id = "builtin-phase-number",
        Name = "Phase + Number",
        IsBuiltIn = true,
        Tokens = new()
        {
            new() { Token = NamingToken.PhaseLetter, Separator = "-" },
            new() { Token = NamingToken.CircuitNumber, Separator = "" }
        }
    };

    /// <summary>Full: panel-phase-number-load ("LP1-A-1-Lighting 2nd Floor").</summary>
    public static CircuitNamingScheme Full() => new()
    {
        Id = "builtin-full",
        Name = "Full",
        IsBuiltIn = true,
        Tokens = new()
        {
            new() { Token = NamingToken.PanelName, Separator = "-" },
            new() { Token = NamingToken.PhaseLetter, Separator = "-" },
            new() { Token = NamingToken.CircuitNumber, Separator = "-" },
            new() { Token = NamingToken.LoadName, Separator = "" }
        }
    };

    /// <summary>Returns all built-in naming schemes.</summary>
    public static List<CircuitNamingScheme> GetBuiltInSchemes() => new()
    {
        Numerical(),
        PanelPrefixNumber(),
        PhaseNumber(),
        Full()
    };
}
