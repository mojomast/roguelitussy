using Godot;

namespace Roguelike.Godot;

public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    public override void _Ready()
    {
        Instance = this;
        GD.Print("GameManager initialized.");
    }
}
