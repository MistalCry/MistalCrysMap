using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace MistalCrysMap
{
    /// <summary>
    /// 接入游戏自己的 Settings -> Game。
    /// 地图位置和缩放属于本地 UI 状态，单独存在 PlayerPrefs，不塞进设置页。
    /// </summary>
    internal static class ModSettings
    {
        public const string EnabledSettingName = "mistalcrysmapenabled";
        public const string ToggleKeySettingName = "mistalcrysmaptogglekey";
        public const string ExplorationMapSettingName = "mistalcrysmapexploration";
        public const string MinimapShapeSettingName = "mistalcrysmapminimapshape";
        public const string MinimapSizeSettingName = "mistalcrysmapminimapsize";

        private const string MigrationKeyPrefix = "MistalCrysMap.SettingsMigration.";
        private const string ToggleKeyMigrationName = MigrationKeyPrefix + "ToggleBackToM";
        private const string ShapeMigrationName = MigrationKeyPrefix + "DefaultSquare";
        private const string SizeMigrationName = MigrationKeyPrefix + "Default125";
        private const KeyCode OldDefaultToggleKey = KeyCode.Escape;
        private const KeyCode DefaultToggleKey = KeyCode.M;
        private const float OldDefaultMinimapSize = 1f;
        private const float DefaultMinimapSize = 1.25f;
        private const float MinMinimapSize = 0.5f;
        private const float MaxMinimapSize = 3f;

        private static readonly string[] MinimapShapeChoices = { "rectangle", "square", "circle" };
        private static readonly BindingFlags SettingsFieldFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo SettingsInitializedField = typeof(Settings).GetField("initialized", SettingsFieldFlags);
        private static readonly FieldInfo SettingsListField = typeof(Settings).GetField("settings", SettingsFieldFlags);

        private static readonly Dictionary<string, string> LocaleFallbacks =
            new Dictionary<string, string>
            {
                { "gameset" + EnabledSettingName, "MistalCrysMap" },
                { "gameset" + EnabledSettingName + "dsc", "Show the single-player map overlay." },
                { "gameset" + ToggleKeySettingName, "Map Toggle Key" },
                { "gameset" + ToggleKeySettingName + "dsc", "Key used to show or hide the minimap." },
                { "gameset" + ExplorationMapSettingName, "Exploration Map" },
                { "gameset" + ExplorationMapSettingName + "dsc", "Hide terrain until the player has explored it. Hazards remain visible." },
                { "gameset" + MinimapShapeSettingName, "Minimap Shape" },
                { "gameset" + MinimapShapeSettingName + "dsc", "Choose the minimap frame shape." },
                { "gameset" + MinimapShapeSettingName + "rectangle", "Rectangle" },
                { "gameset" + MinimapShapeSettingName + "square", "Square" },
                { "gameset" + MinimapShapeSettingName + "circle", "Circle" },
                { "gameset" + MinimapSizeSettingName, "Minimap Size" },
                { "gameset" + MinimapSizeSettingName + "dsc", "Scale the minimap frame size." }
            };

        public static bool Enabled => GetBool(EnabledSettingName, true);
        public static KeyCode ToggleKey => GetKey(ToggleKeySettingName, DefaultToggleKey);
        public static bool ExplorationMap => GetBool(ExplorationMapSettingName, false);
        public static MinimapShapeKind MinimapShape => (MinimapShapeKind)GetDropdown(MinimapShapeSettingName, (int)MinimapShapeKind.Square, MinimapShapeChoices.Length);
        public static float MinimapSize => GetFloat(MinimapSizeSettingName, DefaultMinimapSize, MinMinimapSize, MaxMinimapSize);

        public static void AddMissingSettings(List<Setting> settings)
        {
            if (settings == null)
                return;

            AddBool(settings, EnabledSettingName, true);
            AddKey(settings, ToggleKeySettingName, DefaultToggleKey);
            AddBool(settings, ExplorationMapSettingName, false);
            AddDropdown(settings, MinimapShapeSettingName, (int)MinimapShapeKind.Square, MinimapShapeChoices);
            AddFloat(settings, MinimapSizeSettingName, DefaultMinimapSize, MinMinimapSize, MaxMinimapSize, FormatMultiplier);
            MigrateOldDefaults(settings);
        }

        public static bool TryGetLocaleFallback(string key, out string value)
        {
            return LocaleFallbacks.TryGetValue(key, out value);
        }

        private static bool GetBool(string name, bool fallback)
        {
            if (!TryGetLoadedSetting(name, out SettingBool setting))
                return fallback;

            return setting == null ? fallback : setting.value;
        }

        private static KeyCode GetKey(string name, KeyCode fallback)
        {
            if (!TryGetLoadedSetting(name, out SettingKeybind setting))
                return fallback;

            return setting == null || setting.value == KeyCode.None ? fallback : setting.value;
        }

        private static int GetDropdown(string name, int fallback, int choiceCount)
        {
            if (!TryGetLoadedSetting(name, out SettingDropdown setting))
                return Mathf.Clamp(fallback, 0, Mathf.Max(0, choiceCount - 1));

            int value = setting == null ? fallback : setting.value;
            return Mathf.Clamp(value, 0, Mathf.Max(0, choiceCount - 1));
        }

        private static float GetFloat(string name, float fallback, float min, float max)
        {
            if (!TryGetLoadedSetting(name, out SettingFloat setting))
                return Mathf.Clamp(fallback, min, max);

            float value = setting == null ? fallback : setting.value;
            return Mathf.Clamp(value, min, max);
        }

        private static bool TryGetLoadedSetting<T>(string name, out T setting) where T : Setting
        {
            setting = null;
            if (!AreSettingsInitialized())
                return false;

            if (!(SettingsListField?.GetValue(null) is List<Setting> settings))
                return false;

            setting = settings.Find(item => item != null && item.name == name) as T;
            return setting != null;
        }

        private static bool AreSettingsInitialized()
        {
            return SettingsInitializedField?.GetValue(null) is bool initialized && initialized;
        }

        private static void AddBool(List<Setting> settings, string name, bool value)
        {
            SettingBool existing = settings.Find(setting => setting != null && setting.name == name) as SettingBool;
            if (existing != null)
                return;

            settings.Add(new SettingBool
            {
                name = name,
                category = Setting.SettingCategory.Game,
                value = value
            });
        }

        private static void AddKey(List<Setting> settings, string name, KeyCode value)
        {
            SettingKeybind existing = settings.Find(setting => setting != null && setting.name == name) as SettingKeybind;
            if (existing != null)
                return;

            settings.Add(new SettingKeybind
            {
                name = name,
                category = Setting.SettingCategory.Game,
                value = value
            });
        }

        private static void AddDropdown(List<Setting> settings, string name, int value, string[] choices)
        {
            SettingDropdown existing = settings.Find(setting => setting != null && setting.name == name) as SettingDropdown;
            if (existing != null)
            {
                existing.choices = choices;
                existing.value = Mathf.Clamp(existing.value, 0, choices.Length - 1);
                return;
            }

            settings.Add(new SettingDropdown
            {
                name = name,
                category = Setting.SettingCategory.Game,
                value = Mathf.Clamp(value, 0, choices.Length - 1),
                choices = choices
            });
        }

        private static void AddFloat(List<Setting> settings, string name, float value, float min, float max, System.Func<float, string> formatter)
        {
            SettingFloat existing = settings.Find(setting => setting != null && setting.name == name) as SettingFloat;
            if (existing != null)
            {
                existing.min = min;
                existing.max = max;
                existing.formatValue = formatter;
                existing.value = Mathf.Clamp(existing.value, min, max);
                return;
            }

            settings.Add(new SettingFloat
            {
                name = name,
                category = Setting.SettingCategory.Game,
                value = value,
                min = min,
                max = max,
                formatValue = formatter
            });
        }

        private static void MigrateOldDefaults(List<Setting> settings)
        {
            bool changed = false;
            SettingKeybind key = settings.Find(setting => setting != null && setting.name == ToggleKeySettingName) as SettingKeybind;
            if (PlayerPrefs.GetInt(ToggleKeyMigrationName, 0) == 0)
            {
                if (key != null && key.value == OldDefaultToggleKey)
                    key.value = DefaultToggleKey;

                PlayerPrefs.SetInt(ToggleKeyMigrationName, 1);
                changed = true;
            }

            SettingDropdown shape = settings.Find(setting => setting != null && setting.name == MinimapShapeSettingName) as SettingDropdown;
            if (PlayerPrefs.GetInt(ShapeMigrationName, 0) == 0)
            {
                if (shape != null && shape.value == (int)MinimapShapeKind.Rectangle)
                    shape.value = (int)MinimapShapeKind.Square;

                PlayerPrefs.SetInt(ShapeMigrationName, 1);
                changed = true;
            }

            SettingFloat size = settings.Find(setting => setting != null && setting.name == MinimapSizeSettingName) as SettingFloat;
            if (PlayerPrefs.GetInt(SizeMigrationName, 0) == 0)
            {
                if (size != null && Mathf.Approximately(size.value, OldDefaultMinimapSize))
                    size.value = DefaultMinimapSize;

                PlayerPrefs.SetInt(SizeMigrationName, 1);
                changed = true;
            }

            if (changed)
                PlayerPrefs.Save();
        }

        private static string FormatMultiplier(float value)
        {
            return value.ToString("0.##") + "x";
        }

        public enum MinimapShapeKind
        {
            Rectangle = 0,
            Square = 1,
            Circle = 2
        }
    }
}
