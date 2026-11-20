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
            LocManager.Instance.SetLanguage("zhs");
            var witch = ModelDb.Character<TheWitch>();
            var menus = game.MainMenu!.SubmenuStack;
            var select = menus.GetSubmenuType<NCharacterSelectScreen>();
            select.InitializeSingleplayer();
            menus.Push(select);
            var button = select.GetNode<NCharacterSelectButton>($"CharSelectButtons/ButtonContainer/{witch.Id.Entry}_button");
            Check(!button.IsLocked, "Witch character button selectable");
            select.SelectCharacter(button, witch);
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
