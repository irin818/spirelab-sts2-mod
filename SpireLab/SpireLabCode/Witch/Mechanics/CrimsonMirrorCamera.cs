using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

/// <summary>The opening camera follows native combat geometry, in normalized viewport coordinates.</summary>
internal sealed class CrimsonMirrorCamera
{
    internal readonly record struct Framing(Vector2 Focus, float Zoom, Vector2 Slide)
    {
        public Vector2 Project(Vector2 point) => Focus + (point - Focus) * Zoom + Slide;
    }

    private readonly Lens? _actors;
    private readonly Lens? _background;
    private readonly Rect2? _foe;

    public CrimsonMirrorCamera(NCombatRoom room, Rect2? foe)
    {
        _foe = foe;
        var actors = room.SceneContainer;
        if (actors == null || actors.IsAncestorOf(room.Ui))
        {
            return;
        }

        _actors = new Lens(actors);
        var backdrop = room.GetNodeOrNull<Control>("%BgContainer");
        if (backdrop != null && !actors.IsAncestorOf(backdrop) && !backdrop.IsAncestorOf(actors)
            && !backdrop.IsAncestorOf(room.Ui))
        {
            _background = new Lens(backdrop);
        }
    }

    public static Rect2? ReadSubject(Control? hitbox)
    {
        if (hitbox == null || !hitbox.IsInsideTree() || hitbox.Size.Y < 1)
        {
            return null;
        }

        var viewport = hitbox.GetViewportRect().Size;
        if (viewport.X < 1 || viewport.Y < 1)
        {
            return null;
        }

        var canvas = hitbox.GetGlobalTransformWithCanvas();
        var start = (canvas * Vector2.Zero) / viewport;
        var end = (canvas * hitbox.Size) / viewport;
        return new Rect2(start, end - start).Abs();
    }

    internal static readonly Vector2 StageCenter = new(960, 517);
    internal static readonly Vector2 MirrorCenter = new(960, 641);

    // Depth weights apply to both framing and roll. Each painted plane keeps its own world
    // coordinates, so a foreground gesture can cross the glass without drifting off its axis.
    internal readonly record struct StageFrame(Vector2 Aim, float Zoom, float Roll, Vector2 Impulse,
        float FaceFocus, float MirrorFocus, float HallFocus)
    {
        public void Apply(Node2D plane, float depth)
        {
            var zoom = Mathf.Lerp(1f, Zoom, depth);
            var rotation = Roll * depth;
            var target = StageCenter.Lerp(Aim, depth);
            plane.Scale = Vector2.One * zoom;
            plane.Rotation = rotation;
            plane.Position = StageCenter - target.Rotated(rotation) * zoom + Impulse * depth;
        }
    }

    public static StageFrame Theatre(float time)
    {
        var face = CrimsonMirrorPortrait.Face;
        var mirror = MirrorCenter - new Vector2(0, 40);
        var neutral = new StageFrame(StageCenter, 1f, 0f, Vector2.Zero, 2.5f, 0.55f, 0.45f);
        var reveal = neutral with { Aim = mirror, Zoom = 0.93f };
        var arrive = reveal with { Aim = face + new Vector2(-6, 34), Zoom = 1.34f, Roll = -0.018f,
            FaceFocus = 0.20f, MirrorFocus = 1.70f, HallFocus = 1.52f };
        var smile = arrive with { Aim = face + new Vector2(24, 20), Zoom = 1.48f, Roll = -0.026f,
            FaceFocus = 0f, MirrorFocus = 1.97f };
        var hand = neutral with { Aim = new Vector2(1029, 587), Zoom = 1.03f, Roll = 0.012f,
            FaceFocus = 1.33f, MirrorFocus = 0f, HallFocus = 2.05f };
        var core = hand with { Aim = mirror + new Vector2(-9, 29), Zoom = 1.20f,
            Roll = 0.004f, FaceFocus = 1.85f };
        var recoil = core with { Aim = new Vector2(966, 589), Zoom = 0.99f, Roll = -0.011f,
            FaceFocus = 2.22f, HallFocus = 1.46f };

        if (time < 1.05f)
        {
            var opening = Smooth(time, 0.42f, 1.05f);
            return Blend(neutral with { Zoom = 1.11f }, reveal, opening);
        }
        if (time < 1.55f)
        {
            var climb = Smooth(time, 1.05f, 1.55f);
            // The arc briefly looks past the upper right rim before settling on her face.
            var control = new Vector2(1133, 533);
            var arc = reveal.Aim.Lerp(control, climb).Lerp(control.Lerp(arrive.Aim, climb), climb);
            return Blend(reveal, arrive, climb) with { Aim = arc };
        }
        if (time < 2.15f)
        {
            var breath = Smooth(time, 1.55f, 2.15f);
            return Blend(arrive, smile, breath) with
            {
                Aim = arrive.Aim.Lerp(smile.Aim, breath) + new Vector2(10, -9) * Mathf.Sin(breath * Mathf.Pi)
            };
        }
        if (time < CrimsonFinalMirror.ShatterTime)
        {
            var retreat = Smooth(time, 2.15f, CrimsonFinalMirror.ShatterTime);
            return Blend(smile, hand, retreat);
        }
        if (time < 2.95f)
        {
            var jab = Smooth(time, CrimsonFinalMirror.ShatterTime, 2.79f);
            var recover = Smooth(time, 2.79f, 2.95f);
            var elapsed = (time - CrimsonFinalMirror.ShatterTime) / (2.95f - CrimsonFinalMirror.ShatterTime);
            var impulse = Mathf.Sin(elapsed * 9.3f) * Mathf.Pow(1f - elapsed, 2f);
            return Blend(Blend(hand, core, jab), recoil, recover) with { Impulse = new Vector2(-18, 13) * impulse };
        }
        return Blend(recoil, neutral with { FaceFocus = 2.9f, MirrorFocus = 1.80f, HallFocus = 2.3f },
            Smooth(time, 2.95f, CrimsonFinalMirror.Duration));
    }

