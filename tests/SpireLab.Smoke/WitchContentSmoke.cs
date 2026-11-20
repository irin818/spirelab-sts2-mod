using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

internal static class WitchContentSmoke
{
    public static void ValidateDefinitions(TheWitch witch, Player player)
    {
        var all = witch.CardPool.AllCards.ToArray();
        var groups = all.GroupBy(card => card.Rarity).ToDictionary(group => group.Key, group => group.Count());
        Check(all.Length == 91 && all.Select(card => card.Id).Distinct().Count() == 91
            && groups[CardRarity.Basic] == 4 && groups[CardRarity.Common] == 20 && groups[CardRarity.Uncommon] == 38
            && groups[CardRarity.Rare] == 27 && groups[CardRarity.Ancient] == 2, "91 distinct cards with exact GDD rarity counts");
        Check(witch.RelicPool.AllRelics.Count() == 9 && witch.PotionPool.AllPotions.Count() == 3, "nine relics and three potions");
        ValidateCardText(witch);
        var tome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
        tome.SetupForPlayer(player);
        Check(tome.AncientCard == ModelDb.Card<SevenfoldDistillation>().Id
            && ArchaicTooth.TranscendenceCards.Contains(ModelDb.Card<BeyondTheGlass>())
            && ModelDb.Relic<TouchOfOrobas>().GetUpgradedStarterRelic(ModelDb.Relic<CrackedVanityMirror>()) is TwinStandingMirror,
            "base-game Tome, Archaic Tooth and starter relic upgrade route to the specified models");
    }

    public static void ValidateCardText(TheWitch witch)
    {
        foreach (var card in witch.CardPool.AllCards)
        {
            CheckText(card.Title, card.Id.Entry);
            CheckText(card.GetDescriptionForPile(PileType.None), card.Id.Entry);
            CheckText(card.GetDescriptionForUpgradePreview(), card.Id.Entry);
        }

        Check(true, "all Witch card names, descriptions and authored upgrade previews resolve");
    }

