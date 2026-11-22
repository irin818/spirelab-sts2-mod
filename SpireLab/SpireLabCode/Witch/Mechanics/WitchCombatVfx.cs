using System;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

/// <summary>
/// Cohesive gothic-anime procedural 2D combat VFX for The Witch.
/// Uses antialiased vector geometry locked to the Witch palette (#1C161D, #302832, #EADDE1, #CDD8E2, #78233D, #CF486A).
/// 100% non-blocking (0.28s-0.46s), Retina-crisp, zero Harmony patches, auto-freed cleanly.
/// </summary>
public static partial class WitchCombatVfx
{
    private static readonly Color InkBlack = new("1c161d");
    private static readonly Color Ivory = new("eadde1");
    private static readonly Color CoolSilver = new("cdd8e2");
    private static readonly Color WineRed = new("78233d");
    private static readonly Color GarnetCrimson = new("cf486a");

    internal enum EffectKind
    {
        BlockBarrier,
        ReflectBeam,
        EnlightenRipple,
        EnergySwirl,
        ReserveSwirl,
        DesireFracture,
        MirrorInvert,
        PotionArc,
        PotionAlchemy,
        MirrorSlash,
        MirrorWard,
        GrimoirePages,
        CounterpointResonance
    }

    public static int ActiveEffectCount { get; private set; }
    public static int SpawnedEffectCount { get; private set; }

    public static void SpawnCardCast(Creature witch, Creature? target, string? suit, CardType cardType, bool isCounterpoint)
    {
        if (ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }

        var targetPos = ResolveTargetPos(witch, target, witchPos);
        var isAttack = cardType == CardType.Attack;

        switch (suit)
        {
            case "potion":
                SpawnNode(host, isAttack ? EffectKind.PotionArc : EffectKind.PotionAlchemy,
                    isAttack ? witchPos : witchPos, targetPos, reduced ? 0.30f : 0.38f, reduced, "WitchCastVfx");
                break;
            case "mirror":
                SpawnNode(host, isAttack ? EffectKind.MirrorSlash : EffectKind.MirrorWard,
                    isAttack ? targetPos : witchPos, targetPos, reduced ? 0.28f : 0.36f, reduced, "WitchCastVfx");
                break;
            case "grimoire":
                SpawnNode(host, EffectKind.GrimoirePages,
                    witchPos, isAttack ? targetPos : witchPos, reduced ? 0.32f : 0.42f, reduced, "WitchCastVfx");
                break;
            case "desire":
                // Desire also triggers dedicated WitchFeedback.Desire; keep cast overlay subtle/unified
                break;
            default:
                SpawnNode(host, isAttack ? EffectKind.MirrorSlash : EffectKind.MirrorWard,
                    isAttack ? targetPos : witchPos, targetPos, reduced ? 0.26f : 0.34f, reduced, "WitchCastVfx");
                break;
        }

        if (isCounterpoint)
        {
            SpawnNode(host, EffectKind.CounterpointResonance, witchPos, witchPos, reduced ? 0.34f : 0.44f, reduced, "WitchCounterpointVfx");
        }
    }

