using System.Collections.Generic;
using UnityEngine;

namespace MistalCrysMap.Runtime
{
    /// <summary>
    /// 地图的 IMGUI 覆盖层。
    /// 小地图默认右上角；右键打开大地图；大地图用地图快捷键退出、右键拖动、滚轮缩放。
    /// </summary>
    internal sealed class MapOverlay : MonoBehaviour
    {
        private const float MinimapInnerPadding = 7f;
        private const float BigMapMarginRatio = 0.08f;
        private const float BigMapMinMargin = 24f;
        private const float BigMapHeaderHeight = 58f;
        private const float BigMapToggleHeight = 22f;
        private const float BigMapSpriteScaleStep = 0.1f;
        private const float BigMapSpriteSizeControlWidth = 108f;
        private const float BigMapCenterPlayerWidth = 70f;
        private const float BigMapClearWaypointWidth = 86f;
        private const float PlayerCenterPulseDuration = 3f;
        private const KeyCode BigMapCenterOnPlayerKey = KeyCode.Home;
        private const int CircularLineSegments = 32;
        private const float ZoomStep = 1.12f;
        private static readonly Color PanelColor = new Color(0.20f, 0.22f, 0.23f, 0.82f);
        private static readonly Color ViewportColor = new Color(0.18f, 0.23f, 0.25f, 0.88f);

        private static MapOverlay instance;

        private readonly MapWorldSampler worldSampler = new MapWorldSampler();
        private readonly MapHazardScanner hazardScanner = new MapHazardScanner();

        private Texture2D panelTexture;
        private Texture2D viewportTexture;
        private Texture2D circlePanelTexture;
        private Texture2D markerCircleTexture;
        private GUIStyle panelStyle;
        private GUIStyle titleStyle;
        private GUIStyle smallTextStyle;
        private GUIStyle markerStyle;
        private GUIStyle playerStyle;
        private GUIStyle toggleStyle;

        private bool draggingMinimap;
        private bool draggingBigMap;
        private Vector2 minimapDragOffset;
        private Vector2 lastBigDragMouse;
        private bool wasBigMapOpen;
        private float playerHaloBoostUntil = -999f;

        public static void Install(GameObject owner)
        {
            if (instance != null || owner == null)
                return;

            instance = owner.AddComponent<MapOverlay>();
        }

        public static void Uninstall()
        {
            if (instance == null)
                return;

            instance.Dispose();
            Destroy(instance);
            instance = null;
        }

        private void Awake()
        {
            MapUiState.Load();
        }

        private void Update()
        {
            if (!ModSettings.Enabled)
            {
                MapUiState.BigMapOpen = false;
                MapUiState.ResetPointerCapture();
                wasBigMapOpen = false;
                return;
            }

            MapUiState.Load();
            MapUiState.RefreshMinimapLayout();
            MapUiState.ClampMinimapToScreen();

            Body body = GetLocalBody();
            if (body == null)
            {
                MapUiState.BigMapOpen = false;
                MapUiState.ResetPointerCapture();
                wasBigMapOpen = false;
                return;
            }

            if (MapUiState.BlockPlayerInput)
                body.moveDir = Vector2.zero;

            if (MapUiState.BigMapOpen && !wasBigMapOpen)
                hazardScanner.RequestImmediateFullRefresh();

            wasBigMapOpen = MapUiState.BigMapOpen;

            bool mapVisible = MapUiState.MinimapVisible || MapUiState.BigMapOpen;
            worldSampler.Update(ModSettings.ExplorationMap, body, mapVisible);
            if (MapUiState.MinimapVisible || MapUiState.BigMapOpen)
                hazardScanner.RefreshIfNeeded(body, MapUiState.BigMapOpen);

            HandleKeyboard();
        }

        private void OnGUI()
        {
            if (!ModSettings.Enabled)
                return;

            if (GetLocalBody() == null)
                return;

            MapUiState.Load();
            MapUiState.RefreshMinimapLayout();
            EnsureStyles();

            if (MapUiState.BigMapOpen)
            {
                DrawBigMap();
                return;
            }

            if (MapUiState.MinimapVisible)
                DrawMinimap();
        }

        private void OnDestroy()
        {
            Dispose();
        }

        private void HandleKeyboard()
        {
            if (MapUiState.BigMapOpen)
            {
                if (Input.GetKeyDown(ModSettings.ToggleKey))
                {
                    MapUiState.BigMapOpen = false;
                    draggingBigMap = false;
                    MapUiState.Save();
                    return;
                }

                if (Input.GetKeyDown(BigMapCenterOnPlayerKey))
                {
                    TryCenterBigMapOnPlayerWithFeedback(GetBigMapViewport(GetBigMapPanel()));
                }

                return;
            }

            if (MapUiState.BigMapOpen || IsTextOrMenuInputActive())
                return;

            if (Input.GetKeyDown(ModSettings.ToggleKey))
            {
                MapUiState.MinimapVisible = !MapUiState.MinimapVisible;
                MapUiState.Save();
            }
        }

        private static bool IsTextOrMenuInputActive()
        {
            if (PauseHandler.paused)
                return true;

            return ConsoleScript.instance != null && ConsoleScript.instance.active;
        }

