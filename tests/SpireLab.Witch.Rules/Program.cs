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
Expect(WitchRules.Reflect(1, 50) == 0 && WitchRules.Reflect(7, 50) == 3
    && WitchRules.Reflect(7, 50, 1.5m) == 5, "reflection rounds down after source modifiers");
Expect(!WitchRules.CanPayLife(1, 1) && !WitchRules.CanPayLife(4, 0)
    && WitchRules.CanPayLife(2, 1), "payment requires a positive cost and leaves at least one HP");

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
