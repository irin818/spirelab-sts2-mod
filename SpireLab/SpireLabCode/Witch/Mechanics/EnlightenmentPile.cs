using BaseLib.Abstracts;
using BaseLib.Patches.Content;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

// The original card remains in CombatState with the same identity while held off the table.
public sealed class EnlightenmentPile() : CustomPile(HeldType)
{
    [CustomEnum]
    public static PileType HeldType;

    public override bool CardShouldBeVisible(CardModel card) => false;
    public override Vector2 GetTargetPosition(CardModel model, Vector2 size) => size / 2;
}
