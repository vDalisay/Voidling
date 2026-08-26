using Godot;
using Voidling.Presentation.Voidlings;

namespace Voidling.Presentation.Racing;

public partial class RaceScreen
{
    private int _appearanceEntrantIndex;

    private void BeginAppearanceBinding()
    {
        _appearanceEntrantIndex = 0;
        ChildEnteredTree += ApplyAppearanceToEntrantSprite;
    }

    private void ApplyAppearanceToEntrantSprite(Node child)
    {
        if (_entry == null ||
            child is not AnimatedSprite2D sprite ||
            sprite.SpriteFrames != VoidlingVisualFactory.GetRaceFrames() ||
            _appearanceEntrantIndex >= _entry.Entrants.Count)
        {
            return;
        }

        var entrant = _entry.Entrants[_appearanceEntrantIndex++];
        VoidlingAppearancePresenter.Apply(
            sprite,
            entrant.Appearance,
            entrant.Participant.TintHex,
            VoidlingAppearanceContext.RaceRun);
    }
}
