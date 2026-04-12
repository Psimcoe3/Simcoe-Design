namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Demand response and automatic load shedding service.
/// Models utility demand response programs and generator-limited load management:
///
/// - Peak demand tracking and threshold monitoring
/// - Priority-based load shedding schedules (NEC 700/701/702 aware)
/// - Demand limiting to stay below utility contract levels
/// - Generator load management when capacity is limited
/// - Automatic transfer and retransfer sequencing
///
/// Load priorities follow NEC emergency hierarchy:
/// Priority 1: Life safety (NEC 700) — never shed
/// Priority 2: Legally required standby (NEC 701) — shed only in extreme emergency
/// Priority 3: Critical optional standby — shed before non-critical
/// Priority 4: Non-critical optional — first to shed, last to restore
/// </summary>
public static class LoadSheddingService
{
    /// <summary>Load shedding priority level — lower number = higher priority (last to shed, first to restore).</summary>
    public enum SheddingPriority
    {
        /// <summary>NEC 700 life safety — NEVER shed.</summary>
        LifeSafety = 1,

        /// <summary>NEC 701 legally required standby — shed only in extreme cases.</summary>
        LegallyRequired = 2,

        /// <summary>Critical production/data loads — shed before non-critical.</summary>
        CriticalOptional = 3,

        /// <summary>Comfort/convenience loads — first to shed.</summary>
        NonCritical = 4,
    }

    /// <summary>
    /// A load block that can be shed or restored as a unit.
    /// </summary>
    public record LoadBlock
    {
        public string Id { get; init; } = "";
        public string Description { get; init; } = "";
        public SheddingPriority Priority { get; init; } = SheddingPriority.NonCritical;
        public double DemandKW { get; init; }
        public double DemandKVA { get; init; }
        public bool IsShed { get; init; }

        /// <summary>Minimum time (seconds) this load must remain off after being shed before restore.</summary>
        public int MinOffTimeSec { get; init; } = 300;

        /// <summary>Delay (seconds) before shedding after command.</summary>
        public int ShedDelaySec { get; init; } = 0;
    }

    /// <summary>
    /// Result of a load shedding analysis.
    /// </summary>
    public record LoadSheddingPlan
    {
        public double CurrentDemandKW { get; init; }
        public double TargetMaxKW { get; init; }
        public double OverloadKW { get; init; }
        public List<LoadBlock> BlocksToShed { get; init; } = new();
        public double ShedTotalKW { get; init; }
        public double RemainingDemandKW { get; init; }
        public bool IsAdequate { get; init; }
        public string? Issue { get; init; }
    }

    /// <summary>
    /// Demand tracking snapshot.
    /// </summary>
    public record DemandSnapshot
    {
        public double PeakDemandKW { get; init; }
        public double AverageDemandKW { get; init; }
        public double CurrentDemandKW { get; init; }
        public double ThresholdKW { get; init; }
        public double UtilizationPercent { get; init; }
        public bool ExceedsThreshold { get; init; }
        public double MarginKW { get; init; }
    }

    /// <summary>
    /// Creates a demand tracking snapshot from load data.
    /// </summary>
    public static DemandSnapshot TrackDemand(
        IEnumerable<LoadBlock> loads,
        double thresholdKW)
    {
        var loadList = loads.Where(l => !l.IsShed).ToList();
        double current = loadList.Sum(l => l.DemandKW);
        double peak = current; // In a real system, tracked over time
        double avg = current;

        return new DemandSnapshot
        {
            PeakDemandKW = Math.Round(peak, 1),
            AverageDemandKW = Math.Round(avg, 1),
            CurrentDemandKW = Math.Round(current, 1),
            ThresholdKW = thresholdKW,
            UtilizationPercent = thresholdKW > 0 ? Math.Round(current / thresholdKW * 100, 1) : 0,
            ExceedsThreshold = current > thresholdKW,
            MarginKW = Math.Round(thresholdKW - current, 1),
        };
    }

    /// <summary>
    /// Generates a load shedding plan to bring demand below the target.
    /// Sheds lowest-priority loads first; never sheds LifeSafety loads.
    /// </summary>
    public static LoadSheddingPlan CreateSheddingPlan(
        IEnumerable<LoadBlock> loads,
        double targetMaxKW)
    {
        var loadList = loads.ToList();
        double currentDemand = loadList.Where(l => !l.IsShed).Sum(l => l.DemandKW);
        double overload = currentDemand - targetMaxKW;

        if (overload <= 0)
        {
            return new LoadSheddingPlan
            {
                CurrentDemandKW = Math.Round(currentDemand, 1),
                TargetMaxKW = targetMaxKW,
                OverloadKW = 0,
                IsAdequate = true,
                RemainingDemandKW = Math.Round(currentDemand, 1),
            };
        }

        // Sort by priority descending (shed lowest priority first), then by demand (shed largest first)
        var shedCandidates = loadList
            .Where(l => !l.IsShed && l.Priority != SheddingPriority.LifeSafety)
            .OrderByDescending(l => (int)l.Priority)
            .ThenByDescending(l => l.DemandKW)
            .ToList();

        var toShed = new List<LoadBlock>();
        double shedTotal = 0;

        foreach (var block in shedCandidates)
        {
            if (shedTotal >= overload) break;
            toShed.Add(block);
            shedTotal += block.DemandKW;
        }

        double remaining = currentDemand - shedTotal;
        bool adequate = remaining <= targetMaxKW;

        return new LoadSheddingPlan
        {
            CurrentDemandKW = Math.Round(currentDemand, 1),
            TargetMaxKW = targetMaxKW,
            OverloadKW = Math.Round(overload, 1),
            BlocksToShed = toShed,
            ShedTotalKW = Math.Round(shedTotal, 1),
            RemainingDemandKW = Math.Round(remaining, 1),
            IsAdequate = adequate,
            Issue = adequate ? null : $"Cannot reduce below {targetMaxKW:F1} kW — life safety and legally required loads total {remaining:F1} kW",
        };
    }

    /// <summary>
    /// Generates a restore sequence (reverse of shedding order — highest priority restored first).
    /// Includes staggered restoration to avoid inrush.
    /// </summary>
    public static List<(LoadBlock Block, int RestoreOrderIndex, int CumulativeDelaySec)> CreateRestoreSequence(
        IEnumerable<LoadBlock> shedBlocks,
        int staggerDelaySec = 10)
    {
        var ordered = shedBlocks
            .OrderBy(b => (int)b.Priority)
            .ThenByDescending(b => b.DemandKW)
            .ToList();

        var sequence = new List<(LoadBlock, int, int)>();
        int cumDelay = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            cumDelay = i * staggerDelaySec;
            int effectiveDelay = Math.Max(cumDelay, ordered[i].MinOffTimeSec);
            sequence.Add((ordered[i], i + 1, effectiveDelay));
        }

        return sequence;
    }

    /// <summary>
    /// Evaluates generator capacity against connected loads and recommends shedding if needed.
    /// </summary>
    public static LoadSheddingPlan EvaluateGeneratorCapacity(
        IEnumerable<LoadBlock> loads,
        double generatorKW)
    {
        return CreateSheddingPlan(loads, generatorKW);
    }
}
