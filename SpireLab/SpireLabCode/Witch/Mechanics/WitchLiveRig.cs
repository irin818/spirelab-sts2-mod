using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

public static class WitchLiveRig
{
    public const string WitchShaderPath = $"{TheWitch.Art}/live/witch_live.gdshader";
    public const string WitchMaskPath = $"{TheWitch.Art}/live/witch_rig_masks.png";
    public const string MirrorShaderPath = $"{TheWitch.Art}/live/witch_mirror_live.gdshader";
    public const string MirrorMaskPath = $"{TheWitch.Art}/live/mirror_rig_masks.png";

    private const string ControllerName = "WitchLiveRigController";

    internal static readonly string[] AssetPaths =
    [
        WitchShaderPath,
        WitchMaskPath,
        MirrorShaderPath,
        MirrorMaskPath
    ];

    private static readonly HashSet<WitchLiveRigController> AllControllers = [];

    public static int ActiveControllerCount
    {
        get
        {
            AllControllers.RemoveWhere(c => !GodotObject.IsInstanceValid(c) || !c.IsInsideTree());
            var count = 0;
            foreach (var c in AllControllers)
            {
                if (c.IsRigActive)
                {
                    count++;
                }
            }
            return count;
        }
    }

    internal static void Register(WitchLiveRigController controller) => AllControllers.Add(controller);
    internal static void Unregister(WitchLiveRigController controller) => AllControllers.Remove(controller);

    public static void RefreshAllModes()
    {
        AllControllers.RemoveWhere(c => !GodotObject.IsInstanceValid(c) || !c.IsInsideTree());
        foreach (var controller in AllControllers.ToArray())
        {
            controller.RefreshMode();
        }
    }

    public static WitchLiveRigController? EnsureAttached(Creature? creature)
    {
        if (creature == null || DisplayServer.GetName() == "headless")
        {
            return null;
        }

        var visuals = creature.GetCreatureNode()?.Visuals;
        if (visuals == null || !GodotObject.IsInstanceValid(visuals))
        {
            return null;
        }

        var existing = visuals.GetNodeOrNull<WitchLiveRigController>(ControllerName);
        if (existing != null)
        {
            existing.RefreshMode();
            return existing;
        }

        var controller = new WitchLiveRigController { Name = ControllerName };
        visuals.AddChild(controller);
        return controller;
    }

    public static WitchLiveRigController? Find(Creature? creature)
    {
        var visuals = creature?.GetCreatureNode()?.Visuals;
        return visuals != null && GodotObject.IsInstanceValid(visuals)
            ? visuals.GetNodeOrNull<WitchLiveRigController>(ControllerName)
            : null;
    }

    public static void PulseAttack(Creature? creature) => EnsureAttached(creature)?.PulseAttack();
    public static void PulseGesture(Creature? creature, string? suit, bool isAttack) => EnsureAttached(creature)?.PulseGesture(suit, isAttack);
    public static void PulseCast(Creature? creature, string? suit) => EnsureAttached(creature)?.PulseCast(suit);
    public static void PulseCounterpoint(Creature? creature) => EnsureAttached(creature)?.PulseCounterpoint();
    public static void PulseDesire(Creature? creature) => EnsureAttached(creature)?.PulseDesire();
    public static void PulseBlock(Creature? creature) => EnsureAttached(creature)?.PulseBlock();
    public static void PulseReflect(Creature? creature) => EnsureAttached(creature)?.PulseReflect();
    public static void PulseHit(Creature? creature) => EnsureAttached(creature)?.PulseHit();
    public static void SetMirrorAwakened(Creature? creature, bool awakened) => EnsureAttached(creature)?.SetMirrorAwakened(awakened);
    public static void SetInscriptionLevel(Creature? creature, int baseReflectPercent) =>
        EnsureAttached(creature)?.SetInscriptionLevel(baseReflectPercent);
    public static void SetLowHealth(Creature? creature, bool lowHealth) => EnsureAttached(creature)?.SetLowHealth(lowHealth);
}

public sealed partial class WitchLiveRigController : Node
{
    private static readonly Color RimPotion = new("cf486a");
    private static readonly Color RimMirror = new("cdd8e2");
    private static readonly Color RimGrimoire = new("eadde1");
    private static readonly Color RimDesire = new("b52b4f");

    private Sprite2D? _witchSprite;
    private Sprite2D? _mirrorSprite;
    private AnimationPlayer? _animPlayer;
    private ShaderMaterial? _witchMat;
    private ShaderMaterial? _mirrorMat;

    private float _timeSec;
    private string _lastAnim = "";

