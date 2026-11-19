using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Powers;

namespace SpireLab.SpireLabCode.Cards;

public sealed class PlatePolish() : SpireLabCard(1, CardType.Skill, CardRarity.Common, TargetType.Self, "guard")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new BlockVar(7, ValueProp.Move), new DynamicVar("Lumen", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await LumenPower.Gain(choice, Owner.Creature, DynamicVars["Lumen"].IntValue, this);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public sealed class BoreLine() : SpireLabCard(1, CardType.Attack, CardRarity.Common, TargetType.AllEnemies, "caliper")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Attack(choice, play);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public sealed class ChartRoute() : SpireLabCard(1, CardType.Skill, CardRarity.Common, TargetType.Self, "map")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        await CardPileCmd.Draw(choice, DynamicVars.Cards.BaseValue, Owner);

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}
