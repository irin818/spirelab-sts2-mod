namespace SpireLab.SpireLabCode.Witch.Mechanics;

public static class WitchRules
{
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

    public static int Reflect(int blocked, int percent, decimal multiplier = 1) =>
        (int)Math.Floor((decimal)blocked * percent / 100 * multiplier);
}
