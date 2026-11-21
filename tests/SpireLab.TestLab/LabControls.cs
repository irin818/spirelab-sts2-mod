using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using SpireLab.SpireLabCode.Witch.UI;

namespace SpireLab.TestLab;

internal sealed partial class Lab
{
    private sealed record Status(string Name, Func<WitchCombatState, int> Read, Action<WitchCombatState, int> Write);
    private static readonly Status[] Statuses =
    [
        new("镜中人", s => s.MirrorForm ? 1 : 0, (s, v) => s.MirrorForm = v > 0),
        new("镜面迷宫 %", s => s.MazeReflectPercent, (s, v) => s.MazeReflectPercent = v),
        new("镜葬 %", s => s.NextReflectPercent, (s, v) => s.NextReflectPercent = v),
        new("下一次固定反伤", s => s.DeferredReflections.Sum(), (s, v) => { s.DeferredReflections.Clear(); if (v > 0) s.DeferredReflections.Add(v); }),
        new("怠惰免疫", s => s.SlothImmunity ? 1 : 0, (s, v) => s.SlothImmunity = v > 0),
        new("镜花水月", s => s.Immunity ?? 0, (s, v) => s.Immunity = v > 0 ? v : null),
        new("魔力之巅", s => s.RetainEnergy ? 1 : 0, (s, v) => s.RetainEnergy = v > 0),
        new("堕落自信", s => s.DesireDiscount ? 1 : 0, (s, v) => s.DesireDiscount = v > 0),
        new("额外起手能量", s => s.StartingEnergy, (s, v) => s.StartingEnergy = v),
        new("魔力泵回合", s => s.ManaPumpDurations.DefaultIfEmpty().Max(), (s, v) => { s.ManaPumpDurations.Clear(); if (v > 0) s.ManaPumpDurations.Add(v); }),
        new("镜厅格挡", s => s.RetainedBlockCap, (s, v) => s.RetainedBlockCap = v),
        new("禁书扉页保留", s => s.RetainRandomCount, (s, v) => s.RetainRandomCount = v),
        new("染血书签能量", s => s.BookmarkEnergy, (s, v) => s.BookmarkEnergy = v),
        new("反光刻印", s => s.InscriptionEnabled ? 1 : 0, (s, v) => s.InscriptionEnabled = v > 0),
        new("虚像减伤", s => s.BlockReduction, (s, v) => s.BlockReduction = v),
        new("真实之影", s => s.ShadowDraw, (s, v) => s.ShadowDraw = v),
        new("延迟能量", s => s.NextEnergy, (s, v) => s.NextEnergy = v),
        new("延迟格挡", s => s.NextBlock, (s, v) => s.NextBlock = v),
        new("延迟抽牌", s => s.NextDraw, (s, v) => s.NextDraw = v)
    ];
    private static readonly string[] SuitKeys = ["", "potion", "mirror", "grimoire", "desire"];
    private static readonly CardRarity[] Rarities = [CardRarity.Basic, CardRarity.Common, CardRarity.Uncommon, CardRarity.Rare, CardRarity.Ancient];
    private WitchCard[] _cards = [];
    private WitchCard[] _visibleCards = [];
    private RelicModel[] _relics = [];
    private PotionModel[] _potions = [];
    private PowerModel[] _nativePowers = [];
    private EncounterModel[] _encounters = [];
    private ItemList _cardList = null!;
    private ItemList _pileList = null!;
    private TextEdit _log = null!;
    private LineEdit _hits = null!;
    private SpinBox _enemySlot = null!;
    private SpinBox _dummyCount = null!;
    private CheckBox _nonAttack = null!;
    private OptionButton _scenarioSelect = null!;
    private OptionButton _encounterSelect = null!;
    private OptionButton _pileSelect = null!;
    private RichTextLabel _description = null!;

