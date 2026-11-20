using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;

namespace SpireLab.SpireLabCode.Witch.Cards;

// C03
public sealed class Distillation() : WitchCard("C03", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        State.NextEnergy += DynamicVars.Energy.IntValue;
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// C04
public sealed class ShatterPhial() : WitchCard("C04", 2, CardType.Attack, CardRarity.Common, TargetType.AllEnemies)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(10)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// C05
public sealed class Residue() : WitchCard("C05", 0, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override int LifeCost => Number("Life");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Life", 2), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (await PayLife(choice))
        {
            await GainEnergy(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Life"].UpgradeValueBy(-1);
}

// C06
public sealed class ConcentratedFlame() : WitchCard("C06", 2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(14), new DynamicVar("Bonus", 3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, ExtraEnergy > 0 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// C07
public sealed class VolatileSteam() : WitchCard("C07", 1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(6), new DynamicVar("Weak", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        if (play.Target?.IsAlive == true)
        {
            await Weak(choice, play);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

// C08
public sealed class Sediment() : WitchCard("C08", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(8), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        if (State.LastRemainingEnergy > 0)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// C09
public sealed class EmberTrace() : WitchCard("C09", 0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(3), new DynamicVar("Bonus", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        if (ExtraEnergy > 0 && play.Target?.IsAlive == true)
        {
            await Deal(choice, play, Number("Bonus"));
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// C10
public sealed class MirrorChip() : WitchCard("C10", 0, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(4), new DynamicVar("Bonus", 2)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, Owner.Creature.Block > 0 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(2);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// C12
public sealed class EtchedMirrorSlash() : WitchCard("C12", 1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(8), new DynamicVar("Bonus", 3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, Owner.Creature.Block > 0 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// C13
public sealed class SplitFace() : WitchCard("C13", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(6), new DynamicVar("Reflection", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.NextReflections.Add(Number("Reflection"));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Reflection"].UpgradeValueBy(2);
    }
}

// C14
public sealed class ColdMirrorBarrier() : WitchCard("C14", 2, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(14)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Block(play);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

// C15
public sealed class MirrorTransfer() : WitchCard("C15", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(5), new DynamicVar("Next", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.NextBlock += Number("Next");
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(2);
        DynamicVars["Next"].UpgradeValueBy(2);
    }
}

// C16
public sealed class SeveredShadow() : WitchCard("C16", 1, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(7), new DynamicVar("Bonus", 4)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, BelowHalf ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(2);
    }
}

// C17
public sealed class ReflectedLight() : WitchCard("C17", 1, CardType.Skill, CardRarity.Common, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(4), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (Owner.Creature.Block > 0)
        {
            await Draw(choice);
        }
        await Block(play);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// C18
public sealed class Refraction() : WitchCard("C18", 2, CardType.Attack, CardRarity.Common, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(13), new DynamicVar("Bonus", 4)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, State.BlockGains > 0 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars["Bonus"].UpgradeValueBy(2);
    }
}
