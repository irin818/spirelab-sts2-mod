using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using BaseLib.Utils;
using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

[Pool(typeof(WitchCardPool))]
public abstract class WitchCard(string specId, int cost, CardType type, CardRarity rarity, TargetType target)
    : CustomCardModel(cost, type, rarity, target)
{
    [CustomEnum]
    public static CardTag Desire;

    internal static readonly HashSet<string> RetiredSpecIds =
        ["C17", "U02", "U03", "U17", "U25", "U26", "U29", "R06", "R22"];

    public string SpecId { get; } = specId;
    public virtual bool IsDesire => false;
    public virtual int LifeCost => 0;
    public bool IsRetiredFromRewardPool => RetiredSpecIds.Contains(SpecId);
    public bool IsInRewardPool =>
        Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare && !IsRetiredFromRewardPool;
    public override string CustomPortraitPath => $"{TheWitch.Art}/cards/{SpecId.ToLowerInvariant()}.png";
    public override string PortraitPath => CustomPortraitPath;
    public override string BetaPortraitPath => CustomPortraitPath;
    public override Material? CreateCustomFrameMaterial => WitchCardAppearance.Frame(Rarity);

    private protected WitchCombatState State => WitchCombatState.For(Owner);
    protected int Number(string name) => DynamicVars[name].IntValue;

    protected Task<AttackCommand> Attack(PlayerChoiceContext choice, CardPlay play, int extra = 0, int hits = 1) =>
        Deal(choice, play, DynamicVars.Damage.BaseValue + extra, hits);

    protected Task<AttackCommand> Deal(PlayerChoiceContext choice, CardPlay play, decimal amount, int hits = 1)
    {
        var attack = DamageCmd.Attack(amount).FromCard(this, play).WithHitCount(hits)
            .WithHitFx("vfx/vfx_attack_slash");
        switch (TargetType)
        {
            case TargetType.AllEnemies:
                attack.TargetingAllOpponents(CombatState!);
                break;
            case TargetType.RandomEnemy:
                attack.TargetingRandomOpponents(CombatState!);
                break;
            default:
                attack.Targeting(play.Target!);
                break;
        }

        return attack.Execute(choice);
    }

    protected bool BelowHalf => WitchRules.IsBelowHalf(Owner.Creature.CurrentHp, Owner.Creature.MaxHp);
    protected int ExtraEnergy => State.ExtraEnergy;
    protected bool IsCounterpoint => State.CurrentPlayWasCounterpoint;

    protected Task Draw(PlayerChoiceContext choice, decimal count) => CardPileCmd.Draw(choice, count, Owner);
    protected Task Draw(PlayerChoiceContext choice) => CardPileCmd.Draw(choice, DynamicVars.Cards.BaseValue, Owner);
    protected Task GainEnergy(PlayerChoiceContext choice) => WitchEnergy.Gain(choice, Owner, DynamicVars.Energy.IntValue, fromCard: true);
    protected Task Weak(PlayerChoiceContext choice, CardPlay play) =>
        PowerCmd.Apply<MegaCrit.Sts2.Core.Models.Powers.WeakPower>(choice, play.Target!,
            Number("Weak"), Owner.Creature, this);

    protected async Task<bool> DiscardOne(PlayerChoiceContext choice, bool optional = false)
    {
        var prompt = CardSelectorPrefs.DiscardSelectionPrompt;
        var prefs = new CardSelectorPrefs(prompt, optional ? 0 : 1, 1) { Cancelable = optional };
        var selected = (await CardSelectCmd.FromHandForDiscard(choice, Owner, prefs, card => card != this, this)).FirstOrDefault();
        if (selected == null)
        {
            return false;
        }

        await CardCmd.Discard(choice, selected);
        return true;
    }

    protected async Task<bool> ExhaustOne(PlayerChoiceContext choice, string prompt)
    {
        var prefs = new CardSelectorPrefs(new LocString("card_selection", prompt), 1, 1)
        {
            Cancelable = false,
            RequireManualConfirmation = true,
            PretendCardsCanBePlayed = true
        };
        var selected = (await CardSelectCmd.FromHand(choice, Owner, prefs, card => card != this, this)).FirstOrDefault();
        if (selected == null || selected == this)
        {
            return false;
        }

        var result = await CardCmd.Exhaust(choice, selected);
        return result?.success == true;
    }

    protected Task<bool> PayLife(PlayerChoiceContext choice) => WitchLife.Pay(choice, Owner, LifeCost, fromCard: true);

    protected async Task<CardModel?> ChooseOtherHandCard(PlayerChoiceContext choice, string prompt)
    {
        var prefs = new CardSelectorPrefs(new LocString("card_selection", prompt), 0, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true,
            PretendCardsCanBePlayed = true
        };
        return (await CardSelectCmd.FromHand(choice, Owner, prefs, card => card != this, this)).SingleOrDefault();
    }

    protected async Task<bool> ConfirmOptionalEffect(PlayerChoiceContext choice, string prompt)
    {
        var prefs = new CardSelectorPrefs(new LocString("card_selection", prompt), 0, 1)
        {
            Cancelable = true,
            RequireManualConfirmation = true
        };
        return (await CardSelectCmd.FromSimpleGrid(choice, [this], Owner, prefs)).Any();
    }

    protected Task<decimal> Block(CardPlay play, int extra = 0) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue + extra, ValueProp.Move, play);

    protected static DamageVar Damage(int value) => new(value, ValueProp.Move);
    protected static BlockVar Guard(int value) => new(value, ValueProp.Move);
}

public abstract class DesireCard(string specId, int cost, CardType type, CardRarity rarity)
    : WitchCard(specId, cost, type, rarity, TargetType.Self)
{
    public sealed override bool IsDesire => true;
    protected sealed override HashSet<CardTag> CanonicalTags => [Desire];
}
