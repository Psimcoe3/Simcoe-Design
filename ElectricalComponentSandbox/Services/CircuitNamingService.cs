using ElectricalComponentSandbox.Models;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Formats circuit display names using a configurable <see cref="CircuitNamingScheme"/>.
/// Analogous to Revit's <c>CircuitNamingScheme</c> + <c>GetCombinedParameters</c> evaluation.
/// </summary>
public static class CircuitNamingService
{
    /// <summary>
    /// Evaluates the naming scheme against a circuit and its panel to produce a formatted name.
    /// </summary>
    /// <param name="circuit">The circuit to name.</param>
    /// <param name="panel">The panel schedule containing this circuit (for panel name).</param>
    /// <param name="scheme">The naming scheme to apply. When null, returns <see cref="Circuit.CircuitNumber"/>.</param>
    /// <returns>Formatted circuit name string.</returns>
    public static string FormatCircuitName(Circuit circuit, PanelSchedule panel, CircuitNamingScheme? scheme)
    {
        if (scheme == null || scheme.Tokens.Count == 0)
            return circuit.CircuitNumber;

        var parts = new List<string>();
        for (int i = 0; i < scheme.Tokens.Count; i++)
        {
            var entry = scheme.Tokens[i];
            string value = ResolveToken(entry, circuit, panel);
            if (string.IsNullOrEmpty(value))
                continue;

            parts.Add(value);

            // Append separator unless this is the last token with a value
            bool isLast = true;
            for (int j = i + 1; j < scheme.Tokens.Count; j++)
            {
                string nextVal = ResolveToken(scheme.Tokens[j], circuit, panel);
                if (!string.IsNullOrEmpty(nextVal))
                {
                    isLast = false;
                    break;
                }
            }
            if (!isLast && !string.IsNullOrEmpty(entry.Separator))
                parts.Add(entry.Separator);
        }

        return string.Concat(parts);
    }

    /// <summary>
    /// Batch-formats all circuits in a panel schedule.
    /// Returns a dictionary mapping circuit ID → formatted name.
    /// </summary>
    public static Dictionary<string, string> FormatAll(PanelSchedule panel, CircuitNamingScheme? scheme)
    {
        var result = new Dictionary<string, string>();
        foreach (var circuit in panel.Circuits)
        {
            result[circuit.Id] = FormatCircuitName(circuit, panel, scheme);
        }
        return result;
    }

    private static string ResolveToken(NamingTokenEntry entry, Circuit circuit, PanelSchedule panel)
    {
        return entry.Token switch
        {
            NamingToken.CircuitNumber => circuit.CircuitNumber,
            NamingToken.PanelName => panel.PanelName,
            NamingToken.PhaseLetter => circuit.Phase,
            NamingToken.LoadName => circuit.Description,
            NamingToken.Prefix => entry.Literal ?? string.Empty,
            NamingToken.Suffix => entry.Literal ?? string.Empty,
            _ => string.Empty
        };
    }
}
