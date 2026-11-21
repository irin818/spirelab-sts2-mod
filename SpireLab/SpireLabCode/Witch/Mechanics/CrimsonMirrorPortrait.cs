using Godot;
using MegaCrit.Sts2.Core.Assets;
using static SpireLab.SpireLabCode.Witch.Mechanics.CrimsonMirrorCamera;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal sealed class CrimsonMirrorPortrait
{
    private const string Art = TheWitch.Art + "/finisher/v2/";
    private static readonly string[] HandFrames = ["lift", "palm", "clench", "fist"];
    private static readonly Vector2[] PalmPixels = [new(499, 123), new(568, 115), new(544, 120), new(546, 91)];
    private static readonly Vector2 FacePixel = new(541, 469);
    private static readonly Vector2 HandOrigin = new(86, 474);
    internal static readonly Vector2 Face = new(824, 359);
    internal static readonly Vector2 ExpectedPalm = Face + new Vector2(323, 115);
    internal static IEnumerable<string> Assets => HandFrames.Select(frame => Art + frame + ".webp").Concat(
        [Art + "portrait.webp", Art + "hair-back.webp", Art + "hair-front.webp", Art + "portrait.gdshader"]);

    private readonly Node2D _rig = new() { Name = "WitchPortrait", Position = Face };
    private readonly Sprite2D _back;
    private readonly Sprite2D _front;
    private readonly Sprite2D _arm;
    private readonly ShaderMaterial _ink;
    private readonly Texture2D[] _hands = HandFrames.Select(LoadTexture).ToArray();
    private int _gestureIndex;

    public CrimsonMirrorPortrait(Node2D stage)
    {
        stage.AddChild(_rig);
        _back = Draw("hair-back", new Vector2(0, 628));
        var plate = Draw("portrait", Vector2.Zero);
        _ink = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(Art + "portrait.gdshader") };
        plate.Material = _ink;
        _front = Draw("hair-front", new Vector2(266, 449));
        _arm = new Sprite2D
        {
            Name = "CastingArm",
            Texture = _hands[0],
            Centered = false,
            Offset = -HandOrigin,
            Scale = Vector2.One * 0.43f,
            ZIndex = 1
        };
        _rig.AddChild(_arm);
    }

    public Vector2 Render(float time, float fade, float contraction, float snap, float shatterTime, float stagePan)
    {
        var arrival = Smooth(time, 0.70f, 1.13f);
        _rig.Visible = arrival * fade > 0.002f;
        var tint = new Color(0.80f, 0.74f, 0.90f).Lerp(new Color(1f, 0.61f, 0.71f), snap * 0.7f);
        _rig.Modulate = tint with { A = arrival * fade };
        var sway = Mathf.Sin(time * 4.8f + 0.4f);
        _rig.Position = Face + new Vector2(stagePan * -0.125f, 13 * (1 - arrival) + 2.1f * sway);
        _ink.SetShaderParameter("drift", sway);
        _ink.SetShaderParameter("pressure", contraction);
        _ink.SetShaderParameter("eyes", Smooth(time, 0.92f, 1.46f) * (0.74f + 0.26f * contraction));
        _back.Position = new Vector2(-3 * sway - 7 * contraction, 3 * Mathf.Cos(time * 4));
        _back.Rotation = 0.009f * sway;
        _front.Position = new Vector2(2.1f * sway + 1.3f * contraction, 2.8f * sway);
        _front.Rotation = -0.011f * sway;
        var lifting = Smooth(time, 0.83f, 1.22f);
        var lower = Smooth(time, 2.19f, 2.62f);
        var index = time switch
        {
            < 1.11f => 0,
            < 1.58f => 1,
            _ when time < shatterTime => 2,
            _ => 3
        };
        if (index != _gestureIndex)
        {
            _gestureIndex = index;
            _arm.Texture = _hands[index];
        }
        _arm.Position = new Vector2(123 - 12 * contraction, 277 - 20 * lifting + 32 * lower);
        _arm.Rotation = 0.49f * (1 - lifting + lower) - 0.095f * contraction - 0.06f * snap;
        _arm.Modulate = Colors.White with { A = Smooth(time, 0.82f, 1.06f) };
        var localPalm = _arm.Transform * (PalmPixels[index] - HandOrigin);
        return _rig.Position + localPalm;
    }

    private Sprite2D Draw(string asset, Vector2 pixelOffset)
    {
        var layer = new Sprite2D
        {
            Texture = LoadTexture(asset), Centered = false, Offset = pixelOffset - FacePixel,
            Scale = Vector2.One * 0.668f
        };
        _rig.AddChild(layer);
        return layer;
    }

    private static Texture2D LoadTexture(string name) => PreloadManager.Cache.GetTexture2D(Art + name + ".webp");
}
