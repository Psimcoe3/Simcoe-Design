namespace ElectricalComponentSandbox.Services;

/// <summary>
/// IEEE 519-2022 harmonic analysis — calculates Total Harmonic Distortion (THD),
/// individual harmonic content, K-factor for transformer derating, and neutral
/// current estimation for three-phase systems with nonlinear loads.
///
/// Key IEEE 519 limits (Table 2):
/// - Voltage THD ≤ 5% for general systems (≤ 69 kV)
/// - Individual harmonic voltage ≤ 3%
/// - Current TDD limits based on ISC/IL ratio
///
/// K-factor is used per IEEE C57.110 for transformer derating.
/// </summary>
public static class HarmonicAnalysisService
{
    /// <summary>
    /// Represents harmonic content of a load or bus.
    /// Harmonics are stored as (harmonic order → magnitude as percent of fundamental).
    /// </summary>
    public record HarmonicSpectrum
    {
        public string Id { get; init; } = "";
        public string Description { get; init; } = "";
        public double FundamentalAmps { get; init; }

        /// <summary>Harmonic order → magnitude as % of fundamental (e.g., 3→80 means 3rd harmonic is 80% of fundamental).</summary>
        public Dictionary<int, double> HarmonicPercents { get; init; } = new();
    }

    /// <summary>
    /// Result of harmonic analysis at a bus or point.
    /// </summary>
    public record HarmonicAnalysisResult
    {
        public double THDPercent { get; init; }
        public double KFactor { get; init; }
        public double RMSCurrentAmps { get; init; }
        public double NeutralCurrentAmps { get; init; }
        public double NeutralToPhaseRatio { get; init; }
        public double WorstIndividualHarmonicPercent { get; init; }
        public int WorstHarmonicOrder { get; init; }
        public bool ExceedsTHDLimit { get; init; }
        public bool ExceedsIndividualLimit { get; init; }
        public List<string> Violations { get; init; } = new();
    }

    /// <summary>IEEE 519 Table 2: voltage THD limit for general systems ≤ 69 kV.</summary>
    public const double VoltageTHDLimit = 5.0;

    /// <summary>IEEE 519 Table 2: individual harmonic voltage limit.</summary>
    public const double IndividualHarmonicLimit = 3.0;

    /// <summary>
    /// Typical harmonic spectrum for common nonlinear load types.
    /// Values are percent of fundamental current.
    /// </summary>
    public static Dictionary<int, double> GetTypicalSpectrum(NonlinearLoadType loadType)
    {
        return loadType switch
        {
            NonlinearLoadType.SixPulseVFD => new Dictionary<int, double>
            {
                [5] = 20.0, [7] = 14.3, [11] = 9.1, [13] = 7.7,
                [17] = 5.9, [19] = 5.3, [23] = 4.3, [25] = 4.0,
            },
            NonlinearLoadType.TwelvePulseVFD => new Dictionary<int, double>
            {
                [11] = 9.1, [13] = 7.7, [23] = 4.3, [25] = 4.0,
            },
            NonlinearLoadType.SinglePhaseComputer => new Dictionary<int, double>
            {
                [3] = 80.0, [5] = 60.0, [7] = 40.0, [9] = 20.0,
                [11] = 10.0, [13] = 5.0,
            },
            NonlinearLoadType.LED_Lighting => new Dictionary<int, double>
            {
                [3] = 70.0, [5] = 45.0, [7] = 25.0, [9] = 10.0,
                [11] = 5.0,
            },
            NonlinearLoadType.UPS => new Dictionary<int, double>
            {
                [5] = 30.0, [7] = 12.0, [11] = 8.0, [13] = 5.0,
            },
            NonlinearLoadType.Welder => new Dictionary<int, double>
            {
                [3] = 15.0, [5] = 8.0, [7] = 4.0,
            },
            _ => new Dictionary<int, double>(),
        };
    }

    /// <summary>
    /// Calculates Total Harmonic Distortion (THD) as percent of fundamental.
    /// THD = √(Σ(Ih²)) / I1 × 100
    /// </summary>
    public static double CalculateTHD(Dictionary<int, double> harmonicPercents)
    {
        if (harmonicPercents.Count == 0) return 0;
        double sumSquares = harmonicPercents.Values.Sum(v => v * v);
        return Math.Sqrt(sumSquares);
    }

    /// <summary>
    /// Calculates K-factor for transformer derating per IEEE C57.110.
    /// K = Σ(Ih² × h²) / Σ(Ih²)
    /// where Ih is harmonic current (per unit of fundamental) and h is harmonic order.
    /// </summary>
    public static double CalculateKFactor(Dictionary<int, double> harmonicPercents)
    {
        // Include fundamental (h=1, 100%)
        double numerator = 1.0 * 1.0 * 1; // fundamental: (1.0)^2 × 1^2
        double denominator = 1.0; // fundamental: (1.0)^2

        foreach (var (order, percent) in harmonicPercents)
        {
            double pu = percent / 100.0;
            numerator += pu * pu * order * order;
            denominator += pu * pu;
        }

        return denominator > 0 ? numerator / denominator : 1.0;
    }