    private static void CheckText(string text, string id)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Contains("SPIRELAB-") || text.Contains('{'))
        {
            throw new InvalidOperationException($"Unresolved localization for {id}: {text}");
        }
    }

    public static async Task Exercise(SceneTree tree, Player player, PlayerChoiceContext choice)
    {
        var creature = player.Creature;
        var enemy = creature.CombatState!.HittableEnemies.First();
        var state = WitchCombatState.For(player);
        var piles = player.PlayerCombatState!;
        await CreatureCmd.SetCurrentHp(creature, 30);
        await Play<ReflectiveInscription>(player, choice);
        await Play<SplitFace>(player, choice);
        await Play<MirrorBurial>(player, choice);
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        await CreatureCmd.GainBlock(creature, 14, ValueProp.Unpowered, null);
        var enemyHp = enemy.CurrentHp;
        await CreatureCmd.Damage(choice, creature, 7, ValueProp.Move, enemy);
        await CreatureCmd.Damage(choice, creature, 7, ValueProp.Move, enemy);
        Check(enemy.CurrentHp == enemyHp - 19 && creature.Block == 0 && creature.CurrentHp == 30,
            "two hits reflect per-hit 50%, with flat bonuses and burial consumed only once");

        await Play<MoonInTheMirror>(player, choice);
        await CreatureCmd.GainBlock(creature, 6, ValueProp.Unpowered, null);
        enemyHp = enemy.CurrentHp;
        await CreatureCmd.Damage(choice, creature, 9, ValueProp.Move, enemy);
        Check(creature.Block == 6 && creature.CurrentHp == 30 && enemy.CurrentHp == enemyHp - 12,
            "immunity uses neither Block nor mirror HP and retaliates once");
        await Play<VirtualImage>(player, choice);
        await CreatureCmd.Damage(choice, creature, 9, ValueProp.Move, enemy);
        Check(creature.Block == 3 && creature.CurrentHp == 30, "next-attack reduction occurs before Block");
        await Play<HallOfAllMirrors>(player, choice);
        state.BlockGains = 0;
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        await Play<EmptyMirror>(player, choice);
        Check(creature.Block == 13, "Empty Mirror and Hall combine then round 13.5 down");

        await RelicCmd.Obtain<CrackedPhial>(player);
        await RelicCmd.Obtain<ObsidianDistiller>(player);
        await RelicCmd.Obtain<LostFormula>(player);
        await RelicCmd.Obtain<BloodstainedRing>(player);
        await RelicCmd.Obtain<DoubleVeil>(player);
        await RelicCmd.Obtain<SealedPotionRelic>(player);
        await RelicCmd.Obtain<SilverMirrorFrame>(player);
        await Play<BlueFlameFormula>(player, choice);
        var energy = piles.Energy;
        await Play<ForbiddenRefinement>(player, choice, upgrade: true);
        Check(piles.Energy == energy + 5 && state.CrackedPhialUsed && state.DistillerUsed,
            "refinement triggers flask and one distiller bonus without recursion");
        energy = piles.Energy;
        await Play<AlienExtract>(player, choice);
        Check(piles.Energy == energy + 2, "distiller does not recur on the second card gain");

        await CreatureCmd.SetCurrentHp(creature, 35);
        await Play<GildedPact>(player, choice);
        Check(creature.CurrentHp == 32 && state.MirrorHp == 12 && state.LifePaymentTriggered,
            "life payment crosses half and triggers first veil and blood ring");
        await Play<ElixirPhial>(player, choice, upgrade: true);
        Check(creature.CurrentHp == 35 && state.MirrorHp == 12, "exhausting heal affects real HP only");
        var tonic = ModelDb.Potion<AbyssalDistillate>().ToMutable();
        await PotionCmd.TryToProcure(tonic, player);
        await CreatureCmd.SetCurrentHp(creature, 5);
        Check(!tonic.PassesCustomUsabilityCheck, "distillate cannot be used at exactly 5 real HP");
        await CreatureCmd.SetCurrentHp(creature, 6);
        energy = piles.Energy;
        await tonic.OnUseWrapper(choice, creature);
        Check(creature.CurrentHp == 1 && piles.Energy == energy + 3 && state.MirrorHp == 12,
            "distillate pays real HP and grants energy at the boundary");

        var original = piles.Hand.Cards.First(card => !state.StandIns.ContainsKey(card));
        var substitute = await Enlightenment.Replace(player, original) ?? throw new InvalidOperationException("No Greed fixture");
        await Play<PrimalGreed>(player, choice);
        Check(substitute.HasBeenRemovedFromState && original.Pile?.IsCombatPile == true
            && original.Pile.Type != EnlightenmentPile.HeldType && player.Deck.Cards.Count == 10,
            "Greed does not shuffle a substitute or lose its original");

        await CreatureCmd.SetCurrentHp(creature, 30);
        if (enemy.IsAlive)
        {
            await CreatureCmd.Damage(choice, enemy, 999, ValueProp.Unpowered, creature);
        }
        await CombatManager.Instance.CheckWinCondition();
        await WaitFor(tree, () => !CombatManager.Instance.IsInProgress, "second Witch battle won");
        Check(creature.CurrentHp == 33, "Sealed Potion recovers 3 after card life loss");
        // The fixture enters a scripted next room. Close the actual reward overlay first so
        // deferred card holder recycling runs while the old room is still attached.
        await RunManager.Instance.ProceedFromTerminalRewardsScreen();
        NOverlayStack.Instance?.Clear();
        for (var frame = 0; frame < 3; frame++)
        {
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
        piles = player.PlayerCombatState!;
        await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "third Witch battle");
        await Enlightenment.ReleaseAll(player, WitchCombatState.For(player));
        var source = player.Deck.Cards.OfType<WitchStrike>().First();
        CardCmd.Upgrade(source);
        Check(CardCmd.Enchant<Sharp>(source, 2) != null, "native permanent sample enchantment");
        await CardPileCmd.RemoveFromDeck(player.Deck.Cards.Where(card => card != source).ToArray(), showPreview: false);
        var standIn = await Enlightenment.Replace(player, piles.Hand.Cards.First());
        Check(standIn is WitchStrike && standIn.IsUpgraded && standIn.Enchantment is Sharp { Amount: 2 },
            "substitute inherits actual sampled copy's upgrade and enchantment");
        var sloth = await Add<Sloth>(player);
        var turn = piles.TurnNumber;
        Check(sloth.TryManualPlay(null), "Sloth enters the native action queue as first play");
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "Sloth ends turn through game queue");
        Check(creature.CurrentHp == 33, "first-card Sloth negates next enemy attack");

        await Play<PinnacleOfMana>(player, choice);
        await PlayerCmd.SetEnergy(WitchEnergy.EngineLimit, player);
        turn = piles.TurnNumber;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "large retained energy crosses turn boundary");
        Check(piles.Energy == WitchEnergy.EngineLimit, "native retained energy stays at game cap");
        var deluge = await Add<ManaDeluge>(player, upgrade: true);
        Check(deluge.TryManualPlay(null), "X-cost attack spends retained energy through game queue");
        await WaitFor(tree, () => !CombatManager.Instance.IsInProgress, "X-cost uses high energy and stops on victory");
    }
}
