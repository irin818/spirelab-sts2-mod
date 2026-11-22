using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Localization.DynamicVars;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

// R02
public sealed class ManaDeluge() : WitchCard("R02", 0, CardType.Attack, CardRarity.Rare, TargetType.RandomEnemy)
{
    protected override bool HasEnergyCostX => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(7)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var hits = ResolveEnergyXValue();
        if (hits > 0)
        {
            await Attack(choice, play, hits: hits);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(2);
}

// R03
public sealed class PureMana() : WitchCard("R03", 3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(1)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.StartingEnergy += DynamicVars.Energy.IntValue;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// R04
public sealed class RedCatalyst() : WitchCard("R04", 1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.RedCatalystDraw += DynamicVars.Cards.IntValue;
        State.RedCatalystStacks += 1;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// R05
public sealed class PotionTorrent() : WitchCard("R05", 2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2), new CardsVar(3)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await GainEnergy(choice);
        await Draw(choice);
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// R06
public sealed class RefinedAbyss() : WitchCard("R06", 1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Ward", 0)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.DepthsEnergy++;
        State.DepthsBlock += Number("Ward");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Ward"].UpgradeValueBy(3);
}

// R07
public sealed class GildedPact() : WitchCard("R07", 1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    public override int LifeCost => Number("Life");
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Life", 3), new CardsVar(2), new EnergyVar(2)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        if (await PayLife(choice))
        {
            await Draw(choice);
            await GainEnergy(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Life"].UpgradeValueBy(-1);
}

// R08
public sealed class FinalDistillation() : WitchCard("R08", 3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(28), new CardsVar(1), new EnergyVar(1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Attack(choice, play);
        await Draw(choice);
        if (play.Target?.IsDead == true)
        {
            await Draw(choice);
            await GainEnergy(choice);
        }
    }

    protected override void OnUpgrade() => DynamicVars.Damage.UpgradeValueBy(8);
}

// R09
public sealed class DuskRefinement() : WitchCard("R09", 1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new DynamicVar("Ward", 0)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.DuskDraw += DynamicVars.Cards.IntValue;
        State.DuskBlock += Number("Ward");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Ward"].UpgradeValueBy(2);
}

// R11
public sealed class HallOfAllMirrors() : WitchCard("R11", 3, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Percent", 50)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.FirstBlockPercent += Number("Percent");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Percent"].UpgradeValueBy(25);
}

// R12
public sealed class MoonInTheMirror() : WitchCard("R12", 2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Retaliate", 12)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.Immunity = Math.Max(State.Immunity ?? 0, Number("Retaliate"));
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Retaliate"].UpgradeValueBy(6);
}

// R13
public sealed class TenThousandShards() : WitchCard("R13", 2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(12), new DynamicVar("Bonus", 4)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play) =>
        Attack(choice, play, Math.Min(3, State.BlockGains) * Number("Bonus"));

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(4);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// R14
public sealed class ShadowOfTheReal() : WitchCard("R14", 1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Next", 1)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.ShadowDraw += Number("Next");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Next"].UpgradeValueBy(1);
}

// R15
public sealed class EmptyMirror() : WitchCard("R15", 1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1), new DynamicVar("Ward", 0)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.EmptyMirrorDraw += DynamicVars.Cards.IntValue;
        State.EmptyMirrorBlock += Number("Ward");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Ward"].UpgradeValueBy(3);
}

// R16
public sealed class MirrorCage() : WitchCard("R16", 2, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Floor", WitchRules.MirrorCageFloor)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var damage = WitchRules.MirrorCageDamage(Owner.Creature.Block, Number("Floor"));
        return Deal(choice, play, damage, hits: 2);
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// R17
public sealed class MirrorBurial() : WitchCard("R17", 2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override bool GainsBlock => true;
    protected override IEnumerable<DynamicVar> CanonicalVars => [Guard(18)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        await Block(play);
        State.NextReflectPercent += 100;
    }

    protected override void OnUpgrade() => DynamicVars.Block.UpgradeValueBy(6);
}

// R18
public sealed class FracturedCrown() : WitchCard("R18", 1, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Bonus", 3)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.CrownDamage += Number("Bonus");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Bonus"].UpgradeValueBy(2);
}

// R19
public sealed class ForbiddenFinalChapter() : WitchCard("R19", 3, CardType.Attack, CardRarity.Rare, TargetType.AnyEnemy)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [Damage(18), new DynamicVar("Bonus", 5)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var skills = Owner.PlayerCombatState!.Hand.Cards.Where(card => card != this && card.Type == CardType.Skill).ToArray();
        var exhausted = 0;
        foreach (var card in skills)
        {
            if (card.Pile?.Type == PileType.Hand && (await CardCmd.Exhaust(choice, card))?.success == true)
            {
                exhausted++;
            }
        }
        await Attack(choice, play, exhausted * Number("Bonus"));
    }

    protected override void OnUpgrade()
    {
        DynamicVars.Damage.UpgradeValueBy(6);
        DynamicVars["Bonus"].UpgradeValueBy(1);
    }
}

// R20
public sealed class TurnThePageOfFate() : WitchCard("R20", 2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "SPIRELAB-WITCH_FATE_DRAW"), 1);
        var selected = (await CardSelectCmd.FromCombatPile(choice, Owner.PlayerCombatState!.DrawPile, Owner, prefs)).SingleOrDefault();
        if (selected != null)
        {
            await CardPileCmd.Add(selected, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// R21
public sealed class BloodstainedBookmark() : WitchCard("R21", 2, CardType.Power, CardRarity.Rare, TargetType.Self)
{

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.BookmarkEnergy++;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => EnergyCost.UpgradeBy(-1);
}

// R22
public sealed class TemporalSeal() : WitchCard("R22", 2, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new EnergyVar(2), new DynamicVar("Next", 2)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.NextEnergy += DynamicVars.Energy.IntValue;
        State.NextDraw += Number("Next");
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars["Next"].UpgradeValueBy(1);
}

// R23
public sealed class ReverseRite() : WitchCard("R23", 2, CardType.Power, CardRarity.Rare, TargetType.Self)
{
    protected override IEnumerable<DynamicVar> CanonicalVars => [new CardsVar(1)];

    protected override Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        State.ReverseRiteDraw += DynamicVars.Cards.IntValue;
        return Task.CompletedTask;
    }

    protected override void OnUpgrade() => DynamicVars.Cards.UpgradeValueBy(1);
}

// R24
public sealed class PactEcho() : WitchCard("R24", 1, CardType.Skill, CardRarity.Rare, TargetType.Self)
{
    public override IEnumerable<CardKeyword> CanonicalKeywords => [CardKeyword.Exhaust];
    protected override IEnumerable<DynamicVar> CanonicalVars => [new DynamicVar("Surcharge", 1)];

    protected override async Task OnPlay(PlayerChoiceContext choice, CardPlay play)
    {
        var prefs = new CardSelectorPrefs(new LocString("card_selection", "SPIRELAB-WITCH_PACT_ECHO"), 1);
        var selected = (await CardSelectCmd.FromCombatPile(choice, Owner.PlayerCombatState!.DiscardPile, Owner, prefs,
            card => !card.Keywords.Contains(CardKeyword.Exhaust))).FirstOrDefault();
        if (selected != null)
        {
            selected.EnergyCost.AddThisTurn(Number("Surcharge"));
            await CardPileCmd.Add(selected, PileType.Hand);
        }
    }

    protected override void OnUpgrade() => DynamicVars["Surcharge"].UpgradeValueBy(-1);
}
