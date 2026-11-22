using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Random;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

internal static class WitchGreedSmoke
{
    public static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var game = NGame.Instance!;
            await game.GameStartupComplete;
            var startup = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet)
                .Where(path => ResourceLoader.Exists(path)).Distinct().ToArray();
            await WaitFor(tree, () => PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(startup), "menu preload");
            if (NModalContainer.Instance?.OpenModal is NEarlyAccessDisclaimer disclaimer)
            {
                await disclaimer.CloseScreen();
            }
            SaveManager.Instance.SetFtuesEnabled(false);
            var selector = new WitchSelector();
            using var selection = CardSelectCmd.UseSelector(selector);
            var run = await game.StartNewSingleplayerRun(ModelDb.Character<TheWitch>(), false,
                ActModel.GetDefaultList(), [], "PRIMALGREED", GameMode.Standard);
            var player = run.Players.Single();
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
            var piles = player.PlayerCombatState!;
            var choice = new BlockingPlayerChoiceContext();
            await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "real combat begins");

            await CreatureCmd.SetCurrentHp(player.Creature, 34);
            var greed = await Add<PrimalGreed>(player);
            await ParkOtherCards(player, greed);
            for (var i = 0; i < 2; i++)
            {
                await Add<ManaSeal>(player);
                await AddToDraw(player);
            }
            var before = Snapshot(player);
            var choicesBefore = selector.HandChoices;
            await PlayThroughQueue(tree, player, greed, cost: 2);
            VerifyPiles(player, before);
            Check(piles.Hand.Cards.Count == 5 && greed.Pile?.Type == PileType.Hand
                && selector.HandChoices == choicesBefore && !greed.Keywords.Contains(CardKeyword.Exhaust),
                $"at half real HP, draw five including Greed without a keep prompt (HP {player.Creature.CurrentHp}/{player.Creature.MaxHp}, hand {piles.Hand.Cards.Count}, Greed {greed.Pile?.Type}, choices {selector.HandChoices - choicesBefore})");
            if (System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_GREED_CAPTURE") is { } path)
            {
                await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                Check(tree.Root.GetTexture().GetImage().SavePng(path) == Error.Ok, "screenshot shows Greed back in the live hand");
            }
            await PlayThroughQueue(tree, player, greed, cost: 2);
            VerifyPiles(player, before);
            Check(piles.Hand.Cards.Count == 5 && greed.Pile?.Type == PileType.Hand,
                "the re-drawn Greed plays again without duplication or premature cleanup");

            var didMiss = false;
            for (var fixture = 0; fixture < 8 && !didMiss; fixture++)
            {
                await ParkOtherCards(player, greed);
                var draw = new List<CardModel>();
                for (var i = 0; i < 12; i++)
                {
                    draw.Add(await AddToDraw(player));
                }
                var predicted = draw.Append(greed).ToList();
                new Rng(player.RunState.Rng.Shuffle.ToSerializable()).Shuffle(predicted);
                if (predicted.IndexOf(greed) < 5)
                {
                    // Consume the real shuffle and advance the seeded RNG for the next fixture.
                    await PlayThroughQueue(tree, player, greed, cost: 2);
                    continue;
                }
                before = Snapshot(player);
                await PlayThroughQueue(tree, player, greed, cost: 2);
                VerifyPiles(player, before);
                Check(piles.Hand.Cards.SequenceEqual(predicted.Take(5)) &&
                    greed.Pile?.Type == PileType.Draw && !piles.DiscardPile.Cards.Contains(greed),
                    "when the native RNG places Greed below the first five, it stays in draw order");
                didMiss = true;
            }
            Check(didMiss, "native seeded shuffle supplied a deterministic not-drawn fixture");

            var enhanced = await Add<PrimalGreed>(player, upgrade: true);
            await ParkOtherCards(player, enhanced);
            await CreatureCmd.SetCurrentHp(player.Creature, 33);
            selector.SelectFirstHand = true;
            var kept = await Add<ManaSeal>(player);
            await AddToDraw(player);
            Check(CardCmd.Enchant<RoyallyApproved>(enhanced, 1) != null, "Greed accepts a native skill enchantment");
            before = Snapshot(player);
            choicesBefore = selector.HandChoices;
            await PlayThroughQueue(tree, player, enhanced, cost: 1);
            VerifyPiles(player, before);
            Check(selector.HandChoices == choicesBefore + 1 && piles.Hand.Cards.First() == kept && !kept.ShouldRetainThisTurn
                && enhanced.Pile?.Type == PileType.Hand && enhanced.IsUpgraded && enhanced.Enchantment is RoyallyApproved,
                "low real HP keeps one other card this play and preserves the upgraded enchanted instance");

            await CreatureCmd.SetCurrentHp(player.Creature, 68);
            selector.SelectFirstHand = false;
            await Play<FallenConfidence>(player, choice);
            await PlayThroughQueue(tree, player, enhanced, cost: 0);
            Check(enhanced.Pile?.Type == PileType.Hand && enhanced.EnergyCost.GetWithModifiers(CostModifiers.All) == 0,
                "Fallen Confidence allows an upgraded Greed to return and play at zero cost");

            // Calamity offers three of four Desire cards when upgraded. Advance its native
            // selection RNG if Greed is not yet among the three.
            selector.PreferredDesire = typeof(PrimalGreed);
            PrimalGreed? generated = null;
            for (var i = 0; i < 8 && generated == null; i++)
            {
                await Play<SourceOfCalamity>(player, choice, upgrade: true);
                generated = selector.LastDesire as PrimalGreed;
            }
            Check(generated is not null && generated != enhanced && generated.Pile?.Type == PileType.Hand,
                "Calamity generates a unique Greed through the real card selection");
            var calamityGreed = generated!;
            await ParkOtherCards(player, calamityGreed);
            before = Snapshot(player);
            await PlayThroughQueue(tree, player, calamityGreed, cost: 0);
            VerifyPiles(player, before);
            Check(calamityGreed.Pile?.Type == PileType.Hand && !player.Deck.Cards.Contains(calamityGreed),
                "Calamity's temporary zero-cost Greed re-draws itself and stays out of the permanent deck");
            for (var i = 1; i < CardPile.MaxCardsInHand; i++)
            {
                await Add<ManaSeal>(player);
            }
            before = Snapshot(player);
            await PlayThroughQueue(tree, player, calamityGreed, cost: 0);
            VerifyPiles(player, before);
            Check(piles.Hand.Cards.Count == 5 && player.Deck.Cards.Count == 10, "playing from a full hand draws legally");
            var turn = piles.TurnNumber;
            PlayerCmd.EndTurn(player, canBackOut: false);
            await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "native turn transition");
            Check(calamityGreed.EnergyCost.GetWithModifiers(CostModifiers.All) == 1,
                "Calamity discount expires next turn while Fallen Confidence still applies");
            var loaded = RunState.FromSerializable(RunManager.Instance.ToSave(null)).Players.Single();
            Check(loaded.Deck.Cards.Count == 10 && loaded.Deck.Cards.All(card => card is not PrimalGreed),
                "save round trip excludes the combat-only Greed instances");
            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }

    private static async Task PlayThroughQueue(SceneTree tree, Player player, PrimalGreed card, int cost)
    {
        await PlayerCmd.SetEnergy(3, player);
        var history = CombatManager.Instance.History;
        var finished = history.CardPlaysFinished.Count();
        Check(card.TryManualPlay(null), "Greed enters the native action queue");
        await WaitFor(tree, () => history.CardPlaysFinished.Count() > finished && !RunManager.Instance.ActionExecutor.IsRunning,
            "Greed finishes its native action including pile cleanup");
        Check(history.CardPlaysFinished.Count() == finished + 1 && player.PlayerCombatState!.Energy == 3 - cost,
            $"Greed resolves once and costs {cost} energy");
    }

    private static async Task<CardModel> AddToDraw(Player player)
    {
        var card = ((CombatState)player.Creature.CombatState!).CreateCard<ManaSeal>(player);
        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player);
        return card;
    }

    private static Task ParkOtherCards(Player player, CardModel active)
    {
        var piles = player.PlayerCombatState!;
        var others = piles.Hand.Cards.Concat(piles.DrawPile.Cards).Concat(piles.DiscardPile.Cards)
            .Where(card => card != active).ToArray();
        return CardPileCmd.Add(others, PileType.Exhaust);
    }

    private static CardModel[] Snapshot(Player player)
    {
        var piles = player.PlayerCombatState!;
        return piles.Hand.Cards.Concat(piles.DrawPile.Cards).Concat(piles.PlayPile.Cards)
            .Concat(piles.DiscardPile.Cards).Concat(piles.ExhaustPile.Cards).ToArray();
    }

    private static void VerifyPiles(Player player, CardModel[] before)
    {
        var after = Snapshot(player);
        Check(after.Length == before.Length && after.Distinct(ReferenceEqualityComparer.Instance).Count() == after.Length
            && after.ToHashSet(ReferenceEqualityComparer.Instance).SetEquals(before),
            "every original card still occupies exactly one combat pile");
    }
}
