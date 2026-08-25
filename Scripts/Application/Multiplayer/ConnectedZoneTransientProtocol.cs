using System;
using Voidling.Application.Ports.Multiplayer;

namespace Voidling.Application.Multiplayer;

public sealed record SharedVoidlingTransform(
    PlatformUserId OwnerId,
    string CreatureId,
    long Sequence,
    float ZoneX,
    float ZoneY,
    float FacingX,
    string AnimationState)
{
    public SharedVoidlingKey Key => new(OwnerId, CreatureId);
}

public static class ConnectedZoneTransientValidation
{
    public const int MaxAnimationStateLength = 32;
    public const float MaxAbsoluteCoordinate = 100_000.0f;

    public static bool IsValid(SharedVoidlingTransform? transform)
        => transform != null &&
           transform.OwnerId.Value > 0 &&
           !string.IsNullOrWhiteSpace(transform.CreatureId) &&
           transform.CreatureId.Length <= ConnectedZoneValidation.MaxCreatureIdLength &&
           transform.Sequence > 0 &&
           float.IsFinite(transform.ZoneX) &&
           float.IsFinite(transform.ZoneY) &&
           MathF.Abs(transform.ZoneX) <= MaxAbsoluteCoordinate &&
           MathF.Abs(transform.ZoneY) <= MaxAbsoluteCoordinate &&
           float.IsFinite(transform.FacingX) &&
           transform.FacingX is >= -1.0f and <= 1.0f &&
           transform.AnimationState != null &&
           transform.AnimationState.Length <= MaxAnimationStateLength;
}

public static class ConnectedZoneTransientProtocol
{
    public const string TransformMessageType = "zone.voidling.transform";

    private sealed record TransformPayload(SharedVoidlingTransform Transform);

    public static byte[] EncodeTransform(PlatformUser sender, SharedVoidlingTransform transform)
    {
        ArgumentNullException.ThrowIfNull(transform);
        return MultiplayerProtocol.EncodeMessage(
            TransformMessageType,
            sender,
            new TransformPayload(transform));
    }

    public static bool TryDecodeTransform(
        ReadOnlySpan<byte> bytes,
        PlatformUserId transportSender,
        out Guid messageId,
        out SharedVoidlingTransform transform)
    {
        transform = default!;
        if (!MultiplayerProtocol.TryDecodeMessage(
                bytes,
                transportSender,
                TransformMessageType,
                out messageId,
                out TransformPayload? payload) ||
            payload?.Transform == null ||
            !ConnectedZoneTransientValidation.IsValid(payload.Transform))
        {
            return false;
        }

        transform = payload.Transform;
        return true;
    }
}
