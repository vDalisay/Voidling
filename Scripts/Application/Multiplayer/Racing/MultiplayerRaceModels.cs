using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Voidling.Application.Multiplayer.Challenges;
using Voidling.Application.Ports.Multiplayer;
using Voidling.Application.Racing;
using Voidling.Domain.Racing;
using Voidling.Domain.Rules;
using VoidlingGame;

namespace Voidling.Application.Multiplayer.Racing;

public sealed record MultiplayerRaceEntrant(
    PlatformUserId OwnerId,
    string OwnedCreatureId,
    RaceParticipantSnapshot Participant,
    bool HasAngelMutation,
    int OtherMutationCount);

public sealed record MultiplayerRaceStartPayload(
    int StartVersion,
    string ChallengeId,
    string CourseId,
    int CourseVersion,
    string CourseHash,
    string RaceRulesHash,
    ulong SimulationSeed,
    MultiplayerRaceEntrant[] Entrants);

public sealed record ResolvedMultiplayerRace(
    MultiplayerRaceStartPayload Start,
    RaceCourse Course,
    RaceEntry Entry);

public sealed record MultiplayerRaceOperationResult(bool Success, string? Error)
{
    public static MultiplayerRaceOperationResult Succeeded { get; } = new(true, null);
    public static MultiplayerRaceOperationResult Failed(string error) => new(false, error);
}

/// <summary>
/// Creates the immutable race participant selected from local ownership. The network participant ID
/// is namespaced by Steam identity so two peers cannot collide by using the same local creature ID.
/// </summary>
public sealed class MultiplayerRaceSelectionFactory
{
    private readonly RaceParticipantSnapshotFactory _snapshots;

    public MultiplayerRaceSelectionFactory(GameBalanceRules rules)
        => _snapshots = new RaceParticipantSnapshotFactory(
            rules ?? throw new ArgumentNullException(nameof(rules)));

    public bool TryCreate(
        GameStateData state,
        PlatformUserId ownerId,
        string creatureId,
        out MultiplayerRaceEntrant entrant,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(state);
        entrant = default!;
        error = null;

        if (ownerId.Value == 0)
        {
            error = "Race participant owner is invalid.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(creatureId))
        {
            error = "Select a locally owned Voidling for the race.";
            return false;
        }

        var creature = state.Voidlings.FirstOrDefault(value =>
            string.Equals(value.Id, creatureId, StringComparison.Ordinal));
        if (creature == null)
        {
            error = "Selected race Voidling is not locally owned.";
            return false;
        }

        var snapshot = _snapshots.Create(creature) with
        {
            CreatureId = MultiplayerRaceValidation.BuildParticipantId(ownerId, creature.Id)
        };
        var hasAngel = creature.RareTraits?.Any(trait =>
            string.Equals(trait.TraitId, "Angel", StringComparison.OrdinalIgnoreCase)) == true;
        var otherMutations = creature.RareTraits?.Count(trait =>
            !string.Equals(trait.TraitId, "Angel", StringComparison.OrdinalIgnoreCase)) ?? 0;

        entrant = new MultiplayerRaceEntrant(
            ownerId,
            creature.Id,
            snapshot,
            hasAngel,
            otherMutations);
        return true;
    }
}

/// <summary>
/// Resolves a received start payload against the local build. A peer never silently races with
/// different course/rule constants: fingerprints must match before RaceSimulation is constructed.
/// </summary>
public sealed class MultiplayerRaceEntryFactory
{
    public const string DemoCourseId = "demo";
    public const int DemoCourseVersion = 1;
    public const int CurrentStartVersion = 1;

    private readonly RaceRules _rules;
    private readonly RaceCourse _course;
    private readonly string _rulesHash;
    private readonly string _courseHash;

    public MultiplayerRaceEntryFactory(GameBalanceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        _rules = rules.Racing;
        _course = RaceCourse.Demo;
        _rulesHash = RaceRulesFingerprint.Compute(_rules);
        _courseHash = RaceCourseFingerprint.Compute(_course);
    }

    public string LocalRulesHash => _rulesHash;
    public string LocalCourseHash => _courseHash;

    public MultiplayerRaceStartPayload CreateStartPayload(
        string challengeId,
        IReadOnlyCollection<MultiplayerRaceEntrant> entrants)
    {
        if (!MultiplayerRaceValidation.IsValidChallengeId(challengeId))
            throw new ArgumentException("Challenge ID is invalid.", nameof(challengeId));
        ArgumentNullException.ThrowIfNull(entrants);

        var ordered = entrants
            .OrderBy(entrant => entrant.OwnerId.Value)
            .ToArray();
        if (!MultiplayerRaceValidation.IsValidEntrants(ordered, out var error))
            throw new ArgumentException(error, nameof(entrants));

        return new MultiplayerRaceStartPayload(
            CurrentStartVersion,
            challengeId,
            DemoCourseId,
            DemoCourseVersion,
            _courseHash,
            _rulesHash,
            StableRaceSeed.FromChallengeId(challengeId),
            ordered);
    }

