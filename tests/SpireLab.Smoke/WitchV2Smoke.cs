using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;
using static SpireLab.Smoke.WitchSmoke;

namespace SpireLab.Smoke;

internal static class WitchV2Smoke
{
    public static async Task Exercise(SceneTree tree, Player player, PlayerChoiceContext choice, WitchSelector selector)
    {
        var run = player.RunState;
        await RunManager.Instance.EnterRoomDebug(RoomType.Monster, model: ModelDb.Encounter<NibbitsWeak>().ToMutable(), showTransition: false);
        var piles = player.PlayerCombatState!;
        await WaitFor(tree, () => piles.Phase == PlayerTurnPhase.Play, "V2.0 dedicated test battle begins");
        var creature = player.Creature;
        var enemy = creature.CombatState!.HittableEnemies.First();
        var state = WitchCombatState.For(player);
        await CreatureCmd.SetMaxHp(enemy, 400);
        await CreatureCmd.SetCurrentHp(enemy, 400);
        await CreatureCmd.SetCurrentHp(creature, 50);

        // Ensure plenty of cards in draw pile for draw checks.
        await RefillDraw(player, 15);
        await ClearHand(player, choice);

        // ============================================================
        // 13.2 Counterpoint: all 15 required test cases
        // ============================================================
        // Case 5: First card of turn does not trigger Counterpoint.
        state.LastPlayedType = null;
        var handBefore = piles.Hand.Cards.Count;
        await Play<ColdMirrorBarrier>(player, choice);
        Check(creature.Block == 8 && piles.Hand.Cards.Count == handBefore,
            "V2 CP#5a: first Skill of turn does not trigger Counterpoint draw");

        // Case 4: Skill -> Skill does not trigger Counterpoint.
        handBefore = piles.Hand.Cards.Count;
        await Play<ColdMirrorBarrier>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore && state.LastPlayedType == CardType.Skill,
            "V2 CP#4: Skill -> Skill does not trigger Counterpoint");

