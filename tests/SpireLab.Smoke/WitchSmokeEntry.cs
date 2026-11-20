using Godot;
using MegaCrit.Sts2.Core.Modding;

namespace SpireLab.Smoke;

[ModInitializer(nameof(Initialize))]
public static class WitchSmokeEntry
{
    public static void Initialize()
    {
        var args = OS.GetCmdlineArgs();
        Action? action = args switch
        {
            _ when args.Contains("--witch-smoke") => () => _ = WitchSmoke.Run(),
            _ when args.Contains("--witch-save-probe") => () => _ = WitchSaveProbe.Run(resume: false),
            _ when args.Contains("--witch-resume-probe") => () => _ = WitchSaveProbe.Run(resume: true),
            _ when args.Contains("--witch-campaign") => () => _ = WitchCampaign.Start(),
            _ when args.Contains("--witch-art") => () => _ = WitchVisualPreview.Run(),
            _ => null
        };

        if (action != null)
        {
            Callable.From(action).CallDeferred();
        }
    }
}