    // Spring-damper physical state (Live2D physics feel)
    private float _bodyLean;
    private float _bodyLeanVel;
    private float _headExtraTilt;
    private float _headExtraTiltVel;
    private float _hairLag;
    private float _hairLagVel;
    private float _skirtLag;
    private float _skirtLagVel;

    // Expression & arcane glow state
    private float _blinkTimer = 2.6f;
    private float _blinkPhase = -1f; // <0 idle, 0..1 active blink
    private bool _doubleBlinkPending;
    private float _eyeGlowTarget = 0.20f;
    private float _eyeGlow = 0.20f;
    private float _castSurge;
    private float _rimStrength;
    private Color _rimColor = RimMirror;
    private float _mirrorAwakenedTarget;
    private float _mirrorAwakened;
    private float _mirrorPulse;
    private float _inscriptionGlowTarget;
    private float _inscriptionGlow;
    private bool _lowHealth;

    private bool _isActive;
    private AnimatedArtMode _appliedMode = AnimatedArtMode.Static;

    public bool IsRigActive => _isActive;
    public int FrameCount { get; private set; }
    public int BlinkCount { get; private set; }
    public string LastImpulseName { get; private set; } = "none";

    public override void _Ready()
    {
        var parent = GetParent();
        _witchSprite = parent?.GetNodeOrNull<Sprite2D>("Visuals");
        _mirrorSprite = parent?.GetNodeOrNull<Sprite2D>("MirrorFormVisual");
        _animPlayer = parent?.GetNodeOrNull<AnimationPlayer>("AnimationPlayer");

        WitchLiveRig.Register(this);
        RefreshMode();
    }

    public override void _ExitTree()
    {
        DetachMaterials();
        WitchLiveRig.Unregister(this);
    }

    public void RefreshMode()
    {
        var mode = WitchAnimatedCardArt.Mode;
        if (mode == AnimatedArtMode.Static || DisplayServer.GetName() == "headless" || _witchSprite == null)
        {
            DetachMaterials();
            SetProcess(false);
            _isActive = false;
            _appliedMode = AnimatedArtMode.Static;
            return;
        }

        if (!_isActive || _appliedMode != mode)
        {
            AttachMaterials(mode);
        }
    }

    private void AttachMaterials(AnimatedArtMode mode)
    {
        if (_witchSprite == null || !GodotObject.IsInstanceValid(_witchSprite))
        {
            return;
        }

        var witchShader = ResourceLoader.Load<Shader>(WitchLiveRig.WitchShaderPath);
        var witchMask = ResourceLoader.Load<Texture2D>(WitchLiveRig.WitchMaskPath);
        if (witchShader == null || witchMask == null)
        {
            return;
        }

        _witchMat ??= new ShaderMaterial { Shader = witchShader };
        _witchMat.SetShaderParameter("rig_mask_tex", witchMask);
        _witchMat.SetShaderParameter("motion_scale", mode == AnimatedArtMode.Reduced ? 0.42f : 1.0f);
        _witchSprite.Material = _witchMat;

        if (_mirrorSprite != null && GodotObject.IsInstanceValid(_mirrorSprite))
        {
            var mirrorShader = ResourceLoader.Load<Shader>(WitchLiveRig.MirrorShaderPath);
            var mirrorMask = ResourceLoader.Load<Texture2D>(WitchLiveRig.MirrorMaskPath);
            if (mirrorShader != null && mirrorMask != null)
            {
                _mirrorMat ??= new ShaderMaterial { Shader = mirrorShader };
                _mirrorMat.SetShaderParameter("mirror_mask_tex", mirrorMask);
                _mirrorMat.SetShaderParameter("motion_scale", mode == AnimatedArtMode.Reduced ? 0.42f : 1.0f);
                _mirrorSprite.Material = _mirrorMat;
            }
        }

        _appliedMode = mode;
        _isActive = true;
        WitchLiveRig.Register(this);
        SetProcess(true);
        PushUniforms();
    }

    private void DetachMaterials()
    {
        if (_witchSprite != null && GodotObject.IsInstanceValid(_witchSprite) && _witchSprite.Material == _witchMat)
        {
            _witchSprite.Material = null;
        }

        if (_mirrorSprite != null && GodotObject.IsInstanceValid(_mirrorSprite) && _mirrorSprite.Material == _mirrorMat)
        {
            _mirrorSprite.Material = null;
        }

        _witchMat = null;
        _mirrorMat = null;
    }

