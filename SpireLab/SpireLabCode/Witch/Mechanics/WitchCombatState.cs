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
                EnergyEvents.Enqueue(!SuppressCardEnergySource && (CardEnergySource || ResolvingCard));
            }
        };
    }

    public bool OpeningChoiceResolved { get; set; }
    public CardModel? OpeningCard { get; set; }
    public bool TraitSwapUsed { get; set; }
    public bool TraitSwapOffered { get; set; }
    public bool TrackEnergy { get; set; }
    public int ExtraEnergy { get; private set; }
    public int TurnEnlightenments { get; set; }
    public int CombatEnlightenments { get; set; }
    public int Plays { get; set; }
    public int BlockGains { get; set; }
    public int NextEnergy { get; set; }
    public int NextDraw { get; set; }
    public int NextBlock { get; set; }
    public int RetainedRelicEnergy { get; set; }
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
    public List<int> DeferredReflections { get; } = [];
    public int NextReflectPercent { get; set; }
    public int WeakOnBlock { get; set; }
    public bool InscriptionEnabled { get; set; }
    public int InscriptionWard { get; set; }
    public int NextSkillDiscount { get; set; }
    public int NextSkillWard { get; set; }
    public int SkillDiscount { get; set; }
    public int SkillWard { get; set; }
    public decimal ReflectMultiplier { get; set; } = 1;
    public int ShadowDraw { get; set; }
    public int BlockReduction { get; set; }
    public int BlockBeforeAttack { get; set; }
    public int PendingAttackBonus { get; set; }
    public int ActiveAttackBonus { get; set; }
    public int CrownDamage { get; set; }
    public int? Immunity { get; set; }
    public int? SpentImmunity { get; set; }
    public int CardLifeLost { get; private set; }
    public void RecordCardLifeLoss(int amount) => CardLifeLost = (int)Math.Min(10L, (long)CardLifeLost + amount);
    public bool LifePaymentTriggered { get; set; }
    public bool VeilTriggered { get; set; }
    public bool FirstCombatBlockTriggered { get; set; }

    public int StartingEnergy { get; set; }
    public List<int> ManaPumpDurations { get; } = [];
    public int RetainedBlockCap { get; set; }
    public int BlueFlameDamage { get; set; }
    public bool SevenfoldDraw { get; set; }
    public bool EnergyTriggered { get; set; }
    public bool ResolvingCard { get; set; }
    public bool CardEnergySource { get; set; }
    public bool SuppressCardEnergySource { get; set; }
    public bool ResolvingEnergy { get; set; }
    public bool CrackedPhialUsed { get; set; }
    public bool DistillerUsed { get; set; }
    public Queue<bool> EnergyEvents { get; } = new();

    public List<(int PerEnergy, int Cap)> TurnEndEnergyBlocks { get; } = [];
    public int DepthsEnergy { get; set; }
    public int DepthsBlock { get; set; }
    public int RedCatalystBlock { get; set; }
    public int ReverseRiteDraw { get; set; }
    public int HighPrintedPlays { get; set; }
    public int DuskDraw { get; set; }
    public int DuskBlock { get; set; }
    public bool FreePlayTriggered { get; set; }

    public decimal BlockMultiplier { get; set; } = 1;
    public int FirstBlockPercent { get; set; }
    public int RetainRandomCount { get; set; }
    public HashSet<CardModel> RetainedCards { get; } = new(ReferenceEqualityComparer.Instance);
    public int BookmarkEnergy { get; set; }
    public bool BookmarkUsed { get; set; }

    public void BeginTurn()
    {
        TrackEnergy = false;
        ExtraEnergy = 0;
        TraitSwapUsed = false;
        TraitSwapOffered = false;
        BookmarkUsed = false;
        TurnEnlightenments = 0;
        Plays = 0;
        BlockGains = 0;
        Reflected = false;
        BlockedLastEnemyTurn = BlockedDuringEnemyTurn;
        ReflectedLastEnemyTurn = ReflectedDuringEnemyTurn;
        BlockedDuringEnemyTurn = false;
        ReflectedDuringEnemyTurn = false;
        NextReflectPercent = 0;
        WeakOnBlock = 0;
        SkillDiscount = NextSkillDiscount;
        SkillWard = NextSkillWard;
        NextSkillDiscount = 0;
        NextSkillWard = 0;
        ReflectMultiplier = 1;
        ActiveAttackBonus = 0;
        ResolvingCard = false;
        EnergyTriggered = false;
        DistillerUsed = false;
        HighPrintedPlays = 0;
        FreePlayTriggered = false;
        BlockMultiplier = 1;
        TurnEndEnergyBlocks.Clear();
        Immunity = null;
        SpentImmunity = null;
    }
}
