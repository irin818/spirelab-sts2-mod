using Godot;
using MegaCrit.Sts2.Core.Entities.UI;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

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
    Hover = 1,
    Inspect = 2
}

internal sealed record CardArtProfile(
    string SpecId,
    CardArtVariant Variant,
    string ShaderPath,
    string MaskPath,
    float LoopSeconds,
    float HoverTargetStrength,
    float WakeSeconds,
    float SettleSeconds);

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
            HoverTargetStrength: 0.62f,
            WakeSeconds: 0.45f,
            SettleSeconds: 0.35f)
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

    public static int ActiveControllerCount => LiveControllers.Count(controller => GodotObject.IsInstanceValid(controller) && controller.IsActive);
    public static int AttachedControllerCount => LiveControllers.Count(GodotObject.IsInstanceValid);

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

            controller.RefreshMode();
        }
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

    internal static void Register(WitchCardArtController controller) => LiveControllers.Add(controller);
    internal static void Unregister(WitchCardArtController controller) => LiveControllers.Remove(controller);
}

public partial class WitchCardArtController : Node
{
    internal const string ControllerName = "WitchAnimatedArtController";

    private static readonly StringName ParamMaskTex = "mask_tex";
    private static readonly StringName ParamLoopPhase = "loop_phase";
    private static readonly StringName ParamEffectStrength = "effect_strength";
    private static readonly StringName ParamInspectWeight = "inspect_weight";
    private static readonly StringName ParamReducedMotion = "reduced_motion";

    private NCard? _card;
    private TextureRect? _portrait;
    private CardArtProfile? _profile;
    private ShaderMaterial? _material;

    private NCardHolder? _boundHolder;
    private NClickableControl? _boundHitbox;
    private NInspectCardScreen? _boundInspectScreen;

    private bool _holderFocused;
    private bool _hitboxFocused;
    private float _phase;
    private float _strength;
    private float _inspectWeight;

    public CardArtDisplayState DisplayState { get; private set; } = CardArtDisplayState.Idle;
    public bool IsActive => _profile != null && IsProcessing();
    public float CurrentPhase => _phase;
    public float CurrentStrength => _strength;

    public override void _EnterTree()
    {
        WitchAnimatedCardArt.Register(this);
        if (_profile != null)
        {
            CallDeferred(MethodName.RefreshContext);
        }
    }

    public override void _ExitTree()
    {
        UnbindContext();
        ReleaseMaterial();
        SetProcess(false);
        WitchAnimatedCardArt.Unregister(this);
    }

    internal void Assign(NCard card, TextureRect portrait, CardArtProfile profile)
    {
        var sameProfile = _profile?.SpecId == profile.SpecId && _card == card;
        _card = card;
        _portrait = portrait;
        _profile = profile;

        if (!sameProfile)
        {
            _phase = 0f;
            _strength = 0f;
            _inspectWeight = 0f;
        }

        RefreshContext();
        CallDeferred(MethodName.RefreshContext);
    }

    internal void ClearAssignment()
    {
        _profile = null;
        DisplayState = CardArtDisplayState.Idle;
        _holderFocused = false;
        _hitboxFocused = false;
        _strength = 0f;
        _inspectWeight = 0f;
        UnbindContext();
        ReleaseMaterial();
        SetProcess(false);
    }

    internal void RefreshMode()
    {
        if (_profile == null)
        {
            return;
        }

        UpdateTargetState();
    }

    private void RefreshContext()
    {
        if (_profile == null || _card == null || !GodotObject.IsInstanceValid(_card) || !IsInsideTree())
        {
            return;
        }

        var inspectScreen = FindAncestor<NInspectCardScreen>(_card);
        var holder = inspectScreen == null ? FindAncestor<NCardHolder>(_card) : null;

        if (inspectScreen != _boundInspectScreen || holder != _boundHolder || (holder != null && holder.Hitbox != _boundHitbox))
        {
            UnbindContext();
            BindContext(inspectScreen, holder);
        }

        UpdateTargetState();
    }

