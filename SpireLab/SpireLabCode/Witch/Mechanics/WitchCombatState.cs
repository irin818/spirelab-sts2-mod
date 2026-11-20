using System.Runtime.CompilerServices;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;

namespace SpireLab.SpireLabCode.Witch.Mechanics;

internal sealed class WitchCombatState
{
    private static readonly ConditionalWeakTable<PlayerCombatState, WitchCombatState> States = new();

    public static WitchCombatState? Find(Player? player) =>
        player?.Character is TheWitch && player.PlayerCombatState is { } combat
            ? States.GetValue(combat, state => new WitchCombatState(state))
            : null;

    public static WitchCombatState For(Player player) =>
        Find(player) ?? throw new InvalidOperationException("Witch rules require a live Witch combat state.");

    private WitchCombatState(PlayerCombatState combat)
    {
        combat.EnergyChanged += (before, after) =>
        {
            if (TrackEnergy && after > before)
            {
                ExtraEnergy = (int)Math.Min(int.MaxValue, (long)ExtraEnergy + after - before);
            }
        };
    }

    public Dictionary<CardModel, CardModel?> StandIns { get; } = new(ReferenceEqualityComparer.Instance);
    public Dictionary<CardModel, int> HandInsertions { get; } = new(ReferenceEqualityComparer.Instance);
    public bool TrackEnergy { get; set; }
    public int ExtraEnergy { get; private set; }
    public int TurnEnlightenments { get; set; }
    public int CombatEnlightenments { get; set; }
    public int Plays { get; set; }
    public int BlockGains { get; set; }
    public int NextEnergy { get; set; }
    public int NextDraw { get; set; }
    public int NextBlock { get; set; }
    public int LastRemainingEnergy { get; set; }
    public int MirrorHp { get; set; }
    public bool Reflected { get; set; }
    public bool BlockedDuringEnemyTurn { get; set; }
    public bool ReflectedDuringEnemyTurn { get; set; }
    public bool BlockedLastEnemyTurn { get; set; }
    public bool ReflectedLastEnemyTurn { get; set; }
    public bool MirrorForm { get; set; }
    public bool DesireDiscount { get; set; }
    public bool RetainEnergy { get; set; }
    public int NextReflect { get; set; }
    public int? Immunity { get; set; }
    public int? SpentImmunity { get; set; }
    public int CardLifeLost { get; set; }

    public void BeginTurn()
    {
        TrackEnergy = false;
        ExtraEnergy = 0;
        TurnEnlightenments = 0;
        Plays = 0;
        BlockGains = 0;
        Reflected = false;
        BlockedLastEnemyTurn = BlockedDuringEnemyTurn;
        ReflectedLastEnemyTurn = ReflectedDuringEnemyTurn;
        BlockedDuringEnemyTurn = false;
        ReflectedDuringEnemyTurn = false;
        NextReflect = 0;
        Immunity = null;
        SpentImmunity = null;
    }
}
