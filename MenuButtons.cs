using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ModMenu;

static class MenuButtons
{
    const string CloneName = "ModMenuButton";

    [HarmonyPostfix, HarmonyPatch(typeof(MainMenu), nameof(MainMenu.Start))]
    static void MainMenuStarted(MainMenu __instance) => Inject(__instance.transform, "GoToSettings", "main menu");

    [HarmonyPostfix, HarmonyPatch(typeof(PauseUi), nameof(PauseUi.Awake))]
    static void PauseAwake(PauseUi __instance) =>
        Inject(__instance.main != null ? __instance.main.transform : __instance.transform, "GoToOptions", "pause menu");

    static void Inject(Transform root, string method, string where)
    {
        try
        {
            if (root.GetComponentsInChildren<Transform>(true).Any(t => t.name == CloneName)) return;

            var template = Buttons(root).FirstOrDefault(b => Methods(b).Contains(method));
            if (template == null)
            {
                template = Buttons(null).FirstOrDefault(b => Methods(b).Contains(method));
                if (template != null) Plugin.Logger.LogWarning($"[{where}] {method} button found outside {root.name}: {Path(template.transform)}");
            }
            if (template == null)
            {
                var known = Buttons(root).Select(b => $"{Path(b.transform)} -> {string.Join("/", Methods(b))}");
                Plugin.Logger.LogError($"[{where}] no button calls {method}, Mods button not added. Buttons:\n{string.Join("\n", known)}\nHierarchy:\n{Dump(root, 0, 4)}");
                return;
            }

            var clone = Object.Instantiate(template.gameObject, template.transform.parent);
            clone.name = CloneName;
            clone.transform.SetSiblingIndex(template.transform.GetSiblingIndex() + 1);

            foreach (var component in clone.GetComponentsInChildren<MonoBehaviour>(true))
            {
                var type = component.GetIl2CppType().FullName;
                if (!type.Contains("Locali")) continue;
                Plugin.Logger.LogDebug($"[{where}] removed {type} from the Mods button");
                Object.DestroyImmediate(component);
            }

            var original = template.GetComponentsInChildren<TMP_Text>(true).Select(t => t.text).FirstOrDefault(t => !string.IsNullOrWhiteSpace(t)) ?? "";
            var caption = original.Any(char.IsLetter) && original == original.ToUpperInvariant() ? "MODS" : "Mods";
            foreach (var text in clone.GetComponentsInChildren<TMP_Text>(true)) text.text = caption;

            var button = clone.GetComponent<Button>();
            var sound = clone.GetComponent<MyButton>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener((Action)(() =>
            {
                if (sound != null) sound.PlaySfx();
                Plugin.Host.Open();
            }));

            if (template.transform.parent.GetComponent<LayoutGroup>() == null)
                Plugin.Logger.LogWarning($"[{where}] {template.transform.parent.name} has no layout group, the Mods button may overlap {template.name}");
            Plugin.Logger.LogInfo($"[{where}] Mods button added next to {Path(template.transform)} (text \"{original}\")");
        }
        catch (Exception e)
        {
            Plugin.Logger.LogError($"[{where}] failed to add the Mods button: {e}");
        }
    }

    static IEnumerable<Button> Buttons(Transform root) =>
        root != null
            ? root.GetComponentsInChildren<Button>(true)
            : Object.FindObjectsOfType(Il2CppType.Of<Button>(), true).Select(o => o.Cast<Button>());

    static IEnumerable<string> Methods(Button button)
    {
        var calls = button.onClick?.m_PersistentCalls?.m_Calls;
        if (calls == null) yield break;
        for (var i = 0; i < calls.Count; i++)
            if (calls[i] != null) yield return calls[i].methodName;
    }

    static string Path(Transform t) => t.parent == null ? t.name : $"{Path(t.parent)}/{t.name}";

    static string Dump(Transform t, int depth, int maxDepth)
    {
        var components = t.GetComponents<Component>().Select(c => c.GetIl2CppType().Name);
        var sb = new StringBuilder($"{new string(' ', depth * 2)}{t.name} [{string.Join(", ", components)}]{(t.gameObject.activeSelf ? "" : " (inactive)")}\n");
        if (depth < maxDepth)
            for (var i = 0; i < t.childCount; i++) sb.Append(Dump(t.GetChild(i), depth + 1, maxDepth));
        return sb.ToString();
    }
}
