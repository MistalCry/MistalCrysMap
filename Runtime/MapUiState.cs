using UnityEngine;

namespace MistalCrysMap.Runtime
{
    /// <summary>
    /// 只保存本机 UI 偏好。
    /// 这些值不属于游戏规则，后续加多人时也不应该同步给别人。
    /// </summary>
    internal static class MapUiState
    {
        private const string KeyPrefix = "MistalCrysMap.";
        private const float RectangleMinimapWidth = 240f;
        private const float RectangleMinimapHeight = 170f;
        private const float SquareMinimapSide = 200f;
        private const float DefaultMargin = 18f;
        private const float MinBigMapSpriteScale = 0.65f;
        private const float MaxBigMapSpriteScale = 3f;
        private const string SpriteDefaultOffMigrationKey = KeyPrefix + "SpriteDefaultOffMigration";

        private static bool loaded;

        public static bool MinimapVisible = true;
        public static bool BigMapOpen;
        public static Rect MinimapRect;
        public static float MinimapZoom = 1f;
        public static float BigMapZoom = 1f;
        public static Vector2 BigMapPan = Vector2.zero;
        public static bool MinimapInputInProgress;
        public static bool ShowInteractables = true;
        public static bool ShowHazards = true;
        public static bool ShowTraders = true;
        public static bool ShowEnemies = true;
        public static bool ShowItemDrops = true;
        public static bool ShowThornbackElder = true;
        public static bool ShowSpriteIcons;
        public static float BigMapSpriteScale = 1f;
        public static bool HasWaypoint;
        public static Vector2 WaypointWorldPosition = Vector2.zero;

        public static bool BlockPlayerInput => BigMapOpen || MinimapInputInProgress || IsMinimapMouseActionActive();

        public static void Load()
        {
            if (loaded)
                return;

            loaded = true;
            MinimapVisible = PlayerPrefs.GetInt(KeyPrefix + "MinimapVisible", 1) != 0;
            MinimapZoom = Mathf.Clamp(PlayerPrefs.GetFloat(KeyPrefix + "MinimapZoom", 1f), 0.35f, 24f);
            BigMapZoom = Mathf.Clamp(PlayerPrefs.GetFloat(KeyPrefix + "BigMapZoom", 1f), 0.35f, 16f);
            BigMapPan = new Vector2(
                PlayerPrefs.GetFloat(KeyPrefix + "BigMapPanX", 0f),
                PlayerPrefs.GetFloat(KeyPrefix + "BigMapPanY", 0f));
            ShowInteractables = PlayerPrefs.GetInt(KeyPrefix + "ShowInteractables", 1) != 0;
            ShowHazards = PlayerPrefs.GetInt(KeyPrefix + "ShowHazards", 1) != 0;
            ShowTraders = PlayerPrefs.GetInt(KeyPrefix + "ShowTraders", 1) != 0;
            ShowEnemies = PlayerPrefs.GetInt(KeyPrefix + "ShowEnemies", 1) != 0;
            ShowItemDrops = PlayerPrefs.GetInt(KeyPrefix + "ShowItemDrops", 1) != 0;
            ShowThornbackElder = PlayerPrefs.GetInt(KeyPrefix + "ShowThornbackElder", 1) != 0;
            ShowSpriteIcons = PlayerPrefs.GetInt(KeyPrefix + "ShowSpriteIcons", 0) != 0;
            BigMapSpriteScale = Mathf.Clamp(PlayerPrefs.GetFloat(KeyPrefix + "BigMapSpriteScale", 1f), MinBigMapSpriteScale, MaxBigMapSpriteScale);
            HasWaypoint = PlayerPrefs.GetInt(KeyPrefix + "HasWaypoint", 0) != 0;
            WaypointWorldPosition = new Vector2(
                PlayerPrefs.GetFloat(KeyPrefix + "WaypointWorldX", 0f),
                PlayerPrefs.GetFloat(KeyPrefix + "WaypointWorldY", 0f));
            if (PlayerPrefs.GetInt(SpriteDefaultOffMigrationKey, 0) == 0)
            {
                ShowSpriteIcons = false;
                PlayerPrefs.SetInt(KeyPrefix + "ShowSpriteIcons", 0);
                PlayerPrefs.SetInt(SpriteDefaultOffMigrationKey, 1);
                PlayerPrefs.Save();
            }

            Vector2 defaultSize = CurrentMinimapSize();
            float defaultX = Mathf.Max(DefaultMargin, Screen.width - defaultSize.x - DefaultMargin);
            float defaultY = DefaultMargin;
            MinimapRect = new Rect(
                PlayerPrefs.GetFloat(KeyPrefix + "MinimapX", defaultX),
                PlayerPrefs.GetFloat(KeyPrefix + "MinimapY", defaultY),
                defaultSize.x,
                defaultSize.y);

            ClampMinimapToScreen();
        }

