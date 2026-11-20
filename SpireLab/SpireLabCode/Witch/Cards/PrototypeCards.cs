using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

// C01
public sealed class PotionToss() : WitchCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, "potion")
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
public sealed class PotionMist() : WitchCard(1, CardType.Skill, CardRarity.Common, TargetType.Self, "potion")
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
public sealed class SilverScreen() : WitchCard(1, CardType.Skill, CardRarity.Common, TargetType.Self, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(7)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Block(play);
    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// C19
public sealed class RazorPages() : WitchCard(1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy, "book")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(7), new DynamicVar("Bonus", 3)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, State.TurnEnlightenments > 0 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// C20
public sealed class BlackVeil() : WitchCard(1, CardType.Skill, CardRarity.Common, TargetType.Self, "book")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(6), new DynamicVar("Bonus", 2)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Block(play, State.Plays == 1 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// R01
public sealed class PinnacleOfMana() : WitchCard(4, CardType.Power, CardRarity.Rare, TargetType.Self, "potion")
{
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.RetainEnergy = true;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// R10
public sealed class TheMirrorSelf() : WitchCard(3, CardType.Power, CardRarity.Rare, TargetType.Self, "mirror")
{
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => WitchLife.EnterMirror(Owner);
    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}
