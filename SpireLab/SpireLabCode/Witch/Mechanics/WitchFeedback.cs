using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class WitchFeedback
{
    public static void Enlighten(Creature creature)
    {
        WitchLiveRig.PulseGesture(creature, "mirror", isAttack: false);
        WitchCombatVfx.SpawnCoreCue(creature, "enlighten");
    }

    public static void Reflect(Creature dealer, Creature? source = null)
    {
        if (source != null)
        {
            WitchLiveRig.PulseReflect(source);
        }
        WitchCombatVfx.SpawnCoreCue(dealer, "reflect", source);
    }

    public static void Desire(Creature creature)
    {
        WitchLiveRig.PulseDesire(creature);
        WitchCombatVfx.SpawnCoreCue(creature, "desire");
    }

    public static void Energy(Creature creature)
    {
        WitchLiveRig.PulseCast(creature, "potion");
        WitchCombatVfx.SpawnCoreCue(creature, "energy");
    }

    public static void ReserveEnergy(Creature creature)
    {
        WitchLiveRig.PulseCast(creature, "potion");
        WitchCombatVfx.SpawnCoreCue(creature, "reserve");
    }

    public static void Block(Creature creature)
    {
        WitchLiveRig.PulseBlock(creature);
        WitchCombatVfx.SpawnCoreCue(creature, null);
    }

    public static void BlockGained(Creature creature, decimal amount)
    {
        if (amount > 0)
        {
            WitchCombatVfx.SpawnSmallBlockGlint(creature);
        }
    }

    public static void Hit(Creature creature)
    {
        WitchLiveRig.PulseHit(creature);
    }

    public static void CardCast(Creature witch, Creature? target, WitchCard card, bool isCounterpoint)
    {
        var suit = WitchCardAppearance.Suit(card);
        if (isCounterpoint)
        {
            WitchLiveRig.PulseCounterpoint(witch);
        }
        else
        {
            WitchLiveRig.PulseGesture(witch, suit, card.Type == CardType.Attack);
        }

        WitchCombatVfx.SpawnCardCast(witch, target, card, suit, isCounterpoint);
    }

    public static void AttackHit(Creature witch, Creature target, WitchCard card) =>
        WitchCombatVfx.SpawnAttackHit(witch, target, card);

    public static void LifePaid(Creature witch, int actualPaid, bool isGamble) =>
        WitchCombatVfx.SpawnLifePaid(witch, actualPaid, isGamble);

    public static void PurifyTransmute(Creature witch) =>
        WitchCombatVfx.SpawnPurifyTransmute(witch);

    public static void CardDrawn(Creature witch) =>
        WitchCombatVfx.SpawnCardDraw(witch);

    public static void CardDiscarded(Creature witch) =>
        WitchCombatVfx.SpawnCardDiscard(witch);

    public static void CardExhausted(Creature witch) =>
        WitchCombatVfx.SpawnCardExhaust(witch);

    public static void UpdateInscription(Creature witch, int baseReflectPercent) =>
        WitchLiveRig.SetInscriptionLevel(witch, baseReflectPercent);

    public static void CombatEnded() =>
        WitchCombatVfx.ClearTransientCombatEffects();

    public static void Invert(Creature creature)
    {
        WitchLiveRig.SetMirrorAwakened(creature, true);
        if (creature.GetCreatureNode()?.Visuals.GetNodeOrNull<Sprite2D>("MirrorFormVisual") is { } mirror)
        {
            var awakening = mirror.CreateTween();
            awakening.TweenProperty(mirror, "modulate", new Color("ffe8ec"), 0.28);
            awakening.TweenProperty(mirror, "modulate", Colors.White, 0.35);
        }

        WitchCombatVfx.SpawnCoreCue(creature, "mirror");
    }

    public static void UpdateHealth(Creature creature)
    {
        WitchLiveRig.EnsureAttached(creature);
        var low = creature.IsAlive && WitchRules.IsBelowHalf(creature.CurrentHp, creature.MaxHp);
        WitchLiveRig.SetLowHealth(creature, low);

        var visuals = creature.GetCreatureNode()?.Visuals;
        if (visuals?.GetNodeOrNull<AnimationPlayer>("HealthAnimation") is not { } animation)
        {
            return;
        }

        // This player only controls the sprite's own color; BaseLib keeps ownership of combat animations.
        if (low)
        {
            if (!animation.IsPlaying())
            {
                animation.Play("low_health");
            }
        }
        else if (animation.IsPlaying())
        {
            animation.Stop();
            visuals.Body.SelfModulate = Colors.White;
        }
    }
}
