using HarmonyLib;

namespace ModMenu;

[HarmonyPatch(typeof(MyInputManager))]
static class InputBlock
{
    static bool Open => Plugin.Host != null && Plugin.Host.IsOpen;

    [HarmonyPrefix, HarmonyPatch(nameof(MyInputManager.GetButton))]
    static bool Button(ref bool __result) => Pass(ref __result);

    [HarmonyPrefix, HarmonyPatch(nameof(MyInputManager.GetButtonDown))]
    static bool ButtonDown(ref bool __result) => Pass(ref __result);

    [HarmonyPrefix, HarmonyPatch(nameof(MyInputManager.GetButtonUp))]
    static bool ButtonUp(ref bool __result) => Pass(ref __result);

    [HarmonyPrefix, HarmonyPatch(nameof(MyInputManager.GetAxis))]
    static bool Axis(ref float __result)
    {
        if (!Open) return true;
        __result = 0;
        return false;
    }

    static bool Pass(ref bool result)
    {
        if (!Open) return true;
        result = false;
        return false;
    }
}
