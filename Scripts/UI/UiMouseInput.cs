using System;
using Godot;

namespace Godotussy;

public static class UiMouseInput
{
    public static void RegisterClickable(Control node, Action onLeftClick, Action? onRightClick = null)
    {
        var baseColor = node.Modulate;
        void Handle(InputEvent input)
        {
            if (input is InputEventMouseMotion)
            {
                node.Modulate = UiStyle.BrightGold();
                return;
            }

            if (input is not InputEventMouseButton { Pressed: true } button)
            {
                return;
            }

            if (button.ButtonIndex == MouseButton.Left)
            {
                node.Modulate = baseColor;
                onLeftClick();
            }
            else if (button.ButtonIndex == MouseButton.Right)
            {
                node.Modulate = baseColor;
                onRightClick?.Invoke();
            }
        }

        switch (node)
        {
            case UiMousePanel panel:
                panel.InputSubmitted = Handle;
                break;
            case UiMouseColorRect colorRect:
                colorRect.InputSubmitted = Handle;
                break;
            case UiMouseLabel label:
                label.InputSubmitted = Handle;
                break;
        }
    }
}

public partial class UiMousePanel : Panel
{
    public Action<InputEvent>? InputSubmitted { get; set; }

    public override void _GuiInput(InputEvent @event)
    {
        InputSubmitted?.Invoke(@event);
    }
}

public partial class UiMouseColorRect : ColorRect
{
    public Action<InputEvent>? InputSubmitted { get; set; }

    public override void _GuiInput(InputEvent @event)
    {
        InputSubmitted?.Invoke(@event);
    }
}

public partial class UiMouseLabel : Label
{
    public Action<InputEvent>? InputSubmitted { get; set; }

    public override void _GuiInput(InputEvent @event)
    {
        InputSubmitted?.Invoke(@event);
    }
}
