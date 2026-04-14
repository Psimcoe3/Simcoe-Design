using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Distribution recloser sequence planning and fuse-saving / fuse-blowing coordination checks.
/// </summary>
public static class RecloserCoordinationService
{
    public enum RecloserMode
    {
        FuseSaving,
        FuseBlowing,
    }

        public record RecloserSettings
    {
        public double PickupAmps { get; init; }
        public RecloserMode Mode { get; init; }
        public int FastShots { get; init; }
        public int SlowShots { get; init; }
        public double FastCurveMultiplier { get; init; }
        public double SlowCurveMultiplier { get; init; }
        public List<double> RecloseDeadTimesSec { get; init; } = new();
    }

    public record RecloserOperation
    {
        public int ShotNumber { get; init; }
        public bool IsFastShot { get; init; }
        public double TripTimeMs { get; init; }
        public double DeadTimeSec { get; init; }
    }

    public record FuseCoordinationAssessment
    {
        public double FaultCurrentAmps { get; init; }
        public double FuseMeltTimeMs { get; init; }
        public double RecloserTripTimeMs { get; init; }
        public bool IsCoordinated { get; init; }
        public string? Issue { get; init; }
    }

    public static RecloserSettings GetDefaultSettings(RecloserMode mode, double pickupAmps = 200)
    {
        return mode switch
        {
            RecloserMode.FuseBlowing => new RecloserSettings
            {
                PickupAmps = pickupAmps,
                Mode = mode,
                FastShots = 0,
                SlowShots = 3,
                FastCurveMultiplier = 0.2,
                SlowCurveMultiplier = 1.0,
                RecloseDeadTimesSec = new List<double> { 1.0, 5.0, 10.0 },
            },
            _ => new RecloserSettings
            {
                PickupAmps = pickupAmps,
                Mode = mode,
                FastShots = 2,
                SlowShots = 2,
                FastCurveMultiplier = 0.2,
                SlowCurveMultiplier = 1.0,
                RecloseDeadTimesSec = new List<double> { 0.5, 2.0, 5.0 },
            },
        };
    }

    public static double EstimateTripTimeMs(RecloserSettings settings, double faultCurrentAmps, bool fastOperation)
    {
        if (settings.PickupAmps <= 0 || faultCurrentAmps <= 0)
            throw new ArgumentException("Pickup and fault current must be positive.");

        double multiple = faultCurrentAmps / settings.PickupAmps;
        if (multiple <= 1.0)
            return double.MaxValue;

        double multiplier = fastOperation ? settings.FastCurveMultiplier : settings.SlowCurveMultiplier;
            double tripTime = 80.0 * multiplier / ((multiple - 1.0) * (multiple - 1.0));
        return Math.Round(Math.Min(tripTime, 10000.0), 2);
    }

    public static double EstimateFuseMeltTimeMs(double fuseMinMeltAmps, double faultCurrentAmps)
    {
        if (fuseMinMeltAmps <= 0 || faultCurrentAmps <= 0)
            throw new ArgumentException("Fuse and fault current must be positive.");

        double multiple = faultCurrentAmps / fuseMinMeltAmps;
        if (multiple <= 1.0)
            return double.MaxValue;

        double meltTime = 120.0 / ((multiple - 1.0) * (multiple - 1.0));
        return Math.Round(Math.Min(meltTime, 10000.0), 2);
    }

    public static List<RecloserOperation> CreateOperationSequence(RecloserSettings settings, double faultCurrentAmps)
    {
        var operations = new List<RecloserOperation>();
        int totalShots = settings.FastShots + settings.SlowShots;

        for (int index = 0; index < totalShots; index++)
        {
            bool fastShot = index < settings.FastShots;
            double deadTime = index < settings.RecloseDeadTimesSec.Count ? settings.RecloseDeadTimesSec[index] : 0;
            operations.Add(new RecloserOperation
            {
                ShotNumber = index + 1,
                IsFastShot = fastShot,
                TripTimeMs = EstimateTripTimeMs(settings, faultCurrentAmps, fastShot),
                DeadTimeSec = deadTime,
            });
        }

        return operations;
    }

    public static FuseCoordinationAssessment EvaluateFuseCoordination(
        RecloserSettings settings,
        double fuseMinMeltAmps,
        double faultCurrentAmps)
    {
        double fuseMeltTime = EstimateFuseMeltTimeMs(fuseMinMeltAmps, faultCurrentAmps);
        bool fastShot = settings.Mode == RecloserMode.FuseSaving && settings.FastShots > 0;
        double recloserTime = EstimateTripTimeMs(settings, faultCurrentAmps, fastShot);

        bool coordinated = settings.Mode switch
        {
            RecloserMode.FuseSaving => recloserTime < fuseMeltTime,
            _ => recloserTime > fuseMeltTime,
        };

        string? issue = coordinated
            ? null
            : settings.Mode == RecloserMode.FuseSaving
                ? "Fast recloser shot is not clearing before the fuse melts"
                : "Slow recloser shot is too fast to allow fuse operation first";

        return new FuseCoordinationAssessment
        {
            FaultCurrentAmps = Math.Round(faultCurrentAmps, 1),
            FuseMeltTimeMs = fuseMeltTime,
            RecloserTripTimeMs = recloserTime,
            IsCoordinated = coordinated,
            Issue = issue,
        };
    }
}