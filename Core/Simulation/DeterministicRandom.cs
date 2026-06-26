using System;

namespace Roguelike.Core;

/// <summary>
/// Small serializable PRNG for systems whose random continuation must survive save/load.
/// </summary>
public sealed class DeterministicRandom
{
    private const ulong Multiplier = 6364136223846793005UL;
    private const ulong Increment = 1442695040888963407UL;

    public DeterministicRandom(int seed)
        : this(unchecked((ulong)seed) + 1UL)
    {
    }

    public DeterministicRandom(ulong state)
    {
        State = state == 0UL ? 1UL : state;
    }

    public ulong State { get; private set; }

    public int Next(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        return (int)(NextUInt64() % (uint)maxExclusive);
    }

    public int Next(int minInclusive, int maxExclusive)
    {
        if (minInclusive >= maxExclusive)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive));
        }

        return minInclusive + Next(maxExclusive - minInclusive);
    }

    public ulong Peek()
    {
        var previousState = State;
        State = unchecked((State * Multiplier) + Increment);
        var value = State;
        State = previousState;
        return value;
    }

    private ulong NextUInt64()
    {
        State = unchecked((State * Multiplier) + Increment);
        return State;
    }
}
