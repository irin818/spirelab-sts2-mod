using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.AutoSlay;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Random;
using SpireLab.SpireLabCode.Witch;

namespace SpireLab.Smoke;

internal static class WitchCampaign
{
    public static async Task Start()
    {
        var tree = (SceneTree)Engine.GetMainLoop();
        try
        {
            await NGame.Instance!.GameStartupComplete;
            var paths = AssetSets.MainMenuSet.Where(path => path.EndsWith(".tscn", StringComparison.Ordinal)
                && ResourceLoader.Exists(path)).ToArray();
            await WitchSmoke.WaitFor(tree, () => paths.All(PreloadManager.Cache.GetLoadedCacheAssets().Contains),
                "campaign starts after menu assets load");
            var characters = ModelDb.AllCharacters.ToList();
            var witchIndex = characters.FindIndex(character => character is TheWitch);
            if (witchIndex < 0)
            {
                throw new InvalidOperationException("AutoSlay cannot find the Witch.");
            }

            // Native AutoSlay reveals the original characters and its first RNG call selects a button.
            // Use a seed that selects the Witch both with and without the bonus Random Character button.
            var seed = Enumerable.Range(10000, 10000).Select(value => $"WITCH{value}").First(candidate =>
            {
                var hash = StringHelper.GetDeterministicHashCode(candidate);
                return new Rng(hash).NextInt(characters.Count) == witchIndex
                    && new Rng(hash).NextInt(characters.Count + 1) == witchIndex;
            });

            GD.Print($"[WitchCampaign] Starting assisted game on seed {seed}");
            new AutoSlayer().Start(seed);
        }
        catch (Exception error)
        {
            GD.PushError($"[WitchCampaign] FAIL {error}");
            tree.Quit(1);
        }
    }
}
