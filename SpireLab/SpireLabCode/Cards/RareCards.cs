using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Powers;

namespace SpireLab.SpireLabCode.Cards;

public sealed class DeepSurvey() : SpireLabCard(1, CardType.Power, CardRarity.Rare, TargetType.Self, "surveyor")
{
    protected override IEnumerable<DynamicVar> CanonicalVars =>
        [new DynamicVar("Lumen", 3), new DynamicVar("PerTurn", 2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await LumenPower.Gain(choice, Owner.Creature, DynamicVars["Lumen"].IntValue, this);
        await PowerCmd.Apply<EchoLanternPower>(choice, Owner.Creature, DynamicVars["PerTurn"].BaseValue, Owner.Creature, this);
        await Generate<FaultDebt>(PileType.Discard);
    }

    protected override void OnUpgrade() => DynamicVars["PerTurn"].UpgradeValueBy(1);
}

public sealed class AtlasSurge() : SpireLabCard(0, CardType.Skill, CardRarity.Rare, TargetType.Self, "compass")
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2), new CardsVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await PlayerCmd.GainEnergy(DynamicVars.Energy.BaseValue, Owner);
        await CardPileCmd.Draw(choice, DynamicVars.Cards.BaseValue, Owner);
        await Generate<FallingGrit>(PileType.Discard);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

public sealed class BreakRidge() : SpireLabCard(2, CardType.Attack, CardRarity.Rare, TargetType.AllEnemies, "shard")
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DamageVar(8, ValueProp.Move)];
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) => Attack(choice, play, hits: 2);
    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(3);
}
