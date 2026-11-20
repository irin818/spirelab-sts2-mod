using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class WitchFeedback
{
    public static void Enlighten(Creature creature) => Pulse(creature, "enlighten", new Color("c19a60"), 43);
    public static void Invert(Creature creature) => Pulse(creature, "mirror", new Color("b8c2ce"), 96);
    public static void Reflect(Creature dealer) => Pulse(dealer, "reflect", new Color("b8c2ce"), 28);
    public static void Desire(Creature creature) => Pulse(creature, "desire", new Color("cf3044"), 63);
    public static void Energy(Creature creature) => Pulse(creature, "energy", new Color("9479b5"), 29);

    private static void Pulse(Creature creature, string cue, Color color, float radius)
    {
        if (DisplayServer.GetName() == "headless" || creature.GetVfxContainer() is not { } host)
        {
            return;
        }

        var pulse = new Line2D
        {
            Width = 3,
            DefaultColor = color,
            Antialiased = true,
            Closed = true,
            Points = [new(-radius, 0), new(0, -radius), new(radius, 0), new(0, radius)]
        };
        host.AddChild(pulse);
        var tween = pulse.CreateTween().SetParallel();
        tween.TweenProperty(pulse, "scale", new Vector2(1.8f, 1.8f), 0.34);
        tween.TweenProperty(pulse, "modulate:a", 0f, 0.34);
        tween.Finished += pulse.QueueFree;

        var audio = new AudioStreamPlayer
        {
            Stream = ResourceLoader.Load<AudioStream>($"{MainFile.ResPath}/audio/witch/{cue}.wav"),
            VolumeDb = -15
        };
        host.AddChild(audio);
        audio.Finished += audio.QueueFree;
        audio.Play();
    }
}
