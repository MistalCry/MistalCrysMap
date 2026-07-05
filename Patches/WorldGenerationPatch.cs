using HarmonyLib;
using MistalCrysMap.Runtime;

namespace MistalCrysMap.Patches
{
    /// <summary>
    /// 游戏世界块发生变化时只打脏标记。
    /// 真正的地图贴图重建仍由 MapWorldSampler 在地图可见时集中处理，避免在挖掘或爆炸逻辑里做重活。
    /// </summary>
    internal static class WorldGenerationPatch
    {
        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.SetBlock))]
        private static class SetBlockPatch
        {
            private static void Postfix()
            {
                MapWorldSampler.MarkWorldDirty();
            }
        }

        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.SetBlockNoUpdate))]
        private static class SetBlockNoUpdatePatch
        {
            private static void Postfix()
            {
                MapWorldSampler.MarkWorldDirty();
            }
        }

        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.GenerateBlockCircle))]
        private static class GenerateBlockCirclePatch
        {
            private static void Postfix()
            {
                MapWorldSampler.MarkWorldDirty();
            }
        }

        [HarmonyPatch(typeof(WorldGeneration), nameof(WorldGeneration.SimpleBlockCircle))]
        private static class SimpleBlockCirclePatch
        {
            private static void Postfix()
            {
                MapWorldSampler.MarkWorldDirty();
            }
        }
    }
}
