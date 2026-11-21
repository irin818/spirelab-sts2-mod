using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Entities.Powers;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

// These powers expose the same counters that the combat hooks consume. Their own hooks
// stay disabled so a display refresh never schedules the effect for a second time.
public abstract class WitchVisiblePower(string art, bool showAmount = true) : CustomPowerModel
{
    public override PowerType Type => PowerType.Buff;
    public override PowerStackType StackType => showAmount ? PowerStackType.Counter : PowerStackType.Single;
    public override bool ShouldReceiveCombatHooks => false;
    public override bool ShouldPlayVfx => false;
    public override string CustomPackedIconPath => ResolveIcon("powers");
    public override string CustomBigIconPath => ResolveIcon("powers/big");
    public override string CustomBigBetaIconPath => CustomBigIconPath;

    private static string Resolve(string path, string placeholder) => ResourceLoader.Exists(path) ? path : placeholder;
    private string ResolveIcon(string directory) => Resolve($"{TheWitch.Art}/{directory}/{art}.png",
        $"{TheWitch.Art}/abilities/status/{art}.svg");
}

public sealed class InsightPower() : WitchVisiblePower("insight")
{
    public override int DisplayAmount => Amount == 2 ? 1 : 0;
    public override LocString Description => new("powers", Id.Entry + (Amount == 2 ? ".ready" : ".description"));
    protected override string SmartDescriptionLocKey => Amount == 2 ? Id.Entry + ".ready" : base.SmartDescriptionLocKey;
}

public sealed class PinnaclePower() : WitchVisiblePower("pinnacle", showAmount: false);
public sealed class MirrorFormPower() : WitchVisiblePower("mirror_form", showAmount: false);
public sealed class DesireDiscountPower() : WitchVisiblePower("desire_discount", showAmount: false);
public sealed class ManaPumpPower() : WitchVisiblePower("mana_pump");
public sealed class ReflectiveInscriptionPower() : WitchVisiblePower("reflective_inscription", showAmount: false);
public sealed class InscribedSkillPower() : WitchVisiblePower("inscribed_skill");
public sealed class WitchImmunityPower() : WitchVisiblePower("immunity");
public sealed class SlothImmunityPower() : WitchVisiblePower("immunity", showAmount: false);
public sealed class GrimoireRetainPower() : WitchVisiblePower("grimoire_retain");
public sealed class BookmarkPower() : WitchVisiblePower("bookmark");
public sealed class GlassHallPower() : WitchVisiblePower("glass_hall");
public sealed class ReflectBoostPower() : WitchVisiblePower("reflect_boost");
public sealed class ReflectChargesPower() : WitchVisiblePower("reflect_charges");
public sealed class PendingEnergyPower() : WitchVisiblePower("pending_energy");
public sealed class PendingBlockPower() : WitchVisiblePower("pending_block");
public sealed class PendingDrawPower() : WitchVisiblePower("pending_draw");
public sealed class StartingManaPower() : WitchVisiblePower("starting_mana");
public sealed class SevenfoldDrawPower() : WitchVisiblePower("sevenfold_draw", showAmount: false);
public sealed class BlueFlamePower() : WitchVisiblePower("blue_flame");
public sealed class RedCatalystPower() : WitchVisiblePower("red_catalyst");
public sealed class RefinedAbyssPower() : WitchVisiblePower("refined_abyss");
public sealed class DuskRefinementPower() : WitchVisiblePower("dusk_refinement");
public sealed class AllMirrorsPower() : WitchVisiblePower("all_mirrors");
public sealed class ShadowDrawPower() : WitchVisiblePower("shadow_draw");
public sealed class FracturedCrownPower() : WitchVisiblePower("fractured_crown");
public sealed class ReverseRitePower() : WitchVisiblePower("reverse_rite");
public sealed class VirtualImagePower() : WitchVisiblePower("virtual_image");
public sealed class SplitFacePower() : WitchVisiblePower("split_face");

internal static class WitchStatuses
{
    public static async Task Refresh(PlayerChoiceContext choice, Player player, WitchCombatState state)
    {
        var creature = player.Creature;
        if (!CombatManager.Instance.IsInProgress || !creature.IsAlive)
        {
            return;
        }

        await Show<InsightPower>(choice, creature, state.TraitSwapOffered ? 1 : 2);
        await Show<PinnaclePower>(choice, creature, state.RetainEnergy ? 1 : 0);
        await Show<MirrorFormPower>(choice, creature, state.MirrorForm ? 1 : 0);
        await Show<DesireDiscountPower>(choice, creature, state.DesireDiscount ? 1 : 0);
        await Show<ManaPumpPower>(choice, creature, state.ManaPumpDurations.Count == 0 ? 0 : state.ManaPumpDurations.Max());
        await Show<ReflectiveInscriptionPower>(choice, creature, state.InscriptionEnabled ? 1 : 0);
        await Show<InscribedSkillPower>(choice, creature, state.NextSkillDiscount + state.SkillDiscount);
        await Show<WitchImmunityPower>(choice, creature, state.Immunity.HasValue ? 1 : 0);
        await Show<SlothImmunityPower>(choice, creature, state.SlothImmunity ? 1 : 0);
        await Show<GrimoireRetainPower>(choice, creature, state.RetainRandomCount);
        await Show<BookmarkPower>(choice, creature, state.BookmarkEnergy);
        await Show<GlassHallPower>(choice, creature, state.RetainedBlockCap);
        await Show<ReflectBoostPower>(choice, creature, state.MazeReflectPercent);
        var pendingReflections = state.DeferredReflections.Count + (state.NextReflectPercent > 0 ? 1 : 0);
        await Show<ReflectChargesPower>(choice, creature, pendingReflections);
        await Show<PendingEnergyPower>(choice, creature, state.NextEnergy);
        await Show<PendingBlockPower>(choice, creature, state.NextBlock);
        await Show<PendingDrawPower>(choice, creature, state.NextDraw);
        await Show<StartingManaPower>(choice, creature, state.StartingEnergy);
        await Show<SevenfoldDrawPower>(choice, creature, state.SevenfoldDraw ? 1 : 0);
        await Show<BlueFlamePower>(choice, creature, state.BlueFlameDamage);
        await Show<RedCatalystPower>(choice, creature, state.RedCatalystBlock);
        await Show<RefinedAbyssPower>(choice, creature, state.DepthsEnergy);
        await Show<DuskRefinementPower>(choice, creature, state.DuskDraw);
        await Show<AllMirrorsPower>(choice, creature, state.FirstBlockPercent);
        await Show<ShadowDrawPower>(choice, creature, state.ShadowDraw);
        await Show<FracturedCrownPower>(choice, creature, state.CrownDamage);
        await Show<ReverseRitePower>(choice, creature, state.ReverseRiteDraw);
        await Show<VirtualImagePower>(choice, creature, state.BlockReduction);
        await Show<SplitFacePower>(choice, creature, state.WeakOnBlock);
    }

    private static async Task Show<T>(PlayerChoiceContext choice, Creature creature, int amount) where T : WitchVisiblePower
    {
        var existing = creature.GetPower<T>();
        if (amount > 0)
        {
            if (existing == null)
            {
                await PowerCmd.Apply<T>(choice, creature, amount, creature, null, silent: true);
            }
            else if (existing.Amount != amount)
            {
                existing.SetAmount(amount, silent: true);
            }
        }
        else if (existing != null)
        {
            await PowerCmd.Remove(existing);
        }
    }
}
