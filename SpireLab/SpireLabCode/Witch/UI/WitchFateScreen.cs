using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardLibrary;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.addons.mega_text;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.UI;

internal sealed partial class WitchFateScreen : NCardGridSelectionScreen
{
    private static readonly string?[] Suits = [null, "potion", "mirror", "grimoire", "desire"];
    private static readonly CardType[] Types = [CardType.Attack, CardType.Skill, CardType.Power];

    private IReadOnlyList<CardModel> _rewards = [];
    private Player _player = null!;
    private HBoxContainer _filters = null!;
    private Control _bottom = null!;
    private MegaRichTextLabel _prompt = null!;
    private NConfirmButton _confirm = null!;
    private OptionButton _cost = null!;
    private OptionButton _suit = null!;
    private OptionButton _type = null!;
    private CardModel? _selected;
    private int _filterVersion;

    protected override IEnumerable<Control> PeekButtonTargets => [_filters, _bottom];

    internal static WitchFateScreen Create(IReadOnlyList<CardModel> rewards, Player player)
    {
        var picker = new WitchFateScreen { Name = "WitchFateScreen", _rewards = rewards, _cards = rewards, _player = player };
        picker.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var template = PreloadManager.Cache.GetScene(NSimpleCardSelectScreen.AssetPaths.Single()).Instantiate<Control>();

        // Keep the game's real grid, top-bar inset, combat peek, prompt and confirm controls.
        // These unique nodes are owned by the original scene root and must follow the controls.
        foreach (var child in template.GetChildren())
        {
            var owned = child.FindChildren("*", owned: false).Prepend(child)
                .Where(node => node.Owner == template).ToArray();
            template.RemoveChild(child);
            picker.AddChild(child);
            foreach (var node in owned)
            {
                node.Owner = picker;
            }
        }
        template.Free();
        return picker;
    }

    public override void _Ready()
    {
        _bottom = GetNode<Control>("%BottomText");
        _prompt = _bottom.GetNode<MegaRichTextLabel>("%BottomLabel");
        _confirm = GetNode<NConfirmButton>("%Confirm");
        _confirm.Disable();
        _confirm.Connect(NClickableControl.SignalName.Released, Callable.From((NButton _) => Confirm()));

        _filters = new HBoxContainer { Name = "FateFilters", Alignment = BoxContainer.AlignmentMode.Center };
        _filters.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        _filters.OffsetTop = 90;
        _filters.OffsetBottom = 146;
        _filters.AddThemeConstantOverride("separation", 14);
        AddChild(_filters);
        _cost = AddFilter("CostFilter", ["ALL_COSTS", "0", "1", "2", "3", "4+", "X"]);
        _suit = AddFilter("SuitFilter", ["ALL_SUITS", "POTION", "MIRROR", "GRIMOIRE", "DESIRE"]);
        _type = AddFilter("TypeFilter", ["ALL_TYPES", "ATTACK", "SKILL", "POWER"]);
        var reset = new Button { Name = "ResetFilters", Text = Text("RESET_FILTERS"), CustomMinimumSize = new Vector2(120, 48) };
        reset.AddThemeFontSizeOverride("font_size", 22);
        reset.Pressed += () =>
        {
            _cost.Select(0);
            _suit.Select(0);
            _type.Select(0);
            ScheduleFilter();
        };
        _filters.AddChild(reset);
        UpdatePrompt();

        GetNode<MegaCrit.Sts2.Core.Nodes.Cards.NCardGrid>("%CardGrid").YOffset = 56;
        ConnectSignalsAndInitGrid();
        var piles = GetNode<NCombatPilesContainer>("%CombatPiles");
        piles.Initialize(_player);
        piles.Disable();
        piles.Hide();
        _peekButton.Connect(NPeekButton.SignalName.Toggled, Callable.From((NPeekButton _) =>
        {
            if (_peekButton.IsPeeking) piles.Enable();
            else piles.Disable();
            piles.Visible = _peekButton.IsPeeking;
        }));
    }

    private OptionButton AddFilter(string name, string[] options)
    {
        var button = new OptionButton { Name = name, CustomMinimumSize = new Vector2(172, 48) };
        button.AddThemeFontSizeOverride("font_size", 22);
        foreach (var option in options)
        {
            button.AddItem(option is "0" or "1" or "2" or "3" or "4+" or "X" ? option : Text(option));
        }
        button.ItemSelected += _ => ScheduleFilter();
        _filters.AddChild(button);
        return button;
    }

    private void ScheduleFilter()
    {
        var version = ++_filterVersion;
        Callable.From(() =>
        {
            if (!IsInsideTree() || version != _filterVersion) return;
            _cards = _rewards.Where(card => MatchesCost(card, _cost.Selected)
                && (_suit.Selected == 0 || WitchCardAppearance.Suit((WitchCard)card) == Suits[_suit.Selected])
                && (_type.Selected == 0 || card.Type == Types[_type.Selected - 1])).ToArray();
            _grid.SetCards(_cards, PileType.None, [SortingOrders.Ascending]);
            if (_selected != null) _grid.HighlightCard(_selected);
            UpdatePrompt();
        }).CallDeferred();
    }

    internal static bool MatchesCost(CardModel card, int filter)
    {
        if (filter == 0) return true;
        if (filter == 6 || card.EnergyCost.CostsX) return filter == 6 && card.EnergyCost.CostsX;
        return filter == 5 ? card.EnergyCost.Canonical >= 4 : card.EnergyCost.Canonical == filter - 1;
    }

    protected override void OnCardClicked(CardModel card)
    {
        if (!_cards.Contains(card)) return;
        if (_selected != null) _grid.UnhighlightCard(_selected);
        _selected = card;
        _grid.HighlightCard(card);
        _confirm.Enable();
        UpdatePrompt();
    }

    private void UpdatePrompt()
    {
        var prompt = new LocString("card_selection", _selected != null ? "SPIRELAB-WITCH_FATE_SELECTED"
            : _cards.Count == 0 ? "SPIRELAB-WITCH_FATE_EMPTY" : "SPIRELAB-WITCH_OPENING_CHOICE");
        if (_selected != null) prompt.Add("Card", _selected.Title);
        _prompt.Text = prompt.GetFormattedText();
    }

    private void Confirm()
    {
        if (_selected != null && _completionSource.TrySetResult([_selected]))
        {
            NOverlayStack.Instance?.Remove(this);
        }
    }

    private static string Text(string key) => new LocString("card_selection", $"SPIRELAB-WITCH_FATE_{key}").GetFormattedText();
}
