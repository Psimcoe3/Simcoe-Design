namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Energy and utility cost analysis service.
///
/// Models utility billing structures to estimate monthly/annual energy costs
/// and quantify savings from power factor correction, load management, and
/// demand reduction strategies.
///
/// Typical utility rate structures:
///   - Energy charge ($/kWh) — consumption
///   - Demand charge ($/kW) — peak demand in billing period
///   - Power factor penalty — surcharge when PF &lt; 0.90 (or 0.85)
///   - Time-of-use (TOU) — variable rates by hour
///   - Ratchet clause — demand floor from previous 11 months
///
/// This service also calculates simple payback period for capital improvements.
/// </summary>
public static class EnergyUtilityCostService
{
    /// <summary>
    /// Utility rate schedule.
    /// </summary>
    public record UtilityRate
    {
        /// <summary>Energy charge in $/kWh.</summary>
        public double EnergyChargePerKWh { get; init; } = 0.12;

        /// <summary>Demand charge in $/kW per month.</summary>
        public double DemandChargePerKW { get; init; } = 12.0;

        /// <summary>PF threshold below which a penalty applies.</summary>
        public double PowerFactorThreshold { get; init; } = 0.90;

        /// <summary>
        /// PF penalty: ratio billing. Billed demand = actual demand × (threshold / actual PF).
        /// </summary>
        public bool UsesPowerFactorPenalty { get; init; } = true;

        /// <summary>Monthly fixed/customer charge.</summary>
        public double MonthlyFixedCharge { get; init; } = 25.0;

        /// <summary>Sales tax / regulatory surcharge percentage.</summary>
        public double TaxAndSurchargePercent { get; init; } = 5.0;
    }

    /// <summary>
    /// Monthly load profile input.
    /// </summary>
    public record MonthlyLoadProfile
    {
        public string Month { get; init; } = "";
        public double AverageLoadKW { get; init; }
        public double PeakDemandKW { get; init; }
        public double OperatingHours { get; init; } = 720; // ~30 days × 24h
        public double PowerFactor { get; init; } = 0.85;
    }

    /// <summary>
    /// Monthly utility bill result.
    /// </summary>
    public record MonthlyBill
    {
        public string Month { get; init; } = "";
        public double EnergyConsumptionKWh { get; init; }
        public double EnergyCharge { get; init; }
        public double BilledDemandKW { get; init; }
        public double DemandCharge { get; init; }
        public double PowerFactorPenaltyKW { get; init; }
        public double FixedCharge { get; init; }
        public double Subtotal { get; init; }
        public double TaxAndSurcharge { get; init; }
        public double TotalBill { get; init; }
    }

    /// <summary>
    /// Annual utility cost analysis result.
    /// </summary>
    public record AnnualCostAnalysis
    {
        public List<MonthlyBill> MonthlyBills { get; init; } = new();
        public double TotalEnergyKWh { get; init; }
        public double TotalEnergyCost { get; init; }
        public double TotalDemandCost { get; init; }
        public double TotalPowerFactorPenalty { get; init; }
        public double TotalFixedCharges { get; init; }
        public double TotalTaxAndSurcharge { get; init; }
        public double AnnualCost { get; init; }
        public double AverageCostPerKWh { get; init; }
    }

    /// <summary>
    /// Savings from a proposed improvement.
    /// </summary>
    public record SavingsAnalysis
    {
        public double AnnualCostBefore { get; init; }
        public double AnnualCostAfter { get; init; }
        public double AnnualSavings { get; init; }
        public double ImplementationCost { get; init; }
        public double SimplePaybackYears { get; init; }
        public double ReturnOnInvestmentPercent { get; init; }
    }

