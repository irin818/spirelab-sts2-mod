using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using SpireLab.SpireLabCode.Character;

namespace SpireLab.SpireLabCode.Cards;

[Pool(typeof(SpireLabCardPool))]
public abstract class SpireLabCard(int cost, CardType type, CardRarity rarity, TargetType target, string art)
    : CustomCardModel(cost, type, rarity, target)
{
    public override string CustomPortraitPath => $"{MainFile.ResPath}/images/surveyor/cards/{art}.png";
    public override string PortraitPath => CustomPortraitPath;
    public override string BetaPortraitPath => CustomPortraitPath;

    protected async Task Attack(PlayerChoiceContext choice, CardPlay play, decimal? damage = null, int hits = 1)
    {
        var attack = DamageCmd.Attack(damage ?? DynamicVars.Damage.BaseValue)
            .FromCard(this, play)
            .WithHitCount(hits)
            .WithHitFx("vfx/vfx_attack_blunt");

        if (TargetType == TargetType.AllEnemies)
        {
            ArgumentNullException.ThrowIfNull(CombatState);
            attack.TargetingAllOpponents(CombatState);
        }
        else
        {
            ArgumentNullException.ThrowIfNull(play.Target);
            attack.Targeting(play.Target);
        }

        await attack.Execute(choice);
    }

    protected async Task Generate<T>(PileType pile) where T : CardModel
    {
        if (CombatState is { } combat && !CombatManager.Instance.IsOverOrEnding)
        {
            await CardPileCmd.AddGeneratedCardToCombat(combat.CreateCard<T>(Owner), pile, Owner);
        }
    }
}
