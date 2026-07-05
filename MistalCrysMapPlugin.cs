using BepInEx;
using HarmonyLib;
using System.IO;
using System.Reflection;
using MistalCrysMap.Patches;
using MistalCrysMap.Runtime;
using UnityEngine;

namespace MistalCrysMap
{
    /// <summary>
    /// 模组入口。
    /// 负责 Harmony 生命周期、设置注入和运行时宿主对象。
    /// </summary>
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public sealed class MistalCrysMapPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "com.mistalcry.mistalcrysmap";
        public const string PluginName = "MistalCrysMap";
        public const string PluginVersion = "1.0.1";

        private Harmony harmony;
        private GameObject runtimeHost;
        private static bool applicationQuitting;

        internal static MistalCrysMapPlugin Instance { get; private set; }
        internal static string PluginDirectory { get; private set; }

        private void Awake()
        {
            Instance = this;
            PluginDirectory = Path.GetDirectoryName(Info.Location);

            harmony = new Harmony(PluginGuid);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            runtimeHost = new GameObject("MistalCrysMap Runtime");
            runtimeHost.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(runtimeHost);

            MapOverlay.Install(runtimeHost);
            Logger.LogInfo(PluginName + " " + PluginVersion + " loaded.");
        }

        private void OnApplicationQuit()
        {
            applicationQuitting = true;
        }

        private void OnDestroy()
        {
            if (!applicationQuitting && runtimeHost != null)
            {
                if (Instance == this)
                    Instance = null;

                return;
            }

            MapOverlay.Uninstall();

            if (runtimeHost != null)
            {
                Destroy(runtimeHost);
                runtimeHost = null;
            }

            if (harmony != null)
                harmony.UnpatchAll(PluginGuid);

            harmony = null;

            if (Instance == this)
                Instance = null;
        }
    }
}
