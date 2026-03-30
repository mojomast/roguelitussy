namespace Roguelike.Core;

public sealed record RoomData(
    int X,
    int Y,
    int Width,
    int Height,
    Position Center);
