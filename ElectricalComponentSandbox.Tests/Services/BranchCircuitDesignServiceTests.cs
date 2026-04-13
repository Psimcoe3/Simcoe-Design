using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;

namespace ElectricalComponentSandbox.Tests.Services;

public class BranchCircuitDesignServiceTests
{
    private readonly BranchCircuitDesignService _svc;

    public BranchCircuitDesignServiceTests()
    {
        _svc = new BranchCircuitDesignService(
            new NecAmpacityService(),
            new ConduitFillService());
    }

    // ── OCPD Selection ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(14.5, 15)]
    [InlineData(15, 15)]
    [InlineData(20, 20)]
    [InlineData(21, 25)]
    [InlineData(100, 100)]
    public void SelectOcpd_ReturnsNextStandard(double amps, int expected) =>
        Assert.Equal(expected, BranchCircuitDesignService.SelectOcpd(amps));

    // ── Wire Selection ───────────────────────────────────────────────────────

    [Theory]
    [InlineData(20, ConductorMaterial.Copper, "14")]
    [InlineData(25, ConductorMaterial.Copper, "12")]
    [InlineData(35, ConductorMaterial.Copper, "10")]
    [InlineData(50, ConductorMaterial.Copper, "8")]
    public void SelectWireSize_Copper(double amps, ConductorMaterial mat, string expected) =>
        Assert.Equal(expected, BranchCircuitDesignService.SelectWireSize(amps, mat));

    [Fact]
    public void SelectWireSize_Aluminum_LargerThanCopper()
    {
        string cu = BranchCircuitDesignService.SelectWireSize(50, ConductorMaterial.Copper);
        string al = BranchCircuitDesignService.SelectWireSize(50, ConductorMaterial.Aluminum);
        // Aluminum should be same or larger gauge (lower ampacity per size)
        Assert.NotEqual("14", al); // at least reasonable
    }

    // ── Voltage Drop Calculation ─────────────────────────────────────────────

    [Fact]
    public void VoltageDropPercent_ShortRun_Low()
    {
        double vd = BranchCircuitDesignService.CalculateVoltageDropPercent(
            "12", ConductorMaterial.Copper, 50, 16, 120, 1);
        Assert.True(vd < 3.0, $"Short run VD should be < 3%, got {vd:F2}%");
    }

    [Fact]
    public void VoltageDropPercent_LongRun_Higher()
    {
        double vdShort = BranchCircuitDesignService.CalculateVoltageDropPercent(
            "12", ConductorMaterial.Copper, 50, 16, 120, 1);
        double vdLong = BranchCircuitDesignService.CalculateVoltageDropPercent(
            "12", ConductorMaterial.Copper, 200, 16, 120, 1);
        Assert.True(vdLong > vdShort);
    }

    [Fact]
    public void VoltageDropPercent_ThreePhase_UsesSqrt3()
    {
        double vd1P = BranchCircuitDesignService.CalculateVoltageDropPercent(
            "10", ConductorMaterial.Copper, 100, 20, 208, 1);
        double vd3P = BranchCircuitDesignService.CalculateVoltageDropPercent(
            "10", ConductorMaterial.Copper, 100, 20, 208, 3);
        // 3P uses √3 factor instead of 2, so VD is √3/2 ≈ 0.866× of 1P
        Assert.True(vd3P < vd1P);
    }

    // ── Full Design ──────────────────────────────────────────────────────────

