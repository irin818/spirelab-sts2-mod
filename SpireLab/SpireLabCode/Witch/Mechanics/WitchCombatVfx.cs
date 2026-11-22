using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

/// <summary>
/// Crimson Mirror Nocturne (V2.1) unified non-blocking combat VFX director and audio budget manager.
/// - Palette locked strictly to The Witch gothic colors (#1C161D, #302832, #EADDE1, #CDD8E2, #78233D, #CF486A).
/// - Strict concurrency caps (P0 HUD always unobstructed; P3 ornaments drop gracefully under extreme burst).
/// - Per-cue audio cooldown & active voice budget so rapid Counterpoint/Gamble loops never clip or stack.
/// - Zero Harmony patches, zero gameplay mutation, instant cleanup on combat end.
/// </summary>
public static partial class WitchCombatVfx
{
    private static readonly Color InkBlack = new("1c161d");
    private static readonly Color Charcoal = new("302832");
    private static readonly Color Ivory = new("eadde1");
    private static readonly Color CoolSilver = new("cdd8e2");
    private static readonly Color WineRed = new("78233d");
    private static readonly Color GarnetCrimson = new("cf486a");

    private const int MaxActiveFull = 18;
    private const int MaxActiveReduced = 8;
    private const int MaxConcurrentAudio = 5;
    private const double MinAudioCooldownSec = 0.065;
    private const double DrawCoalesceSec = 0.18;
    private const double DiscardCoalesceSec = 0.14;
    private const double ExhaustCoalesceSec = 0.12;
    private const double GambleRapidWindowSec = 0.55;

    internal static readonly string[] AudioAssets =
    [
        $"{MainFile.ResPath}/audio/witch/vfx/potion_throw.wav",
        $"{MainFile.ResPath}/audio/witch/vfx/potion_burst.wav",
        $"{MainFile.ResPath}/audio/witch/vfx/mirror_slash.wav",
        $"{MainFile.ResPath}/audio/witch/vfx/grimoire_page.wav",
        $"{MainFile.ResPath}/audio/witch/vfx/counterpoint_chime.wav",
        $"{MainFile.ResPath}/audio/witch/vfx/exhaust_ash.wav",
        $"{MainFile.ResPath}/audio/witch/vfx/gamble_siphon.wav"
    ];

    internal enum EffectKind
    {
        BlockBarrier,
        SmallBlockGlint,
        ReflectBeam,
        EnlightenRipple,
        EnergySwirl,
        ReserveSwirl,
        DesireFracture,
        MirrorInvert,
        PotionVialThrow,
        PotionAlchemy,
        ConcentratedFlameJet,
        BoilingEchoHit,
        FinalDistillationCrystal,
        SilverScreenFold,
        MirrorMazeSanctuary,
        MirrorSlash,
        MirrorCageBlade,
        MirrorWard,
        RazorPagesFan,
        GrimoirePages,
        PurifyTransmute,
        PrimalGreedSwirl,
        SlothVeil,
        CalamityShards,
        LifeSiphon,
        CounterpointRelay,
        CardDrawFlutter,
        CardDiscardInk,
        CardExhaustAsh
    }

    private static readonly HashSet<WitchVfxNode> LiveNodes = [];
    private static readonly Dictionary<string, double> LastAudioSec = new(StringComparer.Ordinal);
    private static int _activeAudioVoices;

    // Semantic visual relay & burst state
    private static Color _lastRelayColor = CoolSilver;
    private static double _lastRelaySec = -10.0;
    private static double _lastDrawSec = -10.0;
    private static double _lastDiscardSec = -10.0;
    private static double _lastExhaustSec = -10.0;
    private static double _lastGambleSec = -10.0;
    private static int _gambleBurstCount;
    private static string _activeMultiHitSpec = "";
    private static int _activeMultiHitIndex;

    public static int ActiveEffectCount => LiveNodes.Count;
    public static int PeakActiveEffects { get; private set; }
    public static int SpawnedEffectCount { get; private set; }
    public static int DroppedDecorativeCount { get; private set; }
    public static int AudioPlayedCount { get; private set; }
    public static int AudioThrottledCount { get; private set; }

    public static void ResetTelemetry()
    {
        PeakActiveEffects = ActiveEffectCount;
        SpawnedEffectCount = 0;
        DroppedDecorativeCount = 0;
        AudioPlayedCount = 0;
        AudioThrottledCount = 0;
    }

    public static void ClearTransientCombatEffects()
    {
        foreach (var node in LiveNodes.ToArray())
        {
            if (GodotObject.IsInstanceValid(node))
            {
                node.QueueFree();
            }
        }
        LiveNodes.Clear();
        _lastRelaySec = -10.0;
        _gambleBurstCount = 0;
        _activeMultiHitSpec = "";
        _activeMultiHitIndex = 0;
    }

