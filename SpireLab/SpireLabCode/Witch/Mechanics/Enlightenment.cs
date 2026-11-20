using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class Enlightenment
{
    public static async Task ChooseOpeningCard(PlayerChoiceContext choice, Player player, CombatState combat)
    {
        var state = WitchCombatState.For(player);
        if (state.OpeningChoiceResolved || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        state.OpeningChoiceResolved = true;
        var rewards = player.Character.CardPool.AllCards.OfType<WitchCard>()
            .Where(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare)
            .OrderBy(card => card.SpecId, StringComparer.Ordinal).Select(card => combat.CreateCard(card, player)).ToArray();
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "SPIRELAB-WITCH_OPENING_CHOICE"), 1)
        {
            RequireManualConfirmation = true
        };
        try
        {
            var selected = (await CardSelectCmd.FromSimpleGrid(choice, rewards, player, prefs)).SingleOrDefault();
            if (selected == null || CombatManager.Instance.IsOverOrEnding)
            {
                return;
            }

            // A uniform slot in the already shuffled deck includes this card in the initial permutation.
            var added = await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Random, skipVisuals: true);
            if (added.success && selected.Pile?.Type == PileType.Draw)
            {
                state.OpeningCard = selected;
                WitchFeedback.Enlighten(player.Creature);
            }
        }
        finally
        {
            // The grid needs an owner on each display instance. Unchosen cards never enter a pile.
            foreach (var candidate in rewards.Where(card => card.Pile == null && !card.HasBeenRemovedFromState))
            {
                candidate.RemoveFromState();
            }
        }
    }

    public static async Task ChooseSwap(PlayerChoiceContext choice, Player player, AbstractModel source, bool fromTrait = true)
    {
        if (!CanSwap(player, fromTrait) || CandidateCards(player.PlayerCombatState!).FirstOrDefault() == null)
        {
            return;
        }

        var prefs = new CardSelectorPrefs(new LocString("card_selection", "SPIRELAB-WITCH_ENLIGHTENMENT"), 0, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
            PretendCardsCanBePlayed = true
        };
        var selected = (await CardSelectCmd.FromHand(choice, player, prefs, card => card != source, source)).SingleOrDefault();
        if (selected != null)
        {
            await Swap(player, selected, fromTrait);
        }
    }

    public static async Task<CardModel?> Swap(Player player, CardModel outgoing, bool fromTrait = true)
    {
        if (!CanSwap(player, fromTrait) || outgoing.Owner != player || outgoing.Pile?.Type != PileType.Hand)
        {
            return null;
        }

        var candidates = CandidateCards(player.PlayerCombatState!).ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var incoming = candidates[player.RunState.Rng.CombatCardSelection.NextInt(candidates.Length)];
        var incomingPile = incoming.Pile!;
        // Vacate a hand slot first so a full hand does not send the incoming card to discard.
        var movedOut = await CardPileCmd.Add(outgoing, incomingPile);
        if (!movedOut.success || outgoing.Pile != incomingPile)
        {
            return null;
        }

        var movedIn = await CardPileCmd.Add(incoming, PileType.Hand);
        if (!movedIn.success || incoming.Pile?.Type != PileType.Hand)
        {
            if (!CombatManager.Instance.IsOverOrEnding)
            {
                await CardPileCmd.Add(outgoing, PileType.Hand);
            }
            return null;
        }

        var state = WitchCombatState.For(player);
        state.TraitSwapUsed |= fromTrait;
        WitchFeedback.Enlighten(player.Creature);
        state.TurnEnlightenments++;
        state.CombatEnlightenments++;
        if (state.CombatEnlightenments == 1 && player.Relics.OfType<CrackedVanityMirror>().FirstOrDefault() is { } starter)
        {
            await starter.FlashAndBlock(3);
        }
        if (state.TurnEnlightenments == 1 && player.Relics.OfType<TwinStandingMirror>().FirstOrDefault() is { } upgraded)
        {
            await upgraded.FlashAndBlock(3);
        }

        return incoming;
    }

    private static bool CanSwap(Player player, bool fromTrait) =>
        !CombatManager.Instance.IsOverOrEnding && player.PlayerCombatState?.Hand.IsEmpty == false
        && (!fromTrait || !WitchCombatState.For(player).TraitSwapUsed);

    private static IEnumerable<CardModel> CandidateCards(PlayerCombatState combat) =>
        combat.DrawPile.Cards.Concat(combat.DiscardPile.Cards);
}
