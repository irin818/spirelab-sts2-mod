using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Powers;

namespace SpireLab.SpireLabCode.Relics;

public sealed class SurveyLantern : SpireLabRelic
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Lumen", 2)];

    public override async Task BeforeCombatStart()
    {
        Flash();
        await LumenPower.Gain(new ThrowingPlayerChoiceContext(), Owner.Creature, DynamicVars["Lumen"].IntValue);
    }
}

public sealed class BrassMeridian : SpireLabRelic
{
    public override RelicRarity Rarity => RelicRarity.Common;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Unpowered)];

    public override async Task BeforeCombatStart()
    {
        Flash();
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, null);
    }
}
