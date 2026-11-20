using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Entities.Relics;

namespace SpireLab.SpireLabCode.Witch;

[Pool(typeof(WitchRelicPool))]
public sealed class CrackedVanityMirror : CustomRelicModel
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override string PackedIconPath => $"{TheWitch.Art}/witch_icon.svg";
    protected override string PackedIconOutlinePath => PackedIconPath;
    protected override string BigIconPath => PackedIconPath;
}