        // Case 2: Skill -> Attack triggers Counterpoint (RazorPages draws 2 instead of 1).
        handBefore = piles.Hand.Cards.Count;
        var hpBefore = enemy.CurrentHp;
        await Play<RazorPages>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 7 && piles.Hand.Cards.Count == handBefore + 2 && state.LastPlayedType == CardType.Attack,
            "V2 CP#2: Skill -> Attack triggers Counterpoint (RazorPages draws 2)");

        // Case 3: Attack -> Attack does not trigger Counterpoint (RazorPages draws 1).
        await ClearHand(player, choice);
        handBefore = piles.Hand.Cards.Count;
        await Play<RazorPages>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore + 1,
            "V2 CP#3: Attack -> Attack does not trigger Counterpoint (RazorPages draws 1)");

        // Case 1: Attack -> Skill triggers Counterpoint (ColdMirrorBarrier draws 1).
        handBefore = piles.Hand.Cards.Count;
        await Play<ColdMirrorBarrier>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore + 1,
            "V2 CP#1: Attack -> Skill triggers Counterpoint (ColdMirrorBarrier draws 1)");

        // Case 6 & 13: Power card (EmptyMirror+) interrupts chain; then first alternating play triggers R15 once.
        await ClearHand(player, choice);
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        await Play<WitchStrike>(player, choice);
        await Play<EmptyMirror>(player, choice, upgrade: true);
        Check(state.LastPlayedType == CardType.Power && creature.GetPower<EmptyMirrorPower>()?.Amount == 1,
            "V2 R15: Empty Mirror is a Power that sets LastPlayedType to Power and displays native icon");
        handBefore = piles.Hand.Cards.Count;
        await Play<ColdMirrorBarrier>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore && creature.Block == 8 && !state.EmptyMirrorTriggered,
            "V2 CP#6: Power card interrupts Counterpoint chain");

        // Now Skill -> Attack triggers R15 Empty Mirror+ (+1 draw, +3 Block) ONCE.
        handBefore = piles.Hand.Cards.Count;
        var blockBefore = creature.Block;
        await Play<WitchStrike>(player, choice);
        Check(state.EmptyMirrorTriggered && piles.Hand.Cards.Count == handBefore + 1 && creature.Block == blockBefore + 3,
            "V2 CP#13a: Empty Mirror+ triggers on first alternating Attack/Skill play (+1 draw, +3 Block)");
        // Second alternating play same turn must NOT re-trigger R15.
        handBefore = piles.Hand.Cards.Count;
        blockBefore = creature.Block;
        await Play<WitchDefend>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore && creature.Block == blockBefore + 5,
            "V2 CP#13b: Empty Mirror does not trigger a second time in the same turn");

        // Case 8: Multi-hit Attack (BoilingEcho) counts as a single Attack play.
        await ClearHand(player, choice);
        Check(state.ExtraEnergy == 0, "V2 CP#8 precondition: no extra energy gained yet");
        hpBefore = enemy.CurrentHp;
        await Play<BoilingEcho>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 18 && state.LastPlayedType == CardType.Attack,
            "V2 CP#8a: BoilingEcho deals 9x2 and records Attack once");
        handBefore = piles.Hand.Cards.Count;
        await Play<ColdMirrorBarrier>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore + 1,
            "V2 CP#8b: Skill after multi-hit Attack triggers Counterpoint");

        // Case 9: Reflection damage does NOT alter LastPlayedType.
        state.FirstCombatBlockTriggered = true; // isolate pure SilverScreen 50% reflection from SilverMirrorFrame (+5)
        await Play<SilverScreen>(player, choice);
        Check(state.LastPlayedType == CardType.Skill && state.TempReflectPercent == 50,
            "V2 C11: SilverScreen grants 50% temporary reflection");
        hpBefore = enemy.CurrentHp;
        await CreatureCmd.Damage(choice, creature, 6, ValueProp.Move, enemy);
        Check(enemy.CurrentHp == hpBefore - 3 && state.LastPlayedType == CardType.Skill,
            "V2 CP#9a: reflection damage lands (50% of 6 = 3) without mutating LastPlayedType");
        await ClearHand(player, choice);
        handBefore = piles.Hand.Cards.Count;
        await Play<RazorPages>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore + 2,
            "V2 CP#9b: Skill -> Reflection -> Attack still triggers Counterpoint");

        // Case 10: Desire Skill (AbyssOfGambling) participates as Skill in Counterpoint.
        await ClearHand(player, choice);
        await CreatureCmd.SetCurrentHp(creature, 40);
        await Play<AbyssOfGambling>(player, choice);
        Check(state.LastPlayedType == CardType.Skill, "V2 CP#10a: Desire Skill sets LastPlayedType to Skill");
        await ClearHand(player, choice);
        handBefore = piles.Hand.Cards.Count;
        await Play<RazorPages>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore + 2,
            "V2 CP#10b: Desire Skill -> Attack triggers Counterpoint");

        // Case 11: Fallen Confidence (Desire Power) interrupts Counterpoint.
        await Play<WitchStrike>(player, choice);
        await Play<FallenConfidence>(player, choice);
        await ClearHand(player, choice);
        handBefore = piles.Hand.Cards.Count;
        await Play<ColdMirrorBarrier>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore,
            "V2 CP#11: Fallen Confidence (Desire Power) interrupts Counterpoint");

        // Case 12: C15 MirrorTransfer energy refund is capped at once per turn.
        await Play<WitchStrike>(player, choice);
        var energyBefore = piles.Energy;
        await Play<MirrorTransfer>(player, choice);
        var gainedFirst = piles.Energy - energyBefore;
        Check(gainedFirst >= 1 && state.MirrorTransferEnergyUsed,
            "V2 CP#12a: first Counterpoint MirrorTransfer grants extra energy");
        await Play<WitchStrike>(player, choice);
        energyBefore = piles.Energy;
        await Play<MirrorTransfer>(player, choice);
        Check(piles.Energy == energyBefore,
            "V2 CP#12b: second Counterpoint MirrorTransfer same turn grants no energy");

        // Case 15: Unplayable card does not mutate LastPlayedType or Counterpoint state.
        await Play<WitchDefend>(player, choice);
        await CreatureCmd.SetCurrentHp(creature, 1);
        var blockedGamble = await Add<AbyssOfGambling>(player);
        Check(!blockedGamble.CanPlay() && !blockedGamble.TryManualPlay(null) && state.LastPlayedType == CardType.Skill,
            "V2 CP#15: unplayable card fails cleanly without altering LastPlayedType");
        await CreatureCmd.SetCurrentHp(creature, 50);

        // Case 7 & 5b + Combo A: New turn resets LastPlayedType, caps, and temporary reflection,
        // then C19 (first Attack -> no Counterpoint) -> C15 -> C16 -> C14 runs on natural 3 Energy.
        await PlayerCmd.SetEnergy(0, player); // prevent LostFormula relic from adding start-of-turn energy
        var turn = piles.TurnNumber;
        selector.SelectFirstHand = false;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "V2 CP#7: advance to next turn");
        Check(state.LastPlayedType == null && !state.MirrorTransferEnergyUsed && !state.EmptyMirrorTriggered && state.TempReflectPercent == 0,
            "V2 CP#7: turn start resets LastPlayedType, caps, and temporary reflection");
        await CreatureCmd.LoseBlock(choice, enemy, enemy.Block, null);
        await RefillDraw(player, 15);
        await ClearHand(player, choice);
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        hpBefore = enemy.CurrentHp;
        var aEnergy = piles.Energy;
        // Play via manual queue on a natural 3-Energy turn to verify CP#5b + C15 Counterpoint energy refund!
        var c19 = await Add<RazorPages>(player);
        var c15 = await Add<MirrorTransfer>(player);
        var c16 = await Add<SeveredShadow>(player);
        var c14 = await Add<ColdMirrorBarrier>(player);
        var beforeC19Hand = piles.Hand.Cards.Count;
        await PlayManual(tree, c19, enemy);
        Check(piles.Hand.Cards.Count == beforeC19Hand,
            "V2 CP#5b: first Attack of new turn does not trigger Counterpoint (RazorPages plays -1, draws +1)");
        await PlayManual(tree, c15, null);
        await PlayManual(tree, c16, enemy);
        await PlayManual(tree, c14, null);
        // Damage: 7 (C19) + 10 (C16) = 17. Block: 6 (C15) + 8 (C14) = 14 (+3 from R15 EmptyMirror+ triggered on C19->C15 = 17).
        Check(enemy.CurrentHp == hpBefore - 17 && creature.Block >= 14 && piles.Energy >= aEnergy - 3
            && piles.Hand.Cards.Count >= 3,
            $"V2 Combo A: C19->C15->C16->C14 deals 17 (actual {hpBefore - enemy.CurrentHp}), gains Block ({creature.Block}), draws ({piles.Hand.Cards.Count}), refunds energy (delta {piles.Energy - aEnergy})");

        // Combo B: AbyssOfGambling (R27) -> BrittleGlass (U23) -> BackflowDraught (U15) -> BoilingEcho (U10)
        turn = piles.TurnNumber;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "V2 Combo B fresh turn");
        await CreatureCmd.SetCurrentHp(creature, 40);
        await CreatureCmd.SetCurrentHp(enemy, 400);
        await CreatureCmd.LoseBlock(choice, enemy, enemy.Block, null);
        await RefillDraw(player, 15);
        await ClearHand(player, choice);
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        await Play<AbyssOfGambling>(player, choice);
        Check(creature.CurrentHp == 20 && state.LifePaidByCardThisTurn && state.ExtraEnergy >= 2,
            "V2 Combo B1: Gambling pays 20 real HP, flags LifePaidByCardThisTurn, grants extra energy");
        hpBefore = enemy.CurrentHp;
        await Play<BrittleGlass>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 20,
            "V2 Combo B2: BrittleGlass hits twice (10x2=20) after real HP payment");
        selector.SelectFirstHand = true;
        await Add<WitchStrike>(player); // ensure discard candidate
        var bBlock = creature.Block;
        var bEnergy = piles.Energy;
        await Play<BackflowDraught>(player, choice);
        Check(creature.Block == bBlock + 4 && piles.Energy >= bEnergy + 1,
            "V2 Combo B3: BackflowDraught draws, discards for +1 Energy, and gains 4 Counterpoint Block");
        hpBefore = enemy.CurrentHp;
        await Play<BoilingEcho>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 27,
            "V2 Combo B4: BoilingEcho hits 3 times (9x3=27) with ExtraEnergy > 0");

        // Combo C: PrimalGreed (U37) -> EtchedMirrorSlash (C12) -> RazorPages (C19)
        turn = piles.TurnNumber;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "V2 Combo C fresh turn");
        await CreatureCmd.SetCurrentHp(creature, 50);
        await CreatureCmd.LoseBlock(choice, enemy, enemy.Block, null);
        await RefillDraw(player, 15);
        await ClearHand(player, choice);
        await Play<PrimalGreed>(player, choice);
        Check(state.DesirePlayedThisTurn && state.LastPlayedType == CardType.Skill,
            "V2 Combo C1: PrimalGreed flags DesirePlayedThisTurn and LastPlayedType=Skill");
        await ClearHand(player, choice);
        hpBefore = enemy.CurrentHp;
        handBefore = piles.Hand.Cards.Count;
        await Play<EtchedMirrorSlash>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 9 && piles.Hand.Cards.Count == handBefore + 2 && state.EmptyMirrorTriggered,
            "V2 Combo C2: EtchedMirrorSlash deals 9 and draws 1 after Desire (+1 from active EmptyMirror+)");
        await ClearHand(player, choice);
        handBefore = piles.Hand.Cards.Count;
        await Play<RazorPages>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore + 1,
            "V2 Combo C3: Attack -> RazorPages draws 1 (no Counterpoint)");

        // Combo D: RedCatalyst (R04) + SourceOfCalamity (R26) -> temp Desire -> discounted Attack (1/turn limit)
        turn = piles.TurnNumber;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "V2 Combo D fresh turn");
        await CreatureCmd.LoseBlock(choice, enemy, enemy.Block, null);
        await RefillDraw(player, 15);
        await ClearHand(player, choice);
        await Play<RedCatalyst>(player, choice, upgrade: true);
        Check(state.RedCatalystDraw == 2 && creature.GetPower<RedCatalystPower>()?.Amount == 2,
            "V2 Combo D1: upgraded RedCatalyst registers +2 draw on first Desire");
        var heavyAttack = await Add<FinalDistillation>(player);
        Check(heavyAttack.EnergyCost.GetWithModifiers(CostModifiers.All) == 3, "V2 Combo D2: attack starts at 3 cost");
        handBefore = piles.Hand.Cards.Count;
        selector.PreferredDesire = typeof(PrimalGreed);
        await Play<SourceOfCalamity>(player, choice, upgrade: true);
        // Calamity adds 1 temp Desire to hand + RedCatalyst+ draws 2 = +3 cards, and discounts next Attack by 1!
        Check(state.RedCatalystTriggered && state.NextAttackDiscount == 1
            && heavyAttack.EnergyCost.GetWithModifiers(CostModifiers.All) == 2
            && piles.Hand.Cards.Count == handBefore + 3
            && creature.GetPower<AttackDiscountPower>()?.Amount == 1,
            "V2 Combo D3: Calamity triggers RedCatalyst+ (+2 draw, -1 next Attack cost) and creates 0-cost Desire");
        var tempDesire = selector.LastDesire!;
        Check(tempDesire.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "V2 Combo D4: temp Desire costs 0");
        // Playing the attack consumes the discount cleanly!
        var dEnergy = piles.Energy;
        await PlayManual(tree, heavyAttack, enemy);
        Check(piles.Energy == dEnergy - 2 && state.NextAttackDiscount == 0 && creature.GetPower<AttackDiscountPower>() == null,
            "V2 Combo D5: playing discounted FinalDistillation costs 2 and clears AttackDiscount");
        // Playing a second Desire same turn must NOT re-trigger RedCatalyst.
        await Play<AbyssOfGambling>(player, choice);
        Check(state.NextAttackDiscount == 0, "V2 Combo D6: second Desire same turn does not re-trigger RedCatalyst");

        // Combo E: SilverScreen (+50%) + MirrorMaze (+100%) + ReflectiveInscription (+25%) + TheMirrorSelf (+100%)
        await CreatureCmd.SetCurrentHp(creature, 30);
        await CreatureCmd.SetCurrentHp(enemy, 400);
        await PowerCmd.Remove<StrengthPower>(enemy);
        await CreatureCmd.LoseBlock(choice, enemy, enemy.Block, null);
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        await Play<TheMirrorSelf>(player, choice);
        await Play<ReflectiveInscription>(player, choice);
        await Play<SilverScreen>(player, choice);
        await Play<MirrorMaze>(player, choice);
        Check(state.MirrorForm && state.TempReflectPercent == 150 && state.InscriptionReflectGain == 25 && state.BaseReflectPercent == 0,
            "V2 Combo E1: additive TempReflectPercent = 50 + 100 = 150 alongside MirrorForm 100%");
        // Give enough Block for 2 hits of 8 damage.
        await CreatureCmd.GainBlock(creature, 20, ValueProp.Unpowered, null);
        // Hit 1: 8 blocked * (100 + 150)% = 20 damage (+5 SilverMirrorFrame if first combat block; neutralize frame check first)
        state.FirstCombatBlockTriggered = true;
        hpBefore = enemy.CurrentHp;
        await CreatureCmd.Damage(choice, creature, 8, ValueProp.Move, enemy);
        Check(enemy.CurrentHp == hpBefore - 20 && state.BaseReflectPercent == 25 && state.TempReflectPercent == 150,
            "V2 Combo E2: hit 1 reflects 250% (20), triggers Inscription (+25% base), preserves TempReflect across hits");
        // Hit 2 same enemy turn: 8 blocked * (100 + 25 + 150)% = 22 damage, Inscription does NOT trigger twice same turn!
        hpBefore = enemy.CurrentHp;
        await CreatureCmd.Damage(choice, creature, 8, ValueProp.Move, enemy);
        Check(enemy.CurrentHp == hpBefore - 22 && state.BaseReflectPercent == 25,
            "V2 Combo E3: hit 2 reflects 275% (22) and Inscription only grows once per turn");
        // Cap test: verify AddCappedReflect caps BaseReflectPercent at 100 even with multiple Inscriptions.
        state.BaseReflectPercent = 75;
        state.Reflected = false;
        await Play<ReflectiveInscription>(player, choice, upgrade: true); // +50 more gain (total gain 75)
        await CreatureCmd.GainBlock(creature, 10, ValueProp.Unpowered, null);
        await CreatureCmd.Damage(choice, creature, 4, ValueProp.Move, enemy);
        Check(state.BaseReflectPercent == 100, "V2 Combo E4: ReflectiveInscription BaseReflectPercent caps conservatively at 100%");
        // Turn boundary: TempReflectPercent resets, BaseReflectPercent persists!
        turn = piles.TurnNumber;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "V2 Combo E next turn");
        Check(state.TempReflectPercent == 0 && state.BaseReflectPercent == 100 && creature.GetPower<BaseReflectPower>()?.Amount == 100,
            "V2 Combo E5: TempReflect clears at turn start while BaseReflect persists");

        // Combo F: Purification (U05) Exhausts target into ExhaustPile (NOT DiscardPile), never self, upgraded draws first.
        await RefillDraw(player, 10);
        await ClearHand(player, choice);
        var strikeTarget = await Add<WitchStrike>(player);
        var deckCountBefore = player.Deck.Cards.Count;
        selector.SelectFirstHand = true;
        var fEnergy = piles.Energy;
        var purify = await Add<Purification>(player);
        await PlayManual(tree, purify, null);
        Check(strikeTarget.Pile?.Type == PileType.Exhaust && !piles.DiscardPile.Cards.Contains(strikeTarget)
            && purify.Pile?.Type == PileType.Exhaust && piles.Energy >= fEnergy + 1
            && player.Deck.Cards.Count == deckCountBefore,
            "V2 Combo F1: Purification exhausts chosen Strike into ExhaustPile, exhausts self, grants Energy, keeps deck intact");
        // Empty hand edge case: Purification with no other hand card must not throw or exhaust itself prematurely.
        await ClearHand(player, choice);
        fEnergy = piles.Energy;
        var soloPurify = await Add<Purification>(player);
        await PlayManual(tree, soloPurify, null);
        Check(soloPurify.Pile?.Type == PileType.Exhaust && piles.Energy == fEnergy,
            "V2 Combo F2: Purification with no other hand card safely gains 0 Energy and exhausts normally");
        // Upgraded Purification draws 1 first, then exhausts the drawn card for +1 Energy!
        await ClearHand(player, choice);
        await RefillDraw(player, 5);
        fEnergy = piles.Energy;
        var upgPurify = await Add<Purification>(player, upgrade: true);
        await PlayManual(tree, upgPurify, null);
        Check(upgPurify.Pile?.Type == PileType.Exhaust && piles.Energy >= fEnergy + 1,
            "V2 Combo F3: upgraded Purification draws 1 first, then exhausts it for +1 Energy");

        // Combo G: Half-Moon Ledger (U34) + PrimalGreed (U37) + Exhaust-Attack priority + Counterpoint #14
        await ClearHand(player, choice);
        await RefillDraw(player, 6);
        var ledger = await Add<HalfMoonLedger>(player, upgrade: true);
        Check(ledger.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "V2 Combo G0: upgraded Half-Moon Ledger costs 0");
        await PlayManual(tree, ledger, null);
        Check(state.PendingLedgerAttack && creature.GetPower<HalfMoonLedgerPower>() != null,
            "V2 Combo G1: Half-Moon Ledger arms PendingLedgerAttack");
        var recycledAttack = await Add<WitchStrike>(player);
        var allBeforeG = SnapshotPiles(player);
        handBefore = piles.Hand.Cards.Count;
        await PlayManual(tree, recycledAttack, enemy);
        var allAfterG = SnapshotPiles(player);
        Check(!state.PendingLedgerAttack && creature.GetPower<HalfMoonLedgerPower>() == null
            && (recycledAttack.Pile?.Type == PileType.Draw || recycledAttack.Pile?.Type == PileType.Hand)
            && !piles.DiscardPile.Cards.Contains(recycledAttack)
            && allAfterG.Length == allBeforeG.Length
            && allAfterG.Distinct(ReferenceEqualityComparer.Instance).Count() == allAfterG.Length,
            "V2 Combo G2: Half-Moon Ledger shuffles resolved Attack into Draw (may redraw it) without duplication or loss");
        // CP#14: Playing a Skill after the ledger-recycled Attack triggers Counterpoint cleanly!
        await ClearHand(player, choice);
        handBefore = piles.Hand.Cards.Count;
        await Play<ColdMirrorBarrier>(player, choice);
        Check(piles.Hand.Cards.Count == handBefore + 1,
            "V2 CP#14: Skill after Ledger-recycled Attack triggers Counterpoint");
        // Exhaust-Attack priority rule: TurnInward (Exhaust) stays in ExhaustPile and still draws 1.
        await Play<HalfMoonLedger>(player, choice);
        var exhaustAttack = await Add<TurnInward>(player);
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null); // no block so TurnInward itself draws 0
        handBefore = piles.Hand.Cards.Count;
        await PlayManual(tree, exhaustAttack, enemy);
        Check(exhaustAttack.Pile?.Type == PileType.Exhaust && piles.Hand.Cards.Count == handBefore, // -1 played + 1 ledger draw
            "V2 Combo G3: Half-Moon Ledger respects Exhaust Attack destination and still draws 1");
        // Combined with PrimalGreed: instance conservation holds!
        await Play<HalfMoonLedger>(player, choice);
        var greedAttack = await Add<WitchStrike>(player);
        var greedCard = await Add<PrimalGreed>(player);
        var snapBeforeGreed = SnapshotPiles(player);
        await PlayManual(tree, greedAttack, enemy);
        await PlayManual(tree, greedCard, null);
        var snapAfterGreed = SnapshotPiles(player);
        Check(snapAfterGreed.Length == snapBeforeGreed.Length
            && snapAfterGreed.ToHashSet(ReferenceEqualityComparer.Instance).SetEquals(snapBeforeGreed),
            "V2 Combo G4: Half-Moon Ledger + PrimalGreed preserves every card instance with zero duplication or loss");

        // ============================================================
        // 13.1 Additional V2.0 Card Rules Spot Checks
        // ============================================================
        // C05 Residue: 0 HP loss, +1 Energy, upgraded draws 1.
        await CreatureCmd.SetCurrentHp(creature, 30);
        await ClearHand(player, choice);
        var resHp = creature.CurrentHp;
        var resEnergy = piles.Energy;
        await Play<Residue>(player, choice, upgrade: true);
        Check(creature.CurrentHp == resHp && piles.Energy >= resEnergy + 1 && piles.Hand.Cards.Count == 1,
            "V2 C05: upgraded Residue loses 0 HP, gains Energy, draws 1");

        // U14 Blue Flame Formula: buffs ONLY ONE hit of multi-hit BoilingEcho!
        await PlayerCmd.SetEnergy(0, player); // prevent LostFormula relic from consuming first-extra-energy trigger before U14
        turn = piles.TurnNumber;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "V2 U14 fresh turn");
        await CreatureCmd.SetCurrentHp(creature, 50);
        await CreatureCmd.SetCurrentHp(enemy, 400);
        await CreatureCmd.LoseBlock(choice, enemy, enemy.Block, null);
        await Play<BlueFlameFormula>(player, choice); // +6 single hit
        await Play<Residue>(player, choice); // triggers first extra energy -> PendingSingleHitAttackBonus = 6
        Check(state.PendingSingleHitAttackBonus == 6 && creature.GetPower<BlueFlameReadyPower>()?.Amount == 6,
            "V2 U14a: Blue Flame Formula arms +6 single-hit bonus on first extra energy");
        hpBefore = enemy.CurrentHp;
        // With ExtraEnergy > 0, BoilingEcho hits 3 times (9 each) + 6 on FIRST hit only = 15 + 9 + 9 = 33!
        await Play<BoilingEcho>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 33 && state.PendingSingleHitAttackBonus == 0 && state.ActiveSingleHitBonus == 0,
            $"V2 U14b: Blue Flame buffs only 1 hit of 3-hit BoilingEcho (expected 33, actual {hpBefore - enemy.CurrentHp})");

        // R16 Mirror Cage: max(Block, 10) twice (snapshot before hits).
        await CreatureCmd.LoseBlock(choice, creature, creature.Block, null);
        hpBefore = enemy.CurrentHp;
        await Play<MirrorCage>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 20, "V2 R16a: Mirror Cage deals floor 10x2 = 20 at 0 Block");
        await CreatureCmd.GainBlock(creature, 15, ValueProp.Unpowered, null);
        hpBefore = enemy.CurrentHp;
        await Play<MirrorCage>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 30, "V2 R16b: Mirror Cage deals 15x2 = 30 at 15 Block");

        // U35 Dark Veil Ward (Counterpoint next Attack cost -1) + U07 Elixir Phial (Counterpoint Energy, no heal).
        await Play<WitchStrike>(player, choice);
        var discounted = await Add<Refraction>(player);
        await Play<DarkVeilWard>(player, choice);
        Check(state.NextAttackDiscount == 1 && discounted.EnergyCost.GetWithModifiers(CostModifiers.All) == 1,
            "V2 U35: Counterpoint Dark Veil Ward reduces next Attack cost by 1");
        await Play<WitchStrike>(player, choice);
        var eBeforeElixir = piles.Energy;
        var hpBeforeElixir = creature.CurrentHp;
        await Play<ElixirPhial>(player, choice, upgrade: true);
        Check(creature.CurrentHp == hpBeforeElixir && piles.Energy >= eBeforeElixir + 1,
            "V2 U07: Counterpoint Elixir Phial grants Energy without healing");

        // U30 PageExchange (Counterpoint optional discard) + U32 SigilBlade (optional discard + Counterpoint draw)
        await Play<WitchStrike>(player, choice);
        await ClearHand(player, choice);
        await RefillDraw(player, 10);
        selector.SelectFirstHand = false; // decline optional discard
        await Play<PageExchange>(player, choice);
        Check(piles.Hand.Cards.Count == 3, "V2 U30: Counterpoint PageExchange allows skipping discard (keeps all 3 drawn)");
        selector.SelectFirstHand = true; // accept discard on SigilBlade
        hpBefore = enemy.CurrentHp;
        handBefore = piles.Hand.Cards.Count;
        await Play<SigilBlade>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 18 && piles.Hand.Cards.Count == handBefore, // -1 discard +1 Counterpoint draw
            "V2 U32: Counterpoint SigilBlade discards 1 to hit twice (9x2=18) and draws 1");

        // R08 FinalDistillation: draws 1 without kill, +1 draw +1 Energy on kill.
        await ClearHand(player, choice);
        await RefillDraw(player, 10);
        hpBefore = enemy.CurrentHp;
        await Play<FinalDistillation>(player, choice);
        Check(enemy.CurrentHp == hpBefore - 28 && piles.Hand.Cards.Count == 1,
            "V2 R08a: FinalDistillation deals 28 and draws 1 even without kill");

        // U31 ForbiddenFrontispiece: player-chosen Retain selects the exact chosen card.
        await ClearHand(player, choice);
        var chosenRetain = await Add<SilentVow>(player);
        var normalCard = await Add<WitchDefend>(player);
        await Play<ForbiddenFrontispiece>(player, choice);
        selector.SelectFirstHand = true; // chooses first non-retaining card = normalCard
        turn = piles.TurnNumber;
        CombatManager.Instance.SetReadyToEndTurn(player, canBackOut: false);
        await WaitFor(tree, () => piles.TurnNumber > turn && piles.Phase == PlayerTurnPhase.Play, "V2 U31 retain transition");
        Check(state.RetainedCards.Contains(normalCard) && piles.Hand.Cards.Contains(normalCard),
            "V2 U31: ForbiddenFrontispiece retains the player-chosen hand card");

        // Verify R08 kill bonus in a multi-enemy battle so CombatManager.IsEnding does not suppress GainEnergy!
        var secondEnemy = await CreatureCmd.Add(enemy.Monster!.CanonicalInstance.ToMutable(), creature.CombatState!);
        await CreatureCmd.LoseBlock(choice, enemy, enemy.Block, null);
        await CreatureCmd.SetCurrentHp(enemy, 20);
        await ClearHand(player, choice);
        await RefillDraw(player, 5);
        var killEnergy = piles.Energy;
        var killCard = await Add<FinalDistillation>(player);
        await CardCmd.AutoPlay(choice, killCard, enemy);
        Check(enemy.IsDead && secondEnemy.IsAlive && piles.Hand.Cards.Count == 2 && piles.Energy >= killEnergy + 1,
            "V2 R08b: FinalDistillation kill grants base draw + bonus draw (2) and bonus Energy");
        await CreatureCmd.Damage(choice, secondEnemy, 999, ValueProp.Unpowered, creature);
        await CombatManager.Instance.CheckWinCondition();
        await WaitFor(tree, () => !CombatManager.Instance.IsInProgress, "V2 dedicated test battle won");
        await LeaveCompletedCombat(tree);
    }

    private static async Task PlayManual(SceneTree tree, CardModel card, MegaCrit.Sts2.Core.Entities.Creatures.Creature? target)
    {
        var history = CombatManager.Instance.History;
        var finished = history.CardPlaysFinished.Count();
        Check(card.TryManualPlay(target), $"V2 manual play accepted for {card.Id.Entry}");
        await WaitFor(tree, () => history.CardPlaysFinished.Count() > finished && !RunManager.Instance.ActionExecutor.IsRunning,
            $"V2 manual play finished for {card.Id.Entry}");
    }

    private static async Task RefillDraw(Player player, int count)
    {
        var combat = (CombatState)player.Creature.CombatState!;
        for (var i = 0; i < count; i++)
        {
            var card = combat.CreateCard<WitchStrike>(player);
            await CardPileCmd.AddGeneratedCardToCombat(card, PileType.Draw, player);
        }
    }

    private static async Task ClearHand(Player player, PlayerChoiceContext choice)
    {
        foreach (var card in player.PlayerCombatState!.Hand.Cards.ToArray())
        {
            await CardCmd.Discard(choice, card);
        }
    }

    private static CardModel[] SnapshotPiles(Player player)
    {
        var piles = player.PlayerCombatState!;
        return piles.Hand.Cards.Concat(piles.DrawPile.Cards).Concat(piles.PlayPile.Cards)
            .Concat(piles.DiscardPile.Cards).Concat(piles.ExhaustPile.Cards).ToArray();
    }
}
