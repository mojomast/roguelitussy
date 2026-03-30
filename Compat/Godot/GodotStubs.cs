using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Godot;

public enum Key
{
    None,
    Up,
    Down,
    Left,
    Right,
    Enter,
    Escape,
    Space,
    Period,
    Tab,
    Backquote,
    Plus,
    Minus,
    W,
    A,
    S,
    D,
    Q,
    E,
    G,
    I,
    U,
    C,
    One,
    Two,
    Three,
}

public enum MouseButton
{
    None = 0,
    Left = 1,
    Right = 2,
    Middle = 3,
}

[AttributeUsage(AttributeTargets.Delegate)]
public sealed class SignalAttribute : Attribute
{
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public sealed class ExportAttribute : Attribute
{
}

public class GodotObject
{
}

public class Node : GodotObject
{
    private readonly List<Node> _children = new();
    private static readonly Viewport SharedViewport = new();
    private static readonly SceneTree SharedTree = new();

    public string Name { get; set; } = string.Empty;

    public Node? Parent { get; private set; }

    public bool IsQueuedForDeletion { get; private set; }

    public IReadOnlyList<Node> Children => _children;

    public virtual void _Ready()
    {
    }

    public virtual void _UnhandledInput(InputEvent @event)
    {
    }

    public virtual void AddChild(Node child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    public virtual void RemoveChild(Node child)
    {
        if (_children.Remove(child))
        {
            child.Parent = null;
        }
    }

    public virtual void QueueFree()
    {
        IsQueuedForDeletion = true;
        Parent?.RemoveChild(this);
    }

    public Viewport GetViewport() => SharedViewport;

    public SceneTree GetTree() => SharedTree;

    public Rect2 GetViewportRect() => new(Vector2.Zero, SharedViewport.Size);

    protected T GetNode<T>(string path) where T : Node, new()
    {
        return ResolvePath(path) as T ?? new T();
    }

    protected T? GetNodeOrNull<T>(string path) where T : Node
    {
        return ResolvePath(path) as T;
    }

    private Node? ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith("/root/", StringComparison.Ordinal))
        {
            return null;
        }

        var current = this;
        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current._children.FirstOrDefault(child => string.Equals(child.Name, segment, StringComparison.Ordinal));
            if (current is null)
            {
                return null;
            }
        }

        return current;
    }
}

public class Node2D : Node
{
    public Vector2 Position { get; set; }

    public bool Visible { get; set; } = true;

    public Color Modulate { get; set; } = Colors.White;

    public int ZIndex { get; set; }
}

public class CanvasLayer : Node
{
}

public class Control : Node
{
    public Vector2 Position { get; set; }

    public Vector2 Size { get; set; } = new(200f, 80f);

    public Vector2 CustomMinimumSize { get; set; }

    public bool Visible { get; set; } = true;

    public Color Modulate { get; set; } = Colors.White;

    public virtual void _GuiInput(InputEvent @event)
    {
    }

    public virtual void _Draw()
    {
    }

    public void QueueRedraw()
    {
    }

    protected void DrawRect(Rect2 rect, Color color, bool filled = true)
    {
    }
}

public class Viewport : Node
{
    public Vector2 Size { get; set; } = new(1280f, 720f);

    public bool InputHandled { get; private set; }

    public void SetInputAsHandled()
    {
        InputHandled = true;
    }

    public void ResetInputHandled()
    {
        InputHandled = false;
    }
}

public class SceneTree : Node
{
    public bool QuitRequested { get; private set; }

    public void Quit()
    {
        QuitRequested = true;
    }
}

public class Panel : Control
{
}

public class HBoxContainer : Control
{
}

public class VBoxContainer : Control
{
}

public class GridContainer : Control
{
    public int Columns { get; set; }
}

public class Label : Control
{
    public string Text { get; set; } = string.Empty;
}

public class Button : Control
{
    public string Text { get; set; } = string.Empty;
}

public class CheckBox : Button
{
    public bool ButtonPressed { get; set; }
}

public class LineEdit : Control
{
    public string Text { get; set; } = string.Empty;

    public int CaretColumn { get; set; }
}

public class TextEdit : Control
{
    public string Text { get; set; } = string.Empty;
}

public class ItemList : Control
{
    private readonly List<string> _items = new();

    public int ItemCount => _items.Count;

    public void AddItem(string text)
    {
        _items.Add(text);
    }

    public void Clear()
    {
        _items.Clear();
    }

    public string GetItemText(int index) => index >= 0 && index < _items.Count ? _items[index] : string.Empty;
}

public class OptionButton : Control
{
    private readonly List<string> _items = new();

    public int Selected { get; set; }

    public void AddItem(string text)
    {
        _items.Add(text);
    }

    public void Clear()
    {
        _items.Clear();
        Selected = 0;
    }

    public string GetItemText(int index) => index >= 0 && index < _items.Count ? _items[index] : string.Empty;
}

public class SpinBox : Control
{
    public double MinValue { get; set; }

    public double MaxValue { get; set; } = 100d;

    public double Step { get; set; } = 1d;

    public double Value { get; set; }
}

public class ProgressBar : Control
{
    public double MinValue { get; set; }

    public double MaxValue { get; set; } = 100d;

    public double Value { get; set; }
}

public class RichTextLabel : Control
{
    public bool BbcodeEnabled { get; set; }

    public bool ScrollFollowing { get; set; }

