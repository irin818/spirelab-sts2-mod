using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;

namespace SpireLab.SpireLabCode.Powers;

public sealed class LumenPower : SpireLabPower
{
    public const int Capacity = 6;
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public static async Task Gain(PlayerChoiceContext choice, Creature target, int requested, CardModel? source = null)
    {
        if (CombatManager.Instance.IsOverOrEnding)
        {
            return;
        }

        var current = target.GetPower<LumenPower>()?.Amount ?? 0;
        var added = Math.Clamp(requested, 0, Math.Max(0, Capacity - current));
        if (added > 0)
        {
            await PowerCmd.Apply<LumenPower>(choice, target, added, target, source);
        }
    }

    public static async Task<int> Spend(PlayerChoiceContext choice, Creature target, int limit, CardModel source)
    {
        var power = target.GetPower<LumenPower>();
        if (power is null || limit <= 0)
        {
            return 0;
        }

        var before = power.Amount;
        var spend = Math.Min(before, limit);
        var remaining = await PowerCmd.ModifyAmount(choice, power, -spend, target, source);
        return Math.Clamp(before - remaining, 0, spend);
    }
}

public sealed class EchoLanternPower : SpireLabPower
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => PowerStackType.Counter;

    public override Task AfterPlayerTurnStart(PlayerChoiceContext choiceContext, Player player) =>
        player.Creature == Owner ? LumenPower.Gain(choiceContext, Owner, Amount) : Task.CompletedTask;
}
