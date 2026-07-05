using System.Collections.Generic;
using UnityEngine;

namespace MistalCrysMap.Runtime
{
    /// <summary>
    /// 扫描单机世界里已经生成的危险物。
    /// 第一版只读本机场景，不做任何 KrokMP 同步。
    /// </summary>
    internal sealed class MapHazardScanner
    {
        private const float RefreshInterval = 2f;
        private const float ScanStepInterval = 0.06f;
        private const float DynamicRefreshInterval = 0.25f;
        private const float LocalRefreshInterval = 0.18f;
        private const float LocalScanRadius = 120f;
        private const int LocalColliderCapacity = 1536;
        private const int MarkerScanTypeCount = 18;
        private static readonly string[] TrackedBuildingTerms =
        {
            "pop",
            "hydreed",
            "bush",
            "bounceshroom",
            "glowplant",
            "geotree",
            "mushplant",
            "stoneplant",
            "leadbush",
            "driedbush",
            "cactus",
            "brownshroom",
            "sandrose",
            "plantation",
            "bananaplant",
            "corpse",
            "animalcorpse",
            "bloodcrystal",
            "digestioncrystal",
            "drillpod",
            "emissivecrystal",
            "oxygencrystal",
            "reliefcrystal",
            "soothingcrystal",
            "turbulentcrystal",
            "vine"
        };

        private static readonly string[] HazardBuildingTerms =
        {
            "spentfuel",
            "spentfuelandbarrel",
            "radbarrel",
            "minibarrel",
            "radioactivebarrel",
            "radiationbarrel",
            "wastebarrel",
            "barbedwirefence",
            "barbedfence",
            "stalactite",
            "skullcrusher",
            "caveticks",
            "cavetickspawner",
            "grabberplant"
        };

        private static readonly string[] ThornbackElderTerms =
        {
            "thornbackelder"
        };

        private readonly List<MapMarker> markers = new List<MapMarker>();
        private readonly List<MapMarker> slowMarkers = new List<MapMarker>();
        private readonly List<MapMarker> dynamicMarkers = new List<MapMarker>();
        private readonly List<MapMarker> pendingMarkers = new List<MapMarker>();
        private readonly HashSet<int> combinedObjectIds = new HashSet<int>();
        private readonly HashSet<int> localObjectIds = new HashSet<int>();
        private readonly HashSet<int> pendingObjectIds = new HashSet<int>();
        private readonly HashSet<int> dynamicObjectIds = new HashSet<int>();
        private readonly Collider2D[] localColliders = new Collider2D[LocalColliderCapacity];
        private float nextRefreshTime;
        private float nextDynamicRefreshTime;
        private float nextLocalRefreshTime;
        private int scanIndex;
        private bool scanning;
        private bool fullScanMode = true;
        private bool immediateFullRefreshRequested;

        public IReadOnlyList<MapMarker> Markers => markers;

        public void RequestImmediateFullRefresh()
        {
            immediateFullRefreshRequested = true;
            nextRefreshTime = 0f;
            nextDynamicRefreshTime = 0f;
        }

