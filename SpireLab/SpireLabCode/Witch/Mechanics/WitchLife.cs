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
    private const float MeterWidth = 154;

    public static async Task<bool> Pay(PlayerChoiceContext choice, Player player, int cost, bool fromCard, bool refundOnVictory = false)
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
            state.LifePaidByCardThisTurn = true;
        }

        if (refundOnVictory)
        {
            state.GamblePaidThisTurn = (int)Math.Min(int.MaxValue, (long)state.GamblePaidThisTurn + cost);
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
            state.LifePaidByCardThisTurn = true;
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
            // The native health bar is centered at the origin, with its powers below it.
            meter = new Control
            {
                Name = DisplayName,
                Position = new Vector2(-MeterWidth / 2, -38),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            meter.AddChild(Segment("Shadow", "17131b"));
            meter.AddChild(Segment("Real", "8c2645"));
            meter.AddChild(Segment("Mirror", "d7dbe2"));
            var label = new Label { Name = "Caption", MouseFilter = Control.MouseFilterEnum.Ignore };
            label.AddThemeFontSizeOverride("font_size", 15);
            meter.AddChild(label);
            health.AddChild(meter);
        }

        var realWidth = MeterWidth * creature.CurrentHp / creature.MaxHp;
        var mirrorWidth = MeterWidth * state.MirrorHp / creature.MaxHp;
        meter.GetNode<ColorRect>("Shadow").Size = new Vector2(MeterWidth, 4);
        meter.GetNode<ColorRect>("Real").Size = new Vector2(realWidth, 4);
        var mirrorSegment = meter.GetNode<ColorRect>("Mirror");
        mirrorSegment.Position = new Vector2(realWidth, 0);
        mirrorSegment.Size = new Vector2(mirrorWidth, 4);

        var text = new LocString("combat_messages", "SPIRELAB-WITCH_MIRROR_HP");
        text.Add("Real", creature.CurrentHp);
        text.Add("Mirror", state.MirrorHp);
        text.Add("Total", creature.CurrentHp + state.MirrorHp);
        var caption = meter.GetNode<Label>("Caption");
        caption.Text = text.GetFormattedText();
        caption.ResetSize();
        caption.Position = new Vector2((MeterWidth - caption.GetMinimumSize().X) / 2, 4);
    }

    private static ColorRect Segment(string name, string color) =>
        new() { Name = name, Color = new Color(color), MouseFilter = Control.MouseFilterEnum.Ignore };
}
