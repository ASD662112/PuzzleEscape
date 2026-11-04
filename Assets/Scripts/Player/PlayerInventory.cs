using System;
using System.Collections.Generic;
using PuzzleEscape.Data;
using UnityEngine;

namespace PuzzleEscape.Player
{
    /// <summary>
    /// 32 格物品栏（4 × 8）。固定槽位，允许空格，方便拖动物品重新排序。
    /// 前 8 格同时显示在 HUD 快捷物品栏上。
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public const int Capacity = 32;

        private readonly ItemDefinition[] slots = new ItemDefinition[Capacity];

        public event Action Changed;

        /// <summary>两个槽位互换时触发（from, to），用于同步蜡烛燃烧计时等运行时状态。</summary>
        public event Action<int, int> Swapped;

        /// <summary>某个槽位被清空时触发。</summary>
        public event Action<int> SlotCleared;

        public IReadOnlyList<ItemDefinition> Slots => slots;

        public int Count
        {
            get
            {
                int count = 0;
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public ItemDefinition ItemAt(int index)
        {
            if (index < 0 || index >= slots.Length)
            {
                return null;
            }

            return slots[index];
        }

        public bool Contains(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Id == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        public bool Add(string itemId)
        {
            ItemDefinition definition = ItemDatabase.Get(itemId);
            if (definition == null)
            {
                Debug.LogWarning("[Inventory] 未找到物品定义：" + itemId);
                return false;
            }

            if (definition.Unique && Contains(definition.Id))
            {
                return false;
            }

            int index = FindEmptyIndex();
            if (index < 0)
            {
                return false;
            }

            slots[index] = definition;
            Changed?.Invoke();
            return true;
        }

        public bool Remove(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].Id == itemId)
                {
                    return RemoveAt(i);
                }
            }

            return false;
        }

        public bool RemoveAt(int index)
        {
            if (index < 0 || index >= slots.Length || slots[index] == null)
            {
                return false;
            }

            slots[index] = null;
            SlotCleared?.Invoke(index);
            Changed?.Invoke();
            return true;
        }

        /// <summary>交换两个槽位的内容；空格也可以作为目标槽位。</summary>
        public void Swap(int from, int to)
        {
            if (from < 0 || from >= slots.Length || to < 0 || to >= slots.Length || from == to)
            {
                return;
            }

            ItemDefinition temp = slots[from];
            slots[from] = slots[to];
            slots[to] = temp;

            Swapped?.Invoke(from, to);
            Changed?.Invoke();
        }

        /// <summary>读取存档时重建物品栏；按存档顺序依次放入靠前的空位。</summary>
        public void RestoreFromIds(IList<string> itemIds)
        {
            Array.Clear(slots, 0, slots.Length);

            if (itemIds != null)
            {
                for (int i = 0; i < itemIds.Count; i++)
                {
                    ItemDefinition definition = ItemDatabase.Get(itemIds[i]);
                    if (definition == null)
                    {
                        continue;
                    }

                    if (definition.Unique && Contains(definition.Id))
                    {
                        continue;
                    }

                    int index = FindEmptyIndex();
                    if (index < 0)
                    {
                        break;
                    }

                    slots[index] = definition;
                }
            }

            Changed?.Invoke();
        }

        public string DisplayList
        {
            get
            {
                var names = new List<string>(Capacity);
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null)
                    {
                        names.Add(slots[i].DisplayName);
                    }
                }

                return names.Count == 0 ? "无" : string.Join("、", names.ToArray());
            }
        }

        private int FindEmptyIndex()
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
