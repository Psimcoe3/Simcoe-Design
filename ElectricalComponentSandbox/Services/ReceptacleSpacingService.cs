namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Validates receptacle placement against NEC 210.52 wall-spacing rules.
///
/// NEC 210.52(A) — Dwelling general spaces: No point along a wall line
/// may be more than 6 ft from a receptacle outlet; any wall space 2 ft
/// or wider must have a receptacle.
///
/// NEC 210.52(C) — Kitchen / dining: countertop receptacles every 4 ft
/// max, each countertop space 12 in or wider must have an outlet.
///
/// NEC 210.52(D) — Bathrooms: at least one receptacle within 3 ft of
/// each basin.
///
/// NEC 210.52(E) — Outdoor: one front, one back.
///
/// NEC 210.52(G) — Garages: at least one receptacle.
///
/// This service takes wall segments and receptacle positions, then
/// reports NEC violations and suggests fix locations.
/// </summary>
public static class ReceptacleSpacingService
{
    /// <summary>Identifies the room type for NEC rule selection.</summary>
    public enum RoomType
    {
        General,
        Kitchen,
        Bathroom,
        Garage,
        Outdoor,
        Laundry,
        Hallway,
    }

    /// <summary>A wall segment defined by start/end positions in feet.</summary>
    public record WallSegment
    {
        public string Id { get; init; } = "";
        public double StartX { get; init; }
        public double StartY { get; init; }
        public double EndX { get; init; }
        public double EndY { get; init; }
        public bool IsDoorway { get; init; }
        public bool IsCountertop { get; init; }

        public double Length => Math.Sqrt(
            Math.Pow(EndX - StartX, 2) + Math.Pow(EndY - StartY, 2));
    }

    /// <summary>A placed receptacle position in feet.</summary>
    public record ReceptaclePosition
    {
        public string Id { get; init; } = "";
        public double X { get; init; }
        public double Y { get; init; }
        public string WallSegmentId { get; init; } = "";
        public bool IsGFCI { get; init; }
    }

    /// <summary>A spacing violation.</summary>
    public record SpacingViolation
    {
        public string WallSegmentId { get; init; } = "";
        public string Rule { get; init; } = "";
        public string Description { get; init; } = "";
        public double? SuggestedX { get; init; }
        public double? SuggestedY { get; init; }
    }

    /// <summary>Result of compliance check.</summary>
    public record SpacingResult
    {
        public bool IsCompliant { get; init; }
        public List<SpacingViolation> Violations { get; init; } = new();
        public int TotalReceptacles { get; init; }
        public int MinimumRequired { get; init; }
    }

    /// <summary>
    /// Checks receptacle spacing compliance for a room.
    /// </summary>
    public static SpacingResult CheckCompliance(
        RoomType roomType,
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<ReceptaclePosition> receptacles)
    {
        var violations = new List<SpacingViolation>();
        int minRequired = 0;

        switch (roomType)
        {
            case RoomType.General:
            case RoomType.Hallway:
                CheckGeneralSpacing(walls, receptacles, violations, ref minRequired);
                break;
            case RoomType.Kitchen:
                CheckKitchenSpacing(walls, receptacles, violations, ref minRequired);
                break;
            case RoomType.Bathroom:
                CheckBathroomSpacing(walls, receptacles, violations, ref minRequired);
                break;
            case RoomType.Garage:
                CheckGarageSpacing(walls, receptacles, violations, ref minRequired);
                break;
            case RoomType.Outdoor:
                CheckOutdoorSpacing(walls, receptacles, violations, ref minRequired);
                break;
            case RoomType.Laundry:
                CheckLaundrySpacing(walls, receptacles, violations, ref minRequired);
                break;
        }

        return new SpacingResult
        {
            IsCompliant = violations.Count == 0,
            Violations = violations,
            TotalReceptacles = receptacles.Count,
            MinimumRequired = minRequired,
        };
    }

    /// <summary>
    /// Calculates the minimum number of receptacles required for a room
    /// given wall lengths, per NEC 210.52(A).
    /// </summary>
    public static int CalculateMinimumReceptacles(RoomType roomType, IReadOnlyList<WallSegment> walls)
    {
        double totalUsableWall = walls.Where(w => !w.IsDoorway).Sum(w => w.Length);

        return roomType switch
        {
            RoomType.General or RoomType.Hallway =>
                Math.Max(1, (int)Math.Ceiling(totalUsableWall / 12.0)), // 6 ft rule → every 12 ft of wall needs a receptacle
            RoomType.Kitchen =>
                Math.Max(2, (int)Math.Ceiling(
                    walls.Where(w => w.IsCountertop).Sum(w => w.Length) / 4.0)), // 4 ft countertop spacing
            RoomType.Bathroom => 1,
            RoomType.Garage => 1,
            RoomType.Outdoor => 1,
            RoomType.Laundry => 1,
            _ => 1,
        };
    }

    // ── Internals ────────────────────────────────────────────────────────────