    public static void SpawnCardCast(Creature witch, Creature? target, WitchCard card, string? suit, bool isCounterpoint)
    {
        if (ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }

        var targetPos = ResolveTargetPos(witch, target, witchPos);
        var isAttack = card.Type == CardType.Attack;
        var nextRelayColor = SuitColor(suit);

        // Reset multi-hit escalation counter for U10 BoilingEcho / R16 MirrorCage
        _activeMultiHitSpec = card.SpecId;
        _activeMultiHitIndex = 0;

        // Signature card routing (Sections 6-9) with reusable fallbacks for all other cards
        switch (card.SpecId)
        {
            case "C01": // Potion Toss: spinning crimson glass vial parabola -> shatter splash
                SpawnNode(host, EffectKind.PotionVialThrow, witchPos, targetPos, reduced ? 0.28f : 0.36f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: GarnetCrimson);
                PlayMaterialAudio(host, "vfx/potion_throw", -14f);
                break;

            case "C06": // Concentrated Flame: compressed crimson elixir jet
                SpawnNode(host, EffectKind.ConcentratedFlameJet, witchPos, targetPos, reduced ? 0.26f : 0.34f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: GarnetCrimson);
                PlayMaterialAudio(host, "vfx/potion_burst", -14f);
                break;

            case "R08": // Final Distillation: crimson threads -> dense garnet crystal -> implosion shatter
                SpawnNode(host, EffectKind.FinalDistillationCrystal, witchPos, targetPos, reduced ? 0.38f : 0.50f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: GarnetCrimson);
                PlayMaterialAudio(host, "vfx/potion_burst", -12.5f);
                break;

            case "U10": // Boiling Echo: hits are driven individually by actual AfterDamageGiven events!
                SpawnNode(host, EffectKind.PotionAlchemy, witchPos, witchPos, reduced ? 0.22f : 0.28f,
                    reduced, priority: 2, "WitchCastVfx", primaryColor: GarnetCrimson);
                PlayMaterialAudio(host, "vfx/potion_throw", -16f);
                break;

            case "C11": // Silver Screen: unfolding translucent gothic silver mirror plane
                SpawnNode(host, EffectKind.SilverScreenFold, witchPos, witchPos, reduced ? 0.28f : 0.36f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: CoolSilver);
                PlayMaterialAudio(host, "vfx/mirror_slash", -16f);
                break;

            case "U18": // Mirror Maze: staggered multi-plane silver mirror sanctuary
                SpawnNode(host, EffectKind.MirrorMazeSanctuary, witchPos, witchPos, reduced ? 0.36f : 0.48f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: CoolSilver);
                PlayMaterialAudio(host, "vfx/mirror_slash", -14.5f);
                break;

            case "R16": // Mirror Cage: two opposing blades driven by actual hits in AfterDamageGiven
                SpawnNode(host, EffectKind.MirrorWard, witchPos, witchPos, reduced ? 0.22f : 0.28f,
                    reduced, priority: 2, "WitchCastVfx", primaryColor: CoolSilver);
                break;

            case "C19": // Razor Pages: translucent pages fan -> crisp silver-ink blade
                SpawnNode(host, EffectKind.RazorPagesFan, witchPos, targetPos, reduced ? 0.28f : 0.36f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: Ivory);
                PlayMaterialAudio(host, "vfx/grimoire_page", -14f);
                break;

            case "U37": // Primal Greed: translucent mirror shimmer + swirling phantom card silhouettes
                SpawnNode(host, EffectKind.PrimalGreedSwirl, witchPos, witchPos, reduced ? 0.38f : 0.52f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: GarnetCrimson);
                break;

            case "U38": // Sloth: closing silver mirror panes + tranquil cold-silver veil (non-blocking!)
                SpawnNode(host, EffectKind.SlothVeil, witchPos, witchPos, reduced ? 0.34f : 0.44f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: CoolSilver);
                break;

            case "R26": // Source of Calamity: silver mirror fracturing into 3 irregular crimson-marked shards
                SpawnNode(host, EffectKind.CalamityShards, witchPos, witchPos, reduced ? 0.36f : 0.48f,
                    reduced, priority: 1, "WitchCastVfx", primaryColor: GarnetCrimson);
                break;

            case "R27": // Abyss of Gambling: life siphon is driven strictly by real WitchLife.Pay!
                break;

            default:
                SpawnGenericSuitCast(host, witchPos, targetPos, suit, isAttack, reduced);
                break;
        }

        // Section 10: Continuous Counterpoint visual relay connecting previous spell color -> new spell color
        var now = NowSec();
        if (isCounterpoint)
        {
            var prevColor = (now - _lastRelaySec) < 8.0 ? _lastRelayColor : CoolSilver;
            SpawnNode(host, EffectKind.CounterpointRelay, witchPos, witchPos, reduced ? 0.30f : 0.40f,
                reduced, priority: 2, "WitchCounterpointVfx", primaryColor: prevColor, secondaryColor: nextRelayColor);
            PlayMaterialAudio(host, "vfx/counterpoint_chime", -13.5f);
        }

        _lastRelayColor = nextRelayColor;
        _lastRelaySec = now;
    }

    public static void SpawnAttackHit(Creature witch, Creature target, WitchCard card)
    {
        if (ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced)
            || !target.IsAlive && card.SpecId is not ("U10" or "R16"))
        {
            return;
        }

        var targetPos = target.GetCreatureNode()?.VfxSpawnPosition ?? (witchPos + new Vector2(360f, -15f));
        if (card.SpecId != _activeMultiHitSpec)
        {
            _activeMultiHitSpec = card.SpecId;
            _activeMultiHitIndex = 0;
        }
        var hitIndex = _activeMultiHitIndex++;

