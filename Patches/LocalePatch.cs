using HarmonyLib;

namespace MistalCrysMap.Patches
{
    /// <summary>
    /// 游戏本地化表没有本模组条目时，使用内置英文后备文本。
    /// </summary>
    [HarmonyPatch(typeof(Locale), nameof(Locale.GetOther))]
    internal static class LocalePatch
    {
        private static void Postfix(string str, ref string __result)
        {
            if (__result != str)
                return;

            if (ModSettings.TryGetLocaleFallback(str, out string fallback))
                __result = fallback;
        }
    }
}
