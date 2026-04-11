using ElectricalComponentSandbox.Conduit.Core.Model;
using ElectricalComponentSandbox.Models;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class ConduitMeasurementServiceTests
{
    // ── FormatFeetInches ─────────────────────────────────────────────────

    [Theory]
    [InlineData(0.0, "0\"")]
    [InlineData(1.0, "1'-0\"")]
    [InlineData(1.5, "1'-6\"")]
    [InlineData(12.25, "12'-3\"")]
    [InlineData(0.5, "6\"")]
    [InlineData(100.0, "100'-0\"")]
    public void FormatFeetInches_FormatsCorrectly(double feet, string expected)
    {
        Assert.Equal(expected, ConduitMeasurementService.FormatFeetInches(feet));
    }

    [Fact]
    public void FormatFeetInches_NegativeValue_TreatedAsPositive()
    {
        Assert.Equal("5'-0\"", ConduitMeasurementService.FormatFeetInches(-5.0));
    }

    [Fact]
    public void FormatFeetInches_RoundsUpTo12Inches_IncrementsFootage()
    {
        // 1.99 ft = 1 ft + 11.88 in → rounds to 12 in → 2'-0"
        Assert.Equal("2'-0\"", ConduitMeasurementService.FormatFeetInches(1.999));
    }

    // ── FormatDecimalFeet ────────────────────────────────────────────────

    [Fact]
    public void FormatDecimalFeet_DefaultPrecision()
    {
        Assert.Equal("12.5 ft", ConduitMeasurementService.FormatDecimalFeet(12.50));
    }

    [Fact]
    public void FormatDecimalFeet_CustomPrecision()
    {
        Assert.Equal("12.25 ft", ConduitMeasurementService.FormatDecimalFeet(12.25, 2));
    }

    // ── ComputeLabel ─────────────────────────────────────────────────────

    [Fact]
    public void ComputeLabel_HorizontalSegment()
    {
        var start = new XYZ(0, 0, 0);
        var end = new XYZ(10, 0, 0);
        Assert.Equal("10'-0\"", ConduitMeasurementService.ComputeLabel(start, end));
    }

    [Fact]
    public void ComputeLabel_DiagonalSegment()
    {
        var start = new XYZ(0, 0, 0);
        var end = new XYZ(3, 4, 0);
        // Distance = 5 feet
        Assert.Equal("5'-0\"", ConduitMeasurementService.ComputeLabel(start, end));
    }

    // ── ComputeMidpoint ──────────────────────────────────────────────────

    [Fact]
    public void ComputeMidpoint_ReturnsCenter()
    {
        var start = new XYZ(0, 0, 0);
        var end = new XYZ(10, 6, 4);
        var mid = ConduitMeasurementService.ComputeMidpoint(start, end);

        Assert.Equal(5.0, mid.X);
        Assert.Equal(3.0, mid.Y);
        Assert.Equal(2.0, mid.Z);
    }

    // ── GetRunLengthLabel ────────────────────────────────────────────────

    [Fact]
    public void GetRunLengthLabel_ReturnsFormattedTotal()
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "t1" });
        store.Settings.DefaultConduitTypeId = "t1";
        store.Settings.AutoInsertFittings = false;

        var seg = new ConduitSegment
        {
            StartPoint = new XYZ(0, 0, 0),
            EndPoint = new XYZ(12, 0, 0)
        };
        var run = store.CreateRunFromSegments(new List<ConduitSegment> { seg });

        var (totalFeet, label) = ConduitMeasurementService.GetRunLengthLabel(store, run.Id);

        Assert.Equal(12.0, totalFeet, 3);
        Assert.Equal("12'-0\"", label);
    }
}

public class CircuitSummaryConduitTests
{
    // ── Helpers ──────────────────────────────────────────────────────────

    private static Circuit MakeCircuit(string number, string wireSize = "12", int lengthFt = 100)
    {
        return new Circuit
        {
            CircuitNumber = number,
            Description = $"Circuit {number}",
            Voltage = 120,
            ConnectedLoadVA = 1800,
            WireLengthFeet = lengthFt,
            Wire = new WireSpec { Size = wireSize, Conductors = 2 },
            Breaker = new CircuitBreaker { TripAmps = 20, Poles = 1 }
        };
    }

    // ── Backward compatibility (no conduit data) ─────────────────────────

    [Fact]
    public void GenerateCircuitSummary_NoConduitData_Has8Columns()
    {
        var svc = new ScheduleTableService();
        var circuits = new List<Circuit> { MakeCircuit("1") };

        var table = svc.GenerateCircuitSummary(circuits);

        Assert.Equal(8, table.Columns.Count);
        Assert.DoesNotContain(table.Columns, c => c.Header == "RUN LENGTH");
        Assert.DoesNotContain(table.Columns, c => c.Header == "FILL %");
    }

    // ── RUN LENGTH column ────────────────────────────────────────────────

