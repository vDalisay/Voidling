using System;
using Voidling.Domain.Rules;

namespace Voidling.Domain.Racing;

public enum RaceTerrain
{
    Ground,
    Swim,
    Glide,
    FailedGlideSwim,
    Climb
}

public readonly record struct RaceMovement(float Speed, float StaminaDrainPerSecond);

/// <summary>
/// Pure numeric race model extracted from the MVP RaceController. It intentionally contains
/// no track nodes, sprites, animation timing, camera state, or physics callbacks.
/// </summary>
public sealed class RacePerformanceModel
{
    private readonly RaceRules _rules;

    public RacePerformanceModel(RaceRules rules)
    {
        _rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public float GetMaxStamina(RaceParticipantSnapshot participant)
        => _rules.BaseStamina + participant.Stamina * _rules.StaminaPerPoint;

    public RaceMovement GetMovement(
        RaceParticipantSnapshot participant,
        RaceTerrain terrain,
        float currentStamina,
        float maxStamina,
        bool cheerActive)
    {
        var drain = _rules.BaseStaminaDrainPerSecond;
        var speed = terrain switch
        {
            RaceTerrain.Swim => _rules.SwimBaseSpeed + participant.Swim * _rules.SwimSpeedScale,
            RaceTerrain.Glide => _rules.GlideBaseSpeed + participant.Fly * _rules.GlideSpeedScale,
            RaceTerrain.FailedGlideSwim => _rules.FailedGlideSwimBaseSpeed + participant.Swim * _rules.FailedGlideSwimSpeedScale,
            RaceTerrain.Climb => _rules.ClimbBaseSpeed + participant.Power * _rules.ClimbPowerSpeedScale,
            _ => _rules.GroundBaseSpeed + participant.Run * _rules.GroundRunSpeedScale
        };

        drain += terrain switch
        {
            RaceTerrain.Swim => _rules.SwimExtraDrain,
            RaceTerrain.Glide => _rules.GlideExtraDrain,
            RaceTerrain.FailedGlideSwim => _rules.FailedGlideSwimExtraDrain,
            RaceTerrain.Climb => _rules.ClimbExtraDrain,
            _ => 0.0f
        };

        var staminaRatio = maxStamina <= 0.0f ? 0.0f : currentStamina / maxStamina;
        if (staminaRatio < _rules.LowStaminaThreshold)
            speed *= _rules.LowStaminaSpeedMultiplier;
        if (currentStamina <= 0.01f)
            speed *= _rules.ExhaustedSpeedMultiplier;
        if (cheerActive)
            speed *= _rules.CheerSpeedMultiplier;

        return new RaceMovement(speed, drain);
    }

    public float GetDelayStaminaDrainPerSecond()
        => _rules.BaseStaminaDrainPerSecond * 0.35f;

    public float GetGlideDistance(RaceParticipantSnapshot participant)
        => _rules.GlideBaseDistance + Math.Clamp(participant.Fly, 0.0f, 100.0f) * _rules.GlideDistancePerFlyPoint;

    public float GetObstacleAvoidChance(RaceParticipantSnapshot participant)
        => Math.Clamp(
            _rules.ObstacleAvoidBaseChance + participant.Run / 100.0f * _rules.ObstacleAvoidRunScale,
            _rules.ObstacleAvoidBaseChance,
            _rules.ObstacleAvoidMaxChance);

    public bool AvoidsObstacle(RaceParticipantSnapshot participant, double deterministicRoll)
        => deterministicRoll <= GetObstacleAvoidChance(participant);

    public float GetObstacleDelaySeconds(RaceParticipantSnapshot participant)
        => _rules.ObstacleBaseDelaySeconds
           + (100.0f - Math.Clamp(participant.Run, 0.0f, 100.0f)) / 100.0f
           * _rules.ObstacleLowRunDelaySeconds;

    public float ObstacleRollbackDistance => _rules.ObstacleRollbackDistance;
    public float CheerCost => _rules.CheerCost;
    public float CheerDurationSeconds => _rules.CheerDurationSeconds;
}
