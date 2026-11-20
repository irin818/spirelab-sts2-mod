using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class WitchLife
{
    private const string DisplayName = "SpireLabMirrorHp";

    public static async Task<bool> Pay(Player player, int cost)
    {
        var creature = player.Creature;
        if (!WitchRules.CanPayLife(creature.CurrentHp, cost))
        {
            return false;
        }

        var state = WitchCombatState.For(player);
        state.CardLifeLost += cost;
        await CreatureCmd.SetCurrentHp(creature, creature.CurrentHp - cost);
        return true;
    }

    public static async Task EnterMirror(Player player)
    {
        var state = WitchCombatState.For(player);
        if (state.MirrorForm)
        {
            return;
        }

        state.MirrorForm = true;
        var creature = player.Creature;
        var (real, mirror) = WitchRules.Invert(creature.CurrentHp, creature.MaxHp);
        state.MirrorHp = mirror;
        if (real != creature.CurrentHp)
        {
            await CreatureCmd.SetCurrentHp(creature, real);
        }

        RefreshDisplay(creature, state);
    }

    public static void RefreshDisplay(Creature creature, WitchCombatState state)
    {
        var health = creature.GetCreatureNode()?.GetNodeOrNull<Control>("%HealthBar");
        if (health == null || (!state.MirrorForm && state.MirrorHp == 0))
        {
            return;
        }

        var label = health.GetNodeOrNull<Label>(DisplayName);
        if (label == null)
        {
            label = new Label
            {
                Name = DisplayName,
                Position = new Vector2(0, 30),
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Modulate = new Color("b8c2ce")
            };
            label.AddThemeFontSizeOverride("font_size", 18);
            health.AddChild(label);
        }

        var text = new LocString("combat_messages", "SPIRELAB-WITCH_MIRROR_HP");
        text.Add("Real", creature.CurrentHp);
        text.Add("Mirror", state.MirrorHp);
        text.Add("Total", creature.CurrentHp + state.MirrorHp);
        label.Text = text.GetFormattedText();
    }
}
