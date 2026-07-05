using System.Reflection;
using UnityEngine;

namespace MistalCrysMap.Runtime
{
    /// <summary>
    /// 从 WorldGeneration 读取真实世界块数据，并缓存成一张可缩放绘制的地图贴图。
    /// 默认全图可见；探索模式开启后只显示已经走过的区域。
    /// </summary>
    internal sealed class MapWorldSampler
    {
        private const int MaxTextureSide = 1024;
        private const float ExplorationTextureRefreshInterval = 0.5f;
        private const float TerrainTextureRefreshInterval = 1.5f;
        private const float BlockDirtySignalInterval = 0.75f;
        private const int ExplorationRadiusBlocks = 40;

        private static readonly BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly FieldInfo WorldField = typeof(WorldGeneration).GetField("world", FieldFlags);
        private static readonly FieldInfo WidthField = typeof(WorldGeneration).GetField("width", FieldFlags);
        private static readonly FieldInfo HeightField = typeof(WorldGeneration).GetField("height", FieldFlags);
        private static readonly FieldInfo BlocksField = typeof(WorldGeneration).GetField("worldBlocks", FieldFlags);
        private static readonly FieldInfo TileColorsField = typeof(WorldGeneration).GetField("tileColors", FieldFlags);
        private static readonly FieldInfo GeneratingWorldField = typeof(WorldGeneration).GetField("generatingWorld", FieldFlags);
        private static readonly FieldInfo InstantiatingWorldField = typeof(WorldGeneration).GetField("instantiatingWorld", FieldFlags);
        private static readonly PropertyInfo WorldExistsProperty = typeof(WorldGeneration).GetProperty("worldExists", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static int worldChangeVersion;
        private static float nextBlockDirtySignalTime;

        private WorldGeneration currentWorld;
        private ushort[,] currentBlocks;
        private bool[,] explored;
        private bool terrainDirty;
        private bool explorationDirty;
        private bool currentExplorationMode;
        private bool lastExplorationMode;
        private Color[] lastTileColors;
        private Vector2Int lastExplorationCenter = new Vector2Int(int.MinValue, int.MinValue);
        private float lastRefreshTime = -999f;
        private float lastTerrainRefreshTime = -999f;
        private int observedWorldChangeVersion = -1;
        private int worldWidth;
        private int worldHeight;
        private int textureStep = 1;
        private Texture2D texture;

        public Texture2D Texture => texture;
        public int WorldWidth => worldWidth;
        public int WorldHeight => worldHeight;
        public bool HasWorld => currentWorld != null && currentBlocks != null && worldWidth > 0 && worldHeight > 0 && texture != null;

        public static void MarkWorldDirty()
        {
            if (Time.unscaledTime < nextBlockDirtySignalTime)
                return;

            nextBlockDirtySignalTime = Time.unscaledTime + BlockDirtySignalInterval;
            unchecked
            {
                worldChangeVersion++;
            }
        }

        public void Update(bool explorationMode, Body playerBody, bool shouldRefreshTexture)
        {
            currentExplorationMode = explorationMode;
            WorldGeneration world = GetWorld();
            if (world == null)
            {
                ResetWorldState();
                return;
            }

            ushort[,] blocks = GetBlocks(world);
            if (blocks == null)
            {
                ResetWorldState();
                return;
            }

            if (!IsWorldReady(world))
                return;

            int newWidth = GetDimension(world, WidthField, blocks.GetLength(0));
            int newHeight = GetDimension(world, HeightField, blocks.GetLength(1));
            newWidth = Mathf.Clamp(newWidth, 1, blocks.GetLength(0));
            newHeight = Mathf.Clamp(newHeight, 1, blocks.GetLength(1));

            if (world != currentWorld || blocks != currentBlocks || newWidth != worldWidth || newHeight != worldHeight)
                AttachWorld(world, blocks, newWidth, newHeight);

            if (explorationMode && playerBody != null)
                MarkExplored(world.WorldToBlockPos(playerBody.transform.position));

            Color[] tileColors = TileColorsField?.GetValue(currentWorld) as Color[];
            if (!ReferenceEquals(tileColors, lastTileColors))
            {
                lastTileColors = tileColors;
                terrainDirty = true;
            }

            if (observedWorldChangeVersion != worldChangeVersion)
            {
                observedWorldChangeVersion = worldChangeVersion;
                terrainDirty = true;
            }

            if (!shouldRefreshTexture)
                return;

            bool modeChanged = lastExplorationMode != explorationMode;
            bool terrainRefreshDue = terrainDirty && Time.unscaledTime - lastTerrainRefreshTime >= TerrainTextureRefreshInterval;
            float explorationInterval = terrainDirty ? TerrainTextureRefreshInterval : ExplorationTextureRefreshInterval;
            bool explorationRefreshDue = explorationDirty && Time.unscaledTime - lastRefreshTime >= explorationInterval;
            if (texture == null || modeChanged || terrainRefreshDue || explorationRefreshDue)
                RebuildTexture(explorationMode);
        }

        public Vector2Int WorldToBlock(Vector2 worldPosition)
        {
            if (currentWorld == null)
                return Vector2Int.zero;

            return currentWorld.WorldToBlockPos(worldPosition);
        }

        public Vector2 BlockToWorld(Vector2Int blockPosition)
        {
            if (currentWorld == null)
                return Vector2.zero;

            int maxX = Mathf.Max(0, worldWidth - 1);
            int maxY = Mathf.Max(0, worldHeight - 1);
            blockPosition.x = Mathf.Clamp(blockPosition.x, 0, maxX);
            blockPosition.y = Mathf.Clamp(blockPosition.y, 0, maxY);
            return currentWorld.BlockToWorldPos(blockPosition);
        }

        public bool IsWorldPositionRevealed(Vector2 worldPosition)
        {
            if (!currentExplorationMode)
                return true;

            if (explored == null || currentWorld == null)
                return false;

            Vector2Int block = WorldToBlock(worldPosition);
            if (block.x < 0 || block.y < 0 || block.x >= worldWidth || block.y >= worldHeight)
                return false;

            return explored[block.x, block.y];
        }

        public void Release()
        {
            if (texture != null)
            {
                Object.Destroy(texture);
                texture = null;
            }

            ResetWorldState();
        }

        private static WorldGeneration GetWorld()
        {
            WorldGeneration world = WorldField?.GetValue(null) as WorldGeneration;
            return world != null ? world : Object.FindObjectOfType<WorldGeneration>();
        }

        private static ushort[,] GetBlocks(WorldGeneration world)
        {
            return world == null ? null : BlocksField?.GetValue(world) as ushort[,];
        }

        private static bool IsWorldReady(WorldGeneration world)
        {
            if (world == null)
                return false;

            object value = WorldExistsProperty?.GetValue(world, null);
            if (!(value is bool exists) || !exists)
                return false;

            bool generatingWorld = GeneratingWorldField?.GetValue(world) is bool generating && generating;
            bool instantiatingWorld = InstantiatingWorldField?.GetValue(world) is bool instantiating && instantiating;
            return !generatingWorld && !instantiatingWorld;
        }

        private static int GetDimension(WorldGeneration world, FieldInfo field, int fallback)
        {
            object value = field?.GetValue(world);
            if (value is uint uintValue)
                return (int)uintValue;

            if (value is int intValue)
                return intValue;

            return fallback;
        }

        private void AttachWorld(WorldGeneration world, ushort[,] blocks, int width, int height)
        {
            currentWorld = world;
            currentBlocks = blocks;
            worldWidth = width;
            worldHeight = height;
            explored = new bool[worldWidth, worldHeight];
            terrainDirty = true;
            explorationDirty = true;
            lastTileColors = null;
            lastExplorationCenter = new Vector2Int(int.MinValue, int.MinValue);
            lastRefreshTime = -999f;
            lastTerrainRefreshTime = -999f;
            observedWorldChangeVersion = worldChangeVersion;
        }

        private void ResetWorldState()
        {
            currentWorld = null;
            currentBlocks = null;
            explored = null;
            worldWidth = 0;
            worldHeight = 0;
            terrainDirty = false;
            explorationDirty = false;
            lastTileColors = null;
            lastExplorationCenter = new Vector2Int(int.MinValue, int.MinValue);
            lastRefreshTime = -999f;
            lastTerrainRefreshTime = -999f;
        }

        private void MarkExplored(Vector2Int center)
        {
            if (explored == null)
                return;

            if (center == lastExplorationCenter)
                return;

            lastExplorationCenter = center;
            int minX = Mathf.Max(0, center.x - ExplorationRadiusBlocks);
            int maxX = Mathf.Min(worldWidth - 1, center.x + ExplorationRadiusBlocks);
            int minY = Mathf.Max(0, center.y - ExplorationRadiusBlocks);
            int maxY = Mathf.Min(worldHeight - 1, center.y + ExplorationRadiusBlocks);
            int radiusSq = ExplorationRadiusBlocks * ExplorationRadiusBlocks;

            for (int y = minY; y <= maxY; y++)
            {
                int dy = y - center.y;
                for (int x = minX; x <= maxX; x++)
                {
                    int dx = x - center.x;
                    if (dx * dx + dy * dy > radiusSq || explored[x, y])
                        continue;

                    explored[x, y] = true;
                    explorationDirty = true;
                }
            }
        }

        private void RebuildTexture(bool explorationMode)
        {
            if (currentWorld == null || currentBlocks == null || worldWidth <= 0 || worldHeight <= 0)
                return;

            textureStep = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(worldWidth, worldHeight) / (float)MaxTextureSide));
            int textureWidth = Mathf.Max(1, Mathf.CeilToInt(worldWidth / (float)textureStep));
            int textureHeight = Mathf.Max(1, Mathf.CeilToInt(worldHeight / (float)textureStep));

