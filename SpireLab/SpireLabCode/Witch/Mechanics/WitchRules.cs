namespace SpireLab.SpireLabCode.Witch.Mechanics;

public enum PlayKind
{
    Other = 0,
    Attack = 1,
    Skill = 2
}

public static class WitchRules
{
    public const int InscriptionReflectCap = 100;
    public const int MirrorCageFloor = 10;

    public static bool IsBelowHalf(int realHp, int maxHp) => 2L * realHp < maxHp;

    public static bool CanPayLife(int realHp, int cost) => cost > 0 && cost < realHp;

    public static int GambleCost(int realHp, int percent) =>
        (int)Math.Max(1L, ((long)realHp * percent + 99) / 100);

    public static (int Real, int Mirror) Invert(int realHp, int maxHp)
    {
        var target = Math.Max(1, maxHp - realHp);
        return (Math.Min(realHp, target), Math.Max(0, target - realHp));
    }

    public static int MirrorCapacity(int realHp, int maxHp) => Math.Max(0, maxHp - realHp);

    // The hook expects a multiplier. Round upward so 9 * (3/9) does not truncate to a 2-damage hit.
    public static decimal FlatReductionFactor(decimal amount, int reduction) =>
        amount > reduction ? decimal.Round((amount - reduction) / amount, 20, MidpointRounding.ToPositiveInfinity) : 0;

    public static int Reflect(int blocked, int totalPercent, int fixedDamage = 0) =>
        blocked <= 0 ? 0 : (int)Math.Min(int.MaxValue, (long)blocked * totalPercent / 100 + fixedDamage);

    public static int AddCappedReflect(int current, int gain, int cap = InscriptionReflectCap) =>
        Math.Min(cap, Math.Max(0, current) + Math.Max(0, gain));

    public static bool IsCounterpoint(PlayKind? previous, PlayKind current) =>
        (previous == PlayKind.Attack && current == PlayKind.Skill)
        || (previous == PlayKind.Skill && current == PlayKind.Attack);

    public static int MirrorCageDamage(int block, int floor = MirrorCageFloor) => Math.Max(block, floor);

    public static int RefundLife(int realHp, int maxHp, int paid) =>
        (int)Math.Min((long)realHp + paid, maxHp);
}