        private void DrawMinimap()
        {
            Rect rect = MapUiState.MinimapRect;
            HandleMinimapInput(rect);

            if (!ShouldDrawCurrentGuiEvent())
                return;

            bool circular = ModSettings.MinimapShape == ModSettings.MinimapShapeKind.Circle;
            if (circular)
                GUI.DrawTexture(rect, circlePanelTexture, ScaleMode.StretchToFill, true);
            else
                GUI.Box(rect, GUIContent.none, panelStyle);

            Rect viewport = GetMinimapViewport(rect);
            DrawMapViewport(viewport, MapUiState.MinimapZoom, Vector2.zero, MapUiState.MinimapZoom > 1.01f, true, circular);
            DrawShadowedLabel(new Rect(rect.x + 8f, rect.y + 5f, rect.width - 16f, 18f), "Map", smallTextStyle, new Color(0.86f, 0.94f, 1f, 0.88f));
        }

        private void DrawBigMap()
        {
            Rect panel = GetBigMapPanel();
            Rect viewport = GetBigMapViewport(panel);
            HandleBigMapInput(viewport);
            HandleBigMapFilterInput(panel);

            if (!ShouldDrawCurrentGuiEvent())
                return;

            Rect screen = new Rect(0f, 0f, Screen.width, Screen.height);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.34f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = oldColor;

            GUI.Box(panel, GUIContent.none, panelStyle);

            Rect header = new Rect(panel.x + 14f, panel.y + 4f, panel.width - 28f, BigMapHeaderHeight);
            string closeKey = ModSettings.ToggleKey.ToString();
            DrawShadowedLabel(new Rect(header.x, header.y, header.width, 24f), "MistalCrysMap   " + closeKey + " close   Home center   middle mark   right-drag pan   wheel zoom", titleStyle, new Color(0.88f, 0.95f, 1f, 1f));
            DrawBigMapFilters(panel);

            DrawMapViewport(viewport, MapUiState.BigMapZoom, MapUiState.BigMapPan, false, false, false);
        }

        private static Rect GetBigMapPanel()
        {
            float margin = Mathf.Max(BigMapMinMargin, Mathf.Min(Screen.width, Screen.height) * BigMapMarginRatio);
            return new Rect(margin, margin, Screen.width - margin * 2f, Screen.height - margin * 2f);
        }

        private static Rect GetBigMapViewport(Rect panel)
        {
            return new Rect(panel.x + 12f, panel.y + BigMapHeaderHeight + 10f, panel.width - 24f, panel.height - BigMapHeaderHeight - 22f);
        }

        private static Rect GetMinimapViewport(Rect rect)
        {
            return new Rect(
                rect.x + MinimapInnerPadding,
                rect.y + MinimapInnerPadding,
                rect.width - MinimapInnerPadding * 2f,
                rect.height - MinimapInnerPadding * 2f);
        }

        private void HandleMinimapInput(Rect rect)
        {
            Event evt = Event.current;
            Vector2 mouse = evt.mousePosition;
            Rect viewport = GetMinimapViewport(rect);

            if (evt.type == EventType.MouseDown && evt.button == 2 && viewport.Contains(mouse))
            {
                if (TrySetWaypointAt(viewport, mouse, MapUiState.MinimapZoom, Vector2.zero, MapUiState.MinimapZoom > 1.01f))
                {
                    evt.Use();
                    return;
                }
            }

            if (evt.type == EventType.MouseDown && evt.button == 0 && rect.Contains(mouse))
            {
                draggingMinimap = true;
                MapUiState.MinimapInputInProgress = true;
                minimapDragOffset = mouse - rect.position;
                evt.Use();
                return;
            }

            if (evt.rawType == EventType.MouseUp && evt.button == 0 && draggingMinimap)
            {
                draggingMinimap = false;
                MapUiState.MinimapInputInProgress = false;
                MapUiState.Save();
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 0 && draggingMinimap)
            {
                MapUiState.MinimapRect.position = mouse - minimapDragOffset;
                MapUiState.ClampMinimapToScreen();
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 1 && rect.Contains(mouse))
            {
                MapUiState.BigMapOpen = true;
                MapUiState.MinimapInputInProgress = false;
                draggingBigMap = false;
                hazardScanner.RequestImmediateFullRefresh();
                MapUiState.Save();
                evt.Use();
                return;
            }

            if (evt.type == EventType.ScrollWheel && rect.Contains(mouse))
            {
                MapUiState.MinimapZoom = ApplyZoom(MapUiState.MinimapZoom, evt.delta.y, 0.35f, 24f);
                MapUiState.Save();
                evt.Use();
            }
        }

        private void HandleBigMapInput(Rect viewport)
        {
            Event evt = Event.current;
            Vector2 mouse = evt.mousePosition;
            MapUiState.BigMapPan = ClampBigMapPan(viewport, MapUiState.BigMapPan, MapUiState.BigMapZoom);

            if (evt.type == EventType.MouseDown && evt.button == 2 && viewport.Contains(mouse))
            {
                if (TrySetWaypointAt(viewport, mouse, MapUiState.BigMapZoom, MapUiState.BigMapPan, false))
                {
                    evt.Use();
                    return;
                }
            }

            if (evt.type == EventType.ScrollWheel && viewport.Contains(mouse))
            {
                float oldZoom = MapUiState.BigMapZoom;
                float newZoom = ApplyZoom(oldZoom, evt.delta.y, 0.35f, 16f);
                if (!Mathf.Approximately(oldZoom, newZoom))
                {
                    MapUiState.BigMapPan = ZoomPanAroundMouse(viewport, mouse, MapUiState.BigMapPan, oldZoom, newZoom);
                    MapUiState.BigMapZoom = newZoom;
                    MapUiState.BigMapPan = ClampBigMapPan(viewport, MapUiState.BigMapPan, MapUiState.BigMapZoom);
                    MapUiState.Save();
                }

                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDown && evt.button == 1 && viewport.Contains(mouse))
            {
                draggingBigMap = true;
                lastBigDragMouse = mouse;
                evt.Use();
                return;
            }

            if (evt.type == EventType.MouseDrag && evt.button == 1 && draggingBigMap)
            {
                Vector2 delta = mouse - lastBigDragMouse;
                lastBigDragMouse = mouse;
                MapUiState.BigMapPan += delta;
                MapUiState.BigMapPan = ClampBigMapPan(viewport, MapUiState.BigMapPan, MapUiState.BigMapZoom);
                evt.Use();
                return;
            }

            if (evt.rawType == EventType.MouseUp && evt.button == 1 && draggingBigMap)
            {
                draggingBigMap = false;
                MapUiState.Save();
                evt.Use();
            }
        }

