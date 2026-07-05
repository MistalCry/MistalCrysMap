using System.Collections.Generic;
using UnityEngine;

namespace MistalCrysMap.Runtime
{
    /// <summary>
    /// 记录会频繁移动或数量较多的地图对象，避免运行时反复全场 FindObjectsOfType。
    /// </summary>
    internal static class MapObjectRegistry
    {
        private static readonly List<BuildingEntity> buildings = new List<BuildingEntity>();
        private static readonly List<CrystalEnemy> crystalEnemies = new List<CrystalEnemy>();
        private static readonly List<TraderScript> traders = new List<TraderScript>();
        private static readonly List<ElderThornbackBehaviour> elderThornbacks = new List<ElderThornbackBehaviour>();

        public static IReadOnlyList<BuildingEntity> Buildings => buildings;
        public static IReadOnlyList<CrystalEnemy> CrystalEnemies => crystalEnemies;
        public static IReadOnlyList<TraderScript> Traders => traders;
        public static IReadOnlyList<ElderThornbackBehaviour> ElderThornbacks => elderThornbacks;

        public static void Register(BuildingEntity entity)
        {
            if (entity == null || buildings.Contains(entity))
                return;

            buildings.Add(entity);
        }

        public static void Register(CrystalEnemy enemy)
        {
            if (enemy == null || crystalEnemies.Contains(enemy))
                return;

            crystalEnemies.Add(enemy);
        }

        public static void Register(TraderScript trader)
        {
            if (trader == null || traders.Contains(trader))
                return;

            traders.Add(trader);
        }

        public static void Register(ElderThornbackBehaviour elderThornback)
        {
            if (elderThornback == null || elderThornbacks.Contains(elderThornback))
                return;

            elderThornbacks.Add(elderThornback);
        }

        public static void CleanupDestroyedReferences()
        {
            Cleanup(buildings);
            Cleanup(crystalEnemies);
            Cleanup(traders);
            Cleanup(elderThornbacks);
        }

        private static void Cleanup<T>(List<T> items) where T : Object
        {
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i] == null)
                    items.RemoveAt(i);
            }
        }
    }
}
