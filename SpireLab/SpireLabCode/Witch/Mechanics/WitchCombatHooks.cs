using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
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
                    await WitchStatuses.Refresh(choice, player, state);
                }
            }
        }

    }

    public override decimal ModifyHandDraw(Player player, decimal count) =>
        WitchCombatState.Find(player) is { } state ? count + 1 + state.NextDraw : count;

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState) =>
        WitchCombatState.Find(player) != null && combatState is CombatState combat
            ? Enlightenment.ChooseOpeningCard(choiceContext, player, combat)
            : Task.CompletedTask;

    public override bool ShouldPlayerResetEnergy(Player player) =>
        WitchCombatState.Find(player)?.RetainEnergy != true || player.PlayerCombatState!.TurnNumber == 1;

    public override async Task AfterEnergyReset(Player player)
    {
        if (WitchCombatState.Find(player) is not { } state)
        {
            return;
        }

        state.TrackEnergy = true;
        WitchFeedback.UpdateHealth(player.Creature);
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
        await WitchStatuses.Refresh(choice, player, state);
    }

    public override async Task AfterAutoPrePlayPhaseEnteredEarly(PlayerChoiceContext choice, Player player)
    {
        if (WitchCombatState.Find(player) is { } state)
        {
            state.NextDraw = 0;
            await WitchStatuses.Refresh(choice, player, state);
            await Enlightenment.ChooseSwap(choice, player, this);
            state.TraitSwapOffered = true;
            await WitchStatuses.Refresh(choice, player, state);
        }
    }

    public override async Task BeforeCardPlayed(CardPlay play)
    {
        if (WitchCombatState.Find(play.Player) is { } state)
        {
            var choice = new BlockingPlayerChoiceContext();
            await WitchEnergy.Resolve(choice, play.Player, state);
            state.Plays++;
            if (play.Card is WitchCard && play.Card.Type is CardType.Skill or CardType.Power)
            {
                await CreatureCmd.TriggerAnim(play.Player.Creature, "Cast", 0);
            }
            if (play.Card is WitchCard { IsDesire: true })
            {
                WitchFeedback.Desire(play.Player.Creature);
            }
            state.ResolvingCard = true;
            if (play.Card.Type == CardType.Skill && state.SkillDiscount > 0)
            {
                state.SkillDiscount = 0;
                var ward = state.SkillWard;
                state.SkillWard = 0;
                foreach (var skill in play.Player.PlayerCombatState!.Hand.Cards.Where(card => card.Type == CardType.Skill))
                {
                    skill.InvokeEnergyCostChanged();
                }
                if (ward > 0)
                {
                    await CreatureCmd.GainBlock(play.Player.Creature, ward, ValueProp.Unpowered, null);
                }
            }
            if (play.Card.Type == CardType.Attack)
            {
                state.BlockBeforeAttack = play.Player.Creature.Block;
                state.ActiveAttackBonus = state.PendingAttackBonus;
                state.PendingAttackBonus = 0;
            }
        }
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
        if (!state.BookmarkUsed && state.BookmarkEnergy > 0 && state.RetainedCards.Contains(play.Card))
        {
            state.BookmarkUsed = true;
            await WitchEnergy.Gain(choice, player, state.BookmarkEnergy, fromCard: true);
        }
        await WitchStatuses.Refresh(choice, player, state);
    }

    public override Task AfterPlayerTurnStartLate(PlayerChoiceContext choice, Player player) =>
        WitchCombatState.Find(player) is { } state ? WitchEnergy.Resolve(choice, player, state) : Task.CompletedTask;

    public override async Task AfterPotionUsed(PotionModel potion, Creature? target)
    {
        if (WitchCombatState.Find(potion.Owner) is { } state)
        {
            var choice = new BlockingPlayerChoiceContext();
            await WitchEnergy.Resolve(choice, potion.Owner, state);
            await WitchStatuses.Refresh(choice, potion.Owner, state);
        }
    }

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
            if (remaining > 0 && state.DepthsEnergy > 0)
            {
                state.NextEnergy += state.DepthsEnergy;
                if (state.DepthsBlock > 0)
                {
                    await CreatureCmd.GainBlock(creature, state.DepthsBlock, ValueProp.Unpowered, null);
                }
            }
            if (!state.RetainEnergy && remaining > 0 && player.Relics.OfType<LostFormula>().FirstOrDefault() is { } formula)
            {
                state.RetainedRelicEnergy = 1;
                formula.Flash();
            }
            await WitchStatuses.Refresh(choice, player, state);
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

    public override Task BeforeFlush(PlayerChoiceContext choice, Player player)
    {
        if (WitchCombatState.Find(player) is { } state)
        {
            state.LastRemainingEnergy = player.PlayerCombatState!.Energy;
            if (state.RetainRandomCount > 0)
            {
                var candidates = player.PlayerCombatState.Hand.Cards.Where(card => !card.ShouldRetainThisTurn).ToList();
                player.RunState.Rng.CombatCardSelection.Shuffle(candidates);
                foreach (var card in candidates.Take(state.RetainRandomCount))
                {
                    CardCmd.ApplySingleTurnRetain(card);
                }
            }
        }
        return Task.CompletedTask;
    }

    public override Task AfterFlush(PlayerChoiceContext choiceContext, Player player,
        IReadOnlyCollection<CardModel> flushedCards, IReadOnlyCollection<CardModel> retainedCards)
    {
        WitchCombatState.Find(player)?.RetainedCards.UnionWith(retainedCards);
        return Task.CompletedTask;
    }

    public override bool ShouldClearBlock(Creature creature) => WitchCombatState.Find(creature.Player)?.RetainedBlockCap is not > 0;

    public override Task AfterPreventingBlockClear(AbstractModel preventer, Creature creature) =>
        preventer == this && WitchCombatState.Find(creature.Player) is { } state && creature.Block > state.RetainedBlockCap
            ? CreatureCmd.LoseBlock(new BlockingPlayerChoiceContext(), creature, creature.Block - state.RetainedBlockCap, null)
            : Task.CompletedTask;

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        foreach (var player in room.CombatState.Players)
        {
            if (WitchCombatState.Find(player) is not { } state)
            {
                continue;
            }

            if (state.OpeningCard is { Pile.IsCombatPile: true } card)
            {
                await CardPileCmd.RemoveFromCombat(card, skipVisuals: true);
            }
            state.OpeningCard = null;
        }
    }

    public override bool ShouldPlay(CardModel card, AutoPlayType autoPlayType) =>
        (!card.HasBeenRemovedFromState || WitchCombatState.Find(card.Owner) == null)
        && (card is not WitchCard { LifeCost: > 0 } witch || WitchRules.CanPayLife(witch.Owner.Creature.CurrentHp, witch.LifeCost));

    public override bool TryModifyEnergyCostInCombatLate(CardModel card, decimal originalCost, out decimal modifiedCost)
    {
        modifiedCost = originalCost;
        if (card.Type == CardType.Skill && WitchCombatState.Find(card.Owner) is { SkillDiscount: > 0 } state)
        {
            modifiedCost = Math.Max(0, modifiedCost - state.SkillDiscount);
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
            WitchFeedback.UpdateHealth(creature);
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
            await WitchStatuses.Refresh(choice, target.Player!, state);
            return;
        }

        if (result.BlockedDamage <= 0 || !dealer.IsAlive || !target.IsAlive)
        {
            await WitchStatuses.Refresh(choice, target.Player!, state);
            return;
        }

        WitchFeedback.Block(target);
        state.BlockedDuringEnemyTurn |= target.CombatState?.CurrentSide == CombatSide.Enemy;
        var reflect = new List<int>();
        if (state.MirrorForm)
        {
            reflect.Add(WitchRules.Reflect(result.BlockedDamage, 50, state.ReflectMultiplier));
        }

        reflect.AddRange(state.DeferredReflections.Select(value => WitchRules.Reflect(value, 100, state.ReflectMultiplier)));
        state.DeferredReflections.Clear();

        if (state.WeakOnBlock > 0)
        {
            var weak = state.WeakOnBlock;
            state.WeakOnBlock = 0;
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.WeakPower>(choice, dealer, weak, target, null);
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
                    if (state.InscriptionEnabled)
                    {
                        state.NextSkillDiscount = 1;
                        state.NextSkillWard = state.InscriptionWard;
                    }
                }
                state.Reflected = true;
                state.ReflectedDuringEnemyTurn |= target.CombatState?.CurrentSide == CombatSide.Enemy;
            }
        }
        await WitchStatuses.Refresh(choice, target.Player!, state);
    }
}
