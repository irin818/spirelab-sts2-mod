using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace SpireLab.SpireLabCode.Witch.Cards;

// B01
public sealed class WitchStrike() : WitchCard("B01", 1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(6)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Attack(choice, play);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// B02
public sealed class WitchDefend() : WitchCard("B02", 1, CardType.Skill, CardRarity.Basic, TargetType.Self)
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Defend];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(5)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Block(play);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// B03
public sealed class ThinMirror() : WitchCard("B03", 1, CardType.Skill, CardRarity.Basic, TargetType.Self), ITranscendenceCard
{
    public CardModel GetTranscendenceTransformedCard() => ModelDb.Card<BeyondTheGlass>();
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(9), new DynamicVar("Reflection", 5)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.DeferredReflections.Add(Number("Reflection"));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Reflection"].UpgradeValueBy(2);
    }
}

// B04
public sealed class StarterDraught() : WitchCard("B04", 1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(7), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        State.NextEnergy += DynamicVars.Energy.IntValue;
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}
