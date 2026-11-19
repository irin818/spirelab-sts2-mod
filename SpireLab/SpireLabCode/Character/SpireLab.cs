using BaseLib.Abstracts;
using BaseLib.Utils.NodeFactories;
using Godot;
using MegaCrit.Sts2.Core.Entities.Characters;
using MegaCrit.Sts2.Core.Models;
using SpireLab.SpireLabCode.Cards;
using SpireLab.SpireLabCode.Relics;

namespace SpireLab.SpireLabCode.Character;

public sealed class SpireLab : PlaceholderCharacterModel
{
    public const string CharacterId = "SpireLab";
    public static readonly Color Color = new("85d3c5");
    private const string Scenes = MainFile.ResPath + "/scenes";
    private const string Art = MainFile.ResPath + "/images/surveyor";

    public override Color NameColor => Color;
    public override CharacterGender Gender => CharacterGender.Neutral;
    public override int StartingHp => 72;
    public override float DeathAnimTime => 0.5f;

    public override IEnumerable<CardModel> StartingDeck =>
    [
        ModelDb.Card<CaliperStrike>(), ModelDb.Card<CaliperStrike>(),
        ModelDb.Card<CaliperStrike>(), ModelDb.Card<CaliperStrike>(),
        ModelDb.Card<CeramicGuard>(), ModelDb.Card<CeramicGuard>(),
        ModelDb.Card<CeramicGuard>(), ModelDb.Card<CeramicGuard>(),
        ModelDb.Card<LightProbe>(), ModelDb.Card<ResonantRelease>()
    ];

    public override IReadOnlyList<RelicModel> StartingRelics => [ModelDb.Relic<SurveyLantern>()];
    public override CardPoolModel CardPool => ModelDb.CardPool<SpireLabCardPool>();
    public override RelicPoolModel RelicPool => ModelDb.RelicPool<SpireLabRelicPool>();
    public override PotionPoolModel PotionPool => ModelDb.PotionPool<SpireLabPotionPool>();

    public override string CustomVisualPath => $"{Scenes}/surveyor_combat.tscn";
    public override string CustomCharacterSelectBg => $"{Scenes}/surveyor_select.tscn";
    public override string CustomRestSiteAnimPath => $"{Scenes}/surveyor_rest.tscn";
    public override string CustomMerchantAnimPath => $"{Scenes}/surveyor_merchant.tscn";
    public override string CustomIconTexturePath => $"{Art}/surveyor_icon.png";
    public override string CustomIconOutlineTexturePath => $"{Art}/surveyor_icon.png";
    public override string CustomCharacterSelectIconPath => $"{Art}/surveyor_select_icon.png";
    public override string CustomCharacterSelectLockedIconPath => $"{Art}/surveyor_select_locked.png";
    public override string CustomMapMarkerPath => $"{Art}/surveyor_icon.png";

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
