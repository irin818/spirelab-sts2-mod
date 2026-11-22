using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using SpireLab.SpireLabCode.Witch.UI;
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
            .Where(card => card.IsInRewardPool)
            .OrderBy(card => card.SpecId, StringComparer.Ordinal).Cast<CardModel>().ToArray();
        var model = await PickOpeningModel(choice, player, rewards);
        if (model == null || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        // The grid previews canonical definitions. Only the confirmed model enters combat.
        var selected = combat.CreateCard(model, player);
        var added = await CardPileCmd.Add(selected, PileType.Draw, CardPilePosition.Random, skipVisuals: true);
        if (added.success && selected.Pile?.Type == PileType.Draw)
        {
            state.OpeningCard = selected;
            WitchFeedback.Enlighten(player.Creature);
        }
        else if (selected.Pile == null && !selected.HasBeenRemovedFromState)
        {
            selected.RemoveFromState();
        }
    }

    private static async Task<CardModel?> PickOpeningModel(PlayerChoiceContext choice, Player player, CardModel[] rewards)
    {
        if (CardSelectCmd.Selector != null || CardSelectCmd.LocalSelector != null)
        {
            var prefs = new CardSelectorPrefs(new LocString("card_selection", "SPIRELAB-WITCH_OPENING_CHOICE"), 1)
            {
                RequireManualConfirmation = true
            };
            return (await CardSelectCmd.FromSimpleGrid(choice, rewards, player, prefs)).SingleOrDefault();
        }

        // Follow CardSelectCmd.FromSimpleGrid's synchronized index protocol. Filtering is local display only.
        var sync = RunManager.Instance.PlayerChoiceSynchronizer;
        var choiceId = sync.ReserveChoiceId(player);
        await choice.SignalPlayerChoiceBegun(player, PlayerChoiceOptions.None);
        try
        {
            if (!LocalContext.IsMe(player) || RunManager.Instance.NetService.Type == NetGameType.Replay)
            {
                var indexes = (await sync.WaitForRemoteChoice(player, choiceId)).AsIndexes();
                return indexes.Select(index => rewards[index]).SingleOrDefault();
            }

            NPlayerHand.Instance?.CancelAllCardPlay();
            var screen = WitchFateScreen.Create(rewards, player);
            (NOverlayStack.Instance ?? throw new InvalidOperationException("Missing Fate overlay stack.")).Push(screen);
            var model = (await screen.CardsSelected()).Single();
            sync.SyncLocalChoice(player, choiceId, PlayerChoiceResult.FromIndexes([Array.IndexOf(rewards, model)]));
            return model;
        }
        finally
        {
            await choice.SignalPlayerChoiceEnded();
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