    /// <summary>NEC 210.52(A): 6 ft rule, 2 ft wall minimum.</summary>
    private static void CheckGeneralSpacing(
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<ReceptaclePosition> receptacles,
        List<SpacingViolation> violations,
        ref int minRequired)
    {
        foreach (var wall in walls)
        {
            if (wall.IsDoorway) continue;

            if (wall.Length >= 2.0)
            {
                minRequired++;

                // Find receptacles on this wall
                var wallReceps = receptacles.Where(r => r.WallSegmentId == wall.Id).ToList();

                if (wallReceps.Count == 0)
                {
                    double midX = (wall.StartX + wall.EndX) / 2;
                    double midY = (wall.StartY + wall.EndY) / 2;
                    violations.Add(new SpacingViolation
                    {
                        WallSegmentId = wall.Id,
                        Rule = "NEC 210.52(A)(1)",
                        Description = $"Wall {wall.Id} is {wall.Length:F1} ft with no receptacle (min wall length for receptacle: 2 ft)",
                        SuggestedX = midX,
                        SuggestedY = midY,
                    });
                }

                // Check 6 ft max distance: no point on wall can be >6ft from a receptacle
                if (wall.Length > 12.0 && wallReceps.Count < 2)
                {
                    violations.Add(new SpacingViolation
                    {
                        WallSegmentId = wall.Id,
                        Rule = "NEC 210.52(A)(1)",
                        Description = $"Wall {wall.Id} is {wall.Length:F1} ft — needs {(int)Math.Ceiling(wall.Length / 12.0)} receptacles (6 ft rule)",
                    });
                }
            }
        }
    }

    /// <summary>NEC 210.52(C): Countertop 4 ft / 2 ft rules for kitchens.</summary>
    private static void CheckKitchenSpacing(
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<ReceptaclePosition> receptacles,
        List<SpacingViolation> violations,
        ref int minRequired)
    {
        // General wall spacing still applies
        CheckGeneralSpacing(walls, receptacles, violations, ref minRequired);

        // Additional countertop rules
        var countertopWalls = walls.Where(w => w.IsCountertop && !w.IsDoorway).ToList();
        foreach (var ct in countertopWalls)
        {
            if (ct.Length >= 1.0) // 12 inches minimum
            {
                var ctReceps = receptacles.Where(r => r.WallSegmentId == ct.Id).ToList();
                if (ctReceps.Count == 0)
                {
                    violations.Add(new SpacingViolation
                    {
                        WallSegmentId = ct.Id,
                        Rule = "NEC 210.52(C)(1)",
                        Description = $"Countertop wall {ct.Id} ({ct.Length:F1} ft) requires at least one receptacle",
                        SuggestedX = (ct.StartX + ct.EndX) / 2,
                        SuggestedY = (ct.StartY + ct.EndY) / 2,
                    });
                }

                // 4 ft spacing on countertops
                if (ct.Length > 4.0)
                {
                    int needed = (int)Math.Ceiling(ct.Length / 4.0);
                    if (ctReceps.Count < needed)
                    {
                        violations.Add(new SpacingViolation
                        {
                            WallSegmentId = ct.Id,
                            Rule = "NEC 210.52(C)(1)",
                            Description = $"Countertop {ct.Id} ({ct.Length:F1} ft) needs {needed} receptacles (4 ft rule), has {ctReceps.Count}",
                        });
                    }
                }
            }
        }

        // Kitchen needs minimum 2 small-appliance circuits
        minRequired = Math.Max(minRequired, 2);
    }

    /// <summary>NEC 210.52(D): Basin within 3 ft.</summary>
    private static void CheckBathroomSpacing(
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<ReceptaclePosition> receptacles,
        List<SpacingViolation> violations,
        ref int minRequired)
    {
        minRequired = Math.Max(1, minRequired);

        if (receptacles.Count == 0)
        {
            violations.Add(new SpacingViolation
            {
                Rule = "NEC 210.52(D)",
                Description = "Bathroom requires at least one GFCI receptacle within 3 ft of basin",
            });
        }

        // Check GFCI requirement
        if (receptacles.Count > 0 && !receptacles.Any(r => r.IsGFCI))
        {
            violations.Add(new SpacingViolation
            {
                Rule = "NEC 210.11(C)(3)",
                Description = "Bathroom receptacles must be GFCI protected",
            });
        }
    }

    /// <summary>NEC 210.52(G): At least one receptacle in garage.</summary>
    private static void CheckGarageSpacing(
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<ReceptaclePosition> receptacles,
        List<SpacingViolation> violations,
        ref int minRequired)
    {
        minRequired = 1;

        if (receptacles.Count == 0)
        {
            violations.Add(new SpacingViolation
            {
                Rule = "NEC 210.52(G)(1)",
                Description = "Garage requires at least one receptacle",
            });
        }
    }

    /// <summary>NEC 210.52(E): Front and back outdoor receptacles.</summary>
    private static void CheckOutdoorSpacing(
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<ReceptaclePosition> receptacles,
        List<SpacingViolation> violations,
        ref int minRequired)
    {
        minRequired = 1;

        if (receptacles.Count == 0)
        {
            violations.Add(new SpacingViolation
            {
                Rule = "NEC 210.52(E)",
                Description = "At least one outdoor receptacle required",
            });
        }
    }

    /// <summary>NEC 210.52(F): Laundry requires dedicated circuit receptacle.</summary>
    private static void CheckLaundrySpacing(
        IReadOnlyList<WallSegment> walls,
        IReadOnlyList<ReceptaclePosition> receptacles,
        List<SpacingViolation> violations,
        ref int minRequired)
    {
        minRequired = 1;

        if (receptacles.Count == 0)
        {
            violations.Add(new SpacingViolation
            {
                Rule = "NEC 210.52(F)",
                Description = "Laundry area requires at least one receptacle on a dedicated 20A circuit",
            });
        }
    }
}