    /// <summary>
    /// Calculates a single monthly utility bill.
    /// </summary>
    public static MonthlyBill CalculateMonthlyBill(MonthlyLoadProfile profile, UtilityRate rate)
    {
        double energyKWh = profile.AverageLoadKW * profile.OperatingHours;
        double energyCharge = energyKWh * rate.EnergyChargePerKWh;

        double billedDemandKW = profile.PeakDemandKW;
        double pfPenaltyKW = 0;

        if (rate.UsesPowerFactorPenalty && profile.PowerFactor < rate.PowerFactorThreshold && profile.PowerFactor > 0)
        {
            billedDemandKW = profile.PeakDemandKW * (rate.PowerFactorThreshold / profile.PowerFactor);
            pfPenaltyKW = billedDemandKW - profile.PeakDemandKW;
        }

        double demandCharge = billedDemandKW * rate.DemandChargePerKW;
        double fixedCharge = rate.MonthlyFixedCharge;
        double subtotal = energyCharge + demandCharge + fixedCharge;
        double tax = subtotal * (rate.TaxAndSurchargePercent / 100.0);

        return new MonthlyBill
        {
            Month = profile.Month,
            EnergyConsumptionKWh = Math.Round(energyKWh, 1),
            EnergyCharge = Math.Round(energyCharge, 2),
            BilledDemandKW = Math.Round(billedDemandKW, 2),
            DemandCharge = Math.Round(demandCharge, 2),
            PowerFactorPenaltyKW = Math.Round(pfPenaltyKW, 2),
            FixedCharge = Math.Round(fixedCharge, 2),
            Subtotal = Math.Round(subtotal, 2),
            TaxAndSurcharge = Math.Round(tax, 2),
            TotalBill = Math.Round(subtotal + tax, 2),
        };
    }

    /// <summary>
    /// Calculates annual utility costs from monthly load profiles.
    /// </summary>
    public static AnnualCostAnalysis CalculateAnnualCost(
        IReadOnlyList<MonthlyLoadProfile> profiles, UtilityRate rate)
    {
        var bills = profiles.Select(p => CalculateMonthlyBill(p, rate)).ToList();

        double totalEnergy = bills.Sum(b => b.EnergyConsumptionKWh);
        double totalEnergyCost = bills.Sum(b => b.EnergyCharge);
        double totalDemandCost = bills.Sum(b => b.DemandCharge);
        double totalPFPenalty = bills.Sum(b => b.PowerFactorPenaltyKW * rate.DemandChargePerKW);
        double totalFixed = bills.Sum(b => b.FixedCharge);
        double totalTax = bills.Sum(b => b.TaxAndSurcharge);
        double annualCost = bills.Sum(b => b.TotalBill);

        return new AnnualCostAnalysis
        {
            MonthlyBills = bills,
            TotalEnergyKWh = Math.Round(totalEnergy, 1),
            TotalEnergyCost = Math.Round(totalEnergyCost, 2),
            TotalDemandCost = Math.Round(totalDemandCost, 2),
            TotalPowerFactorPenalty = Math.Round(totalPFPenalty, 2),
            TotalFixedCharges = Math.Round(totalFixed, 2),
            TotalTaxAndSurcharge = Math.Round(totalTax, 2),
            AnnualCost = Math.Round(annualCost, 2),
            AverageCostPerKWh = totalEnergy > 0 ? Math.Round(annualCost / totalEnergy, 4) : 0,
        };
    }

    /// <summary>
    /// Computes savings and payback from a proposed improvement.
    /// </summary>
    public static SavingsAnalysis CalculateSavings(
        double annualCostBefore, double annualCostAfter, double implementationCost)
    {
        double savings = annualCostBefore - annualCostAfter;
        double payback = savings > 0 ? implementationCost / savings : double.PositiveInfinity;
        double roi = implementationCost > 0 ? (savings / implementationCost) * 100 : 0;

        return new SavingsAnalysis
        {
            AnnualCostBefore = Math.Round(annualCostBefore, 2),
            AnnualCostAfter = Math.Round(annualCostAfter, 2),
            AnnualSavings = Math.Round(savings, 2),
            ImplementationCost = Math.Round(implementationCost, 2),
            SimplePaybackYears = Math.Round(payback, 2),
            ReturnOnInvestmentPercent = Math.Round(roi, 1),
        };
    }

    /// <summary>
    /// Convenience: generate 12 uniform monthly profiles for quick analysis.
    /// </summary>
    public static List<MonthlyLoadProfile> GenerateUniformProfiles(
        double averageLoadKW, double peakDemandKW, double powerFactor,
        double monthlyHours = 720)
    {
        var months = new[] { "Jan", "Feb", "Mar", "Apr", "May", "Jun",
                             "Jul", "Aug", "Sep", "Oct", "Nov", "Dec" };
        return months.Select(m => new MonthlyLoadProfile
        {
            Month = m,
            AverageLoadKW = averageLoadKW,
            PeakDemandKW = peakDemandKW,
            OperatingHours = monthlyHours,
            PowerFactor = powerFactor,
        }).ToList();
    }
}
