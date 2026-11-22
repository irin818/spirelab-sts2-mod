using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

internal static class WitchSaveProbe
{
    public static async Task Run(bool resume)
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var game = NGame.Instance!;
            await game.GameStartupComplete;
            // Loading a run while the main menu's deferred preload is active can race the engine's asset worker.
            var startupAssets = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet)
                .Where(path => ResourceLoader.Exists(path)).Distinct().ToArray();
            await WaitFor(tree, () => PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(startupAssets),
                "deferred startup assets finish before opening a saved run");
            SaveManager.Instance.SetFtuesEnabled(false);
            using var selection = CardSelectCmd.UseSelector(new WitchSelector());
            if (resume)
            {
                var save = SaveManager.Instance.LoadRunSave().SaveData
                    ?? throw new InvalidOperationException("Expected saved run from the first process.");
                var run = RunState.FromSerializable(save);
                await RunManager.Instance.SetUpSavedSingleplayer(run, save);
                await game.LoadRun(run, save.PreFinishedRoom);
                var player = run.Players.Single();
                await WaitFor(tree, () => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play,
                    "resumed mapped combat reaches play phase");
                Check(player.Character is TheWitch && player.Deck.Cards.OfType<BeyondTheGlass>().Single().IsUpgraded
                    && player.Deck.Cards.OfType<ReflectedLight>().Count() == 1
                    && player.Deck.Cards.OfType<EmptyMirror>().Single().Type == CardType.Power
                    && player.Relics.Single() is CrackedVanityMirror && player.Creature.CurrentHp == 41,
                    "new process restores Witch, ancient upgrade, retired C17 card, R15 Power migration, relic and real HP");
                var state = WitchCombatState.For(player);
                Check(state.MirrorHp == 0 && state.OpeningCard is PotionMist && !player.Deck.Cards.Contains(state.OpeningCard)
                    && player.Deck.Cards.Count == 13 && player.PlayerCombatState!.Hand.Cards.Count == 6,
                    "resumed battle starts clean and triggers intrinsic draw");
            }
            else
            {
                var run = await game.StartNewSingleplayerRun(ModelDb.Character<TheWitch>(), true,
                    ActModel.GetDefaultList(), [], "WITCHSAVE41", GameMode.Standard);
                var player = run.Players.Single();
                await CreatureCmd.SetCurrentHp(player.Creature, 41);
                var ancient = run.CreateCard(ModelDb.Card<BeyondTheGlass>(), player);
                CardCmd.Upgrade(ancient);
                await CardPileCmd.Add(ancient, PileType.Deck);
                await CardPileCmd.Add(run.CreateCard(ModelDb.Card<ReflectedLight>(), player), PileType.Deck);
                await CardPileCmd.Add(run.CreateCard(ModelDb.Card<EmptyMirror>(), player), PileType.Deck);
                await RunManager.Instance.EnterMapCoord(run.Map.StartingMapPoint.coord);
                await WaitFor(tree, () => player.PlayerCombatState?.Phase == PlayerTurnPhase.Play,
                    "first process enters a real mapped room");
                Check(run.CurrentMapCoord.HasValue && run.CurrentRoom is CombatRoom
                    && SaveManager.Instance.LoadRunSave().SaveData != null,
                    "native map travel writes a resumable save");
            }

            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }
}
