using BaseLib.Abstracts;
using BaseLib.Utils;
using BaseLib.Utils.NodeFactories;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch;

public sealed class TheWitch : PlaceholderCharacterModel
{
    public const string Art = MainFile.ResPath + "/images/witch";
    public const string Scenes = MainFile.ResPath + "/scenes/witch";
    public static readonly Color Crimson = new("cf3044");

    public override Color NameColor => Crimson;
    public override CharacterGender Gender => CharacterGender.Feminine;
    public override int StartingHp => 68;
    public override float DeathAnimTime => 0.5f;
    protected override IEnumerable<string> ExtraAssetPaths => base.ExtraAssetPaths.Concat(CrimsonFinalMirror.Assets);

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<WitchStrike>(), ModelDb.Card<WitchStrike>(), ModelDb.Card<WitchStrike>(), ModelDb.Card<WitchStrike>(),
        ModelDb.Card<WitchDefend>(), ModelDb.Card<WitchDefend>(), ModelDb.Card<WitchDefend>(), ModelDb.Card<WitchDefend>(),
        ModelDb.Card<ThinMirror>(), ModelDb.Card<StarterDraught>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<CrackedVanityMirror>()];
    public override CardPoolModel CardPool => ModelDb.CardPool<WitchCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<WitchRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<WitchPotionPool>();

    public override Color EnergyLabelOutlineColor => new("7f182b");
    public override string CustomEnergyCounterPath => $"{Scenes}/energy.tscn";
    public override string CustomVisualPath => $"{Scenes}/combat.tscn";
    public override string CustomCharacterSelectBg => $"{Scenes}/select.tscn";
    public override string CustomRestSiteAnimPath => $"{Scenes}/rest.tscn";
    public override string CustomMerchantAnimPath => $"{Scenes}/merchant.tscn";
    public override string CustomIconTexturePath => $"{Art}/witch_icon.svg";
    public override string CustomIconOutlineTexturePath => CustomIconTexturePath;
    public override string CustomCharacterSelectIconPath => $"{Art}/witch_portrait.png";
    public override string CustomCharacterSelectLockedIconPath => $"{Art}/witch_portrait_locked.png";
    public override string CustomMapMarkerPath => CustomIconTexturePath;

    public override Control CustomIcon
    {
        get
        {
            var icon = NodeFactory<Control>.CreateFromResource(CustomIconTexturePath);
            icon.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            return icon;
        }
    }
}

public sealed class WitchCardPool : CustomCardPoolModel
{
    public override string Title => "TheWitch";
    public override string BigEnergyIconPath => $"{TheWitch.Art}/witch_energy.svg";
    public override string TextEnergyIconPath => $"{TheWitch.Art}/witch_energy_inline.svg";
    public override Color DeckEntryCardColor => TheWitch.Crimson;
    public override float H => 0.975f;
    public override float S => 0.76f;
    public override float V => 0.81f;
    public override bool IsColorless => false;

    protected override IEnumerable<CardModel> FilterThroughEpochs(UnlockState unlockState, IEnumerable<CardModel> cards) =>
        base.FilterThroughEpochs(unlockState, cards).Where(card =>
            card.Rarity is CardRarity.Common or CardRarity.Uncommon or CardRarity.Rare);
}

public sealed class WitchRelicPool : CustomRelicPoolModel
{
    public override Color LabOutlineColor => TheWitch.Crimson;
    public override string BigEnergyIconPath => $"{TheWitch.Art}/witch_energy.svg";
    public override string TextEnergyIconPath => $"{TheWitch.Art}/witch_energy_inline.svg";
}

public sealed class WitchPotionPool : CustomPotionPoolModel
{
    public override Color LabOutlineColor => TheWitch.Crimson;
    public override string BigEnergyIconPath => $"{TheWitch.Art}/witch_energy.svg";
    public override string TextEnergyIconPath => $"{TheWitch.Art}/witch_energy_inline.svg";
}
