using Godot;
using MegaCrit.Sts2.Core.Modding;
using SpireLab.SpireLabCode.Witch.Cards;

namespace SpireLab.SpireLabCode;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    public const string ModId = "SpireLab";
    public const string ResPath = $"res://{ModId}";

    public static MegaCrit.Sts2.Core.Logging.Logger Logger { get; } = new(ModId, MegaCrit.Sts2.Core.Logging.LogType.Generic);

    public static void Initialize()
    {
        Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(typeof(MainFile).Assembly);
        WitchCardAppearance.RegisterSuitBadges();
        Logger.Info("The Witch initialized.");
    }
}
