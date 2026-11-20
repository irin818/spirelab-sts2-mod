using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.AutoSlay.Helpers;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

internal static class WitchVisualPreview
{
    public static async Task Run()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var game = NGame.Instance!;
            await game.GameStartupComplete;
            var startupAssets = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet)
                .Where(path => ResourceLoader.Exists(path)).Distinct().ToArray();
            await WaitFor(tree, () => PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(startupAssets),
                "deferred startup assets finish before the visual checks");
            if (NModalContainer.Instance?.OpenModal is NEarlyAccessDisclaimer disclaimer)
            {
                await disclaimer.CloseScreen();
            }
            Check(NModalContainer.Instance?.OpenModal == null, "no modal obscures the screenshots");
            SaveManager.Instance.SetFtuesEnabled(false);
            LocManager.Instance.SetLanguage("eng");
            var witch = ModelDb.Character<TheWitch>();
            var menus = game.MainMenu!.SubmenuStack;
            var select = menus.GetSubmenuType<NCharacterSelectScreen>();
            select.InitializeSingleplayer();
            menus.Push(select);
            var button = select.GetNode<NCharacterSelectButton>($"CharSelectButtons/ButtonContainer/{witch.Id.Entry}_button");
            Check(!button.IsLocked, "Witch character button selectable");
            select.SelectCharacter(button, witch);
            CheckSelectionText(select, "Character Trait • Arcane Insight", "choose 1 Witch reward card", "random card from your draw or discard pile",
                "Cracked Vanity Mirror", "first successful Arcane Insight swap each combat", "keeps Arcane Insight without this relic");
            await Capture(tree, "character-select-eng-macos.png");

            LocManager.Instance.SetLanguage("zhs");
            select.SelectCharacter(button, witch);
            CheckSelectionText(select, "角色特性 · 魔法启迪", "选1张魔女奖励牌临时加入抽牌堆", "抽牌堆或弃牌堆中的随机牌交换",
                "破损梳妆镜", "每场战斗第一次成功完成魔法启迪随机交换", "失去此遗物后，魔法启迪仍然生效");
            Check(select.GetNode<TextureRect>($"AnimatedBg/{witch.Id.Entry}_bg").Texture.GetSize() == new Vector2(1600, 900),
                "Witch selection candidate loaded from PCK");
            await Capture(tree, "character-select-macos.png");

            var run = await game.StartNewSingleplayerRun(witch, false, ActModel.GetDefaultList(), [], "WITCHART2", GameMode.Standard);
            var player = run.Players.Single();
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);

            await WaitFor(tree, () => NOverlayStack.Instance?.Peek() is NSimpleCardSelectScreen,
                "actual Fate Selection overlay opens before the first draw");
            var fate = (NSimpleCardSelectScreen)NOverlayStack.Instance!.Peek()!;
            var grid = fate.GetNode<NCardGrid>("%CardGrid");
            await WaitFor(tree, () => grid.CurrentlyDisplayedCardHolders.Any(), "Fate grid creates native card holders");
            await Capture(tree, "fate-selection-macos.png");
            var holder = grid.CurrentlyDisplayedCardHolders.First();
            var fateId = holder.CardModel.Id;
            grid.EmitSignal(NCardGrid.SignalName.HolderPressed, holder);
            await UiHelper.Click(fate.GetNode<NConfirmButton>("%Confirm"));
            await WaitFor(tree, () => NPlayerHand.Instance?.IsInCardSelection == true, "actual optional swap opens after drawing");
            await UiHelper.Click(NPlayerHand.Instance!.GetNode<NConfirmButton>("%SelectModeConfirmButton"));

            var piles = player.PlayerCombatState!;
            await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "native combat hand ready");
            Check(WitchCombatState.For(player).OpeningCard?.Id == fateId && player.Deck.Cards.Count == 10,
                "native Fate selection matches the chosen combat-only card");
            foreach (var card in piles.Hand.Cards.ToArray())
            {
                await CardCmd.Discard(new ThrowingPlayerChoiceContext(), card);
            }

            WitchCard[] cards = [await Add<PinnacleOfMana>(player), await Add<TheMirrorSelf>(player), await Add<FallenConfidence>(player),
                await Add<ForbiddenFrontispiece>(player)];
            Check(piles.Hand.Cards.Count == cards.Length && cards.All(card =>
                card.Pile?.Type == PileType.Hand &&
                ResourceLoader.Load<Texture2D>(card.PortraitPath)?.GetSize() == new Vector2(500, 380)),
                "native hand shows the four themed card portraits including the integrated U31 art");
            await Capture(tree, "card-hand-macos.png");
            await Capture(tree, "status-before-macos.png");
            await CreatureCmd.SetCurrentHp(player.Creature, 20);
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), cards[1], null);
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), cards[0], null);
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), cards[2], null);
            var pinnacle = player.Creature.GetPower<PinnaclePower>();
            Check(pinnacle is { IsVisible: true }
                && player.Creature.GetPower<MirrorFormPower>()?.IsVisible == true
                && player.Creature.GetPower<DesireDiscountPower>()?.IsVisible == true,
                "three persistent powers appear on the real combat state");
            if (System.Environment.GetEnvironmentVariable("SPIRELAB_ART_SYNC_TEST") == "1")
            {
                var relic = witch.StartingRelics[0];
                Check(relic.Icon.GetSize() == new Vector2(94, 94)
                    && pinnacle!.CustomPackedIconPath.EndsWith("/powers/pinnacle.png", StringComparison.Ordinal)
                    && pinnacle.Icon.GetSize() == new Vector2(64, 64)
                    && ResourceLoader.Load<Texture2D>(pinnacle.CustomBigIconPath)?.GetSize() == new Vector2(256, 256)
                    && ResourceLoader.Load<Texture2D>(cards[0].PortraitPath).GetSize() == new Vector2(500, 380),
                    "the PCK resolves the card, relic and both native sizes of the replacement Power icon");
            }
            await Capture(tree, "mirror-form-macos.png");
            var powers = player.Creature.GetCreatureNode()!.GetNode<Control>("%HealthBar")
                .GetNode<NPowerContainer>("%PowerContainer");
            var badge = powers.GetChildren().OfType<NPower>().Single(power => power.Model == pinnacle);
            var tooltips = game.HoverTipsContainer ?? throw new InvalidOperationException("The native hover tips container is missing.");
            badge.EmitSignal(Control.SignalName.MouseEntered);
            await WaitFor(tree, () => tooltips.GetChildren().OfType<NHoverTipSet>()
                .Where(tip => tip.IsVisibleInTree()).SelectMany(tip => tip.FindChildren("Description", "RichTextLabel", true, false))
                .OfType<RichTextLabel>().Any(label => label.GetParsedText().Contains("回合结束保留全部未用能量")),
                "native hover shows the Simplified Chinese Pinnacle power tooltip");
            await Capture(tree, "power-tooltip-macos.png");
            badge.EmitSignal(Control.SignalName.MouseExited);

            var choice = new ThrowingPlayerChoiceContext();
            await CardCmd.AutoPlay(choice, cards[3], null);
            await Play<BloodstainedBookmark>(player, choice);
            await Play<HallOfGlass>(player, choice);
            await Play<RefinedAbyss>(player, choice);
            await Play<ManaPump>(player, choice, upgrade: true);
            await Play<ReflectiveInscription>(player, choice, upgrade: true);
            await Play<MirrorBurial>(player, choice);
            await Play<TemporalSeal>(player, choice);
            await Play<MirrorTransfer>(player, choice);
            await Play<MoonInTheMirror>(player, choice);
            Check(powers.GetChildren().OfType<NPower>().Select(node => node.Model).Any(power => power is WitchImmunityPower)
                && player.Creature.GetPower<GrimoireRetainPower>()?.Amount == 1
                && player.Creature.GetPower<BookmarkPower>()?.Amount == 1
                && player.Creature.GetPower<GlassHallPower>()?.Amount == 5
                && player.Creature.GetPower<RefinedAbyssPower>()?.Amount == 1
                && player.Creature.GetPower<ManaPumpPower>()?.Amount == 3,
                "native buffs show after playing persistent cards and the shared attack-immunity effect");
            Check(player.Creature.GetPower<PendingEnergyPower>()?.Amount == 2
                && player.Creature.GetPower<PendingBlockPower>()?.Amount == 3
                && player.Creature.GetPower<PendingDrawPower>()?.Amount == 2
                && player.Creature.GetPower<ReflectChargesPower>()?.Amount == 1,
                "delayed energy, block, draw and one blocked reflection show their real amounts");
            await Capture(tree, "status-active-macos.png");

            var creature = player.Creature;
            var enemy = creature.CombatState!.HittableEnemies.First();
            var originalBlock = creature.Block;
            await CreatureCmd.Damage(choice, creature, 4, ValueProp.Move, enemy);
            Check(creature.Block == originalBlock && creature.GetPower<WitchImmunityPower>() == null,
                "the first enemy hit consumes immunity without losing Block");
            await CreatureCmd.Damage(choice, creature, 4, ValueProp.Move, enemy);
            Check(creature.GetPower<ReflectChargesPower>() == null && creature.GetPower<InscribedSkillPower>()?.Amount == 1,
                "the next blocked hit consumes the reflection charge and queues one Inscription");
            await Capture(tree, "status-after-hit-macos.png");

            using var selection = CardSelectCmd.UseSelector(new WitchSelector());
            var turn = piles.TurnNumber;
            CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
            await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play,
                "the game advances through the enemy turn and next draw");
            Check(creature.GetPower<PendingEnergyPower>() == null && creature.GetPower<PendingDrawPower>() == null
                && creature.GetPower<PendingBlockPower>() == null && creature.GetPower<ManaPumpPower>()?.Amount == 2,
                "turn start consumes delayed icons and decrements the pump while persistent powers remain");
            await Play<WitchDefend>(player, choice);
            Check(creature.GetPower<InscribedSkillPower>() == null,
                "playing the first Skill consumes the current Inscription discount");
            await Capture(tree, "status-next-turn-macos.png");
            GD.Print("[SpireLabSmoke] PASS");
            game.Quit();
        }
        catch (Exception error)
        {
            GD.PushError($"[SpireLabSmoke] FAIL {error}");
            tree.Quit(1);
        }
    }

    private static void CheckSelectionText(NCharacterSelectScreen select, string traitTitle, string optionalSwap,
        string randomSource, string relicTitle, string relicTrigger, string keepsTrait)
    {
        var panel = select.GetNode<Control>("InfoPanel/VBoxContainer");
        var trait = panel.GetNode<RichTextLabel>("DescriptionLabel").GetParsedText();
        var relic = panel.GetNode<RichTextLabel>("Relic/Description").GetParsedText();
        Check(trait.StartsWith(traitTitle + "\n", StringComparison.Ordinal) && trait.Contains(optionalSwap)
            && trait.Contains(randomSource) && !trait.Contains("[gold]"), $"{traitTitle} uses the native character description");
        Check(panel.GetNode<RichTextLabel>("Relic/Name/RichTextLabel").GetParsedText() == relicTitle
            && relic.Contains(relicTrigger) && relic.Contains(keepsTrait), $"{relicTitle} explains its separate Block trigger");
    }

    private static async Task Capture(SceneTree tree, string fileName)
    {
        // Selection panel, hand layout and battle entrance animate for several frames.
        await tree.ToSignal(tree.CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout);
        await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var destination = System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_ART_SCREENSHOTS")
            ?? throw new InvalidOperationException("Missing screenshot directory.");
        using var image = tree.Root.GetTexture().GetImage();
        Check(image.SavePng(Path.Combine(destination, fileName)) == Error.Ok, $"captured {fileName}");
    }
}