        if (card.SpecId == "U10") // Boiling Echo: Hit 1 condense, Hit 2 splash burst, Hit 3 intensified rupture
        {
            var dur = hitIndex >= 2 ? (reduced ? 0.30f : 0.40f) : (reduced ? 0.22f : 0.28f);
            SpawnNode(host, EffectKind.BoilingEchoHit, targetPos, witchPos, dur,
                reduced, priority: 1, "WitchHitVfx", primaryColor: GarnetCrimson, variantIndex: hitIndex);
            PlayMaterialAudio(host, hitIndex >= 2 ? "vfx/potion_burst" : "vfx/potion_throw", hitIndex >= 2 ? -12.5f : -16f);
        }
        else if (card.SpecId == "R16") // Mirror Cage: opposing alternating diagonal silver blades
        {
            SpawnNode(host, EffectKind.MirrorCageBlade, targetPos, targetPos, reduced ? 0.24f : 0.32f,
                reduced, priority: 1, "WitchHitVfx", primaryColor: CoolSilver, variantIndex: hitIndex);
            PlayMaterialAudio(host, "vfx/mirror_slash", -14f);
        }
    }

    public static void SpawnLifePaid(Creature witch, int actualPaid, bool isGamble)
    {
        if (actualPaid <= 0 || ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }

        var now = NowSec();
        if (isGamble && (now - _lastGambleSec) < GambleRapidWindowSec)
        {
            _gambleBurstCount++;
        }
        else
        {
            _gambleBurstCount = 0;
        }
        _lastGambleSec = now;

        // Auto-shorten under rapid Gamble chains so visual polish NEVER drags rapid loops
        var fastFactor = _gambleBurstCount >= 2 ? 0.62f : 1.0f;
        var dur = (reduced ? 0.24f : 0.32f) * fastFactor;
        var hpOrigin = witchPos + new Vector2(0f, 118f);
        SpawnNode(host, EffectKind.LifeSiphon, hpOrigin, witchPos + new Vector2(18f, -12f), dur,
            reduced || _gambleBurstCount >= 2, priority: 1, "WitchLifeSiphonVfx",
            primaryColor: GarnetCrimson, secondaryColor: Ivory, variantIndex: Math.Min(actualPaid, 30));
        PlayMaterialAudio(host, "vfx/gamble_siphon", _gambleBurstCount >= 2 ? -18f : -14.5f);
    }

    public static void SpawnPurifyTransmute(Creature witch)
    {
        if (ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }

        SpawnNode(host, EffectKind.PurifyTransmute, witchPos + new Vector2(28f, -18f), witchPos, reduced ? 0.32f : 0.44f,
            reduced, priority: 2, "WitchPurifyVfx", primaryColor: GarnetCrimson, secondaryColor: Ivory);
        PlayMaterialAudio(host, "vfx/exhaust_ash", -14.5f);
    }

    public static void SpawnCardDraw(Creature witch)
    {
        var now = NowSec();
        if ((now - _lastDrawSec) < DrawCoalesceSec || ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }
        _lastDrawSec = now;
        SpawnNode(host, EffectKind.CardDrawFlutter, witchPos + new Vector2(-12f, -30f), witchPos + new Vector2(60f, 45f),
            reduced ? 0.20f : 0.28f, reduced, priority: 3, "WitchDrawVfx", primaryColor: Ivory);
        PlayMaterialAudio(host, "vfx/grimoire_page", -21f);
    }

    public static void SpawnCardDiscard(Creature witch)
    {
        var now = NowSec();
        if ((now - _lastDiscardSec) < DiscardCoalesceSec || ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }
        _lastDiscardSec = now;
        SpawnNode(host, EffectKind.CardDiscardInk, witchPos + new Vector2(16f, -12f), witchPos + new Vector2(36f, 64f),
            reduced ? 0.20f : 0.26f, reduced, priority: 3, "WitchDiscardVfx", primaryColor: Charcoal);
    }

    public static void SpawnCardExhaust(Creature witch)
    {
        var now = NowSec();
        if ((now - _lastExhaustSec) < ExhaustCoalesceSec || ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }
        _lastExhaustSec = now;
        SpawnNode(host, EffectKind.CardExhaustAsh, witchPos + new Vector2(18f, -24f), witchPos,
            reduced ? 0.24f : 0.34f, reduced, priority: 2, "WitchExhaustVfx", primaryColor: GarnetCrimson, secondaryColor: Charcoal);
        PlayMaterialAudio(host, "vfx/exhaust_ash", -16f);
    }

    public static void SpawnSmallBlockGlint(Creature witch)
    {
        if (ShouldSkipVisuals(witch, out var host, out var witchPos, out var reduced))
        {
            return;
        }

        SpawnNode(host, EffectKind.SmallBlockGlint, witchPos, witchPos, reduced ? 0.18f : 0.24f,
            reduced, priority: 3, "WitchBlockGlintVfx", primaryColor: CoolSilver);
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
            EffectKind.ReflectBeam => reduced ? 0.24f : 0.32f,
            _ => reduced ? 0.26f : 0.36f
        };

        // Core cues (Block, Reflect, Desire, Mirror) are P1 and bypass P3 dropping
        var node = SpawnNode(host, kind, kind == EffectKind.ReflectBeam ? srcPos : pos, pos, duration,
            reduced || mode == AnimatedArtMode.Static, priority: 1, nodeName);
        if (node == null)
        {
            return;
        }

        if (cue == null)
        {
            return;
        }

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

        PlayMaterialAudio(host, cue == "reserve" ? "energy" : cue, -15f);
    }

    private static void SpawnGenericSuitCast(Node host, Vector2 witchPos, Vector2 targetPos, string? suit, bool isAttack, bool reduced)
    {
        switch (suit)
        {
            case "potion":
                SpawnNode(host, isAttack ? EffectKind.PotionVialThrow : EffectKind.PotionAlchemy,
                    witchPos, targetPos, reduced ? 0.26f : 0.34f, reduced, priority: 2, "WitchCastVfx", primaryColor: GarnetCrimson);
                PlayMaterialAudio(host, isAttack ? "vfx/potion_throw" : "vfx/potion_burst", -16f);
                break;
            case "mirror":
                SpawnNode(host, isAttack ? EffectKind.MirrorSlash : EffectKind.MirrorWard,
                    isAttack ? targetPos : witchPos, targetPos, reduced ? 0.24f : 0.32f, reduced, priority: 2, "WitchCastVfx", primaryColor: CoolSilver);
                PlayMaterialAudio(host, "vfx/mirror_slash", -16f);
                break;
            case "grimoire":
                SpawnNode(host, EffectKind.GrimoirePages, witchPos, isAttack ? targetPos : witchPos,
                    reduced ? 0.28f : 0.36f, reduced, priority: 2, "WitchCastVfx", primaryColor: Ivory);
                PlayMaterialAudio(host, "vfx/grimoire_page", -16f);
                break;
            case "desire":
                // Dedicated WitchFeedback.Desire handles DesireFracture + audio
                break;
            default:
                SpawnNode(host, isAttack ? EffectKind.MirrorSlash : EffectKind.MirrorWard,
                    isAttack ? targetPos : witchPos, targetPos, reduced ? 0.22f : 0.30f, reduced, priority: 2, "WitchCastVfx", primaryColor: CoolSilver);
                break;
        }
    }

    private static void PlayMaterialAudio(Node host, string cueKey, float baseVolumeDb)
    {
        if (DisplayServer.GetName() == "headless")
        {
            return;
        }

        var now = NowSec();
        if (_activeAudioVoices >= MaxConcurrentAudio
            || (LastAudioSec.TryGetValue(cueKey, out var prev) && (now - prev) < MinAudioCooldownSec))
        {
            AudioThrottledCount++;
            return;
        }

        var path = $"{MainFile.ResPath}/audio/witch/{cueKey}.wav";
        var stream = ResourceLoader.Load<AudioStream>(path);
        if (stream == null)
        {
            return;
        }

        LastAudioSec[cueKey] = now;
        _activeAudioVoices++;
        AudioPlayedCount++;

        var pitch = 0.96f + (float)((AudioPlayedCount * 37) % 10) * 0.01f;
        var duck = _activeAudioVoices > 2 ? -2.5f : 0f;
        var audio = new AudioStreamPlayer
        {
            Stream = stream,
            VolumeDb = baseVolumeDb + duck,
            PitchScale = pitch
        };
        host.AddChild(audio);
        audio.Finished += () =>
        {
            _activeAudioVoices = Math.Max(0, _activeAudioVoices - 1);
            if (GodotObject.IsInstanceValid(audio))
            {
                audio.QueueFree();
            }
        };
        audio.TreeExited += () => _activeAudioVoices = Math.Max(0, _activeAudioVoices - 1);
        audio.Play();
    }

    private static Color SuitColor(string? suit) => suit switch
    {
        "potion" => GarnetCrimson,
        "mirror" => CoolSilver,
        "grimoire" => Ivory,
        "desire" => WineRed,
        _ => CoolSilver
    };

    private static double NowSec() => Time.GetTicksUsec() / 1_000_000.0;

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
        if (target is { IsAlive: true } && target.GetCreatureNode() is { } targetNode)
        {
            return targetNode.VfxSpawnPosition;
        }

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

    private static WitchVfxNode? SpawnNode(Node host, EffectKind kind, Vector2 origin, Vector2 target,
        float duration, bool reduced, int priority, string name,
        Color? primaryColor = null, Color? secondaryColor = null, int variantIndex = 0)
    {
        var cap = reduced ? MaxActiveReduced : MaxActiveFull;
        if (LiveNodes.Count >= cap && priority >= 2)
        {
            DroppedDecorativeCount++;
            return null;
        }

        var vfx = new WitchVfxNode
        {
            Name = name,
            Kind = kind,
            LocalTarget = target - origin,
            Duration = duration,
            Reduced = reduced,
            PrimaryColor = primaryColor ?? CoolSilver,
            SecondaryColor = secondaryColor ?? GarnetCrimson,
            VariantIndex = variantIndex,
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
        public float Duration { get; set; } = 0.34f;
        public bool Reduced { get; set; }
        public Color PrimaryColor { get; set; } = CoolSilver;
        public Color SecondaryColor { get; set; } = GarnetCrimson;
        public int VariantIndex { get; set; }

        private float _elapsed;

        public override void _EnterTree()
        {
            LiveNodes.Add(this);
            if (LiveNodes.Count > PeakActiveEffects)
            {
                PeakActiveEffects = LiveNodes.Count;
            }
        }

        public override void _ExitTree()
        {
            LiveNodes.Remove(this);
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
                case EffectKind.BlockBarrier: DrawBlockBarrier(t, easeOut, alpha); break;
                case EffectKind.SmallBlockGlint: DrawSmallBlockGlint(t, easeOut, alpha); break;
                case EffectKind.ReflectBeam: DrawReflectBeam(t, easeOut, alpha); break;
                case EffectKind.EnlightenRipple: DrawEnlightenRipple(t, easeOut, alpha); break;
                case EffectKind.EnergySwirl:
                case EffectKind.ReserveSwirl: DrawEnergySwirl(t, easeOut, alpha, Kind == EffectKind.ReserveSwirl); break;
                case EffectKind.DesireFracture: DrawDesireFracture(t, easeOut, alpha); break;
                case EffectKind.MirrorInvert: DrawMirrorInvert(t, easeOut, alpha); break;
                case EffectKind.PotionVialThrow: DrawPotionVialThrow(t, easeOut, alpha); break;
                case EffectKind.PotionAlchemy: DrawPotionAlchemy(t, easeOut, alpha); break;
                case EffectKind.ConcentratedFlameJet: DrawConcentratedFlameJet(t, easeOut, alpha); break;
                case EffectKind.BoilingEchoHit: DrawBoilingEchoHit(t, easeOut, alpha); break;
                case EffectKind.FinalDistillationCrystal: DrawFinalDistillationCrystal(t, easeOut, alpha); break;
                case EffectKind.SilverScreenFold: DrawSilverScreenFold(t, easeOut, alpha); break;
                case EffectKind.MirrorMazeSanctuary: DrawMirrorMazeSanctuary(t, easeOut, alpha); break;
                case EffectKind.MirrorSlash: DrawMirrorSlash(t, easeOut, alpha, 1f); break;
                case EffectKind.MirrorCageBlade: DrawMirrorSlash(t, easeOut, alpha, VariantIndex % 2 == 0 ? 1f : -1f); break;
                case EffectKind.MirrorWard: DrawMirrorWard(t, easeOut, alpha); break;
                case EffectKind.RazorPagesFan: DrawRazorPagesFan(t, easeOut, alpha); break;
                case EffectKind.GrimoirePages: DrawGrimoirePages(t, easeOut, alpha); break;
                case EffectKind.PurifyTransmute: DrawPurifyTransmute(t, easeOut, alpha); break;
                case EffectKind.PrimalGreedSwirl: DrawPrimalGreedSwirl(t, easeOut, alpha); break;
                case EffectKind.SlothVeil: DrawSlothVeil(t, easeOut, alpha); break;
                case EffectKind.CalamityShards: DrawCalamityShards(t, easeOut, alpha); break;
                case EffectKind.LifeSiphon: DrawLifeSiphon(t, easeOut, alpha); break;
                case EffectKind.CounterpointRelay: DrawCounterpointRelay(t, easeOut, alpha); break;
                case EffectKind.CardDrawFlutter: DrawCardDrawFlutter(t, easeOut, alpha); break;
                case EffectKind.CardDiscardInk: DrawCardDiscardInk(t, easeOut, alpha); break;
                case EffectKind.CardExhaustAsh: DrawCardExhaustAsh(t, easeOut, alpha); break;
            }
        }

        private void DrawBlockBarrier(float t, float easeOut, float alpha)
        {
            var radius = 58f + easeOut * 34f;
            var offset = new Vector2(24f, -8f);
            DrawArc(offset, radius, -1.05f, 1.05f, 28, WithAlpha(CoolSilver, alpha * 0.92f), 3.6f, true);
            DrawArc(offset, radius - 8f, -0.92f, 0.92f, 24, WithAlpha(Ivory, alpha * 0.65f), 1.8f, true);
            DrawArc(offset, radius + 7f, -0.78f, 0.78f, 20, WithAlpha(GarnetCrimson, alpha * 0.35f), 1.4f, true);
            var count = Reduced ? 4 : 6;
            for (var i = 0; i < count; i++)
            {
                var ang = Mathf.Lerp(-0.88f, 0.88f, i / (float)(count - 1));
                var p = offset + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (radius + easeOut * 14f);
                DrawDiamond(p, 7.5f * (1f - t * 0.4f), ang, WithAlpha(CoolSilver, alpha * 0.82f));
            }
        }

        private void DrawSmallBlockGlint(float t, float easeOut, float alpha)
        {
            var offset = new Vector2(46f, -12f);
            var r = 24f + easeOut * 22f;
            DrawArc(offset, r, -0.75f, 0.75f, 16, WithAlpha(CoolSilver, alpha * 0.82f), 2.2f, true);
            DrawFourPointStar(offset + new Vector2(r * 0.85f, -r * 0.3f), 7f * (1f - t * 0.4f), WithAlpha(Ivory, alpha * 0.85f));
        }

        private void DrawReflectBeam(float t, float easeOut, float alpha)
        {
            var dir = LocalTarget.LengthSquared() > 1f ? LocalTarget.Normalized() : Vector2.Right;
            var perp = new Vector2(-dir.Y, dir.X);
            var tip = LocalTarget * Mathf.Clamp(easeOut * 1.45f, 0f, 1f);
            var tail = LocalTarget * Mathf.Clamp((t - 0.18f) * 1.35f, 0f, 0.85f);

            // Mirror crack flash at origin before refraction fires
            if (t < 0.35f)
            {
                var cAlpha = (1f - t / 0.35f) * alpha;
                DrawDiamond(Vector2.Zero, 14f + t * 24f, 0.4f, WithAlpha(CoolSilver, cAlpha * 0.65f));
            }

            DrawLine(tail, tip, WithAlpha(GarnetCrimson, alpha * 0.55f), 9.5f * (1f - t * 0.45f), true);
            DrawLine(tail, tip, WithAlpha(CoolSilver, alpha * 0.96f), 3.6f * (1f - t * 0.3f), true);
            DrawLine(tail + perp * 4f, tip - perp * 2f, WithAlpha(Ivory, alpha * 0.7f), 1.5f, true);

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

        private void DrawPotionVialThrow(float t, float easeOut, float alpha)
        {
            // C01 Signature: Spinning glass alchemy bottle flying along smooth curve -> crisp shatter & crimson splash
            var mid = LocalTarget * 0.5f + new Vector2(0f, -125f);
            var flightT = Mathf.Clamp(t / 0.58f, 0f, 1f);
            var tailT = Mathf.Clamp(flightT - 0.38f, 0f, 1f);

            // Crimson liquid ribbon trail
            var prev = Vector2.Zero;
            var steps = Reduced ? 8 : 14;
            for (var i = 0; i <= steps; i++)
            {
                var u = Mathf.Lerp(tailT, flightT, i / (float)steps);
                var p = QuadBezier(Vector2.Zero, mid, LocalTarget, u);
                if (i > 0)
                {
                    var segA = alpha * (i / (float)steps) * (1f - Mathf.Clamp((t - 0.58f) / 0.42f, 0f, 1f));
                    DrawLine(prev, p, WithAlpha(GarnetCrimson, segA * 0.85f), 5.0f * (i / (float)steps), true);
                    DrawLine(prev, p, WithAlpha(Ivory, segA * 0.55f), 1.5f, true);
                }
                prev = p;
            }

            // Spinning alchemy vial in flight (t < 0.62)
            if (t < 0.62f)
            {
                var pos = QuadBezier(Vector2.Zero, mid, LocalTarget, flightT);
                var spin = flightT * Mathf.Tau * 1.4f;
                DrawAlchemyVial(pos, spin, 13.5f, alpha);
            }

            // Crisp glass shatter + crimson splash droplets on arrival (t >= 0.48)
            if (t >= 0.48f)
            {
                var bt = Mathf.Clamp((t - 0.48f) / 0.52f, 0f, 1f);
                var bEase = 1f - Mathf.Pow(1f - bt, 2.5f);
                var bAlpha = (1f - bt) * (Reduced ? 0.68f : 1.0f);
                DrawArc(LocalTarget, 14f + bEase * 54f, 0f, Mathf.Tau, 28, WithAlpha(GarnetCrimson, bAlpha * 0.88f), 2.6f, true);
                var shards = Reduced ? 5 : 9;
                for (var i = 0; i < shards; i++)
                {
                    var a = i * (Mathf.Tau / shards) - 0.35f;
                    var dist = 14f + bEase * (56f + (i % 3) * 14f);
                    var sp = LocalTarget + new Vector2(Mathf.Cos(a) * dist, Mathf.Sin(a) * dist + bt * bt * 26f);
                    if (i % 2 == 0)
                    {
                        DrawCircle(sp, 4.4f * (1f - bt * 0.45f), WithAlpha(GarnetCrimson, bAlpha * 0.92f));
                    }
                    else
                    {
                        DrawDiamond(sp, 7.2f * (1f - bt * 0.45f), a + bt * 2f, WithAlpha(CoolSilver, bAlpha * 0.9f));
                    }
                }
            }
        }

        private void DrawConcentratedFlameJet(float t, float easeOut, float alpha)
        {
            // C06 Signature: Crimson elixir gathers & compresses at hand -> tight directional alchemical jet
            var dir = LocalTarget.LengthSquared() > 1f ? LocalTarget.Normalized() : Vector2.Right;
            var perp = new Vector2(-dir.Y, dir.X);
            var chargePos = new Vector2(32f, -18f);

            if (t < 0.32f)
            {
                var ct = t / 0.32f;
                var r = Mathf.Lerp(42f, 10f, ct);
                DrawArc(chargePos, r, 0f, Mathf.Tau, 24, WithAlpha(GarnetCrimson, alpha * 0.85f), 2.4f, true);
                DrawCircle(chargePos, 8f + ct * 6f, WithAlpha(WineRed, alpha * 0.65f));
            }

            if (t > 0.18f)
            {
                var jt = Mathf.Clamp((t - 0.18f) / 0.82f, 0f, 1f);
                var jEase = 1f - Mathf.Pow(1f - jt, 2.6f);
                var tip = chargePos.Lerp(LocalTarget, Mathf.Clamp(jEase * 1.35f, 0f, 1f));
                var tail = chargePos.Lerp(LocalTarget, Mathf.Clamp((jt - 0.25f) * 1.3f, 0f, 0.88f));
                DrawLine(tail, tip, WithAlpha(WineRed, alpha * 0.65f), 14f * (1f - jt * 0.5f), true);
                DrawLine(tail, tip, WithAlpha(GarnetCrimson, alpha * 0.94f), 6.2f * (1f - jt * 0.4f), true);
                DrawLine(tail + perp * 3f, tip, WithAlpha(Ivory, alpha * 0.78f), 2.0f, true);
                DrawCircle(tip, (14f + jEase * 24f) * (1f - jt * 0.6f), WithAlpha(GarnetCrimson, alpha * 0.55f));
            }
        }

        private void DrawBoilingEchoHit(float t, float easeOut, float alpha)
        {
            // U10 Signature: Escalating 3-hit alchemy sequence (0=condense, 1=splash burst, 2=garnet rupture)
            var scale = VariantIndex switch { 0 => 0.82f, 1 => 1.05f, _ => 1.38f };
            var r = (22f + easeOut * 52f) * scale;
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 28, WithAlpha(GarnetCrimson, alpha * 0.92f), (2.4f + VariantIndex * 0.9f), true);
            if (VariantIndex >= 1)
            {
                DrawPolygonRing(Vector2.Zero, r * 0.72f, 6, t * 1.1f, WithAlpha(Ivory, alpha * 0.72f), 1.8f);
            }
            if (VariantIndex >= 2)
            {
                DrawCircle(Vector2.Zero, r * 0.52f, WithAlpha(WineRed, alpha * 0.26f));
                DrawArc(Vector2.Zero, r * 1.18f, 0f, Mathf.Tau, 32, WithAlpha(CoolSilver, alpha * 0.75f), 1.8f, true);
            }
            var count = Reduced ? 4 : (5 + VariantIndex * 2);
            for (var i = 0; i < count; i++)
            {
                var a = i * (Mathf.Tau / count) + VariantIndex * 0.4f;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (r * 0.95f);
                DrawDiamond(p, (6.5f + VariantIndex * 2f) * (1f - t * 0.45f), a, WithAlpha(i % 2 == 0 ? GarnetCrimson : Ivory, alpha * 0.9f));
            }
        }

        private void DrawFinalDistillationCrystal(float t, float easeOut, float alpha)
        {
            // R08 Signature: Converging crimson threads -> faceted garnet crystal -> brief inward implosion & shatter
            var crystalStart = new Vector2(36f, -24f);
            if (t < 0.34f)
            {
                var ct = t / 0.34f;
                var spread = Mathf.Lerp(78f, 10f, ct);
                for (var i = 0; i < 6; i++)
                {
                    var a = i * (Mathf.Tau / 6f) + ct * 1.2f;
                    var p = crystalStart + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * spread;
                    DrawLine(p, crystalStart, WithAlpha(GarnetCrimson, alpha * 0.8f), 2.0f, true);
                }
                DrawDiamond(crystalStart, 10f + ct * 10f, ct * 0.8f, WithAlpha(Ivory, alpha * 0.9f));
            }

            if (t >= 0.24f && t < 0.68f)
            {
                var ft = Mathf.Clamp((t - 0.24f) / 0.40f, 0f, 1f);
                var pos = crystalStart.Lerp(LocalTarget, ft * ft);
                var tail = crystalStart.Lerp(LocalTarget, Mathf.Clamp(ft - 0.32f, 0f, 1f));
                DrawLine(tail, pos, WithAlpha(WineRed, alpha * 0.75f), 10f, true);
                DrawLine(tail, pos, WithAlpha(GarnetCrimson, alpha * 0.95f), 4.2f, true);
                DrawDiamond(pos, 18f, (LocalTarget - crystalStart).Angle(), WithAlpha(Ivory, alpha * 0.95f));
            }

            if (t >= 0.56f)
            {
                var bt = Mathf.Clamp((t - 0.56f) / 0.44f, 0f, 1f);
                // Brief inward implosion (bt < 0.35), then crisp crystalline rupture
                var r = bt < 0.35f ? Mathf.Lerp(56f, 14f, bt / 0.35f) : Mathf.Lerp(14f, 92f, (bt - 0.35f) / 0.65f);
                var bAlpha = (1f - bt) * (Reduced ? 0.7f : 1.0f);
                DrawArc(LocalTarget, r, 0f, Mathf.Tau, 36, WithAlpha(GarnetCrimson, bAlpha * 0.92f), 3.0f, true);
                DrawPolygonRing(LocalTarget, r * 0.78f, 6, bt * 1.4f, WithAlpha(CoolSilver, bAlpha * 0.85f), 2.0f);
                var shards = Reduced ? 5 : 10;
                for (var i = 0; i < shards; i++)
                {
                    var a = i * (Mathf.Tau / shards) + 0.25f;
                    var sp = LocalTarget + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    DrawDiamond(sp, 9.5f * (1f - bt * 0.5f), a, WithAlpha(i % 2 == 0 ? GarnetCrimson : CoolSilver, bAlpha * 0.92f));
                }
            }
        }

        private void DrawSilverScreenFold(float t, float easeOut, float alpha)
        {
            // C11 Signature: Thin translucent gothic silver mirror plane unfolding cleanly in front of Witch
            var center = new Vector2(58f, -14f);
            var h = 76f * (0.35f + 0.65f * easeOut);
            var w = 18f * (0.25f + 0.75f * easeOut);
            var pts = new[]
            {
                center + new Vector2(-w * 0.4f, -h),
                center + new Vector2(w, -h * 0.72f),
                center + new Vector2(w, h * 0.72f),
                center + new Vector2(-w * 0.4f, h)
            };
            DrawColoredPolygon(pts, WithAlpha(CoolSilver, alpha * 0.20f));
            DrawPolyline([pts[0], pts[1], pts[2], pts[3], pts[0]], WithAlpha(CoolSilver, alpha * 0.94f), 2.6f, true);
            // Diagonal silver refraction sheen sweeping top->bottom
            var sy = Mathf.Lerp(-h * 0.75f, h * 0.75f, easeOut);
            DrawLine(center + new Vector2(-w * 0.3f, sy - 14f), center + new Vector2(w * 0.9f, sy + 10f),
                WithAlpha(Ivory, alpha * 0.88f), 2.4f, true);
            DrawFourPointStar(center + new Vector2(w, -h * 0.72f), 8f * (1f - t * 0.4f), WithAlpha(Ivory, alpha * 0.9f));
        }

        private void DrawMirrorMazeSanctuary(float t, float easeOut, float alpha)
        {
            // U18 Signature: 4 angled silver mirror panes staggering around the Witch to form brief prism space
            var count = Reduced ? 3 : 4;
            for (var i = 0; i < count; i++)
            {
                var ang = i * (Mathf.Tau / count) + 0.45f + t * 0.42f;
                var dist = 48f + easeOut * 34f;
                var c = new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist * 0.68f - 10f);
                DrawMirrorPane(c, ang + Mathf.Pi * 0.5f, 24f, 44f, alpha * (0.75f + 0.2f * (i % 2)));
            }
            DrawArc(Vector2.Zero, 56f + easeOut * 38f, 0f, Mathf.Tau, 36, WithAlpha(CoolSilver, alpha * 0.55f), 1.6f, true);
        }

        private void DrawRazorPagesFan(float t, float easeOut, float alpha)
        {
            // C19 Signature: Parchment pages fan out around grimoire -> razor silver-ink blade streaks to target
            var fan = Reduced ? 3 : 5;
            for (var i = 0; i < fan; i++)
            {
                var ang = Mathf.Lerp(-0.9f, 0.7f, i / (float)Math.Max(1, fan - 1)) + t * 0.8f;
                var dist = 28f + easeOut * 44f;
                var p = new Vector2(Mathf.Cos(ang) * dist, Mathf.Sin(ang) * dist - 14f);
                DrawPageCard(p, ang, 10.5f * (1f - t * 0.35f), WithAlpha(Ivory, alpha * 0.88f), WithAlpha(InkBlack, alpha * 0.55f));
            }

            var tip = LocalTarget * Mathf.Clamp(easeOut * 1.38f, 0f, 1f);
            var tail = LocalTarget * Mathf.Clamp((t - 0.16f) * 1.25f, 0f, 0.85f);
            DrawLine(tail, tip, WithAlpha(CoolSilver, alpha * 0.70f), 6.2f * (1f - t * 0.4f), true);
            DrawLine(tail, tip, WithAlpha(Ivory, alpha * 0.96f), 2.4f, true);
            DrawDiamond(tip, 12f * (1f - t * 0.4f), LocalTarget.Angle(), WithAlpha(Ivory, alpha * 0.95f));
        }

        private void DrawPurifyTransmute(float t, float easeOut, float alpha)
        {
            // U05 Signature: Phantom card folds & compresses into alchemy sigil -> pops into rising garnet spark
            if (t < 0.55f)
            {
                var ft = t / 0.55f;
                var foldScale = Mathf.Lerp(1.0f, 0.15f, ft * ft);
                DrawPageCard(Vector2.Zero, ft * 1.8f, 22f * foldScale, WithAlpha(Ivory, alpha * 0.82f), WithAlpha(GarnetCrimson, alpha * 0.9f));
                DrawArc(Vector2.Zero, Mathf.Lerp(34f, 12f, ft), 0f, Mathf.Tau, 24, WithAlpha(GarnetCrimson, alpha * 0.85f), 2.0f, true);
            }
            else
            {
                var st = (t - 0.55f) / 0.45f;
                var lift = new Vector2(0f, -st * 54f);
                DrawPolygonRing(Vector2.Zero, 14f + st * 34f, 6, st * 1.4f, WithAlpha(GarnetCrimson, (1f - st) * alpha), 1.8f);
                DrawFourPointStar(lift, 12f * (1f - st * 0.4f), WithAlpha(Ivory, (1f - st) * 0.95f));
                DrawCircle(lift, 5.5f * (1f - st * 0.5f), WithAlpha(GarnetCrimson, (1f - st) * 0.92f));
            }
        }

        private void DrawPrimalGreedSwirl(float t, float easeOut, float alpha)
        {
            // U37 Signature: Spectral mirror swirl + phantom card silhouettes orbiting into shuffle vortex
            var r = 42f + easeOut * 58f;
            DrawArc(Vector2.Zero, r, t * 2.2f, t * 2.2f + Mathf.Tau * 0.8f, 36, WithAlpha(GarnetCrimson, alpha * 0.82f), 2.4f, true);
            DrawArc(Vector2.Zero, r * 0.72f, -t * 2.6f, -t * 2.6f + Mathf.Tau * 0.75f, 32, WithAlpha(CoolSilver, alpha * 0.78f), 1.8f, true);
            var cards = Reduced ? 4 : 7;
            for (var i = 0; i < cards; i++)
            {
                var a = i * (Mathf.Tau / cards) + t * 4.2f;
                var rad = Mathf.Lerp(r * 1.05f, 16f, t * 0.75f);
                var p = new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.65f);
                DrawPageCard(p, a + 0.5f, 11f * (1f - t * 0.3f), WithAlpha(i % 2 == 0 ? Ivory : WineRed, alpha * 0.84f), WithAlpha(GarnetCrimson, alpha * 0.75f));
            }
        }

        private void DrawSlothVeil(float t, float easeOut, float alpha)
        {
            // U38 Signature: Two curved silver mirror panes gently closing inward + tranquil cold-silver veil
            var spread = Mathf.Lerp(86f, 26f, easeOut);
            DrawArc(new Vector2(-spread, -8f), 58f, -0.95f, 0.95f, 24, WithAlpha(CoolSilver, alpha * 0.88f), 2.8f, true);
            DrawArc(new Vector2(spread, -8f), 58f, Mathf.Pi - 0.95f, Mathf.Pi + 0.95f, 24, WithAlpha(CoolSilver, alpha * 0.88f), 2.8f, true);
            DrawCircle(new Vector2(0f, -8f), 46f + easeOut * 28f, WithAlpha(CoolSilver, alpha * 0.14f));
            DrawArc(new Vector2(0f, -8f), 42f + easeOut * 34f, 0f, Mathf.Tau, 36, WithAlpha(Ivory, alpha * 0.62f), 1.6f, true);
        }

        private void DrawCalamityShards(float t, float easeOut, float alpha)
        {
            // R26 Signature: Single mirror splitting into 3 irregular silver shards with distinct crimson desire runes
            for (var i = 0; i < 3; i++)
            {
                var a = -Mathf.Pi * 0.5f + (i - 1) * 0.92f;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (18f + easeOut * 58f) + new Vector2(0f, -16f);
                DrawMirrorPane(p, a * 0.35f, 20f, 30f, alpha);
                DrawFourPointStar(p, 7.5f * (1f - t * 0.3f), WithAlpha(GarnetCrimson, alpha * 0.94f));
            }
        }

        private void DrawLifeSiphon(float t, float easeOut, float alpha)
        {
            // R27 Signature: Fine crimson silk threads from HP bar converging to Witch palm -> energy spark
            var strands = Reduced ? 2 : 4;
            for (var i = 0; i < strands; i++)
            {
                var curveOff = new Vector2((i - (strands - 1) * 0.5f) * 34f, -22f);
                var mid = LocalTarget * 0.5f + curveOff;
                var head = Mathf.Clamp(easeOut * 1.3f, 0f, 1f);
                var tail = Mathf.Clamp((t - 0.18f) * 1.25f, 0f, 0.85f);
                var prev = QuadBezier(Vector2.Zero, mid, LocalTarget, tail);
                for (var s = 1; s <= 8; s++)
                {
                    var u = Mathf.Lerp(tail, head, s / 8f);
                    var p = QuadBezier(Vector2.Zero, mid, LocalTarget, u);
                    DrawLine(prev, p, WithAlpha(GarnetCrimson, alpha * 0.88f), 2.6f, true);
                    prev = p;
                }
            }
            if (t > 0.38f)
            {
                var st = (t - 0.38f) / 0.62f;
                DrawFourPointStar(LocalTarget, (11f + st * 10f) * (1f - st * 0.5f), WithAlpha(Ivory, (1f - st) * alpha));
                DrawArc(LocalTarget, 10f + st * 32f, 0f, Mathf.Tau, 20, WithAlpha(GarnetCrimson, (1f - st) * alpha * 0.85f), 2.0f, true);
            }
        }

        private void DrawCounterpointRelay(float t, float easeOut, float alpha)
        {
            // Section 10: Continuous spell relay transitioning smoothly from previous spell color -> new spell color
            var r1 = 36f + easeOut * 70f;
            DrawArc(Vector2.Zero, r1, 0f, Mathf.Tau, 40, WithAlpha(PrimaryColor, alpha * 0.92f), 2.6f, true);
            DrawArc(Vector2.Zero, r1 + 6f, t * 2.4f, t * 2.4f + 1.25f, 18, WithAlpha(Ivory, alpha * 0.88f), 3.4f, true);
            DrawArc(Vector2.Zero, r1 + 6f, t * 2.4f + Mathf.Pi, t * 2.4f + Mathf.Pi + 1.25f, 18, WithAlpha(Ivory, alpha * 0.88f), 3.4f, true);

            // Flowing connecting ribbon thread arcing from previous hue into next hue
            var ribbonStart = new Vector2(-46f, -38f);
            var ribbonEnd = new Vector2(54f, -18f);
            var ribbonCtrl = new Vector2(0f, -82f + easeOut * 28f);
            var prev = ribbonStart;
            for (var i = 1; i <= 10; i++)
            {
                var u = i / 10f;
                var p = QuadBezier(ribbonStart, ribbonCtrl, ribbonEnd, u);
                var col = PrimaryColor.Lerp(SecondaryColor, u);
                DrawLine(prev, p, WithAlpha(col, alpha * 0.92f), 3.2f * Mathf.Sin(u * Mathf.Pi) + 1.0f, true);
                prev = p;
            }

            if (t > 0.14f)
            {
                var t2 = Mathf.Clamp((t - 0.14f) / 0.86f, 0f, 1f);
                var e2 = 1f - Mathf.Pow(1f - t2, 2.8f);
                var a2 = (1f - t2) * (Reduced ? 0.68f : 0.96f);
                var r2 = 42f + e2 * 90f;
                DrawArc(Vector2.Zero, r2, 0f, Mathf.Tau, 42, WithAlpha(SecondaryColor, a2 * 0.90f), 2.8f, true);
                DrawArc(Vector2.Zero, r2 - 6f, -t2 * 2.8f + 0.5f, -t2 * 2.8f + 1.6f, 18, WithAlpha(SecondaryColor, a2), 3.8f, true);
            }
        }

        private void DrawCardDrawFlutter(float t, float easeOut, float alpha)
        {
            var p = Vector2.Zero.Lerp(LocalTarget, easeOut);
            DrawPageCard(p, -0.35f + t * 0.9f, 11f, WithAlpha(Ivory, alpha * 0.72f), WithAlpha(CoolSilver, alpha * 0.55f));
        }

        private void DrawCardDiscardInk(float t, float easeOut, float alpha)
        {
            var p = Vector2.Zero.Lerp(LocalTarget, easeOut);
            DrawPageCard(p, 0.25f + t * 1.1f, 9.5f * (1f - t * 0.35f), WithAlpha(Charcoal, alpha * 0.72f), WithAlpha(Ivory, alpha * 0.35f));
        }

        private void DrawCardExhaustAsh(float t, float easeOut, float alpha)
        {
            // Distinct from Discard: crimson silk thread unraveling into dark ash motes
            DrawLine(new Vector2(-24f, 0f), new Vector2(24f, -easeOut * 28f), WithAlpha(GarnetCrimson, alpha * 0.85f), 2.0f, true);
            var motes = Reduced ? 4 : 7;
            for (var i = 0; i < motes; i++)
            {
                var a = i * (Mathf.Tau / motes) + 0.4f;
                var p = new Vector2(Mathf.Cos(a) * (10f + easeOut * 30f), -easeOut * (18f + (i % 3) * 14f));
                DrawDiamond(p, 4.8f * (1f - t * 0.5f), a, WithAlpha(i % 2 == 0 ? GarnetCrimson : Charcoal, alpha * 0.88f));
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
            var r = 34f + easeOut * 86f;
            DrawCircle(Vector2.Zero, r * 0.88f, WithAlpha(WineRed, alpha * 0.16f));
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 40, WithAlpha(GarnetCrimson, alpha * 0.88f), 2.8f, true);
            DrawPolygonRing(Vector2.Zero, r * 0.72f, 6, -t * 0.75f, WithAlpha(WineRed, alpha * 0.85f), 2.2f);
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
            var r = 40f + easeOut * 112f;
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 48, WithAlpha(CoolSilver, alpha * 0.94f), 3.6f, true);
            DrawArc(Vector2.Zero, r * 0.78f, 0f, Mathf.Tau, 40, WithAlpha(GarnetCrimson, alpha * 0.78f), 2.2f, true);
            DrawLine(new Vector2(0f, -r * 1.12f), new Vector2(0f, r * 1.12f), WithAlpha(Ivory, alpha * 0.85f), 2.6f, true);
            DrawPolygonRing(Vector2.Zero, r * 0.92f, 8, t * 0.6f, WithAlpha(CoolSilver, alpha * 0.65f), 1.8f);
        }

        private void DrawPotionAlchemy(float t, float easeOut, float alpha)
        {
            var r = 36f + easeOut * 56f;
            DrawArc(Vector2.Zero, r, 0f, Mathf.Tau, 36, WithAlpha(GarnetCrimson, alpha * 0.85f), 2.4f, true);
            DrawArc(Vector2.Zero, r * 0.86f, 0f, Mathf.Tau, 32, WithAlpha(Ivory, alpha * 0.62f), 1.4f, true);
            DrawPolygonRing(Vector2.Zero, r * 0.82f, 3, t * 0.8f, WithAlpha(CoolSilver, alpha * 0.68f), 1.6f);
            DrawPolygonRing(Vector2.Zero, r * 0.82f, 3, t * 0.8f + Mathf.Pi / 3f, WithAlpha(GarnetCrimson, alpha * 0.72f), 1.6f);
        }

        private void DrawMirrorSlash(float t, float easeOut, float alpha, float flipX)
        {
            var len = 54f + easeOut * 66f;
            var dir = new Vector2(0.82f * flipX, -0.57f);
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
            var r = 42f + easeOut * 48f;
            DrawPolygonRing(Vector2.Zero, r, 6, t * 0.45f, WithAlpha(CoolSilver, alpha * 0.90f), 2.8f);
            DrawPolygonRing(Vector2.Zero, r * 0.74f, 6, -t * 0.65f, WithAlpha(Ivory, alpha * 0.68f), 1.6f);
            DrawFourPointStar(new Vector2(r * 0.65f, -r * 0.45f), 9f * (1f - t * 0.35f), WithAlpha(Ivory, alpha * 0.92f));
        }

        private void DrawGrimoirePages(float t, float easeOut, float alpha)
        {
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
            if (LocalTarget.LengthSquared() > 2500f)
            {
                var dart = LocalTarget * Mathf.Clamp(easeOut * 1.35f, 0f, 1f);
                DrawLine(LocalTarget * Mathf.Clamp(t * 0.7f, 0f, 0.7f), dart, WithAlpha(Ivory, alpha * 0.88f), 3.0f, true);
                DrawFourPointStar(dart, 10f * (1f - t * 0.35f), WithAlpha(CoolSilver, alpha * 0.95f));
            }
        }

        private void DrawAlchemyVial(Vector2 center, float rot, float r, float alpha)
        {
            var ca = Mathf.Cos(rot);
            var sa = Mathf.Sin(rot);
            Vector2 Rot(float x, float y) => center + new Vector2(x * ca - y * sa, x * sa + y * ca);
            // Round glass bulb + crimson liquid core + angled neck
            DrawCircle(center, r, WithAlpha(WineRed, alpha * 0.85f));
            DrawCircle(Rot(0f, r * 0.2f), r * 0.72f, WithAlpha(GarnetCrimson, alpha * 0.92f));
            DrawArc(center, r, 0f, Mathf.Tau, 20, WithAlpha(CoolSilver, alpha * 0.96f), 2.2f, true);
            DrawLine(Rot(0f, -r), Rot(0f, -r * 1.65f), WithAlpha(CoolSilver, alpha * 0.92f), 4.2f, true);
            DrawCircle(Rot(-r * 0.35f, -r * 0.35f), r * 0.22f, WithAlpha(Ivory, alpha * 0.90f));
        }

        private void DrawMirrorPane(Vector2 center, float rot, float w, float h, float alpha)
        {
            var ca = Mathf.Cos(rot);
            var sa = Mathf.Sin(rot);
            Vector2 Rot(float x, float y) => center + new Vector2(x * ca - y * sa, x * sa + y * ca);
            var pts = new[] { Rot(0f, -h * 0.5f), Rot(w * 0.5f, 0f), Rot(0f, h * 0.5f), Rot(-w * 0.5f, 0f) };
            DrawColoredPolygon(pts, WithAlpha(CoolSilver, alpha * 0.22f));
            DrawPolyline([pts[0], pts[1], pts[2], pts[3], pts[0]], WithAlpha(CoolSilver, alpha * 0.92f), 2.0f, true);
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
