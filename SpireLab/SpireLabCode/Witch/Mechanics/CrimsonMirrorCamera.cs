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
    private readonly Rect2? _ally;

    public CrimsonMirrorCamera(NCombatRoom room, Rect2? foe, Rect2? ally)
    {
        _foe = foe;
        _ally = ally;
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

    public Framing Frame(float time)
    {
        var enemy = _foe?.GetCenter() ?? new Vector2(0.57f, 0.44f);
        var witch = _ally?.GetCenter() ?? enemy;
        // Aim at the upper half of tall opponents. Small ones can handle a tighter push.
        enemy.Y -= (_foe?.Size.Y ?? 0f) * 0.11f;
        witch.Y -= (_ally?.Size.Y ?? 0f) * 0.18f;
        var turn = Smooth(time, 0.40f, 0.94f);
        var push = Smooth(time, 0, 0.41f);
        var bridge = 1 - Smooth(time, 0.77f, 1.16f);
        var zoom = Mathf.Lerp(1.32f, 1.13f, Mathf.Clamp(((_foe?.Size.Y ?? 0.4f) - 0.12f) / 0.46f, 0, 1));
        var focus = enemy.Lerp(witch, turn);
        var destination = new Vector2(0.58f, 0.45f).Lerp(new Vector2(0.44f, 0.45f), turn);
        var amount = push * bridge;
        return new Framing(focus, 1f + (zoom - 1f - 0.04f * turn) * amount, (destination - focus) * amount * 0.84f);
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
