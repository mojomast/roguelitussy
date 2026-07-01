using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class ShrineConfirmationUI : MenuBase
{
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private ShrineConfirmationRequest? _request;

    public ShrineConfirmationUI()
    {
        Name = "ShrineConfirmationUI";
        Title = "Shrine Offering";
        ConfigureOptions("Offer HP", "Cancel");
        Visible = false;
    }

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.ShrineConfirmationRequested -= Open;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.ShrineConfirmationRequested += Open;
        }
    }

    public void Open(ShrineConfirmationRequest request)
    {
        _request = request;
        base.Open();
    }

    public string BuildConfirmationMarkup() => BuildBodyText();

    protected override string BuildBodyText()
    {
        return _request is null
            ? "No shrine is selected."
            : $"Offer {_request.HPCost} HP for a {_request.ShrineType} reward?";
    }

    protected override string BuildFooterText() => "Enter confirm  Esc cancel";

    protected override void ActivateSelected()
    {
        if (SelectedIndex != 0 || _request is null || _gameManager is null)
        {
            Close();
            return;
        }

        _gameManager.ProcessPlayerAction(new InteractShrineAction(_request.ActorId, _request.ShrineId));
        Close();
    }
}