    private void DrawControls()
    {
        _cards = _witch.CardPool.AllCards.OfType<WitchCard>().OrderBy(card => card.SpecId, StringComparer.Ordinal).ToArray();
        _relics = _witch.RelicPool.AllRelics.ToArray();
        _potions = _witch.PotionPool.AllPotions.ToArray();
        _nativePowers = ModelDb.AllPowers.Where(power => power is not WitchVisiblePower).OrderBy(power => power.Id.Entry).ToArray();
        _encounters = ModelDb.AllEncounters.Where(encounter => encounter.RoomType == MegaCrit.Sts2.Core.Rooms.RoomType.Monster)
            .Append(_encounter).Append(ModelDb.Encounter<LabDummyEncounter>()).Distinct().OrderBy(encounter => encounter.Id.Entry).ToArray();

        var layer = new CanvasLayer { Layer = 40 };
        tree.Root.AddChild(layer);
        var rootControl = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        layer.AddChild(rootControl);
        rootControl.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var (leftPanel, left) = SidePanel(rootControl, right: false);
        var (rightPanel, right) = SidePanel(rootControl, right: true);
        var toggle = new Button { Text = "Witch Test Lab · DEBUG" };
        rootControl.AddChild(toggle);
        toggle.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterTop);
        toggle.OffsetLeft = -115;
        toggle.OffsetRight = 115;
        toggle.OffsetTop = 87;
        toggle.OffsetBottom = 129;
        toggle.Pressed += () =>
        {
            var visible = !leftPanel.Visible;
            leftPanel.Visible = visible;
            rightPanel.Visible = visible;
        };

        Label(left, "魔女测试台 · 91 张注册牌");
        var search = new LineEdit { PlaceholderText = "中文卡名 / ID" };
        left.AddChild(search);
        var rarity = Menu(left, ["全部稀有度", "基础", "普通", "罕见", "稀有", "古代"]);
        var cost = Menu(left, ["全部费用", "0", "1", "2", "3", "4+", "X"]);
        var suit = Menu(left, ["全部体系", "魔药", "镜子", "魔法书", "欲望"]);
        _cardList = new ItemList { CustomMinimumSize = new Vector2(0, 220) };
        left.AddChild(_cardList);
        _description = new RichTextLabel { BbcodeEnabled = true, CustomMinimumSize = new Vector2(0, 145), FitContent = true };
        left.AddChild(_description);
        var upgrade = new CheckBox { Text = "升级版" };
        left.AddChild(upgrade);
        _pileSelect = Menu(left, ["手牌", "抽牌堆", "弃牌堆", "消耗堆"]);
        var count = Number(left, "数量", 1, 1, 30);
        Button(left, "添加选中卡牌", () => Action(async () =>
        {
            var index = _cardList.GetSelectedItems().FirstOrDefault(-1);
            if (index < 0) return;
            for (var i = 0; i < count.Value; i++) await Add(_visibleCards[index], Piles[_pileSelect.Selected], upgrade.ButtonPressed);
        }));
        _pileList = new ItemList { CustomMinimumSize = new Vector2(0, 146) };
        left.AddChild(_pileList);
        _pileSelect.ItemSelected += _ => UpdatePiles();
        Button(left, "移除选中实例", () => Action(async () =>
        {
            var index = _pileList.GetSelectedItems().FirstOrDefault(-1);
            var cards = Piles[_pileSelect.Selected].GetPile(_player).Cards;
            if (index >= 0 && index < cards.Count) await CardPileCmd.RemoveFromCombat(cards[index]);
        }));
        Button(left, "清空四个可操作牌堆", () => Action(ClearPiles));
        Button(left, "将弃牌堆洗入抽牌堆", () => Action(() => CardPileCmd.Shuffle(_choice, _player)));
        Button(left, "抽取指定数量", () => Action(() => CardPileCmd.Draw(_choice, (int)count.Value, _player)));
        Button(left, "弃置所有手牌", () => Action(async () =>
        {
            foreach (var card in Combat.Hand.Cards.ToArray()) await CardCmd.Discard(_choice, card);
        }));
        Button(left, "移除所有手牌", () => Action(async () =>
        {
            if (!Combat.Hand.IsEmpty) await CardPileCmd.RemoveFromCombat(Combat.Hand.Cards.ToArray());
        }));
        void Describe()
        {
            var index = _cardList.GetSelectedItems().FirstOrDefault(-1);
            if (index < 0) { _description.Text = ""; return; }
            var card = _visibleCards[index];
            var family = WitchCardAppearance.Suit(card) switch
            {
                "potion" => "魔药", "mirror" => "镜子", "grimoire" => "魔法书", "desire" => "欲望", _ => "基础"
            };
            _description.Text = $"{card.SpecId}  {card.Title}  {card.Rarity} / {card.Type} / {family}\n"
                + (upgrade.ButtonPressed && card.IsUpgradable
                    ? card.GetDescriptionForUpgradePreview() : card.GetDescriptionForPile(PileType.None));
        }
        void Filter()
        {
            _visibleCards = _cards.Where(card =>
                (card.Title.Contains(search.Text, StringComparison.OrdinalIgnoreCase)
                 || card.SpecId.Contains(search.Text, StringComparison.OrdinalIgnoreCase)
                 || card.Id.Entry.Contains(search.Text, StringComparison.OrdinalIgnoreCase))
                && (rarity.Selected == 0 || card.Rarity == Rarities[rarity.Selected - 1])
                && WitchFateScreen.MatchesCost(card, cost.Selected)
                && (suit.Selected == 0 || WitchCardAppearance.Suit(card) == SuitKeys[suit.Selected])).ToArray();
            _cardList.Clear();
            foreach (var card in _visibleCards)
                _cardList.AddItem($"{card.SpecId}  {card.Title}  {PrintedCost(card, upgrade.ButtonPressed)}");
            if (_visibleCards.Length != 0) _cardList.Select(0);
            Describe();
        }
        search.TextChanged += _ => Filter();
        rarity.ItemSelected += _ => Filter();
        cost.ItemSelected += _ => Filter();
        suit.ItemSelected += _ => Filter();
        upgrade.Toggled += _ => Filter();
        _cardList.ItemSelected += _ => Describe();
        Filter();

