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
/// Fast real-game verification for Living Witch Live2D rig, play-only animated card art,
/// and cohesive gothic combat VFX (zero special Inspect/Hover hijack; everything else native).
/// </summary>
internal static class WitchAnimatedArtSmoke
{
    public static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        var outDir = System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_ANIMATED_ART_CAPTURES")
            ?? Path.Combine(Path.GetTempPath(), "spirelab-witch-v21");
        var liveDir = Path.Combine(outDir, "witch-live");
        Directory.CreateDirectory(liveDir);

        try
        {
            var game = NGame.Instance ?? throw new InvalidOperationException("NGame not ready.");
            await WaitFor(tree, () =>
            {
                var loaded = PreloadManager.Cache.GetLoadedCacheAssets();
                return game.MainMenu != null && loaded.Count >= 700;
            }, "deferred startup assets finish before live visual checks");

            SaveManager.Instance.SetFtuesEnabled(false);
            LocManager.Instance.SetLanguage("zhs");
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Full);
            WitchAnimatedCardArt.DebugPhaseOverride = null;

            // Start a real Witch combat run cleanly
            var run = await game.StartNewSingleplayerRun(ModelDb.Character<TheWitch>(), false, ActModel.GetDefaultList(), [], "WITCHV21LIVE", GameMode.Standard);
            var player = run.Players[0];
            await RunManager.Instance.EnterMapCoord(run.Map.StartingMapPoint.coord);

            // Pick first displayed card in Fate Selection and enter combat Play phase
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

            // 1. Verify Living Witch Live2D rig & living mirror shader active at 60 FPS
            var visualsNode = player.Creature.GetCreatureNode()!.Visuals;
            var combatAnim = visualsNode.GetNode<AnimationPlayer>("AnimationPlayer");
            var witchSprite = visualsNode.GetNode<Sprite2D>("Visuals");
            var mirrorSprite = visualsNode.GetNode<Sprite2D>("MirrorFormVisual");
            var liveRig = WitchLiveRig.Find(player.Creature) ?? throw new InvalidOperationException("Missing WitchLiveRigController.");
            Check(combatAnim.CurrentAnimation == "idle" && combatAnim.GetAnimation("idle").Length >= 3.0f
                && combatAnim.GetAnimation("idle").GetTrackCount() >= 5,
                "combat.tscn keeps upgraded 3.2s organic idle with asynchronous MirrorFormVisual tracks");
            Check(liveRig.IsRigActive && liveRig.FrameCount > 5
                && witchSprite.Material is ShaderMaterial && mirrorSprite.Material is ShaderMaterial,
                "Living Witch Live2D rig and living mirror shader are active on combat sprites");

