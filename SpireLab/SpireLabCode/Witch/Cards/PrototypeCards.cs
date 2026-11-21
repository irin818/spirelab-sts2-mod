using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

// C01
public sealed class PotionToss() : WitchCard("C01", 1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(8), new DynamicVar("Bonus", 3)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, State.ExtraEnergy > 0 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// C02
public sealed class PotionMist() : WitchCard("C02", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(6), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.NextEnergy += DynamicVars.Energy.IntValue;
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// C11
public sealed class SilverScreen() : WitchCard("C11", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(7)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Block(play);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// C19
public sealed class RazorPages() : WitchCard("C19", 1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(6), new CardsVar(1)];
    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        await Draw(choice);
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// C20
public sealed class BlackVeil() : WitchCard("C20", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(7)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Block(play);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// R01
public sealed class PinnacleOfMana() : WitchCard("R01", 4, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.RetainEnergy = true;
        WitchFeedback.ReserveEnergy(Owner.Creature);
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// R10
public sealed class TheMirrorSelf() : WitchCard("R10", 3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => WitchLife.EnterMirror(Owner);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
