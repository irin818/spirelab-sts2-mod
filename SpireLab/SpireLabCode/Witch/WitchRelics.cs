using BaseLib.Abstracts;
using BaseLib.Utils;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.SpireLabCode.Witch;

[Pool(typeof(WitchRelicPool))]
public abstract class WitchRelic(string art) : CustomRelicModel
{
    public override string PackedIconPath => $"{TheWitch.Art}/relics/{art}.png";
    protected override string PackedIconOutlinePath => $"{TheWitch.Art}/relics/{art}_outline.png";
    protected override string BigIconPath => $"{TheWitch.Art}/relics/big/{art}.png";

    internal Task<decimal> FlashAndBlock(int amount)
    {
        Flash();
        return CreatureCmd.GainBlock(Owner.Creature, amount, ValueProp.Unpowered, null);
    }
}

public sealed class CrackedVanityMirror() : WitchRelic("cracked_vanity_mirror")
{
    public override RelicRarity Rarity => RelicRarity.Starter;
    public override RelicModel? GetUpgradeReplacement() => ModelDb.Relic<TwinStandingMirror>();
}

public sealed class TwinStandingMirror() : WitchRelic("twin_standing_mirror")
{
    public override RelicRarity Rarity => RelicRarity.Ancient;
}

public sealed class CrackedPhial() : WitchRelic("cracked_phial")
{
    public override RelicRarity Rarity => RelicRarity.Common;
}

public sealed class SilverMirrorFrame() : WitchRelic("silver_mirror_frame")
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
}

public sealed class LostFormula() : WitchRelic("lost_formula")
{
    public override RelicRarity Rarity => RelicRarity.Uncommon;
}

public sealed class ObsidianDistiller() : WitchRelic("obsidian_distiller")
{
    public override RelicRarity Rarity => RelicRarity.Rare;
}

public sealed class DoubleVeil() : WitchRelic("double_veil")
{
    public override RelicRarity Rarity => RelicRarity.Rare;
}

public sealed class BloodstainedRing() : WitchRelic("bloodstained_ring")
{
    public override RelicRarity Rarity => RelicRarity.Rare;
}

public sealed class SealedPotionRelic() : WitchRelic("sealed_potion")
{
    public override RelicRarity Rarity => RelicRarity.Shop;

    public override async Task AfterCombatEnd(CombatRoom room)
    {
        if (Owner.Creature.IsAlive && WitchCombatState.Find(Owner)?.CardLifeLost >= 10)
        {
            Flash();
            await CreatureCmd.Heal(Owner.Creature, 3);
        }
    }
}
