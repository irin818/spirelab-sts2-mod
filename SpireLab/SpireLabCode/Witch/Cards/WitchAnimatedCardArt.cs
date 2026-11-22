using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch.Cards;

public enum AnimatedArtMode
{
    Full = 0,
    Reduced = 1,
    Static = 2
}

public enum CardArtVariant
{
    Original = 0,
    Alternate = 1
}

public enum CardArtDisplayState
{
    Idle = 0,
    Play = 1
}

internal sealed record CardArtProfile(
    string SpecId,
    CardArtVariant Variant,
    string ShaderPath,
    string MaskPath,
    float LoopSeconds,
    float WakeSeconds);

/// <summary>
/// Play-only animated card art controller attached lazily via BaseLib ExtraCardUi (zero Harmony patches).
/// Keeps Hand, Card Library, Fate Selection, Reward Screen, and Inspect Screen 100% native (Material = null);
/// activates animated art exclusively while the card is being played in the combat Play Queue / Play Container.
/// </summary>
public static class WitchAnimatedCardArt
{
    private const string R10Root = $"{TheWitch.Art}/cards/animated/r10";
    private const string R10ShaderPath = $"{R10Root}/r10_mirror_self.gdshader";
    private const string R10MaskPath = $"{R10Root}/r10_masks.png";

    private static readonly Dictionary<string, CardArtProfile> Profiles = new(StringComparer.Ordinal)
    {
        ["R10"] = new(
            SpecId: "R10",
            Variant: CardArtVariant.Original,
            ShaderPath: R10ShaderPath,
            MaskPath: R10MaskPath,
            LoopSeconds: 4.2f,
            WakeSeconds: 0.14f)
    };

    private static readonly HashSet<WitchCardArtController> LiveControllers = [];
    private static readonly Dictionary<string, (Shader Shader, Texture2D Mask)> AssetCache = new(StringComparer.Ordinal);
    private static AnimatedArtMode? _modeOverride;
    private static bool _envRead;

    public static IReadOnlyList<string> Assets { get; } = [R10ShaderPath, R10MaskPath];

    public static AnimatedArtMode Mode
    {
        get
        {
            if (DisplayServer.GetName() == "headless")
            {
                return AnimatedArtMode.Static;
            }

            if (!_envRead)
            {
                _envRead = true;
                var env = System.Environment.GetEnvironmentVariable("SPIRELAB_WITCH_ART_MODE");
                if (Enum.TryParse<AnimatedArtMode>(env, ignoreCase: true, out var parsed))
                {
                    _modeOverride = parsed;
                }
            }

            if (_modeOverride is { } explicitMode)
            {
                return explicitMode;
            }

            if (SaveManager.Instance?.PrefsSave?.FastMode == FastModeType.Instant)
            {
                return AnimatedArtMode.Reduced;
            }

            return AnimatedArtMode.Full;
        }
    }

    internal static float? DebugPhaseOverride { get; set; }

    public static int ActiveControllerCount =>
        LiveControllers.Count(controller => GodotObject.IsInstanceValid(controller) && controller.IsActive);

    public static int PlayActivationsCount { get; private set; }

    public static void SetMode(AnimatedArtMode? mode)
    {
        _envRead = true;
        _modeOverride = mode;
        foreach (var controller in LiveControllers.ToArray())
        {
            if (!GodotObject.IsInstanceValid(controller))
            {
                LiveControllers.Remove(controller);
                continue;
            }

            controller.RefreshContext();
        }

        WitchLiveRig.RefreshAllModes();
    }

    public static void Sync(NCard node, CardModel? model)
    {
        var portrait = node.GetNodeOrNull<TextureRect>("%Portrait");
        if (portrait == null)
        {
            return;
        }

        var existing = portrait.GetNodeOrNull<WitchCardArtController>(WitchCardArtController.ControllerName);
        if (node.Visibility != ModelVisibility.Visible
            || model is not WitchCard witch
            || !Profiles.TryGetValue(witch.SpecId, out var profile))
        {
            existing?.ClearAssignment();
            return;
        }

        if (existing == null)
        {
            existing = new WitchCardArtController { Name = WitchCardArtController.ControllerName };
            portrait.AddChild(existing);
        }

        existing.Assign(node, portrait, profile);
    }

