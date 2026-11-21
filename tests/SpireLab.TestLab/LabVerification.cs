using BaseLib.Abstracts;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Encounters;
using MegaCrit.Sts2.Core.ValueProps;
using SpireLab.SpireLabCode.Witch.Cards;
using SpireLab.SpireLabCode.Witch.Mechanics;

namespace SpireLab.TestLab;

// Loaded only by the separate Debug harness. Executes one controlled hit in the real end phase.
public sealed class LabEndTurnHit() : CustomSingletonModel(HookType.Combat)
{
    internal static Creature? Target { get; set; }

    public override async Task BeforeSideTurnEnd(PlayerChoiceContext choice, CombatSide side,
        IEnumerable<Creature> participants)
    {
        if (side != CombatSide.Player || Target is not { } enemy) return;
        Target = null;
        await CreatureCmd.Damage(choice, enemy, 999, ValueProp.Unpowered, participants.First());
    }
}

internal sealed partial class Lab
{
    private async Task Verify()
    {
        void Check(bool passed, string description)
        {
            if (!passed) throw new InvalidOperationException(description);
            Report($"PASS {description}");
        }

        Check(_cards.Length == 91 && _relics.Length == 9 && _potions.Length == 3, "91 registered cards, 9 relics, 3 potions");
        var native = (CombatState)Witch.CombatState!;
        foreach (var definition in _cards)
        {
            var original = native.CreateCard(definition, _player);
            var upgrade = native.CreateCard(definition, _player);
            if (upgrade.IsUpgradable) { CardCmd.Upgrade(upgrade); Check(upgrade.IsUpgraded, definition.SpecId + " real upgrade"); }
            original.RemoveFromState();
            upgrade.RemoveFromState();
        }
        Check(!ModelDb.Card<PrimalGreed>().Keywords.Contains(CardKeyword.Exhaust), "Primal Greed has no Exhaust keyword");
        var foe = Witch.CombatState!.HittableEnemies.First();
        await CreatureCmd.SetMaxHp(foe, 500);
        await CreatureCmd.SetCurrentHp(foe, 500);
        await CreatureCmd.SetCurrentHp(Witch, 34);

        async Task Reflect(string name, int initialBlock, int[] hits, int[] expected)
        {
            await SetBlock(initialBlock);
            for (var i = 0; i < hits.Length; i++)
            {
                var before = foe.CurrentHp;
                await Hit(foe, hits[i]);
                Check(before - foe.CurrentHp == expected[i],
                    $"{name} hit {i + 1}: expected {expected[i]}, actual {before - foe.CurrentHp}");
            }
        }

        State.MirrorForm = false;
        await AddToPlay<TheMirrorSelf>();
        await Reflect("Mirror Self", 10, [6], [6]);
        await Reflect("partial block", 10, [15], [10]);
        await Reflect("multi-hit", 10, [4, 4, 4], [4, 4, 2]);
        State.MirrorForm = false;
        await AddToPlay<MirrorMaze>();
        await Reflect("Mirror Maze alone", 10, [6], [6]);
        State.MazeReflectPercent = 0;
        await AddToPlay<MirrorMaze>(true);
        await Reflect("upgraded Mirror Maze alone", 10, [6], [9]);
        State.MazeReflectPercent = 0;
        await AddToPlay<TheMirrorSelf>();
        await AddToPlay<MirrorMaze>();
        await Reflect("Mirror Self + Maze", 10, [6], [12]);
        await AddToPlay<MirrorBurial>();
        await Reflect("Mirror Self + Maze + Burial", 10, [6, 4], [18, 8]);
        State.MazeReflectPercent = 0;
        await AddToPlay<MirrorBurial>();
        await Reflect("Mirror Self + Burial", 10, [6], [12]);
        await AddToPlay<ThinMirror>(true);
        await Reflect("no block leaves fixed reflection queued", 0, [6], [0]);
        await Reflect("fixed + Mirror Self", 10, [6], [13]);
        State.MirrorForm = false;
        State.MirrorHp = 0;
        await SetBlock(0);
        await AddToPlay<ThinMirror>();
        Check(Witch.Block == 9, "early-game Thin Mirror grants 9 Block for one energy");
        var earlyHp = Witch.CurrentHp;
        await Reflect("early-game Thin Mirror vs 12", 9, [12], [5]);
        Check(Witch.CurrentHp == earlyHp - 3, "Thin Mirror leaves 3 real HP damage from a 12 damage hit");
        State.MirrorForm = true;
        var reflectedHp = foe.CurrentHp;
        await SetBlock(0);
        State.MirrorHp = 10;
        await Hit(foe, 4, nonAttack: true);
        Check(State.MirrorHp == 6 && foe.CurrentHp == reflectedHp, "non-attack damage consumes Mirror HP without reflecting");
        await Hit(foe, 6);
        Check(State.MirrorHp == 0 && foe.CurrentHp == reflectedHp, "Mirror HP is not Block");
        await SetBlock(10);
        State.SlothImmunity = true;
        await RefreshStatuses();
        var playerHp = Witch.CurrentHp;
        var foeHp = foe.CurrentHp;
        await Hit(foe, 4);
        await Hit(foe, 4);
        await Hit(foe, 4, nonAttack: true);
        Check(Witch.Block == 10 && Witch.CurrentHp == playerHp && foe.CurrentHp == foeHp, "Sloth blocks repeated attacks and non-attack damage without consuming Block or reflecting");
        Check(await WitchLife.Pay(_choice, _player, 1, fromCard: false) && Witch.CurrentHp == playerHp - 1,
            "direct HP payment still applies during immunity");
        State.SlothImmunity = false;
        await RefreshStatuses();
        await AddToPlay<MoonInTheMirror>();
        await Reflect("Moon in the Mirror ignores the Block formula", 10, [6], [12]);
        Check(Witch.Block == 10 && State.Immunity == null, "Moon leaves Block untouched and spends its immunity once");

        await AddToPlay<FallenConfidence>();
        var greed = (PrimalGreed)await Add(ModelDb.Card<PrimalGreed>(), PileType.Hand, true);
        Check(greed.EnergyCost.GetWithModifiers(CostModifiers.All) == 0, "upgraded Greed is free with Fallen Confidence");
        var instances = Piles.SelectMany(pile => pile.GetPile(_player).Cards).ToHashSet(ReferenceEqualityComparer.Instance);
        using (CardSelectCmd.PushSelector(new InitialChoice()))
            await CardCmd.AutoPlay(_choice, greed, null);
        Check(greed.Pile?.Type == PileType.Discard &&
            instances.SetEquals(Piles.SelectMany(pile => pile.GetPile(_player).Cards)), "Greed naturally discards without copying combat cards");

        _hits.Text = "4,4,4";
        await Save();
        await Load();
        Check(_scenario != "" && _hits.Text == "4,4,4", "saved test fixture restarts from independent JSON");
        await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        using (var image = tree.Root.GetTexture().GetImage())
            Check(image.SavePng(Path.Combine(_root, "lab-macos.png")) == Error.Ok, "captured the running Lab");

        _encounter = ModelDb.Encounter<NibbitsNormal>();
        await Start("mirror-basic");
        var foes = Witch.CombatState!.HittableEnemies;
        Check(foes.Count == 2, "native two-enemy encounter");
        await CreatureCmd.SetCurrentHp(foes[1], 1);
        var untouched = foes[0].CurrentHp;
        await Hit(foes[1], 4);
        Check(foes[1].IsDead && foes[0].CurrentHp == untouched && CombatManager.Instance.IsInProgress,
            "reflection kills the actual attacker only");
        _encounter = ModelDb.Encounter<NibbitsWeak>();

        async Task<CardModel> SetupGamble(int hp)
        {
            await Start("early-game");
            await CreatureCmd.SetCurrentHp(Witch, hp);
            return await Add(ModelDb.Card<AbyssOfGambling>(), PileType.Hand);
        }

        async Task KillEnemy()
        {
            await CreatureCmd.Damage(_choice, Witch.CombatState!.HittableEnemies.First(), 999,
                ValueProp.Unpowered, Witch);
            await CombatManager.Instance.CheckWinCondition();
            await Wait(() => !CombatManager.Instance.IsInProgress);
        }

        var gamble = await SetupGamble(40);
        await CardCmd.AutoPlay(_choice, gamble, null);
        Check(Witch.CurrentHp == 20 && State.GamblePaidThisTurn == 20, "Gamble pays 20 real HP");
        await CardCmd.AutoPlay(_choice, gamble, null);
        Check(Witch.CurrentHp == 10 && State.GamblePaidThisTurn == 30, "Gamble aggregates two real payments in the same turn");
        await CreatureCmd.SetCurrentHp(Witch, 65);
        await KillEnemy();
        Check(Witch.CurrentHp == 68, "same-turn victory restores only up to maximum");
        gamble = await SetupGamble(40);
        State.MirrorHp = 20;
        await CardCmd.AutoPlay(_choice, gamble, null);
        Check(Witch.CurrentHp == 20 && State.MirrorHp == 20, "Gamble leaves Mirror HP untouched");
        await WitchLife.Pay(_choice, _player, 2, fromCard: true);
        await KillEnemy();
        Check(Witch.CurrentHp == 38, "Gamble does not refund a different payment");
        gamble = await SetupGamble(40);
        await CardCmd.AutoPlay(_choice, gamble, null);
        LabEndTurnHit.Target = Witch.CombatState!.HittableEnemies.First();
        using (CardSelectCmd.PushSelector(new InitialChoice()))
        {
            PlayerCmd.EndTurn(_player, canBackOut: false);
            await Wait(() => !CombatManager.Instance.IsInProgress);
        }
        Check(Witch.CurrentHp == 40 && LabEndTurnHit.Target == null, "win in the native player end phase refunds Gamble");
        gamble = await SetupGamble(40);
        await CardCmd.AutoPlay(_choice, gamble, null);
        var turn = Combat.TurnNumber;
        using (CardSelectCmd.PushSelector(new InitialChoice()))
        {
            PlayerCmd.EndTurn(_player, canBackOut: false);
            await Wait(() => Combat.TurnNumber > turn && Combat.Phase == PlayerTurnPhase.Play);
        }
        playerHp = Witch.CurrentHp;
        await KillEnemy();
        Check(Witch.CurrentHp == playerHp, "a later-turn win does not restore old payments");
        gamble = await SetupGamble(40);
        await CardCmd.AutoPlay(_choice, gamble, null);
        await CreatureCmd.Kill(Witch);
        await CombatManager.Instance.CheckWinCondition();
        await Wait(() => !CombatManager.Instance.IsInProgress);
        Check(Witch.CurrentHp == 0, "defeat grants no refund");

        _encounter = ModelDb.Encounter<LabDummyEncounter>();
        _hits.Text = "4,4,4";
        _nonAttack.ButtonPressed = false;
        await Start("mirror-basic");
        var scripted = Witch.CombatState!.HittableEnemies.Single();
        var scriptedHp = scripted.CurrentHp;
        var nextTurn = Combat.TurnNumber;
        var traceAtStart = _records.Count;
        using (CardSelectCmd.PushSelector(new InitialChoice()))
        {
            PlayerCmd.EndTurn(_player, canBackOut: false);
            await Wait(() => Combat.TurnNumber > nextTurn && Combat.Phase == PlayerTurnPhase.Play);
        }
        var results = _records.Skip(traceAtStart).Where(line => line.StartsWith("反射 · ", StringComparison.Ordinal))
            .Select(line => int.Parse(line.Split(" · 最终 ")[1])).ToArray();
        Check(scriptedHp - scripted.CurrentHp == 10 && results.SequenceEqual([4, 4, 2]),
            "scripted dummy executes 4/4/4 in the native enemy turn, reflections 4/4/2");

        await File.WriteAllLinesAsync(Path.Combine(_root, "combat-events.log"), _records);
        GD.Print("[WitchTestLab] PASS");
        tree.Quit();
    }
}
