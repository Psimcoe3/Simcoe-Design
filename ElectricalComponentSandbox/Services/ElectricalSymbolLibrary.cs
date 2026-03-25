using System.Windows;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Defines a 2D electrical symbol as a collection of geometric primitives.
/// Symbols follow IEEE Std 315 / NEC standard plan view representations.
/// </summary>
public class SymbolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Symbol width and height in document units (before scaling)</summary>
    public double Width { get; set; } = 20;
    public double Height { get; set; } = 20;

    /// <summary>Geometric primitives that make up this symbol</summary>
    public List<SymbolPrimitive> Primitives { get; set; } = new();
}

public enum SymbolPrimitiveType
{
    Line,       // Two-point line
    Circle,     // Center + radius
    Arc,        // Center + radius + start/sweep angles
    Rectangle,  // Top-left + width + height
    Text,       // Position + text string
    Polyline    // Multiple connected points
}

public class SymbolPrimitive
{
    public SymbolPrimitiveType Type { get; set; }
    public List<Point> Points { get; set; } = new();
    public double Radius { get; set; }
    public double StartAngle { get; set; }
    public double SweepAngle { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsFilled { get; set; }
}

/// <summary>
/// Library of standard electrical plan symbols.
/// </summary>
public class ElectricalSymbolLibrary
{
    private readonly Dictionary<string, SymbolDefinition> _symbols = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, SymbolDefinition> Symbols => _symbols;

    public ElectricalSymbolLibrary()
    {
        RegisterStandardSymbols();
    }

    public SymbolDefinition? GetSymbol(string name) =>
        _symbols.TryGetValue(name, out var sym) ? sym : null;

    public IReadOnlyList<SymbolDefinition> GetByCategory(string category) =>
        _symbols.Values.Where(s => string.Equals(s.Category, category, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<string> GetCategories() =>
        _symbols.Values.Select(s => s.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();

    private void Register(SymbolDefinition symbol) => _symbols[symbol.Name] = symbol;

    private void RegisterStandardSymbols()
    {
        RegisterReceptacles();
        RegisterSwitches();
        RegisterLighting();
        RegisterDistribution();
        RegisterFireAlarm();
        RegisterData();
    }

    // ───────────────────────────────────────────────────────────
    //  Receptacles
    // ───────────────────────────────────────────────────────────

    private void RegisterReceptacles()
    {
        // 1. Duplex Receptacle - Circle with two parallel vertical lines (prongs)
        Register(new SymbolDefinition
        {
            Name = "Duplex Receptacle",
            Category = "Receptacles",
            Description = "Standard 120V duplex receptacle outlet",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-3, -4), new Point(-3, 4) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(3, -4), new Point(3, 4) } }
            }
        });

