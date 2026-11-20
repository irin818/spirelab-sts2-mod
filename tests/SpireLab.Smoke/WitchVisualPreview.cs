using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
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
            var commonScenes = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet)
                .Where(path => path.EndsWith(".tscn", StringComparison.Ordinal) && ResourceLoader.Exists(path))
                .ToArray();
            await WaitFor(tree, () => commonScenes.All(PreloadManager.Cache.GetLoadedCacheAssets().Contains),
                "main menu scenes available");
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

            using var selection = CardSelectCmd.UseSelector(new WitchSelector());
            var run = await game.StartNewSingleplayerRun(witch, false, ActModel.GetDefaultList(), [], "WITCHART2", GameMode.Standard);
            var player = run.Players.Single();
            await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
            var piles = player.PlayerCombatState!;
            await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "native combat hand ready");
            foreach (var card in piles.Hand.Cards.ToArray())
            {
                await CardCmd.Discard(new ThrowingPlayerChoiceContext(), card);
            }

            WitchCard[] cards = [await Add<PinnacleOfMana>(player), await Add<TheMirrorSelf>(player), await Add<FallenConfidence>(player)];
            Check(piles.Hand.Cards.Count == 3 && cards.All(card =>
                card.Pile?.Type == PileType.Hand &&
                ResourceLoader.Load<Texture2D>(card.PortraitPath)?.GetSize() == new Vector2(500, 380)),
                "three native hand cards use the candidate portraits");
            await Capture(tree, "card-hand-macos.png");
            await CreatureCmd.SetCurrentHp(player.Creature, 20);
            await CardCmd.AutoPlay(new ThrowingPlayerChoiceContext(), cards[1], null);
            await Capture(tree, "mirror-form-macos.png");
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