    public static void SpawnCoreCue(Creature creature, string? cue, Creature? sourceCreature = null)
    {
        if (DisplayServer.GetName() == "headless" || creature.GetVfxContainer() is not { } host
            || creature.GetCreatureNode() is not { } targetNode)
        {
            return;
        }

        var mode = WitchAnimatedCardArt.Mode;
        var reduced = mode == AnimatedArtMode.Reduced;
        var pos = targetNode.VfxSpawnPosition;
        var srcPos = sourceCreature?.GetCreatureNode()?.VfxSpawnPosition ?? (pos + new Vector2(-280f, 0f));

        // Preserve exact node names expected by existing tests ("WitchBlockFlash" / "WitchMagicFlash")
        var nodeName = cue == null ? "WitchBlockFlash" : "WitchMagicFlash";
        var kind = cue switch
        {
            null => EffectKind.BlockBarrier,
            "reflect" => EffectKind.ReflectBeam,
            "enlighten" => EffectKind.EnlightenRipple,
            "energy" => EffectKind.EnergySwirl,
            "reserve" => EffectKind.ReserveSwirl,
            "desire" => EffectKind.DesireFracture,
            "mirror" => EffectKind.MirrorInvert,
            _ => EffectKind.EnlightenRipple
        };

        var duration = kind switch
        {
            EffectKind.MirrorInvert => reduced ? 0.40f : 0.56f,
            EffectKind.DesireFracture => reduced ? 0.34f : 0.44f,
            EffectKind.ReflectBeam => reduced ? 0.28f : 0.36f,
            _ => reduced ? 0.28f : 0.38f
        };

        var node = SpawnNode(host, kind, kind == EffectKind.ReflectBeam ? srcPos : pos, pos, duration,
            reduced || mode == AnimatedArtMode.Static, nodeName);

        if (cue == null)
        {
            return;
        }

        // Preserve signature ability glyph floating gently at center
        var glyphPath = cue == "energy" ? $"{TheWitch.Art}/witch_energy.svg" : $"{TheWitch.Art}/abilities/{cue}.svg";
        var glyphTex = ResourceLoader.Load<Texture2D>(glyphPath);
        if (glyphTex != null)
        {
            var glyph = new Sprite2D
            {
                Texture = glyphTex,
                Scale = new Vector2(0.46f, 0.46f),
                Position = kind == EffectKind.ReflectBeam ? (pos - srcPos) : Vector2.Zero
            };
            node.AddChild(glyph);
            var gt = glyph.CreateTween().SetParallel();
            gt.TweenProperty(glyph, "scale", new Vector2(0.68f, 0.68f), duration * 0.85f).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            gt.TweenProperty(glyph, "modulate:a", 0f, duration).SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        }

        var audioPath = $"{MainFile.ResPath}/audio/witch/{(cue == "reserve" ? "energy" : cue)}.wav";
        var stream = ResourceLoader.Load<AudioStream>(audioPath);
        if (stream != null)
        {
            var audio = new AudioStreamPlayer { Stream = stream, VolumeDb = -15 };
            host.AddChild(audio);
            audio.Finished += audio.QueueFree;
            audio.Play();
        }
    }

    private static bool ShouldSkipVisuals(Creature witch, out Node host, out Vector2 witchPos, out bool reduced)
    {
        host = null!;
        witchPos = Vector2.Zero;
        reduced = false;
        if (DisplayServer.GetName() == "headless" || WitchAnimatedCardArt.Mode == AnimatedArtMode.Static)
        {
            return true;
        }

        if (witch.GetVfxContainer() is not { } container || witch.GetCreatureNode() is not { } node)
        {
            return true;
        }

        host = container;
        witchPos = node.VfxSpawnPosition;
        reduced = WitchAnimatedCardArt.Mode == AnimatedArtMode.Reduced;
        return false;
    }

    private static Vector2 ResolveTargetPos(Creature witch, Creature? target, Vector2 witchPos)
    {
        if (target?.GetCreatureNode() is { } targetNode)
        {
            return targetNode.VfxSpawnPosition;
        }

        // For AllEnemies / RandomEnemy cards without a single target, aim toward the first alive enemy
        var combat = witch.CombatState;
        if (combat != null)
        {
            foreach (var enemy in combat.Enemies)
            {
                if (enemy.IsAlive && enemy.GetCreatureNode() is { } enemyNode)
                {
                    return enemyNode.VfxSpawnPosition;
                }
            }
        }

        return witchPos + new Vector2(360f, -15f);
    }