    public static void NotifyCardPlayed(CardModel card)
    {
        if (card is not WitchCard witch || !Profiles.ContainsKey(witch.SpecId))
        {
            return;
        }

        foreach (var controller in LiveControllers.ToArray())
        {
            if (!GodotObject.IsInstanceValid(controller))
            {
                LiveControllers.Remove(controller);
                continue;
            }

            if (controller.MatchesCard(card))
            {
                controller.ForcePlayActive();
            }
        }
    }

    internal static void RecordPlayActivation() => PlayActivationsCount++;
    internal static void Register(WitchCardArtController controller) => LiveControllers.Add(controller);
    internal static void Unregister(WitchCardArtController controller) => LiveControllers.Remove(controller);

    internal static bool TryLoadAssets(CardArtProfile profile, out Shader shader, out Texture2D mask)
    {
        if (AssetCache.TryGetValue(profile.SpecId, out var cached)
            && GodotObject.IsInstanceValid(cached.Shader)
            && GodotObject.IsInstanceValid(cached.Mask))
        {
            shader = cached.Shader;
            mask = cached.Mask;
            return true;
        }

        var loadedShader = ResourceLoader.Load<Shader>(profile.ShaderPath);
        var loadedMask = ResourceLoader.Load<Texture2D>(profile.MaskPath);
        if (loadedShader == null || loadedMask == null)
        {
            shader = null!;
            mask = null!;
            return false;
        }

        AssetCache[profile.SpecId] = (loadedShader, loadedMask);
        shader = loadedShader;
        mask = loadedMask;
        return true;
    }
}

public sealed partial class WitchCardArtController : Node
{
    public const string ControllerName = "WitchCardArtController";

    private NCard? _card;
    private TextureRect? _portrait;
    private CardArtProfile? _profile;
    private ShaderMaterial? _material;

    private float _phase = 0.20f;
    private float _strength;
    private bool _forcedPlay;

    public CardArtDisplayState DisplayState { get; private set; } = CardArtDisplayState.Idle;
    public bool IsActive { get; private set; }
    public float LoopPhase => _phase;
    public float EffectStrength => _strength;

    internal bool MatchesCard(CardModel model) => _card != null && GodotObject.IsInstanceValid(_card) && ReferenceEquals(_card.Model, model);

    internal void Assign(NCard card, TextureRect portrait, CardArtProfile profile)
    {
        _card = card;
        _portrait = portrait;
        _profile = profile;
        _forcedPlay = false;

        WitchAnimatedCardArt.Register(this);
        RefreshContext();
        if (IsInsideTree())
        {
            CallDeferred(MethodName.RefreshContext);
        }
    }

    internal void ClearAssignment()
    {
        _profile = null;
        _forcedPlay = false;
        DeactivateImmediate();
    }

    internal void ForcePlayActive()
    {
        if (_profile == null || WitchAnimatedCardArt.Mode == AnimatedArtMode.Static)
        {
            return;
        }

        _forcedPlay = true;
        ActivatePlay();
    }

    public override void _EnterTree()
    {
        WitchAnimatedCardArt.Register(this);
        CallDeferred(MethodName.RefreshContext);
    }

    public override void _ExitTree()
    {
        _forcedPlay = false;
        DeactivateImmediate();
        WitchAnimatedCardArt.Unregister(this);
    }

