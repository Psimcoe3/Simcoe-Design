using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Volt-VAR mode selection and reactive-support dispatch for inverter or dynamic VAR resources.
/// </summary>
public static class VoltVarControlService
{
    public enum VoltVarMode
    {
        Hold,
        InjectReactive,
        AbsorbReactive,
    }

    public record ReactiveDevice
    {
        public string Id { get; init; } = "";
        public double MaxInjectKvar { get; init; }
        public double MaxAbsorbKvar { get; init; }
        public int Priority { get; init; }
        public bool IsAvailable { get; init; } = true;
    }

    public record ReactiveAllocation
    {
        public string Id { get; init; } = "";
        public double AssignedKvar { get; init; }
        public bool AtLimit { get; init; }
    }

    public record VoltVarPlan
    {
        public VoltVarMode Mode { get; init; }
        public double RequiredKvar { get; init; }
        public double AssignedKvar { get; init; }
        public double EstimatedVoltageChangePu { get; init; }
        public bool IsAdequate { get; init; }
        public string? Issue { get; init; }
        public List<ReactiveAllocation> Allocations { get; init; } = new();
    }

    public static double CalculateRequiredKvarForPowerFactor(
        double realPowerKW,
        double currentPowerFactor,
        double targetPowerFactor)
    {
        if (realPowerKW < 0)
            throw new ArgumentException("Real power cannot be negative.");
        if (currentPowerFactor <= 0 || currentPowerFactor > 1 || targetPowerFactor <= 0 || targetPowerFactor > 1)
            throw new ArgumentException("Power factors must be greater than 0 and no more than 1.");
        if (targetPowerFactor < currentPowerFactor)
            throw new ArgumentException("Target power factor must not be worse than the current power factor.");

        double currentQ = realPowerKW * Math.Tan(Math.Acos(currentPowerFactor));
        double targetQ = realPowerKW * Math.Tan(Math.Acos(targetPowerFactor));
        return Math.Round(Math.Max(0, currentQ - targetQ), 2);
    }

    public static VoltVarMode DetermineMode(
        double measuredVoltagePu,
        double targetVoltagePu = 1.0,
        double deadbandPu = 0.01)
    {
        if (measuredVoltagePu <= 0 || targetVoltagePu <= 0)
            throw new ArgumentException("Voltages must be positive.");
        if (deadbandPu < 0)
            throw new ArgumentException("Deadband cannot be negative.");

        if (measuredVoltagePu < targetVoltagePu - deadbandPu)
            return VoltVarMode.InjectReactive;
        if (measuredVoltagePu > targetVoltagePu + deadbandPu)
            return VoltVarMode.AbsorbReactive;
        return VoltVarMode.Hold;
    }

    public static double CalculateRequiredKvarForVoltage(
        double measuredVoltagePu,
        double targetVoltagePu = 1.0,
        double sensitivityPuPerMvar = 0.02)
    {
        if (measuredVoltagePu <= 0 || targetVoltagePu <= 0)
            throw new ArgumentException("Voltages must be positive.");
        if (sensitivityPuPerMvar <= 0)
            throw new ArgumentException("Sensitivity must be positive.");

        double voltageError = Math.Abs(targetVoltagePu - measuredVoltagePu);
        return Math.Round(voltageError / sensitivityPuPerMvar * 1000.0, 2);
    }

    public static VoltVarPlan CreateVoltVarPlan(
        IEnumerable<ReactiveDevice> devices,
        double requiredKvar,
        VoltVarMode mode,
        double sensitivityPuPerMvar = 0.02)
    {
        if (requiredKvar < 0)
            throw new ArgumentException("Required kvar cannot be negative.");
        if (sensitivityPuPerMvar <= 0)
            throw new ArgumentException("Sensitivity must be positive.");

        var onlineDevices = devices
            .Where(device => device.IsAvailable)
            .OrderBy(device => device.Priority)
            .ToList();

        if (mode == VoltVarMode.Hold || requiredKvar == 0)
        {
            return new VoltVarPlan
            {
                Mode = VoltVarMode.Hold,
                RequiredKvar = 0,
                AssignedKvar = 0,
                EstimatedVoltageChangePu = 0,
                IsAdequate = true,
            };
        }

        var allocations = new List<ReactiveAllocation>();
        double remaining = requiredKvar;
        double assigned = 0;

        foreach (var device in onlineDevices)
        {
            if (remaining <= 0)
                break;

            double capability = mode == VoltVarMode.InjectReactive ? device.MaxInjectKvar : device.MaxAbsorbKvar;
            double magnitude = Math.Min(remaining, capability);
            double signedKvar = mode == VoltVarMode.InjectReactive ? magnitude : -magnitude;

            allocations.Add(new ReactiveAllocation
            {
                Id = device.Id,
                AssignedKvar = Math.Round(signedKvar, 2),
                AtLimit = Math.Abs(magnitude - capability) < 0.001,
            });

            assigned += magnitude;
            remaining -= magnitude;
        }

        double signedAssigned = mode == VoltVarMode.InjectReactive ? assigned : -assigned;
        double estimatedVoltageChange = signedAssigned / 1000.0 * sensitivityPuPerMvar;

        return new VoltVarPlan
        {
            Mode = mode,
            RequiredKvar = Math.Round(requiredKvar, 2),
            AssignedKvar = Math.Round(signedAssigned, 2),
            EstimatedVoltageChangePu = Math.Round(estimatedVoltageChange, 4),
            IsAdequate = remaining <= 0.001,
            Issue = remaining <= 0.001 ? null : "Available reactive support is below required kvar",
            Allocations = allocations,
        };
    }
}