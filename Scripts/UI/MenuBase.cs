using System.Collections.Generic;
using System.Text;
using Godot;

namespace Godotussy;

public abstract partial class MenuBase : Control
{
    private readonly List<string> _options = new();

    protected int SelectedIndex { get; private set; }

    public string Title { get; protected set; } = string.Empty;

    public string MenuText { get; private set; } = string.Empty;

    public IReadOnlyList<string> Options => _options;

    protected void ConfigureOptions(params string[] options)
    {
        _options.Clear();
        _options.AddRange(options);
        if (SelectedIndex >= _options.Count)
        {
            SelectedIndex = 0;
        }

        RebuildMenuText();
    }

    protected void SetSelection(int index)
    {
        if (_options.Count == 0)
        {
            SelectedIndex = 0;
            MenuText = string.Empty;
            return;
        }

        SelectedIndex = (index % _options.Count + _options.Count) % _options.Count;
        RebuildMenuText();
    }

    public virtual bool HandleKey(Key key)
    {
        if (!Visible || _options.Count == 0)
        {
            return false;
        }

        switch (key)
        {
            case Key.Up:
            case Key.W:
                SetSelection(SelectedIndex - 1);
                return true;
            case Key.Down:
            case Key.S:
                SetSelection(SelectedIndex + 1);
                return true;
            case Key.Enter:
                ActivateSelected();
                return true;
            case Key.Escape:
                Cancel();
                return true;
            default:
                return HandleCustomKey(key);
        }
    }

    public virtual void Open()
    {
        Visible = true;
        SetSelection(SelectedIndex);
    }

    public virtual void Close()
    {
        Visible = false;
    }

    protected virtual bool HandleCustomKey(Key key)
    {
        return false;
    }

    protected virtual void Cancel()
    {
        Close();
    }

    protected abstract void ActivateSelected();

    protected virtual string BuildBodyText()
    {
        return string.Empty;
    }

    protected void RebuildMenuText()
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(Title))
        {
            builder.AppendLine(Title);
            builder.AppendLine();
        }

        var body = BuildBodyText();
        if (!string.IsNullOrWhiteSpace(body))
        {
            builder.AppendLine(body);
            builder.AppendLine();
        }

        for (var index = 0; index < _options.Count; index++)
        {
            builder.Append(index == SelectedIndex ? "> " : "  ");
            builder.AppendLine(_options[index]);
        }

        MenuText = builder.ToString().TrimEnd();
    }
}