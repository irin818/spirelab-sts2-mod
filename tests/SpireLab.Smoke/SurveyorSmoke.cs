using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rewards;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SpireLab.SpireLabCode.Cards;
using SpireLab.SpireLabCode.Potions;
using SpireLab.SpireLabCode.Powers;
using SpireLab.SpireLabCode.Relics;
using SpireLab.Visuals;
using Surveyor = SpireLab.SpireLabCode.Character.SpireLab;

namespace SpireLab.Smoke;

[ModInitializer(nameof(Initialize))]
public static class SurveyorSmoke
{
    public static void Initialize()
    {
        if (OS.GetCmdlineArgs().Contains("--witch-campaign"))
        {
            Callable.From(() => { _ = WitchCampaign.Start(); }).CallDeferred();
        }
        else if (OS.GetCmdlineArgs().Contains("--witch-smoke"))
        {
            Callable.From(() => { _ = WitchSmoke.Run(); }).CallDeferred();
        }
        else if (OS.GetCmdlineArgs().Contains("--spirelab-smoke"))
        {
            Callable.From(() => { _ = Run(); }).CallDeferred();
        }
    }

    private static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var game = NGame.Instance ?? throw new InvalidOperationException("Expected the game root.");
            await game.GameStartupComplete;
            SaveManager.Instance.SetFtuesEnabled(false);
            var character = ModelDb.Character<Surveyor>();
            var run = await game.StartNewSingleplayerRun(character, false, ActModel.GetDefaultList(), [], "72026", GameMode.Standard);
            var player = run.Players.Single();
            Check(character.StartingDeck.Count() == 10, "starting deck");
            Check(character.StartingRelics.Single() is SurveyLantern, "starting relic");
            Check(character.CardPool.AllCards.Count(card => card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare) == 9,
                "standard reward cards");
            var rewardPool = character.CardPool.GetUnlockedCards(player.UnlockState, CardMultiplayerConstraint.SingleplayerOnly);
            Check(rewardPool.All(card => card.Rarity is not (CardRarity.Token or CardRarity.Status or CardRarity.Curse)),
                "generated cards excluded from rewards");
            Check(character.CardPool.AllCards.All(card => ResourceLoader.Exists(card.PortraitPath)), "card portrait resources");
            Check(ResourceLoader.Exists(character.CharacterSelectBg) && ResourceLoader.Exists(character.CustomVisualPath)
                && ResourceLoader.Exists(character.CustomRestSiteAnimPath) && ResourceLoader.Exists(character.CustomMerchantAnimPath),
                "selection, combat, rest site and merchant scene resources");

