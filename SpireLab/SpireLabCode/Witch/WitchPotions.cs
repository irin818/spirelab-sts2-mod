using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Potions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch;

[Pool(typeof(WitchPotionPool))]
public abstract class WitchPotion(string art) : CustomPotionModel
{
    public override PotionUsage Usage => PotionUsage.CombatOnly;
    public override TargetType TargetType => TargetType.Self;
    public override string? CustomPackedImagePath => $"{TheWitch.Art}/potions/{art}.png";
    public override string? CustomPackedOutlinePath => $"{TheWitch.Art}/potions/{art}_outline.png";
}

public sealed class MistDraught() : WitchPotion("mist_draught")
{
    public override PotionRarity Rarity => PotionRarity.Common;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(11, ValueProp.Unpowered), new DynamicVar("Reflection", 4)];

    protected override async Task OnUse(PlayerChoiceContext choice, Creature? target)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
        WitchCombatState.For(Owner).NextReflections.Add(DynamicVars["Reflection"].IntValue);
    }
}

public sealed class TwinDraught() : WitchPotion("twin_draught")
{
    public override PotionRarity Rarity => PotionRarity.Uncommon;

    protected override async Task OnUse(PlayerChoiceContext choice, Creature? target)
    {
        await Enlightenment.Choose(choice, Owner, this);
        await CardPileCmd.Draw(choice, Owner);
    }
}

public sealed class AbyssalDistillate() : WitchPotion("abyssal_distillate")
{
    public override PotionRarity Rarity => PotionRarity.Rare;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Life", 5), new EnergyVar(3)];
    public override bool PassesCustomUsabilityCheck => WitchRules.CanPayLife(Owner.Creature.CurrentHp, DynamicVars["Life"].IntValue);

    protected override async Task OnUse(PlayerChoiceContext choice, Creature? target)
    {
        if (await WitchLife.Pay(choice, Owner, DynamicVars["Life"].IntValue, fromCard: false))
        {
            await WitchEnergy.Gain(choice, Owner, DynamicVars.Energy.IntValue);
        }
    }
}