    private static WitchVfxNode SpawnNode(Node host, EffectKind kind, Vector2 origin, Vector2 target,
        float duration, bool reduced, string name)
    {
        var vfx = new WitchVfxNode
        {
            Name = name,
            Kind = kind,
            LocalTarget = target - origin,
            Duration = duration,
            Reduced = reduced,
            ZIndex = 12
        };
        host.AddChild(vfx);
        vfx.GlobalPosition = origin;
        SpawnedEffectCount++;
        return vfx;
    }

    private sealed partial class WitchVfxNode : Node2D
    {
        public EffectKind Kind { get; set; }
        public Vector2 LocalTarget { get; set; }
        public float Duration { get; set; } = 0.36f;
        public bool Reduced { get; set; }

        private float _elapsed;

        public override void _EnterTree()
        {
            ActiveEffectCount++;
        }

        public override void _ExitTree()
        {
            ActiveEffectCount = Math.Max(0, ActiveEffectCount - 1);
        }

        public override void _Process(double delta)
        {
            _elapsed += (float)delta;
            if (_elapsed >= Duration)
            {
                QueueFree();
                return;
            }

            QueueRedraw();
        }

        public override void _Draw()
        {
            var t = Mathf.Clamp(_elapsed / Math.Max(0.05f, Duration), 0f, 1f);
            var easeOut = 1f - Mathf.Pow(1f - t, 3f);
            var fade = t < 0.14f ? t / 0.14f : 1f - Mathf.Pow((t - 0.14f) / 0.86f, 1.8f);
            var alpha = Mathf.Clamp(fade, 0f, 1f) * (Reduced ? 0.68f : 1.0f);

            switch (Kind)
            {
                case EffectKind.BlockBarrier:
                    DrawBlockBarrier(t, easeOut, alpha);
                    break;
                case EffectKind.ReflectBeam:
                    DrawReflectBeam(t, easeOut, alpha);
                    break;
                case EffectKind.EnlightenRipple:
                    DrawEnlightenRipple(t, easeOut, alpha);
                    break;
                case EffectKind.EnergySwirl:
                case EffectKind.ReserveSwirl:
                    DrawEnergySwirl(t, easeOut, alpha, Kind == EffectKind.ReserveSwirl);
                    break;
                case EffectKind.DesireFracture:
                    DrawDesireFracture(t, easeOut, alpha);
                    break;
                case EffectKind.MirrorInvert:
                    DrawMirrorInvert(t, easeOut, alpha);
                    break;
                case EffectKind.PotionArc:
                    DrawPotionArc(t, easeOut, alpha);
                    break;
                case EffectKind.PotionAlchemy:
                    DrawPotionAlchemy(t, easeOut, alpha);
                    break;
                case EffectKind.MirrorSlash:
                    DrawMirrorSlash(t, easeOut, alpha);
                    break;
                case EffectKind.MirrorWard:
                    DrawMirrorWard(t, easeOut, alpha);
                    break;
                case EffectKind.GrimoirePages:
                    DrawGrimoirePages(t, easeOut, alpha);
                    break;
                case EffectKind.CounterpointResonance:
                    DrawCounterpointResonance(t, easeOut, alpha);
                    break;
            }
        }

        private void DrawBlockBarrier(float t, float easeOut, float alpha)
        {
            // Curved gothic silver mirror shield arc in front of Witch + faceted lattice + fine glass sparks
            var radius = 58f + easeOut * 34f;
            var offset = new Vector2(24f, -8f);
            DrawArc(offset, radius, -1.05f, 1.05f, 28, WithAlpha(CoolSilver, alpha * 0.92f), 3.6f, true);
            DrawArc(offset, radius - 8f, -0.92f, 0.92f, 24, WithAlpha(Ivory, alpha * 0.65f), 1.8f, true);
            DrawArc(offset, radius + 7f, -0.78f, 0.78f, 20, WithAlpha(GarnetCrimson, alpha * 0.35f), 1.4f, true);

            // Faceted diamond shards along shield arc
            var count = Reduced ? 4 : 6;
            for (var i = 0; i < count; i++)
            {
                var ang = Mathf.Lerp(-0.88f, 0.88f, i / (float)(count - 1));
                var p = offset + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (radius + easeOut * 14f);
                DrawDiamond(p, 7.5f * (1f - t * 0.4f), ang, WithAlpha(CoolSilver, alpha * 0.82f));
            }
        }