        public static void Save()
        {
            PlayerPrefs.SetInt(KeyPrefix + "MinimapVisible", MinimapVisible ? 1 : 0);
            PlayerPrefs.SetFloat(KeyPrefix + "MinimapX", MinimapRect.x);
            PlayerPrefs.SetFloat(KeyPrefix + "MinimapY", MinimapRect.y);
            PlayerPrefs.SetFloat(KeyPrefix + "MinimapZoom", MinimapZoom);
            PlayerPrefs.SetFloat(KeyPrefix + "BigMapZoom", BigMapZoom);
            PlayerPrefs.SetFloat(KeyPrefix + "BigMapPanX", BigMapPan.x);
            PlayerPrefs.SetFloat(KeyPrefix + "BigMapPanY", BigMapPan.y);
            PlayerPrefs.SetInt(KeyPrefix + "ShowInteractables", ShowInteractables ? 1 : 0);
            PlayerPrefs.SetInt(KeyPrefix + "ShowHazards", ShowHazards ? 1 : 0);
            PlayerPrefs.SetInt(KeyPrefix + "ShowTraders", ShowTraders ? 1 : 0);
            PlayerPrefs.SetInt(KeyPrefix + "ShowEnemies", ShowEnemies ? 1 : 0);
            PlayerPrefs.SetInt(KeyPrefix + "ShowItemDrops", ShowItemDrops ? 1 : 0);
            PlayerPrefs.SetInt(KeyPrefix + "ShowThornbackElder", ShowThornbackElder ? 1 : 0);
            PlayerPrefs.SetInt(KeyPrefix + "ShowSpriteIcons", ShowSpriteIcons ? 1 : 0);
            PlayerPrefs.SetFloat(KeyPrefix + "BigMapSpriteScale", BigMapSpriteScale);
            PlayerPrefs.SetInt(KeyPrefix + "HasWaypoint", HasWaypoint ? 1 : 0);
            PlayerPrefs.SetFloat(KeyPrefix + "WaypointWorldX", WaypointWorldPosition.x);
            PlayerPrefs.SetFloat(KeyPrefix + "WaypointWorldY", WaypointWorldPosition.y);
            PlayerPrefs.Save();
        }

        public static void SetWaypoint(Vector2 worldPosition)
        {
            WaypointWorldPosition = worldPosition;
            HasWaypoint = true;
        }

        public static void ClearWaypoint()
        {
            HasWaypoint = false;
            WaypointWorldPosition = Vector2.zero;
        }

        public static void AdjustBigMapSpriteScale(float delta)
        {
            BigMapSpriteScale = Mathf.Clamp(BigMapSpriteScale + delta, MinBigMapSpriteScale, MaxBigMapSpriteScale);
        }

        public static bool IsMarkerKindVisible(MapMarkerKind kind)
        {
            switch (kind)
            {
                case MapMarkerKind.Interactable:
                    return ShowInteractables;
                case MapMarkerKind.Hazard:
                    return ShowHazards;
                case MapMarkerKind.Trader:
                    return ShowTraders;
                case MapMarkerKind.Enemy:
                    return ShowEnemies;
                case MapMarkerKind.ItemDrop:
                    return ShowItemDrops;
                case MapMarkerKind.ThornbackElder:
                    return ShowThornbackElder;
                default:
                    return true;
            }
        }

        public static void ClampMinimapToScreen()
        {
            float maxX = Mathf.Max(DefaultMargin, Screen.width - MinimapRect.width - DefaultMargin);
            float maxY = Mathf.Max(DefaultMargin, Screen.height - MinimapRect.height - DefaultMargin);
            MinimapRect.x = Mathf.Clamp(MinimapRect.x, DefaultMargin, maxX);
            MinimapRect.y = Mathf.Clamp(MinimapRect.y, DefaultMargin, maxY);
        }

        public static void RefreshMinimapLayout()
        {
            Vector2 size = CurrentMinimapSize();
            if (Mathf.Approximately(MinimapRect.width, size.x) && Mathf.Approximately(MinimapRect.height, size.y))
                return;

            Vector2 center = MinimapRect.size == Vector2.zero
                ? new Vector2(Screen.width - DefaultMargin - size.x * 0.5f, DefaultMargin + size.y * 0.5f)
                : MinimapRect.center;

            MinimapRect.width = size.x;
            MinimapRect.height = size.y;
            MinimapRect.center = center;
            ClampMinimapToScreen();
        }

        public static void ResetBigMapView()
        {
            BigMapZoom = 1f;
            BigMapPan = Vector2.zero;
        }

        public static void ResetPointerCapture()
        {
            MinimapInputInProgress = false;
        }

        private static Vector2 CurrentMinimapSize()
        {
            float scale = ModSettings.MinimapSize;
            switch (ModSettings.MinimapShape)
            {
                case ModSettings.MinimapShapeKind.Square:
                case ModSettings.MinimapShapeKind.Circle:
                    return new Vector2(SquareMinimapSide * scale, SquareMinimapSide * scale);
                default:
                    return new Vector2(RectangleMinimapWidth * scale, RectangleMinimapHeight * scale);
            }
        }

        private static bool IsMinimapMouseActionActive()
        {
            if (!MinimapVisible || BigMapOpen)
                return false;

            bool mouseAction = Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2) || Mathf.Abs(Input.mouseScrollDelta.y) > 0.001f;
            return mouseAction && MinimapRect.Contains(CurrentGuiMousePosition());
        }

        private static Vector2 CurrentGuiMousePosition()
        {
            Vector3 mouse = Input.mousePosition;
            return new Vector2(mouse.x, Screen.height - mouse.y);
        }
    }
}
