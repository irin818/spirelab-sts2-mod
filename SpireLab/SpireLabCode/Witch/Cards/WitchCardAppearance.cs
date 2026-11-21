using System.Globalization;
using BaseLib.Patches.UI;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.UI;

namespace SpireLab.SpireLabCode.Witch.Cards;

internal static class WitchCardAppearance
{
    public static Material? Frame(CardRarity rarity)
    {
        if (rarity == CardRarity.Ancient)
        {
            return null;
        }

        var trim = rarity switch
        {
            CardRarity.Basic => new Color("aaaaac"),
            CardRarity.Common => new Color("ded0c8"),
            CardRarity.Uncommon => new Color("bac7d6"),
            _ => new Color("d36b83")
        };
        var material = new ShaderMaterial
        {
            Shader = GD.Load<Shader>($"{TheWitch.Art}/frames/gothic_frame.gdshader")
        };
        material.SetShaderParameter("trim", trim);
        return material;
    }

    public static void RegisterSuitBadges()
    {
        ExtraCardUi.RegisterCreateCardUiElement(ExtraCardUi.CardUiPositioning.Left,
            _ => new TextureRect
            {
                Name = "WitchSuit",
                Size = new Vector2(38, 38),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                MouseFilter = Control.MouseFilterEnum.Ignore,
                Visible = false
            },
            (node, model, badge) =>
            {
                if (node.Visibility != ModelVisibility.Visible || model is not WitchCard witch || Suit(witch) is not { } suit)
                {
                    return false;
                }

                badge.Texture = GD.Load<Texture2D>($"{TheWitch.Art}/suits/{suit}.svg");
                return true;
            });
    }

    internal static string? Suit(WitchCard card)
    {
        if (card.IsDesire)
        {
            return "desire";
        }

        var number = int.Parse(card.SpecId.AsSpan(1), CultureInfo.InvariantCulture);
        return card.SpecId[0] switch
        {
            'B' when number == 3 => "mirror",
            'B' when number == 4 => "potion",
            'C' or 'R' when number <= 9 => "potion",
            'C' or 'R' when number <= 18 => "mirror",
            'U' when number <= 15 => "potion",
            'U' when number <= 29 => "mirror",
            'C' or 'U' or 'R' => "grimoire",
            'A' => number == 1 ? "mirror" : "potion",
            _ => null
        };
    }
}
