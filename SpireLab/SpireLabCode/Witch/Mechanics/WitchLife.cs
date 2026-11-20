using Godot;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class WitchLife
{
    private const string DisplayName = "SpireLabMirrorHp";

    public static async Task<bool> Pay(PlayerChoiceContext choice, Player player, int cost, bool fromCard)
    {
        var creature = player.Creature;
        if (!WitchRules.CanPayLife(creature.CurrentHp, cost))
        {
            return false;
        }

        var state = WitchCombatState.For(player);
        if (fromCard)
        {
            state.RecordCardLifeLoss(cost);
        }

        await CreatureCmd.SetCurrentHp(creature, creature.CurrentHp - cost);
        await AfterPayment(choice, player, state);
        return true;
    }

    public static async Task AfterPayment(PlayerChoiceContext choice, Player player, WitchCombatState state)
    {
        if (!state.LifePaymentTriggered && player.Relics.OfType<BloodstainedRing>().FirstOrDefault() is { } ring)
        {
            state.LifePaymentTriggered = true;
            ring.Flash();
            await CardPileCmd.Draw(choice, 2, player);
        }
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
            state.RecordCardLifeLoss(creature.CurrentHp - real);
            await CreatureCmd.SetCurrentHp(creature, real);
        }

        RefreshDisplay(creature, state);
        WitchFeedback.Invert(creature);
    }

    public static void RefreshDisplay(Creature creature, WitchCombatState state)
    {
        var health = creature.GetCreatureNode()?.GetNodeOrNull<Control>("%HealthBar");
        if (health == null || (!state.MirrorForm && state.MirrorHp == 0))
        {
            return;
        }

        var meter = health.GetNodeOrNull<Control>(DisplayName);
        if (meter == null)
        {
            meter = new Control { Name = DisplayName, Position = new Vector2(0, 26), MouseFilter = Control.MouseFilterEnum.Ignore };
            meter.AddChild(Segment("Shadow", "17131b"));
            meter.AddChild(Segment("Real", "7f182b"));
            meter.AddChild(Segment("Mirror", "b8c2ce"));
            var caption = new Label { Name = "Caption", Position = new Vector2(0, 4), MouseFilter = Control.MouseFilterEnum.Ignore };
            caption.AddThemeFontSizeOverride("font_size", 15);
            meter.AddChild(caption);
            health.AddChild(meter);
        }

        const float width = 154;
        var realWidth = width * creature.CurrentHp / creature.MaxHp;
        var mirrorWidth = width * state.MirrorHp / creature.MaxHp;
        meter.GetNode<ColorRect>("Shadow").Size = new Vector2(width, 4);
        meter.GetNode<ColorRect>("Real").Size = new Vector2(realWidth, 4);
        var mirrorSegment = meter.GetNode<ColorRect>("Mirror");
        mirrorSegment.Position = new Vector2(realWidth, 0);
        mirrorSegment.Size = new Vector2(mirrorWidth, 4);

        var text = new LocString("combat_messages", "SPIRELAB-WITCH_MIRROR_HP");
        text.Add("Real", creature.CurrentHp);
        text.Add("Mirror", state.MirrorHp);
        text.Add("Total", creature.CurrentHp + state.MirrorHp);
        meter.GetNode<Label>("Caption").Text = text.GetFormattedText();
    }

    private static ColorRect Segment(string name, string color) =>
        new() { Name = name, Color = new Color(color), MouseFilter = Control.MouseFilterEnum.Ignore };
}
