using System.Collections.Generic;
using UnityEngine;

namespace MistalCrysMap.Runtime
{
    internal static class MapSpriteCache
    {
        private const int MaxCachedSprites = 4096;
        private static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

        public static Sprite GetSprite(Component target)
        {
            if (target == null)
                return null;

            int id = target.gameObject.GetInstanceID();
            if (sprites.TryGetValue(id, out Sprite cached) && cached != null)
                return cached;

            Sprite sprite = ResolveSprite(target);
            if (sprite == null)
                return null;

            if (sprites.Count > MaxCachedSprites)
                sprites.Clear();

            sprites[id] = sprite;
            return sprite;
        }

        private static Sprite ResolveSprite(Component target)
        {
            SpriteRenderer rootRenderer = target.GetComponent<SpriteRenderer>();
            if (rootRenderer != null && rootRenderer.sprite != null)
                return rootRenderer.sprite;

            SpriteRenderer parentRenderer = target.GetComponentInParent<SpriteRenderer>();
            if (parentRenderer != null && parentRenderer.sprite != null)
                return parentRenderer.sprite;

            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>();
            Sprite bestSprite = null;
            float bestArea = -1f;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null || renderer.sprite == null)
                    continue;

                Vector3 size = renderer.bounds.size;
                float area = size.x * size.y;
                if (area <= bestArea)
                    continue;

                bestArea = area;
                bestSprite = renderer.sprite;
            }

            return bestSprite;
        }
    }
}