    public void PulseAttack()
    {
        LastImpulseName = "attack";
        _bodyLeanVel += 6.2f;
        _headExtraTiltVel += 1.4f;
        _hairLagVel -= 5.8f;
        _skirtLagVel -= 4.6f;
        _castSurge = Mathf.Max(_castSurge, 0.55f);
        _eyeGlow = Mathf.Max(_eyeGlow, 0.72f);
        _rimColor = RimPotion;
        _rimStrength = Mathf.Max(_rimStrength, 0.55f);
    }

    public void PulseCast(string? suit)
    {
        LastImpulseName = $"cast:{suit ?? "basic"}";
        _bodyLeanVel -= 2.2f;
        _headExtraTiltVel -= 1.0f;
        _hairLagVel += 3.8f;
        _skirtLagVel += 3.5f;
        _castSurge = 1.0f;
        _eyeGlow = Mathf.Max(_eyeGlow, 0.85f);
        _rimColor = suit switch
        {
            "potion" => RimPotion,
            "mirror" => RimMirror,
            "grimoire" => RimGrimoire,
            "desire" => RimDesire,
            _ => RimMirror
        };
        _rimStrength = Mathf.Max(_rimStrength, 0.72f);
        if (suit == "mirror" || suit == "desire")
        {
            _mirrorPulse = Mathf.Max(_mirrorPulse, 0.65f);
        }
    }

    public void PulseCounterpoint()
    {
        LastImpulseName = "counterpoint";
        _headExtraTiltVel += 1.8f;
        _hairLagVel += 4.2f;
        _skirtLagVel -= 3.8f;
        _castSurge = 1.0f;
        _eyeGlow = Mathf.Max(_eyeGlow, 1.05f);
        _rimColor = RimPotion;
        _rimStrength = 0.92f;
        _mirrorPulse = Mathf.Max(_mirrorPulse, 0.75f);
    }

    public void PulseDesire()
    {
        LastImpulseName = "desire";
        _headExtraTiltVel -= 1.6f;
        _hairLagVel += 5.0f;
        _castSurge = 1.0f;
        _eyeGlow = 1.35f;
        _rimColor = RimDesire;
        _rimStrength = 1.0f;
        _mirrorPulse = 1.0f;
    }

    public void PulseBlock()
    {
        LastImpulseName = "block";
        _bodyLeanVel -= 3.0f;
        _hairLagVel += 2.6f;
        _skirtLagVel += 2.8f;
        _rimColor = RimMirror;
        _rimStrength = Mathf.Max(_rimStrength, 0.78f);
        _mirrorPulse = Mathf.Max(_mirrorPulse, 0.72f);
    }

    public void PulseReflect()
    {
        LastImpulseName = "reflect";
        _eyeGlow = Mathf.Max(_eyeGlow, 1.0f);
        _rimColor = RimMirror;
        _rimStrength = 0.95f;
        _mirrorPulse = 1.0f;
    }

    public void PulseHit()
    {
        LastImpulseName = "hit";
        _bodyLeanVel -= 7.8f;
        _headExtraTiltVel -= 2.2f;
        _hairLagVel += 7.5f;
        _skirtLagVel += 6.2f;
        TriggerBlinkNow();
    }

    public void SetMirrorAwakened(bool awakened)
    {
        _mirrorAwakenedTarget = awakened ? 1.0f : 0.0f;
        if (awakened)
        {
            LastImpulseName = "mirror_awakened";
            _mirrorPulse = 1.0f;
            _eyeGlow = Mathf.Max(_eyeGlow, 1.15f);
            _rimColor = RimMirror;
            _rimStrength = 0.90f;
        }
    }