    public string Text { get; private set; } = string.Empty;

    public void Clear()
    {
        Text = string.Empty;
    }

    public void AppendText(string text)
    {
        Text += text;
    }
}

public class ColorRect : Control
{
    public Color Color { get; set; } = Colors.Transparent;
}

public class TextureRect : Control
{
    public enum StretchModeEnum
    {
        Scale,
        KeepAspectCentered,
    }

    public object? Texture { get; set; }

    public StretchModeEnum StretchMode { get; set; }
}

public class TextureButton : Control
{
    public bool ButtonPressed { get; set; }
}

public class InputEvent : GodotObject
{
    public virtual bool IsActionPressed(string action) => false;
}

public class InputEventKey : InputEvent
{
    public bool Pressed { get; set; }

    public Key Keycode { get; set; }

    public Key PhysicalKeycode { get; set; }
}

public class InputEventMouseButton : InputEvent
{
    public bool Pressed { get; set; }

    public MouseButton ButtonIndex { get; set; }

    public Vector2 Position { get; set; }
}

public class InputEventMouseMotion : InputEvent
{
    public Vector2 Position { get; set; }
}

public static class Input
{
    private static readonly HashSet<MouseButton> PressedButtons = new();

    public static bool IsMouseButtonPressed(MouseButton button) => PressedButtons.Contains(button);

    public static void SetMouseButtonPressed(MouseButton button, bool pressed)
    {
        if (pressed)
        {
            PressedButtons.Add(button);
            return;
        }

        PressedButtons.Remove(button);
    }
}

public class Camera2D : Node2D
{
    public bool Enabled { get; set; }

    public bool PositionSmoothingEnabled { get; set; }

    public float PositionSmoothingSpeed { get; set; }

    public Vector2 Zoom { get; set; } = new(1f, 1f);
}

public class TileMapLayer : Node2D
{
    private readonly Dictionary<Vector2I, TileCell> _cells = new();

    public void Clear()
    {
        _cells.Clear();
    }

    public void SetCell(Vector2I coords, int sourceId, Vector2I atlasCoords)
    {
        _cells[coords] = new TileCell(sourceId, atlasCoords);
    }

    public void EraseCell(Vector2I coords)
    {
        _cells.Remove(coords);
    }

    public bool TryGetCell(Vector2I coords, out TileCell cell)
    {
        return _cells.TryGetValue(coords, out cell);
    }

    public IReadOnlyDictionary<Vector2I, TileCell> GetUsedCells() => _cells;
}

public class EditorPlugin : Node
{
    private readonly List<Control> _bottomPanelControls = new();

    public IReadOnlyList<Control> BottomPanelControls => _bottomPanelControls;

    public virtual void _EnterTree()
    {
    }

    public virtual void _ExitTree()
    {
    }

    protected void AddControlToBottomPanel(Control control, string title)
    {
        if (!_bottomPanelControls.Contains(control))
        {
            _bottomPanelControls.Add(control);
        }
    }

    protected void RemoveControlFromBottomPanel(Control control)
    {
        _bottomPanelControls.Remove(control);
    }
}

public static class ProjectSettings
{
    public static string GlobalizePath(string path)
    {
        if (path.StartsWith("res://", StringComparison.Ordinal))
        {
            var relativePath = path[6..].Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        return Path.GetFullPath(path);
    }
}

public readonly struct Vector2I
{
    public Vector2I(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override bool Equals(object? obj) => obj is Vector2I other && other.X == X && other.Y == Y;
}

public readonly struct Vector2
{
    public Vector2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public float X { get; }

    public float Y { get; }

    public static Vector2 Zero => new(0f, 0f);

    public static Vector2 operator +(Vector2 left, Vector2 right) => new(left.X + right.X, left.Y + right.Y);

    public static Vector2 operator -(Vector2 left, Vector2 right) => new(left.X - right.X, left.Y - right.Y);

    public static Vector2 operator *(Vector2 left, float scalar) => new(left.X * scalar, left.Y * scalar);

    public override bool Equals(object? obj) => obj is Vector2 other && other.X.Equals(X) && other.Y.Equals(Y);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"({X}, {Y})";
}

public readonly struct Rect2
{
    public Rect2(Vector2 position, Vector2 size)
    {
        Position = position;
        Size = size;
    }

    public Vector2 Position { get; }

    public Vector2 Size { get; }
}

public readonly struct Color
{
    public Color(float r, float g, float b, float a = 1f)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public float R { get; }

    public float G { get; }

    public float B { get; }

    public float A { get; }
}

public static class Colors
{
    public static Color White => new(1f, 1f, 1f, 1f);
    public static Color Black => new(0f, 0f, 0f, 1f);
    public static Color Red => new(1f, 0f, 0f, 1f);
    public static Color Green => new(0.2f, 0.8f, 0.25f, 1f);
    public static Color Yellow => new(0.95f, 0.8f, 0.2f, 1f);
    public static Color Orange => new(0.95f, 0.55f, 0.2f, 1f);
    public static Color Gray => new(0.6f, 0.6f, 0.6f, 1f);
    public static Color Cyan => new(0.2f, 0.8f, 0.9f, 1f);
    public static Color Transparent => new(0f, 0f, 0f, 0f);
}

public class Sprite2D : Node2D
{
    public object? Texture { get; set; }
}

public readonly record struct TileCell(int SourceId, Vector2I AtlasCoords);
