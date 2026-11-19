using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Powers;

namespace SpireLab.SpireLabCode.Cards;

public sealed class EchoLantern() : SpireLabCard(1, CardType.Power, CardRarity.Uncommon, TargetType.Self, "lantern")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Lumen", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        await PowerCmd.Apply<EchoLanternPower>(choice, Owner.Creature, DynamicVars["Lumen"].BaseValue, Owner.Creature, this);

    protected override void OnUpgrade() => DynamicVars["Lumen"].UpgradeValueBy(1);
}

public sealed class PocketShard() : SpireLabCard(1, CardType.Skill, CardRarity.Uncommon, TargetType.Self, "shard")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(6, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);
        await Generate<SurveyShard>(PileType.Hand);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(4);
}

public sealed class FaultTouch() : SpireLabCard(1, CardType.Attack, CardRarity.Uncommon, TargetType.AnyEnemy, "echo")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(9, ValueProp.Move), new DynamicVar("Vulnerable", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        ArgumentNullException.ThrowIfNull(play.Target);
        await Attack(choice, play);
        if (play.Target.IsAlive)
        {
            await PowerCmd.Apply<VulnerablePower>(choice, play.Target, DynamicVars["Vulnerable"].BaseValue, Owner.Creature, this);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Vulnerable"].UpgradeValueBy(1);
}
