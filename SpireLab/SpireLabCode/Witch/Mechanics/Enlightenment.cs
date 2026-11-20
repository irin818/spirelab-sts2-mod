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
        var cards = await CardSelectCmd.FromHand(choice, player, prefs, filter: null, source);
        foreach (var card in cards.ToArray())
        {
            await Replace(player, card);
        }
    }

    public static async Task<CardModel?> Replace(Player player, CardModel source)
    {
        if (player.Creature.CombatState is not CombatState combat || source.Pile?.Type != PileType.Hand
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
        if (state.StandIns.TryGetValue(source, out var original))
        {
            await Release(player, state, source, original);
            source = null!;
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
        state.StandIns.Add(substitute, source);
        var added = await CardPileCmd.AddGeneratedCardToCombat(substitute, PileType.Hand, player);
        state.HandInsertions.Remove(substitute);
        if (!added.success || substitute.Pile?.Type != PileType.Hand)
        {
            await Release(player, state, substitute, source);
            return null;
        }

        state.TurnEnlightenments++;
        state.CombatEnlightenments++;
        if (state.CombatEnlightenments == 1 && player.Relics.Any(relic => relic is CrackedVanityMirror))
        {
            await CreatureCmd.GainBlock(player.Creature, 3, ValueProp.Unpowered, null);
        }

        return substitute;
    }

    public static async Task Release(Player player, WitchCombatState state, CardModel standIn, CardModel? original)
    {
        if (!state.StandIns.Remove(standIn))
        {
            return;
        }

        if (standIn.Pile?.IsCombatPile == true)
        {
            await CardPileCmd.RemoveFromCombat(standIn);
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