        private void DrawReflectBeam(float t, float easeOut, float alpha)
        {
            // Razor-sharp cold-silver + crimson refraction beam from Witch's mirror to attacking enemy + shatter burst
            var dir = LocalTarget.LengthSquared() > 1f ? LocalTarget.Normalized() : Vector2.Right;
            var perp = new Vector2(-dir.Y, dir.X);
            var tip = LocalTarget * Mathf.Clamp(easeOut * 1.45f, 0f, 1f);
            var tail = LocalTarget * Mathf.Clamp((t - 0.18f) * 1.35f, 0f, 0.85f);

            // Outer crimson refraction sheath + inner cold-silver razor core
            DrawLine(tail, tip, WithAlpha(GarnetCrimson, alpha * 0.55f), 9.5f * (1f - t * 0.45f), true);
            DrawLine(tail, tip, WithAlpha(CoolSilver, alpha * 0.96f), 3.6f * (1f - t * 0.3f), true);
            DrawLine(tail + perp * 4f, tip - perp * 2f, WithAlpha(Ivory, alpha * 0.7f), 1.5f, true);

            // Impact mirror shard burst at enemy
            if (t > 0.16f)
            {
                var bt = Mathf.Clamp((t - 0.16f) / 0.84f, 0f, 1f);
                var bEase = 1f - Mathf.Pow(1f - bt, 2.6f);
                var bAlpha = (1f - bt) * (Reduced ? 0.65f : 1.0f);
                DrawPolygonRing(LocalTarget, 18f + bEase * 52f, 6, bt * 0.5f, WithAlpha(CoolSilver, bAlpha * 0.88f), 2.4f);
                var shards = Reduced ? 5 : 9;
                for (var i = 0; i < shards; i++)
                {
                    var a = i * (Mathf.Tau / shards) + 0.35f;
                    var sp = LocalTarget + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (14f + bEase * 68f);
                    DrawDiamond(sp, 9f * (1f - bt * 0.55f), a, WithAlpha(i % 2 == 0 ? CoolSilver : GarnetCrimson, bAlpha * 0.9f));
                }
            }
        }

        private void DrawEnlightenRipple(float t, float easeOut, float alpha)
        {
            var r1 = 26f + easeOut * 66f;
            var r2 = 16f + easeOut * 48f;
            DrawArc(Vector2.Zero, r1, 0f, Mathf.Tau, 36, WithAlpha(Ivory, alpha * 0.82f), 2.4f, true);
            DrawPolygonRing(Vector2.Zero, r2, 4, t * 0.9f, WithAlpha(CoolSilver, alpha * 0.72f), 1.8f);
            var stars = Reduced ? 4 : 6;
            for (var i = 0; i < stars; i++)
            {
                var a = i * (Mathf.Tau / stars) + t * 1.4f;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r1 * 0.85f);
                DrawFourPointStar(p, 6.5f * (1f - t * 0.3f), WithAlpha(i % 2 == 0 ? Ivory : GarnetCrimson, alpha * 0.85f));
            }
        }

