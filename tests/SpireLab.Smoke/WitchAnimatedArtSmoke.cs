using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using SpireLab.SpireLabCode.Witch.UI;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

internal static class WitchAnimatedArtSmoke
{
    public static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var game = NGame.Instance ?? throw new InvalidOperationException("Expected NGame.Instance.");
            await game.GameStartupComplete;
            var startupAssets = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet)
                .Where(path => ResourceLoader.Exists(path)).Distinct().ToArray();
            await WaitFor(tree, () => PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(startupAssets),
                "deferred startup assets finish before animated art checks");
            if (NModalContainer.Instance?.OpenModal is NEarlyAccessDisclaimer disclaimer)
            {
                await disclaimer.CloseScreen();
            }
            SaveManager.Instance.SetFtuesEnabled(false);
            LocManager.Instance.SetLanguage("zhs");

            var outDir = System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_ANIMATED_ART_CAPTURES")
                ?? throw new InvalidOperationException("Missing SPIRELAB_WITCH_ANIMATED_ART_CAPTURES.");
            var loopDir = Path.Combine(outDir, "loop");
            Directory.CreateDirectory(loopDir);

            // -----------------------------------------------------------------
            // 1. Card Library / Compendium check (real NCardLibrary submenu)
            // -----------------------------------------------------------------
            var witch = ModelDb.Character<TheWitch>();
            var library = game.MainMenu!.SubmenuStack.GetSubmenuType<NCardLibrary>();
            game.MainMenu.SubmenuStack.Push(library);
            await WaitFrames(tree, 8);
            var libraryGrid = library.FindChildren("*", "Control", true, false).OfType<NCardLibraryGrid>().FirstOrDefault();
            Check(libraryGrid != null, "native Card Library opens cleanly");
            game.MainMenu.SubmenuStack.Pop();
            await WaitFrames(tree, 4);

            // -----------------------------------------------------------------
            // 2. Start real Witch run & enter combat (checking Fate Selection R10)
            // -----------------------------------------------------------------
            var run = await game.StartNewSingleplayerRun(witch, false, ActModel.GetDefaultList(), [], "WITCHV21R10", GameMode.Standard);
            var player = run.Players.Single();
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);

            await WaitFor(tree, () => NOverlayStack.Instance?.Peek() is WitchFateScreen,
                "Fate Selection opens for R10 animated art browse check");
            var fate = (WitchFateScreen)NOverlayStack.Instance!.Peek()!;
            var fateGrid = fate.GetNode<NCardGrid>("%CardGrid");
            var fateCosts = fate.GetNode<OptionButton>("FateFilters/CostFilter");
            var fateSuits = fate.GetNode<OptionButton>("FateFilters/SuitFilter");
            var fateTypes = fate.GetNode<OptionButton>("FateFilters/TypeFilter");
            fateCosts.Select(4); // Cost 3
            fateCosts.EmitSignal(OptionButton.SignalName.ItemSelected, 4);
            fateSuits.Select(2); // Mirror
            fateSuits.EmitSignal(OptionButton.SignalName.ItemSelected, 2);
            fateTypes.Select(3); // Power
            fateTypes.EmitSignal(OptionButton.SignalName.ItemSelected, 3);
            await WaitFor(tree, () => fateGrid.CurrentlyDisplayedCardHolders.Any(h => h.CardModel is TheMirrorSelf),
                "Fate Selection displays R10 The Mirror Self");
            var fateR10Holder = fateGrid.CurrentlyDisplayedCardHolders.First(h => h.CardModel is TheMirrorSelf);
            var fateR10Card = fateR10Holder.CardNode!;
            var fateR10Ctrl = GetController(fateR10Card);
            Check(fateR10Ctrl != null && fateR10Ctrl.DisplayState == CardArtDisplayState.Idle
                && fateR10Card.GetNode<TextureRect>("%Portrait").Material == null,
                "Fate Selection R10 stays static (zero shader material) when unfocused");
            // Hover R10 inside Fate Selection
            fateR10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Focused, fateR10Holder.Hitbox);
            await WaitFrames(tree, 6);
            Check(fateR10Ctrl!.DisplayState == CardArtDisplayState.Hover && fateR10Ctrl.IsActive
                && fateR10Card.GetNode<TextureRect>("%Portrait").Material is ShaderMaterial,
                "Fate Selection R10 activates hover animation on Hitbox focus");
            fateR10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Unfocused, fateR10Holder.Hitbox);
            await WaitFor(tree, () => !fateR10Ctrl.IsActive && fateR10Card.GetNode<TextureRect>("%Portrait").Material == null,
                "Fate Selection R10 settles back to static and clears shader material on unfocus");

            // Confirm R10 and skip optional swap to enter Play phase
            fateGrid.EmitSignal(NCardGrid.SignalName.HolderPressed, fateR10Holder);
            await WaitFrames(tree, 2);
            await MegaCrit.Sts2.Core.AutoSlay.Helpers.UiHelper.Click(fate.GetNode<NConfirmButton>("%Confirm"));
            await WaitFor(tree, () => NPlayerHand.Instance?.IsInCardSelection == true, "optional swap opens");
            await MegaCrit.Sts2.Core.AutoSlay.Helpers.UiHelper.Click(NPlayerHand.Instance!.GetNode<NConfirmButton>("%SelectModeConfirmButton"));

            var piles = player.PlayerCombatState!;
            await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "combat play phase ready");

            // Check updated combat.tscn breathing & asynchronous mirror animation
            var combatAnim = player.Creature.GetCreatureNode()!.Visuals.GetNode<AnimationPlayer>("AnimationPlayer");
            Check(combatAnim.CurrentAnimation == "idle" && combatAnim.GetAnimation("idle").Length >= 3.0f
                && combatAnim.GetAnimation("idle").GetTrackCount() >= 5,
                "combat.tscn plays upgraded 3.2s organic idle with asynchronous MirrorFormVisual tracks");

            // -----------------------------------------------------------------
            // 3. Normal Hand Display, Mouse Hover, and Controller Focus
            // -----------------------------------------------------------------
            var choice = new ThrowingPlayerChoiceContext();
            foreach (var card in piles.Hand.Cards.ToArray())
            {
                await CardCmd.Discard(choice, card);
            }

            await Add<WitchStrike>(player);
            var r10HandModel = await Add<TheMirrorSelf>(player);
            await Add<ColdMirrorBarrier>(player);
            await WaitFrames(tree, 15);

            var hand = NPlayerHand.Instance!;
            var r10Holder = hand.GetCardHolder(r10HandModel) ?? throw new InvalidOperationException("Missing R10 hand holder.");
            var r10CardNode = r10Holder.CardNode ?? throw new InvalidOperationException("Missing R10 hand NCard.");
            var r10Ctrl = GetController(r10CardNode) ?? throw new InvalidOperationException("Missing R10 controller on hand card.");
            var r10Portrait = r10CardNode.GetNode<TextureRect>("%Portrait");

            // State A: Normal hand card must remain 100% static!
            Check(r10Ctrl.DisplayState == CardArtDisplayState.Idle && !r10Ctrl.IsActive && r10Portrait.Material == null,
                "normal hand R10 stays strictly static (Idle, IsActive=false, Material=null)");
            await Capture(tree, Path.Combine(outDir, "01-hand-static.png"));

            // State B (Mouse Hover via Hitbox.Focused / Unfocused)
            WitchAnimatedCardArt.DebugPhaseOverride = 0.28f; // silver sweep peak during hover screenshot
            r10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Focused, r10Holder.Hitbox);
            await WaitFrames(tree, 6);
            Check(r10Ctrl.DisplayState == CardArtDisplayState.Hover && r10Ctrl.IsActive
                && r10Portrait.Material is ShaderMaterial && r10Ctrl.CurrentStrength > 0.5f,
                "hand R10 wakes into Hover animated shader on mouse hover (Hitbox.Focused)");
            await Capture(tree, Path.Combine(outDir, "02-hand-hover.png"));
            WitchAnimatedCardArt.DebugPhaseOverride = null;
            r10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Unfocused, r10Holder.Hitbox);
            await WaitFor(tree, () => r10Ctrl.DisplayState == CardArtDisplayState.Idle && !r10Ctrl.IsActive && r10Portrait.Material == null,
                "hand R10 smoothly settles back to Idle and releases material on unhover");

            // State B (Controller / Gamepad Focus via Control.FocusEntered / FocusExited)
            r10Holder.EmitSignal(Control.SignalName.FocusEntered);
            await WaitFrames(tree, 6);
            Check(r10Ctrl.DisplayState == CardArtDisplayState.Hover && r10Ctrl.IsActive && r10Portrait.Material is ShaderMaterial,
                "hand R10 activates Hover animation on controller focus (FocusEntered)");
            r10Holder.EmitSignal(Control.SignalName.FocusExited);
            await WaitFor(tree, () => r10Ctrl.DisplayState == CardArtDisplayState.Idle && !r10Ctrl.IsActive && r10Portrait.Material == null,
                "hand R10 settles cleanly back to Idle on controller unfocus (FocusExited)");

            // -----------------------------------------------------------------
            // 4. Enlarged Inspect (NInspectCardScreen): Static, Start, Mid, Peak,
            //    Return, Full Loop, Upgrade Preview, EN/ZHS, Rapid Switch, Cleanup
            // -----------------------------------------------------------------
            var inspect = game.GetInspectCardScreen();
            var r01Model = ModelDb.Card<PinnacleOfMana>().ToMutable();
            var r25Model = ModelDb.Card<FallenConfidence>().ToMutable();
            inspect.Open([r10HandModel, r01Model, r25Model], 0);
            await WaitFor(tree, () => inspect.Visible, "native NInspectCardScreen opens on R10");
            await tree.ToSignal(tree.CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);

            var inspectCard = inspect.GetNode<NCard>("Card");
            var inspectPortrait = inspectCard.GetNode<TextureRect>("%Portrait");
            var inspectCtrl = GetController(inspectCard) ?? throw new InvalidOperationException("Missing R10 controller on NInspectCardScreen.");
            Check(inspectCtrl.DisplayState == CardArtDisplayState.Inspect && inspectCtrl.IsActive && inspectPortrait.Material is ShaderMaterial,
                "NInspectCardScreen activates full Inspect animated shader for R10");

            // Live frame-time & phase progression check (60 live frames without phase pin!)
            var phaseStart = inspectCtrl.CurrentPhase;
            var frameMs = new List<double>(60);
            var prevUsec = Time.GetTicksUsec();
            for (var f = 0; f < 60; f++)
            {
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var now = Time.GetTicksUsec();
                frameMs.Add((now - prevUsec) / 1000.0);
                prevUsec = now;
            }
            var phaseAfter = inspectCtrl.CurrentPhase;
            frameMs.Sort();
            var meanMs = frameMs.Average();
            var p95Ms = frameMs[(int)(frameMs.Count * 0.95)];
            var p99Ms = frameMs[(int)(frameMs.Count * 0.99)];
            GD.Print($"[SpireLabSmoke] PERF R10 Inspect 60 frames: mean={meanMs:F2}ms p95={p95Ms:F2}ms p99={p99Ms:F2}ms fps={1000.0 / Math.Max(meanMs, 0.01):F1} phase={phaseStart:F3}->{phaseAfter:F3}");
            Check(phaseAfter != phaseStart && inspectCtrl.CurrentStrength >= 0.98f,
                "Inspect animation continuously advances loop phase smoothly at full strength");

            // Capture 1: Static original mode inside real NInspectCardScreen
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Static);
            await WaitFrames(tree, 3);
            Check(!inspectCtrl.IsActive && inspectPortrait.Material == null && WitchAnimatedCardArt.ActiveControllerCount == 0,
                "Static mode immediately removes shader material and stops all controllers");
            await Capture(tree, Path.Combine(outDir, "03-inspect-static-original.png"));

            // Capture Reduced Motion mode check
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Reduced);
            await WaitFrames(tree, 3);
            Check(inspectCtrl.IsActive && inspectPortrait.Material is ShaderMaterial matReduced
                && Math.Abs(matReduced.GetShaderParameter("reduced_motion").AsSingle() - 1.0f) < 0.01f,
                "Reduced mode runs gentle shader with reduced_motion=1.0 in Inspect");
            // Verify hover stays disabled in Reduced mode
            r10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Focused, r10Holder.Hitbox);
            await WaitFrames(tree, 4);
            Check(!r10Ctrl.IsActive && r10Portrait.Material == null, "Reduced mode disables hand hover animation");
            r10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Unfocused, r10Holder.Hitbox);

            // Restore Full mode and capture all key states + 28-frame seamless loop!
            WitchAnimatedCardArt.SetMode(AnimatedArtMode.Full);
            await WaitFrames(tree, 3);

            var keyShots = new (string File, float Phase)[]
            {
                ("04-inspect-start.png", 0.04f),
                ("05-inspect-mid-sweep.png", 0.28f),
                ("06-inspect-peak-iris.png", 0.54f),
                ("07-inspect-peak-crack.png", 0.72f),
                ("08-inspect-return.png", 0.96f),
            };
            foreach (var (file, phase) in keyShots)
            {
                WitchAnimatedCardArt.DebugPhaseOverride = phase;
                await WaitFrames(tree, 2);
                await Capture(tree, Path.Combine(outDir, file));
            }

            // Capture 28 frames evenly across [0, 1) for real-game GIF preview of the 4.2s loop
            const int LoopFrames = 28;
            for (var i = 0; i < LoopFrames; i++)
            {
                WitchAnimatedCardArt.DebugPhaseOverride = i / (float)LoopFrames;
                await WaitFrames(tree, 2);
                await Capture(tree, Path.Combine(loopDir, $"frame_{i:D2}.png"), quiet: true);
            }
            GD.Print($"[SpireLabSmoke] OK captured {LoopFrames} real-game loop frames for R10 Inspect preview");

            // Upgrade preview toggle inside NInspectCardScreen (ZHS)
            WitchAnimatedCardArt.DebugPhaseOverride = 0.54f;
            var upgradeTick = inspect.GetNode<NTickbox>("%Upgrade");
            upgradeTick.IsTicked = true;
            upgradeTick.EmitSignal(NTickbox.SignalName.Toggled, upgradeTick);
            await WaitFrames(tree, 4);
            Check(inspectCard.Model is TheMirrorSelf { IsUpgraded: true }
                && inspectCtrl.DisplayState == CardArtDisplayState.Inspect
                && inspectPortrait.Material is ShaderMaterial,
                "upgraded R10 preview keeps animated Inspect shader active");
            await Capture(tree, Path.Combine(outDir, "09-inspect-upgraded-zhs.png"));
            upgradeTick.IsTicked = false;
            upgradeTick.EmitSignal(NTickbox.SignalName.Toggled, upgradeTick);
            await WaitFrames(tree, 3);

            // English localization inspect check (also tests close -> reopen lifecycle)
            inspect.Close();
            await WaitFor(tree, () => !inspect.Visible, "NInspectCardScreen closes before English reopen");
            LocManager.Instance.SetLanguage("eng");
            inspect.Open([r10HandModel, r01Model, r25Model], 0);
            await WaitFor(tree, () => inspect.Visible, "NInspectCardScreen reopens cleanly in English");
            await tree.ToSignal(tree.CreateTimer(0.35), SceneTreeTimer.SignalName.Timeout);
            Check(inspectCard.Model?.Title == "The Mirror Self"
                && inspectCtrl.DisplayState == CardArtDisplayState.Inspect
                && inspectPortrait.Material is ShaderMaterial,
                "English R10 inspect displays 'The Mirror Self' with animated shader active");
            await Capture(tree, Path.Combine(outDir, "10-inspect-eng.png"));
            LocManager.Instance.SetLanguage("zhs");
            WitchAnimatedCardArt.DebugPhaseOverride = null;

            // Rapid switching across [R10, R01, R25] 6 times
            var rightBtn = inspect.GetNode<NButton>("RightArrow");
            var leftBtn = inspect.GetNode<NButton>("LeftArrow");
            for (var cycle = 0; cycle < 3; cycle++)
            {
                rightBtn.EmitSignal(NClickableControl.SignalName.Released, rightBtn);
                await WaitFrames(tree, 2);
                Check(inspectCard.Model is PinnacleOfMana && inspectPortrait.Material == null && WitchAnimatedCardArt.ActiveControllerCount == 0,
                    $"rapid switch cycle {cycle + 1} -> R01 clears portrait material and stops controller");
                rightBtn.EmitSignal(NClickableControl.SignalName.Released, rightBtn);
                await WaitFrames(tree, 2);
                Check(inspectCard.Model is FallenConfidence && inspectPortrait.Material == null && WitchAnimatedCardArt.ActiveControllerCount == 0,
                    $"rapid switch cycle {cycle + 1} -> R25 stays static with zero active controllers");
                leftBtn.EmitSignal(NClickableControl.SignalName.Released, leftBtn);
                leftBtn.EmitSignal(NClickableControl.SignalName.Released, leftBtn);
                await WaitFrames(tree, 3);
                Check(inspectCard.Model is TheMirrorSelf && inspectCtrl.DisplayState == CardArtDisplayState.Inspect && inspectPortrait.Material is ShaderMaterial,
                    $"rapid switch cycle {cycle + 1} <- R10 cleanly re-activates Inspect animation");
            }

            // Close NInspectCardScreen -> verify full stop and material cleanup
            inspect.Close();
            await WaitFor(tree, () => !inspect.Visible, "NInspectCardScreen closes cleanly");
            await WaitFrames(tree, 4);
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0 && inspectPortrait.Material == null,
                "closing NInspectCardScreen stops all animations and releases shader material");

            // -----------------------------------------------------------------
            // 5. Card Reward Selection Screen with R10
            // -----------------------------------------------------------------
            var rewardR10 = run.CreateCard(ModelDb.Card<TheMirrorSelf>(), player);
            var rewardOther1 = run.CreateCard(ModelDb.Card<SilverScreen>(), player);
            var rewardOther2 = run.CreateCard(ModelDb.Card<RazorPages>(), player);
            var rewardScreen = NCardRewardSelectionScreen.ShowScreen(
                [new CardCreationResult(rewardR10), new CardCreationResult(rewardOther1), new CardCreationResult(rewardOther2)],
                []) ?? throw new InvalidOperationException("Expected NCardRewardSelectionScreen.");
            await WaitFrames(tree, 8);
            var rewardR10Holder = rewardScreen.GetNode<Control>("UI/CardRow").GetChildren().OfType<NGridCardHolder>()
                .First(holder => holder.CardModel is TheMirrorSelf);
            var rewardR10Ctrl = GetController(rewardR10Holder.CardNode!)!;
            Check(rewardR10Ctrl.DisplayState == CardArtDisplayState.Idle && rewardR10Holder.CardNode!.GetNode<TextureRect>("%Portrait").Material == null,
                "reward screen R10 starts static at rest");
            WitchAnimatedCardArt.DebugPhaseOverride = 0.28f;
            rewardR10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Focused, rewardR10Holder.Hitbox);
            await WaitFrames(tree, 4);
            Check(rewardR10Ctrl.DisplayState == CardArtDisplayState.Hover && rewardR10Ctrl.IsActive,
                "reward screen R10 wakes on hover");
            await Capture(tree, Path.Combine(outDir, "11-reward-hover.png"));
            WitchAnimatedCardArt.DebugPhaseOverride = null;
            rewardR10Holder.Hitbox.EmitSignal(NClickableControl.SignalName.Unfocused, rewardR10Holder.Hitbox);
            NOverlayStack.Instance?.Remove(rewardScreen);
            await WaitFrames(tree, 4);
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0, "removing reward screen leaves zero active controllers");

            // -----------------------------------------------------------------
            // 6. Consecutive combat plays (ensure zero gameplay change!)
            // -----------------------------------------------------------------
            await CreatureCmd.SetCurrentHp(player.Creature, 20);
            await PlayerCmd.SetEnergy(6, player);
            var enemy = player.Creature.CombatState!.HittableEnemies.First();
            var strike = piles.Hand.Cards.OfType<WitchStrike>().First();
            var barrier = piles.Hand.Cards.OfType<ColdMirrorBarrier>().First();
            Check(r10HandModel.TryManualPlay(null), "R10 enters native combat queue cleanly");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && WitchCombatState.For(player).MirrorForm,
                "R10 resolves identical V2.0 MirrorForm + MirrorHp inversion (20 real, 28 mirror)");
            Check(player.Creature.CurrentHp == 20 && WitchCombatState.For(player).MirrorHp == 28,
                "R10 animated art does not alter combat life inversion or mirror HP");
            Check(strike.TryManualPlay(enemy), "follow-up Strike plays cleanly");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && strike.Pile?.Type == PileType.Discard, "Strike finishes");
            Check(barrier.TryManualPlay(null), "follow-up Counterpoint ColdMirrorBarrier plays cleanly");
            await WaitFor(tree, () => !RunManager.Instance.ActionExecutor.IsRunning && barrier.Pile?.Type == PileType.Discard, "ColdMirrorBarrier finishes");
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0, "zero active animated card controllers remain after playing R10");

            // 7. Return to main menu & restart run (quit/re-enter lifecycle)
            await game.ReturnToMainMenuAfterRun();
            Check(WitchAnimatedCardArt.ActiveControllerCount == 0 && game.MainMenu != null,
                "returning to main menu leaves zero active controllers or leaks");

            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }

    private static WitchCardArtController? GetController(NCard card) =>
        card.GetNodeOrNull<TextureRect>("%Portrait")?.GetNodeOrNull<WitchCardArtController>(WitchCardArtController.ControllerName);

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
