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
    public override async Task BeforeSideTurnStart(PlayerChoiceContext choice, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        if (side == CombatSide.Player)
        {
            foreach (var creature in participants)
            {
                if (WitchCombatState.Find(creature.Player) is { } state)
                {
                    state.BeginTurn();
                    var player = creature.Player!;
                    var piles = player.PlayerCombatState!;
                    if (state.RetainEnergy && piles.Energy > WitchEnergy.EngineLimit - piles.MaxEnergy)
                    {
                        await PlayerCmd.SetEnergy(WitchEnergy.EngineLimit - piles.MaxEnergy, player);
                    }
                }
            }
        }

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
        var energy = state.NextEnergy + state.StartingEnergy + state.ManaPumpDurations.Count;
        state.NextEnergy = 0;
        for (var i = state.ManaPumpDurations.Count - 1; i >= 0; i--)
        {
            if (--state.ManaPumpDurations[i] == 0)
            {
                state.ManaPumpDurations.RemoveAt(i);
            }
        }

        var choice = new BlockingPlayerChoiceContext();
        if (energy > 0)
        {
            await WitchEnergy.Gain(choice, player, energy, fromCard: true);
        }

        var retained = state.RetainedRelicEnergy;
        state.RetainedRelicEnergy = 0;
        if (retained > 0 && !state.RetainEnergy)
        {
            await WitchEnergy.Gain(choice, player, retained);
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

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (WitchCombatState.Find(play.Player) is { } state)
        {
            await WitchEnergy.Resolve(new BlockingPlayerChoiceContext(), play.Player, state);
            state.Plays++;
            if (play.Card is WitchCard { IsDesire: true })
            {
                WitchFeedback.Desire(play.Player.Creature);
            }
            state.ResolvingCard = true;
            if (play.Card.Type == CardType.Attack)
            {
                state.BlockBeforeAttack = play.Player.Creature.Block;
                state.ActiveAttackBonus = state.PendingAttackBonus;
                state.PendingAttackBonus = 0;
            }
        }
    }

    public override Task AfterEnergySpent(CardModel card, int amount)
    {
        if (card is SilentVow && card.Owner.PlayerCombatState?.Hand.Cards.Count >= 8)
        {
            card.EnergyCost.SetThisTurn(1, reduceOnly: true);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterCardPlayedLate(PlayerChoiceContext choice, CardPlay play)
    {
        var player = play.Player;
        if (WitchCombatState.Find(player) is not { } state)
        {
            return;
        }

        await WitchEnergy.Resolve(choice, player, state);
        state.ResolvingCard = false;
        state.ActiveAttackBonus = 0;
        if (!play.Card.EnergyCost.CostsX && play.Card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 2)
        {
            state.HighPrintedPlays++;
            if (state.HighPrintedPlays <= 2 && state.RedCatalystBlock > 0)
            {
                await CreatureCmd.GainBlock(player.Creature, state.RedCatalystBlock, ValueProp.Unpowered, null);
            }

            if (state.HighPrintedPlays == 1 && state.ReverseRiteDraw > 0)
            {
                await CardPileCmd.Draw(choice, state.ReverseRiteDraw, player);
            }
        }

        if (!state.FreePlayTriggered && !play.Card.EnergyCost.CostsX && play.Resources.EnergyValue == 0)
        {
            state.FreePlayTriggered = true;
            if (state.DuskDraw > 0)
            {
                await CardPileCmd.Draw(choice, state.DuskDraw, player);
            }
            if (state.DuskBlock > 0)
            {
                await CreatureCmd.GainBlock(player.Creature, state.DuskBlock, ValueProp.Unpowered, null);
            }
        }
    }

    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext choice, Player player) =>
        WitchCombatState.Find(player) is { } state ? WitchEnergy.Resolve(choice, player, state) : Task.CompletedTask;

    public override Task AfterPotionUsed(PotionModel potion, Creature? target) =>
        WitchCombatState.Find(potion.Owner) is { } state
            ? WitchEnergy.Resolve(new BlockingPlayerChoiceContext(), potion.Owner, state)
            : Task.CompletedTask;

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choice, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player)
        {
            return;
        }

        foreach (var creature in participants)
        {
            if (WitchCombatState.Find(creature.Player) is not { } state)
            {
                continue;
            }

            var player = creature.Player!;
            await WitchEnergy.Resolve(choice, player, state);
            var remaining = player.PlayerCombatState!.Energy;
            foreach (var (perEnergy, cap) in state.TurnEndEnergyBlocks)
            {
                await CreatureCmd.GainBlock(creature, Math.Min((long)remaining * perEnergy, cap), ValueProp.Unpowered, null);
            }
            if (remaining > 0 && state.DepthsBlock > 0)
            {
                await CreatureCmd.GainBlock(creature, Math.Min(3, remaining) * state.DepthsBlock, ValueProp.Unpowered, null);
            }
            if (creature.Block > 0)
            {
                state.NextBlock += state.StartingBlockIfRetained;
            }

            if (!state.RetainEnergy && remaining > 0 && player.Relics.OfType<LostFormula>().FirstOrDefault() is { } formula)
            {
                state.RetainedRelicEnergy = 1;
                formula.Flash();
            }
        }
    }

    public override decimal ModifyBlockMultiplicative(Creature target, decimal block, ValueProp props, CardModel? cardSource, CardPlay? play)
    {
        if (block <= 0 || WitchCombatState.Find(target.Player) is not { } state)
        {
            return 1;
        }

        var first = state.BlockGains == 0 ? 1 + state.FirstBlockPercent / 100m : 1;
        return state.BlockMultiplier * first;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play)
    {
        if (!props.IsPoweredAttack() || dealer?.Player is not { } player || WitchCombatState.Find(player) is not { } state)
        {
            return 0;
        }

        var crown = WitchRules.IsBelowHalf(dealer.CurrentHp, dealer.MaxHp) ? state.CrownDamage : 0;
        return crown + (play?.Card.Type == CardType.Attack ? state.ActiveAttackBonus : 0);
    }

    public override decimal ModifyDamageMultiplicative(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play) =>
        amount > 0 && EnemyAttack(dealer, props) && WitchCombatState.Find(target?.Player) is { BlockReduction: > 0 } state
            ? WitchRules.FlatReductionFactor(amount, state.BlockReduction) : 1;

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
        (!card.HasBeenRemovedFromState || WitchCombatState.Find(card.Owner) == null)
        && (card is not WitchCard { LifeCost: > 0 } witch || WitchRules.CanPayLife(witch.Owner.Creature.CurrentHp, witch.LifeCost));

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card is SilentVow && card.Owner.PlayerCombatState?.Hand.Cards.Count >= 8)
        {
            modifiedCost = Math.Min(modifiedCost, 1);
        }
        if (modifiedCost > 0 && card is WitchCard { IsDesire: true } && WitchCombatState.Find(card.Owner)?.DesireDiscount == true)
        {
            modifiedCost = Math.Max(0, modifiedCost - 1);
        }

        return modifiedCost != originalCost;
    }

    private static bool EnemyAttack(Creature? dealer, ValueProp props) =>
        dealer?.Side == CombatSide.Enemy && props.IsCardOrMonsterMove() && !props.HasFlag(ValueProp.Unblockable);

    public override decimal ModifyDamageCap(Creature? target, ValueProp props, Creature? dealer, CardModel? cardSource, CardPlay? play) =>
        EnemyAttack(dealer, props) && WitchCombatState.Find(target?.Player)?.Immunity != null ? 0 : decimal.MaxValue;

    public override Task BeforeDamageReceived(PlayerChoiceContext choice, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (EnemyAttack(dealer, props) && WitchCombatState.Find(target.Player) is { } state)
        {
            if (state.Immunity is int retaliation)
            {
                state.SpentImmunity = retaliation;
                state.Immunity = null;
            }
            else
            {
                state.BlockReduction = 0;
            }
        }

        return Task.CompletedTask;
    }

    public override decimal ModifyHpLostAfterOstyLate(Creature target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? cardSource)
    {
        var selfInflictedCardLoss = props.HasFlag(ValueProp.Unblockable) && cardSource?.Owner == target.Player
            && target.Player != null && dealer?.Side != CombatSide.Enemy;
        if (amount >= 1 && !selfInflictedCardLoss && WitchCombatState.Find(target.Player) is { MirrorHp: > 0 } state)
        {
            // This hook runs after block even for penetrating damage. Native status damage,
            // such as PoisonPower, may have no dealer. Deliberate payments use WitchLife.Pay.
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
            var capacity = WitchRules.MirrorCapacity(creature.CurrentHp, creature.MaxHp);
            state.MirrorHp = Math.Min(state.MirrorHp, capacity);
            if (delta < 0 && CombatManager.Instance.IsInProgress && !state.VeilTriggered
                && creature.IsAlive && WitchRules.IsBelowHalf(creature.CurrentHp, creature.MaxHp)
                && 2m * (creature.CurrentHp - delta) >= creature.MaxHp
                && creature.Player!.Relics.OfType<DoubleVeil>().FirstOrDefault() is { } veil)
            {
                state.VeilTriggered = true;
                state.MirrorHp = Math.Min(capacity, state.MirrorHp + 12);
                veil.Flash();
            }
            WitchLife.RefreshDisplay(creature, state);
        }

        return Task.CompletedTask;
    }

    public override async Task AfterDamageGiven(PlayerChoiceContext choice, Creature? dealer, DamageResult result,
        ValueProp props, Creature target, CardModel? cardSource)
    {
        if (WitchCombatState.Find(target.Player) is not { } state)
        {
            return;
        }

        if (cardSource?.Owner == target.Player && result.UnblockedDamage > 0)
        {
            state.RecordCardLifeLoss(result.UnblockedDamage);
        }

        if (!EnemyAttack(dealer, props) || dealer == null)
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

        var firstBlock = !state.BlockedAttackThisTurn;
        state.BlockedAttackThisTurn = true;
        state.BlockedDuringEnemyTurn |= target.CombatState?.CurrentSide == CombatSide.Enemy;
        var reflect = new List<int>();
        if (state.MirrorForm)
        {
            reflect.Add(WitchRules.Reflect(result.BlockedDamage, 50, state.ReflectMultiplier));
        }

        reflect.AddRange(state.NextReflections.Select(value => WitchRules.Reflect(value, 100, state.ReflectMultiplier)));
        reflect.AddRange(state.DeferredReflections.Select(value => WitchRules.Reflect(value, 100, state.ReflectMultiplier)));
        state.NextReflections.Clear();
        state.DeferredReflections.Clear();

        if (firstBlock && state.FirstReflectDamage > 0)
        {
            reflect.Add(WitchRules.Reflect(state.FirstReflectDamage, 100, state.ReflectMultiplier));
        }
        if (state.NextReflectPercent > 0)
        {
            reflect.Add(WitchRules.Reflect(result.BlockedDamage, state.NextReflectPercent, state.ReflectMultiplier));
            state.NextReflectPercent = 0;
        }
        if (!state.FirstCombatBlockTriggered && target.Player!.Relics.OfType<SilverMirrorFrame>().FirstOrDefault() is { } frame)
        {
            state.FirstCombatBlockTriggered = true;
            frame.Flash();
            reflect.Add(WitchRules.Reflect(5, 100, state.ReflectMultiplier));
        }

        if (reflect.Any(amount => amount > 0))
        {
            WitchFeedback.Reflect(dealer);
        }

        foreach (var reflection in reflect)
        {
            if (reflection <= 0 || !dealer.IsAlive)
            {
                continue;
            }

            var hits = await CreatureCmd.Damage(choice, dealer, reflection, ValueProp.Unpowered, target);
            if (hits.Any(hit => hit.TotalDamage > 0))
            {
                if (!state.Reflected)
                {
                    state.NextDraw += state.ShadowDraw;
                }
                state.Reflected = true;
                state.ReflectedDuringEnemyTurn |= target.CombatState?.CurrentSide == CombatSide.Enemy;
            }
        }
    }
}
