using Godot;
using MegaCrit.Sts2.Core.Assets;
using static SpireLab.SpireLabCode.Witch.Mechanics.CrimsonMirrorCamera;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal sealed class CrimsonMirrorPortrait
{
    private const string Art = TheWitch.Art + "/finisher/v3/";
    private static readonly string[] HandFrames = ["reach", "palm", "curl", "fist"];
    private static readonly Vector2[] PalmPixels = [new(292, 298), new(337, 322), new(275, 319), new(199, 232)];
    private static readonly Vector2 FacePixel = new(1046, 279);
    private static readonly Vector2 HandOrigin = new(575, 580);
    internal static readonly Vector2 Face = new(960, 320);
    internal static IEnumerable<string> Assets => HandFrames.Select(frame => Art + frame + ".webp").Concat(
        [Art + "portrait.webp", Art + "hair-back.webp", Art + "hair-front.webp", Art + "expression.webp",
         Art + "portrait.gdshader", Art + "depth_focus.gdshader"]);

    private readonly Node2D _rig = new() { Name = "WitchPortrait", Position = Face };
    private readonly Sprite2D _back;
    private readonly Sprite2D _front;
    private readonly Sprite2D _arm;
    private readonly Sprite2D _armBlend;
    private readonly ShaderMaterial _ink;
    private readonly ShaderMaterial _hairInk;
    private readonly ShaderMaterial _cuff;
    private readonly Texture2D[] _hands = HandFrames.Select(LoadTexture).ToArray();
    private int _gestureIndex;

    public CrimsonMirrorPortrait(Node2D portraitPlane, Node2D handPlane)
    {
        portraitPlane.AddChild(_rig);
        _back = Draw("hair-back", new Vector2(276, 267));
        var plate = Draw("portrait", Vector2.Zero);
        plate.Name = "PortraitPlate";
        _ink = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(Art + "portrait.gdshader") };
        _ink.SetShaderParameter("expression_tex", LoadTexture("expression"));
        _ink.SetShaderParameter("portrait_aspect", (float)plate.Texture.GetWidth() / plate.Texture.GetHeight());
        plate.Material = _ink;
        _front = Draw("hair-front", new Vector2(700, 216));
        _hairInk = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(Art + "depth_focus.gdshader") };
        _back.Material = _hairInk;
        _front.Material = _hairInk;
        _arm = Hand("CastingArm", _hands[0]);
        _armBlend = Hand("CastingArmBlend", _hands[1]);
        _cuff = new ShaderMaterial { Shader = PreloadManager.Cache.GetAsset<Shader>(CrimsonMirrorGarden.Art + "sleeve.gdshader") };
        _arm.Material = _armBlend.Material = _cuff;
        handPlane.AddChild(_arm);
        handPlane.AddChild(_armBlend);
    }

    public Vector2 Render(float time, float fade, float flash, float focus, float shatterTime)
    {
        var arrival = Smooth(time, 1.01f, 1.52f);
        var gaze = Smooth(time, 1.53f, 1.91f);
        var lids = Smooth(time, 1.60f, 1.90f);
        var brow = Smooth(time, 1.64f, 2.02f);
        var smile = Smooth(time, 1.72f, 2.12f);
        var reach = Smooth(time, 2.16f, 2.64f);
        var afterglow = Smooth(time, 2.91f, 3.42f);
        var erosion = Smooth(time, 2.22f, shatterTime + 0.065f);
        _rig.Visible = arrival * fade > 0.002f && erosion < 0.9995f;
        _rig.Modulate = new Color(0.95f, 0.85f, 0.94f).Lerp(new Color(1, 0.62f, 0.74f), 0.45f * flash)
            with { A = arrival * fade };
        var breath = Mathf.Sin(time * 4.4f - 0.8f);
        var tilt = 0.073f * Smooth(time, 1.68f, 2.09f) - 0.046f * Smooth(time, 2.24f, 2.65f);
        _rig.Position = Face + new Vector2(1.8f * breath, 18f * (1 - arrival) - 6f * gaze + 2.2f * breath);
        _ink.SetShaderParameter("tilt", tilt + 0.005f * breath);
        _ink.SetShaderParameter("breath", breath);
        _ink.SetShaderParameter("lean", reach * (1 - afterglow));
        _ink.SetShaderParameter("acting", new Vector3(gaze, lids, brow));
        _ink.SetShaderParameter("smile", smile);
        _ink.SetShaderParameter("defocus", focus);
        _ink.SetShaderParameter("erosion", erosion);
        _ink.SetShaderParameter("eyes", (0.24f + 0.62f * gaze) * (1f - 0.65f * afterglow));
        _ink.SetShaderParameter("silver", flash);
        _hairInk.SetShaderParameter("defocus", focus + 0.12f);
        _hairInk.SetShaderParameter("erosion", Smooth(time, 2.19f, shatterTime - 0.08f));
        var backSway = Mathf.Sin(time * 4.2f - 0.65f);
        var frontSway = Mathf.Sin(time * 5.8f + 0.46f);
        _back.Position = new Vector2(-6 * backSway - 8 * reach, 2.7f * breath + 4.0f * backSway);
        _back.Rotation = 0.009f * backSway;
        _front.Position = new Vector2(4 * frontSway + 4 * reach, 2.8f * frontSway - 2 * gaze);
        _front.Rotation = -0.012f * frontSway + 0.007f * gaze;

        var index = time switch { < 2.39f => 0, < 2.64f => 1, _ when time < shatterTime => 2, _ => 3 };
        if (index != _gestureIndex)
        {
            _gestureIndex = index;
            _arm.Texture = _hands[index];
        }

        var retreat = Smooth(time, 2.97f, 3.54f);
        var armAlpha = Smooth(time, 2.13f, 2.38f) * (1 - Smooth(time, shatterTime + 0.13f, 3.30f)) * fade;
        _cuff.SetShaderParameter("recoil", flash);
        _arm.Position = Face + new Vector2(120 + 18 * reach + (index == 3 ? 39 : 0) - 58 * retreat, 365 + 40 * retreat - 8 * reach);
        _arm.Scale = Vector2.One * (0.45f + 0.49f * reach - 0.20f * retreat + 0.034f * flash);
        _arm.Rotation = -0.30f * (1 - reach) + 0.041f * reach + 0.043f * flash - 0.26f * retreat;
        var blend = index switch
        {
            0 => Smooth(time, 2.335f, 2.390f),
            1 => Smooth(time, 2.584f, 2.640f),
            _ => 0f
        };
        var next = Mathf.Min(index + 1, HandFrames.Length - 1);
        _arm.Visible = armAlpha > 0.003f;
        _arm.Modulate = Colors.White with { A = armAlpha * (1 - blend) };
        _armBlend.Visible = _arm.Visible && blend > 0.001f;
        if (_armBlend.Visible)
        {
            if (_armBlend.Texture != _hands[next])
            {
                _armBlend.Texture = _hands[next];
            }
            _armBlend.Transform = _arm.Transform;
            _armBlend.Modulate = Colors.White with { A = armAlpha * blend };
        }
        return _arm.Transform * (PalmPixels[index].Lerp(PalmPixels[next], blend) - HandOrigin);
    }

    private static Sprite2D Hand(string name, Texture2D texture) =>
        new() { Name = name, Texture = texture, Centered = false, Offset = -HandOrigin,
            TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };

    private Sprite2D Draw(string asset, Vector2 pixelOffset)
    {
        var layer = new Sprite2D
        {
            Texture = LoadTexture(asset), Centered = false, Offset = pixelOffset - FacePixel,
            Scale = Vector2.One * 0.725f, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps
        };
        _rig.AddChild(layer);
        return layer;
    }

    private static Texture2D LoadTexture(string name) => PreloadManager.Cache.GetTexture2D(Art + name + ".webp");
}