            var restored = RunState.FromSerializable(RunManager.Instance.ToSave(null)).Players.Single();
            Check(restored.Character.Id == character.Id && restored.Deck.Cards.Select(card => card.Id)
                .SequenceEqual(player.Deck.Cards.Select(card => card.Id))
                && restored.Relics.Single() is SurveyLantern, "run serialization round-trip");
            await RelicCmd.Obtain<BrassMeridian>(player);
            var room = await RunManager.Instance.EnterRoomDebug(RoomType.Monster,
                model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
            var piles = player.PlayerCombatState ?? throw new InvalidOperationException("Expected combat resources.");
            await WaitFor(tree, () => piles.Energy > 0 && piles.Hand.Cards.Count > 0, "first player turn");

            var state = player.Creature.CombatState ?? throw new InvalidOperationException("Expected combat.");
            var enemy = state.HittableEnemies.First();
            var choice = new ThrowingPlayerChoiceContext();
            Check(Lumen(player) == 2 && player.Creature.Block == 6, "starting Lumen and relic block");
            await Play<CeramicGuard>(player, choice);
            Check(player.Creature.Block == 11, "basic skill grants block");
            var initialHp = enemy.CurrentHp;
            await Play<LightProbe>(player, choice, enemy);
            Check(Lumen(player) == 4 && enemy.CurrentHp == initialHp - 5, "probe deals damage and adds Lumen");
            await LumenPower.Gain(choice, player.Creature, 20);
            Check(Lumen(player) == 6, "Lumen respects its six-stack cap");
            var beforeRelease = enemy.CurrentHp;
            await Play<ResonantRelease>(player, choice, enemy);
            Check(Lumen(player) == 3 && enemy.CurrentHp == beforeRelease - 15, "release spends three and deals 15 damage");
            var block = player.Creature.Block;
            await Play<PocketShard>(player, choice);
            Check(player.Creature.Block == block + 6 && piles.Hand.Cards.Any(card => card is SurveyShard),
                "skill blocks and creates a hand token");
            var token = piles.Hand.Cards.Single(card => card is SurveyShard);
            await CardCmd.AutoPlay(choice, token, enemy);
            Check(piles.ExhaustPile.Cards.Contains(token), "token exhausts after play");
            await Play<EchoLantern>(player, choice);
            Check(player.Creature.GetPower<EchoLanternPower>()?.Amount == 1, "power installs its turn listener");
            var turn = piles.TurnNumber;
            CombatManager.Instance.SetReadyToEndTurn(player, false);
            await WaitFor(tree, () => piles.TurnNumber > turn && Lumen(player) == 4, "real turn transition triggers power");
            await Play<AtlasSurge>(player, choice, upgrade: true);
            Check(piles.DiscardPile.Cards.Any(card => card is FallingGrit), "upgraded rare creates status");
            await Play<DeepSurvey>(player, choice);
            Check(piles.DiscardPile.Cards.Any(card => card is FaultDebt) && Lumen(player) == 6, "rare creates curse and caps Lumen");

            var tonic = ModelDb.Potion<LumenTonic>().ToMutable();
            var solvent = ModelDb.Potion<FaultSolvent>().ToMutable();
            await PotionCmd.TryToProcure(tonic, player);
            await PotionCmd.TryToProcure(solvent, player);
            await LumenPower.Spend(choice, player.Creature, 3, token);
            await tonic.OnUseWrapper(choice, player.Creature);
            await solvent.OnUseWrapper(choice, enemy);
            Check(Lumen(player) == 6 && enemy.GetPower<VulnerablePower>()?.Amount == 3, "self and targeted potions");

            await Play<BreakRidge>(player, choice, upgrade: true);
            Check(enemy.IsDead, "upgraded multi-hit attack defeats enemy");
            // AutoPlay runs the card wrapper outside the native player-action queue.
            await CombatManager.Instance.CheckWinCondition();
            await WaitFor(tree, () => !CombatManager.Instance.IsInProgress, "combat ends");
            var rewards = new RewardsSet(player).WithRewardsFromRoom(room);
            await rewards.GenerateWithoutOffering();
            var rewardCards = rewards.Rewards.OfType<CardReward>().Single().Cards.ToArray();
            Check(rewardCards.Length == 3 && rewardCards.Select(card => card.Id).Distinct().Count() == 3
                && rewardCards.All(card => card.Pool == character.CardPool),
                "three distinct character card reward options");

            await RunManager.Instance.EnterRoomDebug(RoomType.Shop, showTransition: false);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(run.CurrentRoom is MerchantRoom && NMerchantRoom.Instance?.PlayerVisuals.Single() is SurveyorMerchantVisual visual
                    && visual.GetNode<AnimationPlayer>("AnimationPlayer").IsPlaying(),
                "merchant room plays Surveyor idle animation");
            await RunManager.Instance.EnterRoomDebug(RoomType.RestSite, showTransition: false);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            Check(run.CurrentRoom is RestSiteRoom && NRestSiteRoom.Instance != null, "rest site room");
            LocManager.Instance.SetLanguage("zhs");
            Check(character.Title.GetRawText() == "提灯测绘师"
                && ModelDb.Card<LightProbe>().Title == "灯光探针"
                && ModelDb.Potion<FaultSolvent>().Title.GetRawText() == "断层溶剂", "Simplified Chinese localization");
            LocManager.Instance.SetLanguage("eng");
            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }

    private static async Task WaitFor(SceneTree tree, Func<bool> predicate, string check)
    {
        for (var frame = 0; frame < 1800 && !predicate(); frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        Check(predicate(), check);
    }

    private static int Lumen(Player player) => player.Creature.GetPower<LumenPower>()?.Amount ?? 0;

    private static async Task Play<T>(Player player, PlayerChoiceContext choice, Creature? target = null, bool upgrade = false)
        where T : CardModel
    {
        var combat = player.Creature.CombatState ?? throw new InvalidOperationException("Expected combat.");
        var card = combat.CreateCard<T>(player);
        if (upgrade)
        {
            CardCmd.Upgrade(card);
        }
        await CardCmd.AutoPlay(choice, card, target);
    }

    private static void Check(bool condition, string check)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Failed check: {check}");
        }
        GD.Print($"[SpireLabSmoke] OK {check}");
    }
}
