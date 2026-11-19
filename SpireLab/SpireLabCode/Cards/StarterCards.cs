using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Powers;

namespace SpireLab.SpireLabCode.Cards;

public sealed class CaliperStrike() : SpireLabCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, "caliper")
{
    protected override HashSet<CardTag> CanonicalTags => [CardTag.Strike];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(6, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Attack(choice, play);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public sealed class CeramicGuard() : SpireLabCard(1, CardType.Skill, CardRarity.Basic, TargetType.Self, "guard")
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [new BlockVar(5, ValueProp.Move)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        await CreatureCmd.GainBlock(Owner.Creature, DynamicVars.Block, play);

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(3);
}

public sealed class LightProbe() : SpireLabCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, "lantern")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Innate];
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(5, ValueProp.Move), new DynamicVar("Lumen", 2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        await LumenPower.Gain(choice, Owner.Creature, DynamicVars["Lumen"].IntValue, this);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public sealed class ResonantRelease() : SpireLabCard(1, CardType.Attack, CardRarity.Basic, TargetType.AnyEnemy, "echo")
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DamageVar(6, ValueProp.Move), new DynamicVar("Spend", 3), new DynamicVar("Bonus", 3)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var spent = await LumenPower.Spend(choice, Owner.Creature, DynamicVars["Spend"].IntValue, this);
        await Attack(choice, play, DynamicVars.Damage.BaseValue + spent * DynamicVars["Bonus"].BaseValue);
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(4);
}
