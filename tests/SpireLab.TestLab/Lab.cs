using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Combat.History.Entries;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.CardRewardAlternatives;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using SpireLab.SpireLabCode.Witch.UI;

namespace SpireLab.TestLab;

[ModInitializer(nameof(Initialize))]
public static class LabEntry
{
    public static void Initialize()
    {
        if (OS.GetCmdlineArgs().Contains("--witch-test-lab"))
            Callable.From(() => { _ = Launch(); }).CallDeferred();
    }

    private static async Task Launch()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            var profile = System.Environment.GetEnvironmentVariable("SPIRELAB_TEST_LAB_PROFILE");
            var root = System.Environment.GetEnvironmentVariable("SPIRELAB_TEST_LAB_ROOT");
            if (profile == null || !profile.StartsWith("SpireLabTestLab-", StringComparison.Ordinal)
                || Path.GetFileName(OS.GetUserDataDir()) != profile || root == null
                || !OS.GetExecutablePath().StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                || !OS.GetCmdlineArgs().Contains("--force-steam=off"))
                throw new InvalidOperationException("A verified isolated application and save profile are required.");
            var verify = System.Environment.GetEnvironmentVariable("SPIRELAB_TEST_LAB_VERIFY") == "true";
            await new Lab(tree, root, verify).Run(
                System.Environment.GetEnvironmentVariable("SPIRELAB_TEST_LAB_SCENARIO") ?? "mirror-basic");
        }
        catch (Exception error)
        {
            GD.PushError($"[WitchTestLab] FAIL {error}");
            tree.Quit(1);
        }
    }
}

internal sealed partial class Lab(SceneTree tree, string root, bool verify)
{
    internal static readonly string[] Scenarios = ["mirror-basic", "mirror-stacking", "mirror-multihit",
        "mirror-hp", "desire-greed", "desire-sloth", "desire-gamble", "arcane-insight", "early-game"];
    private static readonly PileType[] Piles = [PileType.Hand, PileType.Draw, PileType.Discard, PileType.Exhaust];
    private readonly string _root = root;
    private readonly bool _verify = verify;
    private readonly TheWitch _witch = ModelDb.Character<TheWitch>();
    private readonly PlayerChoiceContext _choice = new BlockingPlayerChoiceContext();
    private readonly List<string> _records = [];
    private readonly string _snapshotPath = Path.Combine(verify ? root : Path.GetDirectoryName(root)!, "last-scenario.json");
    private Player _player = null!;
    private EncounterModel _encounter = ModelDb.Encounter<NibbitsWeak>();
    private string _scenario = "mirror-basic";
    private string _seed = "WITCHLAB";
    private int _historyIndex;
    private int _damageSerial;
    private bool _busy;
    private Creature Witch => _player.Creature;
    private WitchCombatState State => WitchCombatState.For(_player);
    private PlayerCombatState Combat => _player.PlayerCombatState!;

    public async Task Run(string scenario)
    {
        // The game displays its main menu before the deferred startup preload finishes.
        using var loading = new LabLoadingOverlay(tree.Root);
        await NGame.Instance!.GameStartupComplete;
        var assets = AssetSets.CommonAssets.Concat(AssetSets.MainMenuSet).Where(path => ResourceLoader.Exists(path)).Distinct().ToArray();
        await Wait(() => PreloadManager.Cache.GetLoadedCacheAssets().IsSupersetOf(assets));
        if (_verify) VerifyStartupInputGuard();
        SaveManager.Instance.SetFtuesEnabled(false);
        GD.Print($"[WitchTestLab] ISOLATED {OS.GetUserDataDir()}");
        DrawControls();
        CombatManager.Instance.History.Changed += ReadHistory;
        CombatManager.Instance.CombatWon += _ => Report($"胜利 · 真实生命 {Witch.CurrentHp}/{Witch.MaxHp}");
        CombatManager.Instance.TurnEnded += state => Report($"第 {state.RoundNumber} 轮 · {state.CurrentSide} 回合结束");
        WitchCombatState.DebugTrace += Report;
        await Start(scenario == "mirror-reflect" ? "mirror-basic" : scenario, startupOverlay: loading);
        if (_verify)
            await Verify();
    }

