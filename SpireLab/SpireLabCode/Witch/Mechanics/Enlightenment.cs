using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.ValueProps;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class Enlightenment
{
    public static async Task Choose(PlayerChoiceContext choice, Player player, AbstractModel source, int maximum = 1)
    {
        if (player.Deck.IsEmpty || player.PlayerCombatState?.Hand.IsEmpty != false || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(new LocString("card_selection", "SPIRELAB-WITCH_ENLIGHTENMENT"), 0, maximum)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
            PretendCardsCanBePlayed = true
        };
        var cards = await CardSelectCmd.FromHand(choice, player, prefs, card => card != source, source);
        foreach (var card in cards.ToArray())
        {
            await Replace(player, card);
        }
    }

    public static async Task<CardModel?> Replace(Player player, CardModel source)
    {
        if (source.Owner != player || player.Creature.CombatState is not CombatState combat || source.Pile?.Type != PileType.Hand
            || CombatManager.Instance.IsOverOrEnding)
        {
            return null;
        }

        var pool = player.Deck.Cards.Where(card => card.Rarity is not (CardRarity.Token or CardRarity.Status)).ToArray();
        if (pool.Length == 0)
        {
            return null;
        }

        var state = WitchCombatState.For(player);
        var hand = player.PlayerCombatState!.Hand;
        var index = hand.Cards.ToList().IndexOf(source);
        var sample = pool[player.RunState.Rng.CombatCardSelection.NextInt(pool.Length)];
        var substitute = combat.CloneCard(sample);
        CardModel? heldSource = source;
        if (state.StandIns.TryGetValue(source, out var original))
        {
            await Release(player, state, source, original);
            heldSource = null;
        }
        else
        {
            var held = await CardPileCmd.Add(source, EnlightenmentPile.HeldType);
            if (!held.success)
            {
                substitute.RemoveFromState();
                return null;
            }
        }

        state.HandInsertions.Add(substitute, index);
        state.StandIns.Add(substitute, heldSource);
        var added = await CardPileCmd.AddGeneratedCardToCombat(substitute, PileType.Hand, player);
        state.HandInsertions.Remove(substitute);
        if (!added.success || substitute.Pile?.Type != PileType.Hand)
        {
            await Release(player, state, substitute, heldSource);
            return null;
        }

        WitchFeedback.Enlighten(player.Creature);
        state.TurnEnlightenments++;
        state.CombatEnlightenments++;
        if (state.CombatEnlightenments == 1 && player.Relics.OfType<CrackedVanityMirror>().FirstOrDefault() is { } starter)
        {
            await starter.FlashAndBlock(3);
        }

        if (state.TurnEnlightenments == 1)
        {
            if (player.Relics.OfType<TwinStandingMirror>().FirstOrDefault() is { } upgraded)
            {
                await upgraded.FlashAndBlock(3);
            }
            if (state.EnlightenmentBlock > 0)
            {
                await CreatureCmd.GainBlock(player.Creature, state.EnlightenmentBlock, ValueProp.Unpowered, null);
            }
            if (state.EnlightenmentDiscount)
            {
                substitute.EnergyCost.AddThisTurn(-1, reduceOnly: true);
            }
        }

        return substitute;
    }

    public static async Task Release(Player player, WitchCombatState state, CardModel standIn, CardModel? original)
    {
        if (!state.StandIns.Remove(standIn))
        {
            return;
        }

        if (standIn.Pile?.Type is PileType.Hand or PileType.Play)
        {
            // Move any visible holder away before the silent cleanup. Discard and Exhaust
            // have already completed their own holder transitions when their pile hook fires.
            await CardPileCmd.Add(standIn, EnlightenmentPile.HeldType);
        }
        if (standIn.Pile?.IsCombatPile == true)
        {
            await CardPileCmd.RemoveFromCombat(standIn, skipVisuals: true);
        }

        if (original?.Pile?.Type == EnlightenmentPile.HeldType && player.Creature.IsAlive
            && CombatManager.Instance.IsInProgress)
        {
            await CardPileCmd.Add(original, PileType.Discard);
        }
    }

    public static async Task ReleaseAll(Player player, WitchCombatState state)
    {
        foreach (var (standIn, original) in state.StandIns.ToArray())
        {
            await Release(player, state, standIn, original);
        }
    }
}
