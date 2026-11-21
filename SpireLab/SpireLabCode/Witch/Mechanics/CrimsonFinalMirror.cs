using System.Runtime.CompilerServices;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using static SpireLab.SpireLabCode.Witch.Mechanics.CrimsonMirrorCamera;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class CrimsonFinalMirror
{
    private const string Art = TheWitch.Art + "/finisher/";
    private const string Audio = MainFile.ResPath + "/audio/witch/finisher/";
    internal const float Duration = 2.7f;
    internal const float ShatterTime = 1.95f;
    private static readonly Vector2 Design = new(1920, 1080);
    private static readonly Vector2 MirrorCenter = new(1248, 578);
    private static readonly Vector2 WitchCenter = new(365, 641);
    private static readonly string[] PosePaths = ["gaze", "raise", "open", "grasp", "fist", "rest"];
    private static readonly ConditionalWeakTable<ICombatState, RoomCue> Cues = new();
    private static bool _materialsWarmed;

    private sealed class RoomCue
    {
        public Creature? Candidate;
        public Texture2D? Silhouette;
        public Rect2? Framing;
        public bool Played;
    }

    internal static IEnumerable<string> Assets =>
        PosePaths.Select(pose => Art + pose + ".webp").Concat(
        [
            Art + "theatre.jpg", Art + "mirror.webp", Art + "soul.webp",
            Art + "mirror_drape.gdshader", Art + "soul.gdshader",
            Audio + "ritual-swell.wav", Audio + "mirror-shatter.wav"
        ]).Concat(CrimsonMirrorPortrait.Assets);

    private static bool CanPresent(ICombatState state) =>
        DisplayServer.GetName() != "headless" && RunManager.Instance.NetService.Type == NetGameType.Singleplayer &&
        state.Players.Any(player => player.Character is TheWitch && LocalContext.IsMe(player));

    internal static void WarmMaterials(ICombatState state)
    {
        if (_materialsWarmed || !CanPresent(state) || NCombatRoom.Instance is not { } room)
        {
            return;
        }
        _materialsWarmed = true;
        var warm = new CanvasLayer { Layer = 1 };
        try
        {
            // Shader resources are preloaded at run start. Submit tiny draws before the first
            // hand so the renderer can prepare their canvas pipelines before the fatal hit.
            var samples = new (string Texture, string Shader, bool Drape)[]
            {
                ("theatre.jpg", "mirror_drape.gdshader", true),
                ("soul.webp", "soul.gdshader", false),
                ("v2/portrait.webp", "v2/portrait.gdshader", false)
            };
            room.AddChild(warm);
            foreach (var sample in samples)
            {
                var texture = PreloadManager.Cache.GetTexture2D(Art + sample.Texture);
                var material = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(Art + sample.Shader) };
                if (sample.Drape)
                {
                    material.SetShaderParameter("fade", 0f);
                    warm.AddChild(new TextureRect
                    {
                        Size = Vector2.One * 2, Texture = texture, Material = material,
                        ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, MouseFilter = Control.MouseFilterEnum.Ignore
                    });
                }
                else
                {
                    warm.AddChild(new Sprite2D
                    {
                        Texture = texture, Material = material, Centered = false,
                        SelfModulate = new Color(1f, 1f, 1f, 0.01f), Scale = Vector2.One * 0.002f
                    });
                }
            }
            var dispose = warm.CreateTween();
            dispose.TweenInterval(0.1);
            dispose.TweenCallback(Callable.From(warm.QueueFree));
        }
        catch (Exception error)
        {
            GD.PushWarning($"[The Witch] Finisher shader warmup skipped: {error.Message}");
            warm.QueueFree();
        }
    }

    internal static void RememberDefeat(Creature creature)
    {
        if (!creature.IsEnemy || creature.CombatState is not { } state || !CanPresent(state))
        {
            return;
        }

        // BeforeDeath is the last hook before the native visual is retired. Keep only a reference
        // to an existing static texture. Animated Spine enemies use the painted apparition.
        var cue = Cues.GetOrCreateValue(state);
        cue.Candidate = creature;
        var node = creature.GetCreatureNode();
        cue.Framing = ReadSubject(node?.Hitbox);
        var body = node?.Body;
        cue.Silhouette = body is Sprite2D { RegionEnabled: false, Hframes: 1, Vframes: 1 } sprite
            ? sprite.Texture
            : null;
    }

    internal static async Task Play(CombatRoom room)
    {
        if (!CanPresent(room.CombatState) ||
            NCombatRoom.Instance is not { } host || !host.IsInsideTree() || host.IsQueuedForDeletion())
        {
            return;
        }

        var cue = Cues.GetOrCreateValue(room.CombatState);
        if (cue.Played)
        {
            return;
        }
        cue.Played = true;

        var layer = new CanvasLayer { Name = "WitchCrimsonFinalMirror", Layer = 1 };
        var last = cue.Candidate is { IsDead: true } ? cue.Candidate : null;
        var ally = room.CombatState.Players.FirstOrDefault(player => player.Character is TheWitch && LocalContext.IsMe(player));
        var framing = last == null ? null : cue.Framing;
        var camera = new CrimsonMirrorCamera(host, framing, ReadSubject(ally?.Creature.GetCreatureNode()?.Hitbox));
        try
        {
            if (Assets.Any(path => !ResourceLoader.Exists(path)))
            {
                throw new InvalidOperationException("The finisher's resource pack is incomplete.");
            }
            var spirit = last == null ? null : cue.Silhouette;
            var corpse = host.RemovingCreatureNodes.FirstOrDefault(node => node.Entity == last)?.Body;
            var presentation = new Presentation(layer, camera, framing, corpse, spirit);
            host.AddChild(layer);
            await presentation.Play();
        }
        catch (Exception exception)
        {
            // Reward and saving must continue on a missing pack asset or a presentation error.
            GD.PushWarning($"[The Witch] Crimson Final Mirror skipped: {exception.Message}");
        }
        finally
        {
            camera.Restore();
            if (GodotObject.IsInstanceValid(layer))
            {
                if (layer.IsInsideTree())
                {
                    layer.Visible = false;
                    layer.QueueFree();
                }
                else if (!layer.IsQueuedForDeletion())
                {
                    layer.Free();
                }
            }
        }
    }

    private sealed class Presentation
    {
        private readonly CanvasLayer _layer;
        private readonly CrimsonMirrorCamera _camera;
        private readonly Rect2? _target;
        private readonly Node2D? _corpse;
        private readonly Node2D _opening = new();
        private readonly Node2D _enemyCracks;
        private readonly Sprite2D _enemyEcho;
        private readonly Line2D _turnRibbon;
        private readonly CrimsonMirrorPortrait _portrait;
        private readonly Polygon2D[] _embers;
        private readonly Control _layout = new() { MouseFilter = Control.MouseFilterEnum.Ignore, ClipContents = true };
        private readonly ColorRect _shade = new() { MouseFilter = Control.MouseFilterEnum.Ignore };
        private readonly TextureRect _theatre = new()
        {
            Name = "MirrorDrape",
            Texture = Load<Texture2D>(Art + "theatre.jpg"),
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        private readonly ShaderMaterial _drape = new() { Shader = Load<Shader>(Art + "mirror_drape.gdshader") };
        private readonly ShaderMaterial _spiritInk = new() { Shader = Load<Shader>(Art + "soul.gdshader") };
        private readonly Node2D _shot = new() { Name = "StageShot" };
        private readonly Node2D _stage = new();
        private readonly Sprite2D _mirror;
        private readonly Sprite2D _witch;
        private readonly Sprite2D _soul;
        private readonly Texture2D[] _poses = PosePaths.Select(pose => Load<Texture2D>(Art + pose + ".webp")).ToArray();
        private readonly Node2D _cracks;
        private readonly Node2D _sigil;
        private readonly Line2D[] _filaments;
        private readonly Polygon2D _spark;
        private readonly Node2D _wingCracks;
        private readonly Shard[] _shards;
        private readonly AudioStreamPlayer _swell = LoadSound("ritual-swell", -8);
        private readonly AudioStreamPlayer _shatter = LoadSound("mirror-shatter", -8);
        private bool _impactPlayed;

        private readonly record struct Shard(Polygon2D Node, Vector2 From, Vector2 Travel, float Spin);

        internal Presentation(CanvasLayer layer, CrimsonMirrorCamera camera, Rect2? target, Node2D? corpse, Texture2D? spirit)
        {
            _layer = layer;
            _camera = camera;
            _target = target;
            _corpse = corpse;
            if (_theatre.Texture == null || _poses.Any(pose => pose == null) || _drape.Shader == null || _spiritInk.Shader == null)
            {
                throw new InvalidOperationException("The theatre or cast art is missing.");
            }

            layer.AddChild(_layout);
            _layout.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _layout.AddChild(_shade);
            _layout.AddChild(_theatre);
            _shade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _theatre.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            _theatre.Material = _drape;
            _layout.AddChild(_opening);
            _enemyEcho = new Sprite2D { Texture = spirit ?? Load<Texture2D>(Art + "soul.webp") };
            _opening.AddChild(_enemyEcho);
            _enemyCracks = MakeCracks(_opening, Vector2.Zero);
            _enemyCracks.Name = "ActualTargetCracks";
            _turnRibbon = MakeThread(_opening, new Color(0.98f, 0.24f, 0.43f, 0.8f), 1.9f);
            _layout.AddChild(_stage);
            _stage.AddChild(_shot);
            _layout.Resized += Fit;
            _portrait = new CrimsonMirrorPortrait(_shot);
            _mirror = Painted(Load<Texture2D>(Art + "mirror.webp"), 552, MirrorCenter);
            _mirror.Name = "SoulMirror";
            _soul = Painted(spirit ?? Load<Texture2D>(Art + "soul.webp"), 322, MirrorCenter);
            _soul.Material = _spiritInk;
            _cracks = MakeCracks(_shot, MirrorCenter);
            _wingCracks = MakeWingCracks();
            _witch = Painted(_poses[0], 462, WitchCenter);
            _sigil = MakeSigil();
            _filaments =
            [
                MakeThread(_shot, new Color(0.99f, 0.53f, 0.67f, 0.67f), 1.65f),
                MakeThread(_shot, new Color(0.90f, 0.25f, 0.48f, 0.60f), 1.15f),
                MakeThread(_shot, new Color(0.95f, 0.24f, 0.41f, 0.40f), 1.25f)
            ];
            _embers = MakeEmbers();
            _spark = new Polygon2D
            {
                Position = MirrorCenter,
                ZIndex = 2,
                Color = new Color("f8c4d9"),
                Polygon = [new(-137, 0), new(-9, -7), new(0, -39), new(9, -7), new(137, 0),
                           new(9, 7), new(0, 39), new(-9, 7)]
            };
            _shot.AddChild(_spark);
            _shards = MakeShards();
            _swell.PitchScale = 0.83f;
            _shatter.Name = "MirrorImpact";
            layer.AddChild(_swell);
            layer.AddChild(_shatter);
        }

        private void Fit()
        {
            var scale = Mathf.Min(_layout.Size.X / Design.X, _layout.Size.Y / Design.Y);
            _stage.Scale = Vector2.One * scale;
            _stage.Position = (_layout.Size - Design * scale) / 2;
        }

        internal async Task Play()
        {
            Fit();
            Render(0);
            _swell.Play();
            var finished = new TaskCompletionSource();
            void Exit() => finished.TrySetResult();
            _layer.TreeExiting += Exit;
            var tween = _layer.CreateTween().SetIgnoreTimeScale(true).SetPauseMode(Tween.TweenPauseMode.Process);
            tween.TweenMethod(Callable.From<float>(Render), 0f, Duration, Duration);
            tween.Finished += Exit;
            try
            {
                await finished.Task;
            }
            finally
            {
                if (GodotObject.IsInstanceValid(_layer))
                {
                    _layer.TreeExiting -= Exit;
                }
                if (GodotObject.IsInstanceValid(tween) && tween.IsRunning())
                {
                    tween.Kill();
                }
            }
        }

        private void Render(float time)
        {
            var fade = 1 - Smooth(time, 2.18f, Duration);
            var framer = _camera.Frame(time);
            _camera.Apply(framer);
            RenderOpening(time, framer);

            var aperture = Smooth(time, 0.43f, 0.97f);
            var seam = Smooth(time, 0.30f, 0.47f) * (1 - Smooth(time, 0.80f, 1.00f));
            var release = Smooth(time, ShatterTime, 2.15f);
            var snap = Mathf.Sin(release * Mathf.Pi);
            var compression = Smooth(time, 1.50f, 1.92f);
            var portraitArrival = Smooth(time, 0.59f, 1.28f);
            var portraitToMirror = Smooth(time, 1.47f, 1.98f);
            var cameraAim = new Vector2(950, 508).Lerp(CrimsonMirrorPortrait.Face.Lerp(CrimsonMirrorPortrait.ExpectedPalm, 0.40f), portraitArrival)
                .Lerp(MirrorCenter - new Vector2(38, 38), portraitToMirror);
            var cameraZoom = 1f + (0.17f * portraitArrival + 0.09f * portraitToMirror) * fade;
            var screenCenter = new Vector2(960, 515);
            var aim = screenCenter.Lerp(cameraAim, portraitArrival * fade);
            var kickPhase = Mathf.Clamp((time - ShatterTime) / 0.23f, 0, 1);
            var kick = Mathf.Sin(kickPhase * Mathf.Tau) * (1 - kickPhase) * (1 - kickPhase);
            _shot.Scale = Vector2.One * cameraZoom;
            _shot.Position = screenCenter - aim * cameraZoom + new Vector2(-24, 13) * kick;
            _shade.Color = new Color(0.016f, 0.010f, 0.041f,
                Mathf.Lerp(0.32f, 0.67f, aperture) * Smooth(time, 0, 0.39f) * fade);
            _drape.SetShaderParameter("aperture", 0.548f * aperture);
            _drape.SetShaderParameter("curtain", seam);
            _drape.SetShaderParameter("fade", fade);
            _drape.SetShaderParameter("elapsed", time);
            _drape.SetShaderParameter("impact", snap);
            _drape.SetShaderParameter("contraction", compression * (1 - release));
            _drape.SetShaderParameter("parallax", (aim - screenCenter) / Design * -0.29f);
            _drape.SetShaderParameter("depth_zoom", Mathf.Lerp(1f, cameraZoom, 0.34f));
            var palm = _portrait.Render(time, fade, compression, snap, ShatterTime, aim.X - screenCenter.X);

            _mirror.Visible = time < ShatterTime;
            var reveal = Smooth(time, 0.73f, 1.15f);
            _mirror.Modulate = Colors.White with { A = reveal * fade };
            _soul.Modulate = Colors.White with { A = reveal * (1 - release) * fade };
            _soul.Position = MirrorCenter + new Vector2(0, Mathf.Sin(time * 6f) * 10 * (1 - compression));
            _soul.Scale = Vector2.One * FitSprite(_soul.Texture, 216, 305) * Mathf.Lerp(1, 0.065f, compression);
            _spiritInk.SetShaderParameter("fracture", compression);
            _wingCracks.Modulate = Colors.White with { A = (0.31f * compression * (1 - release) + snap * 0.82f) * fade };
            _wingCracks.Scale = Vector2.One * Mathf.Lerp(1.43f, 1f, compression);
            _cracks.Visible = time < ShatterTime;
            _cracks.Modulate = Colors.White with { A = Smooth(time, 1.43f, 1.91f) };
            _cracks.Scale = Vector2.One * Mathf.Lerp(0.2f, 1f, compression);
            var pose = time switch
            {
                < 0.5f => 0,
                < 1.03f => 1,
                < 1.53f => 2,
                < ShatterTime => 3,
                < 2.18f => 4,
                _ => 5
            };
            if (_witch.Texture != _poses[pose])
            {
                _witch.Texture = _poses[pose];
            }
            _witch.Modulate = new Color(0.70f, 0.64f, 0.81f, 0.79f * aperture * fade);
            var thread = Smooth(time, 0.98f, 1.28f) * (1 - Smooth(time, 1.91f, 2.08f));
            _sigil.Position = palm;
            _sigil.Modulate = Colors.White with { A = thread };
            _sigil.Rotation = time * -0.86f;
            _sigil.Scale = Vector2.One * Mathf.Lerp(1.42f, 0.60f, compression);
            for (var i = 0; i < _filaments.Length; i++)
            {
                var bend = (i - 1) * 35 * (1 - 0.68f * compression);
                Bend(_filaments[i], palm, MirrorCenter + new Vector2(-29, (i - 1) * 30 * (1 - compression)),
                    bend, thread, time);
            }
            RenderEmbers(palm, time, aperture * fade, compression);
            _spark.Modulate = Colors.White with { A = Mathf.Max(snap * 0.81f, compression * 0.64f * (1 - release)) };
            _spark.Scale = Vector2.One * Mathf.Lerp(0.14f, 1.48f, release);
            if (time >= ShatterTime && !_impactPlayed)
            {
                _impactPlayed = true;
                _shatter.Play();
            }

            var burst = 1 - Mathf.Pow(1 - Mathf.Clamp((time - ShatterTime) / (Duration - ShatterTime), 0, 1), 2.6f);
            foreach (var shard in _shards)
            {
                shard.Node.Visible = _impactPlayed;
                shard.Node.Position = shard.From + shard.Travel * (1.35f * burst) + new Vector2(0, 83 * burst * burst);
                shard.Node.Rotation = burst * shard.Spin;
                shard.Node.Modulate = Colors.White with { A = fade * (1 - 0.31f * burst) };
            }
        }

        private void RenderOpening(float time, Framing camera)
        {
            _opening.Visible = time < 0.95f;
            if (!_opening.Visible)
            {
                return;
            }

            var layout = _layout.Size;
            var foe = _target?.GetCenter() ?? camera.Focus;
            var focus = camera.Project(foe) * layout;
            var envelope = Smooth(time, 0.04f, 0.36f) * (1 - Smooth(time, 0.46f, 0.76f));
            var size = Mathf.Clamp((_target?.Size.Y ?? 0.24f) / 0.24f, 0.63f, 1.35f);
            _enemyCracks.Position = focus;
            _enemyCracks.Scale = Vector2.One * (layout.Y / Design.Y) * size * Mathf.Lerp(0.63f, 1.25f, envelope);
            _enemyCracks.Modulate = Colors.White with { A = envelope * 0.67f };
            var stillVisible = GodotObject.IsInstanceValid(_corpse) && _corpse!.IsVisibleInTree();
            _enemyEcho.Position = focus;
            _enemyEcho.Scale = Vector2.One * (layout.Y / Design.Y) * size * 0.63f * camera.Zoom;
            _enemyEcho.Modulate = new Color("a098c3") with { A = stillVisible ? 0.05f * envelope : 0.40f * envelope };
            var follow = Smooth(time, 0.35f, 0.87f);
            Bend(_turnRibbon, focus, camera.Project(camera.Focus) * layout,
                -layout.Y * (0.02f + 0.059f * follow),
                Smooth(time, 0.37f, 0.58f) * (1 - Smooth(time, 0.71f, 0.94f)), time);
        }

        private Polygon2D[] MakeEmbers()
        {
            var embers = new Polygon2D[15];
            for (var i = 0; i < embers.Length; i++)
            {
                var radius = 1.3f + (i % 4) * 0.53f;
                var ember = new Polygon2D
                {
                    Color = new Color(i % 3 == 0 ? "f6becb" : "f55578"),
                    Polygon = [new(-radius, 0), new(0, -radius * 1.6f), new(radius, 0), new(0, radius * 1.6f)]
                };
                _shot.AddChild(ember);
                embers[i] = ember;
            }
            return embers;
        }

        private void RenderEmbers(Vector2 palm, float time, float opacity, float compression)
        {
            for (var i = 0; i < _embers.Length; i++)
            {
                var travel = Mathf.PosMod(time * (0.26f + i % 3 * 0.049f) + i * 0.203f, 1);
                var curve = Mathf.Sin(travel * Mathf.Pi);
                var start = i % 3 == 0 ? palm : new Vector2(i % 2 == 0 ? 410 : 1580, 613 - i * 11);
                var end = MirrorCenter + new Vector2((i - 7) * 9, (i % 4) * 24) * (1 - compression);
                _embers[i].Position = start.Lerp(end, travel) + new Vector2(0, -(44 + i * 4) * curve);
                _embers[i].Modulate = Colors.White with { A = curve * opacity * (0.28f + compression * 0.39f) };
            }
        }

        private Sprite2D Painted(Texture2D? texture, float height, Vector2 position)
        {
            if (texture == null)
            {
                throw new InvalidOperationException("A required painted sprite is missing.");
            }
            var sprite = new Sprite2D
            {
                Texture = texture,
                Position = position,
                Scale = Vector2.One * (height / texture.GetHeight())
            };
            _shot.AddChild(sprite);
            return sprite;
        }

        private static Node2D MakeCracks(Node parent, Vector2 origin)
        {
            var node = new Node2D { Position = origin };
            parent.AddChild(node);
            Stroke(node, new Color("eb547a"), 2.2f, new(-155, -97), new(-76, -59), new(-55, -13),
                Vector2.Zero, new(23, 64), new(76, 71), new(116, 149));
            Stroke(node, new Color("ffb5d6"), 1.5f, new(-97, 160), new(-72, 58), new(-14, 24),
                Vector2.Zero, new(54, -42), new(42, -100), new(99, -180));
            Stroke(node, new Color("d93762"), 1.4f, new(-155, 3), new(-102, 20), Vector2.Zero,
                new(73, -20), new(104, -66), new(151, -82));
            return node;
        }

        private Node2D MakeWingCracks()
        {
            var cracks = new Node2D { Position = MirrorCenter };
            _shot.AddChild(cracks);
            foreach (var side in new[] { -1f, 1f })
            {
                var branch = new Node2D { Scale = new Vector2(side, 1) };
                cracks.AddChild(branch);
                Stroke(branch, new Color("e07d9c"), 1.8f, new(126, -16), new(249, -102),
                    new(337, -89), new(487, -177), new(631, -207));
                Stroke(branch, new Color("ca587e"), 1.2f, new(248, -102), new(264, -166),
                    new(311, -210), new(337, -286));
                Stroke(branch, new Color("dd6384"), 1.5f, new(126, 31), new(264, 69),
                    new(388, 49), new(515, 134), new(658, 157));
            }
            return cracks;
        }

        private Node2D MakeSigil()
        {
            var ring = new Node2D { ZIndex = 2 };
            _shot.AddChild(ring);
            var ink = new Color("ee6a90");
            var points = Enumerable.Range(0, 33).Select(i => Vector2.FromAngle(Mathf.Tau * i / 32) * 19).ToArray();
            Stroke(ring, ink, 1.4f, points);
            for (var i = 0; i < 8; i++)
            {
                var ray = Vector2.FromAngle(Mathf.Tau * i / 8);
                Stroke(ring, ink, 1.2f, ray * 23, ray * 28);
            }
            Stroke(ring, new Color("ffe4ed"), 1.2f, new(0, -14), new(9, 0), new(0, 14), new(-9, 0), new(0, -14));
            return ring;
        }

        private static Line2D MakeThread(Node parent, Color ink, float width)
        {
            var line = new Line2D { DefaultColor = ink, Width = width, Antialiased = true, Points = new Vector2[15] };
            parent.AddChild(line);
            return line;
        }

        private static void Bend(Line2D line, Vector2 start, Vector2 end, float bow, float alpha, float time)
        {
            line.Visible = alpha > 0.004f;
            if (!line.Visible)
            {
                return;
            }
            line.Modulate = Colors.White with { A = alpha };
            for (var i = 0; i < 15; i++)
            {
                var t = i / 14f;
                var curve = Mathf.Sin(t * Mathf.Pi);
                line.SetPointPosition(i, start.Lerp(end, t) + new Vector2(0, curve * (bow + 5f * Mathf.Sin(t * 9 - time * 11))));
            }
        }

        private Shard[] MakeShards()
        {
            var result = new List<Shard>();
            var texture = _mirror.Texture;
            var size = texture.GetSize();
            var rim = new Vector2[]
            {
                new(0.02f, 0.03f), new(0.32f, 0.03f), new(0.59f, 0.01f), new(0.98f, 0.03f),
                new(1, 0.30f), new(0.99f, 0.61f), new(0.99f, 1), new(0.68f, 0.99f),
                new(0.49f, 1), new(0.22f, 0.99f), new(0, 0.99f), new(0.01f, 0.60f), new(0, 0.33f)
            };
            for (var i = 0; i < rim.Length; i++)
            {
                var uv = new[] { new Vector2(0.53f, 0.49f), rim[i], rim[(i + 1) % rim.Length] };
                var center = (uv[0] + uv[1] + uv[2]) / 3;
                var origin = MirrorCenter + (center - Vector2.One * 0.5f) * size * _mirror.Scale;
                var direction = (origin - MirrorCenter).Normalized();
                var polygon = new Polygon2D
                {
                    Texture = texture,
                    Polygon = uv.Select(point => (point - center) * size * _mirror.Scale).ToArray(),
                    UV = uv.Select(point => point * size).ToArray(),
                    Color = new Color("e6bcdd"),
                    Visible = false
                };
                _shot.AddChild(polygon);
                result.Add(new Shard(polygon, origin, direction * (110 + i * 12), (i % 2 == 0 ? 1 : -1) * (0.4f + i * 0.13f)));
            }
            for (var i = 0; i < 19; i++)
            {
                var direction = Vector2.FromAngle(i * 2.399963f);
                var length = 4 + i % 5 * 2;
                var polygon = new Polygon2D
                {
                    Polygon = [new(-length, 0), new(1, -2), new(length, 0), new(1, 2)],
                    Color = new Color(i % 3 == 0 ? "dac6e9" : "e94069"),
                    Visible = false
                };
                _shot.AddChild(polygon);
                result.Add(new Shard(polygon, MirrorCenter + direction * 12, direction * (168 + i * 13), i * 0.32f));
            }
            return result.ToArray();
        }

        private static void Stroke(Node parent, Color ink, float width, params Vector2[] line) =>
            parent.AddChild(new Line2D { DefaultColor = ink, Width = width, Points = line, Antialiased = true });

        private static T Load<T>(string path) where T : Resource =>
            PreloadManager.Cache.GetAsset<T>(path);

        private static AudioStreamPlayer LoadSound(string name, float volume)
        {
            var settings = SaveManager.Instance.SettingsSave;
            return new AudioStreamPlayer
            {
                Stream = Load<AudioStream>(Audio + name + ".wav"),
                VolumeDb = volume + Mathf.LinearToDb(Mathf.Max(0.0001f, settings.VolumeMaster * settings.VolumeSfx))
            };
        }

        private static float FitSprite(Texture2D texture, float width, float height) =>
            Mathf.Min(width / texture.GetWidth(), height / texture.GetHeight());
    }
}
