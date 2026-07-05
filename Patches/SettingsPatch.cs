using HarmonyLib;
using System.Collections.Generic;

namespace MistalCrysMap.Patches
{
    /// <summary>
    /// 在游戏构建默认设置列表时追加地图设置。
    /// </summary>
    [HarmonyPatch(typeof(Settings), nameof(Settings.DefaultSettings))]
    internal static class SettingsPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(ref List<Setting> __result)
        {
            ModSettings.AddMissingSettings(__result);
        }
    }
}