    internal void RefreshContext()
    {
        if (_card == null || _portrait == null || _profile == null
            || !GodotObject.IsInstanceValid(_card) || !GodotObject.IsInstanceValid(_portrait))
        {
            DeactivateImmediate();
            return;
        }

        if (WitchAnimatedCardArt.Mode == AnimatedArtMode.Static)
        {
            DeactivateImmediate();
            return;
        }

        if (_forcedPlay || IsInPlayZone(_card))
        {
            ActivatePlay();
        }
        else
        {
            DeactivateImmediate();
        }
    }

    private void ActivatePlay()
    {
        if (_portrait == null || _profile == null || !GodotObject.IsInstanceValid(_portrait))
        {
            return;
        }

        if (!WitchAnimatedCardArt.TryLoadAssets(_profile, out var shader, out var mask))
        {
            DeactivateImmediate();
            return;
        }

        var wasActive = IsActive;
        _material ??= new ShaderMaterial { Shader = shader };
        _material.SetShaderParameter("mask_tex", mask);
        _material.SetShaderParameter("is_inspect", 1.0f);
        _material.SetShaderParameter("reduced_motion", WitchAnimatedCardArt.Mode == AnimatedArtMode.Reduced ? 1.0f : 0.0f);

        _portrait.Material = _material;
        DisplayState = CardArtDisplayState.Play;
        IsActive = true;
        if (!wasActive)
        {
            // Start near the silver glass sweep peak so even brief card plays show vivid animated art immediately
            _phase = 0.22f;
            _strength = 0.35f;
            WitchAnimatedCardArt.RecordPlayActivation();
        }

        PushUniforms();
        SetProcess(true);
    }

    private void DeactivateImmediate()
    {
        DisplayState = CardArtDisplayState.Idle;
        IsActive = false;
        _strength = 0f;
        SetProcess(false);

        if (_portrait != null && GodotObject.IsInstanceValid(_portrait) && _portrait.Material == _material)
        {
            _portrait.Material = null;
        }

        _material = null;
    }

    public override void _Process(double delta)
    {
        if (!IsActive || _card == null || _portrait == null || _profile == null
            || !GodotObject.IsInstanceValid(_card) || !GodotObject.IsInstanceValid(_portrait))
        {
            DeactivateImmediate();
            return;
        }

        // Automatically deactivate as soon as the card leaves PlayQueue / PlayContainer (e.g. moves to Discard/Exhaust)
        if (!_forcedPlay && !IsInPlayZone(_card))
        {
            DeactivateImmediate();
            return;
        }

        if (_forcedPlay && _card.Model?.Pile is { Type: not PileType.Play and not PileType.Hand })
        {
            _forcedPlay = false;
            if (!IsInPlayZone(_card))
            {
                DeactivateImmediate();
                return;
            }
        }

        var dt = (float)Mathf.Clamp(delta, 0.0005, 0.05);
        _strength = Mathf.Min(1.0f, _strength + dt / Math.Max(0.05f, _profile.WakeSeconds));
        _phase = WitchAnimatedCardArt.DebugPhaseOverride ?? Mathf.PosMod(_phase + dt / _profile.LoopSeconds, 1.0f);
        PushUniforms();
    }

    private void PushUniforms()
    {
        if (_material == null)
        {
            return;
        }

        _material.SetShaderParameter("loop_phase", _phase);
        _material.SetShaderParameter("effect_strength", _strength);
        _material.SetShaderParameter("is_inspect", 1.0f);
        _material.SetShaderParameter("reduced_motion", WitchAnimatedCardArt.Mode == AnimatedArtMode.Reduced ? 1.0f : 0.0f);
    }

    private static bool IsInPlayZone(NCard card)
    {
        var ui = NCombatRoom.Instance?.Ui;
        var playContainer = ui?.PlayContainer;
        var playQueue = ui?.PlayQueue;

        for (Node? parent = card.GetParent(); parent != null; parent = parent.GetParent())
        {
            if (parent is NCardPlayQueue || parent == playQueue || parent == playContainer)
            {
                return true;
            }
        }

        return card.Model?.Pile?.Type == PileType.Play;
    }
}
