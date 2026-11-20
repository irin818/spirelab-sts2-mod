using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Commands.Builders;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

[Pool(typeof(WitchCardPool))]
public abstract class WitchCard(int cost, CardType type, CardRarity rarity, TargetType target, string theme)
    : CustomCardModel(cost, type, rarity, target)
{
    [CustomEnum]
    public static CardTag Desire;

    public virtual bool IsDesire => false;
    public override string CustomPortraitPath => $"{TheWitch.Art}/cards/{theme}.png";
    public override string PortraitPath => CustomPortraitPath;
    public override string BetaPortraitPath => CustomPortraitPath;

    private protected WitchCombatState State => WitchCombatState.For(Owner);
    protected int Number(string name) => DynamicVars[name].IntValue;

    protected Task<AttackCommand> Attack(PlayerChoiceContext choice, CardPlay play, int extra = 0) =>
        DamageCmd.Attack(DynamicVars.Damage.BaseValue + extra).FromCard(this, play).Targeting(play.Target!)
            .WithHitFx("vfx/vfx_attack_slash").Execute(choice);

    protected Task<decimal> Block(CardPlay play, int extra = 0) =>
        CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block.BaseValue + extra, ValueProp.Move, play);

    protected static DamageVar Damage(int value) => new(value, ValueProp.Move);
    protected static BlockVar Guard(int value) => new(value, ValueProp.Move);
}

public abstract class DesireCard(int cost, CardType type, CardRarity rarity)
    : WitchCard(cost, type, rarity, TargetType.Self, "desire")
{
    public sealed override bool IsDesire => true;
    protected sealed override HashSet<CardTag> CanonicalTags => [Desire];
}