        private void DrawMapViewport(Rect viewport, float zoom, Vector2 pan, bool centerOnPlayerWhenZoomed, bool compact, bool circular)
        {
            if (!circular)
                GUI.Box(viewport, GUIContent.none, panelStyle);

            Rect localViewport = new Rect(0f, 0f, viewport.width, viewport.height);
            GUI.BeginGroup(viewport);
            if (circular)
                DrawCircularTexture(localViewport, localViewport, viewportTexture);
            else
                GUI.DrawTexture(localViewport, viewportTexture);

            if (!worldSampler.HasWorld)
            {
                DrawShadowedLabel(localViewport, "WORLD LOADING", compact ? smallTextStyle : titleStyle, new Color(0.7f, 0.78f, 0.86f, 0.88f));
                GUI.EndGroup();
                return;
            }

            Rect content = GetContentRect(localViewport, zoom, pan, centerOnPlayerWhenZoomed);
            if (circular)
                DrawCircularTexture(localViewport, content, worldSampler.Texture);
            else
                GUI.DrawTexture(content, worldSampler.Texture, ScaleMode.StretchToFill, true);

            DrawWaypoint(localViewport, content, compact, circular);
            DrawMarkers(localViewport, content, compact, circular);
            DrawPlayerMarker(localViewport, content, compact, circular);
            GUI.EndGroup();
        }

        private Rect GetContentRect(Rect viewport, float zoom, Vector2 pan, bool centerOnPlayer)
        {
            Texture2D texture = worldSampler.Texture;
            if (texture == null)
                return viewport;

            float baseScale = Mathf.Min(viewport.width / texture.width, viewport.height / texture.height);
            float scale = baseScale * Mathf.Max(0.01f, zoom);
            Vector2 size = new Vector2(texture.width * scale, texture.height * scale);

            if (centerOnPlayer)
            {
                Body body = GetLocalBody();
                if (body != null)
                {
                    Vector2 point = WorldPointToContent(body.transform.position, new Rect(0f, 0f, size.x, size.y));
                    return new Rect(viewport.center.x - point.x, viewport.center.y - point.y, size.x, size.y);
                }
            }

            return new Rect(
                viewport.center.x - size.x * 0.5f + pan.x,
                viewport.center.y - size.y * 0.5f + pan.y,
                size.x,
                size.y);
        }

        private Vector2 ZoomPanAroundMouse(Rect viewport, Vector2 mouse, Vector2 oldPan, float oldZoom, float newZoom)
        {
            Texture2D texture = worldSampler.Texture;
            if (texture == null)
                return oldPan;

            Vector2 oldSize = GetContentSize(viewport, oldZoom);
            Vector2 oldTopLeft = viewport.center + oldPan - oldSize * 0.5f;
            Vector2 normalized = new Vector2(
                oldSize.x <= 0.01f ? 0.5f : Mathf.Clamp01((mouse.x - oldTopLeft.x) / oldSize.x),
                oldSize.y <= 0.01f ? 0.5f : Mathf.Clamp01((mouse.y - oldTopLeft.y) / oldSize.y));

            Vector2 newSize = GetContentSize(viewport, newZoom);
            Vector2 newTopLeft = mouse - Vector2.Scale(normalized, newSize);
            Vector2 newCenter = newTopLeft + newSize * 0.5f;
            return newCenter - viewport.center;
        }

        private Vector2 ClampBigMapPan(Rect viewport, Vector2 pan, float zoom)
        {
            Vector2 size = GetContentSize(viewport, zoom);
            if (size == Vector2.zero)
                return Vector2.zero;

            float limitX = Mathf.Max(0f, (size.x + viewport.width) * 0.5f - 48f);
            float limitY = Mathf.Max(0f, (size.y + viewport.height) * 0.5f - 48f);
            return new Vector2(
                Mathf.Clamp(pan.x, -limitX, limitX),
                Mathf.Clamp(pan.y, -limitY, limitY));
        }

        private Vector2 GetContentSize(Rect viewport, float zoom)
        {
            Texture2D texture = worldSampler.Texture;
            if (texture == null)
                return Vector2.zero;

            float baseScale = Mathf.Min(viewport.width / texture.width, viewport.height / texture.height);
            float scale = baseScale * Mathf.Max(0.01f, zoom);
            return new Vector2(texture.width * scale, texture.height * scale);
        }

