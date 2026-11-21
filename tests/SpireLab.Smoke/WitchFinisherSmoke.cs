using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Creatures;
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
    private static bool CaptureEnabled => System.Environment.GetEnvironmentVariable("SPIRELAB_SMOKE_TIMING_ONLY") != "1";

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
            DisplayServer.WindowMoveToForeground();
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
            var rightSlot = CrimsonMirrorCamera.ReadSubject(enemy.GetCreatureNode()?.Hitbox);
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
            }, subject: enemy, shots: [(0.33f, "01-enemy-push")], rewardShot: true);
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
            }, subject: enemy, shots: [(0.70f, "02-camera-turn")]);
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
            }, shots: [(1.29f, "03-witch-closeup"), (2.45f, "06-dissolve")]);
            Check(targets.All(target => target.IsDead), "AOE defeats all remaining enemies in one play");
            await LeaveCompletedCombat(tree);

            await Enter(tree, player, ModelDb.Encounter<NibbitsNormal>());
            enemy = player.Creature.CombatState!.Enemies.First();
            foreach (var other in player.Creature.CombatState.Enemies.Where(creature => creature != enemy).ToArray())
            {
                await CreatureCmd.Kill(other);
            }
            var leftSlot = CrimsonMirrorCamera.ReadSubject(enemy.GetCreatureNode()?.Hitbox);
            Check(rightSlot is { } right && leftSlot is { } left && Mathf.Abs(left.GetCenter().X - right.GetCenter().X) > 0.04f,
                "normal and multi-hit fixtures exercise separate native enemy positions");
            await CreatureCmd.SetCurrentHp(enemy, 12);
            var echo = await Add<BoilingEcho>(player);
            await Observe(tree, "multi-hit final hit", async () =>
            {
                Check(echo.TryManualPlay(enemy), "two-hit card uses the native action queue");
                await Task.CompletedTask;
            }, subject: enemy, resize: true, shots: [(0.31f, "08-left-slot"), (1.73f, "04-collapse")]);
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
            }, shots: [(0.31f, "09-boss"), (2.065f, "05-shatter")]);
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

    private static async Task Observe(SceneTree tree, string label, Func<Task> finish, Creature? subject = null,
        bool interrupt = false, bool resize = false, (float Seconds, string Name)[]? shots = null, bool rewardShot = false)
    {
        var room = (CombatRoom)RunManager.Instance.DebugOnlyGetState()!.CurrentRoom!;
        var host = NCombatRoom.Instance!;
        var baselineScale = host.SceneContainer.Scale;
        var baselinePosition = host.SceneContainer.Position;
        var baselineUi = host.Ui.GetGlobalTransformWithCanvas();
        GD.Print($"[SpireLabSmoke] SCENE {host.SceneContainer.GetPath()} / UI {host.Ui.GetPath()}");
        var subjectFrame = CrimsonMirrorCamera.ReadSubject(subject?.GetCreatureNode()?.Hitbox);
        var started = Time.GetTicksUsec();
        var ending = finish();
        await WaitFor(tree, () => FindLayer() != null, $"{label} triggers the cinematic");
        var layer = FindLayer()!;
        var layout = layer.GetChild<Control>(0);
        var stage = layout.GetNode<TextureRect>("MirrorDrape");
        var drape = (ShaderMaterial)stage.Material;
        var cracks = layout.FindChild("ActualTargetCracks", recursive: true, owned: false) as Node2D;
        var shot = layout.FindChild("StageShot", recursive: true, owned: false) as Node2D;
        var arm = layout.FindChild("CastingArm", recursive: true, owned: false) as Sprite2D;
        var mirror = layout.FindChild("SoulMirror", recursive: true, owned: false) as Sprite2D;
        Check(cracks != null && shot != null && arm != null && mirror != null, $"{label} mounts the real V2 scene");
        var impact = layer.GetNode<AudioStreamPlayer>("MirrorImpact");
        var initialArm = arm!.Texture;
        var shown = Time.GetTicksUsec();
        Check(!room.IsPreFinished && NOverlayStack.Instance?.Peek() == null, $"{label} starts before rewards");
        var displaySize = DisplayServer.WindowGetSize();
        var frameTimes = new List<double>();
        var previous = shown;
        var images = new List<(string Name, Image Image)>();
        var scheduled = CaptureEnabled ? shots ?? [] : [];
        var resized = false;
        var pushed = false;
        var recentered = false;
        var raised = false;
        var fistSynced = false;
        var stageTravel = false;
        var initialStage = Vector2.Zero;
        while (GodotObject.IsInstanceValid(layer) && layer.IsInsideTree() && !layer.IsQueuedForDeletion()
            && Time.GetTicksUsec() - shown < 4_000_000)
        {
            var time = drape.GetShaderParameter("elapsed").AsSingle();
            if (!pushed && time is >= 0.17f and <= 0.62f && host.SceneContainer.Scale.X > baselineScale.X * 1.05f)
            {
                pushed = true;
                if (subjectFrame is { } target)
                {
                    var focus = cracks!.Position / layout.Size;
                    Check(focus.DistanceTo(target.GetCenter()) < 0.32f && target.Size.Y > 0.02f,
                        $"{label} frames the actual defeated target at {target.GetCenter()} with bounds {target.Size}");
                }
            }
            if (time is >= 1.18f and <= 1.49f)
            {
                recentered = host.SceneContainer.Scale.IsEqualApprox(baselineScale);
                raised = arm.Texture != initialArm && arm.GetParent<Node2D>().Visible;
                initialStage = shot!.Position;
            }
            if (time is >= 1.84f and < 1.94f)
            {
                stageTravel = shot!.Position.DistanceTo(initialStage) > 85 && mirror!.Visible;
            }
            if (time is >= 1.98f and < 2.15f)
            {
                fistSynced = arm.Texture.ResourcePath.EndsWith("/v2/fist.webp", StringComparison.Ordinal) && !mirror!.Visible && impact.Playing;
            }
            if (images.Count < scheduled.Length && time >= scheduled[images.Count].Seconds)
            {
                // Spread readbacks over actual wins so one GPU stall cannot skip several adjacent beats.
                var capture = scheduled[images.Count];
                images.Add((capture.Name, await ReadDrawnFrame(tree)));
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
        Check(interrupt ? runtime is > 590 and < 1400 : runtime is >= 2500 and <= 2950,
            $"{label} presentation duration is correct");
        Check(pushed, $"{label} actually changes the native scene camera during the push");
        if (!interrupt)
        {
            Check(recentered && raised && stageTravel && fistSynced, $"{label} follows the camera path and synchronizes fist, shatter and impact audio");
        }
        if (resize)
        {
            Check(DisplayServer.WindowGetSize() != displaySize, "resizing a live window keeps the cinematic running");
            DisplayServer.WindowSetSize(displaySize);
        }
        Check(images.Count == scheduled.Length, $"{label} completes all scheduled frame captures");
        await ending;
        await WaitFor(tree, () => FindLayer() == null && room.IsPreFinished && NOverlayStack.Instance?.Peek() != null,
            $"{label} cleans its nodes and reaches normal rewards");
        Check(host.SceneContainer.Scale.IsEqualApprox(baselineScale), $"{label} restores the native camera scale");
        if (!resize)
        {
            Check(host.SceneContainer.Position.DistanceTo(baselinePosition) < 4
                && host.Ui.GetGlobalTransformWithCanvas().IsEqualApprox(baselineUi),
                $"{label} restores the original camera position and leaves the combat UI transform intact");
        }
        if (CaptureEnabled && rewardShot)
        {
            await tree.ToSignal(tree.CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
            images.Add(("07-native-rewards", await ReadDrawnFrame(tree)));
        }
        foreach (var (name, image) in images)
        {
            using (image)
            {
                Save(image, name);
            }
        }
    }

    private static async Task<Image> ReadDrawnFrame(SceneTree tree)
    {
        // ProcessFrame precedes canvas drawing, so reading here directly can save an older swap-buffer.
        await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        return tree.Root.GetTexture().GetImage();
    }

    private static void Save(Image image, string name)
    {
        var directory = System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_FINISHER_CAPTURES")
            ?? throw new InvalidOperationException("Missing capture directory.");
        Check(image.SavePng(Path.Combine(directory, name + ".png")) == Error.Ok, $"captured {name}");
    }
}
