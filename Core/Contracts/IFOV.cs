using System;

namespace Roguelike.Core;

public interface IFOV
{
    void Compute(
        Position origin,
        int radius,
        Func<Position, bool> blocksLight,
        Action<Position> markVisible
    );
}