    private void BindContext(NInspectCardScreen? inspectScreen, NCardHolder? holder)
    {
        if (inspectScreen != null && GodotObject.IsInstanceValid(inspectScreen))
        {
            _boundInspectScreen = inspectScreen;
            _boundInspectScreen.VisibilityChanged += OnContextVisibilityChanged;
        }
        else if (holder != null && GodotObject.IsInstanceValid(holder))
        {
            _boundHolder = holder;
            _boundHolder.FocusEntered += OnHolderFocusEntered;
            _boundHolder.FocusExited += OnHolderFocusExited;
            _boundHolder.VisibilityChanged += OnContextVisibilityChanged;
            _holderFocused = _boundHolder.HasFocus();

            if (holder.Hitbox is { } hitbox && GodotObject.IsInstanceValid(hitbox))
            {
                _boundHitbox = hitbox;
                _boundHitbox.Focused += OnHitboxFocused;
                _boundHitbox.Unfocused += OnHitboxUnfocused;
            }
        }
    }

    private void UnbindContext()
    {
        if (_boundInspectScreen != null && GodotObject.IsInstanceValid(_boundInspectScreen))
        {
            _boundInspectScreen.VisibilityChanged -= OnContextVisibilityChanged;
        }
        _boundInspectScreen = null;

        if (_boundHolder != null && GodotObject.IsInstanceValid(_boundHolder))
        {
            _boundHolder.FocusEntered -= OnHolderFocusEntered;
            _boundHolder.FocusExited -= OnHolderFocusExited;
            _boundHolder.VisibilityChanged -= OnContextVisibilityChanged;
        }
        _boundHolder = null;

        if (_boundHitbox != null && GodotObject.IsInstanceValid(_boundHitbox))
        {
            _boundHitbox.Focused -= OnHitboxFocused;
            _boundHitbox.Unfocused -= OnHitboxUnfocused;
        }
        _boundHitbox = null;

        _holderFocused = false;
        _hitboxFocused = false;
    }

    private void OnContextVisibilityChanged() => UpdateTargetState();

    private void OnHolderFocusEntered()
    {
        _holderFocused = true;
        UpdateTargetState();
    }

    private void OnHolderFocusExited()
    {
        _holderFocused = false;
        UpdateTargetState();
    }

    private void OnHitboxFocused(NClickableControl _)
    {
        _hitboxFocused = true;
        UpdateTargetState();
    }

    private void OnHitboxUnfocused(NClickableControl _)
    {
        _hitboxFocused = false;
        UpdateTargetState();
    }

    private void UpdateTargetState()
    {
        if (_profile == null || _card == null || _portrait == null
            || !GodotObject.IsInstanceValid(_card) || !GodotObject.IsInstanceValid(_portrait))
        {
            ClearAssignment();
            return;
        }

        var mode = WitchAnimatedCardArt.Mode;
        if (mode == AnimatedArtMode.Static || !_card.IsVisibleInTree())
        {
            DisplayState = CardArtDisplayState.Idle;
            _strength = 0f;
            _inspectWeight = 0f;
            ReleaseMaterial();
            SetProcess(false);
            return;
        }

        var nextState = CardArtDisplayState.Idle;
        if (_boundInspectScreen != null && GodotObject.IsInstanceValid(_boundInspectScreen) && _boundInspectScreen.Visible)
        {
            nextState = CardArtDisplayState.Inspect;
        }
        else if (mode == AnimatedArtMode.Full && (_holderFocused || _hitboxFocused))
        {
            nextState = CardArtDisplayState.Hover;
        }

        var previous = DisplayState;
        DisplayState = nextState;

        if (nextState == CardArtDisplayState.Inspect)
        {
            if (previous == CardArtDisplayState.Idle && WitchAnimatedCardArt.DebugPhaseOverride == null)
            {
                _phase = 0f;
            }
            EnsureMaterial();
            ApplyUniforms(mode);
            SetProcess(true);
        }
        else if (nextState == CardArtDisplayState.Hover)
        {
            if (previous == CardArtDisplayState.Idle && _strength <= 0.01f && WitchAnimatedCardArt.DebugPhaseOverride == null)
            {
                // Start just before the silver mirror sweep so hover wakes gracefully.
                _phase = 0.08f;
            }
            EnsureMaterial();
            ApplyUniforms(mode);
            SetProcess(true);
        }
        else
        {
            // Idle: if already settled (or just exited Inspect), release material immediately;
            // if settling from Hover, fade smoothly to 0 in _Process then release.
            if (previous == CardArtDisplayState.Inspect || _strength <= 0.005f)
            {
                _strength = 0f;
                _inspectWeight = 0f;
                ReleaseMaterial();
                SetProcess(false);
            }
            else
            {
                SetProcess(true);
            }
        }
    }

