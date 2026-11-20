using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.ValueProps;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class WitchEnergy
{
    public const int EngineLimit = 999999999;

    public static async Task Gain(PlayerChoiceContext choice, Player player, int amount, bool fromCard = false)
    {
        var state = WitchCombatState.For(player);
        state.CardEnergySource |= fromCard;
        try
        {
            await PlayerCmd.GainEnergy(amount, player);
        }
        finally
        {
            state.CardEnergySource = false;
        }

        await Resolve(choice, player, state);
    }

    public static async Task Resolve(PlayerChoiceContext choice, Player player, WitchCombatState state)
    {
        if (state.ResolvingEnergy)
        {
            return;
        }

        state.ResolvingEnergy = true;
        try
        {
            while (state.EnergyEvents.TryDequeue(out var fromCard))
            {
                var first = !state.EnergyTriggered;
                state.EnergyTriggered = true;
                if (first)
                {
                    WitchFeedback.Energy(player.Creature);
                }
                if (first && state.BlueFlameDamage > 0)
                {
                    var enemy = player.RunState.Rng.CombatTargets.NextItem(player.Creature.CombatState!.HittableEnemies);
                    if (enemy != null)
                    {
                        await CreatureCmd.Damage(choice, enemy, state.BlueFlameDamage, ValueProp.Unpowered, player.Creature);
                    }
                }
                if (first && state.SevenfoldDraw)
                {
                    await CardPileCmd.Draw(choice, 1, player);
                }

                if (fromCard)
                {
                    if (!state.CrackedPhialUsed && player.Relics.OfType<CrackedPhial>().FirstOrDefault() is { } phial)
                    {
                        state.CrackedPhialUsed = true;
                        await phial.FlashAndBlock(3);
                    }
                    if (!state.DistillerUsed && player.Relics.OfType<ObsidianDistiller>().FirstOrDefault() is { } distiller)
                    {
                        state.DistillerUsed = true;
                        distiller.Flash();
                        state.SuppressCardEnergySource = true;
                        try
                        {
                            await PlayerCmd.GainEnergy(1, player);
                        }
                        finally
                        {
                            state.SuppressCardEnergySource = false;
                        }
                    }
                }
            }
        }
        finally
        {
            state.ResolvingEnergy = false;
        }
    }
}
