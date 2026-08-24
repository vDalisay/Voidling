using System;
using System.Text;

namespace Voidling.Domain.Shared;

/// <summary>
/// Creates deterministic random streams from a persistent seed and a stable semantic salt.
/// Salts are part of the persistence/replay contract: unrelated new random decisions should
/// use new salts rather than consuming values from an existing sequential stream.
/// </summary>
public static class StableRandom
{
    public static Random Create(ulong seed, string salt)
    {
        ArgumentNullException.ThrowIfNull(salt);
        var hash = Derive(seed, salt);
        return new Random(unchecked((int)(hash ^ (hash >> 32))));
    }

    public static ulong Derive(ulong seed, string salt)
    {
        ArgumentNullException.ThrowIfNull(salt);

        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        var hash = offset;
        for (var i = 0; i < sizeof(ulong); i++)
        {
            hash ^= (byte)(seed >> (8 * i));
            hash *= prime;
        }

        foreach (var value in Encoding.UTF8.GetBytes(salt))
        {
            hash ^= value;
            hash *= prime;
        }

        return hash;
    }
}