        private void DrawEnergySwirl(float t, float easeOut, float alpha, bool reserve)
        {
            var primary = reserve ? Ivory : GarnetCrimson;
            var secondary = reserve ? GarnetCrimson : CoolSilver;
            var r = 24f + easeOut * 54f;
            for (var i = 0; i < 3; i++)
            {
                var start = i * (Mathf.Tau / 3f) + t * 4.2f;
                DrawArc(Vector2.Zero, r - i * 7f, start, start + 1.45f, 18, WithAlpha(i == 0 ? primary : secondary, alpha * 0.85f), 2.8f, true);
            }
            var motes = Reduced ? 4 : 8;
            for (var i = 0; i < motes; i++)
            {
                var a = i * (Mathf.Tau / motes) + t * 3.0f;
                var lift = -easeOut * (28f + (i % 3) * 16f);
                var p = new Vector2(Mathf.Cos(a) * (22f + easeOut * 28f), Mathf.Sin(a) * 14f + lift);
                DrawCircle(p, 3.2f * (1f - t * 0.45f), WithAlpha(i % 2 == 0 ? primary : Ivory, alpha * 0.88f));
            }
        }

        private void DrawDesireFracture(float t, float easeOut, float alpha)
        {
            // Forbidden burgundy-crimson sigil fracture + spiraling dark rose petals
            var r = 34f + easeOut * 86f;
            DrawCircle(Vector2.Zero, r * 0.88f, WithAlpha(WineRed, alpha * 0.16f));
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 40, WithAlpha(GarnetCrimson, alpha * 0.88f), 2.8f, true);
            DrawPolygonRing(Vector2.Zero, r * 0.72f, 6, -t * 0.75f, WithAlpha(WineRed, alpha * 0.85f), 2.2f);

            // Sharp crimson fracture lines radiating outward
            var cracks = Reduced ? 4 : 7;
            for (var i = 0; i < cracks; i++)
            {
                var a = i * (Mathf.Tau / cracks) + 0.22f;
                var inner = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (18f + easeOut * 16f);
                var mid = new Vector2(Mathf.Cos(a + 0.08f), Mathf.Sin(a + 0.08f)) * (36f + easeOut * 44f);
                var outer = new Vector2(Mathf.Cos(a - 0.05f), Mathf.Sin(a - 0.05f)) * (48f + easeOut * 74f);
                DrawLine(inner, mid, WithAlpha(GarnetCrimson, alpha * 0.85f), 2.2f, true);
                DrawLine(mid, outer, WithAlpha(Ivory, alpha * 0.55f), 1.3f, true);
            }

