using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;

namespace SpireLab.SpireLabCode.Cards;

public sealed class SurveyShard() : SpireLabCard(0, CardType.Attack, CardRarity.Token, TargetType.AnyEnemy, "shard")
{
    public override bool CanBeGeneratedInCombat => false;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(5, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Attack(choice, play);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}

public sealed class FallingGrit() : SpireLabCard(-1, CardType.Status, CardRarity.Status, TargetType.None, "echo")
{
    public override bool CanBeGeneratedInCombat => false;
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Ethereal, CardKeyword.Unplayable];
}

public sealed class FaultDebt() : SpireLabCard(-1, CardType.Curse, CardRarity.Curse, TargetType.None, "map")
{
    public override bool CanBeGeneratedInCombat => false;
    public override int MaxUpgradeLevel => 0;
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Unplayable];
}
