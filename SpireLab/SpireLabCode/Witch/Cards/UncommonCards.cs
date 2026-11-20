using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;

namespace SpireLab.SpireLabCode.Witch.Cards;

// U01
public sealed class Craving() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3), new DynamicVar("Vulnerable", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        await PowerCmd.Apply<VulnerablePower>(choice, Owner.Creature, Number("Vulnerable"), Owner.Creature, this);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U02
public sealed class Recovery() : WitchCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Heal", 4), Guard(6)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await CreatureCmd.Heal(Owner.Creature, Number("Heal"));
        await Block(play);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Heal"].UpgradeValueBy(1);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

// U03
public sealed class ManaSeal() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(8), new DynamicVar("Next", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        if (Owner.PlayerCombatState!.Energy > 0)
        {
            State.NextDraw += Number("Next");
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// U04
public sealed class ForbiddenRefinement() : WitchCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        GainEnergy(choice);

    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1);
}

// U05
public sealed class Purification() : WitchCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1), new CardsVar(0)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (IsUpgraded)
        {
            await Draw(choice);
        }
        if (await DiscardOne(choice))
        {
            await GainEnergy(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U06
public sealed class OverloadFormula() : WitchCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "potion")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(9), new DynamicVar("Bonus", 3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, Math.Min(3, ExtraEnergy) * Number("Bonus"));

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// U07
public sealed class ElixirPhial() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Heal", 2)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        CreatureCmd.Heal(Owner.Creature, Number("Heal"));

    protected override void OnUpgrade() => DynamicVars["Heal"].UpgradeValueBy(1);
}

// U08
public sealed class ManaPump() : WitchCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, "potion")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Turns", 2)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.ManaPumpDurations.Add(Number("Turns"));
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Turns"].UpgradeValueBy(1);
}

// U09
public sealed class BottledLightning() : WitchCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "potion")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(5), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        if (ExtraEnergy > 0)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

// U10
public sealed class BoilingEcho() : WitchCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "potion")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(8)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, hits: 2);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// U11
public sealed class RefineTheRemainder() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Per", 3), new DynamicVar("Cap", 12)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.TurnEndEnergyBlocks.Add((Number("Per"), Number("Cap")));
        return Task.CompletedTask;
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Per"].UpgradeValueBy(1);
        DynamicVars["Cap"].UpgradeValueBy(4);
    }
}

// U12
public sealed class VacuumDraught() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy, "potion")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Weak", 2), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (play.Target?.IsAlive == true)
        {
            await Weak(choice, play);
        }
        if (BelowHalf)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Weak"].UpgradeValueBy(1);
}

// U13
public sealed class AlienExtract() : WitchCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2), new CardsVar(2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await GainEnergy(choice);
        await Draw(choice);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U14
public sealed class BlueFlameFormula() : WitchCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, "potion")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Flame", 4)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.BlueFlameDamage += Number("Flame");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Flame"].UpgradeValueBy(2);
}

// U15
public sealed class BackflowDraught() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "potion")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        if (ExtraEnergy > 0 && await DiscardOne(choice, optional: true))
        {
            await GainEnergy(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U16
public sealed class MirrorBlade() : WitchCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(8)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var attack = await Attack(choice, play);
        var lost = attack.Results.SelectMany(hits => hits).Sum(hit => hit.UnblockedDamage);
        if (lost >= 2)
        {
            await CreatureCmd.GainBlock(Owner.Creature, lost / 2, ValueProp.Move, play);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// U17
public sealed class Afterimage() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(6)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.NextBlock += DynamicVars.Block.IntValue;
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

// U18
public sealed class MirrorMaze() : WitchCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(10)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.ReflectMultiplier *= 1.5m;
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

// U19
public sealed class ReturnReflection() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(7), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        if (State.BlockedLastEnemyTurn)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// U20
public sealed class BreakTheMirror() : WitchCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "mirror")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(8), new DynamicVar("Cap", 6)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var block = Math.Min(Owner.Creature.Block, Number("Cap"));
        if (block > 0)
        {
            await CreatureCmd.LoseBlock(choice, Owner.Creature, block, Owner.Creature);
        }
        await Attack(choice, play, 2 * block);
    }

    protected override void OnUpgrade()
    {
        DynamicVars["Cap"].UpgradeValueBy(2);
        DynamicVars.Damage.UpgradeValueBy(2);
    }
}

// U21
public sealed class ReflectiveInscription() : WitchCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Reflection", 3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.FirstReflectDamage += Number("Reflection");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Reflection"].UpgradeValueBy(2);
}

// U22
public sealed class HallOfGlass() : WitchCard(2, CardType.Power, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Next", 3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.StartingBlockIfRetained += Number("Next");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Next"].UpgradeValueBy(2);
}

// U23
public sealed class BrittleGlass() : WitchCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(9), Guard(3)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        if (BelowHalf)
        {
            await Block(play);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

// U24
public sealed class DoubleSidedMirror() : WitchCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(12), new DynamicVar("Bonus", 4)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.PendingAttackBonus += Number("Bonus");
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4);
        DynamicVars["Bonus"].UpgradeValueBy(2);
    }
}

// U25
public sealed class RearSilhouette() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(7), new DynamicVar("Low", 13)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Block(play, BelowHalf ? Number("Low") - DynamicVars.Block.IntValue : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Low"].UpgradeValueBy(4);
    }
}

// U26
public sealed class MirrorPrison() : WitchCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(14), new DynamicVar("Weak", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        if (play.Target?.IsAlive == true)
        {
            await Weak(choice, play);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

// U27
public sealed class TurnInward() : WitchCard(0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "mirror")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(3), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var hasBlock = State.BlockBeforeAttack > 0;
        await Attack(choice, play);
        if (hasBlock)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

// U28
public sealed class VirtualImage() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Reduce", 6)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.BlockReduction += Number("Reduce");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Reduce"].UpgradeValueBy(3);
}

// U29
public sealed class RefractionPrism() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "mirror")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(6), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        if (State.ReflectedLastEnemyTurn)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// U30
public sealed class PageExchange() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "book")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        await DiscardOne(choice);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U31
public sealed class ForbiddenFrontispiece() : WitchCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, "book")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Ward", 2)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.EnlightenmentBlock += Number("Ward");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Ward"].UpgradeValueBy(1);
}

// U32
public sealed class SigilBlade() : WitchCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "book")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(7), new DynamicVar("Bonus", 7)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, State.TurnEnlightenments > 0 ? Number("Bonus") : 0);

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(2);
    }
}

// U33
public sealed class SilentVow() : WitchCard(2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "book")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(18)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5);
}

// U34
public sealed class HalfMoonLedger() : WitchCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "book")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Next", 2)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.NextDraw += Number("Next");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Next"].UpgradeValueBy(1);
}

// U35
public sealed class DarkVeilWard() : WitchCard(2, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "book")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(12), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        await Draw(choice);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

// U36
public sealed class BlankPactPage() : WitchCard(0, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "book")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        await DiscardOne(choice);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}
