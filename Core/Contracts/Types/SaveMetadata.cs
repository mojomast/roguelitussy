using System;

namespace Roguelike.Core;

public sealed record SaveMetadata(
    int SlotIndex,
    int Depth,
    int TurnNumber,
    string PlayerName,
    DateTime SavedAt,
    int Version
);