    [Fact]
    public void GenerateCircuitSummary_WithRunLengths_AddsColumn()
    {
        var svc = new ScheduleTableService();
        var circuits = new List<Circuit> { MakeCircuit("1") };
        var runLengths = new Dictionary<string, double> { ["1"] = 25.5 };

        var table = svc.GenerateCircuitSummary(circuits, runLengths, null);

        Assert.Equal(9, table.Columns.Count);
        Assert.Equal("RUN LENGTH", table.Columns[8].Header);
        Assert.Contains("25.5 ft", table.Rows[0][8]);
    }

    [Fact]
    public void GenerateCircuitSummary_MissingRunLength_ShowsDash()
    {
        var svc = new ScheduleTableService();
        var circuits = new List<Circuit> { MakeCircuit("1"), MakeCircuit("2") };
        var runLengths = new Dictionary<string, double> { ["1"] = 10.0 };

        var table = svc.GenerateCircuitSummary(circuits, runLengths, null);

        Assert.Contains("10.0 ft", table.Rows[0][8]);
        Assert.Equal("\u2014", table.Rows[1][8]); // em-dash for missing
    }

    // ── FILL % column ────────────────────────────────────────────────────

    [Fact]
    public void GenerateCircuitSummary_WithFillResults_AddsColumn()
    {
        var svc = new ScheduleTableService();
        var circuits = new List<Circuit> { MakeCircuit("1") };
        var fill = new ConduitFillResult
        {
            FillPercent = 28.5,
            ExceedsCode = false
        };
        var fills = new Dictionary<string, ConduitFillResult> { ["1"] = fill };

        var table = svc.GenerateCircuitSummary(circuits, null, fills);

        Assert.Equal(9, table.Columns.Count);
        Assert.Equal("FILL %", table.Columns[8].Header);
        Assert.Equal("28.5%", table.Rows[0][8]);
    }

    [Fact]
    public void GenerateCircuitSummary_FillExceedsCode_ShowsFlag()
    {
        var svc = new ScheduleTableService();
        var circuits = new List<Circuit> { MakeCircuit("1") };
        var fill = new ConduitFillResult
        {
            FillPercent = 55.0,
            ExceedsCode = true
        };
        var fills = new Dictionary<string, ConduitFillResult> { ["1"] = fill };

        var table = svc.GenerateCircuitSummary(circuits, null, fills);

        Assert.Equal("55.0% !", table.Rows[0][8]);
    }

    // ── Both RUN LENGTH and FILL % ───────────────────────────────────────

    [Fact]
    public void GenerateCircuitSummary_BothColumnsPresent()
    {
        var svc = new ScheduleTableService();
        var circuits = new List<Circuit> { MakeCircuit("1") };
        var runLengths = new Dictionary<string, double> { ["1"] = 50.0 };
        var fill = new ConduitFillResult { FillPercent = 35.0, ExceedsCode = false };
        var fills = new Dictionary<string, ConduitFillResult> { ["1"] = fill };

        var table = svc.GenerateCircuitSummary(circuits, runLengths, fills);

        Assert.Equal(10, table.Columns.Count);
        Assert.Equal("RUN LENGTH", table.Columns[8].Header);
        Assert.Equal("FILL %", table.Columns[9].Header);
        Assert.Contains("50.0 ft", table.Rows[0][8]);
        Assert.Equal("35.0%", table.Rows[0][9]);
    }

    // ── Fill % math integration ──────────────────────────────────────────

    [Fact]
    public void ConduitFillService_CalculateFill_IntegratesWithCircuit()
    {
        var fillService = new ConduitFillService();
        // Circuit with 2 x #12 THHN wires in 1/2" EMT
        var wireSizes = new List<string> { "12", "12" };

        var result = fillService.CalculateFill("1/2", ConduitMaterialType.EMT, wireSizes);

        Assert.True(result.FillPercent > 0);
        Assert.Equal(31.0, result.MaxAllowedFillPercent); // 2 conductors = 31%
        Assert.Equal(2, result.ConductorCount);
    }

    [Fact]
    public void ConduitFillService_ExceedsCode_FlaggedCorrectly()
    {
        var fillService = new ConduitFillService();
        // 6 x #6 wires in 1/2" EMT — should exceed fill
        var wireSizes = new List<string> { "6", "6", "6", "6", "6", "6" };

        var result = fillService.CalculateFill("1/2", ConduitMaterialType.EMT, wireSizes);

        Assert.True(result.ExceedsCode);
    }

    // ── Aggregate length sum verification ────────────────────────────────

    [Fact]
    public void AggregateLengthSumMatchesSegments()
    {
        var store = new ConduitModelStore();
        store.AddType(new ConduitType { Id = "t1" });
        store.Settings.DefaultConduitTypeId = "t1";
        store.Settings.AutoInsertFittings = false;

        var segments = new List<ConduitSegment>
        {
            new() { StartPoint = new XYZ(0, 0, 0), EndPoint = new XYZ(10, 0, 0) },
            new() { StartPoint = new XYZ(10, 0, 0), EndPoint = new XYZ(10, 15, 0) }
        };
        var run = store.CreateRunFromSegments(segments);

        double total = ConduitRunService.GetTotalLength(store, run.Id);
        Assert.Equal(25.0, total, 3); // 10 + 15
    }
}
