using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.GameOverScreen;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Cards;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.Smoke;

internal static class WitchSmoke
{
    public static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var game = NGame.Instance ?? throw new InvalidOperationException("Expected the game root.");
            await game.GameStartupComplete;
            var commonScenes = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet)
                .Where(path => path.EndsWith(".tscn", StringComparison.Ordinal) && ResourceLoader.Exists(path)).Distinct().ToArray();
            await WaitFor(tree, () =>
            {
                var loaded = PreloadManager.Cache.GetLoadedCacheAssets();
                return commonScenes.All(loaded.Contains);
            }, "main menu and common scenes finish preloading");
            SaveManager.Instance.SetFtuesEnabled(false);
            var witch = ModelDb.Character<TheWitch>();
            Check(ModelDb.AllCharacters.Where(character => character.Id.Entry.StartsWith("SPIRELAB-", StringComparison.Ordinal))
                .SequenceEqual([witch]), "Witch is the only installed SpireLab character");
            var run = await game.StartNewSingleplayerRun(witch, false, ActModel.GetDefaultList(), [], "682026", GameMode.Standard);
            var player = run.Players.Single();
            Check(witch.StartingHp == 68 && player.Deck.Cards.Count == 10 && player.Deck.Cards.Select(card => card.Id).Distinct().Count() == 4, "Witch starting deck and HP");
            Check(player.Relics.Single() is CrackedVanityMirror, "Witch starting relic");
            Check(witch.CardPool.AllCards.Count(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare) == 85
                && witch.CardPool.AllCards.Count(card => card.Tags.Contains(WitchCard.Desire)) == 5, "85 reward cards and exactly five Desire cards");
            var portraits = witch.CardPool.AllCards.Cast<WitchCard>().ToArray();
            Check(portraits.Select(card => card.SpecId).Distinct().Count() == 91
                && portraits.Select(card => card.PortraitPath).Distinct().Count() == 91
                && portraits.All(card =>
                {
                    var art = ResourceLoader.Load<Texture2D>(card.PortraitPath);
                    return art != null && (card.Rarity == CardRarity.Ancient
                        ? art.GetSize() == new Vector2(606, 852) : art.GetSize() == new Vector2(500, 380));
                }), "91 unique card assets load at their expected normal and ancient sizes");
            Check(new[] { "enlighten", "mirror", "reflect", "desire", "reserve" }.All(glyph =>
                ResourceLoader.Load<Texture2D>($"{TheWitch.Art}/abilities/{glyph}.svg")?.GetSize() == new Vector2(64, 64)),
                "five custom ability glyphs load from the resource pack");

            WitchContentSmoke.ValidateDefinitions(witch, player);
            var selector = new WitchSelector();
            using var selection = CardSelectCmd.UseSelector(selector);
            var room = await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
            var piles = player.PlayerCombatState!;
            await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "Witch enters play phase");
            var choice = new ThrowingPlayerChoiceContext();
            var state = WitchCombatState.For(player);
            var creature = player.Creature;
            var enemy = creature.CombatState!.HittableEnemies.First();
            Check(piles.Hand.Cards.Count == 6 && state.CombatEnlightenments == 0 && creature.Block == 0, "passive sixth draw, skip gives no credit or Block");

            var original = piles.Hand.Cards[2];
            var proxy = await Enlightenment.Replace(player, original) ?? throw new InvalidOperationException("No substitute");
            Check(ReferenceEquals(piles.Hand.Cards[2], proxy) && original.Pile?.Type == EnlightenmentPile.HeldType
                && piles.Hand.Cards.Count == 6 && state.CombatEnlightenments == 1 && creature.Block == 3, "same-slot swap with held original and once-per-combat Block");
            Check(player.Deck.Cards.Count == 10 && !player.Deck.Cards.Contains(proxy), "permanent deck unchanged");
            await CardCmd.Discard(choice, proxy);
            Check(proxy.HasBeenRemovedFromState && piles.DiscardPile.Cards.Contains(original) && state.StandIns.Count == 0, "discarded substitute restores original");
            var second = piles.Hand.Cards[1];
            var nextProxy = await Enlightenment.Replace(player, second) ?? throw new InvalidOperationException("No substitute");
            var chained = await Enlightenment.Replace(player, nextProxy) ?? throw new InvalidOperationException("No chained substitute");
            Check(nextProxy.HasBeenRemovedFromState && piles.DiscardPile.Cards.Contains(second) && state.StandIns.Count == 1 && creature.Block == 3,
                "retransformation releases original without copying it or re-awarding relic Block");

            var attack = await Add<StarterDraught>(player);
            var beforeEnemy = enemy.CurrentHp;
            var beforeEnergy = piles.Energy;
            Check(attack.TryManualPlay(enemy), "native action queue accepts a Witch attack");
            await WaitFor(tree, () => attack.Pile?.Type == PileType.Discard && piles.Energy == beforeEnergy - 1, "manual attack pays energy and finishes");
            Check(enemy.CurrentHp < beforeEnemy && state.NextEnergy == 1, "attack deals damage and schedules extra energy");
            await Play<PinnacleOfMana>(player, choice);
            var remaining = piles.Energy;
            var turn = piles.TurnNumber;
            selector.SelectFirstHand = true;
            CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
            await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "actual enemy/player turn transition");
            Check(chained.HasBeenRemovedFromState && state.CombatEnlightenments == 4 && state.TurnEnlightenments == 1
                && piles.Energy == remaining + 4 && state.ExtraEnergy == 1, "turn expiry, one new passive transform and retained energy");
            await Enlightenment.ReleaseAll(player, state);

            await CreatureCmd.SetCurrentHp(creature, 20);
            var visuals = creature.GetCreatureNode()!.Visuals;
            var healthAnimation = visuals.GetNode<AnimationPlayer>("HealthAnimation");
            var combatAnimation = visuals.GetNode<AnimationPlayer>("AnimationPlayer");
            Check(healthAnimation.CurrentAnimation == "low_health", "low-health animation plays below half real HP");
            var castPlayed = false;
            void DetectCast(StringName animation) => castPlayed |= animation == "cast";
            combatAnimation.AnimationStarted += DetectCast;
            await Play<TheMirrorSelf>(player, choice, upgrade: true);
            combatAnimation.AnimationStarted -= DetectCast;
            Check(castPlayed && combatAnimation.AnimationGetNext("cast") == "idle", "Witch power casts with a return to idle");
            Check(creature.CurrentHp == 20 && state.MirrorHp == 28, "mirror inversion at 68 max and 20 real HP");
            await Play<TheMirrorSelf>(player, choice);
            Check(creature.CurrentHp == 20 && state.MirrorHp == 28, "mirror inversion does not repeat");
            await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
            await Play<ThinMirror>(player, choice);
            var foe = enemy.CurrentHp;
            var blockFlash = false;
            var effects = creature.GetVfxContainer()!;
            void DetectBlockFlash(Node child) => blockFlash |= child.Name == "WitchBlockFlash";
            effects.ChildEnteredTree += DetectBlockFlash;
            var hit = (await CreatureCmd.Damage(choice, creature, 12, ValueProp.Move, enemy)).Single();
            effects.ChildEnteredTree -= DetectBlockFlash;
            Check(blockFlash, "blocking an attack shows the Witch block flash");
            Check(hit.BlockedDamage == 7 && creature.CurrentHp == 20 && state.MirrorHp == 23 && enemy.CurrentHp == foe - 5,
                "7 Block absorbs attack, 5 mirror HP absorbs overflow, 3+2 reflection");
            var poisonLike = (await CreatureCmd.Damage(choice, creature, 4, ValueProp.Unblockable | ValueProp.Unpowered,
                dealer: null, cardSource: null, cardPlay: null)).Single();
            Check(poisonLike.BlockedDamage == 0 && creature.CurrentHp == 20 && state.MirrorHp == 19,
                "unblockable status damage still consumes mirror HP without counting as Block");
            await CreatureCmd.Heal(creature, 100);
            Check(creature.CurrentHp + state.MirrorHp == 68, "healing and mirror HP obey effective cap");
            Check(!healthAnimation.IsPlaying() && creature.GetCreatureNode()!.Body.SelfModulate == Colors.White,
                "healing resumes normal sprite color");

            await CreatureCmd.SetCurrentHp(creature, 5);
            var gambling = await Add<AbyssOfGambling>(player);
            await Play<FallenConfidence>(player, choice);
            await Play<FallenConfidence>(player, choice);
            Check(gambling.EnergyCost.GetWithModifiers(CostModifiers.All) == 0
                && (await Add<Sloth>(player)).EnergyCost.GetWithModifiers(CostModifiers.All) == 1, "Desire discount only once");
            var blood = creature.CurrentHp;
            var mirror = state.MirrorHp;
            var mana = piles.Energy;
            var actions = RunManager.Instance.ActionExecutor;
            for (var i = 0; i < 2; i++)
            {
                Check(gambling.TryManualPlay(null), "gamble payable");
                blood -= WitchRules.GambleCost(blood, 50);
                await WaitFor(tree, () => !actions.IsRunning && gambling.Pile?.Type == PileType.Hand && creature.CurrentHp == blood
                    && state.MirrorHp == mirror && piles.Energy == mana + 2 * (i + 1), "gamble pays real HP, gains energy and returns");
            }
            Check(creature.CurrentHp == 1 && !gambling.CanPlay(), "gamble stops at 1 real HP");

            await Play<SourceOfCalamity>(player, choice, upgrade: true);
            Check(selector.DesireOptions == 3 && selector.LastDesire?.EnergyCost.GetWithModifiers(CostModifiers.All) == 0
                && selector.LastDesire?.Pile?.Type == PileType.Hand && player.Deck.Cards.Count == 10, "Calamity offers three distinct other Desires at zero for the turn");

            var recovery = await Add<Recovery>(player);
            Check(recovery.TryManualPlay(null), "Recovery enters the native queue");
            await WaitFor(tree, () => !actions.IsRunning && recovery.Pile?.Type == PileType.Exhaust && creature.CurrentHp == 5,
                "Recovery heals four real HP and exhausts");
            var elixir = await Add<ElixirPhial>(player);
            Check(elixir.TryManualPlay(null), "Elixir enters the native queue");
            await WaitFor(tree, () => !actions.IsRunning && elixir.Pile?.Type == PileType.Exhaust && creature.CurrentHp == 7,
                "Elixir heals two real HP and exhausts");
            var restartGamble = piles.Hand.Cards.OfType<AbyssOfGambling>().First();
            var healingBudgetMana = piles.Energy;
            Check(restartGamble.TryManualPlay(null), "Gamble is playable after healing");
            await WaitFor(tree, () => !actions.IsRunning && restartGamble.Pile?.Type == PileType.Hand && creature.CurrentHp == 3,
                "first Gamble funded by the healing budget returns to hand");
            Check(restartGamble.TryManualPlay(null), "Gamble is playable at three HP");
            await WaitFor(tree, () => !actions.IsRunning && restartGamble.Pile?.Type == PileType.Hand && creature.CurrentHp == 1,
                "second Gamble funded by the healing budget returns to hand");
            Check(state.MirrorHp == mirror && piles.Energy == healingBudgetMana + 4 && !restartGamble.CanPlay(),
                "two healing cards fund only two more Gamble plays");

            // The fixture kills the enemy directly. A player still sees native card animations
            // after the action queue settles, and the next debug room must not interrupt them.
            var combatRoom = NCombatRoom.Instance!;
            await tree.ToSignal(tree.CreateTimer(0.4), SceneTreeTimer.SignalName.Timeout);
            await WaitFor(tree, () => !combatRoom.CombatVfxContainer.GetChildren().Any(node => node is NCardFlyVfx)
                && !combatRoom.Ui.GetChildren().Any(node => node is NCardExhaustVfx), "native card effects settle before scripted combat win");
            await CreatureCmd.Damage(choice, enemy, 999, ValueProp.Unpowered, creature);
            await CombatManager.Instance.CheckWinCondition();
            await WaitFor(tree, () => !CombatManager.Instance.IsInProgress, "Witch wins a native battle");
            var reward = new RewardsSet(player).WithRewardsFromRoom(room);
            await reward.GenerateWithoutOffering();
            Check(reward.Rewards.OfType<CardReward>().Single().Cards.All(card => card.Pool == witch.CardPool), "Witch rewards belong to her pool");
            var restored = RunState.FromSerializable(RunManager.Instance.ToSave(null)).Players.Single();
            Check(restored.Character.Id == witch.Id && restored.Creature.CurrentHp == 1 && restored.Deck.Cards.Count == 10, "save carries real HP and permanent deck only");

            await CreatureCmd.SetCurrentHp(creature, 68);
            await LeaveCompletedCombat(tree);
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
            piles = player.PlayerCombatState!;
            await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "second real Witch battle");
            state = WitchCombatState.For(player);
            Check(!state.MirrorForm && state.MirrorHp == 0 && !state.DesireDiscount, "next battle resets temporary state");
            await Play<TheMirrorSelf>(player, choice);
            Check(creature.CurrentHp == 1 && state.MirrorHp == 0, "maximum-HP inversion cannot kill its owner");
            await WitchContentSmoke.Exercise(tree, player, choice);
            LocManager.Instance.SetLanguage("zhs");
            WitchContentSmoke.ValidateCardText(witch);
            Check(witch.Title.GetRawText() == "魔女" && ModelDb.Card<AbyssOfGambling>().Title == "狂赌之渊", "Simplified Chinese names loaded");
            LocManager.Instance.SetLanguage("eng");
            await ExerciseDeathAndShop(tree, game, player);
            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }

    internal static async Task LeaveCompletedCombat(SceneTree tree)
    {
        // Let deferred card holder recycling finish while the previous combat scene still exists.
        await RunManager.Instance.ProceedFromTerminalRewardsScreen();
        NOverlayStack.Instance?.Clear();
        for (var frame = 0; frame < 3; frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task ExerciseDeathAndShop(SceneTree tree, NGame game, Player player)
    {
        var creature = player.Creature;
        await CreatureCmd.Kill(creature);
        await WaitFor(tree, () => creature.IsDead && NOverlayStack.Instance?.Peek() is NGameOverScreen,
            "Witch death reaches the native game over screen");
        await game.ReturnToMainMenuAfterRun();
        Check(game.MainMenu != null && !RunManager.Instance.IsInProgress, "game over returns to a clean main menu");

        var run = await game.StartNewSingleplayerRun(ModelDb.Character<TheWitch>(), false,
            ActModel.GetDefaultList(), [], "WITCHSHOP68", GameMode.Standard);
        var restarted = run.Players.Single();
        Check(restarted.Creature.CurrentHp == 68 && restarted.Deck.Cards.Count == 10 && restarted.Relics.Single() is CrackedVanityMirror,
            "fresh Witch run restores initial life, deck and relic");
        await RunManager.Instance.EnterRoomDebug(RoomType.Shop, showTransition: false);
        var inventory = NMerchantRoom.Instance?.Inventory.Inventory
            ?? throw new InvalidOperationException("Expected a stocked merchant.");
        Check(inventory.CharacterCardEntries.Count == 5 && inventory.CharacterCardEntries.All(entry =>
            entry.CreationResult?.Card.Pool is WitchCardPool), "merchant stocks all five Witch card slots");
        var removal = inventory.CardRemovalEntry ?? throw new InvalidOperationException("Expected merchant card removal.");
        var cost = removal.Cost;
        var gold = restarted.Gold;
        using var selection = CardSelectCmd.UseSelector(new WitchSelector { SelectFirstHand = true });
        Check(await removal.OnTryPurchaseWrapper(inventory) && removal.Used
            && restarted.Deck.Cards.Count == 9 && restarted.Gold == gold - cost, "merchant removal updates the permanent deck and gold");
    }

    internal static async Task<T> Add<T>(Player player, bool upgrade = false) where T : CardModel
    {
        if (player.PlayerCombatState!.Hand.Cards.Count == CardPile.MaxCardsInHand)
        {
            await CardCmd.Discard(new ThrowingPlayerChoiceContext(), player.PlayerCombatState.Hand.Cards.First());
        }

        var combat = (CombatState)player.Creature.CombatState!;
        var card = combat.CreateCard<T>(player);
        if (upgrade)
        {
            CardCmd.Upgrade(card);
        }

        await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Hand, player);
        return card;
    }

    internal static async Task Play<T>(Player player, PlayerChoiceContext choice, bool upgrade = false) where T : CardModel =>
        await CardCmd.AutoPlay(choice, await Add<T>(player, upgrade), null);

    internal static async Task WaitFor(SceneTree tree, Func<bool> predicate, string check)
    {
        for (var frame = 0; frame < 2200 && !predicate(); frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        Check(predicate(), check);
    }

    internal static void Check(bool condition, string check)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Failed check: {check}");
        }

        GD.Print($"[SpireLabSmoke] OK {check}");
    }

    internal sealed class WitchSelector : ICardSelector
    {
        public bool SelectFirstHand { get; set; }
        public int DesireOptions { get; private set; }
        public CardModel? LastDesire { get; private set; }

        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect)
        {
            var cards = options.ToArray();
            if (cards.All(card => card.Pile == null && card is DesireCard))
            {
                if (cards.Length != cards.Select(card => card.Id).Distinct().Count() || cards.Any(card => card is SourceOfCalamity))
                {
                    throw new InvalidOperationException("Calamity offered duplicate cards or itself.");
                }

                DesireOptions = cards.Length;
                LastDesire = cards[0];
                return Task.FromResult<IEnumerable<CardModel>>([cards[0]]);
            }

            return Task.FromResult(SelectFirstHand ? cards.Take(1) : []);
        }

        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options, IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new InvalidOperationException("Unexpected reward choice.");
    }
}