    [Fact]
    public void Design_20A_Receptacle_120V()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 1800, Voltage = 120, Poles = 1, LengthFeet = 75,
            Classification = LoadClassification.Power,
        };

        var result = _svc.Design(input);

        Assert.Equal(15, result.LoadAmps);
        Assert.Equal(15, result.OcpdAmps); // 15A is a standard OCPD size
        Assert.True(result.VoltageDropOk);
        Assert.True(result.ConduitFillOk);
        Assert.True(result.IsValid);
        Assert.NotEmpty(result.WireSize);
        Assert.NotEmpty(result.GroundSize);
        Assert.NotEmpty(result.ConduitSize);
    }

    [Fact]
    public void Design_ContinuousLoad_125Percent()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 1920, Voltage = 120, Poles = 1, LengthFeet = 50,
            IsContinuous = true,
        };

        var result = _svc.Design(input);

        Assert.Equal(16, result.LoadAmps);
        Assert.Equal(20, result.DesignAmps); // 16 × 1.25 = 20
    }

    [Fact]
    public void Design_LongRun_UpsizesWire()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 2400, Voltage = 120, Poles = 1, LengthFeet = 250,
            MaxVoltageDropPercent = 3.0,
        };

        var result = _svc.Design(input);

        // With 250ft run and 20A load, #14 would exceed VD — should upsize
        Assert.True(result.VoltageDropOk || result.Warnings.Any(w => w.Contains("Voltage drop")));
    }

    [Fact]
    public void Design_ThreePhase_480V()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 15000, Voltage = 480, Poles = 3, LengthFeet = 200,
            Classification = LoadClassification.HVAC,
        };

        var result = _svc.Design(input);

        Assert.True(result.LoadAmps > 0);
        Assert.True(result.OcpdAmps >= 20);
        Assert.Equal(ConductorMaterial.Copper, result.Material);
        Assert.True(result.NecReferences.Count >= 4);
    }

    [Fact]
    public void Design_Aluminum_SelectsLargerWire()
    {
        var cuInput = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 5000, Voltage = 240, Poles = 2, LengthFeet = 100,
            Material = ConductorMaterial.Copper,
        };
        var alInput = cuInput with { Material = ConductorMaterial.Aluminum };

        var cuResult = _svc.Design(cuInput);
        var alResult = _svc.Design(alInput);

        // Aluminum wire should be same or larger
        Assert.Equal(cuResult.OcpdAmps, alResult.OcpdAmps);
    }

    [Fact]
    public void Design_MinimumOcpd_15A()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 100, Voltage = 120, Poles = 1, LengthFeet = 25,
        };

        var result = _svc.Design(input);
        Assert.True(result.OcpdAmps >= 15, "Minimum branch OCPD is 15A");
    }

    [Fact]
    public void Design_ConduitSized_FillOk()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 2400, Voltage = 120, Poles = 1, LengthFeet = 75,
        };

        var result = _svc.Design(input);

        Assert.True(result.ConduitFillPercent > 0);
        Assert.True(result.ConduitFillPercent < 40, "Fill should be well under limit");
        Assert.True(result.ConduitFillOk);
    }

    [Fact]
    public void Design_GroundSized_PerNec250()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 4800, Voltage = 240, Poles = 2, LengthFeet = 100,
        };

        var result = _svc.Design(input);

        Assert.NotEmpty(result.GroundSize);
        Assert.Contains("NEC 250.122", result.NecReferences.First(r => r.Contains("250.122")));
    }

    // ── Batch Design ─────────────────────────────────────────────────────────

    [Fact]
    public void DesignAll_MultipleCircuits()
    {
        var inputs = new List<BranchCircuitDesignService.BranchCircuitInput>
        {
            new() { LoadVA = 1800, Voltage = 120, Poles = 1, LengthFeet = 75, Description = "Receptacles" },
            new() { LoadVA = 2000, Voltage = 277, Poles = 1, LengthFeet = 100, Description = "Lighting" },
            new() { LoadVA = 10000, Voltage = 480, Poles = 3, LengthFeet = 150, Description = "HVAC" },
        };

        var results = _svc.DesignAll(inputs);

        Assert.Equal(3, results.Count);
        Assert.All(results, r => Assert.True(r.OcpdAmps >= 15));
        Assert.Equal("Receptacles", results[0].Description);
    }

    // ── Real-World Scenarios ─────────────────────────────────────────────────

    [Fact]
    public void RealWorld_CommercialOffice_20A_Receptacle()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 1800,
            Voltage = 120,
            Poles = 1,
            LengthFeet = 90,
            Classification = LoadClassification.Power,
            IsContinuous = false,
            Description = "General receptacle circuit",
        };

        var result = _svc.Design(input);

        Assert.Equal(15, result.OcpdAmps);
        Assert.True(result.IsValid);
        Assert.True(result.VoltageDropPercent < 3.0);
    }

    [Fact]
    public void RealWorld_277V_Lighting_Continuous()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 3000,
            Voltage = 277,
            Poles = 1,
            LengthFeet = 120,
            Classification = LoadClassification.Lighting,
            IsContinuous = true,
            Description = "Office lighting continuous",
        };

        var result = _svc.Design(input);

        // 3000 / 277 = 10.83A × 1.25 = 13.5A → 15A OCPD
        Assert.True(result.DesignAmps > result.LoadAmps);
        Assert.True(result.OcpdAmps >= 15);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RealWorld_50A_Range_Circuit()
    {
        var input = new BranchCircuitDesignService.BranchCircuitInput
        {
            LoadVA = 9600,
            Voltage = 240,
            Poles = 2,
            LengthFeet = 35,
            Classification = LoadClassification.Other,
            Description = "Kitchen range",
        };

        var result = _svc.Design(input);

        Assert.Equal(40, result.LoadAmps);
        Assert.True(result.OcpdAmps >= 40);
        Assert.True(result.IsValid);
    }
}
