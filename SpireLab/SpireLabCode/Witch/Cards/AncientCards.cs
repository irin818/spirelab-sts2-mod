using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;

namespace SpireLab.SpireLabCode.Witch.Cards;

// A01
public sealed class BeyondTheGlass() : WitchCard("A01", 1, CardType.Skill, CardRarity.Ancient, TargetType.Self)
{
    public override bool GainsBlock => true;
    // Ancient sources may apply the native + upgrade flag. D5 keeps the effect values fixed.

    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(14), new DynamicVar("Reflection", 8), new DynamicVar("Next", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.DeferredReflections.Add(Number("Reflection"));
        State.NextDraw += Number("Next");
    }
}

// A02
public sealed class SevenfoldDistillation() : WitchCard("A02", 2, CardType.Power, CardRarity.Ancient, TargetType.Self), ITomeCard
{
    // Ancient sources may apply the native + upgrade flag. D5 keeps the effect values fixed.

    public CharacterModel TomeCharacter => ModelDb.Character<TheWitch>();
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.StartingEnergy += DynamicVars.Energy.IntValue;
        State.SevenfoldDraw = true;
        return Task.CompletedTask;
    }
}