        private bool TryCenterBigMapOnPlayer(Rect viewport)
        {
            Body body = GetLocalBody();
            if (body == null || !worldSampler.HasWorld)
                return false;

            MapUiState.BigMapPan = ClampBigMapPan(viewport, BigMapPanForWorldPosition(body.transform.position, viewport, MapUiState.BigMapZoom), MapUiState.BigMapZoom);
            draggingBigMap = false;
            return true;
        }

        private bool TryCenterBigMapOnPlayerWithFeedback(Rect viewport)
        {
            if (!TryCenterBigMapOnPlayer(viewport))
                return false;

            playerHaloBoostUntil = Time.unscaledTime + PlayerCenterPulseDuration;
            MapUiState.Save();
            return true;
        }

        private Vector2 BigMapPanForWorldPosition(Vector2 worldPosition, Rect viewport, float zoom)
        {
            Vector2 size = GetContentSize(viewport, zoom);
            if (size == Vector2.zero)
                return MapUiState.BigMapPan;

            Vector2Int block = worldSampler.WorldToBlock(worldPosition);
            float normX = worldSampler.WorldWidth <= 0 ? 0.5f : Mathf.Clamp01((block.x + 0.5f) / worldSampler.WorldWidth);
            float normY = worldSampler.WorldHeight <= 0 ? 0.5f : Mathf.Clamp01((block.y + 0.5f) / worldSampler.WorldHeight);
            return new Vector2((0.5f - normX) * size.x, (normY - 0.5f) * size.y);
        }

        private void DrawMarkers(Rect viewport, Rect content, bool compact, bool circular)
        {
            IReadOnlyList<MapMarker> markers = hazardScanner.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                MapMarker marker = markers[i];
                if (marker.Target == null)
                    continue;

                if (!MapUiState.IsMarkerKindVisible(marker.Kind))
                    continue;

                Vector2 worldPosition = marker.WorldPosition;
                if (!worldSampler.IsWorldPositionRevealed(worldPosition))
                    continue;

                Vector2 point = WorldPointToContent(worldPosition, content);
                DrawMapMarker(viewport, point, marker, MarkerSize(marker.Kind, compact), compact, circular);
            }
        }

        private void DrawPlayerMarker(Rect viewport, Rect content, bool compact, bool circular)
        {
            Body body = GetLocalBody();
            if (body == null)
                return;

            Vector2 point = WorldPointToContent(body.transform.position, content);
            string label = body.isRight ? ">" : "<";
            DrawPlayerHalo(viewport, point, compact, circular);
            DrawLabeledMarker(viewport, point, label, new Color(0.22f, 1f, 0.95f, 1f), compact ? 15f : 20f, playerStyle, circular);
        }

        private Vector2 WorldPointToContent(Vector2 worldPosition, Rect content)
        {
            Vector2Int block = worldSampler.WorldToBlock(worldPosition);
            float normX = worldSampler.WorldWidth <= 0 ? 0.5f : Mathf.Clamp01((block.x + 0.5f) / worldSampler.WorldWidth);
            float normY = worldSampler.WorldHeight <= 0 ? 0.5f : Mathf.Clamp01((block.y + 0.5f) / worldSampler.WorldHeight);
            return new Vector2(
                content.x + normX * content.width,
                content.y + content.height - normY * content.height);
        }

        private Vector2 ContentPointToWorld(Vector2 contentPoint, Rect content)
        {
            float normX = content.width <= 0.01f ? 0.5f : Mathf.Clamp01((contentPoint.x - content.x) / content.width);
            float normY = content.height <= 0.01f ? 0.5f : Mathf.Clamp01((content.yMax - contentPoint.y) / content.height);
            int blockX = Mathf.Clamp(Mathf.FloorToInt(normX * worldSampler.WorldWidth), 0, Mathf.Max(0, worldSampler.WorldWidth - 1));
            int blockY = Mathf.Clamp(Mathf.FloorToInt(normY * worldSampler.WorldHeight), 0, Mathf.Max(0, worldSampler.WorldHeight - 1));
            return worldSampler.BlockToWorld(new Vector2Int(blockX, blockY));
        }

        private bool TrySetWaypointAt(Rect viewport, Vector2 mouse, float zoom, Vector2 pan, bool centerOnPlayer)
        {
            if (!worldSampler.HasWorld)
                return false;

            Rect content = GetContentRect(viewport, zoom, pan, centerOnPlayer);
            MapUiState.SetWaypoint(ContentPointToWorld(mouse, content));
            MapUiState.Save();
            return true;
        }

        private void DrawWaypoint(Rect viewport, Rect content, bool compact, bool circular)
        {
            if (!MapUiState.HasWaypoint)
                return;

            Body body = GetLocalBody();
            if (body == null)
                return;

            Vector2 playerPoint = WorldPointToContent(body.transform.position, content);
            Vector2 waypointPoint = WorldPointToContent(MapUiState.WaypointWorldPosition, content);
            DrawMapLine(viewport, playerPoint, waypointPoint, new Color(1f, 0.84f, 0.18f, compact ? 0.72f : 0.82f), compact ? 2f : 3f, circular);
            DrawWaypointMarker(viewport, waypointPoint, compact, circular);
        }

