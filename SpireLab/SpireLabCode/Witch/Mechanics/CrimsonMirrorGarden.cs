using Godot;
using MegaCrit.Sts2.Core.Assets;
using static SpireLab.SpireLabCode.Witch.Mechanics.CrimsonMirrorCamera;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal sealed class CrimsonMirrorGarden
{
    internal const string Art = TheWitch.Art + "/finisher/v4/";
    private static readonly string[] Plates = ["mirror-garden", "rose-vine", "rose-sprig", "petals", "rose-glass"];
    internal static IEnumerable<string> Assets => Plates.Select(name => Art + name + ".webp")
        .Append(Art + "sleeve.gdshader");

    private readonly Node2D _garden = new() { Name = "LivingMirrorGarden" };
    private readonly Node2D _reflections = new() { Name = "PeripheralReflections" };
    private readonly Sprite2D[] _vines = new Sprite2D[4];
    private readonly Sprite2D[] _glass = new Sprite2D[2];
    private readonly Sprite2D[] _echoes = new Sprite2D[2];
    private readonly Sprite2D _petals;

    public CrimsonMirrorGarden(Node2D stage)
    {
        stage.AddChild(_reflections);
        stage.AddChild(_garden);
        var expression = PreloadManager.Cache.GetTexture2D(TheWitch.Art + "/finisher/v3/expression.webp");
        for (var i = 0; i < 2; i++)
        {
            _vines[i * 2] = Sprite(_garden, Texture("rose-vine"));
            _vines[i * 2 + 1] = Sprite(_garden, Texture("rose-sprig"));
            _glass[i] = Sprite(_reflections, Texture("rose-glass"));
            _echoes[i] = Sprite(_reflections, expression);
            _reflections.MoveChild(_echoes[i], _glass[i].GetIndex());
            _vines[i * 2].FlipH = _vines[i * 2 + 1].FlipH = _glass[i].FlipH = _echoes[i].FlipH = i == 1;
        }
        _petals = Sprite(_garden, Texture("petals"));
    }

    public void Render(StageFrame view, float time, float reveal, float fade, float pull, float impact)
    {
        view.Apply(_reflections, 0.28f);
        view.Apply(_garden, 0.49f);
        var repose = 1 - 0.46f * Smooth(time, 1.52f, 1.95f) * (1 - Smooth(time, 2.06f, 2.26f));
        var presence = reveal * fade * repose;
        _garden.Visible = _reflections.Visible = presence > 0.002f;
        for (var i = 0; i < 2; i++)
        {
            var side = i == 0 ? -1 : 1;
            var lag = time - 0.14f * i;
            var curl = Mathf.Sin(lag * 3.2f) * 0.10f;
            var root = new Vector2(960 + side * 835, 807 - i * 37);
            var stem = _vines[i * 2];
            stem.Position = root.Lerp(MirrorCenter, pull * 0.31f) + new Vector2(0, 13 * Mathf.Sin(lag * 3.1f));
            stem.Scale = new Vector2(0.74f + pull * 0.16f, 0.66f - pull * 0.10f);
            stem.Rotation = side * (-0.32f + curl - pull * 0.41f);
            stem.Modulate = new Color(0.77f, 0.61f, 0.77f, presence * 0.69f);

            var bloom = _vines[i * 2 + 1];
            bloom.Position = new Vector2(960 + side * 792, 333 + 39 * i).Lerp(MirrorCenter, pull * 0.26f);
            bloom.Scale = Vector2.One * (0.43f + 0.06f * Mathf.Sin(lag * 4.4f) + 0.07f * pull);
            bloom.Rotation = side * (curl - pull * 0.37f);
            bloom.Modulate = new Color(0.85f, 0.63f, 0.78f, presence * (0.49f + 0.21f * impact));

            var pane = _glass[i];
            pane.Position = new Vector2(960 + side * (661 - 13 * Mathf.Sin(lag * 2.0f)), 495 + 22 * i)
                .Lerp(MirrorCenter, pull * 0.24f);
            pane.Scale = new Vector2(0.58f + 0.07f * Mathf.Cos(lag * 3.0f), 0.58f - 0.11f * pull);
            pane.Rotation = side * (0.19f + curl + 0.16f * pull);
            pane.Modulate = new Color(0.78f, 0.66f, 0.88f, presence * 0.59f);

            var echo = _echoes[i];
            var glance = Smooth(lag, 1.27f, 1.77f) * (1 - Smooth(lag, 2.08f, 2.54f));
            echo.Position = pane.Position + new Vector2(-side * (46 + 14 * Mathf.Sin(lag * 4.3f)), 18);
            echo.Scale = Vector2.One * (0.40f + 0.06f * glance);
            echo.Rotation = pane.Rotation - side * (0.30f + curl);
            echo.Modulate = new Color(0.70f, 0.37f, 0.72f, reveal * fade * glance * 0.52f);
        }

        _petals.Position = new Vector2(1518 - 42 * time, 820 - 55 * time).Lerp(MirrorCenter, pull * 0.86f);
        _petals.Rotation = 0.28f * time + pull * 0.78f;
        _petals.Scale = Vector2.One * (0.58f - 0.37f * pull + 0.31f * impact);
        _petals.Modulate = new Color(0.78f, 0.53f, 0.71f, presence * 0.63f);
    }

    private static Texture2D Texture(string name) => PreloadManager.Cache.GetTexture2D(Art + name + ".webp");

    private static Sprite2D Sprite(Node2D parent, Texture2D texture)
    {
        var sprite = new Sprite2D { Texture = texture, TextureFilter = CanvasItem.TextureFilterEnum.LinearWithMipmaps };
        parent.AddChild(sprite);
        return sprite;
    }
}