            // Spiraling dark rose petals
            var petals = Reduced ? 4 : 8;
            for (var i = 0; i < petals; i++)
            {
                var a = i * (Mathf.Tau / petals) + t * 2.3f;
                var dist = 24f + easeOut * (62f + (i % 3) * 18f);
                var p = new Vector2(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist * 0.72f - easeOut * 18f);
                DrawDiamond(p, 7.2f * (1f - t * 0.35f), a + t * 3f, WithAlpha(i % 2 == 0 ? GarnetCrimson : WineRed, alpha * 0.88f));
            }
        }

        private void DrawMirrorInvert(float t, float easeOut, float alpha)
        {
            // Dramatic silver-crimson awakening shockwave + vertical reflection rift
            var r = 40f + easeOut * 118f;
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 48, WithAlpha(CoolSilver, alpha * 0.94f), 3.6f, true);
            DrawArc(Vector2.Zero, r * 0.78f, 0f, Mathf.Tau, 40, WithAlpha(GarnetCrimson, alpha * 0.78f), 2.2f, true);
            DrawLine(new Vector2(0f, -r * 1.15f), new Vector2(0f, r * 1.15f), WithAlpha(Ivory, alpha * 0.85f), 2.6f, true);
            DrawPolygonRing(Vector2.Zero, r * 0.92f, 8, t * 0.6f, WithAlpha(CoolSilver, alpha * 0.65f), 1.8f);
        }

        private void DrawPotionArc(float t, float easeOut, float alpha)
        {
            // Graceful curved crimson-silver potion splash arc from Witch to target
            var mid = LocalTarget * 0.5f + new Vector2(0f, -110f);
            var prev = Vector2.Zero;
            var steps = Reduced ? 10 : 16;
            var headT = Mathf.Clamp(easeOut * 1.25f, 0f, 1f);
            var tailT = Mathf.Clamp(headT - 0.42f, 0f, 1f);
            for (var i = 0; i <= steps; i++)
            {
                var u = Mathf.Lerp(tailT, headT, i / (float)steps);
                var p = QuadBezier(Vector2.Zero, mid, LocalTarget, u);
                if (i > 0)
                {
                    var segAlpha = alpha * (i / (float)steps);
                    DrawLine(prev, p, WithAlpha(GarnetCrimson, segAlpha * 0.85f), 5.5f * (i / (float)steps), true);
                    DrawLine(prev, p, WithAlpha(Ivory, segAlpha * 0.65f), 1.8f, true);
                }
                prev = p;
            }

            // Glass splash + droplets on impact
            if (headT > 0.72f)
            {
                var bt = Mathf.Clamp((t - 0.35f) / 0.65f, 0f, 1f);
                var bEase = 1f - Mathf.Pow(1f - bt, 2.2f);
                var bAlpha = (1f - bt) * (Reduced ? 0.65f : 1.0f);
                DrawArc(LocalTarget, 16f + bEase * 48f, 0f, Mathf.Tau, 24, WithAlpha(GarnetCrimson, bAlpha * 0.85f), 2.5f, true);
                var drops = Reduced ? 4 : 7;
                for (var i = 0; i < drops; i++)
                {
                    var a = i * (Mathf.Tau / drops) - 0.4f;
                    var dp = LocalTarget + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (14f + bEase * 54f) + new Vector2(0f, bt * bt * 24f);
                    DrawCircle(dp, 4.2f * (1f - bt * 0.5f), WithAlpha(i % 2 == 0 ? GarnetCrimson : CoolSilver, bAlpha * 0.9f));
                }
            }
        }

        private void DrawPotionAlchemy(float t, float easeOut, float alpha)
        {
            // Gothic alchemy circle + inscribed 6-point hexagram + rising crimson bubbles
            var r = 36f + easeOut * 58f;
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 36, WithAlpha(GarnetCrimson, alpha * 0.85f), 2.4f, true);
            DrawArc(Vector2.Zero, r * 0.86f, 0f, Mathf.Tau, 32, WithAlpha(Ivory, alpha * 0.62f), 1.4f, true);
            DrawPolygonRing(Vector2.Zero, r * 0.82f, 3, t * 0.8f, WithAlpha(CoolSilver, alpha * 0.68f), 1.6f);
            DrawPolygonRing(Vector2.Zero, r * 0.82f, 3, t * 0.8f + Mathf.Pi / 3f, WithAlpha(GarnetCrimson, alpha * 0.72f), 1.6f);

            var bubbles = Reduced ? 4 : 7;
            for (var i = 0; i < bubbles; i++)
            {
                var x = Mathf.Sin(i * 2.1f + t * 3f) * (r * 0.62f);
                var y = 24f - easeOut * (68f + (i % 3) * 20f);
                DrawArc(new Vector2(x, y), 4.8f * (1f - t * 0.35f), 0f, Mathf.Tau, 12, WithAlpha(i % 2 == 0 ? GarnetCrimson : Ivory, alpha * 0.85f), 1.6f, true);
            }
        }

        private void DrawMirrorSlash(float t, float easeOut, float alpha)
        {
            // Razor-sharp diagonal cold-silver crescent mirror slash across target
            var len = 54f + easeOut * 66f;
            var dir = new Vector2(0.82f, -0.57f);
            var perp = new Vector2(-dir.Y, dir.X);
            var a = -dir * len;
            var b = dir * len;
            var curve = perp * (22f * (1f - t * 0.5f));

            var prev = a;
            for (var i = 1; i <= 12; i++)
            {
                var u = i / 12f;
                var p = QuadBezier(a, curve, b, u);
                var taper = Mathf.Sin(u * Mathf.Pi);
                DrawLine(prev, p, WithAlpha(GarnetCrimson, alpha * 0.42f * taper), 9.0f * taper, true);
                DrawLine(prev, p, WithAlpha(CoolSilver, alpha * 0.96f * taper), 3.6f * taper, true);
                DrawLine(prev, p, WithAlpha(Ivory, alpha * 0.92f * taper), 1.5f * taper, true);
                prev = p;
            }

            var shards = Reduced ? 4 : 7;
            for (var i = 0; i < shards; i++)
            {
                var ang = i * (Mathf.Tau / shards) + 0.5f;
                var sp = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (16f + easeOut * 52f);
                DrawDiamond(sp, 7.5f * (1f - t * 0.45f), ang, WithAlpha(i % 2 == 0 ? CoolSilver : Ivory, alpha * 0.85f));
            }
        }

        private void DrawMirrorWard(float t, float easeOut, float alpha)
        {
            // Faceted hexagonal cold-silver mirror ward around the Witch
            var r = 42f + easeOut * 48f;
            DrawPolygonRing(Vector2.Zero, r, 6, t * 0.45f, WithAlpha(CoolSilver, alpha * 0.90f), 2.8f);
            DrawPolygonRing(Vector2.Zero, r * 0.74f, 6, -t * 0.65f, WithAlpha(Ivory, alpha * 0.68f), 1.6f);
            DrawFourPointStar(new Vector2(r * 0.65f, -r * 0.45f), 9f * (1f - t * 0.35f), WithAlpha(Ivory, alpha * 0.92f));
        }

        private void DrawGrimoirePages(float t, float easeOut, float alpha)
        {
            // Swirling ivory parchment silhouettes + silver-ink rune ring
            var r = 34f + easeOut * 56f;
            DrawArc(Vector2.Zero, r, t * 2.5f, t * 2.5f + Mathf.Tau * 0.75f, 30, WithAlpha(Ivory, alpha * 0.78f), 2.0f, true);
            DrawArc(Vector2.Zero, r * 0.78f, -t * 2.0f, -t * 2.0f + Mathf.Tau * 0.65f, 26, WithAlpha(CoolSilver, alpha * 0.68f), 1.5f, true);

            var pages = Reduced ? 4 : 6;
            for (var i = 0; i < pages; i++)
            {
                var ang = i * (Mathf.Tau / pages) + t * 2.2f;
                var p = new Vector2(Mathf.Cos(ang) * (r * 0.9f), Mathf.Sin(ang) * (r * 0.55f) - easeOut * 36f);
                DrawPageCard(p, ang * 0.4f, 11f * (1f - t * 0.25f), WithAlpha(Ivory, alpha * 0.86f), WithAlpha(InkBlack, alpha * 0.55f));
            }

            // If attacking a target, fire a sleek ivory-silver rune dart
            if (LocalTarget.LengthSquared() > 2500f)
            {
                var dart = LocalTarget * Mathf.Clamp(easeOut * 1.35f, 0f, 1f);
                DrawLine(LocalTarget * Mathf.Clamp(t * 0.7f, 0f, 0.7f), dart, WithAlpha(Ivory, alpha * 0.88f), 3.0f, true);
                DrawFourPointStar(dart, 10f * (1f - t * 0.35f), WithAlpha(CoolSilver, alpha * 0.95f));
            }
        }

        private void DrawCounterpointResonance(float t, float easeOut, float alpha)
        {
            // Unmistakable twin-phase Counterpoint visual:
            // Phase 1 (immediate): crisp cold-silver harmonic ring + twin silver crescents
            var r1 = 38f + easeOut * 74f;
            DrawArc(Vector2.Zero, r1, 0f, Mathf.Tau, 40, WithAlpha(CoolSilver, alpha * 0.92f), 2.8f, true);
            DrawArc(Vector2.Zero, r1 + 6f, t * 2.2f, t * 2.2f + 1.1f, 18, WithAlpha(Ivory, alpha * 0.88f), 3.6f, true);
            DrawArc(Vector2.Zero, r1 + 6f, t * 2.2f + Mathf.Pi, t * 2.2f + Mathf.Pi + 1.1f, 18, WithAlpha(Ivory, alpha * 0.88f), 3.6f, true);

            // Phase 2 (+0.16 normalized delay): counter-rotating garnet-crimson outer resonance ring
            if (t > 0.16f)
            {
                var t2 = Mathf.Clamp((t - 0.16f) / 0.84f, 0f, 1f);
                var e2 = 1f - Mathf.Pow(1f - t2, 2.8f);
                var a2 = (1f - t2) * (Reduced ? 0.68f : 0.96f);
                var r2 = 44f + e2 * 94f;
                DrawArc(Vector2.Zero, r2, 0f, Mathf.Tau, 44, WithAlpha(GarnetCrimson, a2 * 0.90f), 3.0f, true);
                DrawArc(Vector2.Zero, r2 - 6f, -t2 * 2.6f + 0.6f, -t2 * 2.6f + 1.7f, 18, WithAlpha(GarnetCrimson, a2), 4.0f, true);
                DrawArc(Vector2.Zero, r2 - 6f, -t2 * 2.6f + 0.6f + Mathf.Pi, -t2 * 2.6f + 1.7f + Mathf.Pi, 18, WithAlpha(GarnetCrimson, a2), 4.0f, true);
            }
        }

        private void DrawPolygonRing(Vector2 center, float radius, int sides, float rot, Color color, float width)
        {
            var pts = new Vector2[sides + 1];
            for (var i = 0; i <= sides; i++)
            {
                var a = rot + i * (Mathf.Tau / sides);
                pts[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            DrawPolyline(pts, color, width, true);
        }

        private void DrawDiamond(Vector2 center, float size, float rot, Color color)
        {
            var ca = Mathf.Cos(rot);
            var sa = Mathf.Sin(rot);
            Vector2 Rot(float x, float y) => center + new Vector2(x * ca - y * sa, x * sa + y * ca);
            DrawColoredPolygon([Rot(size, 0f), Rot(0f, size * 0.48f), Rot(-size, 0f), Rot(0f, -size * 0.48f)], color);
        }

        private void DrawFourPointStar(Vector2 center, float size, Color color)
        {
            var inner = size * 0.28f;
            DrawColoredPolygon(
            [
                center + new Vector2(0f, -size),
                center + new Vector2(inner, -inner),
                center + new Vector2(size, 0f),
                center + new Vector2(inner, inner),
                center + new Vector2(0f, size),
                center + new Vector2(-inner, inner),
                center + new Vector2(-size, 0f),
                center + new Vector2(-inner, -inner)
            ], color);
        }

        private void DrawPageCard(Vector2 center, float rot, float size, Color fill, Color border)
        {
            var ca = Mathf.Cos(rot);
            var sa = Mathf.Sin(rot);
            var w = size * 0.65f;
            var h = size;
            Vector2 Rot(float x, float y) => center + new Vector2(x * ca - y * sa, x * sa + y * ca);
            var pts = new[] { Rot(-w, -h), Rot(w, -h), Rot(w, h), Rot(-w, h) };
            DrawColoredPolygon(pts, fill);
            DrawPolyline([pts[0], pts[1], pts[2], pts[3], pts[0]], border, 1.2f, true);
        }

        private static Vector2 QuadBezier(Vector2 p0, Vector2 p1, Vector2 p2, float u)
        {
            var inv = 1f - u;
            return inv * inv * p0 + 2f * inv * u * p1 + u * u * p2;
        }

        private static Color WithAlpha(Color c, float a) => new(c.R, c.G, c.B, Mathf.Clamp(a, 0f, 1f));
    }
}
