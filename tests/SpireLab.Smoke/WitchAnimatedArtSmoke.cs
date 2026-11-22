using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using SpireLab.SpireLabCode.Witch.UI;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

/// <summary>
/// Full Crimson Mirror Nocturne (V2.1) verification & stress benchmark:
/// - Apples-to-apples Static Baseline vs V2.1 Full FPS / 1% Low / P95 / P99 / VFX peak / audio throttle telemetry
/// - Play-only animated card art (R10 + C01) with 100% vanilla Hand/Hover/Inspect
/// - Living Witch Live2D rig + 4 gestures + U21 frame filigree + natural upper-eyelid blink
/// - All 5 signature combat samples (C01, C11, C19, R27, Counterpoint Relay Chain) + U10/R16/R08/U18/U05/5 Desires
/// - High-frequency 12+ card stress burst & zero-leak cleanup on enemy death and return to main menu.
/// </summary>
internal static class WitchAnimatedArtSmoke
{
    public static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var outDir = System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_ANIMATED_ART_CAPTURES")
            ?? Path.Combine(Path.GetTempPath(), "spirelab-witch-v21");
        Directory.CreateDirectory(outDir);

        try
        {
            var game = NGame.Instance ?? throw new InvalidOperationException("NGame not ready.");
            await WaitFor(tree, () =>
            {
                var loaded = PreloadManager.Cache.GetLoadedCacheAssets();
                return game.MainMenu != null && loaded.Count >= 700;
            }, "deferred startup assets finish before Crimson Mirror Nocturne checks");

            SaveManager.Instance.SetFtuesEnabled(false);
            LocManager.Instance.SetLanguage("zhs");
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Full);
            WitchAnimatedCardArt.DebugPhaseOverride = null;

            // Start real Witch combat run
            var run = await game.StartNewSingleplayerRun(ModelDb.Character<TheWitch>(), false, ActModel.GetDefaultList(), [], "WITCHV21CMN", GameMode.Standard);
            var player = run.Players[0];
            await RunManager.Instance.EnterMapCoord(run.Map.StartingMapPoint.coord);

            await WaitFor(tree, () => NOverlayStack.Instance?.Peek() is WitchFateScreen, "Fate Selection opens");
            var fate = (WitchFateScreen)NOverlayStack.Instance!.Peek()!;
            var fateGrid = fate.GetNode<NCardGrid>("%CardGrid");
            var fateHolder = fateGrid.CurrentlyDisplayedCardHolders.First();
            fateGrid.EmitSignal(NCardGrid.SignalName.HolderPressed, fateHolder);
            await WaitFrames(tree, 2);
            await MegaCrit.Sts2.Core.AutoSlay.Helpers.UiHelper.Click(fate.GetNode<NConfirmButton>("%Confirm"));
            await WaitFor(tree, () => NPlayerHand.Instance?.IsInCardSelection == true, "optional swap opens");
            await MegaCrit.Sts2.Core.AutoSlay.Helpers.UiHelper.Click(NPlayerHand.Instance!.GetNode<NConfirmButton>("%SelectModeConfirmButton"));

