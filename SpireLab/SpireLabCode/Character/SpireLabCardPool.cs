using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace SpireLab.SpireLabCode.Character;

public sealed class SpireLabCardPool : CustomCardPoolModel
{
    public override string Title => SpireLab.CharacterId;
    public override string BigEnergyIconPath => $"{MainFile.ResPath}/images/surveyor/energy.png";
    public override string TextEnergyIconPath => $"{MainFile.ResPath}/images/surveyor/energy.png";
    public override float H => 0.49f;
    public override float S => 0.65f;
    public override float V => 0.94f;
    public override Color DeckEntryCardColor => SpireLab.Color;
    public override bool IsColorless => false;

    // Generated cards belong to this character for rendering and lookup. They stay out of shops and rewards,
    // including effects that choose uniformly from unlocked cards without rolling a standard rarity.
    protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards) =>
        base.FilterThroughEpochs(unlockState, cards).Where(card =>
            card.Rarity is not (CardRarity.Token or CardRarity.Status or CardRarity.Curse));
}
