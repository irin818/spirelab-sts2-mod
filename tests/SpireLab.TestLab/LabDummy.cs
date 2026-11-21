using BaseLib.Abstracts;
using MegaCrit.Sts2.Core.Animation;
using MegaCrit.Sts2.Core.Bindings.MegaSpine;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.ValueProps;

namespace SpireLab.TestLab;

public sealed class LabDummyEncounter() : CustomEncounterModel(RoomType.Monster, autoAdd: false)
{
    internal int Dummies { get; set; } = 1;
    internal int[] Hits { get; set; } = [];
    internal bool NonAttack { get; set; }

    public override bool IsValidForAct(ActModel act) => false;
    public override IEnumerable<MonsterModel> AllPossibleMonsters => [ModelDb.Monster<LabDummy>()];

    protected override IReadOnlyList<(MonsterModel, string?)> GenerateMonsters() =>
        Enumerable.Range(0, Dummies).Select(_ => (CreateDummy(), (string?)null)).ToArray();

    private MonsterModel CreateDummy()
    {
        var dummy = (LabDummy)ModelDb.Monster<LabDummy>().ToMutable();
        dummy.Hits = Hits;
        dummy.NonAttack = NonAttack;
        return dummy;
    }
}

public sealed class LabDummy : CustomMonsterModel
{
    internal int[] Hits { get; set; } = [];
    internal bool NonAttack { get; set; }

    public override int MinInitialHp => 250;
    public override int MaxInitialHp => 250;
    protected override string VisualsPath => "res://scenes/creature_visuals/nibbit.tscn";
    public override LocString Title => ModelDb.Monster<Nibbit>().Title;
    public override CreatureAnimator GenerateAnimator(MegaSprite controller) => ModelDb.Monster<Nibbit>().GenerateAnimator(controller);

    protected override MonsterMoveStateMachine GenerateMoveStateMachine()
    {
        AbstractIntent intent = Hits.Length switch
        {
            0 => new SleepIntent(),
            _ when NonAttack => new UnknownIntent(),
            1 => new SingleAttackIntent(Hits[0]),
            _ when Hits.All(hit => hit == Hits[0]) => new MultiAttackIntent(Hits[0], Hits.Length),
            _ => new UnknownIntent()
        };
        var move = new MoveState("WITCH_LAB_SCRIPT", PlaySequence, intent);
        move.FollowUpState = move;
        return new MonsterMoveStateMachine([move], move);
    }

    private async Task PlaySequence(IReadOnlyList<Creature> targets)
    {
        var choice = new ThrowingPlayerChoiceContext();
        var props = NonAttack ? ValueProp.Unpowered | ValueProp.Unblockable : ValueProp.Move;
        foreach (var hit in Hits)
        {
            if (!Creature.IsAlive || !targets.Any(target => target.IsAlive)) break;
            await CreatureCmd.Damage(choice, targets, hit, props, Creature);
        }
    }
}