        Label(right, "战斗与场景");
        _scenarioSelect = Menu(right, Scenarios);
        _scenarioSelect.Select(Array.IndexOf(Scenarios, _scenario));
        _encounterSelect = Menu(right, _encounters.Select(encounter => encounter is LabDummyEncounter ? "脚本木桩（仅实验室）" : encounter.Id.Entry));
        _encounterSelect.Select(Array.IndexOf(_encounters, _encounter));
        Button(right, "启动场景 / 重新开始", () => Action(() =>
        {
            _encounter = _encounters[_encounterSelect.Selected];
            return Start(Scenarios[_scenarioSelect.Selected]);
        }, requireFight: false));
        var real = Number(right, "真实生命", 34, 1, 9999);
        var max = Number(right, "最大生命", 68, 1, 9999);
        var mirror = Number(right, "镜像生命", 0, 0, 9999);
        var energy = Number(right, "能量", 3, 0, 9999);
        var block = Number(right, "格挡", 10, 0, 9999);
        Button(right, "应用角色数值", () => Action(async () =>
        {
            await CreatureCmd.SetMaxHp(Witch, (int)max.Value);
            await CreatureCmd.SetCurrentHp(Witch, Math.Clamp((int)real.Value, 1, Witch.MaxHp));
            State.MirrorHp = Math.Clamp((int)mirror.Value, 0, WitchRules.MirrorCapacity(Witch.CurrentHp, Witch.MaxHp));
            await PlayerCmd.SetEnergy((int)energy.Value, _player);
            await SetBlock((int)block.Value);
            await RefreshStatuses();
        }));
        var status = Menu(right, Statuses.Select(item => item.Name));
        var amount = Number(right, "层数 / 回合 / 数值（0 移除）", 1, 0, 9999);
        Button(right, "设置魔女状态", () => Action(async () =>
        {
            Statuses[status.Selected].Write(State, (int)amount.Value);
            await RefreshStatuses();
        }));
        var native = Menu(right, _nativePowers.Select(power => power.Id.Entry));
        Button(right, "设置原版状态", () => Action(async () =>
        {
            var model = _nativePowers[native.Selected];
            var existing = Witch.Powers.FirstOrDefault(power => power.Id == model.Id);
            if (existing != null) await PowerCmd.Remove(existing);
            if (amount.Value > 0) await PowerCmd.Apply(_choice, model.ToMutable(), Witch, (int)amount.Value, Witch, null);
        }));
        Button(right, "重置本回合计次", () => Action(async () =>
        {
            State.Plays = State.BlockGains = State.HighPrintedPlays = 0;
            State.TraitSwapUsed = State.BookmarkUsed = State.DistillerUsed = State.FreePlayTriggered = State.Reflected = false;
            await RefreshStatuses();
        }));
        var relic = Menu(right, _relics.Select(model => model.Id.Entry));
        Button(right, "添加 / 移除所选遗物", () => Action(async () =>
        {
            var model = _relics[relic.Selected];
            var owned = _player.Relics.FirstOrDefault(item => item.Id == model.Id);
            if (owned == null) await RelicCmd.Obtain(model.ToMutable(), _player);
            else await RelicCmd.Remove(owned);
        }));
        var potion = Menu(right, _potions.Select(model => model.Id.Entry));
        Button(right, "添加 / 移除所选药水", () => Action(async () =>
        {
            var model = _potions[potion.Selected];
            var owned = _player.Potions.FirstOrDefault(item => item.Id == model.Id);
            if (owned == null) await PotionCmd.TryToProcure(model.ToMutable(), _player);
            else await PotionCmd.Discard(owned);
        }));
        _enemySlot = Number(right, "存活敌人的顺序", 1, 1, 8);
        var enemyHp = Number(right, "目标生命", 100, 1, 9999);
        Button(right, "应用目标生命", () => Action(async () =>
        {
            var enemy = SelectedEnemy();
            await CreatureCmd.SetMaxHp(enemy, Math.Max(enemy.MaxHp, (int)enemyHp.Value));
            await CreatureCmd.SetCurrentHp(enemy, (int)enemyHp.Value);
        }));
        _hits = new LineEdit { Text = "4,4,4", PlaceholderText = "每段伤害，逗号分隔" };
        right.AddChild(_hits);
        _nonAttack = new CheckBox { Text = "非攻击伤害（绕过格挡）" };
        right.AddChild(_nonAttack);
        _dummyCount = Number(right, "木桩数量（重开时生效）", 1, 1, 3);
        Button(right, "由指定敌人执行伤害序列", () => Action(async () =>
        {
            var hits = ParseHits();
            var enemy = SelectedEnemy();
            foreach (var hit in hits)
            {
                if (!enemy.IsAlive || !Witch.IsAlive || !CombatManager.Instance.IsInProgress) break;
                await Hit(enemy, hit, _nonAttack.ButtonPressed);
            }
        }));
        Button(right, "结束回合", () => Action(() =>
        {
            PlayerCmd.EndTurn(_player, canBackOut: false);
            return Task.CompletedTask;
        }));
        Button(right, "保存场景配置", () => Action(Save));
        Button(right, "读取上次配置", () => Action(Load, requireFight: false));
        _log = new TextEdit { Editable = false, WrapMode = TextEdit.LineWrappingMode.Boundary, CustomMinimumSize = new Vector2(0, 170) };
        right.AddChild(_log);
        Button(right, "复制结算日志", () => { DisplayServer.ClipboardSet(string.Join('\n', _records)); return Task.CompletedTask; });
        Button(right, "导出结算日志", async () =>
        {
            var path = Path.Combine(_root, "combat-events.log");
            await File.WriteAllLinesAsync(path, _records);
            Report($"日志已导出 · {path}");
        });
    }

    private int[] ParseHits() => _hits.Text.Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(text => Math.Clamp(int.Parse(text), 0, 9999)).Take(30).ToArray();

    private Creature SelectedEnemy()
    {
        var enemies = Witch.CombatState!.HittableEnemies;
        var index = (int)_enemySlot.Value - 1;
        return index < enemies.Count ? enemies[index] : throw new InvalidOperationException("该顺位没有存活敌人。");
    }

    private void UpdatePiles()
    {
        if (_pileList == null || _player?.PlayerCombatState == null) return;
        _pileList.Clear();
        foreach (var card in Piles[_pileSelect.Selected].GetPile(_player).Cards)
            _pileList.AddItem($"{card.Title} {(card.IsUpgraded ? "+" : "")}");
    }

    private static string PrintedCost(WitchCard card, bool upgraded)
    {
        if (card.EnergyCost.CostsX) return "X";
        if (!upgraded || !card.IsUpgradable) return card.EnergyCost.Canonical.ToString();
        var preview = card.ToMutable();
        preview.UpgradeInternal();
        return preview.EnergyCost.GetWithModifiers(CostModifiers.None).ToString();
    }

    private static (PanelContainer Panel, VBoxContainer Controls) SidePanel(Control parent, bool right)
    {
        var panel = new PanelContainer();
        parent.AddChild(panel);
        panel.SetAnchorsAndOffsetsPreset(right ? Control.LayoutPreset.RightWide : Control.LayoutPreset.LeftWide);
        panel.OffsetLeft = right ? -324 : 10;
        panel.OffsetRight = right ? -10 : 324;
        panel.OffsetTop = 100;
        panel.OffsetBottom = -230;
        var scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        panel.AddChild(scroll);
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        return (panel, list);
    }

    private static void Label(Container parent, string text) => parent.AddChild(new Label { Text = text });

    private static OptionButton Menu(Container parent, IEnumerable<string> names)
    {
        var menu = new OptionButton { FitToLongestItem = false, ClipText = true };
        foreach (var name in names) menu.AddItem(name);
        parent.AddChild(menu);
        return menu;
    }

    private static SpinBox Number(Container parent, string title, int value, int min, int max)
    {
        var row = new HBoxContainer();
        parent.AddChild(row);
        Label(row, title);
        var number = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(number);
        return number;
    }

    private static void Button(Container parent, string label, Func<Task> action)
    {
        var button = new Button { Text = label };
        button.Pressed += () => _ = action();
        parent.AddChild(button);
    }
}
