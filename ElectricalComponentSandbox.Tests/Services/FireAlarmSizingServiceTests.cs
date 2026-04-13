using System.Collections.Generic;
using System.Linq;
using ElectricalComponentSandbox.Services;
using Xunit;

namespace ElectricalComponentSandbox.Tests.Services;

public class FireAlarmSizingServiceTests
{
    private static List<FireAlarmSizingService.FaDevice> SampleDevices() => new()
    {
        new() { Id = "SD-1", Type = FireAlarmSizingService.DeviceType.SmokeDetector,
                SupervisoryCurrentMA = 0.3, AlarmCurrentMA = 5, Circuit = "IDC-1" },
        new() { Id = "SD-2", Type = FireAlarmSizingService.DeviceType.SmokeDetector,
                SupervisoryCurrentMA = 0.3, AlarmCurrentMA = 5, Circuit = "IDC-1" },
        new() { Id = "PS-1", Type = FireAlarmSizingService.DeviceType.PullStation,
                SupervisoryCurrentMA = 0.2, AlarmCurrentMA = 3, Circuit = "IDC-2" },
        new() { Id = "HS-1", Type = FireAlarmSizingService.DeviceType.HornStrobe,
                SupervisoryCurrentMA = 0, AlarmCurrentMA = 115, Circuit = "NAC-1" },
        new() { Id = "HS-2", Type = FireAlarmSizingService.DeviceType.HornStrobe,
                SupervisoryCurrentMA = 0, AlarmCurrentMA = 115, Circuit = "NAC-1" },
        new() { Id = "HS-3", Type = FireAlarmSizingService.DeviceType.HornStrobe,
                SupervisoryCurrentMA = 0, AlarmCurrentMA = 115, Circuit = "NAC-2" },
        new() { Id = "MM-1", Type = FireAlarmSizingService.DeviceType.MonitorModule,
                SupervisoryCurrentMA = 1.0, AlarmCurrentMA = 2, Circuit = "SLC-1" },
    };

    // ── Battery Sizing ───────────────────────────────────────────────────────

    [Fact]
    public void Battery_IncludesPanelCurrent()
    {
        var result = FireAlarmSizingService.CalculateBattery(
            new List<FireAlarmSizingService.FaDevice>(),
            panelSupervisoryMA: 100, panelAlarmMA: 200);
        Assert.Equal(100, result.TotalSupervisoryCurrentMA);
        Assert.Equal(200, result.TotalAlarmCurrentMA);
    }

    [Fact]
    public void Battery_SupervisoryAH_24Hours()
    {
        var result = FireAlarmSizingService.CalculateBattery(
            new List<FireAlarmSizingService.FaDevice>(),
            panelSupervisoryMA: 100, panelAlarmMA: 0);
        // 100mA × 24h = 2.4 AH
        Assert.Equal(2.4, result.SupervisoryAH, 1);
    }

    [Fact]
    public void Battery_AlarmAH_5Minutes()
    {
        var result = FireAlarmSizingService.CalculateBattery(
            new List<FireAlarmSizingService.FaDevice>(),
            panelSupervisoryMA: 0, panelAlarmMA: 1000);
        // 1000mA × 5/60 h = 0.0833 AH
        Assert.Equal(0.08, result.AlarmAH, 1);
    }

    [Fact]
    public void Battery_20PercentSafetyMargin()
    {
        var result = FireAlarmSizingService.CalculateBattery(
            new List<FireAlarmSizingService.FaDevice>(),
            panelSupervisoryMA: 100, panelAlarmMA: 200);
        Assert.True(result.RecommendedBatteryAH > result.TotalRequiredAH);
    }

    [Fact]
    public void Battery_FullSystem()
    {
        var result = FireAlarmSizingService.CalculateBattery(SampleDevices());
        Assert.True(result.TotalSupervisoryCurrentMA > 100); // Panel + devices
        Assert.True(result.TotalAlarmCurrentMA > 200); // Panel + horn strobes
        Assert.True(result.RecommendedBatteryAH > 0);
    }

    [Fact]
    public void Battery_CustomStandbyHours()
    {
        var r24 = FireAlarmSizingService.CalculateBattery(SampleDevices(), supervisoryHours: 24);
        var r60 = FireAlarmSizingService.CalculateBattery(SampleDevices(), supervisoryHours: 60);
        Assert.True(r60.RecommendedBatteryAH > r24.RecommendedBatteryAH);
    }

    // ── NAC Circuit Loading ──────────────────────────────────────────────────

    [Fact]
    public void NacCircuits_GroupedByCircuit()
    {
        var nacs = FireAlarmSizingService.AnalyzeNacCircuits(SampleDevices());
        Assert.Equal(2, nacs.Count); // NAC-1, NAC-2
    }

    [Fact]
    public void NacCircuits_TotalAlarmCurrent()
    {
        var nacs = FireAlarmSizingService.AnalyzeNacCircuits(SampleDevices());
        var nac1 = nacs.First(n => n.CircuitId == "NAC-1");
        Assert.Equal(230, nac1.TotalAlarmCurrentMA); // 2 × 115
        Assert.Equal(2, nac1.DeviceCount);
    }

