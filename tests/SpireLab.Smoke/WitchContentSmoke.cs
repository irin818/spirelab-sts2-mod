using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using Finesse = MegaCrit.Sts2.Core.Models.Cards.Finesse;
using FlashOfSteel = MegaCrit.Sts2.Core.Models.Cards.FlashOfSteel;
using MegaCrit.Sts2.Core.Models.Enchantments;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Models.Relics;
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
        var witchCards = all.Cast<WitchCard>().ToArray();
        var groups = all.GroupBy(card => card.Rarity).ToDictionary(group => group.Key, group => group.Count());
        var active = witchCards.Where(card => !card.IsRetiredFromRewardPool).ToArray();
        var retired = witchCards.Where(card => card.IsRetiredFromRewardPool).Select(card => card.SpecId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Check(all.Length == 91 && all.Select(card => card.Id).Distinct().Count() == 91
            && groups[CardRarity.Basic] == 4 && groups[CardRarity.Common] == 20 && groups[CardRarity.Uncommon] == 38
            && groups[CardRarity.Rare] == 27 && groups[CardRarity.Ancient] == 2,
            "91 distinct cards preserved in ModelDb with exact legacy rarity counts");
        Check(active.Length == 82
            && active.Count(card => card.Rarity == CardRarity.Basic) == 4
            && active.Count(card => card.Rarity == CardRarity.Common) == 19
            && active.Count(card => card.Rarity == CardRarity.Uncommon) == 32
            && active.Count(card => card.Rarity == CardRarity.Rare) == 25
            && active.Count(card => card.Rarity == CardRarity.Ancient) == 2
            && retired.SequenceEqual(["C17", "R06", "R22", "U02", "U03", "U17", "U25", "U26", "U29"])
            && ModelDb.Card<EmptyMirror>().Type == CardType.Power,
            "V2.0 active card pool has 82 cards (4B/19C/32U/25R/2A), exact 9 retired IDs, and R15 EmptyMirror is a Power");
        ValidatePrintedCosts(witchCards);
        Check(witch.RelicPool.AllRelics.Count() == 9 && witch.PotionPool.AllPotions.Count() == 3, "nine relics and three potions");
        Check(witch.RelicPool.AllRelics.OfType<WitchRelic>().Count(relic =>
        {
            var small = relic.PackedIconPath;
            var outline = small.Replace(".png", "_outline.png", StringComparison.Ordinal);
            var big = $"{TheWitch.Art}/relics/big/{Path.GetFileName(small)}";
            return HasSize(small, 94) && HasSize(outline, 94) && HasSize(big, 256);
        }) == 9, "all relic portraits, outlines and large icons load at native sizes");
        Check(witch.PotionPool.AllPotions.OfType<WitchPotion>().Count(potion =>
            HasSize(potion.CustomPackedImagePath!, 256) && HasSize(potion.CustomPackedOutlinePath!, 256)) == 3,
            "all potion portraits and outlines load at native sizes");
        var standardTrims = all.OfType<WitchCard>().Where(card => card.Rarity != CardRarity.Ancient)
            .GroupBy(card => card.Rarity).Select(group => (group.First().FrameMaterial as ShaderMaterial)?.GetShaderParameter("trim"))
            .ToArray();
        Check(standardTrims.All(trim => trim.HasValue && trim.Value.VariantType == Variant.Type.Color)
            && standardTrims.Distinct().Count() == 4, "four regular rarity frame palettes use the custom shader");
        ValidateCardText(witch);
        var tome = (DustyTome)ModelDb.Relic<DustyTome>().ToMutable();
        tome.SetupForPlayer(player);
        Check(tome.AncientCard == ModelDb.Card<SevenfoldDistillation>().Id
            && ArchaicTooth.TranscendenceCards.Contains(ModelDb.Card<BeyondTheGlass>())
            && ModelDb.Relic<TouchOfOrobas>().GetUpgradedStarterRelic(ModelDb.Relic<CrackedVanityMirror>()) is TwinStandingMirror,
            "base-game Tome, Archaic Tooth and starter relic upgrade route to the specified models");
    }

    private static bool HasSize(string path, int size) =>
        ResourceLoader.Load<Texture2D>(path)?.GetSize() == new Vector2(size, size);

    private static void ValidatePrintedCosts(IEnumerable<WitchCard> cards)
    {
        var costsByRarity = new Dictionary<char, string>
        {
            ['B'] = "1111",
            ['C'] = "11120211001111111211",
            ['U'] = "12120111021121111211111112011111211022",
            ['R'] = "2X3121131332211221322221321",
            ['A'] = "12"
        };
        string[] reducedOnUpgrade = ["U34", "U37", "R01", "R03", "R10", "R16", "R20", "R21", "R25"];
        foreach (var card in cards)
        {
            var number = int.Parse(card.SpecId.AsSpan(1), System.Globalization.CultureInfo.InvariantCulture);
            var printed = costsByRarity[card.SpecId[0]][number - 1];
            var upgraded = card.ToMutable();
            if (upgraded.IsUpgradable)
            {
                upgraded.UpgradeInternal();
                upgraded.FinalizeUpgradeInternal();
            }

            var expectedUpgrade = printed - '0' - (reducedOnUpgrade.Contains(card.SpecId) ? 1 : 0);
            var correct = printed == 'X'
                ? card.EnergyCost.CostsX && upgraded.EnergyCost.CostsX
                : !card.EnergyCost.CostsX && !upgraded.EnergyCost.CostsX
                    && card.EnergyCost.GetWithModifiers(CostModifiers.None) == printed - '0'
                    && upgraded.EnergyCost.GetWithModifiers(CostModifiers.None) == expectedUpgrade;
            if (!correct)
            {
                throw new InvalidOperationException($"Unexpected printed cost for {card.SpecId} ({card.Id}).");
            }
        }

        Check(true, "all 91 base and upgraded printed costs match V2.0, including X");
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

    public static async Task Exercise(SceneTree tree, Player player, PlayerChoiceContext choice, WitchSelector selector)
    {
        var creature = player.Creature;
        var enemy = creature.CombatState!.HittableEnemies.First();
        var state = WitchCombatState.For(player);
        var piles = player.PlayerCombatState!;
        // The same foe is needed later for several independent reflection and damage checks.
        await CreatureCmd.SetMaxHp(enemy, 100);
        await CreatureCmd.SetCurrentHp(enemy, 100);
        await CreatureCmd.SetCurrentHp(creature, 30);
        await Play<ReflectiveInscription>(player, choice, upgrade: true);
        await Play<MirrorBurial>(player, choice);
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        await CreatureCmd.GainBlock(creature, 14, ValueProp.Unpowered, null);
        var enemyHp = enemy.CurrentHp;
        await CreatureCmd.Damage(choice, creature, 7, ValueProp.Move, enemy);
        await CreatureCmd.Damage(choice, creature, 7, ValueProp.Move, enemy);
        Check(enemy.CurrentHp == enemyHp - 24 && creature.Block == 0 && creature.CurrentHp == 30
            && state.BaseReflectPercent == 50
            && creature.GetPower<ReflectChargesPower>() == null && creature.GetPower<BaseReflectPower>()?.Amount == 50,
            "two hits reflect 200% (14) then 150% (10), consume Burial once and grow upgraded Inscription (+50% base reflect)");
        await Play<SplitFace>(player, choice);
        await CreatureCmd.Damage(choice, creature, 6, ValueProp.Move, enemy);
        Check(enemy.GetPower<WeakPower>()?.Amount == 1 && creature.GetPower<SplitFacePower>() == null,
            "Split Face applies Weak on the first block and clears its native icon");
        await PowerCmd.Remove<WeakPower>(enemy);

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
        await Play<ColdMirrorBarrier>(player, choice);
        Check(creature.Block == 12, $"Hall of All Mirrors boosts first Block 8 -> 12 (actual {creature.Block})");

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
        Check(piles.Energy == energy + 5 && state.CrackedPhialUsed && state.DistillerUsed
            && state.PendingSingleHitAttackBonus == 6 && creature.GetPower<BlueFlameReadyPower>()?.Amount == 6,
            "refinement triggers flask, one distiller bonus without recursion, and arms Blue Flame single-hit +6");
        energy = piles.Energy;
        await Play<AlienExtract>(player, choice);
        Check(piles.Energy == energy + 2, "distiller does not recur on the second card gain");

        await CreatureCmd.SetCurrentHp(creature, 35);
        await Play<GildedPact>(player, choice);
        Check(creature.CurrentHp == 32 && state.MirrorHp == 12 && state.LifePaymentTriggered,
            "life payment crosses half and triggers first veil and blood ring");
        await Play<Recovery>(player, choice);
        Check(creature.CurrentHp == 36 && state.MirrorHp == 12, "legacy exhausting heal affects real HP only");
        await Play<ElixirPhial>(player, choice, upgrade: true);
        Check(creature.CurrentHp == 36 && state.MirrorHp == 12, "V2 Elixir Phial does not heal");
        var tonic = ModelDb.Potion<AbyssalDistillate>().ToMutable();
        await PotionCmd.TryToProcure(tonic, player);
        await CreatureCmd.SetCurrentHp(creature, 5);
        Check(!tonic.PassesCustomUsabilityCheck, "distillate cannot be used at exactly 5 real HP");
        await CreatureCmd.SetCurrentHp(creature, 6);
        energy = piles.Energy;
        await tonic.OnUseWrapper(choice, creature);
        Check(creature.CurrentHp == 1 && piles.Energy == energy + 3 && state.MirrorHp == 12,
            "distillate pays real HP and grants energy at the boundary");

        if (piles.Hand.Cards.Count >= CardPile.MaxCardsInHand)
        {
            await CardCmd.Discard(choice, piles.Hand.Cards.Last());
        }
        var original = piles.Hand.Cards.First();
        var incoming = await Enlightenment.Swap(player, original, fromTrait: false) ?? throw new InvalidOperationException("No Greed fixture");
        var kept = piles.Hand.Cards.First();
        await Play<PrimalGreed>(player, choice);
        Check(!incoming.HasBeenRemovedFromState && original.Pile?.IsCombatPile == true && player.Deck.Cards.Count == 10
            && piles.Hand.Cards.Contains(kept) && !kept.ShouldRetainThisTurn,
            "Greed retains the integrity of the cards moved by Arcane Insight");

        await CreatureCmd.SetCurrentHp(creature, 30);
        if (enemy.IsAlive)
        {
            await CreatureCmd.Damage(choice, enemy, 999, ValueProp.Unpowered, creature);
        }
        await CombatManager.Instance.CheckWinCondition();
        await WaitFor(tree, () => !CombatManager.Instance.IsInProgress, "second Witch battle won");
        Check(creature.CurrentHp == 33, "Sealed Potion recovers 3 after card life loss");
        await LeaveCompletedCombat(tree);

        selector.OpeningSpecId = "R05";
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
        piles = player.PlayerCombatState!;
        await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "third Witch battle");
        var torrent = (PotionTorrent)WitchCombatState.For(player).OpeningCard!;
        var source = player.Deck.Cards.OfType<WitchStrike>().First();
        CardCmd.Upgrade(source);
        Check(CardCmd.Enchant<Sharp>(source, 2) != null, "native permanent sample enchantment");
        await CardPileCmd.RemoveFromDeck(player.Deck.Cards.Where(card => card != source).ToArray(), showPreview: false);
        await CardPileCmd.Add(piles.DrawPile.Cards.Concat(piles.DiscardPile.Cards).Where(card => card != torrent).ToArray(), PileType.Exhaust);
        await CardPileCmd.Add(torrent, PileType.Play, skipVisuals: true);
        var fillers = new List<CardModel>();
        while (piles.Hand.Cards.Count < CardPile.MaxCardsInHand)
        {
            fillers.Add(await Add<ManaSeal>(player));
        }
        var sample = ((CombatState)creature.CombatState!).CloneCard(source);
        await CardPileCmd.Add(sample, PileType.Discard);
        var offered = piles.Hand.Cards.First();
        var swapped = await Enlightenment.Swap(player, offered, fromTrait: false);
        Check(swapped == sample && sample.IsUpgraded && sample.Enchantment is Sharp { Amount: 2 }
            && offered.Pile?.Type == PileType.Discard && piles.Hand.Cards.Count == CardPile.MaxCardsInHand,
            "full-hand discard swap moves the actual upgraded, enchanted card into the freed slot");
        await CardPileCmd.Add(fillers, PileType.Exhaust);
        var sloth = await Add<Sloth>(player);
        var keptBySloth = piles.Hand.Cards.First(card => card != sloth);
        var turn = piles.TurnNumber;
        Check(sloth.TryManualPlay(null), "Sloth enters the native action queue as first play");
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "Sloth ends turn through game queue");
        Check(creature.CurrentHp == 33 && WitchCombatState.For(player).RetainedCards.Contains(keptBySloth)
            && creature.GetPower<SlothImmunityPower>() == null,
            "low-health first-card Sloth both retains a card and clears its full immunity at the next turn");

        await Play<ForbiddenFrontispiece>(player, choice);
        await Play<BloodstainedBookmark>(player, choice);
        await Play<ManaPump>(player, choice, upgrade: true);
        state = WitchCombatState.For(player);
        Check(creature.GetPower<ManaPumpPower>()?.Amount == 3, "upgraded Mana Pump shows three future activations");
        energy = piles.Energy;
        var distillerBonus = state.DistillerUsed ? 0 : 1;
        await CardCmd.AutoPlay(choice, keptBySloth, null);
        Check(state.BookmarkUsed && piles.Energy == energy + 1 + distillerBonus,
            "Bookmark returns energy for a card actually retained by the game");

        await CardPileCmd.Add(piles.DrawPile.Cards.Concat(piles.DiscardPile.Cards).ToArray(), PileType.Exhaust);
        await CardPileCmd.Add(torrent, PileType.Draw);
        await Play<TurnThePageOfFate>(player, choice);
        Check(torrent.Pile?.Type == PileType.Hand,
            "Fate's tutor brings the actual combat-only Torrent to hand");
        await CardCmd.AutoPlay(choice, torrent, null);
        Check(torrent.Pile?.Type == PileType.Exhaust && !player.Deck.Cards.Contains(torrent)
            && await Enlightenment.Swap(player, piles.Hand.Cards.First(), fromTrait: false) == null,
            "the selected Torrent exhausts and cannot reenter hand through Arcane Insight");

        await Play<PinnacleOfMana>(player, choice);
        await PlayerCmd.SetEnergy(WitchEnergy.EngineLimit, player);
        turn = piles.TurnNumber;
        selector.SelectFirstHand = true;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "large retained energy crosses turn boundary");
        Check(piles.Energy == WitchEnergy.EngineLimit, "native retained energy stays at game cap");
        Check(creature.GetPower<ManaPumpPower>()?.Amount == 2 && state.RetainedCards.Count >= 2
            && !state.BookmarkUsed, "Frontispiece retains another real card, Bookmark resets and Mana Pump counts down");
        var deluge = await Add<ManaDeluge>(player, upgrade: true);
        Check(deluge.TryManualPlay(null), "X-cost attack spends retained energy through game queue");
        await WaitFor(tree, () => !CombatManager.Instance.IsInProgress, "X-cost uses high energy and stops on victory");
        await LeaveCompletedCombat(tree);

        // Run dedicated V2.0 mechanics, 15 Counterpoint cases, and 7 cross-archetype combos A-G.
        await WitchV2Smoke.Exercise(tree, player, choice, selector);

        // Confirm the Witch rules still permit a proven two-card draw cycle with base-game colorless cards.
        var run = player.RunState;
        var finesse = run.CreateCard(ModelDb.Card<Finesse>(), player);
        var flash = run.CreateCard(ModelDb.Card<FlashOfSteel>(), player);
        await CardPileCmd.Add(finesse, PileType.Deck);
        await CardPileCmd.Add(flash, PileType.Deck);
        await CardPileCmd.RemoveFromDeck([source], showPreview: false);
        await RelicCmd.Remove(player.Relics.Single(relic => relic is CrackedVanityMirror));
        selector.OpeningSpecId = "C02";
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
        piles = player.PlayerCombatState!;
        await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "fourth Witch battle with two-card deck");
        state = WitchCombatState.For(player);
        Check(state.CombatEnlightenments == 0 && creature.Block == 0 && piles.Hand.Cards.Count == 3
            && state.OpeningCard is PotionMist, "opening choice works without the starter relic, and an empty swap pool is skipped");
        await CardPileCmd.Add(state.OpeningCard!, PileType.Exhaust);
        enemy = creature.CombatState!.HittableEnemies.First();
        var originalFinesse = piles.Hand.Cards.OfType<Finesse>().Single();
        var originalFlash = piles.Hand.Cards.OfType<FlashOfSteel>().Single();
        var cycleMana = piles.Energy;
        var cycleHp = creature.CurrentHp;
        var cycleTurn = piles.TurnNumber;
        var startEnemyHp = enemy.CurrentHp;
        for (var i = 0; i < 8; i++)
        {
            var blockStep = i % 2 == 0;
            var played = blockStep ? (CardModel)originalFinesse : originalFlash;
            var next = blockStep ? (CardModel)originalFlash : originalFinesse;
            if (!played.TryManualPlay(blockStep ? null : enemy))
            {
                throw new InvalidOperationException("The next card in the two-card cycle was not playable.");
            }

            await WaitFor(tree, () => played.Pile?.Type == PileType.Discard && next.Pile?.Type == PileType.Hand,
                $"two-card loop step {i + 1} returns the other card");
        }

        Check(piles.Energy == cycleMana && creature.CurrentHp == cycleHp && piles.TurnNumber == cycleTurn
            && creature.Block == 16 && enemy.CurrentHp == startEnemyHp - 20 && state.CombatEnlightenments == 0,
            "same-turn cycle gains Block and deals damage without depleting energy or real HP");
    }
}
