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
            foreach (var boundary in new[] { 0.42f, 1.05f, 1.55f, 2.15f, CrimsonFinalMirror.ShatterTime, 2.95f })
            {
                var before = CrimsonMirrorCamera.Theatre(boundary - 0.0001f);
                var after = CrimsonMirrorCamera.Theatre(boundary + 0.0001f);
                Check(before.Aim.DistanceTo(after.Aim) < 1.5f && Math.Abs(before.Zoom - after.Zoom) < 0.002f,
                    $"stage camera is continuous at {boundary:F2}s");
            }
            await Enter(tree, player, ModelDb.Encounter<NibbitsNormal>());
            Check(PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(CrimsonFinalMirror.Assets),
                "textures, shaders and audio are preloaded before first battle");
            var costumeArt = TheWitch.Art + "/finisher/v3/";
            Check(PreloadManager.Cache.GetTexture2D(costumeArt + "portrait.webp").GetSize() == new Vector2(1672, 941)
                && PreloadManager.Cache.GetTexture2D(costumeArt + "expression.webp").GetSize() == new Vector2(435, 307)
                && PreloadManager.Cache.GetTexture2D(costumeArt + "palm.webp").GetSize() == new Vector2(631, 623),
                "the user-reference art loads at its lossless cutout and expression dimensions");
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
            }, subject: enemy, shots: [(0.82f, "02-pullback-reveal"), (2.42f, "05-reaching-hand"), (2.64f, "11-portrait-erasure")]);
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
            }, shots: [(1.30f, "03-mirror-arc"), (3.22f, "07-return")]);
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
            }, subject: enemy, resize: true, shots: [(0.31f, "09-left-slot"), (2.12f, "04-wicked-smile")]);
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
            }, shots: [(0.31f, "10-boss"), (2.85f, "06-shatter")]);
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

    private static string FormatMemory(double bytes) =>
        bytes > 0 ? FormattableString.Invariant($"{bytes / 1048576:F1}") : "unavailable";

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
        var portrait = layout.FindChild("PortraitPlate", recursive: true, owned: false) as Sprite2D;
        var portraitDepth = layout.FindChild("PortraitDepth", recursive: true, owned: false) as Node2D;
        var garden = layout.FindChild("LivingMirrorGarden", recursive: true, owned: false) as Node2D;
        var echoes = layout.FindChild("PeripheralReflections", recursive: true, owned: false) as Node2D;
        var handDepth = layout.FindChild("HandDepth", recursive: true, owned: false) as Node2D;
        var soul = layout.FindChild("CapturedSoul", recursive: true, owned: false) as Sprite2D;
        Check(cracks != null && shot != null && arm != null && mirror != null && portrait != null && soul != null &&
            portraitDepth != null && handDepth != null && portraitDepth.GetIndex() < shot.GetIndex() && handDepth.GetIndex() > shot.GetIndex()
            && garden != null && echoes != null && garden.GetIndex() < portraitDepth.GetIndex() && echoes.GetIndex() < garden.GetIndex(),
            $"{label} layers the central mirror in front of the portrait and behind the reaching hand");
        var acting = (ShaderMaterial)portrait!.Material;
        var mirrorInk = (ShaderMaterial)mirror!.Material;
        var impact = layer.GetNode<AudioStreamPlayer>("MirrorImpact");
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
        var reached = false;
        var focusPulled = false;
        var smiled = false;
        var fistSynced = false;
        var stageTravel = false;
        var widened = false;
        var collapsed = false;
        var restoredShot = false;
        var backgroundMotion = false;
        var paintedErosion = false;
        var firstVineRotation = (float?)null;
        var brightEcho = false;
        var peakTextureBytes = 0d;
        var peakVideoBytes = 0d;
        var peakStaticBytes = 0d;
        var peakNodeCount = 0d;
        var initialStage = Vector2.Zero;
        var peakNativeZoom = baselineScale.X;
        while (GodotObject.IsInstanceValid(layer) && layer.IsInsideTree() && !layer.IsQueuedForDeletion()
            && Time.GetTicksUsec() - shown < 4_500_000)
        {
            var time = drape.GetShaderParameter("elapsed").AsSingle();
            var textureBytes = Performance.GetMonitor(Performance.Monitor.RenderTextureMemUsed);
            // MegaDot can report a wrapped texture counter during deferred releases.
            if (textureBytes > 0 && textureBytes < 64d * 1024 * 1024 * 1024)
            {
                peakTextureBytes = Math.Max(peakTextureBytes, textureBytes);
            }
            peakVideoBytes = Math.Max(peakVideoBytes, Performance.GetMonitor(Performance.Monitor.RenderVideoMemUsed));
            peakStaticBytes = Math.Max(peakStaticBytes, Performance.GetMonitor(Performance.Monitor.MemoryStatic));
            peakNodeCount = Math.Max(peakNodeCount, Performance.GetMonitor(Performance.Monitor.ObjectNodeCount));
            if (time is > 1.05f and < 2.64f)
            {
                var vine = garden!.GetChild<Sprite2D>(0);
                firstVineRotation ??= vine.Rotation;
                backgroundMotion |= garden.Visible && Math.Abs(vine.Rotation - firstVineRotation.Value) > 0.045f;
                brightEcho |= echoes!.GetChild<Sprite2D>(0).Modulate.A > 0.16f;
            }
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
            peakNativeZoom = Math.Max(peakNativeZoom, host.SceneContainer.Scale.X);
            if (time is > 0.74f and < 1.10f)
            {
                widened |= host.SceneContainer.Scale.X < peakNativeZoom - 0.035f;
            }
            if (time is >= 1.08f and <= 1.33f)
            {
                recentered |= host.SceneContainer.Scale.IsEqualApprox(baselineScale);
                initialStage = shot!.Position;
            }
            if (time is >= 1.85f and <= 2.21f)
            {
                stageTravel |= shot!.Position.DistanceTo(initialStage) > 120;
                focusPulled |= acting.GetShaderParameter("defocus").AsSingle() < 0.45f
                    && mirrorInk.GetShaderParameter("defocus").AsSingle() > 1.4f;
                smiled |= acting.GetShaderParameter("smile").AsSingle() > 0.8f
                    && Math.Abs(acting.GetShaderParameter("tilt").AsSingle()) > 0.04f;
            }
            if (time >= 2.40f && time < CrimsonFinalMirror.ShatterTime)
            {
                reached |= arm!.Visible && arm.Texture.ResourcePath.EndsWith("/v3/palm.webp", StringComparison.Ordinal) &&
                    handDepth!.Scale.X > portraitDepth!.Scale.X &&
                    acting.GetShaderParameter("defocus").AsSingle() > 0.45f;
                paintedErosion |= arm!.Visible && arm.Modulate.A > 0.65f
                    && acting.GetShaderParameter("erosion").AsSingle() > 0.54f;
                collapsed |= mirror!.Visible && soul!.Visible && soul.Scale.X < 0.24f &&
                    ((ShaderMaterial)soul.Material).GetShaderParameter("fracture").AsSingle() > 0.80f;
            }
            if (time >= CrimsonFinalMirror.ShatterTime && time < 2.99f)
            {
                fistSynced |= arm!.Texture.ResourcePath.EndsWith("/v3/fist.webp", StringComparison.Ordinal)
                    && arm.Visible && arm.Modulate.A > 0.55f && !mirror!.Visible && !soul!.Visible && impact.Playing;
            }
            if (time > 3.30f)
            {
                restoredShot |= shot!.Position.Length() < 95 && Math.Abs(shot.Rotation) < 0.007;
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
        var p99 = sorted[(int)((sorted.Length - 1) * 0.99)];
        var texture = FormatMemory(peakTextureBytes);
        var staticMemory = FormatMemory(peakStaticBytes);
        GD.Print(FormattableString.Invariant($"[SpireLabSmoke] PERF {label}; window={displaySize.X}x{displaySize.Y}; action-to-overlay={(shown - started) / 1000d:F1}ms; runtime={runtime:F1}ms; frames={sorted.Length}; mean={sorted.Average():F2}ms; p95={p95:F2}ms; p99={p99:F2}ms; max={sorted[^1]:F2}ms; fps={1000 / sorted.Average():F1}; peakTextureMiB={texture}; peakVideoMiB={peakVideoBytes / 1048576:F1}; peakStaticMiB={staticMemory}; peakNodes={peakNodeCount:F0}"));
        Check(interrupt ? runtime is > 590 and < 1400 : runtime is >= 3310 and <= 3810,
            $"{label} presentation duration is correct");
        Check(pushed, $"{label} actually changes the native scene camera during the push");
        if (!interrupt)
        {
            Check(widened && recentered && stageTravel && restoredShot, $"{label} pulls back, climbs to the portrait and settles to the original framing");
            Check(focusPulled && smiled && reached, $"{label} transfers focus through the smile and into the foreground palm");
            Check(backgroundMotion && brightEcho, $"{label} animates thorn roses and a delayed peripheral reflection");
            Check(paintedErosion, $"{label} erodes pigment on the portrait while the foreground hand remains opaque");
            Check(collapsed && fistSynced, $"{label} collapses the soul before the synchronized fist, shatter and impact audio");
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
            images.Add(("08-native-rewards", await ReadDrawnFrame(tree)));
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