    public bool TryResolve(
        MultiplayerRaceStartPayload payload,
        out ResolvedMultiplayerRace race,
        out string? error)
    {
        race = default!;
        error = null;
        if (!MultiplayerRaceValidation.IsValidStartPayload(payload, out error))
            return false;
        if (payload.StartVersion != CurrentStartVersion)
        {
            error = $"Unsupported multiplayer race start version {payload.StartVersion}.";
            return false;
        }
        if (!string.Equals(payload.CourseId, DemoCourseId, StringComparison.Ordinal) ||
            payload.CourseVersion != DemoCourseVersion ||
            !string.Equals(payload.CourseHash, _courseHash, StringComparison.Ordinal))
        {
            error = "Multiplayer race course does not match this game build.";
            return false;
        }
        if (!string.Equals(payload.RaceRulesHash, _rulesHash, StringComparison.Ordinal))
        {
            error = "Multiplayer race rules do not match this game build.";
            return false;
        }
        if (payload.SimulationSeed != StableRaceSeed.FromChallengeId(payload.ChallengeId))
        {
            error = "Multiplayer race seed does not match the canonical challenge seed.";
            return false;
        }

        var entrants = payload.Entrants
            .Select(value => new RaceEntrant(
                value.Participant,
                value.HasAngelMutation,
                value.OtherMutationCount))
            .ToArray();
        race = new ResolvedMultiplayerRace(
            payload,
            _course,
            new RaceEntry(payload.SimulationSeed, _rules, Array.AsReadOnly(entrants)));
        return true;
    }
}

public static class MultiplayerRaceStartCodec
{
    public static byte[] Encode(MultiplayerRaceStartPayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);
        return JsonSerializer.SerializeToUtf8Bytes(payload);
    }

    public static bool TryDecode(
        ReadOnlySpan<byte> bytes,
        out MultiplayerRaceStartPayload payload,
        out string? error)
    {
        payload = default!;
        error = null;
        if (bytes.Length == 0 || bytes.Length > ChallengeValidation.MaxStartPayloadBytes)
        {
            error = "Multiplayer race start payload has an invalid size.";
            return false;
        }

        try
        {
            var decoded = JsonSerializer.Deserialize<MultiplayerRaceStartPayload>(bytes);
            if (!MultiplayerRaceValidation.IsValidStartPayload(decoded, out error))
                return false;
            payload = decoded!;
            return true;
        }
        catch (JsonException)
        {
            error = "Multiplayer race start payload is malformed.";
            return false;
        }
        catch (NotSupportedException)
        {
            error = "Multiplayer race start payload uses unsupported data.";
            return false;
        }
    }

    public static string ComputeHash(ReadOnlySpan<byte> bytes)
        => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

public static class MultiplayerRaceValidation
{
    public const int MaxCreatureIdLength = 128;
    public const int MaxDisplayNameLength = 64;
    public const int MaxTintLength = 16;

    public static string BuildParticipantId(PlatformUserId ownerId, string ownedCreatureId)
        => $"{ownerId.Value}:{ownedCreatureId}";

    public static bool IsValidStartPayload(
        MultiplayerRaceStartPayload? payload,
        out string? error)
    {
        error = null;
        if (payload == null ||
            payload.StartVersion < 1 ||
            !IsValidChallengeId(payload.ChallengeId) ||
            string.IsNullOrWhiteSpace(payload.CourseId) || payload.CourseId.Length > 64 ||
            payload.CourseVersion < 1 ||
            !IsSha256(payload.CourseHash) ||
            !IsSha256(payload.RaceRulesHash) ||
            payload.SimulationSeed == 0)
        {
            error = "Multiplayer race start metadata is invalid.";
            return false;
        }

        return IsValidEntrants(payload.Entrants, out error);
    }

    public static bool IsValidEntrants(
        IEnumerable<MultiplayerRaceEntrant>? entrants,
        out string? error)
    {
        error = null;
        if (entrants == null)
        {
            error = "Multiplayer race entrants are missing.";
            return false;
        }

        var array = entrants.ToArray();
        if (array.Length is < 2 or > ChallengeValidation.MaxParticipants)
        {
            error = "Multiplayer races require between 2 and 4 participants.";
            return false;
        }
        if (array.Select(value => value.OwnerId).Distinct().Count() != array.Length)
        {
            error = "Each multiplayer race entrant must belong to a different lobby participant.";
            return false;
        }

        var participantIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entrant in array)
        {
            if (!IsValidEntrant(entrant, out error) ||
                !participantIds.Add(entrant.Participant.CreatureId))
            {
                error ??= "Multiplayer race participant IDs must be unique.";
                return false;
            }
        }