    private async Task Start(string scenario, string? seed = null, LabLoadingOverlay? startupOverlay = null)
    {
        using var loading = startupOverlay ?? new LabLoadingOverlay(tree.Root);
        _scenario = scenario;
        _scenarioSelect.Select(Array.IndexOf(Scenarios, scenario));
        _seed = seed ?? "WITCHLAB";
        if (RunManager.Instance.IsInProgress)
            await NGame.Instance!.ReturnToMainMenuAfterRun();
        var run = await NGame.Instance!.StartNewSingleplayerRun(_witch, false, ActModel.GetDefaultList(), [], _seed, GameMode.Standard);
        _player = run.Players.Single();
        var manualOpening = scenario == "arcane-insight";
        using var selector = manualOpening ? null : CardSelectCmd.PushSelector(new InitialChoice());
        var encounter = _encounter.ToMutable();
        if (encounter is LabDummyEncounter scripted)
        {
            scripted.Dummies = (int)_dummyCount.Value;
            scripted.Hits = ParseHits();
            scripted.NonAttack = _nonAttack.ButtonPressed;
        }
        var entering = RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: encounter, showTransition: false);
        if (manualOpening)
        {
            // Let the player use the native picker even if the first turn is still waiting on their choice.
            await Wait(() => entering.IsCompleted || NOverlayStack.Instance?.Peek() is WitchFateScreen);
            loading.Dispose();
        }
        await entering;
        await Wait(() => _player.PlayerCombatState?.Phase == PlayerTurnPhase.Play);
        await CreatureCmd.SetCurrentHp(Witch, scenario is "early-game" or "arcane-insight" ? 68 : scenario is "mirror-hp" or "desire-sloth" ? 20 : 34);
        if (scenario is "mirror-basic" or "mirror-stacking" or "mirror-multihit" or "mirror-hp")
        {
            await AddToPlay<TheMirrorSelf>();
            if (scenario == "mirror-stacking")
            {
                await AddToPlay<MirrorMaze>();
                await AddToPlay<MirrorBurial>();
            }
            await SetBlock(10);
        }
        else if (scenario == "desire-greed")
        {
            await AddToPlay<FallenConfidence>();
            await Add(ModelDb.Card<PrimalGreed>(), PileType.Hand, upgraded: true);
        }
        else if (scenario == "desire-sloth")
            await Add(ModelDb.Card<Sloth>(), PileType.Hand);
        else if (scenario == "desire-gamble")
            await Add(ModelDb.Card<AbyssOfGambling>(), PileType.Hand);
        await PlayerCmd.SetEnergy(3, _player);
        Report($"开始 {scenario} · {_encounter.Id.Entry} · {_seed}");
        UpdatePiles();
    }

    private sealed class InitialChoice : ICardSelector
    {
        public Task<IEnumerable<CardModel>> GetSelectedCards(IEnumerable<CardModel> options, int minSelect, int maxSelect) =>
            Task.FromResult(options.Take(minSelect));

        public CardRewardSelection GetSelectedCardReward(IReadOnlyList<CardCreationResult> options,
            IReadOnlyList<CardRewardAlternative> alternatives) =>
            throw new InvalidOperationException("Unexpected reward choice in Test Lab.");
    }

    private async Task Wait(Func<bool> condition)
    {
        for (var frame = 0; frame < 2400 && !condition(); frame++)
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        if (!condition()) throw new InvalidOperationException("等待游戏进入操作阶段超时。");
    }

    private async Task Action(Func<Task> action, bool requireFight = true)
    {
        if (_busy) return;
        if (requireFight && (_player?.PlayerCombatState?.Phase != PlayerTurnPhase.Play || !CombatManager.Instance.IsInProgress))
        {
            Report("等待玩家操作阶段，或重开测试。");
            return;
        }
        _busy = true;
        try { await action(); }
        catch (Exception exception) { Report(exception.Message); GD.PushError($"[WitchTestLab] {exception}"); }
        finally { _busy = false; UpdatePiles(); }
    }

    private async Task<CardModel> Add(CardModel definition, PileType pile, bool upgraded = false)
    {
        var card = ((CombatState)Witch.CombatState!).CreateCard(definition, _player);
        if (upgraded) CardCmd.Upgrade(card);
        var result = await CardPileCmd.AddGeneratedCardToCombat(card, pile, _player);
        if (!result.success && card.Pile == null) card.RemoveFromState();
        return card;
    }

    private async Task AddToPlay<T>(bool upgrade = false) where T : CardModel
    {
        if (Combat.Hand.Cards.Count == CardPile.MaxCardsInHand)
            await CardCmd.Discard(_choice, Combat.Hand.Cards.First());
        var card = await Add(ModelDb.Card<T>(), PileType.Hand, upgrade);
        await CardCmd.AutoPlay(_choice, card, null);
    }

    private async Task SetBlock(int amount)
    {
        if (Witch.Block > amount) await CreatureCmd.LoseBlock(_choice, Witch, Witch.Block - amount, null);
        if (Witch.Block < amount) await CreatureCmd.GainBlock(Witch, amount - Witch.Block, ValueProp.Unpowered, null);
    }

    private async Task Hit(Creature enemy, int damage, bool nonAttack = false)
    {
        var before = (Witch.Block, Witch.CurrentHp, State.MirrorHp, EnemyHp: enemy.CurrentHp);
        var props = nonAttack ? ValueProp.Unpowered | ValueProp.Unblockable : ValueProp.Move;
        var result = (await CreatureCmd.Damage(_choice, Witch, damage, props, enemy)).Single();
        Report($"#{++_damageSerial} {enemy.Name} 来伤 {damage}，格挡 {before.Block} → {Witch.Block}，实际吸收 {result.BlockedDamage}，真实生命 {before.CurrentHp} → {Witch.CurrentHp}，镜像 {before.MirrorHp} → {State.MirrorHp}，敌方生命 {before.EnemyHp} → {enemy.CurrentHp}");
        await CombatManager.Instance.CheckWinCondition();
    }

    private void Report(string message)
    {
        _records.Add(message);
        GD.Print($"[WitchTestLab] {message}");
        if (_log != null)
        {
            _log.Text = string.Join('\n', _records.TakeLast(36));
            _log.SetCaretLine(_log.GetLineCount() - 1);
        }
    }

    private void ReadHistory()
    {
        var entries = CombatManager.Instance.History.Entries;
        var count = entries.Count();
        foreach (var entry in entries.Skip(_historyIndex))
        {
            if (entry is CardPlayStartedEntry { CardPlay: var play } && play.Card is WitchCard card)
                Report($"{card.SpecId} {card.Title} · 结算费用 {play.Resources.EnergyValue}，实际支付 {play.Resources.EnergySpent} 能量");
            else
                Report(entry.HumanReadableString);
        }
        _historyIndex = count;
    }

    private sealed record SavedCard(string Id, PileType Pile, bool Upgraded);
    private sealed record SavedPower(string Id, int Amount);
    private sealed record SavedFight(string Scenario, string Encounter, string Seed, int Real, int Max, int Mirror,
        int Energy, int Block, SavedCard[] Cards, string[] Relics, string[] Potions, SavedPower[] Powers,
        Dictionary<string, int> States, string Hits, int[] EnemyHealth, bool NonAttack, int EnemySlot,
        int[] DeferredReflections, int[] ManaPumps, int DummyCount);

    private async Task Save()
    {
        var data = new SavedFight(_scenario, _encounter.Id.Entry, _seed, Witch.CurrentHp, Witch.MaxHp, State.MirrorHp,
            Combat.Energy, Witch.Block,
            Piles.SelectMany(pile => pile.GetPile(_player).Cards.Where(card => card is WitchCard)
                .Select(card => new SavedCard(card.Id.Entry, pile, card.IsUpgraded))).ToArray(),
            _player.Relics.Select(r => r.Id.Entry).ToArray(), _player.Potions.Select(p => p.Id.Entry).ToArray(),
            Witch.Powers.Where(power => power is not WitchVisiblePower).Select(power => new SavedPower(power.Id.Entry, power.Amount)).ToArray(),
            Statuses.ToDictionary(status => status.Name, status => status.Read(State)),
            _hits.Text, Witch.CombatState!.HittableEnemies.Select(enemy => enemy.CurrentHp).ToArray(),
            _nonAttack.ButtonPressed, (int)_enemySlot.Value, State.DeferredReflections.ToArray(), State.ManaPumpDurations.ToArray(),
            (int)_dummyCount.Value);
        var temporary = _snapshotPath + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, _snapshotPath, overwrite: true);
        Report($"场景已保存 · {_snapshotPath}");
    }

    private async Task Load()
    {
        var data = JsonSerializer.Deserialize<SavedFight>(await File.ReadAllTextAsync(_snapshotPath))!;
        _encounter = _encounters.Single(encounter => encounter.Id.Entry == data.Encounter);
        _hits.Text = data.Hits;
        _nonAttack.ButtonPressed = data.NonAttack;
        _dummyCount.Value = data.DummyCount;
        await Start("early-game", data.Seed);
        _scenario = data.Scenario;
        _scenarioSelect.Select(Array.IndexOf(Scenarios, _scenario));
        _encounterSelect.Select(Array.IndexOf(_encounters, _encounter));
        await ClearPiles();
        foreach (var card in data.Cards)
            await Add(_cards.Single(model => model.Id.Entry == card.Id), card.Pile, card.Upgraded);
        foreach (var relic in _player.Relics.ToArray()) await RelicCmd.Remove(relic);
        foreach (var relic in data.Relics) await RelicCmd.Obtain(_relics.Single(model => model.Id.Entry == relic).ToMutable(), _player);
        foreach (var potion in data.Potions) await PotionCmd.TryToProcure(_potions.Single(model => model.Id.Entry == potion).ToMutable(), _player);
        foreach (var power in data.Powers)
            await PowerCmd.Apply(_choice, _nativePowers.Single(model => model.Id.Entry == power.Id).ToMutable(), Witch, power.Amount, Witch, null);
        await CreatureCmd.SetMaxHp(Witch, data.Max);
        await CreatureCmd.SetCurrentHp(Witch, data.Real);
        await PlayerCmd.SetEnergy(data.Energy, _player);
        await SetBlock(data.Block);
        State.MirrorHp = Math.Clamp(data.Mirror, 0, WitchRules.MirrorCapacity(Witch.CurrentHp, Witch.MaxHp));
        foreach (var status in Statuses)
            if (data.States.TryGetValue(status.Name, out var amount)) status.Write(State, amount);
        State.DeferredReflections.Clear();
        State.DeferredReflections.AddRange(data.DeferredReflections);
        State.ManaPumpDurations.Clear();
        State.ManaPumpDurations.AddRange(data.ManaPumps);
        _enemySlot.Value = data.EnemySlot;
        foreach (var (enemy, health) in Witch.CombatState!.HittableEnemies.Zip(data.EnemyHealth))
        {
            await CreatureCmd.SetMaxHp(enemy, Math.Max(enemy.MaxHp, health));
            await CreatureCmd.SetCurrentHp(enemy, health);
        }
        await RefreshStatuses();
        Report($"场景已恢复 · 种子 {data.Seed}");
    }

    private async Task ClearPiles()
    {
        var cards = Piles.SelectMany(pile => pile.GetPile(_player).Cards).ToArray();
        if (cards.Length != 0) await CardPileCmd.RemoveFromCombat(cards, skipVisuals: true);
    }

    private Task RefreshStatuses()
    {
        WitchLife.RefreshDisplay(Witch, State);
        return WitchStatuses.Refresh(_choice, _player, State);
    }
}
