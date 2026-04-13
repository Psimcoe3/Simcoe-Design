using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class MotorControlCenterScheduleServiceTests
{
    // ── NEMA Sizing ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(1.0,  4.0,  MotorControlCenterScheduleService.NemaSize.Size00)]
    [InlineData(3.0,  9.6,  MotorControlCenterScheduleService.NemaSize.Size0)]
    [InlineData(7.5,  22.0, MotorControlCenterScheduleService.NemaSize.Size1)]
    [InlineData(15.0, 42.0, MotorControlCenterScheduleService.NemaSize.Size2)]
    [InlineData(30.0, 68.0, MotorControlCenterScheduleService.NemaSize.Size3)]
    [InlineData(50.0, 130.0, MotorControlCenterScheduleService.NemaSize.Size4)]
    [InlineData(100.0, 248.0, MotorControlCenterScheduleService.NemaSize.Size5)]
    public void NemaSize_CorrectForHP(double hp, double fla, MotorControlCenterScheduleService.NemaSize expected)
    {
        var result = MotorControlCenterScheduleService.SelectNemaSize(fla, hp);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NemaSize_VeryLargeMotor_Size5()
    {
        var result = MotorControlCenterScheduleService.SelectNemaSize(500, 200);
        Assert.Equal(MotorControlCenterScheduleService.NemaSize.Size5, result);
    }

    // ── MCP Sizing ───────────────────────────────────────────────────────────

    [Fact]
    public void MCP_SmallMotor()
    {
        int trip = MotorControlCenterScheduleService.SizeMCP(4.0);
        // 4 × 8 = 32 → next standard ≥ 32 is 50
        Assert.Equal(50, trip);
    }

    [Fact]
    public void MCP_MediumMotor()
    {
        int trip = MotorControlCenterScheduleService.SizeMCP(12.0);
        // 12 × 8 = 96 → next standard ≥ 96 is 100
        Assert.Equal(100, trip);
    }

    [Fact]
    public void MCP_LargeMotor()
    {
        int trip = MotorControlCenterScheduleService.SizeMCP(65.0);
        // 65 × 8 = 520 → next standard ≥ 520 is 600
        Assert.Equal(600, trip);
    }

    // ── Overload Trip ────────────────────────────────────────────────────────

    [Fact]
    public void OverloadTrip_SF115_125Percent()
    {
        double trip = MotorControlCenterScheduleService.CalculateOverloadTrip(10.0, 1.15);
        Assert.Equal(12.5, trip);
    }

    [Fact]
    public void OverloadTrip_SF100_115Percent()
    {
        double trip = MotorControlCenterScheduleService.CalculateOverloadTrip(10.0, 1.0);
        Assert.Equal(11.5, trip);
    }

    // ── Wire Sizing ──────────────────────────────────────────────────────────

    [Fact]
    public void WireSize_SmallMotor()
    {
        string wire = MotorControlCenterScheduleService.SelectWireSize(15);
        Assert.Equal("14 AWG", wire);
    }

    [Fact]
    public void WireSize_MediumMotor()
    {
        string wire = MotorControlCenterScheduleService.SelectWireSize(80);
        Assert.Equal("3 AWG", wire);
    }

    [Fact]
    public void WireSize_LargeMotor()
    {
        string wire = MotorControlCenterScheduleService.SelectWireSize(200);
        Assert.Equal("4/0 AWG", wire);
    }

    // ── Single Bucket ────────────────────────────────────────────────────────

    [Fact]
    public void SizeBucket_10HP()
    {
        var motor = new MotorControlCenterScheduleService.MotorSpec
        {
            Id = "M1", Description = "AHU-1 Supply Fan", HP = 10, FLA = 14.0,
        };
        var bucket = MotorControlCenterScheduleService.SizeBucket(motor);
        Assert.Equal("M1", bucket.MotorId);
        Assert.Equal(10, bucket.HP);
        Assert.True(bucket.McpTripAmps >= 100); // 14*8=112
        Assert.True(bucket.OverloadTripAmps > 14);
        Assert.NotEmpty(bucket.RecommendedWireSize);
        Assert.True(bucket.BucketWatts > 0);
    }

    [Fact]
    public void SizeBucket_VFD_StarterPreserved()
    {
        var motor = new MotorControlCenterScheduleService.MotorSpec
        {
            Id = "M2", HP = 25, FLA = 34.0,
            Starter = MotorControlCenterScheduleService.StarterType.VFD,
        };
        var bucket = MotorControlCenterScheduleService.SizeBucket(motor);
        Assert.Equal(MotorControlCenterScheduleService.StarterType.VFD, bucket.Starter);
    }

    // ── Full Schedule ────────────────────────────────────────────────────────

    [Fact]
    public void GenerateSchedule_ThreeMotors()
    {
        var motors = new[]
        {
            new MotorControlCenterScheduleService.MotorSpec { Id = "M1", HP = 5, FLA = 7.6, Description = "Pump-1" },
            new MotorControlCenterScheduleService.MotorSpec { Id = "M2", HP = 10, FLA = 14.0, Description = "Fan-1" },
            new MotorControlCenterScheduleService.MotorSpec { Id = "M3", HP = 25, FLA = 34.0, Description = "Compressor-1" },
        };
        var sched = MotorControlCenterScheduleService.GenerateSchedule("MCC-1", "MCC-1", motors);
        Assert.Equal(3, sched.TotalBuckets);
        Assert.Equal(40, sched.TotalHP);
        Assert.Equal(7.6 + 14.0 + 34.0, sched.TotalFLA, 1);
    }

    [Fact]
    public void GenerateSchedule_BucketIdsSequential()
    {
        var motors = new[]
        {
            new MotorControlCenterScheduleService.MotorSpec { Id = "M1", HP = 5, FLA = 7.6 },
            new MotorControlCenterScheduleService.MotorSpec { Id = "M2", HP = 10, FLA = 14.0 },
        };
        var sched = MotorControlCenterScheduleService.GenerateSchedule("MCC-1", "MCC-1", motors);
        Assert.Equal("B1", sched.Buckets[0].BucketId);
        Assert.Equal("B2", sched.Buckets[1].BucketId);
    }

    [Fact]
    public void GenerateSchedule_BusUtilization()
    {
        var motors = new[]
        {
            new MotorControlCenterScheduleService.MotorSpec { Id = "M1", HP = 50, FLA = 65.0 },
        };
        var sched = MotorControlCenterScheduleService.GenerateSchedule("MCC-1", "MCC-1", motors, 460, 800);
        double expected = 65.0 / 800 * 100;
        Assert.Equal(expected, sched.BusUtilizationPercent, 0);
    }

    [Fact]
    public void GenerateSchedule_Empty()
    {
        var sched = MotorControlCenterScheduleService.GenerateSchedule("MCC-1", "MCC-1",
            new List<MotorControlCenterScheduleService.MotorSpec>());
        Assert.Equal(0, sched.TotalBuckets);
        Assert.Equal(0, sched.BusUtilizationPercent);
    }

    // ── Feeder Ampacity ──────────────────────────────────────────────────────

    [Fact]
    public void FeederAmpacity_NEC43024()
    {
        var motors = new[]
        {
            new MotorControlCenterScheduleService.MotorSpec { Id = "M1", FLA = 10 },
            new MotorControlCenterScheduleService.MotorSpec { Id = "M2", FLA = 20 },
            new MotorControlCenterScheduleService.MotorSpec { Id = "M3", FLA = 50 },
        };
        // Sum = 80, largest = 50, feeder = 80 + 0.25*50 = 92.5
        double feeder = MotorControlCenterScheduleService.CalculateFeederAmpacity(motors);
        Assert.Equal(92.5, feeder);
    }

    [Fact]
    public void FeederAmpacity_Empty_ReturnsZero()
    {
        double feeder = MotorControlCenterScheduleService.CalculateFeederAmpacity(
            new List<MotorControlCenterScheduleService.MotorSpec>());
        Assert.Equal(0, feeder);
    }

    [Fact]
    public void FeederAmpacity_SingleMotor()
    {
        var motors = new[]
        {
            new MotorControlCenterScheduleService.MotorSpec { Id = "M1", FLA = 34 },
        };
        // 34 + 0.25*34 = 42.5
        double feeder = MotorControlCenterScheduleService.CalculateFeederAmpacity(motors);
        Assert.Equal(42.5, feeder);
    }

    // ── TotalWatts ───────────────────────────────────────────────────────────

    [Fact]
    public void TotalWatts_MatchesBucketSum()
    {
        var motors = new[]
        {
            new MotorControlCenterScheduleService.MotorSpec { Id = "M1", HP = 10, FLA = 14 },
            new MotorControlCenterScheduleService.MotorSpec { Id = "M2", HP = 25, FLA = 34 },
        };
        var sched = MotorControlCenterScheduleService.GenerateSchedule("MCC-1", "MCC-1", motors);
        Assert.Equal(sched.Buckets.Sum(b => b.BucketWatts), sched.TotalWatts);
    }
}
