using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Hooks;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using MegaCrit.Sts2.Core.Models;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

// U37
public sealed class PrimalGreed() : DesireCard("U37", 2, CardType.Skill, CardRarity.Uncommon)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(5)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var kept = BelowHalf ? await ChooseOtherHandCard(choice, "SPIRELAB-WITCH_GREED_KEEP") : null;
        var hand = Owner.PlayerCombatState!.Hand.Cards.Where(card => card != this && card != kept).ToArray();
        var drawPile = Owner.PlayerCombatState.DrawPile;
        var combined = drawPile.Cards.Concat(hand).ToList();
        Owner.RunState.Rng.Shuffle.Shuffle(combined);
        if (combined.Count > 0)
        {
            Hook.ModifyShuffleOrder(CombatState!, Owner, combined, isInitialShuffle: false);
            await CardPileCmd.Add(combined, drawPile);
            await Hook.AfterShuffle(CombatState!, choice, Owner);
        }

        await CardPileCmd.Draw(choice, DynamicVars.Cards.BaseValue, Owner);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// U38
public sealed class Sloth() : DesireCard("U38", 2, CardType.Skill, CardRarity.Uncommon)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(10)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (State.Plays == 1)
        {
            State.Immunity ??= 0;
        }
        else
        {
            await Block(play);
        }

        if (BelowHalf && await ChooseOtherHandCard(choice, "SPIRELAB-WITCH_SLOTH_RETAIN") is { } retained)
        {
            CardCmd.ApplySingleTurnRetain(retained);
        }

        PlayerCmd.EndTurn(Owner, canBackOut: false);
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(2);
}

// R25
public sealed class FallenConfidence() : DesireCard("R25", 3, CardType.Power, CardRarity.Rare)
{
    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (!State.DesireDiscount)
        {
            State.DesireDiscount = true;
            foreach (var card in Owner.PlayerCombatState!.Hand.Cards.OfType<DesireCard>())
            {
                card.InvokeEnergyCostChanged();
            }
        }

        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// R26
public sealed class SourceOfCalamity() : DesireCard("R26", 2, CardType.Skill, CardRarity.Rare)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Options", 2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (CombatState is not CombatState combat || CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var options = new List<CardModel>
        {
            ModelDb.Card<PrimalGreed>(), ModelDb.Card<Sloth>(), ModelDb.Card<FallenConfidence>(), ModelDb.Card<AbyssOfGambling>()
        };
        Owner.RunState.Rng.CombatCardSelection.Shuffle(options);
        var candidates = options.Take(Number("Options")).Select(card => combat.CreateCard(card, Owner)).ToArray();
        var selected = await CardSelectCmd.FromChooseACardScreen(choice, candidates, Owner);
        foreach (var candidate in candidates)
        {
            if (candidate != selected)
            {
                candidate.RemoveFromState();
            }
        }

        if (selected == null)
        {
            MainFile.Logger.Info("Source of Calamity resolved without a card choice.");
            return;
        }

        selected.EnergyCost.SetThisTurn(0);
        var added = await CardPileCmd.AddGeneratedCardToCombat(selected, PileType.Hand, Owner);
        if (!added.success)
        {
            selected.RemoveFromState();
        }
    }

    protected override void OnUpgrade() => DynamicVars["Options"].UpgradeValueBy(1);
}

// R27
public sealed class AbyssOfGambling() : DesireCard("R27", 1, CardType.Skill, CardRarity.Rare)
{
    public override int LifeCost => WitchRules.GambleCost(Owner.Creature.CurrentHp, Number("Percent"));
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Percent", 50), new CardsVar(1), new EnergyVar(2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (await PayLife(choice))
        {
            // At hand capacity, reserve the return slot before asking the engine to draw.
            if (Owner.PlayerCombatState!.Hand.Cards.Count >= CardPile.MaxCardsInHand - 1)
            {
                await CardPileCmd.Add(this, PileType.Hand);
            }

            await Draw(choice);
            await GainEnergy(choice);
        }
    }

    protected override CardLocation GetResultLocationForCardPlay() =>
        new(Owner, PileType.Hand, CardPilePosition.Bottom);

    protected override void OnUpgrade() => DynamicVars["Percent"].UpgradeValueBy(-10);
}
