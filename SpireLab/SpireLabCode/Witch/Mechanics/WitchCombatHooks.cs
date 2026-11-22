using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

public sealed class WitchCombatHooks() : CustomSingletonModel(HookType.Combat)
{
    internal static PlayKind Classify(CardType type) => type switch
    {
        CardType.Attack => PlayKind.Attack,
        CardType.Skill => PlayKind.Skill,
        _ => PlayKind.Other
    };

    internal static void RefreshHandAttackCosts(Player player)
    {
        foreach (var attack in player.PlayerCombatState!.Hand.Cards.Where(card => card.Type == CardType.Attack))
        {
            attack.InvokeEnergyCostChanged();
        }
    }

    public override async Task BeforeSideTurnStart(PlayerChoiceContext choice, CombatSide side,
        IReadOnlyList<Creature> participants, ICombatState combatState)
    {
        foreach (var player in combatState.Players)
        {
            if (WitchCombatState.Find(player) is { } state)
            {
                state.GamblePaidThisTurn = 0;
            }
        }
        if (side == CombatSide.Player)
        {
            foreach (var creature in participants)
            {
                if (WitchCombatState.Find(creature.Player) is { } state)
                {
                    var hadAttackDiscount = state.NextAttackDiscount > 0;
                    state.BeginTurn();
                    var player = creature.Player!;
                    if (hadAttackDiscount)
                    {
                        RefreshHandAttackCosts(player);
                    }
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

    public override Task BeforeHandDraw(Player player, PlayerChoiceContext choiceContext, ICombatState combatState)
    {
        if (WitchCombatState.Find(player) == null || combatState is not CombatState combat)
        {
            return Task.CompletedTask;
        }

        CrimsonFinalMirror.WarmMaterials(combat);
        return Enlightenment.ChooseOpeningCard(choiceContext, player, combat);
    }

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
            if (play.PlayIndex == 0)
            {
                state.CurrentPlayWasCounterpoint = WitchRules.IsCounterpoint(
                    state.LastPlayedType is { } prev ? Classify(prev) : null,
                    Classify(play.Card.Type));
            }
            if (play.Card is WitchCard && play.Card.Type is CardType.Skill or CardType.Power)
            {
                await CreatureCmd.TriggerAnim(play.Player.Creature, "Cast", 0);
            }
            if (play.Card is WitchCard { IsDesire: true })
            {
                WitchFeedback.Desire(play.Player.Creature);
            }
            state.ResolvingCard = true;
            if (play.Card.Type == CardType.Attack)
            {
                state.BlockBeforeAttack = play.Player.Creature.Block;
                if (play.PlayIndex == 0)
                {
                    if (state.NextAttackDiscount > 0)
                    {
                        state.NextAttackDiscount = 0;
                        RefreshHandAttackCosts(play.Player);
                    }
                    state.ActiveAttackBonus = state.PendingAttackBonus;
                    state.PendingAttackBonus = 0;
                    state.ActiveSingleHitBonus = state.PendingSingleHitAttackBonus;
                    state.PendingSingleHitAttackBonus = 0;
                }
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
        state.ActiveSingleHitBonus = 0;

        var wasCounterpoint = state.CurrentPlayWasCounterpoint;
        state.CurrentPlayWasCounterpoint = false;
        state.LastPlayedType = play.Card.Type;

        // U34 Half-Moon Ledger: after the next Attack resolves this turn, shuffle it into draw pile (if non-Exhaust / in Play) and draw 1.
        if (state.PendingLedgerAttack && play.Card.Type == CardType.Attack && play.PlayIndex == play.PlayCount - 1)
        {
            state.PendingLedgerAttack = false;
            if (!CombatManager.Instance.IsOverOrEnding)
            {
                if (play.ResultPile == PileType.Discard && play.Card.Pile?.Type == PileType.Play)
                {
                    await CardPileCmd.Add(play.Card, PileType.Draw, CardPilePosition.Random);
                }
                await CardPileCmd.Draw(choice, 1, player);
            }
        }

        // R15 Empty Mirror (Power): first alternating Attack/Skill play each turn draws 1 (+3 Block when upgraded).
        if (wasCounterpoint && !state.EmptyMirrorTriggered && state.EmptyMirrorDraw > 0 && !CombatManager.Instance.IsOverOrEnding)
        {
            state.EmptyMirrorTriggered = true;
            await CardPileCmd.Draw(choice, state.EmptyMirrorDraw, player);
            if (state.EmptyMirrorBlock > 0)
            {
                await CreatureCmd.GainBlock(player.Creature, state.EmptyMirrorBlock, ValueProp.Unpowered, null);
            }
        }

        if (play.Card is WitchCard { IsDesire: true })
        {
            state.DesirePlayedThisTurn = true;
            if (!state.RedCatalystTriggered && state.RedCatalystDraw > 0 && !CombatManager.Instance.IsOverOrEnding)
            {
                state.RedCatalystTriggered = true;
                await CardPileCmd.Draw(choice, state.RedCatalystDraw, player);
                state.NextAttackDiscount += Math.Max(1, state.RedCatalystStacks);
                RefreshHandAttackCosts(player);
            }
        }

        if (!play.Card.EnergyCost.CostsX && play.Card.EnergyCost.GetWithModifiers(CostModifiers.None) >= 2)
        {
            state.HighPrintedPlays++;
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

        return state.BlockGains == 0 ? 1 + state.FirstBlockPercent / 100m : 1;
    }

    public override decimal ModifyDamageAdditive(Creature? target, decimal amount, ValueProp props,
        Creature? dealer, CardModel? source, CardPlay? play)
    {
        if (!props.IsPoweredAttack() || dealer?.Player is not { } player || WitchCombatState.Find(player) is not { } state)
        {
            return 0;
        }

        var crown = WitchRules.IsBelowHalf(dealer.CurrentHp, dealer.MaxHp) ? state.CrownDamage : 0;
        if (play?.Card.Type == CardType.Attack)
        {
            return crown + state.ActiveAttackBonus + state.ActiveSingleHitBonus;
        }
        return crown;
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

    public override async Task BeforeFlush(PlayerChoiceContext choice, Player player)
    {
        if (WitchCombatState.Find(player) is { } state)
        {
            state.LastRemainingEnergy = player.PlayerCombatState!.Energy;
            if (state.RetainSelectCount > 0 && !CombatManager.Instance.IsOverOrEnding)
            {
                var candidates = player.PlayerCombatState.Hand.Cards.Where(card => !card.ShouldRetainThisTurn).ToList();
                if (candidates.Count > 0)
                {
                    var max = Math.Min(state.RetainSelectCount, candidates.Count);
                    var prefs = new CardSelectorPrefs(new LocString("card_selection", "SPIRELAB-WITCH_FRONTISPIECE_RETAIN"), 0, max)
                    {
                        Cancelable = true,
                        RequireManualConfirmation = true,
                        PretendCardsCanBePlayed = true
                    };
                    var chosen = await CardSelectCmd.FromHand(choice, player, prefs, card => !card.ShouldRetainThisTurn, this);
                    foreach (var card in chosen)
                    {
                        CardCmd.ApplySingleTurnRetain(card);
                    }
                }
            }
        }
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

    public override Task BeforeDeath(Creature creature)
    {
        CrimsonFinalMirror.RememberDefeat(creature);
        return Task.CompletedTask;
    }

    public override Task AfterCombatVictory(CombatRoom room) => CrimsonFinalMirror.Play(room);

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        foreach (var player in room.CombatState.Players)
        {
            if (WitchCombatState.Find(player) is not { } state)
            {
                continue;
            }

            // The engine calls AfterCombatEnd only on victory, before writing the finished-room save.
            var paid = state.GamblePaidThisTurn;
            state.GamblePaidThisTurn = 0;
            if (paid > 0 && player.Creature.IsAlive)
            {
                var creature = player.Creature;
                await CreatureCmd.SetCurrentHp(creature, WitchRules.RefundLife(creature.CurrentHp, creature.MaxHp, paid));
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
        if (card.Type == CardType.Attack && !card.EnergyCost.CostsX
            && WitchCombatState.Find(card.Owner) is { NextAttackDiscount: > 0 } attackState)
        {
            modifiedCost = Math.Max(0, modifiedCost - attackState.NextAttackDiscount);
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
        WitchCombatState.Find(target?.Player) is { } state &&
        (state.SlothImmunity || (EnemyAttack(dealer, props) && state.Immunity.HasValue)) ? 0 : decimal.MaxValue;

    public override Task BeforeDamageReceived(PlayerChoiceContext choice, Creature target, decimal amount,
        ValueProp props, Creature? dealer, CardModel? cardSource)
    {
        if (EnemyAttack(dealer, props) && WitchCombatState.Find(target.Player) is { } state)
        {
            if (state.SlothImmunity)
            {
                return Task.CompletedTask;
            }
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
        // U14 Blue Flame Formula buffs only a single powered attack damage event.
        if (props.IsPoweredAttack() && dealer?.Player is { } attacker
            && WitchCombatState.Find(attacker) is { ActiveSingleHitBonus: > 0 } attackerState)
        {
            attackerState.ActiveSingleHitBonus = 0;
        }

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
        var percent = (state.MirrorForm ? 100 : 0) + state.BaseReflectPercent + state.TempReflectPercent + state.NextReflectPercent;
        var fixedDamage = state.DeferredReflections.Sum();
        state.DeferredReflections.Clear();
        state.NextReflectPercent = 0;

        if (!state.FirstCombatBlockTriggered && target.Player!.Relics.OfType<SilverMirrorFrame>().FirstOrDefault() is { } frame)
        {
            state.FirstCombatBlockTriggered = true;
            frame.Flash();
            fixedDamage += 5;
        }
        if (state.WeakOnBlock > 0)
        {
            var weak = state.WeakOnBlock;
            state.WeakOnBlock = 0;
            await PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.WeakPower>(choice, dealer, weak, target, null);
        }

        // One response per native DamageResult. An unpowered response has no Move flag.
        var reflection = WitchRules.Reflect(result.BlockedDamage, percent, fixedDamage);
        if (reflection > 0 && dealer.IsAlive)
        {
            WitchFeedback.Reflect(dealer);
            var hits = await CreatureCmd.Damage(choice, dealer, reflection, ValueProp.Unpowered, target);
            if (hits.Any(hit => hit.TotalDamage > 0))
            {
                if (!state.Reflected)
                {
                    state.NextDraw += state.ShadowDraw;
                    if (state.InscriptionReflectGain > 0)
                    {
                        state.BaseReflectPercent = WitchRules.AddCappedReflect(state.BaseReflectPercent, state.InscriptionReflectGain);
                    }
                }
                state.Reflected = true;
                state.ReflectedDuringEnemyTurn |= target.CombatState?.CurrentSide == CombatSide.Enemy;
            }
        }
        await WitchStatuses.Refresh(choice, target.Player!, state);
    }
}
