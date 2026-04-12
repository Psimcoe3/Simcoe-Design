using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class NecAmpacityDatabaseTests
{
    // ── Model Defaults ───────────────────────────────────────────────────────

    [Fact]
    public void InsulationTemperatureRating_HasThreeValues()
    {
        var values = Enum.GetValues<InsulationTemperatureRating>();
        Assert.Equal(3, values.Length);
        Assert.Contains(InsulationTemperatureRating.C60, values);
        Assert.Contains(InsulationTemperatureRating.C75, values);
        Assert.Contains(InsulationTemperatureRating.C90, values);
    }

    [Fact]
    public void WireMaterial_DefaultProperties()
    {
        var wm = new WireMaterial();
        Assert.Equal("", wm.Id);
        Assert.Equal("", wm.Name);
        Assert.Equal(ConductorMaterial.Copper, wm.Material);
        Assert.Equal(0.0, wm.BaseResistivityOhmsPerKFt);
        Assert.Equal(InsulationTemperatureRating.C60, wm.TemperatureRating);
    }

    [Fact]
    public void AmpacityTable_DefaultProperties()
    {
        var table = new AmpacityTable();
        Assert.Equal("", table.Id);
        Assert.Equal("", table.Name);
        Assert.Equal(ConductorMaterial.Copper, table.Material);
        Assert.Equal(InsulationTemperatureRating.C60, table.TemperatureRating);
        Assert.Empty(table.Entries);
    }

    [Fact]
    public void AmpacityTable_Lookup_ReturnsAmps()
    {
        var table = new AmpacityTable
        {
            Entries = new Dictionary<string, int> { ["12"] = 25, ["10"] = 35 }
        };
        Assert.Equal(25, table.Lookup("12"));
        Assert.Equal(35, table.Lookup("10"));
    }

    [Fact]
    public void AmpacityTable_Lookup_ReturnsZeroForUnknownSize()
    {
        var table = new AmpacityTable
        {
            Entries = new Dictionary<string, int> { ["12"] = 25 }
        };
        Assert.Equal(0, table.Lookup("14"));
        Assert.Equal(0, table.Lookup("999"));
    }

    [Fact]
    public void CorrectionFactorEntry_DefaultProperties()
    {
        var entry = new CorrectionFactorEntry();
        Assert.Equal(0, entry.AmbientTempMinC);
        Assert.Equal(0, entry.AmbientTempMaxC);
        Assert.Equal(0.0, entry.Factor60C);
        Assert.Equal(0.0, entry.Factor75C);
        Assert.Equal(0.0, entry.Factor90C);
    }

    [Fact]
    public void CorrectionFactorTable_GetFactor_MatchesEntry()
    {
        var table = new CorrectionFactorTable
        {
            Entries = new List<CorrectionFactorEntry>
            {
                new() { AmbientTempMinC = 31, AmbientTempMaxC = 35, Factor60C = 0.91, Factor75C = 0.94, Factor90C = 0.96 }
            }
        };
        Assert.Equal(0.91, table.GetFactor(33, InsulationTemperatureRating.C60));
        Assert.Equal(0.94, table.GetFactor(33, InsulationTemperatureRating.C75));
        Assert.Equal(0.96, table.GetFactor(33, InsulationTemperatureRating.C90));
    }

    [Fact]
    public void CorrectionFactorTable_GetFactor_ReturnsOneForNoMatch()
    {
        var table = new CorrectionFactorTable
        {
            Entries = new List<CorrectionFactorEntry>
            {
                new() { AmbientTempMinC = 31, AmbientTempMaxC = 35, Factor60C = 0.91, Factor75C = 0.94, Factor90C = 0.96 }
            }
        };
        Assert.Equal(1.0, table.GetFactor(80, InsulationTemperatureRating.C75));
    }

    // ── NEC Default Tables ───────────────────────────────────────────────────

    [Fact]
    public void DefaultCopper75C_Matches_PreviousHardcodedValues()
    {
        var table = NecAmpacityService.DefaultCopper75C;
        Assert.Equal(20, table.Lookup("14"));
        Assert.Equal(25, table.Lookup("12"));
        Assert.Equal(35, table.Lookup("10"));
        Assert.Equal(50, table.Lookup("8"));
        Assert.Equal(65, table.Lookup("6"));
        Assert.Equal(85, table.Lookup("4"));
        Assert.Equal(130, table.Lookup("1"));
        Assert.Equal(150, table.Lookup("1/0"));
        Assert.Equal(230, table.Lookup("4/0"));
        Assert.Equal(380, table.Lookup("500"));
    }

    [Fact]
    public void DefaultAluminum75C_Matches_PreviousHardcodedValues()
    {
        var table = NecAmpacityService.DefaultAluminum75C;
        Assert.Equal(15, table.Lookup("14"));
        Assert.Equal(20, table.Lookup("12"));
        Assert.Equal(30, table.Lookup("10"));
        Assert.Equal(40, table.Lookup("8"));
        Assert.Equal(50, table.Lookup("6"));
        Assert.Equal(65, table.Lookup("4"));
        Assert.Equal(100, table.Lookup("1"));
        Assert.Equal(120, table.Lookup("1/0"));
        Assert.Equal(180, table.Lookup("4/0"));
        Assert.Equal(310, table.Lookup("500"));
    }

    [Fact]
    public void DefaultCopper60C_HasAllStandardSizes()
    {
        var table = NecAmpacityService.DefaultCopper60C;
        Assert.Equal(18, table.Entries.Count);
        Assert.Equal(15, table.Lookup("14"));
        Assert.Equal(320, table.Lookup("500"));
    }

    [Fact]
    public void DefaultCopper90C_HasAllStandardSizes()
    {
        var table = NecAmpacityService.DefaultCopper90C;
        Assert.Equal(18, table.Entries.Count);
        Assert.Equal(25, table.Lookup("14"));
        Assert.Equal(430, table.Lookup("500"));
    }

    [Fact]
    public void DefaultAluminum60C_OmitsSmallSizes()
    {
        var table = NecAmpacityService.DefaultAluminum60C;
        Assert.Equal(0, table.Lookup("14")); // Not rated for #14 aluminum at 60C
        Assert.Equal(15, table.Lookup("12"));
    }

    [Fact]
    public void DefaultAluminum90C_HasExpectedValues()
    {
        var table = NecAmpacityService.DefaultAluminum90C;
        Assert.Equal(25, table.Lookup("12"));
        Assert.Equal(350, table.Lookup("500"));
    }

    [Theory]
    [InlineData(InsulationTemperatureRating.C60)]
    [InlineData(InsulationTemperatureRating.C75)]
    [InlineData(InsulationTemperatureRating.C90)]
    public void GetDefaultTable_ReturnsCorrectCopper(InsulationTemperatureRating rating)
    {
        var table = NecAmpacityService.GetDefaultTable(ConductorMaterial.Copper, rating);
        Assert.Equal(ConductorMaterial.Copper, table.Material);
        Assert.Equal(rating, table.TemperatureRating);
        Assert.NotEmpty(table.Entries);
    }

    [Theory]
    [InlineData(InsulationTemperatureRating.C60)]
    [InlineData(InsulationTemperatureRating.C75)]
    [InlineData(InsulationTemperatureRating.C90)]
    public void GetDefaultTable_ReturnsCorrectAluminum(InsulationTemperatureRating rating)
    {
        var table = NecAmpacityService.GetDefaultTable(ConductorMaterial.Aluminum, rating);
        Assert.Equal(ConductorMaterial.Aluminum, table.Material);
        Assert.Equal(rating, table.TemperatureRating);
        Assert.NotEmpty(table.Entries);
    }

    [Fact]
    public void GetAllDefaultTables_ReturnsSixTables()
    {
        var tables = NecAmpacityService.GetAllDefaultTables();
        Assert.Equal(6, tables.Count);
    }

    // ── LookupAmpacity ──────────────────────────────────────────────────────

    [Fact]
    public void LookupAmpacity_Copper75C_DefaultTable()
    {
        int amps = NecAmpacityService.LookupAmpacity("12", ConductorMaterial.Copper);
        Assert.Equal(25, amps);
    }

    [Fact]
    public void LookupAmpacity_Aluminum75C_DefaultTable()
    {
        int amps = NecAmpacityService.LookupAmpacity("12", ConductorMaterial.Aluminum);
        Assert.Equal(20, amps);
    }

    [Fact]
    public void LookupAmpacity_CustomTable_OverridesDefault()
    {
        var custom = new AmpacityTable
        {
            Entries = new Dictionary<string, int> { ["12"] = 999 }
        };
        int amps = NecAmpacityService.LookupAmpacity("12", ConductorMaterial.Copper, customTable: custom);
        Assert.Equal(999, amps);
    }

    [Fact]
    public void LookupAmpacity_UnknownSize_ReturnsZero()
    {
        int amps = NecAmpacityService.LookupAmpacity("0000", ConductorMaterial.Copper);
        Assert.Equal(0, amps);
    }

    // ── Correction Factors ───────────────────────────────────────────────────

    [Fact]
    public void DefaultCorrectionFactors_At30C_ReturnsOne()
    {
        var factors = NecAmpacityService.DefaultCorrectionFactors;
        Assert.Equal(1.00, factors.GetFactor(30, InsulationTemperatureRating.C60));
        Assert.Equal(1.00, factors.GetFactor(30, InsulationTemperatureRating.C75));
        Assert.Equal(1.00, factors.GetFactor(30, InsulationTemperatureRating.C90));
    }

    [Fact]
    public void DefaultCorrectionFactors_At40C_ReturnsExpected()
    {
        var factors = NecAmpacityService.DefaultCorrectionFactors;
        Assert.Equal(0.82, factors.GetFactor(40, InsulationTemperatureRating.C60));
        Assert.Equal(0.88, factors.GetFactor(40, InsulationTemperatureRating.C75));
        Assert.Equal(0.91, factors.GetFactor(40, InsulationTemperatureRating.C90));
    }

    [Fact]
    public void DefaultCorrectionFactors_At50C_75C_Returns075()
    {
        var factors = NecAmpacityService.DefaultCorrectionFactors;
        Assert.Equal(0.75, factors.GetFactor(50, InsulationTemperatureRating.C75));
    }

    [Fact]
    public void DefaultCorrectionFactors_60C_Zeroes_AtHighAmbient()
    {
        var factors = NecAmpacityService.DefaultCorrectionFactors;
        // 60°C insulation can't be used above 55°C ambient
        Assert.Equal(0.00, factors.GetFactor(58, InsulationTemperatureRating.C60));
    }

    [Fact]
    public void DefaultCorrectionFactors_HasTwelveEntries()
    {
        Assert.Equal(12, NecAmpacityService.DefaultCorrectionFactors.Entries.Count);
    }

    // ── GetCorrectedAmpacity ─────────────────────────────────────────────────

    [Fact]
    public void GetCorrectedAmpacity_At30C_EqualsBase()
    {
        // At 30°C ambient, factor = 1.0
        double corrected = NecAmpacityService.GetCorrectedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 30);
        Assert.Equal(25.0, corrected);
    }

    [Fact]
    public void GetCorrectedAmpacity_At40C_ReducesAmpacity()
    {
        // Copper #12 at 75°C = 25A, factor at 40°C = 0.88
        double corrected = NecAmpacityService.GetCorrectedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 40);
        Assert.Equal(25.0 * 0.88, corrected);
    }

    [Fact]
    public void GetCorrectedAmpacity_CustomFactors()
    {
        var customFactors = new CorrectionFactorTable
        {
            Entries = new List<CorrectionFactorEntry>
            {
                new() { AmbientTempMinC = 26, AmbientTempMaxC = 30, Factor60C = 0.50, Factor75C = 0.50, Factor90C = 0.50 }
            }
        };
        double corrected = NecAmpacityService.GetCorrectedAmpacity(
            "12", ConductorMaterial.Copper, InsulationTemperatureRating.C75, 30, customFactors: customFactors);
        Assert.Equal(25.0 * 0.50, corrected);
    }

    // ── RecommendWireSize ────────────────────────────────────────────────────

    [Fact]
    public void RecommendWireSize_20A_Copper75C_Returns14()
    {
        // NEC 310.16: #14 copper at 75°C = 20A, which meets 20A requirement
        string? size = NecAmpacityService.RecommendWireSize(20, ConductorMaterial.Copper);
        Assert.Equal("14", size);
    }

    [Fact]
    public void RecommendWireSize_100A_Copper75C_Returns3()
    {
        // NEC 310.16: #3 copper at 75°C = 100A, which meets 100A requirement
        string? size = NecAmpacityService.RecommendWireSize(100, ConductorMaterial.Copper);
        Assert.Equal("3", size);
    }

    [Fact]
    public void RecommendWireSize_400A_Aluminum75C()
    {
        string? size = NecAmpacityService.RecommendWireSize(400, ConductorMaterial.Aluminum);
        // Aluminum 75C: 500 kcmil = 310A which is too small; should return null
        Assert.Null(size);
    }

    [Fact]
    public void RecommendWireSize_WithAmbientCorrection_UpSizes()
    {
        // At 30°C: 12 AWG copper 75°C = 25A, sufficient for 24A
        string? sizeNormal = NecAmpacityService.RecommendWireSize(24, ConductorMaterial.Copper, ambientTempC: 30);
        Assert.Equal("12", sizeNormal);

        // At 40°C: 12 AWG copper 75°C = 25 * 0.88 = 22A, NOT sufficient for 24A → up to 10 AWG (35 * 0.88 = 30.8)
        string? sizeHot = NecAmpacityService.RecommendWireSize(24, ConductorMaterial.Copper, ambientTempC: 40);
        Assert.Equal("10", sizeHot);
    }

    [Fact]
    public void RecommendWireSize_CustomTable_Overrides()
    {
        var custom = new AmpacityTable
        {
            Entries = new Dictionary<string, int>
            {
                ["14"] = 100, // Hypothetical high-ampacity 14 AWG
            }
        };
        string? size = NecAmpacityService.RecommendWireSize(50, ConductorMaterial.Copper, customTable: custom);
        Assert.Equal("14", size);
    }

    [Fact]
    public void RecommendWireSize_NullWhenNoSizeSufficient()
    {
        string? size = NecAmpacityService.RecommendWireSize(9999, ConductorMaterial.Copper);
        Assert.Null(size);
    }

    [Fact]
    public void RecommendWireSize_90C_SmallerWireThan75C()
    {
        // 90°C has higher ampacity so should allow smaller wire
        string? size75 = NecAmpacityService.RecommendWireSize(
            40, ConductorMaterial.Copper, InsulationTemperatureRating.C75);
        string? size90 = NecAmpacityService.RecommendWireSize(
            40, ConductorMaterial.Copper, InsulationTemperatureRating.C90);

        // 75°C: #8 = 50A (first ≥ 40), 90°C: #10 = 40A (first ≥ 40)
        Assert.Equal("8", size75);
        Assert.Equal("10", size90);
    }

    // ── Wire Materials ───────────────────────────────────────────────────────

    [Fact]
    public void DefaultWireMaterials_HasTwoEntries()
    {
        var materials = NecAmpacityService.GetDefaultWireMaterials();
        Assert.Equal(2, materials.Count);
        Assert.Contains(materials, m => m.Material == ConductorMaterial.Copper);
        Assert.Contains(materials, m => m.Material == ConductorMaterial.Aluminum);
    }

    [Fact]
    public void DefaultCopperThhn_HasCorrectProperties()
    {
        var cu = NecAmpacityService.DefaultCopperThhn;
        Assert.Equal("Cu-THHN", cu.Id);
        Assert.Equal(ConductorMaterial.Copper, cu.Material);
        Assert.Equal(InsulationTemperatureRating.C90, cu.TemperatureRating);
        Assert.True(cu.BaseResistivityOhmsPerKFt > 0);
    }

    [Fact]
    public void DefaultAluminumXhhw_HasCorrectProperties()
    {
        var al = NecAmpacityService.DefaultAluminumXhhw;
        Assert.Equal("Al-XHHW", al.Id);
        Assert.Equal(ConductorMaterial.Aluminum, al.Material);
        Assert.Equal(InsulationTemperatureRating.C90, al.TemperatureRating);
        Assert.True(al.BaseResistivityOhmsPerKFt > 0);
    }

    // ── ProjectModel Integration ─────────────────────────────────────────────

    [Fact]
    public void ProjectModel_HasWireMaterials()
    {
        var project = new ProjectModel();
        Assert.NotNull(project.WireMaterials);
        Assert.Empty(project.WireMaterials);
    }

    [Fact]
    public void ProjectModel_HasAmpacityTables()
    {
        var project = new ProjectModel();
        Assert.NotNull(project.AmpacityTables);
        Assert.Empty(project.AmpacityTables);
    }

    [Fact]
    public void ProjectModel_CanStoreCustomTables()
    {
        var project = new ProjectModel();
        project.AmpacityTables.AddRange(NecAmpacityService.GetAllDefaultTables());
        project.WireMaterials.AddRange(NecAmpacityService.GetDefaultWireMaterials());
        Assert.Equal(6, project.AmpacityTables.Count);
        Assert.Equal(2, project.WireMaterials.Count);
    }

    // ── Backward Compatibility — existing services still work ────────────────

    [Fact]
    public void ElectricalCalculationService_RecommendWireSize_StillWorks()
    {
        var svc = new ElectricalCalculationService();
        var circuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400, // 20A
            DemandFactor = 1.0,
            Poles = 1,
            WireLengthFeet = 50,
            Wire = new WireSpec { Size = "14", Material = ConductorMaterial.Copper }
        };
        var result = svc.RecommendWireSize(circuit);
        Assert.NotNull(result);
        Assert.NotNull(result.RecommendedSize);
    }

    [Fact]
    public void ElectricalCalculationService_RecommendWireSize_WithCustomTable()
    {
        var svc = new ElectricalCalculationService();
        var circuit = new Circuit
        {
            Voltage = 120,
            ConnectedLoadVA = 2400, // 20A
            DemandFactor = 1.0,
            Poles = 1,
            WireLengthFeet = 50,
            Wire = new WireSpec { Size = "14", Material = ConductorMaterial.Copper }
        };

        // Custom table with boosted #14 ampacity — should suffice for 20A
        var custom = new AmpacityTable
        {
            Entries = new Dictionary<string, int>
            {
                ["14"] = 25, ["12"] = 30, ["10"] = 40, ["8"] = 55,
                ["6"] = 75, ["4"] = 95, ["3"] = 115, ["2"] = 130,
                ["1"] = 150, ["1/0"] = 170, ["2/0"] = 200, ["3/0"] = 230,
                ["4/0"] = 260, ["250"] = 290, ["300"] = 320, ["350"] = 350,
                ["400"] = 380, ["500"] = 430,
            }
        };
        var result = svc.RecommendWireSize(circuit, customAmpacityTable: custom);
        Assert.NotNull(result);
        // With boosted table, ampacity for #14 = 25A ≥ 20A → first pass picks #14
        Assert.Equal("14", result.MinSizeForAmpacity);
    }

    [Fact]
    public void NecDesignRuleService_ValidateAll_StillWorks()
    {
        var svc = new NecDesignRuleService();
        var calcSvc = new ElectricalCalculationService();
        var violations = svc.ValidateAll(
            new List<Circuit>(),
            new List<PanelSchedule>(),
            calcSvc);
        Assert.NotNull(violations);
        Assert.Empty(violations);
    }

    // ── StandardSizes ordering ───────────────────────────────────────────────

    [Fact]
    public void StandardSizes_Has18Entries()
    {
        Assert.Equal(18, NecAmpacityService.StandardSizes.Length);
        Assert.Equal("14", NecAmpacityService.StandardSizes[0]);
        Assert.Equal("500", NecAmpacityService.StandardSizes[^1]);
    }

    [Fact]
    public void StandardSizes_AmpacityIncreases_LeftToRight()
    {
        var table = NecAmpacityService.DefaultCopper75C;
        int prev = 0;
        foreach (var size in NecAmpacityService.StandardSizes)
        {
            int amps = table.Lookup(size);
            if (amps > 0)
            {
                Assert.True(amps >= prev, $"Ampacity decreased at {size}: {amps} < {prev}");
                prev = amps;
            }
        }
    }

    // ── NEC Table consistency: 60 < 75 < 90 for same size ────────────────────

    [Theory]
    [InlineData("12")]
    [InlineData("4")]
    [InlineData("1/0")]
    [InlineData("4/0")]
    [InlineData("500")]
    public void Copper_AmpacityIncreases_WithTemperature(string size)
    {
        int a60 = NecAmpacityService.DefaultCopper60C.Lookup(size);
        int a75 = NecAmpacityService.DefaultCopper75C.Lookup(size);
        int a90 = NecAmpacityService.DefaultCopper90C.Lookup(size);
        Assert.True(a60 < a75, $"60°C ({a60}) should be < 75°C ({a75}) for {size}");
        Assert.True(a75 < a90, $"75°C ({a75}) should be < 90°C ({a90}) for {size}");
    }

    [Theory]
    [InlineData("12")]
    [InlineData("4")]
    [InlineData("1/0")]
    [InlineData("4/0")]
    [InlineData("500")]
    public void Aluminum_AmpacityIncreases_WithTemperature(string size)
    {
        int a60 = NecAmpacityService.DefaultAluminum60C.Lookup(size);
        int a75 = NecAmpacityService.DefaultAluminum75C.Lookup(size);
        int a90 = NecAmpacityService.DefaultAluminum90C.Lookup(size);
        Assert.True(a60 < a75, $"60°C ({a60}) should be < 75°C ({a75}) for {size}");
        Assert.True(a75 < a90, $"75°C ({a75}) should be < 90°C ({a90}) for {size}");
    }

    // ── Copper > Aluminum for same size/rating ───────────────────────────────

    [Theory]
    [InlineData("12", InsulationTemperatureRating.C75)]
    [InlineData("4", InsulationTemperatureRating.C75)]
    [InlineData("1/0", InsulationTemperatureRating.C90)]
    [InlineData("500", InsulationTemperatureRating.C60)]
    public void Copper_HasHigherAmpacity_ThanAluminum(string size, InsulationTemperatureRating rating)
    {
        var cu = NecAmpacityService.GetDefaultTable(ConductorMaterial.Copper, rating);
        var al = NecAmpacityService.GetDefaultTable(ConductorMaterial.Aluminum, rating);
        int cuAmps = cu.Lookup(size);
        int alAmps = al.Lookup(size);
        Assert.True(cuAmps > alAmps, $"Copper ({cuAmps}A) should exceed aluminum ({alAmps}A) for {size} at {rating}");
    }

    // ── NEC default fallback when ProjectModel is empty ──────────────────────

    [Fact]
    public void FallbackToNecDefaults_WhenProjectTablesEmpty()
    {
        var project = new ProjectModel();
        Assert.Empty(project.AmpacityTables);

        // Service still works with defaults
        int amps = NecAmpacityService.LookupAmpacity("12", ConductorMaterial.Copper);
        Assert.Equal(25, amps);

        string? size = NecAmpacityService.RecommendWireSize(20, ConductorMaterial.Copper);
        Assert.Equal("14", size);
    }
}