        public void RefreshIfNeeded(Body body, bool useFullScan)
        {
            if (!useFullScan)
            {
                if (fullScanMode)
                {
                    fullScanMode = false;
                    nextLocalRefreshTime = 0f;
                    markers.Clear();
                    scanning = false;
                    pendingMarkers.Clear();
                    pendingObjectIds.Clear();
                }

                RefreshLocalMarkersIfNeeded(body);
                return;
            }

            if (!fullScanMode)
            {
                fullScanMode = true;
                nextRefreshTime = 0f;
                nextDynamicRefreshTime = 0f;
                markers.Clear();
                scanning = false;
                pendingMarkers.Clear();
                pendingObjectIds.Clear();
                immediateFullRefreshRequested = true;
            }

            if (immediateFullRefreshRequested)
            {
                RefreshFullMarkersImmediately();
                return;
            }

            if (Time.unscaledTime >= nextDynamicRefreshTime)
            {
                RefreshDynamicMarkers();
                CombineMarkers();
                nextDynamicRefreshTime = Time.unscaledTime + DynamicRefreshInterval;
            }

            if (Time.unscaledTime < nextRefreshTime)
                return;

            if (!scanning)
            {
                scanning = true;
                scanIndex = 0;
                pendingMarkers.Clear();
                pendingObjectIds.Clear();
            }

            ScanHazardType(scanIndex);
            scanIndex++;

            if (scanIndex >= MarkerScanTypeCount)
            {
                slowMarkers.Clear();
                slowMarkers.AddRange(pendingMarkers);
                pendingMarkers.Clear();
                pendingObjectIds.Clear();
                scanning = false;
                nextRefreshTime = Time.unscaledTime + RefreshInterval;
                CombineMarkers();
                return;
            }

            nextRefreshTime = Time.unscaledTime + ScanStepInterval;
        }

        private void RefreshFullMarkersImmediately()
        {
            immediateFullRefreshRequested = false;
            scanning = false;
            scanIndex = 0;
            pendingMarkers.Clear();
            pendingObjectIds.Clear();

            for (int i = 0; i < MarkerScanTypeCount; i++)
            {
                ScanHazardType(i);
            }

            slowMarkers.Clear();
            slowMarkers.AddRange(pendingMarkers);
            pendingMarkers.Clear();
            pendingObjectIds.Clear();

            RefreshDynamicMarkers();
            CombineMarkers();
            nextRefreshTime = Time.unscaledTime + RefreshInterval;
            nextDynamicRefreshTime = Time.unscaledTime + DynamicRefreshInterval;
        }

