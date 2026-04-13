using ElectricalComponentSandbox.Services;
using static ElectricalComponentSandbox.Services.EnergyUtilityCostService;

namespace ElectricalComponentSandbox.Tests.Services;

public class EnergyUtilityCostServiceTests
{
    private static readonly UtilityRate DefaultRate = new()
    {
        EnergyChargePerKWh = 0.12,
        DemandChargePerKW = 12.0,
        PowerFactorThreshold = 0.90,
        UsesPowerFactorPenalty = true,
        MonthlyFixedCharge = 25.0,
        TaxAndSurchargePercent = 5.0,
    };

    // ── Monthly Bill Calculations ────────────────────────────────────────────

    [Fact]
    public void MonthlyBill_BasicCalculation()
    {
        var profile = new MonthlyLoadProfile
        {
            Month = "Jan", AverageLoadKW = 100, PeakDemandKW = 150,
            OperatingHours = 720, PowerFactor = 0.95,
        };

        var bill = EnergyUtilityCostService.CalculateMonthlyBill(profile, DefaultRate);

        Assert.Equal(72000, bill.EnergyConsumptionKWh);
        Assert.Equal(8640, bill.EnergyCharge);      // 72000 × $0.12
        Assert.Equal(150, bill.BilledDemandKW);      // no PF penalty (0.95 > 0.90)
        Assert.Equal(1800, bill.DemandCharge);       // 150 × $12
        Assert.Equal(0, bill.PowerFactorPenaltyKW);
    }

    [Fact]
    public void MonthlyBill_LowPF_IncursPenalty()
    {
        var profile = new MonthlyLoadProfile
        {
            Month = "Feb", AverageLoadKW = 100, PeakDemandKW = 150,
            OperatingHours = 720, PowerFactor = 0.80,
        };

        var bill = EnergyUtilityCostService.CalculateMonthlyBill(profile, DefaultRate);

        // Billed demand = 150 × (0.90 / 0.80) = 168.75 kW
        Assert.Equal(168.75, bill.BilledDemandKW, 0.01);
        Assert.True(bill.PowerFactorPenaltyKW > 0);
        Assert.True(bill.DemandCharge > 150 * 12);
    }

    [Fact]
    public void MonthlyBill_GoodPF_NoPenalty()
    {
        var profile = new MonthlyLoadProfile
        {
            Month = "Mar", AverageLoadKW = 100, PeakDemandKW = 150,
            OperatingHours = 720, PowerFactor = 0.95,
        };

        var bill = EnergyUtilityCostService.CalculateMonthlyBill(profile, DefaultRate);
        Assert.Equal(0, bill.PowerFactorPenaltyKW);
        Assert.Equal(150, bill.BilledDemandKW);
    }

    [Fact]
    public void MonthlyBill_IncludesTax()
    {
        var profile = new MonthlyLoadProfile
        {
            Month = "Apr", AverageLoadKW = 50, PeakDemandKW = 75,
            OperatingHours = 720, PowerFactor = 0.95,
        };

        var bill = EnergyUtilityCostService.CalculateMonthlyBill(profile, DefaultRate);

        Assert.True(bill.TaxAndSurcharge > 0);
        Assert.Equal(bill.Subtotal + bill.TaxAndSurcharge, bill.TotalBill, 0.01);
    }

    [Fact]
    public void MonthlyBill_IncludesFixedCharge()
    {
        var profile = new MonthlyLoadProfile
        {
            Month = "May", AverageLoadKW = 10, PeakDemandKW = 15,
            OperatingHours = 720, PowerFactor = 0.95,
        };

        var bill = EnergyUtilityCostService.CalculateMonthlyBill(profile, DefaultRate);
        Assert.Equal(25, bill.FixedCharge);
    }

    // ── Annual Analysis ──────────────────────────────────────────────────────

