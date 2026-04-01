using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class DebugConsole : Control
{
    private readonly List<string> _history = new();
    private readonly List<string> _lines = new();
    private int _historyCursor;
    private EventBus? _eventBus;

    public DebugConsole()
    {
        Name = "DebugConsole";
        Visible = false;
        PendingInput = string.Empty;
        Processor = new DebugCommandProcessor();
        CustomMinimumSize = new Vector2(900f, 320f);
    }

    public DebugCommandProcessor Processor { get; }

    public string PendingInput { get; set; }

    public string RenderedText => string.Join("\n", _lines);

    public IReadOnlyList<string> History => _history;

    public void Bind(GameManager? gameManager, EventBus? eventBus, IContentDatabase? content)
    {
        if (_eventBus is not null)
        {
            _eventBus.LogMessage -= OnLogMessage;
        }

        _eventBus = eventBus;
        Processor.Bind(gameManager, eventBus, content);
        if (_eventBus is not null)
        {
            _eventBus.LogMessage += OnLogMessage;
        }
    }

    public void Open()
    {
        Visible = true;
        _historyCursor = _history.Count;
    }

    public void Close()
    {
        Visible = false;
        PendingInput = string.Empty;
        _historyCursor = _history.Count;
    }

    public void Toggle()
    {
        if (Visible)
        {
            Close();
            return;
        }

        Open();
    }

    public bool HandleKey(Key key)
    {
        if (!Visible)
        {
            if (key == Key.Quoteleft)
            {
                Open();
                return true;
            }

            return false;
        }

        switch (key)
        {
            case Key.Quoteleft:
            case Key.Escape:
                Close();
                return true;
            case Key.Enter:
            case Key.KpEnter:
                SubmitCommand();
                return true;
            case Key.Up:
                RecallHistory(-1);
                return true;
            case Key.Down:
                RecallHistory(1);
                return true;
            default:
                return true;
        }
    }

    public string SubmitCommand(string? command = null)
    {
        var text = string.IsNullOrWhiteSpace(command) ? PendingInput : command.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return "No command provided.";
        }

        _history.Add(text);
        _historyCursor = _history.Count;
        AppendLine($"> {text}");
        var result = Processor.Execute(text);
        AppendLine(result);
        PendingInput = string.Empty;
        return result;
    }

    public void ClearOutput()
    {
        _lines.Clear();
    }

    private void OnLogMessage(string message)
    {
        AppendLine($"log: {message}");
    }

    private void RecallHistory(int delta)
    {
        if (_history.Count == 0)
        {
            PendingInput = string.Empty;
            return;
        }

        _historyCursor = Math.Clamp(_historyCursor + delta, 0, _history.Count);
        PendingInput = _historyCursor >= _history.Count ? string.Empty : _history[_historyCursor];
    }

    private void AppendLine(string line)
    {
        _lines.Add(line);
        while (_lines.Count > 120)
        {
            _lines.RemoveAt(0);
        }
    }
}