    public void PulseGesture(string? suit, bool isAttack)
    {
        // Four reusable Live2D character motion profiles (Section 5.1):
        // Potion = graceful cast lift, Mirror = crisp fingertip slice, Grimoire = calm page-turn nod, Desire = mirror resonance
        switch (suit)
        {
            case "potion":
                LastImpulseName = isAttack ? "gesture:potion_throw" : "gesture:potion_cast";
                _bodyLeanVel += isAttack ? 5.4f : -1.8f;
                _headExtraTiltVel += isAttack ? 1.1f : -0.9f;
                _hairLagVel += isAttack ? -4.8f : 3.2f;
                _skirtLagVel += isAttack ? -3.8f : 2.8f;
                _castSurge = 0.95f;
                _eyeGlow = Mathf.Max(_eyeGlow, 0.80f);
                _rimColor = RimPotion;
                _rimStrength = Mathf.Max(_rimStrength, 0.72f);
                break;
            case "mirror":
                LastImpulseName = isAttack ? "gesture:mirror_slice" : "gesture:mirror_ward";
                _bodyLeanVel += isAttack ? 6.4f : -2.6f;
                _headExtraTiltVel += isAttack ? 1.5f : -0.6f;
                _hairLagVel += isAttack ? -5.6f : 2.8f;
                _skirtLagVel += isAttack ? -4.4f : 2.6f;
                _castSurge = 0.82f;
                _eyeGlow = Mathf.Max(_eyeGlow, 0.78f);
                _rimColor = RimMirror;
                _rimStrength = Mathf.Max(_rimStrength, 0.82f);
                _mirrorPulse = Mathf.Max(_mirrorPulse, 0.72f);
                break;
            case "grimoire":
                LastImpulseName = isAttack ? "gesture:grimoire_cast" : "gesture:grimoire_turn";
                _bodyLeanVel += isAttack ? 3.2f : -1.2f;
                _headExtraTiltVel -= 1.35f;
                _hairLagVel += 2.6f;
                _skirtLagVel += 2.2f;
                _castSurge = 0.90f;
                _eyeGlow = Mathf.Max(_eyeGlow, 0.72f);
                _rimColor = RimGrimoire;
                _rimStrength = Mathf.Max(_rimStrength, 0.68f);
                break;
            case "desire":
                PulseDesire();
                break;
            default:
                if (isAttack)
                {
                    PulseAttack();
                }
                else
                {
                    PulseCast(null);
                }
                break;
        }
    }

    public void SetInscriptionLevel(int baseReflectPercent)
    {
        _inscriptionGlowTarget = Mathf.Clamp(baseReflectPercent / 100f, 0f, 1f);
        if (baseReflectPercent > 0)
        {
            _mirrorPulse = Mathf.Max(_mirrorPulse, 0.45f);
        }
    }

    public void SetLowHealth(bool lowHealth)
    {
        _lowHealth = lowHealth;
        _eyeGlowTarget = lowHealth ? 0.58f : 0.20f;
    }

    public void TriggerBlinkNow()
    {
        if (_blinkPhase < 0f)
        {
            _blinkPhase = 0f;
            BlinkCount++;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isActive || _witchMat == null)
        {
            return;
        }

        var dt = (float)Mathf.Clamp(delta, 0.0005, 0.05);
        _timeSec += dt;
        FrameCount++;

        // Observe native AnimationPlayer transitions so even vanilla-triggered attacks/hits get Live2D spring follow-through
        if (_animPlayer != null && GodotObject.IsInstanceValid(_animPlayer))
        {
            var current = _animPlayer.CurrentAnimation ?? "";
            if (current != _lastAnim)
            {
                if (current == "attack")
                {
                    PulseAttack();
                }
                else if (current == "hit")
                {
                    PulseHit();
                }
                _lastAnim = current;
            }
        }

        // 1. Damped harmonic springs (true Live2D physics lag & organic settle)
        StepSpring(ref _bodyLean, ref _bodyLeanVel, 0f, stiffness: 52f, damping: 9.2f, dt);
        StepSpring(ref _headExtraTilt, ref _headExtraTiltVel, 0f, stiffness: 46f, damping: 8.5f, dt);

        // Hair and skirt naturally react to body acceleration/lean with secondary inertia
        var hairTarget = -_bodyLean * 0.85f;
        var skirtTarget = -_bodyLean * 0.70f;
        StepSpring(ref _hairLag, ref _hairLagVel, hairTarget, stiffness: 28f, damping: 5.4f, dt);
        StepSpring(ref _skirtLag, ref _skirtLagVel, skirtTarget, stiffness: 24f, damping: 5.0f, dt);

        // 2. Smooth exponential decay for glow & pulses
        _eyeGlow = Mathf.Lerp(_eyeGlow, _eyeGlowTarget, 1f - Mathf.Exp(-dt * 4.5f));
        _castSurge = Mathf.Max(0f, _castSurge - dt * 2.1f);
        _rimStrength = Mathf.Max(0f, _rimStrength - dt * 2.4f);
        _mirrorPulse = Mathf.Max(0f, _mirrorPulse - dt * 1.9f);
        _mirrorAwakened = Mathf.Lerp(_mirrorAwakened, _mirrorAwakenedTarget, 1f - Mathf.Exp(-dt * 2.6f));
        _inscriptionGlow = Mathf.Lerp(_inscriptionGlow, _inscriptionGlowTarget, 1f - Mathf.Exp(-dt * 3.0f));

        // 3. Natural randomized blinking (with occasional quick double-blink)
        if (_blinkPhase >= 0f)
        {
            _blinkPhase += dt / 0.165f;
            if (_blinkPhase >= 1f)
            {
                _blinkPhase = -1f;
                if (_doubleBlinkPending)
                {
                    _doubleBlinkPending = false;
                    _blinkTimer = 0.12f;
                }
                else
                {
                    // Deterministic-friendly pseudo-random cadence based on time & frame
                    var hash = Mathf.PosMod(Mathf.Sin(_timeSec * 12.9898f + BlinkCount * 78.233f) * 43758.5453f, 1.0f);
                    _blinkTimer = 3.1f + hash * 2.3f;
                    _doubleBlinkPending = hash > 0.78f;
                }
            }
        }
        else
        {
            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                _blinkPhase = 0f;
                BlinkCount++;
            }
        }

