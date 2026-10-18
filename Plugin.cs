using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;

namespace ModMenu;

[BepInPlugin("megabonk.modmenu", "ModMenu", "1.0.0")]
public class Plugin : BasePlugin
{
    internal static ManualLogSource Logger;
    internal static Window Host;

    public override void Load()
    {
        Logger = Log;
        Host = AddComponent<Window>();
        Harmony.CreateAndPatchAll(typeof(MenuButtons));
        Harmony.CreateAndPatchAll(typeof(InputBlock));
    }

    internal static IEnumerable<PluginRef> LoadedPlugins() =>
        IL2CPPChainloader.Instance.Plugins.Values.Select(p => new PluginRef
        {
            Name = p.Metadata.Name,
            Version = p.Metadata.Version.ToString(),
            Instance = p.Instance,
            Assembly = p.Instance?.GetType().Assembly,
        });
}