        // 2. GFCI Receptacle - Duplex receptacle with "GFI" text
        Register(new SymbolDefinition
        {
            Name = "GFCI Receptacle",
            Category = "Receptacles",
            Description = "Ground-fault circuit interrupter receptacle",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-3, -4), new Point(-3, 4) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(3, -4), new Point(3, 4) } },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, -6) }, Text = "GFI" }
            }
        });

        // 3. Weatherproof Receptacle - Duplex receptacle with "WP" text
        Register(new SymbolDefinition
        {
            Name = "Weatherproof Receptacle",
            Category = "Receptacles",
            Description = "Weatherproof rated receptacle for exterior use",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-3, -4), new Point(-3, 4) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(3, -4), new Point(3, 4) } },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, -6) }, Text = "WP" }
            }
        });

        // 4. 240V Receptacle - Circle with three radial lines (120 degrees apart)
        Register(new SymbolDefinition
        {
            Name = "240V Receptacle",
            Category = "Receptacles",
            Description = "240V single-phase receptacle outlet",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                // Three lines radiating from center at 120-degree intervals
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(0, 0), new Point(0, -6) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(0, 0), new Point(5.2, 3) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(0, 0), new Point(-5.2, 3) } }
            }
        });

        // 5. Floor Receptacle - Circle with an X through it
        Register(new SymbolDefinition
        {
            Name = "Floor Receptacle",
            Category = "Receptacles",
            Description = "Floor-mounted receptacle outlet",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-5.6, -5.6), new Point(5.6, 5.6) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(5.6, -5.6), new Point(-5.6, 5.6) } }
            }
        });

        // 6. Dedicated Receptacle - Circle with triangle inside
        Register(new SymbolDefinition
        {
            Name = "Dedicated Receptacle",
            Category = "Receptacles",
            Description = "Dedicated (isolated ground) receptacle",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new()
                {
                    Type = SymbolPrimitiveType.Polyline,
                    Points = { new Point(0, -5), new Point(5, 4), new Point(-5, 4), new Point(0, -5) }
                }
            }
        });
    }

    // ───────────────────────────────────────────────────────────
    //  Switches
    // ───────────────────────────────────────────────────────────

    private void RegisterSwitches()
    {
        // 7. Single Pole Switch - Circle with "S" and a tick mark
        Register(new SymbolDefinition
        {
            Name = "Single Pole Switch",
            Category = "Switches",
            Description = "Single pole light switch",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 6 },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "S" },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(6, 0), new Point(10, 0) } }
            }
        });

        // 8. 3-Way Switch - Circle with "S3"
        Register(new SymbolDefinition
        {
            Name = "3-Way Switch",
            Category = "Switches",
            Description = "Three-way light switch",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 6 },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "S3" },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(6, 0), new Point(10, 0) } }
            }
        });

        // 9. 4-Way Switch - Circle with "S4"
        Register(new SymbolDefinition
        {
            Name = "4-Way Switch",
            Category = "Switches",
            Description = "Four-way light switch",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 6 },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "S4" },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(6, 0), new Point(10, 0) } }
            }
        });

        // 10. Dimmer Switch - Circle with "SD"
        Register(new SymbolDefinition
        {
            Name = "Dimmer Switch",
            Category = "Switches",
            Description = "Dimmer light switch",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 6 },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "SD" },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(6, 0), new Point(10, 0) } }
            }
        });
    }

    // ───────────────────────────────────────────────────────────
    //  Lighting
    // ───────────────────────────────────────────────────────────

    private void RegisterLighting()
    {
        // 11. Ceiling Light - Circle with 4 radiating lines at 45-degree angles
        Register(new SymbolDefinition
        {
            Name = "Ceiling Light",
            Category = "Lighting",
            Description = "Surface-mounted ceiling light fixture",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 6 },
                // Four lines radiating outward from the circle edge at 45-degree angles
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(4.2, -4.2), new Point(8, -8) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(4.2, 4.2), new Point(8, 8) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-4.2, -4.2), new Point(-8, -8) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-4.2, 4.2), new Point(-8, 8) } }
            }
        });

        // 12. Recessed Light - Circle with filled inner circle
        Register(new SymbolDefinition
        {
            Name = "Recessed Light",
            Category = "Lighting",
            Description = "Recessed ceiling light (can light)",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 4, IsFilled = true }
            }
        });

        // 13. Fluorescent Light - Elongated rectangle
        Register(new SymbolDefinition
        {
            Name = "Fluorescent Light",
            Category = "Lighting",
            Description = "Fluorescent or LED linear light fixture",
            Width = 40,
            Height = 12,
            Primitives = new List<SymbolPrimitive>
            {
                new()
                {
                    Type = SymbolPrimitiveType.Rectangle,
                    Points = { new Point(-18, -4) },
                    Width = 36,
                    Height = 8
                }
            }
        });

        // 14. Emergency Light - Rectangle with "EM" text
        Register(new SymbolDefinition
        {
            Name = "Emergency Light",
            Category = "Lighting",
            Description = "Emergency battery-backed light fixture",
            Width = 24,
            Height = 16,
            Primitives = new List<SymbolPrimitive>
            {
                new()
                {
                    Type = SymbolPrimitiveType.Rectangle,
                    Points = { new Point(-10, -5) },
                    Width = 20,
                    Height = 10
                },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "EM" }
            }
        });

        // 15. Exit Sign - Rectangle with "EXIT" text
        Register(new SymbolDefinition
        {
            Name = "Exit Sign",
            Category = "Lighting",
            Description = "Illuminated exit sign",
            Width = 28,
            Height = 16,
            Primitives = new List<SymbolPrimitive>
            {
                new()
                {
                    Type = SymbolPrimitiveType.Rectangle,
                    Points = { new Point(-12, -5) },
                    Width = 24,
                    Height = 10
                },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "EXIT" }
            }
        });
    }

    // ───────────────────────────────────────────────────────────
    //  Panels & Distribution
    // ───────────────────────────────────────────────────────────

    private void RegisterDistribution()
    {
        // 16. Panel Board - Rectangle with diagonal corner marks
        Register(new SymbolDefinition
        {
            Name = "Panel Board",
            Category = "Distribution",
            Description = "Electrical panel board / load center",
            Width = 30,
            Height = 20,
            Primitives = new List<SymbolPrimitive>
            {
                new()
                {
                    Type = SymbolPrimitiveType.Rectangle,
                    Points = { new Point(-12, -8) },
                    Width = 24,
                    Height = 16
                },
                // Diagonal corner marks
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-12, -8), new Point(-8, -4) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(12, -8), new Point(8, -4) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-12, 8), new Point(-8, 4) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(12, 8), new Point(8, 4) } }
            }
        });

        // 17. Transformer - Two overlapping circles
        Register(new SymbolDefinition
        {
            Name = "Transformer",
            Category = "Distribution",
            Description = "Power transformer",
            Width = 24,
            Height = 20,
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(-3, 0) }, Radius = 7 },
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(3, 0) }, Radius = 7 }
            }
        });

        // 18. Disconnect Switch - Rectangle with "DISC" text
        Register(new SymbolDefinition
        {
            Name = "Disconnect Switch",
            Category = "Distribution",
            Description = "Safety disconnect switch",
            Width = 28,
            Height = 16,
            Primitives = new List<SymbolPrimitive>
            {
                new()
                {
                    Type = SymbolPrimitiveType.Rectangle,
                    Points = { new Point(-12, -6) },
                    Width = 24,
                    Height = 12
                },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "DISC" }
            }
        });

        // 19. Motor - Circle with "M" text
        Register(new SymbolDefinition
        {
            Name = "Motor",
            Category = "Distribution",
            Description = "Electric motor",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "M" }
            }
        });
    }

    // ───────────────────────────────────────────────────────────
    //  Fire Alarm
    // ───────────────────────────────────────────────────────────

    private void RegisterFireAlarm()
    {
        // 20. Smoke Detector - Circle with "SD" text and crosshair lines
        Register(new SymbolDefinition
        {
            Name = "Smoke Detector",
            Category = "Fire Alarm",
            Description = "Smoke detector / photoelectric sensor",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "SD" },
                // Crosshair extending beyond the circle
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(0, -10), new Point(0, 10) } },
                new() { Type = SymbolPrimitiveType.Line, Points = { new Point(-10, 0), new Point(10, 0) } }
            }
        });

        // 21. Pull Station - Square rotated 45 degrees (diamond) with center dot
        Register(new SymbolDefinition
        {
            Name = "Pull Station",
            Category = "Fire Alarm",
            Description = "Fire alarm manual pull station",
            Primitives = new List<SymbolPrimitive>
            {
                // Diamond shape (square rotated 45 degrees)
                new()
                {
                    Type = SymbolPrimitiveType.Polyline,
                    Points =
                    {
                        new Point(0, -8),
                        new Point(8, 0),
                        new Point(0, 8),
                        new Point(-8, 0),
                        new Point(0, -8)
                    }
                },
                // Center dot
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 1.5, IsFilled = true }
            }
        });

        // 22. Horn/Strobe - Circle with "HS" text
        Register(new SymbolDefinition
        {
            Name = "Horn/Strobe",
            Category = "Fire Alarm",
            Description = "Fire alarm horn/strobe notification appliance",
            Primitives = new List<SymbolPrimitive>
            {
                new() { Type = SymbolPrimitiveType.Circle, Points = { new Point(0, 0) }, Radius = 8 },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 0) }, Text = "HS" }
            }
        });
    }

    // ───────────────────────────────────────────────────────────
    //  Data / Communication
    // ───────────────────────────────────────────────────────────

    private void RegisterData()
    {
        // 23. Data Outlet - Triangle pointing up
        Register(new SymbolDefinition
        {
            Name = "Data Outlet",
            Category = "Data",
            Description = "Data / network outlet (RJ-45)",
            Primitives = new List<SymbolPrimitive>
            {
                new()
                {
                    Type = SymbolPrimitiveType.Polyline,
                    Points =
                    {
                        new Point(0, -8),
                        new Point(8, 6),
                        new Point(-8, 6),
                        new Point(0, -8)
                    }
                }
            }
        });

        // 24. Phone Outlet - Triangle with "T" text
        Register(new SymbolDefinition
        {
            Name = "Phone Outlet",
            Category = "Data",
            Description = "Telephone outlet (RJ-11)",
            Primitives = new List<SymbolPrimitive>
            {
                new()
                {
                    Type = SymbolPrimitiveType.Polyline,
                    Points =
                    {
                        new Point(0, -8),
                        new Point(8, 6),
                        new Point(-8, 6),
                        new Point(0, -8)
                    }
                },
                new() { Type = SymbolPrimitiveType.Text, Points = { new Point(0, 1) }, Text = "T" }
            }
        });
    }
}
