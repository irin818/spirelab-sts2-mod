using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class WitchFeedback
{
    public static void Enlighten(Creature creature) => Pulse(creature, "enlighten", new Color("eed9d9"), 43);
    public static void Reflect(Creature dealer) => Pulse(dealer, "reflect", new Color("d6e1ed"), 28);
    public static void Desire(Creature creature) => Pulse(creature, "desire", new Color("d84362"), 63);
    public static void Energy(Creature creature) => Pulse(creature, "energy", new Color("c58298"), 29);
    public static void ReserveEnergy(Creature creature) => Pulse(creature, "reserve", new Color("f6ccd4"), 66);
    public static void Block(Creature creature) => Pulse(creature, null, new Color("d6e1ed"), 52);

    public static void Invert(Creature creature)
    {
        if (creature.GetCreatureNode()?.Visuals.GetNodeOrNull<Sprite2D>("MirrorFormVisual") is { } mirror)
        {
            var awakening = mirror.CreateTween();
            awakening.TweenProperty(mirror, "modulate", new Color("ffe8ec"), 0.28);
            awakening.TweenProperty(mirror, "modulate", Colors.White, 0.35);
        }

        Pulse(creature, "mirror", new Color("dfecf9"), 96);
    }

    public static void UpdateHealth(Creature creature)
    {
        var visuals = creature.GetCreatureNode()?.Visuals;
        if (visuals?.GetNodeOrNull<AnimationPlayer>("HealthAnimation") is not { } animation)
        {
            return;
        }

        // This player only controls the sprite's own color; BaseLib keeps ownership of combat animations.
        if (creature.IsAlive && WitchRules.IsBelowHalf(creature.CurrentHp, creature.MaxHp))
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

    private static void Pulse(Creature creature, string? cue, Color color, float radius)
    {
        if (DisplayServer.GetName() == "headless" || creature.GetVfxContainer() is not { } host
            || creature.GetCreatureNode() is not { } target)
        {
            return;
        }

        var pulse = new Line2D
        {
            Name = cue == null ? "WitchBlockFlash" : "WitchMagicFlash",
            Width = 3,
            DefaultColor = color,
            Antialiased = true,
            Closed = true,
            Points = [new(-radius, 0), new(0, -radius), new(radius, 0), new(0, radius)]
        };
        host.AddChild(pulse);
        pulse.GlobalPosition = target.VfxSpawnPosition;
        var tween = pulse.CreateTween().SetParallel();
        tween.TweenProperty(pulse, "scale", new Vector2(1.8f, 1.8f), 0.34);
        tween.TweenProperty(pulse, "modulate:a", 0f, 0.34);
        tween.Finished += pulse.QueueFree;

        if (cue == null)
        {
            return;
        }

        var glyph = cue == "energy" ? $"{TheWitch.Art}/witch_energy.svg" : $"{TheWitch.Art}/abilities/{cue}.svg";
        pulse.AddChild(new Sprite2D
        {
            Texture = ResourceLoader.Load<Texture2D>(glyph),
            Scale = new Vector2(0.48f, 0.48f)
        });

        var audio = new AudioStreamPlayer
        {
            Stream = ResourceLoader.Load<AudioStream>($"{MainFile.ResPath}/audio/witch/{(cue == "reserve" ? "energy" : cue)}.wav"),
            VolumeDb = -15
        };
        host.AddChild(audio);
        audio.Finished += audio.QueueFree;
        audio.Play();
    }
}
