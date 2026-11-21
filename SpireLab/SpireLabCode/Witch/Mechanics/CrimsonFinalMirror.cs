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

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal static class CrimsonFinalMirror
{
    private const string Art = TheWitch.Art + "/finisher/";
    private const string Audio = MainFile.ResPath + "/audio/witch/finisher/";
    private const float Duration = 2.4f;
    private const float ShatterTime = 1.6f;
    private static readonly Vector2 Design = new(1920, 1080);
    private static readonly Vector2 MirrorCenter = new(960, 543);
    private static readonly Vector2 WitchCenter = new(535, 550);
    private static readonly Vector2 Palm = new(651, 394);
    private static readonly string[] PosePaths = ["gaze", "raise", "open", "grasp", "fist", "rest"];
    private static readonly ConditionalWeakTable<ICombatState, RoomCue> Cues = new();

    private sealed class RoomCue
    {
        public Creature? Candidate;
        public Texture2D? Silhouette;
        public bool Played;
    }

    internal static IEnumerable<string> Assets =>
        PosePaths.Select(pose => Art + pose + ".webp").Concat(
        [
            Art + "theatre.jpg", Art + "mirror.webp", Art + "moon.webp", Art + "soul.webp",
            Art + "mirror_drape.gdshader", Art + "soul.gdshader", Art + "lunar_eclipse.gdshader",
            Audio + "ritual-swell.wav", Audio + "mirror-shatter.wav"
        ]);

    private static bool CanPresent(ICombatState state) =>
        DisplayServer.GetName() != "headless" && RunManager.Instance.NetService.Type == NetGameType.Singleplayer &&
        state.Players.Any(player => player.Character is TheWitch && LocalContext.IsMe(player));

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
        var body = creature.GetCreatureNode()?.Body;
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
        try
        {
            if (Assets.Any(path => !ResourceLoader.Exists(path)))
            {
                throw new InvalidOperationException("The finisher's resource pack is incomplete.");
            }
            var spirit = cue.Candidate is { IsDead: true } ? cue.Silhouette : null;
            var presentation = new Presentation(layer, spirit);
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
        private readonly ShaderMaterial _eclipse = new() { Shader = Load<Shader>(Art + "lunar_eclipse.gdshader") };
        private readonly ShaderMaterial _spiritInk = new() { Shader = Load<Shader>(Art + "soul.gdshader") };
        private readonly Node2D _shot = new();
        private readonly Node2D _stage = new();
        private readonly Sprite2D _moon;
        private readonly Sprite2D _mirror;
        private readonly Sprite2D _witch;
        private readonly Sprite2D _soul;
        private readonly Texture2D[] _poses = PosePaths.Select(pose => Load<Texture2D>(Art + pose + ".webp")).ToArray();
        private readonly Node2D _cracks;
        private readonly Node2D _sigil;
        private readonly Node2D _filaments;
        private readonly Polygon2D _spark;
        private readonly Node2D _eyeGlow;
        private readonly Node2D _wingCracks;
        private readonly Shard[] _shards;
        private readonly AudioStreamPlayer _swell = LoadSound("ritual-swell", -8);
        private readonly AudioStreamPlayer _shatter = LoadSound("mirror-shatter", -8);
        private bool _impactPlayed;

        private readonly record struct Shard(Polygon2D Node, Vector2 From, Vector2 Travel, float Spin);

        internal Presentation(CanvasLayer layer, Texture2D? spirit)
        {
            _layer = layer;
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
            _layout.AddChild(_stage);
            _stage.AddChild(_shot);
            _layout.Resized += Fit;
            _moon = Painted(Load<Texture2D>(Art + "moon.webp"), 472, new Vector2(960, 338));
            _moon.Material = _eclipse;
            _mirror = Painted(Load<Texture2D>(Art + "mirror.webp"), 557, MirrorCenter);
            _soul = Painted(spirit ?? Load<Texture2D>(Art + "soul.webp"), 322, MirrorCenter);
            _soul.Material = _spiritInk;
            _cracks = MakeCracks();
            _wingCracks = MakeWingCracks();
            _witch = Painted(_poses[0], 561, WitchCenter);
            _eyeGlow = new Node2D { Position = WitchCenter + new Vector2(7, -172) };
            _shot.AddChild(_eyeGlow);
            Stroke(_eyeGlow, new Color("ee416b"), 2.7f, new(-3, 1), new(1, -0.5f), new(4, -2));
            Stroke(_eyeGlow, new Color("ff9bb1"), 1f, new(-2, 0), new(2, -1));
            _sigil = MakeSigil();
            _filaments = MakeFilaments();
            _spark = new Polygon2D
            {
                Position = MirrorCenter,
                Color = new Color("f8c4d9"),
                Polygon = [new(-137, 0), new(-9, -7), new(0, -39), new(9, -7), new(137, 0),
                           new(9, 7), new(0, 39), new(-9, 7)]
            };
            _shot.AddChild(_spark);
            _shards = MakeShards();
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
            var dissolve = Ease(time, 1.85f, Duration);
            var fade = 1 - dissolve;
            var seam = Ease(time, 0.04f, 0.24f) * (1 - Ease(time, 0.72f, 0.83f));
            var release = Ease(time, ShatterTime, 1.74f);
            var flare = Mathf.Sin(release * Mathf.Pi) * fade;
            _shade.Color = new Color(0.018f, 0.012f, 0.041f, 0.76f * Ease(time, 0, 0.28f) * fade);
            _drape.SetShaderParameter("aperture", 0.535f * Ease(time, 0.25f, 0.80f));
            _drape.SetShaderParameter("curtain", seam);
            _drape.SetShaderParameter("fade", fade);
            _drape.SetShaderParameter("elapsed", time);
            _drape.SetShaderParameter("impact", flare);
            _drape.SetShaderParameter("contraction", Ease(time, 1.19f, 1.565f) * (1 - release));
            // An impulse moves only the staged artwork. The game's camera and HUD are untouched.
            _shot.Position = new Vector2(8, -4) * flare;
            var contraction = Ease(time, 1.19f, 1.565f);
            var eclipse = 0.79f * contraction * (1 - Ease(time, 1.68f, 1.98f));
            _eclipse.SetShaderParameter("eclipse", eclipse);
            _moon.Modulate = Colors.White with { A = Ease(time, 0.55f, 0.89f) * fade };
            _moon.Position = new Vector2(960, 338 - 24 * dissolve + 5 * Mathf.Sin(time * 1.6f));
            _mirror.Visible = time < ShatterTime;
            var reveal = Ease(time, 0.58f, 0.92f);
            _mirror.Modulate = Colors.White with { A = reveal };
            _soul.Modulate = Colors.White with { A = reveal * (1 - release) * fade };
            _soul.Position = MirrorCenter + new Vector2(0, Mathf.Sin(time * 6f) * 9 * (1 - contraction));
            _soul.Scale = Vector2.One * FitSprite(_soul.Texture, 217, 307) * Mathf.Lerp(1, 0.08f, contraction);
            _spiritInk.SetShaderParameter("fracture", contraction);
            _wingCracks.Modulate = Colors.White with { A = flare * 0.84f };
            _eyeGlow.Modulate = Colors.White with { A = Ease(time, 0.15f, 0.23f) * (1 - Ease(time, 0.25f, 0.41f)) };
            _cracks.Visible = time < ShatterTime;
            _cracks.Modulate = Colors.White with { A = Ease(time, 1.30f, 1.58f) };
            _cracks.Scale = Vector2.One * Mathf.Lerp(0.15f, 1f, contraction);
            var pose = time switch
            {
                < 0.25f => 0,
                < 0.80f => 1,
                < 1.22f => 2,
                < ShatterTime => 3,
                < 1.86f => 4,
                _ => 5
            };
            if (_witch.Texture != _poses[pose])
            {
                _witch.Texture = _poses[pose];
            }
            _witch.Modulate = Colors.White.Lerp(new Color("ffd3dd"), flare * 0.68f) with
                { A = Mathf.Max(0.12f * seam, Ease(time, 0.41f, 0.61f)) * fade };
            var thread = Ease(time, 0.70f, 0.95f) * (1 - Ease(time, 1.55f, 1.71f));
            _sigil.Modulate = Colors.White with { A = thread };
            _sigil.Rotation = time * -0.72f;
            _sigil.Scale = Vector2.One * Mathf.Lerp(1.06f, 0.42f, contraction);
            _filaments.Modulate = Colors.White with { A = thread * (1 - contraction * 0.44f) };
            _filaments.Scale = new Vector2(1f, 1f - contraction * 0.45f);
            _filaments.Position = MirrorCenter + new Vector2(0, (Palm.Y - MirrorCenter.Y) * contraction * 0.45f);
            _spark.Modulate = Colors.White with { A = Mathf.Max(flare * 0.75f, contraction * 0.55f * (1 - release)) };
            _spark.Scale = Vector2.One * Mathf.Lerp(0.13f, 1.4f, release);
            if (time >= ShatterTime && !_impactPlayed)
            {
                _impactPlayed = true;
                _shatter.Play();
            }

            var burst = Ease(time, ShatterTime, 2.29f);
            foreach (var shard in _shards)
            {
                shard.Node.Visible = _impactPlayed;
                shard.Node.Position = shard.From + shard.Travel * burst + new Vector2(0, 66 * burst * burst);
                shard.Node.Rotation = burst * shard.Spin;
                shard.Node.Modulate = Colors.White with { A = fade * (1 - 0.30f * burst) };
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

        private Node2D MakeCracks()
        {
            var node = new Node2D { Position = MirrorCenter };
            _shot.AddChild(node);
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
            var ring = new Node2D { Position = Palm };
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

        private Node2D MakeFilaments()
        {
            var threads = new Node2D { Position = MirrorCenter };
            _shot.AddChild(threads);
            for (var strand = -1; strand <= 1; strand++)
            {
                var offset = strand * 27;
                var start = Palm - MirrorCenter;
                var line = Enumerable.Range(0, 17).Select(i =>
                {
                    var t = i / 16f;
                    return start.Lerp(new Vector2(-26, offset * 1.3f), t)
                        + new Vector2(0, Mathf.Sin(t * Mathf.Pi) * (28 + offset));
                }).ToArray();
                Stroke(threads, new Color(1f, 0.4f, 0.58f, strand == 0 ? 0.8f : 0.38f), 1.4f, line);
            }
            return threads;
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

        private static float Ease(float time, float start, float end) =>
            Mathf.SmoothStep(0, 1, Mathf.Clamp((time - start) / (end - start), 0, 1));

        private static float FitSprite(Texture2D texture, float width, float height) =>
            Mathf.Min(width / texture.GetWidth(), height / texture.GetHeight());
    }
}