        private void ScanHazardType(int index)
        {
            MapObjectRegistry.CleanupDestroyedReferences();

            switch (index)
            {
                case 0:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<MineScript>(), "M", new Color(1f, 0.18f, 0.18f, 1f), MapMarkerKind.Hazard);
                    break;
                case 1:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<GunmineScript>(), "G", new Color(1f, 0.42f, 0.1f, 1f), MapMarkerKind.Hazard);
                    break;
                case 2:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<TurretScript>(), "T", new Color(1f, 0.76f, 0.18f, 1f), MapMarkerKind.Hazard);
                    break;
                case 3:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<BearTrap>(), "B", new Color(1f, 0.35f, 0.62f, 1f), MapMarkerKind.Hazard);
                    break;
                case 4:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<CoilScript>(), "C", new Color(0.2f, 0.92f, 1f, 1f), MapMarkerKind.Hazard);
                    break;
                case 5:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<SpikeStabberScript>(), "S", new Color(0.52f, 1f, 0.36f, 1f), MapMarkerKind.Hazard);
                    break;
                case 6:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<SoundCannon>(), "N", new Color(0.7f, 0.45f, 1f, 1f), MapMarkerKind.Hazard);
                    break;
                case 7:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<JumpPadScript>(), "J", new Color(0.35f, 0.65f, 1f, 1f), MapMarkerKind.Hazard);
                    break;
                case 8:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddExtraHazards();
                    break;
                case 9:
                    if (!MapUiState.ShowInteractables)
                        break;
                    AddInteractables();
                    break;
                case 10:
                    break;
                case 11:
                    break;
                case 12:
                    if (!MapUiState.ShowInteractables)
                        break;
                    AddMarkers(Object.FindObjectsOfType<CorpseScript>(), string.Empty, new Color(0.8f, 0.52f, 0.14f, 1f), MapMarkerKind.Interactable);
                    break;
                case 13:
                    if (!MapUiState.ShowInteractables)
                        break;
                    AddPlants();
                    break;
                case 14:
                    if (!MapUiState.ShowTraders)
                        break;
                    AddMerchants();
                    break;
                case 15:
                    if (!MapUiState.ShowInteractables)
                        break;
                    AddMarkers(Object.FindObjectsOfType<BushCol>(), string.Empty, new Color(0.58f, 0.78f, 1f, 1f), MapMarkerKind.Interactable);
                    break;
                case 16:
                    if (!MapUiState.ShowInteractables)
                        break;
                    AddMarkers(Object.FindObjectsOfType<BounceShroom>(), string.Empty, new Color(0.58f, 0.78f, 1f, 1f), MapMarkerKind.Interactable);
                    break;
                case 17:
                    if (!MapUiState.ShowHazards)
                        break;
                    AddMarkers(Object.FindObjectsOfType<CaveTickSpawner>(), "K", new Color(1f, 0.3f, 0.16f, 1f), MapMarkerKind.Hazard);
                    break;
            }
        }

        private void AddExtraHazards()
        {
            Color color = new Color(1f, 0.3f, 0.16f, 1f);
            AddMarkers(Object.FindObjectsOfType<RadioactiveObject>(), "R", color, MapMarkerKind.Hazard);
            AddMarkers(Object.FindObjectsOfType<BarbedFence>(), "W", color, MapMarkerKind.Hazard);
            AddMarkers(Object.FindObjectsOfType<CaveTicks>(), "K", color, MapMarkerKind.Hazard);
            AddMarkers(Object.FindObjectsOfType<GrabberPlant>(), "V", color, MapMarkerKind.Hazard);
            AddMarkers(Object.FindObjectsOfType<StalactiteDropper>(), "^", color, MapMarkerKind.Hazard);
            AddMarkers(MapObjectRegistry.Buildings, "!", color, MapMarkerKind.Hazard, ShouldSkipHazardBuilding);
        }

        private void AddInteractables()
        {
            Color color = new Color(0.58f, 0.78f, 1f, 1f);
            AddMarkers(Object.FindObjectsOfType<UsableObject>(), string.Empty, color, MapMarkerKind.Interactable, ShouldSkipInteractable);
            AddMarkers(Object.FindObjectsOfType<Container>(), string.Empty, color, MapMarkerKind.Interactable);
            AddMarkers(MapObjectRegistry.Buildings, string.Empty, color, MapMarkerKind.Interactable, ShouldSkipInteractableBuilding);
        }

        private void AddDropItems()
        {
            AddMarkers(Item.allItems, string.Empty, new Color(1f, 0.86f, 0.16f, 1f), MapMarkerKind.ItemDrop, ShouldSkipDroppedItem);
        }

        private void RefreshDynamicMarkers()
        {
            MapObjectRegistry.CleanupDestroyedReferences();
            dynamicMarkers.Clear();
            dynamicObjectIds.Clear();

            if (MapUiState.ShowItemDrops)
                AddMarkers(dynamicMarkers, dynamicObjectIds, Item.allItems, string.Empty, new Color(1f, 0.86f, 0.16f, 1f), MapMarkerKind.ItemDrop, ShouldSkipDroppedItem);

            if (MapUiState.ShowTraders)
                AddMarkers(dynamicMarkers, dynamicObjectIds, MapObjectRegistry.Traders, "$", new Color(0.35f, 1f, 0.58f, 1f), MapMarkerKind.Trader);

            if (MapUiState.ShowThornbackElder)
                AddMarkers(dynamicMarkers, dynamicObjectIds, MapObjectRegistry.ElderThornbacks, "Boss", new Color(1f, 0.36f, 0.95f, 1f), MapMarkerKind.ThornbackElder);

            if (MapUiState.ShowEnemies)
            {
                AddMarkers(dynamicMarkers, dynamicObjectIds, MapObjectRegistry.CrystalEnemies, string.Empty, new Color(1f, 0.12f, 0.1f, 1f), MapMarkerKind.Enemy, ShouldSkipEnemy);
                AddMarkers(dynamicMarkers, dynamicObjectIds, MapObjectRegistry.Buildings, string.Empty, new Color(1f, 0.12f, 0.1f, 1f), MapMarkerKind.Enemy, ShouldSkipEnemyBuilding);
            }
        }

        private void CombineMarkers()
        {
            markers.Clear();
            combinedObjectIds.Clear();
            AddCombinedMarkers(slowMarkers);
            AddCombinedMarkers(dynamicMarkers);
        }

        private void AddCombinedMarkers(IReadOnlyList<MapMarker> source)
        {
            for (int i = 0; i < source.Count; i++)
            {
                MapMarker marker = source[i];
                if (marker.Target == null)
                    continue;

                int id = GetMarkerObjectKey(marker.Target, marker.Kind);
                if (!combinedObjectIds.Add(id))
                    continue;

                markers.Add(marker);
            }
        }

        private void RefreshLocalMarkersIfNeeded(Body body)
        {
            if (body == null || Time.unscaledTime < nextLocalRefreshTime)
                return;

            nextLocalRefreshTime = Time.unscaledTime + LocalRefreshInterval;
            markers.Clear();
            localObjectIds.Clear();

            Vector2 center = body.transform.position;
            int count = Physics2D.OverlapCircleNonAlloc(center, LocalScanRadius, localColliders);
            for (int i = 0; i < count; i++)
            {
                Collider2D collider = localColliders[i];
                if (collider == null || !collider.gameObject.activeInHierarchy)
                    continue;

                AddLocalMarkersFromCollider(collider);
            }

            AddNearbyRegistryMarkers(center);
        }

        private void AddLocalMarkersFromCollider(Collider2D collider)
        {
            GameObject gameObject = collider.gameObject;
            BuildingEntity building = gameObject.GetComponentInParent<BuildingEntity>();

            if (MapUiState.ShowTraders)
                AddLocalMarker(gameObject.GetComponentInParent<TraderScript>(), "$", new Color(0.35f, 1f, 0.58f, 1f), MapMarkerKind.Trader);

            if (MapUiState.ShowThornbackElder)
                AddLocalMarker(gameObject.GetComponentInParent<ElderThornbackBehaviour>(), "Boss", new Color(1f, 0.36f, 0.95f, 1f), MapMarkerKind.ThornbackElder);

            if (MapUiState.ShowItemDrops)
                AddLocalMarker(gameObject.GetComponentInParent<Item>(), string.Empty, new Color(1f, 0.86f, 0.16f, 1f), MapMarkerKind.ItemDrop, ShouldSkipDroppedItem);

            if (MapUiState.ShowEnemies)
            {
                AddLocalMarker(gameObject.GetComponentInParent<CrystalEnemy>(), string.Empty, new Color(1f, 0.12f, 0.1f, 1f), MapMarkerKind.Enemy, ShouldSkipEnemy);
                if (building != null && !ShouldSkipEnemyBuilding(building))
                    AddLocalMarker(building, string.Empty, new Color(1f, 0.12f, 0.1f, 1f), MapMarkerKind.Enemy);
            }

            if (MapUiState.ShowHazards)
            {
                Color hazardColor = new Color(1f, 0.3f, 0.16f, 1f);
                AddLocalMarker(gameObject.GetComponentInParent<MineScript>(), "M", new Color(1f, 0.18f, 0.18f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<GunmineScript>(), "G", new Color(1f, 0.42f, 0.1f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<TurretScript>(), "T", new Color(1f, 0.76f, 0.18f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<BearTrap>(), "B", new Color(1f, 0.35f, 0.62f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<CoilScript>(), "C", new Color(0.2f, 0.92f, 1f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<SpikeStabberScript>(), "S", new Color(0.52f, 1f, 0.36f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<SoundCannon>(), "N", new Color(0.7f, 0.45f, 1f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<JumpPadScript>(), "J", new Color(0.35f, 0.65f, 1f, 1f), MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<RadioactiveObject>(), "R", hazardColor, MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<BarbedFence>(), "W", hazardColor, MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<CaveTicks>(), "K", hazardColor, MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<CaveTickSpawner>(), "K", hazardColor, MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<GrabberPlant>(), "V", hazardColor, MapMarkerKind.Hazard);
                AddLocalMarker(gameObject.GetComponentInParent<StalactiteDropper>(), "^", hazardColor, MapMarkerKind.Hazard);
                if (building != null && !ShouldSkipHazardBuilding(building))
                    AddLocalMarker(building, "!", hazardColor, MapMarkerKind.Hazard);
            }

            if (MapUiState.ShowInteractables)
            {
                Color interactColor = new Color(0.58f, 0.78f, 1f, 1f);
                Color plantColor = new Color(0.62f, 0.88f, 0.38f, 1f);
                AddLocalMarker(gameObject.GetComponentInParent<UsableObject>(), string.Empty, interactColor, MapMarkerKind.Interactable, ShouldSkipInteractable);
                AddLocalMarker(gameObject.GetComponentInParent<Container>(), string.Empty, interactColor, MapMarkerKind.Interactable);
                AddLocalMarker(gameObject.GetComponentInParent<CorpseScript>(), string.Empty, new Color(0.8f, 0.52f, 0.14f, 1f), MapMarkerKind.Interactable);
                AddLocalMarker(gameObject.GetComponentInParent<BushCol>(), string.Empty, interactColor, MapMarkerKind.Interactable);
                AddLocalMarker(gameObject.GetComponentInParent<BounceShroom>(), string.Empty, interactColor, MapMarkerKind.Interactable);
                AddLocalMarker(gameObject.GetComponentInParent<BananaPlantSlip>(), string.Empty, plantColor, MapMarkerKind.Interactable);
                AddLocalMarker(gameObject.GetComponentInParent<LeadbushScript>(), string.Empty, plantColor, MapMarkerKind.Interactable);
                AddLocalMarker(gameObject.GetComponentInParent<CactusScript>(), string.Empty, plantColor, MapMarkerKind.Interactable);
                AddLocalMarker(gameObject.GetComponentInParent<DrillPod>(), string.Empty, plantColor, MapMarkerKind.Interactable);
                if (building != null && !ShouldSkipInteractableBuilding(building))
                    AddLocalMarker(building, string.Empty, interactColor, MapMarkerKind.Interactable);
            }
        }

        private void AddNearbyRegistryMarkers(Vector2 center)
        {
            float radiusSq = LocalScanRadius * LocalScanRadius;

            if (MapUiState.ShowTraders)
            {
                IReadOnlyList<TraderScript> traders = MapObjectRegistry.Traders;
                for (int i = 0; i < traders.Count; i++)
                {
                    TraderScript trader = traders[i];
                    if (trader == null)
                        continue;

                    Vector2 offset = (Vector2)trader.transform.position - center;
                    if (offset.sqrMagnitude <= radiusSq)
                        AddLocalMarker(trader, "$", new Color(0.35f, 1f, 0.58f, 1f), MapMarkerKind.Trader);
                }
            }

            if (MapUiState.ShowThornbackElder)
            {
                IReadOnlyList<ElderThornbackBehaviour> elderThornbacks = MapObjectRegistry.ElderThornbacks;
                for (int i = 0; i < elderThornbacks.Count; i++)
                {
                    ElderThornbackBehaviour elderThornback = elderThornbacks[i];
                    if (elderThornback == null)
                        continue;

                    Vector2 offset = (Vector2)elderThornback.transform.position - center;
                    if (offset.sqrMagnitude <= radiusSq)
                        AddLocalMarker(elderThornback, "Boss", new Color(1f, 0.36f, 0.95f, 1f), MapMarkerKind.ThornbackElder);
                }
            }
        }

        private void AddLocalMarker<T>(T item, string label, Color color, MapMarkerKind kind, System.Func<T, bool> skip = null) where T : Component
        {
            if (item == null || !item.gameObject.activeInHierarchy)
                return;

            if (skip != null && skip(item))
                return;

            int id = GetMarkerObjectKey(item, kind);
            if (!localObjectIds.Add(id))
                return;

            markers.Add(new MapMarker(item, label, color, kind));
        }

        private void AddPlants()
        {
            AddMarkers(Object.FindObjectsOfType<BananaPlantSlip>(), string.Empty, new Color(0.62f, 0.88f, 0.38f, 1f), MapMarkerKind.Interactable);
            AddMarkers(Object.FindObjectsOfType<LeadbushScript>(), string.Empty, new Color(0.62f, 0.88f, 0.38f, 1f), MapMarkerKind.Interactable);
            AddMarkers(Object.FindObjectsOfType<CactusScript>(), string.Empty, new Color(0.62f, 0.88f, 0.38f, 1f), MapMarkerKind.Interactable);
            AddMarkers(Object.FindObjectsOfType<DrillPod>(), string.Empty, new Color(0.62f, 0.88f, 0.38f, 1f), MapMarkerKind.Interactable);
        }

        private void AddMerchants()
        {
            AddMarkers(MapObjectRegistry.Traders, "$", new Color(0.35f, 1f, 0.58f, 1f), MapMarkerKind.Trader);
            AddMarkers(Object.FindObjectsOfType<TraderScript>(), "$", new Color(0.35f, 1f, 0.58f, 1f), MapMarkerKind.Trader);
        }

        private void AddMarkers<T>(T[] objects, string label, Color color, MapMarkerKind kind, System.Func<T, bool> skip = null) where T : Component
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Length; i++)
            {
                T item = objects[i];
                if (item == null || !item.gameObject.activeInHierarchy)
                    continue;

                if (skip != null && skip(item))
                    continue;

                int id = GetMarkerObjectKey(item, kind);
                if (!pendingObjectIds.Add(id))
                    continue;

                pendingMarkers.Add(new MapMarker(item, label, color, kind));
            }
        }

        private void AddMarkers<T>(IReadOnlyList<T> objects, string label, Color color, MapMarkerKind kind, System.Func<T, bool> skip = null) where T : Component
        {
            AddMarkers(pendingMarkers, pendingObjectIds, objects, label, color, kind, skip);
        }

        private static void AddMarkers<T>(List<MapMarker> target, HashSet<int> objectIds, IReadOnlyList<T> objects, string label, Color color, MapMarkerKind kind, System.Func<T, bool> skip = null) where T : Component
        {
            if (objects == null)
                return;

            for (int i = 0; i < objects.Count; i++)
            {
                T item = objects[i];
                if (item == null || !item.gameObject.activeInHierarchy)
                    continue;

                if (skip != null && skip(item))
                    continue;

                int id = GetMarkerObjectKey(item, kind);
                if (!objectIds.Add(id))
                    continue;

                target.Add(new MapMarker(item, label, color, kind));
            }
        }

        private static int GetMarkerObjectId(Component item)
        {
            BuildingEntity parentBuilding = item.GetComponentInParent<BuildingEntity>();
            return parentBuilding == null ? item.gameObject.GetInstanceID() : parentBuilding.gameObject.GetInstanceID();
        }

        private static int GetMarkerObjectKey(Component item, MapMarkerKind kind)
        {
            unchecked
            {
                return (GetMarkerObjectId(item) * 397) ^ (int)kind;
            }
        }

        private static bool ShouldSkipInteractable(UsableObject item)
        {
            GameObject gameObject = item.gameObject;
            Item sceneItem = gameObject.GetComponent<Item>();
            return IsSceneItemDrop(sceneItem)
                || HasTrader(item)
                || gameObject.GetComponent<MineScript>() != null
                || gameObject.GetComponent<GunmineScript>() != null
                || gameObject.GetComponent<TurretScript>() != null
                || gameObject.GetComponent<BearTrap>() != null
                || gameObject.GetComponent<CoilScript>() != null
                || gameObject.GetComponent<SpikeStabberScript>() != null
                || gameObject.GetComponent<SoundCannon>() != null
                || gameObject.GetComponent<JumpPadScript>() != null
                || gameObject.GetComponent<FreshItemDrop>() != null
                || gameObject.GetComponent<CrystalEnemy>() != null;
        }

        private static bool ShouldSkipInteractableBuilding(BuildingEntity entity)
        {
            return entity == null || entity.animal || HasTrader(entity) || IsHazardBuilding(entity) || !IsTrackedBuilding(entity);
        }

        private static bool ShouldSkipHazardBuilding(BuildingEntity entity)
        {
            return entity == null || entity.animal || !IsHazardBuilding(entity);
        }

        private static bool ShouldSkipEnemyBuilding(BuildingEntity entity)
        {
            return entity == null || !entity.animal || entity.cantHit || IsThornbackElder(entity);
        }

        private static bool ShouldSkipEnemy(CrystalEnemy enemy)
        {
            return IsThornbackElder(enemy);
        }

        private static bool ShouldSkipDroppedItem(Item item)
        {
            return !IsSceneItemDrop(item);
        }

        private static bool HasTrader(Component item)
        {
            return item != null
                && (item.GetComponent<TraderScript>() != null
                    || item.GetComponentInParent<TraderScript>() != null
                    || item.GetComponentInChildren<TraderScript>() != null);
        }

        private static bool IsThornbackElder(Component item)
        {
            if (item == null)
                return false;

            BuildingEntity entity = item as BuildingEntity;
            return item.GetComponent<ElderThornbackBehaviour>() != null
                || item.GetComponentInParent<ElderThornbackBehaviour>() != null
                || item.GetComponentInChildren<ElderThornbackBehaviour>() != null
                || ContainsTerm(entity?.id, ThornbackElderTerms)
                || ContainsTerm(entity?.fullName, ThornbackElderTerms)
                || ContainsTerm(item.gameObject.name, ThornbackElderTerms);
        }

        private static bool IsSceneItemDrop(Item item)
        {
            if (item == null || !item.gameObject.activeInHierarchy)
                return false;

            if (item.container != null)
                return false;

            if (item.GetComponentInParent<InventorySlot>() != null)
                return false;

            return item.GetComponentInParent<Body>() == null;
        }

        private static bool IsTrackedBuilding(BuildingEntity entity)
        {
            return ContainsTrackedTerm(entity.id)
                || ContainsTrackedTerm(entity.fullName)
                || ContainsTrackedTerm(entity.gameObject.name);
        }

        private static bool IsHazardBuilding(BuildingEntity entity)
        {
            return ContainsTerm(entity.id, HazardBuildingTerms)
                || ContainsTerm(entity.fullName, HazardBuildingTerms)
                || ContainsTerm(entity.gameObject.name, HazardBuildingTerms);
        }

        private static bool ContainsTrackedTerm(string value)
        {
            return ContainsTerm(value, TrackedBuildingTerms);
        }

        private static bool ContainsTerm(string value, string[] terms)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            string normalized = value.ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
            for (int i = 0; i < terms.Length; i++)
            {
                if (normalized.Contains(terms[i]))
                    return true;
            }

            return false;
        }
    }

    internal readonly struct MapMarker
    {
        public readonly Component Target;
        public readonly string Label;
        public readonly Color Color;
        public readonly MapMarkerKind Kind;

        public Vector2 WorldPosition => Target == null ? Vector2.zero : (Vector2)Target.transform.position;

        public MapMarker(Component target, string label, Color color, MapMarkerKind kind)
        {
            Target = target;
            Label = label;
            Color = color;
            Kind = kind;
        }
    }

    internal enum MapMarkerKind
    {
        Hazard,
        Interactable,
        Enemy,
        ItemDrop,
        Trader,
        ThornbackElder
    }
}