        PushUniforms();
    }

    private void PushUniforms()
    {
        if (_witchMat == null)
        {
            return;
        }

        // Irrational-ratio multi-harmonic idle curves so she never feels robotic
        var breathRate = _lowHealth ? 2.35f : 1.85f;
        var breath = Mathf.Sin(_timeSec * breathRate) * 0.85f + Mathf.Sin(_timeSec * breathRate * 2.0f - 0.6f) * 0.15f;
        var idleTilt = Mathf.Sin(_timeSec * 0.92f + 0.4f) * 0.019f + Mathf.Sin(_timeSec * 1.51f) * 0.008f;
        var headTilt = Mathf.Clamp(idleTilt + _headExtraTilt * 0.045f, -0.065f, 0.065f);
        var headNod = Mathf.Sin(_timeSec * breathRate - 0.55f) * 0.0022f - _castSurge * 0.0024f;

        // Asymmetric blink envelope (fast close 0..0.38, brief hold, smooth open 0.52..1.0)
        var blink = 0f;
        if (_blinkPhase >= 0f)
        {
            blink = _blinkPhase < 0.38f
                ? SmoothStep(0f, 0.38f, _blinkPhase)
                : _blinkPhase < 0.52f
                    ? 1f
                    : 1f - SmoothStep(0.52f, 1f, _blinkPhase);
        }

        _witchMat.SetShaderParameter("time_sec", _timeSec);
        _witchMat.SetShaderParameter("breath", breath);
        _witchMat.SetShaderParameter("body_lean", Mathf.Clamp(_bodyLean + Mathf.Sin(_timeSec * 0.78f) * 0.14f, -1.2f, 1.2f));
        _witchMat.SetShaderParameter("head_tilt", headTilt);
        _witchMat.SetShaderParameter("head_nod", headNod);
        _witchMat.SetShaderParameter("hair_lag", Mathf.Clamp(_hairLag, -1.5f, 1.5f));
        _witchMat.SetShaderParameter("skirt_lag", Mathf.Clamp(_skirtLag, -1.5f, 1.5f));
        _witchMat.SetShaderParameter("blink", blink);
        _witchMat.SetShaderParameter("eye_glow", _eyeGlow);
        _witchMat.SetShaderParameter("cast_surge", _castSurge);
        _witchMat.SetShaderParameter("rim_color", new Vector3(_rimColor.R, _rimColor.G, _rimColor.B));
        _witchMat.SetShaderParameter("rim_strength", _rimStrength);

        if (_mirrorMat != null)
        {
            // Asynchronous phase vs real Witch (out-of-phase uncanny reflection!)
            var mBreath = Mathf.Sin(_timeSec * 1.52f + 2.15f);
            var mSway = Mathf.Sin(_timeSec * 0.88f + 1.35f) * 0.75f + Mathf.Sin(_timeSec * 1.63f) * 0.25f;
            _mirrorMat.SetShaderParameter("time_sec", _timeSec);
            _mirrorMat.SetShaderParameter("mirror_breath", mBreath);
            _mirrorMat.SetShaderParameter("mirror_sway", mSway);
            _mirrorMat.SetShaderParameter("mirror_awakened", _mirrorAwakened);
            _mirrorMat.SetShaderParameter("mirror_pulse", _mirrorPulse);
            _mirrorMat.SetShaderParameter("inscription_glow", _inscriptionGlow);
        }
    }

    private static void StepSpring(ref float value, ref float velocity, float target, float stiffness, float damping, float dt)
    {
        var force = (target - value) * stiffness - velocity * damping;
        velocity += force * dt;
        value += velocity * dt;
    }

    private static float SmoothStep(float e0, float e1, float x)
    {
        var t = Mathf.Clamp((x - e0) / (e1 - e0), 0f, 1f);
        return t * t * (3f - 2f * t);
    }
}