        private void DrawMapMarker(Rect viewport, Vector2 point, MapMarker marker, float size, bool compact, bool circular)
        {
            if (MapUiState.ShowSpriteIcons && TryDrawSpriteMarker(viewport, point, marker, size, compact, circular))
                return;

            if (marker.Kind == MapMarkerKind.Hazard)
            {
                DrawLabeledMarker(viewport, point, marker.Label, marker.Color, size, markerStyle, circular);
                return;
            }

            if (marker.Kind == MapMarkerKind.Interactable)
            {
                DrawOutlinedMarker(viewport, point, marker.Color, size, circular);
                return;
            }

            if (marker.Kind == MapMarkerKind.Trader)
            {
                DrawLabeledMarker(viewport, point, marker.Label, marker.Color, size, markerStyle, circular);
                return;
            }

            if (marker.Kind == MapMarkerKind.ThornbackElder)
            {
                DrawLabeledMarker(viewport, point, marker.Label, marker.Color, size, markerStyle, circular);
                return;
            }

            DrawSimpleMarker(viewport, point, marker.Color, size, marker.Kind == MapMarkerKind.ItemDrop, circular);
        }

        private bool TryDrawSpriteMarker(Rect viewport, Vector2 point, MapMarker marker, float fallbackSize, bool compact, bool circular)
        {
            if (marker.Kind == MapMarkerKind.Trader)
            {
                DrawTraderIcon(viewport, point, fallbackSize, compact, circular);
                return true;
            }

            Sprite sprite = MapSpriteCache.GetSprite(marker.Target);
            if (sprite == null || sprite.texture == null)
                return false;

            float size = SpriteMarkerSize(marker.Kind, fallbackSize, compact);
            if (!IsMarkerPointVisible(viewport, point, size, circular))
                return true;

            Rect textureRect;
            try
            {
                textureRect = sprite.textureRect;
            }
            catch (System.Exception)
            {
                textureRect = sprite.rect;
            }

            if (textureRect.width <= 0.01f || textureRect.height <= 0.01f)
                return false;

            float aspect = textureRect.width / textureRect.height;
            Rect rect = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            if (aspect > 1f)
                rect.height /= aspect;
            else
                rect.width *= aspect;

            rect.center = point;
            Rect shadow = new Rect(point.x - size * 0.55f, point.y - size * 0.55f, size * 1.1f, size * 1.1f);
            Rect texCoords = new Rect(
                textureRect.x / sprite.texture.width,
                textureRect.y / sprite.texture.height,
                textureRect.width / sprite.texture.width,
                textureRect.height / sprite.texture.height);

            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(shadow, markerCircleTexture);
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(rect, sprite.texture, texCoords, true);
            GUI.color = oldColor;
            return true;
        }

        private void DrawTraderIcon(Rect viewport, Vector2 point, float fallbackSize, bool compact, bool circular)
        {
            float size = SpriteMarkerSize(MapMarkerKind.Trader, fallbackSize, compact);
            if (!IsMarkerPointVisible(viewport, point, size, circular))
                return;

            Rect outer = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            Rect inner = new Rect(point.x - size * 0.41f, point.y - size * 0.41f, size * 0.82f, size * 0.82f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(outer, markerCircleTexture);
            GUI.color = new Color(0.35f, 1f, 0.58f, 1f);
            GUI.DrawTexture(inner, markerCircleTexture);
            GUI.color = Color.black;
            int oldFontSize = markerStyle.fontSize;
            markerStyle.fontSize = Mathf.RoundToInt(size * 0.54f);
            GUI.Label(outer, "$", markerStyle);
            markerStyle.fontSize = oldFontSize;
            GUI.color = oldColor;
        }

        private void DrawPlayerHalo(Rect viewport, Vector2 point, bool compact, bool circular)
        {
            float markerSize = compact ? 15f : 20f;
            if (!IsMarkerPointVisible(viewport, point, markerSize, circular))
                return;

            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.6f);
            float size = Mathf.Lerp(compact ? 24f : 34f, compact ? 42f : 60f, pulse);
            Rect rect = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);

            Color oldColor = GUI.color;
            float boostRemaining = compact ? 0f : Mathf.Clamp01((playerHaloBoostUntil - Time.unscaledTime) / PlayerCenterPulseDuration);
            if (boostRemaining > 0.001f)
            {
                float progress = 1f - boostRemaining;
                float boostedSize = Mathf.Lerp(72f, 150f, Mathf.SmoothStep(0f, 1f, progress));
                Rect boosted = new Rect(point.x - boostedSize * 0.5f, point.y - boostedSize * 0.5f, boostedSize, boostedSize);
                GUI.color = new Color(0.22f, 1f, 0.95f, Mathf.Lerp(0.2f, 0.03f, progress));
                GUI.DrawTexture(boosted, markerCircleTexture);
            }