    public override void _Process(double delta)
    {
        if (_profile == null || _card == null || _portrait == null
            || !GodotObject.IsInstanceValid(_card) || !GodotObject.IsInstanceValid(_portrait))
        {
            ClearAssignment();
            return;
        }

        var mode = WitchAnimatedCardArt.Mode;
        if (mode == AnimatedArtMode.Static || !_card.IsVisibleInTree())
        {
            DisplayState = CardArtDisplayState.Idle;
            _strength = 0f;
            _inspectWeight = 0f;
            ReleaseMaterial();
            SetProcess(false);
            return;
        }

        var dt = (float)Math.Clamp(delta, 0.0, 0.1);
        var targetStrength = DisplayState switch
        {
            CardArtDisplayState.Inspect => 1.0f,
            CardArtDisplayState.Hover => _profile.HoverTargetStrength,
            _ => 0.0f
        };
        var targetInspect = DisplayState == CardArtDisplayState.Inspect ? 1.0f : 0.0f;

        if (WitchAnimatedCardArt.DebugPhaseOverride is { } pinnedPhase)
        {
            _phase = Mathf.PosMod(pinnedPhase, 1.0f);
            _strength = targetStrength;
            _inspectWeight = targetInspect;
        }
        else
        {
            var rate = targetStrength > _strength
                ? 1.0f / Math.Max(0.05f, _profile.WakeSeconds)
                : 1.0f / Math.Max(0.05f, _profile.SettleSeconds);
            _strength = Mathf.MoveToward(_strength, targetStrength, dt * rate);
            _inspectWeight = Mathf.MoveToward(_inspectWeight, targetInspect, dt * 3.0f);

            if (_strength > 0.001f)
            {
                _phase = (_phase + dt / _profile.LoopSeconds) % 1.0f;
            }
        }

        if (DisplayState == CardArtDisplayState.Idle && _strength <= 0.001f)
        {
            _strength = 0f;
            _inspectWeight = 0f;
            ReleaseMaterial();
            SetProcess(false);
            return;
        }

        EnsureMaterial();
        ApplyUniforms(mode);
    }

    private void EnsureMaterial()
    {
        if (_profile == null || _portrait == null)
        {
            return;
        }

        if (_material == null || !GodotObject.IsInstanceValid(_material))
        {
            if (!WitchAnimatedCardArt.TryLoadAssets(_profile, out var shader, out var mask))
            {
                return;
            }

            _material = new ShaderMaterial { Shader = shader };
            _material.SetShaderParameter(ParamMaskTex, mask);
        }

        if (_portrait.Material != _material)
        {
            _portrait.Material = _material;
        }
    }

    private void ReleaseMaterial()
    {
        if (_portrait != null && GodotObject.IsInstanceValid(_portrait) && _portrait.Material == _material)
        {
            _portrait.Material = null;
        }
        _material = null;
    }

    private void ApplyUniforms(AnimatedArtMode mode)
    {
        if (_material == null || !GodotObject.IsInstanceValid(_material))
        {
            return;
        }

        _material.SetShaderParameter(ParamLoopPhase, _phase);
        _material.SetShaderParameter(ParamEffectStrength, _strength);
        _material.SetShaderParameter(ParamInspectWeight, _inspectWeight);
        _material.SetShaderParameter(ParamReducedMotion, mode == AnimatedArtMode.Reduced ? 1.0f : 0.0f);
    }

    private static T? FindAncestor<T>(Node start) where T : Node
    {
        for (var cur = start.GetParent(); cur != null; cur = cur.GetParent())
        {
            if (cur is T matched)
            {
                return matched;
            }
        }

        return null;
    }
}