            if (texture == null || texture.width != textureWidth || texture.height != textureHeight)
            {
                if (texture != null)
                    Object.Destroy(texture);

                texture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
            }

            Color[] tileColors = TileColorsField?.GetValue(currentWorld) as Color[];
            Color32[] pixels = new Color32[textureWidth * textureHeight];
            for (int ty = 0; ty < textureHeight; ty++)
            {
                int blockY = Mathf.Min(worldHeight - 1, ty * textureStep + textureStep / 2);
                for (int tx = 0; tx < textureWidth; tx++)
                {
                    int blockX = Mathf.Min(worldWidth - 1, tx * textureStep + textureStep / 2);
                    pixels[ty * textureWidth + tx] = GetPixelColor(blockX, blockY, tileColors, explorationMode);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            lastExplorationMode = explorationMode;
            terrainDirty = false;
            explorationDirty = false;
            lastRefreshTime = Time.unscaledTime;
            lastTerrainRefreshTime = Time.unscaledTime;
        }

        private Color32 GetPixelColor(int blockX, int blockY, Color[] tileColors, bool explorationMode)
        {
            if (explorationMode && (explored == null || !explored[blockX, blockY]))
                return new Color32(48, 54, 58, 132);

            ushort block = currentBlocks[blockX, blockY];
            if (block == 0)
                return new Color32(38, 48, 51, 176);

            Color color = GetTileColor(block, tileColors);
            color.a = Mathf.Max(color.a, 0.92f);
            return color;
        }

        private static Color GetTileColor(ushort block, Color[] tileColors)
        {
            if (tileColors != null && block < tileColors.Length)
            {
                Color color = tileColors[block];
                if (color.maxColorComponent > 0.02f)
                    return color;
            }

            float r = 0.25f + ((block * 37) % 100) / 220f;
            float g = 0.28f + ((block * 53) % 100) / 240f;
            float b = 0.30f + ((block * 71) % 100) / 260f;
            return new Color(r, g, b, 1f);
        }
    }
}
