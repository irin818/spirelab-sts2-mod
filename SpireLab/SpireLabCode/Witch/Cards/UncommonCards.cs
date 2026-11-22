using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

// U01
public sealed class Craving() : WitchCard("U01", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class Recovery() : WitchCard("U02", 2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class ManaSeal() : WitchCard("U03", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class ForbiddenRefinement() : WitchCard("U04", 2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        GainEnergy(choice);

    protected override void OnUpgrade() => DynamicVars.Energy.UpgradeValueBy(1);
}

// U05
public sealed class Purification() : WitchCard("U05", 0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1), new CardsVar(0)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (IsUpgraded)
        {
            await Draw(choice);
        }
        if (await ExhaustOne(choice, "SPIRELAB-WITCH_PURIFY_EXHAUST"))
        {
            WitchFeedback.PurifyTransmute(Owner.Creature);
            await GainEnergy(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U06
public sealed class OverloadFormula() : WitchCard("U06", 1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(9)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (Owner.PlayerCombatState!.Energy > 0 && await ConfirmOptionalEffect(choice, "SPIRELAB-WITCH_OVERLOAD_ALL"))
        {
            await PlayerCmd.LoseEnergy(1, Owner);
            await DamageCmd.Attack(DynamicVars.Damage.BaseValue).FromCard(this, play)
                .TargetingAllOpponents(CombatState!).WithHitFx("vfx/vfx_attack_slash").Execute(choice);
        }
        else
        {
            await Attack(choice, play);
        }
    }
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// U07
public sealed class ElixirPhial() : WitchCard("U07", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        if (IsCounterpoint)
        {
            await GainEnergy(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U08
public sealed class ManaPump() : WitchCard("U08", 1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
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
public sealed class BottledLightning() : WitchCard("U09", 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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
public sealed class BoilingEcho() : WitchCard("U10", 2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(9)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, hits: ExtraEnergy > 0 ? 3 : 2);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// U11
public sealed class RefineTheRemainder() : WitchCard("U11", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class VacuumDraught() : WitchCard("U12", 1, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Weak", 2), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (play.Target?.IsAlive == true)
        {
            await Weak(choice, play);
        }
        if (ExtraEnergy > 0)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Weak"].UpgradeValueBy(1);
}

// U13
public sealed class AlienExtract() : WitchCard("U13", 2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class BlueFlameFormula() : WitchCard("U14", 1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Flame", 6)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.BlueFlameBonus += Number("Flame");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Flame"].UpgradeValueBy(3);
}

// U15
public sealed class BackflowDraught() : WitchCard("U15", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2), new EnergyVar(1), Guard(4)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        if (ExtraEnergy > 0 && await DiscardOne(choice, optional: true))
        {
            await GainEnergy(choice);
        }
        if (IsCounterpoint)
        {
            await Block(play);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}

// U16
public sealed class MirrorBlade() : WitchCard("U16", 1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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
public sealed class Afterimage() : WitchCard("U17", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class MirrorMaze() : WitchCard("U18", 2, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(10), new DynamicVar("Reflection", 100)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.TempReflectPercent += Number("Reflection");
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(4);
        DynamicVars["Reflection"].UpgradeValueBy(50);
    }
}

// U19
public sealed class ReturnReflection() : WitchCard("U19", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(7), new DynamicVar("Reflection", 25), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.TempReflectPercent += Number("Reflection");
        if (State.ReflectedLastEnemyTurn)
        {
            await Draw(choice);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Reflection"].UpgradeValueBy(25);
    }
}

// U20
public sealed class BreakTheMirror() : WitchCard("U20", 1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
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
public sealed class ReflectiveInscription() : WitchCard("U21", 1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Reflection", 25)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.InscriptionReflectGain += Number("Reflection");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Reflection"].UpgradeValueBy(25);
}

// U22
public sealed class HallOfGlass() : WitchCard("U22", 1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Next", 5)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.RetainedBlockCap += Number("Next");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Next"].UpgradeValueBy(2);
}

// U23
public sealed class BrittleGlass() : WitchCard("U23", 1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(10)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, hits: State.LifePaidByCardThisTurn ? 2 : 1);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// U24
public sealed class DoubleSidedMirror() : WitchCard("U24", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(8), new DynamicVar("Bonus", 5)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.PendingAttackBonus += Number("Bonus");
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Block.UpgradeValueBy(3);
        DynamicVars["Bonus"].UpgradeValueBy(2);
    }
}

// U25
public sealed class RearSilhouette() : WitchCard("U25", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class MirrorPrison() : WitchCard("U26", 2, CardType.Skill, CardRarity.Uncommon, TargetType.AnyEnemy)
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
public sealed class TurnInward() : WitchCard("U27", 0, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(4), new CardsVar(1)];

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
public sealed class VirtualImage() : WitchCard("U28", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Reduce", 6)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.BlockReduction += Number("Reduce");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Reduce"].UpgradeValueBy(3);
}

// U29 (Retired from reward pool in V2.0; preserved for save compatibility)
public sealed class RefractionPrism() : WitchCard("U29", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
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
public sealed class PageExchange() : WitchCard("U30", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(3)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        await DiscardOne(choice, optional: IsCounterpoint);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U31
public sealed class ForbiddenFrontispiece() : WitchCard("U31", 1, CardType.Power, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.RetainSelectCount += DynamicVars.Cards.IntValue;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// U32
public sealed class SigilBlade() : WitchCard("U32", 1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(9), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        if (await DiscardOne(choice, optional: true))
        {
            if (play.Target?.IsAlive == true)
            {
                await Attack(choice, play);
            }
            if (IsCounterpoint)
            {
                await Draw(choice);
            }
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

// U33
public sealed class SilentVow() : WitchCard("U33", 2, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Retain];
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(18)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play);

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(5);
}

// U34
public sealed class HalfMoonLedger() : WitchCard("U34", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.PendingLedgerAttack = true;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// U35
public sealed class DarkVeilWard() : WitchCard("U35", 1, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(10)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        if (IsCounterpoint)
        {
            State.NextAttackDiscount += 1;
            WitchCombatHooks.RefreshHandAttackCosts(Owner);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

// U36
public sealed class BlankPactPage() : WitchCard("U36", 0, CardType.Skill, CardRarity.Uncommon, TargetType.Self)
{
    public override bool GainsBlock => true;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2), Guard(4)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Draw(choice);
        await DiscardOne(choice);
        if (IsCounterpoint)
        {
            await Block(play);
        }
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Cards.UpgradeValueBy(1);
        DynamicVars.Block.UpgradeValueBy(2);
    }
}
