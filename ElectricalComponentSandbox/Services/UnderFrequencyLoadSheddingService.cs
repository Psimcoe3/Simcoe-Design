using System;
using System.Collections.Generic;
using System.Linq;

namespace ElectricalComponentSandbox.Services;

/// <summary>
/// Under-frequency load shedding staging for bulk-system and campus microgrid response.
/// </summary>
public static class UnderFrequencyLoadSheddingService
{
    public enum FrequencyBase
    {
        Hz50,
        Hz60,
    }

    public record UflsStage
    {
        public int StageNumber { get; init; }
        public double PickupFrequencyHz { get; init; }
        public double ShedPercent { get; init; }
        public int TimeDelayCycles { get; init; }
    }

    public record UflsEvaluation
    {
        public double FrequencyHz { get; init; }
        public double TotalLoadMW { get; init; }
        public double ShedMW { get; init; }
        public double RemainingLoadMW { get; init; }
        public bool EmergencyActive { get; init; }
        public List<UflsStage> TriggeredStages { get; init; } = new();
    }

    public record CoordinationCheck
    {
        public bool IsValid { get; init; }
        public double TotalConfiguredShedPercent { get; init; }
        public string? Issue { get; init; }
    }

    public static List<UflsStage> GetDefaultStages(FrequencyBase frequencyBase = FrequencyBase.Hz60)
    {
        return frequencyBase switch
        {
            FrequencyBase.Hz50 =>
            [
                new UflsStage { StageNumber = 1, PickupFrequencyHz = 49.0, ShedPercent = 5, TimeDelayCycles = 12 },
                new UflsStage { StageNumber = 2, PickupFrequencyHz = 48.8, ShedPercent = 10, TimeDelayCycles = 12 },
                new UflsStage { StageNumber = 3, PickupFrequencyHz = 48.6, ShedPercent = 10, TimeDelayCycles = 18 },
                new UflsStage { StageNumber = 4, PickupFrequencyHz = 48.4, ShedPercent = 15, TimeDelayCycles = 24 },
            ],
            _ =>
            [
                new UflsStage { StageNumber = 1, PickupFrequencyHz = 59.3, ShedPercent = 5, TimeDelayCycles = 12 },
                new UflsStage { StageNumber = 2, PickupFrequencyHz = 59.0, ShedPercent = 10, TimeDelayCycles = 12 },
                new UflsStage { StageNumber = 3, PickupFrequencyHz = 58.7, ShedPercent = 10, TimeDelayCycles = 18 },
                new UflsStage { StageNumber = 4, PickupFrequencyHz = 58.4, ShedPercent = 15, TimeDelayCycles = 24 },
            ],
        };
    }

    /// <summary>
    /// Estimates the percentage of load that must be shed to cover an active generation deficit.
    /// </summary>
    public static double EstimateRequiredShedPercent(double totalLoadMW, double generationDeficitMW)
    {
        if (totalLoadMW <= 0)
            throw new ArgumentException("Total load must be positive.");
        if (generationDeficitMW < 0)
            throw new ArgumentException("Generation deficit cannot be negative.");

        return Math.Round(Math.Min(100, generationDeficitMW / totalLoadMW * 100.0), 1);
    }

    public static UflsEvaluation EvaluateFrequency(
        double frequencyHz,
        double totalLoadMW,
        IEnumerable<UflsStage>? stages = null)
    {
        if (frequencyHz <= 0)
            throw new ArgumentException("Frequency must be positive.");
        if (totalLoadMW < 0)
            throw new ArgumentException("Total load cannot be negative.");

        var activeStages = (stages ?? GetDefaultStages())
            .OrderBy(stage => stage.PickupFrequencyHz)
            .Where(stage => frequencyHz <= stage.PickupFrequencyHz)
            .OrderBy(stage => stage.StageNumber)
            .ToList();

        double shedPercent = activeStages.Sum(stage => stage.ShedPercent);
        double shedMW = totalLoadMW * shedPercent / 100.0;
        double remainingMW = Math.Max(0, totalLoadMW - shedMW);

        return new UflsEvaluation
        {
            FrequencyHz = Math.Round(frequencyHz, 3),
            TotalLoadMW = Math.Round(totalLoadMW, 2),
            ShedMW = Math.Round(shedMW, 2),
            RemainingLoadMW = Math.Round(remainingMW, 2),
            EmergencyActive = activeStages.Count > 0,
            TriggeredStages = activeStages,
        };
    }

    public static double GetRestoreFrequency(double pickupFrequencyHz, double hysteresisHz = 0.4)
    {
        if (pickupFrequencyHz <= 0 || hysteresisHz <= 0)
            throw new ArgumentException("Pickup frequency and hysteresis must be positive.");

        return Math.Round(pickupFrequencyHz + hysteresisHz, 3);
    }

    public static CoordinationCheck ValidateStagePlan(IEnumerable<UflsStage> stages)
    {
        var stageList = stages.OrderBy(stage => stage.StageNumber).ToList();
        if (stageList.Count == 0)
        {
            return new CoordinationCheck
            {
                IsValid = false,
                Issue = "At least one UFLS stage is required",
            };
        }

        for (int index = 1; index < stageList.Count; index++)
        {
            if (stageList[index].PickupFrequencyHz >= stageList[index - 1].PickupFrequencyHz)
            {
                return new CoordinationCheck
                {
                    IsValid = false,
                    TotalConfiguredShedPercent = Math.Round(stageList.Sum(stage => stage.ShedPercent), 1),
                    Issue = "Stage pickup frequencies must decrease with each successive stage",
                };
            }
        }

        double totalShed = stageList.Sum(stage => stage.ShedPercent);
        if (totalShed > 70)
        {
            return new CoordinationCheck
            {
                IsValid = false,
                TotalConfiguredShedPercent = Math.Round(totalShed, 1),
                Issue = "Configured UFLS shed exceeds reasonable maximum",
            };
        }

        return new CoordinationCheck
        {
            IsValid = true,
            TotalConfiguredShedPercent = Math.Round(totalShed, 1),
        };
    }
}