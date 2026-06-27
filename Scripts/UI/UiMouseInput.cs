using System;
using Godot;

namespace Godotussy;

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