    [Fact]
    public void AnnualCost_12Months_SumsCorrectly()
    {
        var profiles = EnergyUtilityCostService.GenerateUniformProfiles(100, 150, 0.92);
        var result = EnergyUtilityCostService.CalculateAnnualCost(profiles, DefaultRate);

        Assert.Equal(12, result.MonthlyBills.Count);
        Assert.True(result.TotalEnergyKWh > 800000); // 100 kW × 720h × 12
        Assert.True(result.AnnualCost > 0);
        Assert.True(result.AverageCostPerKWh > 0);
    }

    [Fact]
    public void AnnualCost_WithPFPenalty_HigherThanWithout()
    {
        var goodPF = EnergyUtilityCostService.GenerateUniformProfiles(100, 150, 0.95);
        var badPF = EnergyUtilityCostService.GenerateUniformProfiles(100, 150, 0.75);

        var goodResult = EnergyUtilityCostService.CalculateAnnualCost(goodPF, DefaultRate);
        var badResult = EnergyUtilityCostService.CalculateAnnualCost(badPF, DefaultRate);

        Assert.True(badResult.AnnualCost > goodResult.AnnualCost,
            "Low PF should result in higher annual cost");
    }

    [Fact]
    public void AnnualCost_AveragePerKWh_Reasonable()
    {
        var profiles = EnergyUtilityCostService.GenerateUniformProfiles(200, 250, 0.90);
        var result = EnergyUtilityCostService.CalculateAnnualCost(profiles, DefaultRate);

        // Average blended rate should be somewhat above the base energy rate
        Assert.True(result.AverageCostPerKWh > DefaultRate.EnergyChargePerKWh,
            "Blended rate should exceed base energy rate (includes demand + fixed)");
        Assert.True(result.AverageCostPerKWh < 0.50,
            "Blended rate should be reasonable (< $0.50/kWh)");
    }

    // ── Savings / Payback ────────────────────────────────────────────────────

    [Fact]
    public void Savings_PositiveWhenCostReduced()
    {
        var result = EnergyUtilityCostService.CalculateSavings(120000, 100000, 40000);

        Assert.Equal(20000, result.AnnualSavings);
        Assert.Equal(2.0, result.SimplePaybackYears);
        Assert.Equal(50, result.ReturnOnInvestmentPercent, 0.1);
    }

    [Fact]
    public void Savings_NegativeWhenCostIncreases()
    {
        var result = EnergyUtilityCostService.CalculateSavings(100000, 120000, 50000);

        Assert.True(result.AnnualSavings < 0);
    }

    [Fact]
    public void Savings_ZeroSavings_InfinitePayback()
    {
        var result = EnergyUtilityCostService.CalculateSavings(100000, 100000, 50000);

        Assert.Equal(0, result.AnnualSavings);
        Assert.True(double.IsPositiveInfinity(result.SimplePaybackYears));
    }

    [Fact]
    public void Savings_ZeroImplementationCost_ZeroPayback()
    {
        var result = EnergyUtilityCostService.CalculateSavings(100000, 80000, 0);

        Assert.Equal(0, result.SimplePaybackYears);
    }

    // ── PF Correction ROI ────────────────────────────────────────────────────

    [Fact]
    public void PFCorrection_ROI_Scenario()
    {
        // Before: plant at 0.78 PF
        var beforeProfiles = EnergyUtilityCostService.GenerateUniformProfiles(500, 650, 0.78);
        var before = EnergyUtilityCostService.CalculateAnnualCost(beforeProfiles, DefaultRate);

        // After: PF corrected to 0.96
        var afterProfiles = EnergyUtilityCostService.GenerateUniformProfiles(500, 650, 0.96);
        var after = EnergyUtilityCostService.CalculateAnnualCost(afterProfiles, DefaultRate);

        double capBankCost = 15000; // estimated capacitor bank installation
        var savings = EnergyUtilityCostService.CalculateSavings(before.AnnualCost, after.AnnualCost, capBankCost);

        Assert.True(savings.AnnualSavings > 0, "PF correction should save money");
        Assert.True(savings.SimplePaybackYears < 5, "Typical PF correction payback < 5 years");
        Assert.True(savings.ReturnOnInvestmentPercent > 10, "ROI should be meaningful");
    }

