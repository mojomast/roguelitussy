using System.Collections.Generic;
using System.Linq;
using Godot;
using Roguelike.Core;

namespace Godotussy;

public partial class RelicChoiceOverlay : MenuBase
{
    private GameManager? _gameManager;
    private EventBus? _eventBus;
    private IReadOnlyList<RelicTemplate> _choices = System.Array.Empty<RelicTemplate>();

    public RelicChoiceOverlay()
    {
        Name = "RelicChoiceOverlay";
        Title = "Choose a Relic";
        Visible = false;
    }

    public IReadOnlyList<RelicTemplate> CurrentChoices => _choices;

    public void Bind(GameManager? gameManager, EventBus? eventBus)
    {
        if (_eventBus is not null)
        {
            _eventBus.RelicChoiceReady -= Open;
        }

        _gameManager = gameManager;
        _eventBus = eventBus;
        if (_eventBus is not null)
        {
            _eventBus.RelicChoiceReady += Open;
        }
    }

    public void Open(IReadOnlyList<RelicTemplate> choices)
    {
        _choices = choices.Where(choice => !string.IsNullOrWhiteSpace(choice.RelicId)).Take(3).ToArray();
        ConfigureOptions(_choices.Select(choice => $"{choice.DisplayName} [{choice.Rarity}]").ToArray());
        base.Open();
    }

    public string BuildChoiceMarkup() => BuildBodyText();

    protected override string BuildBodyText()
    {
        if (_choices.Count == 0)
        {
            return "No relics are available.";
        }

        return string.Join("\n\n", _choices.Select((choice, index) =>
            $"{index + 1}. [b]{ItemRarityPresentation.EscapeBBCode(choice.DisplayName)}[/b] [{choice.Rarity}]\n{ItemRarityPresentation.EscapeBBCode(choice.Description)}"));
    }

    protected override string BuildFooterText() => "Enter claim  Esc close";

    protected override void ActivateSelected()
    {
        if (SelectedIndex < 0 || SelectedIndex >= _choices.Count)
        {
            return;
        }

        if (_gameManager?.ProcessRelicChoice(_choices[SelectedIndex].RelicId) == true)
        {
            Close();
        }
    }
}
