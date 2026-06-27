using Godot;

namespace Godotussy;

public partial class FloorSummaryUI : MenuBase
{
    private const float DefaultCountdownSeconds = 6.0f;
    private ColorRect? _countdownTrack;
    private ColorRect? _countdownFill;
    private FloorStats _stats = new(0, 0, 0, 0, 0, 0, 0, 0);
    private EventBus? _eventBus;

    public FloorSummaryUI()
    {
        Name = "FloorSummaryUI";
        Title = "FLOOR SUMMARY";
        ConfigureOptions("Continue");
        CountdownSeconds = DefaultCountdownSeconds;
        Visible = false;
    }

    public float CountdownSeconds { get; private set; }

    public bool PlayerEngaged { get; private set; }

    public void Bind(EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.FloorSummaryReady -= Open;
        }

        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.FloorSummaryReady += Open;
        }
    }

    public void Open(FloorStats stats)
    {
        _stats = stats;
        CountdownSeconds = DefaultCountdownSeconds;
        PlayerEngaged = false;
        Title = $"FLOOR {_stats.FloorNumber} CLEARED";
        base.Open();
    }

    public string BuildBodyMarkup() => BuildBodyText();

    public override void _Process(double delta)
    {
        if (!Visible || PlayerEngaged || CountdownSeconds <= 0f)
        {
            return;
        }

        CountdownSeconds = System.Math.Max(0f, CountdownSeconds - (float)delta);
        RebuildMenuText();
        if (CountdownSeconds <= 0f)
        {
            ConfirmTransition();
        }
    }

    public override bool HandleKey(Key key)
    {
        if (!Visible)
        {
            return false;
        }

        if (key is Key.Enter or Key.KpEnter or Key.Space or Key.Escape)
        {
            ConfirmTransition();
            return true;
        }

        PlayerEngaged = true;
        CountdownSeconds = DefaultCountdownSeconds;
        RebuildMenuText();

        return true;
    }

    protected override string BuildBodyText()
    {
        var muted = UiStyle.ToHex(UiStyle.MutedText());
        var gold = UiStyle.ToHex(UiStyle.BrightGold());
        var danger = UiStyle.ToHex(UiStyle.DangerRed());

        return $"[color={gold}][b]Floor {_stats.FloorNumber} survived[/b][/color]\n" +
            "----------------------------------------\n" +
            $"[color={muted}]Enemies slain[/color]      [color={gold}]{_stats.EnemiesKilled:N0}[/color]\n" +
            $"[color={muted}]Items found[/color]        [color={gold}]{_stats.ItemsFound:N0}[/color]\n" +
            $"[color={muted}]Gold collected[/color]    [color={gold}]{_stats.GoldCollected:N0}[/color]\n" +
            $"[color={muted}]Damage taken[/color]      [color={danger}]{_stats.DamageTaken:N0}[/color]\n" +
            $"[color={muted}]Turns spent[/color]       [color={gold}]{_stats.TurnsSpent:N0}[/color]\n" +
            $"[color={muted}]Chests opened[/color]     [color={gold}]{_stats.ChestsOpened:N0}[/color]\n" +
            $"[color={muted}]Traps triggered[/color]   [color={gold}]{_stats.TrapsTriggered:N0}[/color]\n" +
            "----------------------------------------\n" +
            $"[color={muted}][i]{ResolveFlavorLine(_stats)}[/i][/color]";
    }

    protected override string BuildFooterText()
    {
        return PlayerEngaged
            ? "[ENTER/SPACE/ESC] Continue"
            : $"[ENTER/SPACE/ESC] Continue     Auto in {CountdownSeconds:0.0}s";
    }

    protected override void OnVisualStateRefreshed(Panel panel, Label label, Vector2 viewportSize, Vector2 panelSize)
    {
        EnsureCountdownBar(panel);
        if (_countdownTrack is null || _countdownFill is null)
        {
            return;
        }

        var width = System.Math.Max(0f, panelSize.X - 40f);
        var fraction = CountdownSeconds <= 0f ? 0f : System.Math.Clamp(CountdownSeconds / DefaultCountdownSeconds, 0f, 1f);
        _countdownTrack.Position = new Vector2(20f, panelSize.Y - 14f);
        _countdownTrack.Size = new Vector2(width, 4f);
        _countdownTrack.Color = UiStyle.PanelInner(0.9f);
        _countdownTrack.Visible = Visible && !PlayerEngaged;
        _countdownFill.Position = Vector2.Zero;
        _countdownFill.Size = new Vector2(width * fraction, 4f);
        _countdownFill.Color = UiStyle.BrightGold(0.95f);
        _countdownFill.Visible = _countdownTrack.Visible;
    }

    protected override void ActivateSelected()
    {
        ConfirmTransition();
    }

    private void ConfirmTransition()
    {
        if (!Visible)
        {
            return;
        }

        Close();
        _eventBus?.EmitFloorTransitionConfirmed();
    }

    private void EnsureCountdownBar(Panel panel)
    {
        if (_countdownTrack is not null && _countdownFill is not null)
        {
            return;
        }

        _countdownTrack = new ColorRect { Name = "CountdownTrack" };
        _countdownFill = new ColorRect { Name = "CountdownFill" };
        _countdownTrack.AddChild(_countdownFill);
        panel.AddChild(_countdownTrack);
    }

    private static string ResolveFlavorLine(FloorStats stats)
    {
        if (stats.DamageTaken == 0)
        {
            return "Perfect floor.";
        }

        if (stats.EnemiesKilled >= 10)
        {
            return "A bloodbath. The dungeon noticed.";
        }

        if (stats.ChestsOpened >= 2)
        {
            return "Thorough work.";
        }

        if (stats.TurnsSpent < 100)
        {
            return "Swift descent.";
        }

        return "Onward.";
    }
}