    [Fact]
    public void NacCircuits_NotOverloaded()
    {
        var nacs = FireAlarmSizingService.AnalyzeNacCircuits(SampleDevices());
        Assert.All(nacs, n => Assert.False(n.Overloaded));
    }

    [Fact]
    public void NacCircuits_OverloadDetection()
    {
        var devices = new List<FireAlarmSizingService.FaDevice>();
        for (int i = 0; i < 30; i++)
        {
            devices.Add(new FireAlarmSizingService.FaDevice
            {
                Id = $"HS-{i}", Type = FireAlarmSizingService.DeviceType.HornStrobe,
                AlarmCurrentMA = 115, Circuit = "NAC-1",
            });
        }
        var nacs = FireAlarmSizingService.AnalyzeNacCircuits(devices, 2500);
        Assert.True(nacs[0].Overloaded); // 30 × 115 = 3450 > 2500
    }

    // ── IDC Circuit Loading ──────────────────────────────────────────────────

    [Fact]
    public void IdcCircuits_GroupedByCircuit()
    {
        var idcs = FireAlarmSizingService.AnalyzeIdcCircuits(SampleDevices());
        Assert.Equal(2, idcs.Count); // IDC-1, IDC-2
    }

    [Fact]
    public void IdcCircuits_DeviceCount()
    {
        var idcs = FireAlarmSizingService.AnalyzeIdcCircuits(SampleDevices());
        var idc1 = idcs.First(i => i.CircuitId == "IDC-1");
        Assert.Equal(2, idc1.DeviceCount);
    }

    [Fact]
    public void IdcCircuits_ExceedsRecommended()
    {
        var devices = new List<FireAlarmSizingService.FaDevice>();
        for (int i = 0; i < 25; i++)
        {
            devices.Add(new FireAlarmSizingService.FaDevice
            {
                Id = $"SD-{i}", Type = FireAlarmSizingService.DeviceType.SmokeDetector,
                SupervisoryCurrentMA = 0.3, AlarmCurrentMA = 5, Circuit = "IDC-1",
            });
        }
        var idcs = FireAlarmSizingService.AnalyzeIdcCircuits(devices, 20);
        Assert.True(idcs[0].ExceedsRecommended);
    }

    // ── System Summary ───────────────────────────────────────────────────────

    [Fact]
    public void SystemSummary_DeviceCounts()
    {
        var summary = FireAlarmSizingService.AnalyzeSystem(SampleDevices());
        Assert.Equal(7, summary.TotalDevices);
        Assert.Equal(3, summary.InitiatingDevices);  // 2 smoke + 1 pull
        Assert.Equal(3, summary.NotificationDevices); // 3 horn strobes
        Assert.Equal(1, summary.ModuleDevices);        // 1 monitor module
    }

    [Fact]
    public void SystemSummary_HasBattery()
    {
        var summary = FireAlarmSizingService.AnalyzeSystem(SampleDevices());
        Assert.True(summary.Battery.RecommendedBatteryAH > 0);
    }

    [Fact]
    public void SystemSummary_NoOverloads()
    {
        var summary = FireAlarmSizingService.AnalyzeSystem(SampleDevices());
        Assert.False(summary.AnyNacOverloaded);
        Assert.False(summary.AnyIdcExceedsRecommended);
    }

    // ── Coverage ─────────────────────────────────────────────────────────────

    [Fact]
    public void Coverage_Smoke_900SqFt()
    {
        double area = FireAlarmSizingService.GetMaxCoverageAreaSqFt(
            FireAlarmSizingService.DeviceType.SmokeDetector);
        Assert.Equal(900, area);
    }

    [Fact]
    public void Coverage_Heat_400SqFt()
    {
        double area = FireAlarmSizingService.GetMaxCoverageAreaSqFt(
            FireAlarmSizingService.DeviceType.HeatDetector);
        Assert.Equal(400, area);
    }

    [Fact]
    public void MinDetectors_Smoke_LargeRoom()
    {
        int count = FireAlarmSizingService.MinimumDetectors(
            FireAlarmSizingService.DeviceType.SmokeDetector, 5000);
        Assert.Equal(6, count); // 5000/900 = 5.56 → 6
    }

    [Fact]
    public void MinDetectors_Heat_SmallRoom()
    {
        int count = FireAlarmSizingService.MinimumDetectors(
            FireAlarmSizingService.DeviceType.HeatDetector, 350);
        Assert.Equal(1, count);
    }

    [Fact]
    public void MinDetectors_NotApplicable_ReturnsZero()
    {
        int count = FireAlarmSizingService.MinimumDetectors(
            FireAlarmSizingService.DeviceType.PullStation, 5000);
        Assert.Equal(0, count);
    }

    // ── Empty System ─────────────────────────────────────────────────────────

    [Fact]
    public void SystemSummary_EmptyDevices()
    {
        var summary = FireAlarmSizingService.AnalyzeSystem(
            new List<FireAlarmSizingService.FaDevice>());
        Assert.Equal(0, summary.TotalDevices);
        Assert.True(summary.Battery.RecommendedBatteryAH > 0); // Panel standby
    }
}
