using HarmonyLib;
using MistalCrysMap.Runtime;

namespace MistalCrysMap.Patches
{
    /// <summary>
    /// 大地图是覆盖层，不暂停世界；但打开时需要阻止玩家控制继续吃键鼠输入。
    /// </summary>
    [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.HandleInput))]
    internal static class PlayerInputPatch
    {
        private static bool Prefix()
        {
            return !MapUiState.BlockPlayerInput;
        }
    }
}