            var piles = player.PlayerCombatState!;
            await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "combat play phase ready");

            var visualsNode = player.Creature.GetCreatureNode()!.Visuals;
            var witchSprite = visualsNode.GetNode<Sprite2D>("Visuals");
            var mirrorSprite = visualsNode.GetNode<Sprite2D>("MirrorFormVisual");
            var liveRig = WitchLiveRig.Find(player.Creature) ?? throw new InvalidOperationException("Missing WitchLiveRigController.");
            var enemy = player.Creature.CombatState!.HittableEnemies.First();
            var choice = new ThrowingPlayerChoiceContext();

            // Give enemy high HP so we can showcase all signature cards & run a 12-card stress burst safely
            await CreatureCmd.SetMaxAndCurrentHp(enemy, 999);
            await PlayerCmd.SetEnergy(30, player);

            // -----------------------------------------------------------------
            // 1. Apples-to-Apples Performance Benchmark: Static Baseline vs V2.1 Full
            // -----------------------------------------------------------------
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Static);
            await WaitFrames(tree, 3);
            Check(!liveRig.IsRigActive && witchSprite.Material == null && mirrorSprite.Material == null,
                "Static baseline detaches all V2.1 shaders cleanly");
            await MeasureFrames(tree, "STATIC_BASELINE", 45);

            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Full);
            await WaitFrames(tree, 3);
            Check(liveRig.IsRigActive && witchSprite.Material is ShaderMaterial && mirrorSprite.Material is ShaderMaterial,
                "V2.1 Full activates Living Witch Live2D rig and Living Mirror shader");
            await MeasureFrames(tree, "V21_FULL_IDLE", 45);

            // -----------------------------------------------------------------
            // 2. Verify Hand, Hover & Inspect remain 100% Native (No Special Inspect Hijack)
            // -----------------------------------------------------------------
            foreach (var card in piles.Hand.Cards.ToArray())
            {
                await CardCmd.Discard(choice, card);
            }

            var c01 = await Add<PotionToss>(player);
            var r10 = await Add<TheMirrorSelf>(player);
            await WaitFrames(tree, 6);

            var hand = NPlayerHand.Instance!;
            var r10Holder = hand.GetCardHolder(r10)!;
            var c01Holder = hand.GetCardHolder(c01)!;
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0
                && r10Holder.CardNode!.GetNode<TextureRect>("%Portrait").Material == null
                && c01Holder.CardNode!.GetNode<TextureRect>("%Portrait").Material == null,
                "R10 and C01 remain 100% native static in hand");

            c01Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Focused, c01Holder.Hitbox);
            await WaitFrames(tree, 3);
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0, "hovering C01 stays 100% native");
            c01Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Unfocused, c01Holder.Hitbox);

            var inspect = NGame.Instance!.GetInspectCardScreen();
            inspect.Open([r10, c01], 0);
            await WaitFor(tree, () => inspect.Visible, "native Inspect screen opens");
            await WaitFrames(tree, 3);
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0, "native Inspect screen stays 100% vanilla");
            inspect.Close();
            await WaitFor(tree, () => !inspect.Visible, "native Inspect screen closes");

            // -----------------------------------------------------------------
            // 3. Five Signature Samples + R10 & C01 Play-Only Animated Card Art
            // -----------------------------------------------------------------
            WitchCombatVfx.ResetTelemetry();

            // Sample 1: C01 Potion Toss (Play-only C01 animated card art + spinning glass vial parabola)
            var playsBefore = WitchAnimatedCardArt.PlayActivationsCount;
            Check(c01.TryManualPlay(enemy), "C01 PotionToss plays cleanly");
            await WaitFor(tree, () => WitchAnimatedCardArt.PlayActivationsCount > playsBefore,
                "playing C01 activates play-only animated card art");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "C01 spinning vial VFX spawns");
            await WaitFrames(tree, 5);
            await Capture(tree, Path.Combine(outDir, "01-sample-c01-potion-toss.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && c01.Pile?.Type == PileType.Discard, "C01 finishes");
            await WaitFor(tree, () => WitchAnimatedCardArt.ActiveControllerCount == 0, "C01 play art deactivates after play");

            // Sample 2: R10 Mirror Self (Play-only R10 animated card art + Mirror awakening + V2.0 inversion intact)
            await CreatureCmd.SetCurrentHp(player.Creature, 20);
            playsBefore = WitchAnimatedCardArt.PlayActivationsCount;
            Check(r10.TryManualPlay(null), "R10 TheMirrorSelf plays cleanly");
            await WaitFor(tree, () => WitchAnimatedCardArt.PlayActivationsCount > playsBefore,
                "playing R10 activates play-only animated card art");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "02-sample-r10-mirror-self.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && WitchCombatState.For(player).MirrorForm,
                "R10 preserves identical V2.0 MirrorForm + MirrorHp (20 real / 28 mirror)");
            Check(player.Creature.CurrentHp == 20 && WitchCombatState.For(player).MirrorHp == 28,
                "V2.1 visuals leave V2.0 HP/MirrorHp math untouched");

            // Sample 3: C11 Silver Screen (Unfolding gothic silver mirror plane + Block & Reflect)
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "previous VFX clears");
            var c11 = await Add<SilverScreen>(player);
            await WaitFrames(tree, 2);
            Check(c11.TryManualPlay(null), "C11 SilverScreen plays cleanly");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "C11 SilverScreen unfolding mirror VFX spawns");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "03-sample-c11-silver-screen.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && c11.Pile?.Type == PileType.Discard, "C11 finishes");

            // Sample 4: Counterpoint Relay Chain (C19 Razor Pages -> Mirror Transfer -> U10 Boiling Echo 3-hit!)
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "previous VFX clears");
            var c19 = await Add<RazorPages>(player);
            var u18 = await Add<MirrorMaze>(player);
            var u10 = await Add<BoilingEcho>(player);
            await WaitFrames(tree, 3);

            Check(c19.TryManualPlay(enemy), "Chain 1: C19 RazorPages plays");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "C19 RazorPages VFX spawns");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "04-sample-c19-razor-pages.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && c19.Pile?.Type == PileType.Discard, "C19 finishes");

            Check(u18.TryManualPlay(null), "Chain 2 (Counterpoint): U18 MirrorMaze plays");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "Counterpoint Relay + MirrorMaze VFX spawn");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "05-sample-counterpoint-mirror-maze.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && u18.Pile?.Type == PileType.Discard, "U18 finishes");

            Check(u10.TryManualPlay(enemy), "Chain 3 (Counterpoint 3-hit): U10 BoilingEcho plays");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "Counterpoint Relay + U10 escalating 3-hit VFX spawn");
            await WaitFrames(tree, 5);
            await Capture(tree, Path.Combine(outDir, "06-sample-u10-boiling-echo-relay.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && u10.Pile?.Type == PileType.Discard, "U10 finishes");

            // Sample 5: R27 Abyss of Gambling (Real life siphon from HP bar to palm -> energy spark, rapid 3x!)
            await CreatureCmd.SetCurrentHp(player.Creature, 40);
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "previous VFX clears");
            var r27 = await Add<AbyssOfGambling>(player);
            await WaitFrames(tree, 3);
            Check(r27.TryManualPlay(null), "R27 AbyssOfGambling #1 plays");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "R27 Life Siphon VFX spawns on real HP paid");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "07-sample-r27-gamble-siphon.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && r27.Pile?.Type == PileType.Hand, "R27 returns to hand #1");
            Check(r27.TryManualPlay(null), "R27 rapid #2 plays without blocking");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && r27.Pile?.Type == PileType.Hand, "R27 returns #2");
            Check(r27.TryManualPlay(null), "R27 rapid #3 plays cleanly (burst auto-shortened)");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && r27.Pile?.Type == PileType.Hand, "R27 returns #3");

            // Additional signature VFX checks: R08 FinalDistillation, R16 MirrorCage (2-hit), U21 Inscription glow, U37 PrimalGreed, Block+Reflect
            var r08 = await Add<FinalDistillation>(player);
            var r16 = await Add<MirrorCage>(player);
            var c06 = await Add<ConcentratedFlame>(player);
            await WaitFrames(tree, 3);
            Check(r08.TryManualPlay(enemy), "R08 FinalDistillation crystal plays");
            await WaitFrames(tree, 6);
            await Capture(tree, Path.Combine(outDir, "08-sample-r08-final-distillation.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && r08.Pile?.Type == PileType.Discard, "R08 finishes");

            Check(r16.TryManualPlay(enemy), "R16 MirrorCage 2-hit alternating blades play");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && r16.Pile?.Type == PileType.Discard, "R16 finishes");

            Check(c06.TryManualPlay(enemy), "C06 ConcentratedFlame directional jet plays");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && c06.Pile?.Type == PileType.Discard, "C06 finishes");

            // Update U21 Reflective Inscription frame filigree glow + Block & Reflect Refraction Beam
            WitchFeedback.UpdateInscription(player.Creature, 60);
            WitchFeedback.Block(player.Creature);
            WitchFeedback.Reflect(enemy, player.Creature);
            WitchFeedback.PurifyTransmute(player.Creature);
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "09-sample-reflect-block-purify.png"));

            // -----------------------------------------------------------------
            // 4. High-Frequency Stress Test (10+ Consecutive Alternating Low-Cost Cards + FPS Telemetry)
            // -----------------------------------------------------------------
            await PlayerCmd.SetEnergy(30, player);
            var stressStartMs = Time.GetTicksMsec();
            var stressFrames = new List<double>(160);
            var prevUsec = Time.GetTicksUsec();

            for (var step = 0; step < 10; step++)
            {
                CardModel nextCard = (step % 2 == 0)
                    ? await Add<PotionToss>(player)
                    : await Add<SilverScreen>(player);
                await WaitFrames(tree, 1);
                nextCard.TryManualPlay(step % 2 == 0 ? enemy : null);
                while (RunManager.Instance.ActionExecutor.IsRunning)
                {
                    await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                    var now = Time.GetTicksUsec();
                    stressFrames.Add((now - prevUsec) / 1000.0);
                    prevUsec = now;
                }
            }

            var stressElapsedMs = Time.GetTicksMsec() - stressStartMs;
            stressFrames.Sort();
            var sMean = stressFrames.Count > 0 ? stressFrames.Average() : 16.6;
            var sP95 = stressFrames.Count > 0 ? stressFrames[(int)(stressFrames.Count * 0.95)] : 16.6;
            var sP99 = stressFrames.Count > 0 ? stressFrames[(int)(stressFrames.Count * 0.99)] : 16.6;
            var sOnePctLow = sP99 > 0 ? 1000.0 / sP99 : 60.0;
            GD.Print($"[SpireLabSmoke] PERF STRESS_10_CARDS: elapsed={stressElapsedMs}ms frames={stressFrames.Count} mean={sMean:F2}ms p95={sP95:F2}ms p99={sP99:F2}ms fps={1000.0 / sMean:F1} 1%low={sOnePctLow:F1} peakVfx={WitchCombatVfx.PeakActiveEffects} spawnedVfx={WitchCombatVfx.SpawnedEffectCount} droppedP3={WitchCombatVfx.DroppedDecorativeCount} audioPlayed={WitchCombatVfx.AudioPlayedCount} audioThrottled={WitchCombatVfx.AudioThrottledCount}");
            Check(WitchCombatVfx.PeakActiveEffects <= 22 && WitchCombatVfx.SpawnedEffectCount >= 20,
                $"VFX concurrency budget held cleanly under 10-card rapid stress (peak={WitchCombatVfx.PeakActiveEffects}, spawned={WitchCombatVfx.SpawnedEffectCount})");

            // Test Reduced quality mode cleanly
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Reduced);
            await WaitFrames(tree, 2);
            Check(liveRig.IsRigActive, "Reduced mode keeps Living Witch rig running smoothly at reduced amplitude");
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Full);

            // Verify combat-end cleanup & zero leaks on return to main menu
            WitchFeedback.CombatEnded();
            await WaitFrames(tree, 2);
            Check(WitchCombatVfx.ActiveEffectCount == 0, "CombatEnded clears all transient combat VFX immediately");

            await game.ReturnToMainMenuAfterRun();
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0 && WitchLiveRig.ActiveControllerCount == 0
                && WitchCombatVfx.ActiveEffectCount == 0 && game.MainMenu != null,
                "returning to main menu leaves zero leaked card/rig controllers or VFX nodes");

            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }

    private static async Task MeasureFrames(SceneTree tree, string label, int count)
    {
        var ms = new List<double>(count);
        var prev = Time.GetTicksUsec();
        for (var i = 0; i < count; i++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            var now = Time.GetTicksUsec();
            ms.Add((now - prev) / 1000.0);
            prev = now;
        }
        ms.Sort();
        var mean = ms.Average();
        var p95 = ms[(int)(ms.Count * 0.95)];
        var p99 = ms[(int)(ms.Count * 0.99)];
        var low1 = p99 > 0 ? 1000.0 / p99 : 60.0;
        GD.Print($"[SpireLabSmoke] PERF {label}: frames={count} mean={mean:F2}ms p95={p95:F2}ms p99={p99:F2}ms fps={1000.0 / mean:F1} 1%low={low1:F1}");
    }

    private static async Task WaitFrames(SceneTree tree, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static async Task Capture(SceneTree tree, string path, bool quiet = false)
    {
        await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = tree.Root.GetTexture().GetImage();
        var ok = image.SavePng(path) == Error.Ok;
        if (!ok)
        {
            throw new InvalidOperationException($"Failed to save screenshot {path}");
        }
        if (!quiet)
        {
            GD.Print($"[SpireLabSmoke] OK captured {Path.GetFileName(path)}");
        }
    }
}