    private static StageFrame Blend(StageFrame a, StageFrame b, float t) =>
        new(a.Aim.Lerp(b.Aim, t), Mathf.Lerp(a.Zoom, b.Zoom, t), Mathf.Lerp(a.Roll, b.Roll, t), Vector2.Zero,
            Mathf.Lerp(a.FaceFocus, b.FaceFocus, t), Mathf.Lerp(a.MirrorFocus, b.MirrorFocus, t), Mathf.Lerp(a.HallFocus, b.HallFocus, t));

    public Framing Frame(float time)
    {
        var enemy = _foe?.GetCenter() ?? new Vector2(0.56f, 0.49f);
        enemy.Y -= (_foe?.Size.Y ?? 0f) * 0.13f;
        var advance = Mathf.Clamp(time / 0.34f, 0, 1);
        var catchUp = 1f - Mathf.Pow(1f - advance, 3f);
        var pullBack = 1f - Smooth(time, 0.42f, 1.04f);
        var zoom = Mathf.Lerp(1.32f, 1.13f, Mathf.Clamp(((_foe?.Size.Y ?? 0.4f) - 0.12f) / 0.46f, 0, 1));
        var amount = (catchUp - 0.045f * Smooth(time, 0.29f, 0.42f)) * pullBack;
        var landing = new Vector2(0.59f, 0.47f);
        return new Framing(enemy, 1f + (zoom - 1f) * amount, (landing - enemy) * amount * 0.87f);
    }

    public void Apply(Framing view)
    {
        _actors?.Apply(view);
        _background?.Apply(new Framing(view.Focus, Mathf.Lerp(1f, view.Zoom, 0.49f), view.Slide * 0.39f));
    }

    public void Restore()
    {
        _background?.Restore();
        _actors?.Restore();
    }

    public static float Smooth(float time, float start, float end)
    {
        // Quintic interpolation avoids a velocity or acceleration step across shots.
        var t = Mathf.Clamp((time - start) / (end - start), 0f, 1f);
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }

    private sealed class Lens(Control layer)
    {
        private readonly Vector2 _scale = layer.Scale;
        private Vector2 _position = layer.Position;
        private Vector2 _lastWritten;
        private bool _touched;

        public void Apply(Framing frame)
        {
            if (!GodotObject.IsInstanceValid(layer) || !layer.IsInsideTree())
            {
                return;
            }
            if (Mathf.IsEqualApprox(frame.Zoom, 1f) && frame.Slide.IsZeroApprox())
            {
                Restore();
                return;
            }

            // The native screen-shake node runs before scene tweens and may refresh Position.
            // Preserve its latest value even if the battle is paused and no refresh occurred.
            ReadBasePosition();
            layer.Position = _position;
            layer.Scale = _scale;
            var screen = layer.GetViewportRect().Size;
            var origin = layer.GetGlobalTransformWithCanvas().Origin;
            var target = frame.Project(origin / screen) * screen;
            layer.Scale = _scale * frame.Zoom;
            var scaled = layer.GetGlobalTransformWithCanvas().Origin;
            var parent = layer.GetParent() as CanvasItem;
            var localDelta = parent == null ? target - scaled
                : parent.GetGlobalTransformWithCanvas().AffineInverse().BasisXform(target - scaled);
            layer.Position = _position + localDelta;
            _lastWritten = layer.Position;
            _touched = true;
        }

        public void Restore()
        {
            if (!_touched || !GodotObject.IsInstanceValid(layer))
            {
                return;
            }

            ReadBasePosition();
            layer.Scale = _scale;
            layer.Position = _position;
            _touched = false;
        }

        private void ReadBasePosition()
        {
            if (!_touched || !layer.Position.IsEqualApprox(_lastWritten))
            {
                _position = layer.Position;
            }
        }
    }
}
