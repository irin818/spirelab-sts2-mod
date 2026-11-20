using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

public sealed class WitchCombatHooks() : CustomSingletonModel(HookType.Combat)
{
    public override Task BeforeSideTurnStart(PlayerChoiceContext choice, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player)
        {
            foreach (var creature in participants)
            {
                WitchCombatState.Find(creature.Player)?.BeginTurn();
            }
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        WitchCombatState.Find(player) is { } state ? count + 1 + state.NextDraw : count;

    public override bool ShouldPlayerResetEnergy(Player player) =>
        WitchCombatState.Find(player)?.RetainEnergy != true || player.PlayerCombatState!.TurnNumber == 1;

    public override async Task AfterEnergyReset(Player player)
    {
        if (WitchCombatState.Find(player) is not { } state)
        {
            return;
        }

        state.TrackEnergy = true;
        var energy = state.NextEnergy;
        state.NextEnergy = 0;
        if (energy > 0)
        {
            await PlayerCmd.GainEnergy(energy, player);
        }

        var block = state.NextBlock;
        state.NextBlock = 0;
        if (block > 0)
        {
            await CreatureCmd.GainBlock(player.Creature, block, ValueProp.Unpowered, null);
        }
    }

    public override async Task AfterAutoPrePlayPhaseEnteredEarly(PlayerChoiceContext choice, Player player)
    {
        if (WitchCombatState.Find(player) is { } state)
        {
            state.NextDraw = 0;
            await Enlightenment.Choose(choice, player, this);
        }
    }

    public override Task BeforeCardPlayed(CardPlay play)
    {
        if (WitchCombatState.Find(play.Player) is { } state)
        {
            state.Plays++;
        }

        return Task.CompletedTask;
    }

    public override Task AfterBlockGained(Creature target, decimal amount, ValueProp props, CardModel? cardSource)
    {
        if (amount > 0 && WitchCombatState.Find(target.Player) is { } state)
        {
            state.BlockGains++;
        }

        return Task.CompletedTask;
    }

    public override Task AfterCardEnteredCombat(CardModel card)
    {
        if (WitchCombatState.Find(card.Owner) is { } state && state.HandInsertions.TryGetValue(card, out var index)
            && card.Pile is { Type: PileType.Hand } hand)
        {
            // CardPileCmd has inserted the model but has yet to create its hand holder.
            hand.RemoveInternal(card, silent: true);
            hand.AddInternal(card, Math.Min(index, hand.Cards.Count), silent: true);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardChangedPilesLate(CardModel card, PileType oldPileType, AbstractModel? clonedBy)
    {
        var player = card.Owner;
        if (WitchCombatState.Find(player) is not { } state)
        {
            return;
        }

        foreach (var (standIn, original) in state.StandIns.ToArray())
        {
            if (standIn.Pile is null || (standIn == card && standIn.Pile.Type is not (PileType.Hand or PileType.Play)))
            {
                await Enlightenment.Release(player, state, standIn, original);
            }
        }
    }

    public override async Task BeforeFlush(PlayerChoiceContext choice, Player player)
    {
        if (WitchCombatState.Find(player) is { } state)
        {
            state.LastRemainingEnergy = player.PlayerCombatState!.Energy;
            await Enlightenment.ReleaseAll(player, state);
        }
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        card is not AbyssOfGambling || WitchRules.CanPayLife(card.Owner.Creature.CurrentHp,
            WitchRules.GambleCost(card.Owner.Creature.CurrentHp, card.DynamicVars["Percent"].IntValue));

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (originalCost > 0 && card is WitchCard { IsDesire: true } && WitchCombatState.Find(card.Owner)?.DesireDiscount == true)
        {
            modifiedCost = Math.Max(0, originalCost - 1);
            return true;
        }

        return false;
    }

    private static bool EnemyAttack(Creature? dealer, ValueProp props) =>
        dealer?.Side == CombatSide.Enemy && props.IsCardOrMonsterMove() && !props.HasFlag(ValueProp.Unblockable);

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? play) =>
        EnemyAttack(dealer, props) && WitchCombatState.Find(target?.Player)?.Immunity != null ? 0 : decimal.MaxValue;

    public override Task BeforeDamageReceived(PlayerChoiceContext choice, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (EnemyAttack(dealer, props) && WitchCombatState.Find(target.Player) is { Immunity: int retaliation } state)
        {
            state.SpentImmunity = retaliation;
            state.Immunity = null;
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        if (amount >= 1 && dealer?.Side == CombatSide.Enemy && !props.HasFlag(ValueProp.Unblockable)
            && WitchCombatState.Find(target.Player) is { MirrorHp: > 0 } state)
        {
            // CreatureCmd obtains this adjustment after damage to block and before mutating real HP.
            var absorbed = (int)Math.Min(decimal.Truncate(amount), state.MirrorHp);
            state.MirrorHp -= absorbed;
            WitchLife.RefreshDisplay(target, state);
            return amount - absorbed;
        }

        return amount;
    }

    public override Task AfterCurrentHpChanged(Creature creature, decimal delta)
    {
        if (WitchCombatState.Find(creature.Player) is { } state)
        {
            state.MirrorHp = Math.Min(state.MirrorHp, WitchRules.MirrorCapacity(creature.CurrentHp, creature.MaxHp));
            WitchLife.RefreshDisplay(creature, state);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choice, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (!EnemyAttack(dealer, props) || WitchCombatState.Find(target.Player) is not { } state || dealer == null)
        {
            return;
        }

        var retaliation = state.SpentImmunity;
        state.SpentImmunity = null;
        if (retaliation is int immuneHit)
        {
            if (immuneHit > 0 && dealer.IsAlive && target.IsAlive)
            {
                await CreatureCmd.Damage(choice, dealer, immuneHit, ValueProp.Unpowered, target);
            }
            return;
        }

        if (result.BlockedDamage <= 0 || !dealer.IsAlive || !target.IsAlive)
        {
            return;
        }

        state.BlockedDuringEnemyTurn |= target.CombatState?.CurrentSide == CombatSide.Enemy;
        var mirrorDamage = state.MirrorForm ? WitchRules.Reflect(result.BlockedDamage, 50) : 0;
        var bonus = state.NextReflect;
        state.NextReflect = 0;
        foreach (var reflection in new[] { mirrorDamage, bonus })
        {
            if (reflection > 0 && dealer.IsAlive)
            {
                var hits = await CreatureCmd.Damage(choice, dealer, reflection, ValueProp.Unpowered, target);
                var dealt = hits.Any(hit => hit.TotalDamage > 0);
                state.Reflected |= dealt;
                state.ReflectedDuringEnemyTurn |= dealt && target.CombatState?.CurrentSide == CombatSide.Enemy;
            }
        }
    }
}
