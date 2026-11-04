using System.Collections.Generic;
using UnityEngine;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 背包/快捷栏物品图标缓存：加载烘焙好的 Resources/ItemIcons 贴图并生成 Sprite。
    /// </summary>
    public static class ItemIconLibrary
    {
        private static readonly Dictionary<string, Sprite> Cache =
            new Dictionary<string, Sprite>();

        public static Sprite Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return null;
            }

            Sprite cached;
            if (Cache.TryGetValue(itemId, out cached))
            {
                return cached;
            }

            Texture2D texture = Resources.Load<Texture2D>("ItemIcons/" + itemId);
            Sprite sprite = null;
            if (texture != null)
            {
                sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sprite.name = "Icon_" + itemId;
            }

            Cache[itemId] = sprite;
            return sprite;
        }
    }
}
