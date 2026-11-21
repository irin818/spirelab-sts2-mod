using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

internal static class WitchFinisherSmoke
{
    private static readonly (float Seconds, string Name)[] Shots =
    [
        (0.20f, "01-reality-seam"), (0.59f, "02-drape"), (1.09f, "03-theatre"),
        (1.47f, "04-collapse"), (1.74f, "05-shatter"), (2.04f, "06-dissolve")
    ];

    public static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var game = NGame.Instance!;
            await game.GameStartupComplete;
            var startup = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet)
                .Where(path => ResourceLoader.Exists(path)).Distinct().ToArray();
            await WaitFor(tree, () => PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(startup),
                "main menu finishes preloading");
            if (NModalContainer.Instance?.OpenModal is NEarlyAccessDisclaimer disclaimer)
            {
                await disclaimer.CloseScreen();
            }
            SaveManager.Instance.SetFtuesEnabled(false);
            var size = (System.Environment.GetEnvironmentVariable("SPIRELAB_SMOKE_RESOLUTION") ?? "1200x675").Split('x');
            DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetSize(new Vector2I(int.Parse(size[0]), int.Parse(size[1])));
            using var selection = CardSelectCmd.UseSelector(new WitchSelector());
            var run = await game.StartNewSingleplayerRun(ModelDb.Character<TheWitch>(), false,
                ActModel.GetDefaultList(), [], "CRIMSONMIRROR", GameMode.Standard);
            var player = run.Players.Single();
            var choice = new BlockingPlayerChoiceContext();
            await Enter(tree, player, ModelDb.Encounter<NibbitsNormal>());
            Check(PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(CrimsonFinalMirror.Assets),
                "textures, shaders and audio are preloaded before first battle");
            var enemy = player.Creature.CombatState!.Enemies.Last();
            foreach (var other in player.Creature.CombatState.Enemies.Where(creature => creature != enemy).ToArray())
            {
                await CreatureCmd.Kill(other);
                Check(FindLayer() == null && CombatManager.Instance.IsInProgress, "non-final defeat does not play");
            }
            await CreatureCmd.SetCurrentHp(enemy, 5);
            var hp = player.Creature.CurrentHp;
            var gamble = await Add<AbyssOfGambling>(player);
            Check(gamble.TryManualPlay(null), "Gamble uses the native action queue");
            await WaitFor(tree, () => player.Creature.CurrentHp < hp && gamble.Pile?.Type ==
                MegaCrit.Sts2.Core.Entities.Cards.PileType.Hand, "Gamble charges real HP before victory");
            var strike = await Add<WitchStrike>(player);
            await Observe(tree, "normal attack", async () =>
            {
                Check(strike.TryManualPlay(enemy), "strike uses the native action queue");
                await Task.CompletedTask;
            }, capture: System.Environment.GetEnvironmentVariable("SPIRELAB_SMOKE_TIMING_ONLY") != "1");
            Check(player.Creature.CurrentHp == hp, "Gamble refund precedes the cinematic and preserves real HP");
            var restored = RunState.FromSerializable(RunManager.Instance.ToSave(null)).Players.Single();
            Check(restored.Creature.CurrentHp == hp && restored.Deck.Cards.Count == 10,
                "victory save retains the refunded HP and the persistent deck");
            await LeaveCompletedCombat(tree);

            await Enter(tree, player, ModelDb.Encounter<NibbitsWeak>());
            enemy = player.Creature.CombatState!.Enemies.Last();
            foreach (var other in player.Creature.CombatState.Enemies.Where(creature => creature != enemy).ToArray())
            {
                await CreatureCmd.Kill(other);
            }
            await CreatureCmd.SetCurrentHp(enemy, 2);
            await Play<ThinMirror>(player, choice);
            await Observe(tree, "lethal block reflection", async () =>
            {
                await CreatureCmd.Damage(choice, player.Creature, 4, ValueProp.Move, enemy);
                await CombatManager.Instance.CheckWinCondition();
            });
            Check(enemy.IsDead, "the 4 blocked damage led to lethal fixed reflection");
            await LeaveCompletedCombat(tree);

            await Enter(tree, player, ModelDb.Encounter<NibbitsNormal>());
            var targets = player.Creature.CombatState!.Enemies.ToArray();
            Check(targets.Length > 1, "AOE fixture has multiple real enemies");
            foreach (var target in targets)
            {
                await CreatureCmd.SetCurrentHp(target, 1);
            }
            var area = await Add<ShatterPhial>(player);
            await Observe(tree, "AOE final hit", async () =>
            {
                Check(area.TryManualPlay(null), "AOE uses the native action queue");
                await Task.CompletedTask;
            });
            Check(targets.All(target => target.IsDead), "AOE defeats all remaining enemies in one play");
            await LeaveCompletedCombat(tree);

            await Enter(tree, player, ModelDb.Encounter<NibbitsWeak>());
            enemy = player.Creature.CombatState!.Enemies.Last();
            foreach (var other in player.Creature.CombatState.Enemies.Where(creature => creature != enemy).ToArray())
            {
                await CreatureCmd.Kill(other);
            }
            await CreatureCmd.SetCurrentHp(enemy, 12);
            var echo = await Add<BoilingEcho>(player);
            await Observe(tree, "multi-hit final hit", async () =>
            {
                Check(echo.TryManualPlay(enemy), "two-hit card uses the native action queue");
                await Task.CompletedTask;
            }, resize: true);
            Check(enemy.IsDead, "two 8-damage hits defeat the target at 12 HP");
            await LeaveCompletedCombat(tree);

            await Enter(tree, player, ModelDb.Encounter<NibbitsNormal>());
            var poisoned = player.Creature.CombatState!.Enemies.ToArray();
            foreach (var target in poisoned)
            {
                await CreatureCmd.SetCurrentHp(target, 2);
                await PowerCmd.Apply<PoisonPower>(choice, target, 3, player.Creature, null);
            }
            await Observe(tree, "poison kills on the enemy turn", async () =>
            {
                CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
                await Task.CompletedTask;
            }, interrupt: true);
            Check(poisoned.All(target => target.IsDead), "native poison kills every remaining enemy");
            await LeaveCompletedCombat(tree);

            await Enter(tree, player, ModelDb.Encounter<KaiserCrabBoss>(), RoomType.Boss);
            var bossTargets = player.Creature.CombatState!.Enemies.ToArray();
            await Observe(tree, "large boss defeat", async () =>
            {
                await CreatureCmd.Damage(choice, bossTargets, 999, ValueProp.Unpowered, player.Creature);
                await CombatManager.Instance.CheckWinCondition();
            });
            Check(bossTargets.All(target => target.IsDead), "native boss parts are defeated");
            await LeaveCompletedCombat(tree);

            await Enter(tree, player, ModelDb.Encounter<NibbitsWeak>());
            await CreatureCmd.Kill(player.Creature);
            await CombatManager.Instance.CheckWinCondition();
            await WaitFor(tree, () => player.Creature.IsDead && !CombatManager.Instance.IsInProgress && NOverlayStack.Instance?.Peek() != null,
                "losing progresses to the native game-over screen");
            Check(FindLayer() == null, "no cinematic plays on defeat");
            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }

    private static async Task Enter(SceneTree tree, Player player, EncounterModel encounter, RoomType type = RoomType.Monster)
    {
        await RunManager.Instance.EnterRoomDebug(type, model: encounter.ToMutable(), showTransition: false);
        await WaitFor(tree, () => player.PlayerCombatState!.Phase == PlayerTurnPhase.Play, "real combat reaches player input");
        Check(FindLayer() == null, "no old cinematic remains in the next room");
    }

    private static CanvasLayer? FindLayer() =>
        NCombatRoom.Instance?.GetNodeOrNull<CanvasLayer>("WitchCrimsonFinalMirror");

    private static async Task Observe(SceneTree tree, string label, Func<Task> finish, bool capture = false, bool interrupt = false, bool resize = false)
    {
        var room = (CombatRoom)RunManager.Instance.DebugOnlyGetState()!.CurrentRoom!;
        var started = Time.GetTicksUsec();
        var ending = finish();
        await WaitFor(tree, () => FindLayer() != null, $"{label} triggers the cinematic");
        var layer = FindLayer()!;
        var stage = layer.GetChild<Control>(0).GetNode<TextureRect>("MirrorDrape");
        var drape = (ShaderMaterial)stage.Material;
        var shown = Time.GetTicksUsec();
        Check(!room.IsPreFinished && NOverlayStack.Instance?.Peek() == null, $"{label} starts before rewards");
        var displaySize = DisplayServer.WindowGetSize();
        var frameTimes = new List<double>();
        var previous = shown;
        var images = new List<(string Name, Image Image)>();
        var resized = false;
        while (GodotObject.IsInstanceValid(layer) && layer.IsInsideTree() && !layer.IsQueuedForDeletion()
            && Time.GetTicksUsec() - shown < 4_000_000)
        {
            var time = drape.GetShaderParameter("elapsed").AsSingle();
            if (capture && images.Count < Shots.Length && time >= Shots[images.Count].Seconds)
            {
                images.Add((Shots[images.Count].Name, tree.Root.GetTexture().GetImage()));
            }
            if (resize && !resized && time > 0.9f)
            {
                resized = true;
                DisplayServer.WindowSetSize(displaySize - new Vector2I(240, 100));
            }
            if (interrupt && time > 0.68f)
            {
                layer.QueueFree();
                break;
            }
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            frameTimes.Add((now - previous) / 1000d);
            previous = now;
        }
        var runtime = (Time.GetTicksUsec() - shown) / 1000d;
        var sorted = frameTimes.Order().ToArray();
        var p95 = sorted[(int)((sorted.Length - 1) * 0.95)];
        GD.Print(FormattableString.Invariant($"[SpireLabSmoke] PERF {label}; window={displaySize.X}x{displaySize.Y}; action-to-overlay={(shown - started) / 1000d:F1}ms; runtime={runtime:F1}ms; frames={sorted.Length}; mean={sorted.Average():F2}ms; p95={p95:F2}ms; max={sorted[^1]:F2}ms"));
        Check(interrupt ? runtime is > 590 and < 1400 : runtime is >= 1950 and <= 2750,
            $"{label} presentation duration is correct");
        if (resize)
        {
            Check(DisplayServer.WindowGetSize() != displaySize, "resizing a live window keeps the cinematic running");
            DisplayServer.WindowSetSize(displaySize);
        }
        if (capture)
        {
            Check(images.Count == Shots.Length, "captured all six scenes from the real renderer");
        }
        await ending;
        await WaitFor(tree, () => FindLayer() == null && room.IsPreFinished && NOverlayStack.Instance?.Peek() != null,
            $"{label} cleans its nodes and reaches normal rewards");
        if (capture)
        {
            await tree.ToSignal(tree.CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
            images.Add(("07-native-rewards", tree.Root.GetTexture().GetImage()));
            foreach (var (name, image) in images)
            {
                using (image)
                {
                    Save(image, name);
                }
            }
        }
    }

    private static void Save(Image image, string name)
    {
        var directory = System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_FINISHER_CAPTURES")
            ?? throw new InvalidOperationException("Missing capture directory.");
        Check(image.SavePng(Path.Combine(directory, name + ".png")) == Error.Ok, $"captured {name}");
    }
}