        return true;
    }

    public static bool IsValidEntrant(MultiplayerRaceEntrant? entrant, out string? error)
    {
        error = null;
        if (entrant == null ||
            entrant.OwnerId.Value == 0 ||
            string.IsNullOrWhiteSpace(entrant.OwnedCreatureId) ||
            entrant.OwnedCreatureId.Length > MaxCreatureIdLength ||
            entrant.Participant == null ||
            entrant.OtherMutationCount < 0)
        {
            error = "Multiplayer race entrant metadata is invalid.";
            return false;
        }

        var participant = entrant.Participant;
        if (!string.Equals(
                participant.CreatureId,
                BuildParticipantId(entrant.OwnerId, entrant.OwnedCreatureId),
                StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(participant.DisplayName) ||
            participant.DisplayName.Length > MaxDisplayNameLength ||
            string.IsNullOrWhiteSpace(participant.TintHex) ||
            participant.TintHex.Length > MaxTintLength ||
            !IsFiniteNonNegative(participant.Run) ||
            !IsFiniteNonNegative(participant.Swim) ||
            !IsFiniteNonNegative(participant.Fly) ||
            !IsFiniteNonNegative(participant.Power) ||
            !IsFiniteNonNegative(participant.Stamina))
        {
            error = "Multiplayer race entrant snapshot is invalid.";
            return false;
        }

        return true;
    }

    public static bool IsValidChallengeId(string challengeId)
        => !string.IsNullOrWhiteSpace(challengeId) && Guid.TryParse(challengeId, out _);

    private static bool IsFiniteNonNegative(float value)
        => float.IsFinite(value) && value >= 0.0f;

    private static bool IsSha256(string value)
        => value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public static class StableRaceSeed
{
    public static ulong FromChallengeId(string challengeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(challengeId);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"voidling:multiplayer-race:v1:{challengeId}"));
        var seed = BinaryPrimitives.ReadUInt64LittleEndian(hash.AsSpan(0, sizeof(ulong)));
        return seed == 0 ? 1UL : seed;
    }
}

public static class RaceRulesFingerprint
{
    public static string Compute(RaceRules rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        var builder = new StringBuilder("race-rules:v1");
        Append(builder,
            rules.BaseStamina,
            rules.StaminaPerPoint,
            rules.BaseStaminaDrainPerSecond,
            rules.GroundBaseSpeed,
            rules.GroundRunSpeedScale,
            rules.SwimBaseSpeed,
            rules.SwimSpeedScale,
            rules.SwimExtraDrain,
            rules.GlideBaseSpeed,
            rules.GlideSpeedScale,
            rules.GlideExtraDrain,
            rules.FailedGlideSwimBaseSpeed,
            rules.FailedGlideSwimSpeedScale,
            rules.FailedGlideSwimExtraDrain,
            rules.LowStaminaThreshold,
            rules.LowStaminaSpeedMultiplier,
            rules.ExhaustedSpeedMultiplier,
            rules.CheerDurationSeconds,
            rules.CheerCost,
            rules.CheerSpeedMultiplier,
            rules.GlideBaseDistance,
            rules.GlideDistancePerFlyPoint,
            rules.ObstacleAvoidBaseChance,
            rules.ObstacleAvoidRunScale,
            rules.ObstacleAvoidMaxChance,
            rules.ObstacleBaseDelaySeconds,
            rules.ObstacleLowRunDelaySeconds,
            rules.ObstacleRollbackDistance);
        builder.Append('|');
        foreach (var reward in rules.PlacementRewards)
            builder.Append(reward).Append(',');
        return Hash(builder.ToString());
    }

    private static void Append(StringBuilder builder, params float[] values)
    {
        foreach (var value in values)
            builder.Append('|').Append(value.ToString("R", CultureInfo.InvariantCulture));
    }

    private static string Hash(string text)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}

public static class RaceCourseFingerprint
{
    public static string Compute(RaceCourse course)
    {
        ArgumentNullException.ThrowIfNull(course);
        var builder = new StringBuilder("race-course:v1");
        Append(builder, course.StartX, course.EndX, course.GlideLaunchStartX);
        foreach (var segment in course.Segments)
        {
            builder.Append('|').Append(segment.Id)
                .Append(':').Append((int)segment.Kind);
            Append(builder, segment.StartX, segment.EndX);
        }
        builder.Append("|obstacles");
        foreach (var obstacle in course.Obstacles)
            Append(builder, obstacle);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))
            .ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, params float[] values)
    {
        foreach (var value in values)
            builder.Append('|').Append(value.ToString("R", CultureInfo.InvariantCulture));
    }
}
