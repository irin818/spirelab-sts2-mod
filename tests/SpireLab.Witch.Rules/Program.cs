using SpireLab.SpireLabCode.Witch.Mechanics;

static void Expect(bool condition, string name)
{
    if (!condition)
    {
        throw new InvalidOperationException(name);
    }
}

Expect(WitchRules.Invert(20, 70) == (20, 30), "low HP gains temporary buffer");
Expect(WitchRules.Invert(50, 70) == (20, 0), "high HP loses real HP");
Expect(WitchRules.Invert(70, 70) == (1, 0), "full HP does not invert to zero");
Expect(WitchRules.Invert(35, 70) == (35, 0), "midpoint is fixed");
Expect(WitchRules.MirrorCapacity(68, 68) == 0 && WitchRules.MirrorCapacity(20, 68) == 48, "effective HP cap");
Expect(!WitchRules.IsBelowHalf(34, 68) && WitchRules.IsBelowHalf(33, 68) && WitchRules.IsBelowHalf(34, 69), "strict half boundary");
foreach (var (block, percent, fixedDamage, expected) in new[]
{
    (6, 100, 0, 6), (10, 100, 0, 10), (4, 100, 0, 4), (2, 100, 0, 2),
    (6, 150, 0, 9), (6, 200, 0, 12), (6, 300, 0, 18),
    (3, 250, 5, 12), (0, 300, 5, 0), (1, 150, 7, 8)
})
{
    var actual = WitchRules.Reflect(block, percent, fixedDamage);
    Expect(actual == expected, $"reflection expected {expected}, got {actual}");
    Console.WriteLine($"Block {block}, {percent}%, fixed {fixedDamage}: expected {expected}, actual {actual}, PASS");
}
Expect(WitchRules.RefundLife(63, 68, 10) == 68 && WitchRules.RefundLife(22, 68, 10) == 32,
    "victory refund caps at max HP and preserves unrelated damage");
Expect(decimal.Truncate(9 * WitchRules.FlatReductionFactor(9, 6)) == 3, "flat reduction survives engine truncation");
Expect(!WitchRules.CanPayLife(1, 1) && !WitchRules.CanPayLife(4, 0)
    && WitchRules.CanPayLife(2, 1), "payment requires a positive cost and leaves at least one HP");

// V2.0 Counterpoint rules
Expect(WitchRules.IsCounterpoint(PlayKind.Attack, PlayKind.Skill), "Attack -> Skill triggers Counterpoint");
Expect(WitchRules.IsCounterpoint(PlayKind.Skill, PlayKind.Attack), "Skill -> Attack triggers Counterpoint");
Expect(!WitchRules.IsCounterpoint(PlayKind.Attack, PlayKind.Attack), "Attack -> Attack does not trigger Counterpoint");
Expect(!WitchRules.IsCounterpoint(PlayKind.Skill, PlayKind.Skill), "Skill -> Skill does not trigger Counterpoint");
Expect(!WitchRules.IsCounterpoint(null, PlayKind.Attack), "first Attack of turn does not trigger Counterpoint");
Expect(!WitchRules.IsCounterpoint(null, PlayKind.Skill), "first Skill of turn does not trigger Counterpoint");
Expect(!WitchRules.IsCounterpoint(PlayKind.Other, PlayKind.Attack), "Power/Other interrupts Counterpoint before Attack");
Expect(!WitchRules.IsCounterpoint(PlayKind.Other, PlayKind.Skill), "Power/Other interrupts Counterpoint before Skill");
Expect(!WitchRules.IsCounterpoint(PlayKind.Attack, PlayKind.Other), "Power/Other does not trigger Counterpoint after Attack");
Expect(!WitchRules.IsCounterpoint(PlayKind.Skill, PlayKind.Other), "Power/Other does not trigger Counterpoint after Skill");

// V2.0 Reflection rate stacking & Reflective Inscription conservative 100% cap
Expect(WitchRules.AddCappedReflect(0, 25) == 25, "inscription +25% from 0");
Expect(WitchRules.AddCappedReflect(75, 50) == 100, "inscription caps at 100%");
Expect(WitchRules.AddCappedReflect(100, 50) == 100, "inscription stays capped at 100%");
Expect(WitchRules.AddCappedReflect(100, 25) == 100, "inscription does not exceed 100%");
Expect(WitchRules.Reflect(8, 50 + 100, 0) == 12, "additive 50% + 100% = 150% reflection");
Expect(WitchRules.Reflect(8, 100 + 50 + 100 + 25, 0) == 22, "MirrorForm 100% + SilverScreen 50% + MirrorMaze 100% + Inscription 25% = 275%");
Expect(WitchRules.Reflect(7, 25, 0) == 1, "25% of 7 floors to 1");
Expect(WitchRules.Reflect(3, 25, 0) == 0, "25% of 3 floors to 0 (no successful reflection)");

// V2.0 Mirror Cage damage floor
Expect(WitchRules.MirrorCageDamage(0) == 10, "Mirror Cage uses floor 10 at 0 Block");
Expect(WitchRules.MirrorCageDamage(9) == 10, "Mirror Cage uses floor 10 at 9 Block");
Expect(WitchRules.MirrorCageDamage(10) == 10, "Mirror Cage uses 10 at 10 Block");
Expect(WitchRules.MirrorCageDamage(18) == 18, "Mirror Cage uses Block when above floor");

var paths = 0;
foreach (var percent in new[] { 40, 50 })
{
    for (var startingHp = 1; startingHp <= 250; startingHp++)
    {
        for (var hp = startingHp; hp > 1;)
        {
            var cost = WitchRules.GambleCost(hp, percent);
            Expect(WitchRules.CanPayLife(hp, cost), "every positive gamble above one HP is payable");
            hp -= cost;
            Expect(hp >= 1, "no gamble can be lethal");
        }
        Expect(!WitchRules.CanPayLife(1, WitchRules.GambleCost(1, percent)), "one HP ends the self-funded loop");
        paths++;
    }
}

Console.WriteLine($"Witch rules passed, including {paths} HP-decreasing gamble paths.");