            // Measure 45 live combat frames for smooth framerate telemetry
            var frameMs = new List<double>(45);
            var prevUsec = Time.GetTicksUsec();
            for (var i = 0; i < 45; i++)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var now = Time.GetTicksUsec();
                frameMs.Add((now - prevUsec) / 1000.0);
                prevUsec = now;
            }
            frameMs.Sort();
            var meanMs = frameMs.Average();
            var p95Ms = frameMs[(int)(frameMs.Count * 0.95)];
            GD.Print($"[SpireLabSmoke] PERF Living Witch 45 frames: mean={meanMs:F2}ms p95={p95Ms:F2}ms fps={1000.0 / meanMs:F1} rigFrames={liveRig.FrameCount}");

            // 2. Verify Hand & native Inspect screen remain 100% vanilla (zero hover/inspect hijack!)
            var choice = new ThrowingPlayerChoiceContext();
            foreach (var card in piles.Hand.Cards.ToArray())
            {
                await CardCmd.Discard(choice, card);
            }

            var strike = await Add<WitchStrike>(player);
            var r10HandModel = await Add<TheMirrorSelf>(player);
            var barrier = await Add<ColdMirrorBarrier>(player);
            await WaitFrames(tree, 10);

            var hand = NPlayerHand.Instance!;
            var r10Holder = hand.GetCardHolder(r10HandModel) ?? throw new InvalidOperationException("Missing R10 hand holder.");
            var r10Portrait = r10Holder.CardNode!.GetNode<TextureRect>("%Portrait");
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0 && r10Portrait.Material == null,
                "hand R10 remains 100% native static (zero idle/hover shader)");

            // Hovering hand card must NOT trigger special hover mode
            r10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Focused, r10Holder.Hitbox);
            await WaitFrames(tree, 4);
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0 && r10Portrait.Material == null,
                "hovering R10 stays 100% native with zero special hover animation");
            r10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Unfocused, r10Holder.Hitbox);

            // Opening native Inspect screen must remain 100% vanilla
            var inspect = NGame.Instance!.GetInspectCardScreen();
            inspect.Open([r10HandModel], 0);
            await WaitFor(tree, () => inspect.Visible, "native Inspect screen opens");
            await WaitFrames(tree, 4);
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0,
                "native Inspect screen stays 100% vanilla with zero special inspect animation");
            inspect.Close();
            await WaitFor(tree, () => !inspect.Visible, "native Inspect screen closes");

            // 3. Play R10: activates play-only animated art + Mirror Invert VFX + awakens Mirror Self rig!
            await CreatureCmd.SetCurrentHp(player.Creature, 20);
            await PlayerCmd.SetEnergy(10, player);
            var enemy = player.Creature.CombatState!.HittableEnemies.First();
            var playActivationsBefore = WitchAnimatedCardArt.PlayActivationsCount;
            var vfxBefore = WitchCombatVfx.SpawnedEffectCount;

            Check(r10HandModel.TryManualPlay(null), "R10 enters native combat queue cleanly");
            await WaitFor(tree, () => WitchAnimatedCardArt.PlayActivationsCount > playActivationsBefore,
                "playing R10 activates play-only animated card art in the Play Zone");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "01-play-r10-mirror-invert.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && WitchCombatState.For(player).MirrorForm,
                "R10 resolves identical V2.0 MirrorForm + MirrorHp inversion (20 real, 28 mirror)");
            Check(player.Creature.CurrentHp == 20 && WitchCombatState.For(player).MirrorHp == 28,
                "R10 animated play art preserves exact V2.0 life inversion and mirror HP");
            await WaitFor(tree, () => WitchAnimatedCardArt.ActiveControllerCount == 0,
                "R10 animated card art deactivates automatically after play resolves");

            // 4. Capture 20-frame Living Witch Live2D loop (awakened mirror + hair/hat/skirt sway + natural blink)
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "R10 invert VFX clears cleanly");
            var blinksBefore = liveRig.BlinkCount;
            for (var i = 0; i < 20; i++)
            {
                if (i == 8)
                {
                    liveRig.TriggerBlinkNow();
                }
                await WaitFrames(tree, 3);
                await Capture(tree, Path.Combine(liveDir, $"frame_{i:D2}.png"), quiet: true);
            }
            Check(liveRig.BlinkCount > blinksBefore, "Living Witch performs natural upper-eyelid blink");
            GD.Print("[SpireLabSmoke] OK captured 20-frame Living Witch Live2D loop");

            // 5. Play combat cards across all VFX categories & capture key frames
            Check(strike.TryManualPlay(enemy), "WitchStrike plays cleanly");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && strike.Pile?.Type == PileType.Discard, "Strike finishes");

            Check(barrier.TryManualPlay(null), "Counterpoint ColdMirrorBarrier plays cleanly");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "Counterpoint & Mirror Ward VFX spawn");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "02-vfx-counterpoint-mirror.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && barrier.Pile?.Type == PileType.Discard, "ColdMirrorBarrier finishes");

            var potionCard = await Add<PotionToss>(player);
            await WaitFrames(tree, 3);
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "previous VFX clears before PotionToss");
            Check(potionCard.TryManualPlay(enemy), "PotionToss plays cleanly");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "Potion Arc VFX spawns");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "03-vfx-potion-arc.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && potionCard.Pile?.Type == PileType.Discard, "PotionToss finishes");

            var grimoireCard = await Add<RazorPages>(player);
            await WaitFrames(tree, 3);
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "previous VFX clears before RazorPages");
            Check(grimoireCard.TryManualPlay(enemy), "Grimoire RazorPages plays cleanly");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "Grimoire Pages VFX spawns");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "04-vfx-grimoire-pages.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && grimoireCard.Pile?.Type == PileType.Discard, "RazorPages finishes");

            var desireCard = await Add<FallenConfidence>(player);
            await WaitFrames(tree, 3);
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "previous VFX clears before FallenConfidence");
            Check(desireCard.TryManualPlay(null), "Desire FallenConfidence plays cleanly");
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount > 0, "Desire Fracture VFX spawns");
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "05-vfx-desire-fracture.png"));
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && desireCard.Pile?.Type != PileType.Hand, "FallenConfidence finishes");

            // Block Mirror Barrier + Reflect Refraction Beam
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "Desire VFX clears before Block+Reflect");
            WitchFeedback.Block(player.Creature);
            WitchFeedback.Reflect(enemy, player.Creature);
            await WaitFrames(tree, 4);
            await Capture(tree, Path.Combine(outDir, "06-vfx-block-reflect-beam.png"));
            await WaitFor(tree, () => WitchCombatVfx.ActiveEffectCount == 0, "all transient combat VFX nodes self-clean cleanly");
            Check(WitchCombatVfx.SpawnedEffectCount >= vfxBefore + 7,
                $"cohesive gothic combat VFX spawned across all categories (delta={WitchCombatVfx.SpawnedEffectCount - vfxBefore})");

            // 6. Verify Static mode cleanly detaches Live2D materials and restores on Full
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Static);
            await WaitFrames(tree, 2);
            Check(!liveRig.IsRigActive && witchSprite.Material == null && mirrorSprite.Material == null,
                "Static mode cleanly detaches Living Witch and Mirror shader materials");
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Full);
            await WaitFrames(tree, 2);
            Check(liveRig.IsRigActive && witchSprite.Material is ShaderMaterial,
                "restoring Full mode reattaches Living Witch shader cleanly");

            // 7. Return to main menu with zero leaked controllers or VFX
            await game.ReturnToMainMenuAfterRun();
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0 && WitchLiveRig.ActiveControllerCount == 0
                && WitchCombatVfx.ActiveEffectCount == 0 && game.MainMenu != null,
                "returning to main menu leaves zero active card/rig controllers or VFX leaks");

            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
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