            GUI.color = new Color(0.22f, 1f, 0.95f, Mathf.Lerp(0.32f, 0.06f, pulse));
            GUI.DrawTexture(rect, markerCircleTexture);
            GUI.color = oldColor;
        }

        private void DrawWaypointMarker(Rect viewport, Vector2 point, bool compact, bool circular)
        {
            float size = compact ? 12f : 18f;
            if (!IsMarkerPointVisible(viewport, point, size, circular))
                return;

            Rect outer = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            Rect inner = new Rect(point.x - size * 0.34f, point.y - size * 0.34f, size * 0.68f, size * 0.68f);
            Rect core = new Rect(point.x - size * 0.13f, point.y - size * 0.13f, size * 0.26f, size * 0.26f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(outer, markerCircleTexture);
            GUI.color = new Color(1f, 0.84f, 0.18f, 1f);
            GUI.DrawTexture(inner, markerCircleTexture);
            GUI.color = new Color(0.08f, 0.08f, 0.06f, 1f);
            GUI.DrawTexture(core, markerCircleTexture);
            GUI.color = oldColor;
        }

        private void DrawLabeledMarker(Rect viewport, Vector2 point, string label, Color color, float size, GUIStyle style, bool circular)
        {
            if (!IsMarkerPointVisible(viewport, point, size, circular))
                return;

            Rect rect = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.black;
            GUI.Label(rect, label, style);
            GUI.color = oldColor;
        }

        private void DrawSimpleMarker(Rect viewport, Vector2 point, Color color, float size, bool round, bool circular)
        {
            if (!IsMarkerPointVisible(viewport, point, size, circular))
                return;

            Rect rect = new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size);
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, round ? markerCircleTexture : Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private void DrawOutlinedMarker(Rect viewport, Vector2 point, Color color, float size, bool circular)
        {
            if (!IsMarkerPointVisible(viewport, point, size, circular))
                return;

            Rect halo = new Rect(point.x - size * 0.68f, point.y - size * 0.68f, size * 1.36f, size * 1.36f);
            Rect border = new Rect(point.x - size * 0.56f, point.y - size * 0.56f, size * 1.12f, size * 1.12f);
            Rect rect = new Rect(point.x - size * 0.38f, point.y - size * 0.38f, size * 0.76f, size * 0.76f);
            Color oldColor = GUI.color;
            GUI.color = new Color(0.88f, 0.96f, 1f, 0.78f);
            GUI.DrawTexture(halo, Texture2D.whiteTexture);
            GUI.color = new Color(0.03f, 0.05f, 0.06f, 0.9f);
            GUI.DrawTexture(border, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = oldColor;
        }

        private static void DrawMapLine(Rect viewport, Vector2 start, Vector2 end, Color color, float width, bool circular)
        {
            if (!circular)
            {
                DrawLineSegment(start, end, color, width);
                return;
            }

            Vector2 previous = start;
            for (int i = 1; i <= CircularLineSegments; i++)
            {
                float t = i / (float)CircularLineSegments;
                Vector2 current = Vector2.Lerp(start, end, t);
                Vector2 midpoint = (previous + current) * 0.5f;
                if (IsMarkerPointVisible(viewport, midpoint, width, true))
                    DrawLineSegment(previous, current, color, width);

                previous = current;
            }
        }

        private static void DrawLineSegment(Vector2 start, Vector2 end, Color color, float width)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.1f)
                return;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), Texture2D.whiteTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private static float MarkerSize(MapMarkerKind kind, bool compact)
        {
            switch (kind)
            {
                case MapMarkerKind.Interactable:
                    return compact ? 7f : 10f;
                case MapMarkerKind.Enemy:
                    return compact ? 8f : 12f;
                case MapMarkerKind.ItemDrop:
                    return compact ? 4f : 6f;
                case MapMarkerKind.Trader:
                    return compact ? 10f : 14f;
                case MapMarkerKind.ThornbackElder:
                    return compact ? 13f : 18f;
                default:
                    return compact ? 11f : 15f;
            }
        }

        private static float SpriteMarkerSize(MapMarkerKind kind, float fallbackSize, bool compact)
        {
            float scale = compact ? 1.35f : MapUiState.BigMapSpriteScale;
            switch (kind)
            {
                case MapMarkerKind.ItemDrop:
                    return Mathf.Max(compact ? 16f : 12f, fallbackSize * 2.4f) * scale;
                case MapMarkerKind.Trader:
                case MapMarkerKind.ThornbackElder:
                    return Mathf.Max(compact ? 26f : 20f, fallbackSize * 1.8f) * scale;
                default:
                    return Mathf.Max(compact ? 19f : 14f, fallbackSize * 1.7f) * scale;
            }
        }

        private static bool IsMarkerPointVisible(Rect viewport, Vector2 point, float size, bool circular)
        {
            if (!viewport.Contains(point))
                return false;

            if (!circular)
                return true;

            float radius = Mathf.Min(viewport.width, viewport.height) * 0.5f - size * 0.5f;
            return (point - viewport.center).sqrMagnitude <= radius * radius;
        }

        private void HandleBigMapFilterInput(Rect panel)
        {
            Event evt = Event.current;
            if (evt.type != EventType.MouseDown || evt.button != 0)
                return;

            if (TryToggleFilterAt(panel, evt.mousePosition))
                evt.Use();
        }

        private void DrawBigMapFilters(Rect panel)
        {
            Rect[] rects = BigMapFilterRects(panel);
            DrawFilterToggle(rects[0], "Interact", MapUiState.ShowInteractables, new Color(0.58f, 0.78f, 1f, 1f));
            DrawFilterToggle(rects[1], "Traps", MapUiState.ShowHazards, new Color(1f, 0.3f, 0.16f, 1f));
            DrawFilterToggle(rects[2], "Traders", MapUiState.ShowTraders, new Color(0.35f, 1f, 0.58f, 1f));
            DrawFilterToggle(rects[3], "Enemies", MapUiState.ShowEnemies, new Color(1f, 0.12f, 0.1f, 1f));
            DrawFilterToggle(rects[4], "Items", MapUiState.ShowItemDrops, new Color(1f, 0.86f, 0.16f, 1f));
            DrawFilterToggle(rects[5], "Thornback", MapUiState.ShowThornbackElder, new Color(1f, 0.36f, 0.95f, 1f));
            DrawFilterToggle(rects[6], "Sprites", MapUiState.ShowSpriteIcons, new Color(0.88f, 0.92f, 1f, 1f));
            if (MapUiState.ShowSpriteIcons)
                DrawSpriteSizeControl(BigMapSpriteSizeRect(panel));

            DrawFilterButton(BigMapCenterPlayerRect(panel), "Center", true, new Color(0.22f, 1f, 0.95f, 1f));
            DrawFilterButton(BigMapClearWaypointRect(panel), "Clear Mark", MapUiState.HasWaypoint, new Color(1f, 0.58f, 0.26f, 1f));
        }

        private bool TryToggleFilterAt(Rect panel, Vector2 mouse)
        {
            Rect[] rects = BigMapFilterRects(panel);
            if (rects[0].Contains(mouse))
            {
                MapUiState.ShowInteractables = !MapUiState.ShowInteractables;
                MapUiState.Save();
                hazardScanner.RequestImmediateFullRefresh();
                return true;
            }

            if (rects[1].Contains(mouse))
            {
                MapUiState.ShowHazards = !MapUiState.ShowHazards;
                MapUiState.Save();
                hazardScanner.RequestImmediateFullRefresh();
                return true;
            }

            if (rects[2].Contains(mouse))
            {
                MapUiState.ShowTraders = !MapUiState.ShowTraders;
                MapUiState.Save();
                hazardScanner.RequestImmediateFullRefresh();
                return true;
            }

            if (rects[3].Contains(mouse))
            {
                MapUiState.ShowEnemies = !MapUiState.ShowEnemies;
                MapUiState.Save();
                hazardScanner.RequestImmediateFullRefresh();
                return true;
            }

            if (rects[4].Contains(mouse))
            {
                MapUiState.ShowItemDrops = !MapUiState.ShowItemDrops;
                MapUiState.Save();
                hazardScanner.RequestImmediateFullRefresh();
                return true;
            }

            if (rects[5].Contains(mouse))
            {
                MapUiState.ShowThornbackElder = !MapUiState.ShowThornbackElder;
                MapUiState.Save();
                hazardScanner.RequestImmediateFullRefresh();
                return true;
            }

            if (rects[6].Contains(mouse))
            {
                MapUiState.ShowSpriteIcons = !MapUiState.ShowSpriteIcons;
                MapUiState.Save();
                return true;
            }

            if (MapUiState.ShowSpriteIcons && TryAdjustSpriteSizeAt(BigMapSpriteSizeRect(panel), mouse))
                return true;

            if (BigMapCenterPlayerRect(panel).Contains(mouse))
            {
                TryCenterBigMapOnPlayerWithFeedback(GetBigMapViewport(panel));
                return true;
            }

            if (BigMapClearWaypointRect(panel).Contains(mouse))
            {
                MapUiState.ClearWaypoint();
                MapUiState.Save();
                return true;
            }

            return false;
        }

        private static Rect[] BigMapFilterRects(Rect panel)
        {
            const int count = 7;
            const float maxWidth = 92f;
            const float minWidth = 52f;
            const float gap = 6f;
            float spriteSizeWidth = MapUiState.ShowSpriteIcons ? BigMapSpriteSizeControlWidth : 0f;
            float gapCount = MapUiState.ShowSpriteIcons ? count + 2f : count + 1f;
            float availableWidth = Mathf.Max(1f, panel.width - 28f - spriteSizeWidth - BigMapCenterPlayerWidth - BigMapClearWaypointWidth - gap * gapCount);
            float width = Mathf.Clamp(availableWidth / count, minWidth, maxWidth);
            float totalWidth = width * count + gap * gapCount + spriteSizeWidth + BigMapCenterPlayerWidth + BigMapClearWaypointWidth;
            float startX = panel.center.x - totalWidth * 0.5f;
            float y = panel.y + 31f;
            Rect[] rects = new Rect[count];
            for (int i = 0; i < count; i++)
            {
                rects[i] = new Rect(startX + (width + gap) * i, y, width, BigMapToggleHeight);
            }

            return rects;
        }

        private static Rect BigMapSpriteSizeRect(Rect panel)
        {
            Rect[] filters = BigMapFilterRects(panel);
            Rect last = filters[filters.Length - 1];
            return new Rect(last.xMax + 6f, last.y, BigMapSpriteSizeControlWidth, BigMapToggleHeight);
        }

        private static Rect BigMapCenterPlayerRect(Rect panel)
        {
            Rect[] filters = BigMapFilterRects(panel);
            Rect anchor = MapUiState.ShowSpriteIcons ? BigMapSpriteSizeRect(panel) : filters[filters.Length - 1];
            return new Rect(anchor.xMax + 6f, anchor.y, BigMapCenterPlayerWidth, BigMapToggleHeight);
        }

        private static Rect BigMapClearWaypointRect(Rect panel)
        {
            Rect anchor = BigMapCenterPlayerRect(panel);
            return new Rect(anchor.xMax + 6f, anchor.y, BigMapClearWaypointWidth, BigMapToggleHeight);
        }

        private bool TryAdjustSpriteSizeAt(Rect rect, Vector2 mouse)
        {
            if (!rect.Contains(mouse))
                return false;

            Rect minus = new Rect(rect.x, rect.y, 24f, rect.height);
            Rect plus = new Rect(rect.xMax - 24f, rect.y, 24f, rect.height);
            if (minus.Contains(mouse))
                MapUiState.AdjustBigMapSpriteScale(-BigMapSpriteScaleStep);
            else if (plus.Contains(mouse))
                MapUiState.AdjustBigMapSpriteScale(BigMapSpriteScaleStep);
            else
                return false;

            MapUiState.Save();
            return true;
        }

        private void DrawSpriteSizeControl(Rect rect)
        {
            Rect minus = new Rect(rect.x, rect.y, 24f, rect.height);
            Rect label = new Rect(rect.x + 26f, rect.y, rect.width - 52f, rect.height);
            Rect plus = new Rect(rect.xMax - 24f, rect.y, 24f, rect.height);
            DrawFilterButton(minus, "-", true, new Color(0.72f, 0.82f, 0.9f, 1f));
            DrawFilterButton(label, MapUiState.BigMapSpriteScale.ToString("0.0") + "x", true, new Color(0.18f, 0.22f, 0.25f, 1f));
            DrawFilterButton(plus, "+", true, new Color(0.72f, 0.82f, 0.9f, 1f));
        }

        private void DrawFilterToggle(Rect rect, string text, bool enabled, Color accent)
        {
            DrawFilterButton(rect, text, enabled, accent);
        }

        private void DrawFilterButton(Rect rect, string text, bool enabled, Color accent)
        {
            Color oldColor = GUI.color;
            GUI.color = enabled ? new Color(accent.r, accent.g, accent.b, 0.82f) : new Color(0.08f, 0.09f, 0.1f, 0.74f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 2f, rect.width, 2f), Texture2D.whiteTexture);
            GUI.color = enabled ? Color.black : new Color(0.72f, 0.78f, 0.82f, 1f);
            int oldFontSize = toggleStyle.fontSize;
            toggleStyle.fontSize = rect.width < 70f ? 9 : 10;
            GUI.Label(rect, text, toggleStyle);
            toggleStyle.fontSize = oldFontSize;
            GUI.color = oldColor;
        }

        private static void DrawCircularTexture(Rect viewport, Rect textureRect, Texture texture)
        {
            int strips = Mathf.Clamp(Mathf.CeilToInt(viewport.height / 4f), 24, 96);
            float stripHeight = viewport.height / strips;
            float radius = Mathf.Min(viewport.width, viewport.height) * 0.5f;
            Vector2 center = viewport.center;

            for (int i = 0; i < strips; i++)
            {
                float y = viewport.y + i * stripHeight;
                float midY = y + stripHeight * 0.5f;
                float dy = midY - center.y;
                float halfWidth = Mathf.Sqrt(Mathf.Max(0f, radius * radius - dy * dy));
                if (halfWidth <= 0.01f)
                    continue;

                Rect strip = new Rect(center.x - halfWidth, y, halfWidth * 2f, stripHeight + 1f);
                GUI.BeginGroup(strip);
                GUI.DrawTexture(
                    new Rect(textureRect.x - strip.x, textureRect.y - strip.y, textureRect.width, textureRect.height),
                    texture,
                    ScaleMode.StretchToFill,
                    true);
                GUI.EndGroup();
            }
        }

        private static float ApplyZoom(float current, float wheelDelta, float min, float max)
        {
            float multiplier = Mathf.Pow(ZoomStep, -wheelDelta);
            return Mathf.Clamp(current * multiplier, min, max);
        }

        private static bool ShouldDrawCurrentGuiEvent()
        {
            return Event.current == null || Event.current.type == EventType.Repaint;
        }

        private static Body GetLocalBody()
        {
            return PlayerCamera.main == null ? null : PlayerCamera.main.body;
        }

        private static void DrawShadowedLabel(Rect rect, string text, GUIStyle style, Color color)
        {
            Color oldColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.65f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            GUI.color = color;
            GUI.Label(rect, text, style);
            GUI.color = oldColor;
        }

        private void EnsureStyles()
        {
            if (panelStyle != null)
                return;

            panelTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            panelTexture.SetPixel(0, 0, PanelColor);
            panelTexture.Apply();

            viewportTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            viewportTexture.SetPixel(0, 0, ViewportColor);
            viewportTexture.Apply();

            circlePanelTexture = CreateCircleTexture(128, PanelColor, Color.clear, 1.5f);
            markerCircleTexture = CreateCircleTexture(64, Color.white, Color.clear, 1f);

            panelStyle = new GUIStyle(GUI.skin.box);
            panelStyle.normal.background = panelTexture;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };

            smallTextStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };

            markerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 9,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };

            playerStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };

            toggleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
        }

        private static Texture2D CreateCircleTexture(int size, Color insideColor, Color outsideColor, float edgeSoftness)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color32[] pixels = new Color32[size * size];
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float edge = edgeSoftness <= 0.01f ? 0f : Mathf.Clamp01((distance - radius) / edgeSoftness);
                    pixels[y * size + x] = distance <= radius
                        ? Color.Lerp(insideColor, outsideColor, edge)
                        : outsideColor;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void Dispose()
        {
            worldSampler.Release();
            MapUiState.Save();

            if (panelTexture != null)
            {
                Destroy(panelTexture);
                panelTexture = null;
            }

            if (viewportTexture != null)
            {
                Destroy(viewportTexture);
                viewportTexture = null;
            }

            if (circlePanelTexture != null)
            {
                Destroy(circlePanelTexture);
                circlePanelTexture = null;
            }

            if (markerCircleTexture != null)
            {
                Destroy(markerCircleTexture);
                markerCircleTexture = null;
            }
        }
    }
}