    // ── Uniform Profile Helper ───────────────────────────────────────────────

    [Fact]
    public void GenerateUniformProfiles_12Months()
    {
        var profiles = EnergyUtilityCostService.GenerateUniformProfiles(100, 150, 0.90, 730);

        Assert.Equal(12, profiles.Count);
        Assert.Equal("Jan", profiles[0].Month);
        Assert.Equal("Dec", profiles[11].Month);
        Assert.All(profiles, p =>
        {
            Assert.Equal(100, p.AverageLoadKW);
            Assert.Equal(150, p.PeakDemandKW);
            Assert.Equal(0.90, p.PowerFactor);
            Assert.Equal(730, p.OperatingHours);
        });
    }

    // ── Real-World ───────────────────────────────────────────────────────────

    [Fact]
    public void RealWorld_IndustrialFacility()
    {
        var rate = new UtilityRate
        {
            EnergyChargePerKWh = 0.085,
            DemandChargePerKW = 15.0,
            PowerFactorThreshold = 0.90,
            UsesPowerFactorPenalty = true,
            MonthlyFixedCharge = 150,
            TaxAndSurchargePercent = 3.0,
        };

        // Seasonal variation
        var profiles = new List<MonthlyLoadProfile>
        {
            new() { Month = "Jan", AverageLoadKW = 800, PeakDemandKW = 1200, OperatingHours = 744, PowerFactor = 0.82 },
            new() { Month = "Feb", AverageLoadKW = 750, PeakDemandKW = 1100, OperatingHours = 672, PowerFactor = 0.82 },
            new() { Month = "Mar", AverageLoadKW = 850, PeakDemandKW = 1250, OperatingHours = 744, PowerFactor = 0.83 },
            new() { Month = "Apr", AverageLoadKW = 900, PeakDemandKW = 1300, OperatingHours = 720, PowerFactor = 0.84 },
            new() { Month = "May", AverageLoadKW = 950, PeakDemandKW = 1400, OperatingHours = 744, PowerFactor = 0.83 },
            new() { Month = "Jun", AverageLoadKW = 1100, PeakDemandKW = 1600, OperatingHours = 720, PowerFactor = 0.80 },
            new() { Month = "Jul", AverageLoadKW = 1200, PeakDemandKW = 1800, OperatingHours = 744, PowerFactor = 0.78 },
            new() { Month = "Aug", AverageLoadKW = 1150, PeakDemandKW = 1750, OperatingHours = 744, PowerFactor = 0.79 },
            new() { Month = "Sep", AverageLoadKW = 1000, PeakDemandKW = 1500, OperatingHours = 720, PowerFactor = 0.81 },
            new() { Month = "Oct", AverageLoadKW = 900, PeakDemandKW = 1350, OperatingHours = 744, PowerFactor = 0.83 },
            new() { Month = "Nov", AverageLoadKW = 850, PeakDemandKW = 1200, OperatingHours = 720, PowerFactor = 0.82 },
            new() { Month = "Dec", AverageLoadKW = 800, PeakDemandKW = 1150, OperatingHours = 744, PowerFactor = 0.82 },
        };

        var result = EnergyUtilityCostService.CalculateAnnualCost(profiles, rate);

        Assert.Equal(12, result.MonthlyBills.Count);
        Assert.True(result.AnnualCost > 100000, "Large industrial facility should cost > $100k/yr");
        Assert.True(result.TotalPowerFactorPenalty > 0, "Low PF should incur penalties");
        Assert.True(result.TotalEnergyKWh > 5000000, "Should use > 5M kWh/yr");
    }
}
