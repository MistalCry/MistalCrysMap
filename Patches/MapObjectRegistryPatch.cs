using HarmonyLib;
using MistalCrysMap.Runtime;

namespace MistalCrysMap.Patches
{
    /// <summary>
    /// 在游戏对象初始化时登记地图会用到的动态对象。
    /// </summary>
    internal static class MapObjectRegistryPatch
    {
        [HarmonyPatch(typeof(BuildingEntity), "Start")]
        private static class BuildingEntityStartPatch
        {
            private static void Postfix(BuildingEntity __instance)
            {
                MapObjectRegistry.Register(__instance);
            }
        }

        [HarmonyPatch(typeof(CrystalEnemy), "Start")]
        private static class CrystalEnemyStartPatch
        {
            private static void Postfix(CrystalEnemy __instance)
            {
                MapObjectRegistry.Register(__instance);
            }
        }

        [HarmonyPatch(typeof(TraderScript), "Awake")]
        private static class TraderScriptAwakePatch
        {
            private static void Postfix(TraderScript __instance)
            {
                MapObjectRegistry.Register(__instance);
            }
        }

        [HarmonyPatch(typeof(TraderScript), "Start")]
        private static class TraderScriptStartPatch
        {
            private static void Postfix(TraderScript __instance)
            {
                MapObjectRegistry.Register(__instance);
            }
        }

        [HarmonyPatch(typeof(ElderThornbackBehaviour), "Start")]
        private static class ElderThornbackBehaviourStartPatch
        {
            private static void Postfix(ElderThornbackBehaviour __instance)
            {
                MapObjectRegistry.Register(__instance);
            }
        }
    }
}