    /// <summary>
    /// Calculates RMS current including harmonics.
    /// Irms = I1 × √(1 + Σ(Ih/I1)²)
    /// </summary>
    public static double CalculateRMSCurrent(double fundamentalAmps, Dictionary<int, double> harmonicPercents)
    {
        double sumSquares = 1.0; // fundamental
        foreach (var percent in harmonicPercents.Values)
        {
            double pu = percent / 100.0;
            sumSquares += pu * pu;
        }
        return fundamentalAmps * Math.Sqrt(sumSquares);
    }

    /// <summary>
    /// Estimates neutral current in a three-phase 4-wire system.
    /// Triplen harmonics (3rd, 9th, 15th, 21st...) add arithmetically in the neutral.
    /// Non-triplen harmonics cancel in balanced three-phase systems.
    /// I_neutral = 3 × √(Σ(I_triplen²))
    /// </summary>
    public static double CalculateNeutralCurrent(double fundamentalAmps, Dictionary<int, double> harmonicPercents)
    {
        double triplenSumSquares = 0;
        foreach (var (order, percent) in harmonicPercents)
        {
            if (order % 3 == 0)
            {
                double amps = fundamentalAmps * percent / 100.0;
                triplenSumSquares += amps * amps;
            }
        }
        return 3.0 * Math.Sqrt(triplenSumSquares);
    }

    /// <summary>
    /// Full harmonic analysis for a load or bus point.
    /// </summary>
    public static HarmonicAnalysisResult Analyze(HarmonicSpectrum spectrum)
    {
        var hp = spectrum.HarmonicPercents;
        double thd = CalculateTHD(hp);
        double kFactor = CalculateKFactor(hp);
        double rms = CalculateRMSCurrent(spectrum.FundamentalAmps, hp);
        double neutral = CalculateNeutralCurrent(spectrum.FundamentalAmps, hp);

        int worstOrder = 0;
        double worstPercent = 0;
        foreach (var (order, percent) in hp)
        {
            if (percent > worstPercent)
            {
                worstOrder = order;
                worstPercent = percent;
            }
        }

        bool exceedsTHD = thd > VoltageTHDLimit;
        bool exceedsIndividual = worstPercent > IndividualHarmonicLimit;

        var violations = new List<string>();
        if (exceedsTHD)
            violations.Add($"IEEE 519: THD {thd:F1}% exceeds {VoltageTHDLimit}% limit");
        foreach (var (order, percent) in hp)
        {
            if (percent > IndividualHarmonicLimit)
                violations.Add($"IEEE 519: Harmonic h{order} at {percent:F1}% exceeds {IndividualHarmonicLimit}% individual limit");
        }

        double neutralRatio = spectrum.FundamentalAmps > 0
            ? neutral / spectrum.FundamentalAmps
            : 0;

        return new HarmonicAnalysisResult
        {
            THDPercent = Math.Round(thd, 2),
            KFactor = Math.Round(kFactor, 2),
            RMSCurrentAmps = Math.Round(rms, 1),
            NeutralCurrentAmps = Math.Round(neutral, 1),
            NeutralToPhaseRatio = Math.Round(neutralRatio, 3),
            WorstIndividualHarmonicPercent = Math.Round(worstPercent, 1),
            WorstHarmonicOrder = worstOrder,
            ExceedsTHDLimit = exceedsTHD,
            ExceedsIndividualLimit = exceedsIndividual,
            Violations = violations,
        };
    }

    /// <summary>
    /// Combines harmonic spectra from multiple loads at a common bus.
    /// Harmonic currents add (assuming worst-case same-phase coincidence).
    /// </summary>
    public static HarmonicSpectrum CombineAtBus(IEnumerable<HarmonicSpectrum> loads)
    {
        double totalFundamental = 0;
        var combinedHarmonics = new Dictionary<int, double>(); // order → amps

        foreach (var load in loads)
        {
            totalFundamental += load.FundamentalAmps;
            foreach (var (order, percent) in load.HarmonicPercents)
            {
                double amps = load.FundamentalAmps * percent / 100.0;
                if (combinedHarmonics.ContainsKey(order))
                    combinedHarmonics[order] += amps;
                else
                    combinedHarmonics[order] = amps;
            }
        }

        // Convert back to percent of combined fundamental
        var percentages = new Dictionary<int, double>();
        if (totalFundamental > 0)
        {
            foreach (var (order, amps) in combinedHarmonics)
            {
                percentages[order] = Math.Round(amps / totalFundamental * 100.0, 1);
            }
        }

        return new HarmonicSpectrum
        {
            Id = "combined",
            Description = "Combined bus harmonic spectrum",
            FundamentalAmps = totalFundamental,
            HarmonicPercents = percentages,
        };
    }
}

public enum NonlinearLoadType
{
    SixPulseVFD,
    TwelvePulseVFD,
    SinglePhaseComputer,
    LED_Lighting,
    UPS,
    Welder,
}
