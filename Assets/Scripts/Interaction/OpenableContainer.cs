using System.Collections;
using System.Collections.Generic;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 可打开的抽屉/柜门。第一次打开时从配置的掉落池中随机刷新一件道具。
    /// 唯一道具由 ItemSpawnDirector 保证全图只有一件，普通杂物可以重复。
    /// </summary>
    public class OpenableContainer : Interactable
    {
        public enum MotionKind
        {
            Drawer,
            Cabinet
        }

        public string DisplayName = "抽屉";
        public MotionKind Motion = MotionKind.Drawer;
        public Transform MovingPart;
        public Vector3 SlideOffset = new Vector3(0f, 0f, 0.38f);
        public Vector3 SwingEuler = new Vector3(0f, -105f, 0f);
        public float Duration = 0.45f;

        [Tooltip("抽屉/柜子内部的道具点位（可多个，按顺序摆放道具）。")]
        public ItemSpawnPoint[] LootSpawnPoints;

        [Tooltip("兼容旧配置：只有一个点位时使用。")]
        public ItemSpawnPoint LootSpawnPoint;

        [Tooltip("打开后最多在里面放几件道具。")]
        public int MaxLootItems = 2;

        public string[] LootItemIds;

        [Tooltip("打开时必定先刷新的道具 Id；用于把手电筒之类唯一道具随机藏进一个容器。")]
        public string GuaranteedItemId;

        [Tooltip("需要持有的道具 Id；留空表示普通可打开的柜子。")]
        public string RequiredItemId;

        public string RequiredItemName = "钥匙";

        private bool opened;
        private bool animating;

        public override string Prompt => opened ? DisplayName + "（已打开）" : "打开" + DisplayName;
        public override bool CanInteract => !opened && !animating;
        public bool IsOpened => opened;

        public override void Interact(PlayerInteractor interactor)
        {
            if (opened || animating)
            {
                return;
            }

            if (!string.IsNullOrEmpty(RequiredItemId))
            {
                PlayerInventory inventory = interactor != null
                    ? interactor.GetComponent<PlayerInventory>()
                    : null;

                if (inventory == null || !inventory.Contains(RequiredItemId))
                {
                    GameManager.Instance?.Toast(DisplayName + "锁着，需要：" + RequiredItemName);
                    AudioManager.Instance?.PlayError();
                    return;
                }
            }

            opened = true;

            // 柜体根碰撞体会把射线挡在柜内的掉落物前面；打开后关闭它，
            // 让玩家可以直接拾取柜内的钥匙/收藏品（与上锁抽屉的处理一致）。
            Collider containerCollider = GetComponent<Collider>();
            if (containerCollider != null)
            {
                containerCollider.enabled = false;
            }

            AudioManager.Instance?.PlayDoor();
            StartCoroutine(OpenRoutine());
        }

        /// <summary>读取存档时直接恢复打开状态，不播动画，也不重新随机战利品。</summary>
        public void RestoreOpened(bool value)
        {
            opened = value;
            animating = false;

            if (!opened)
            {
                return;
            }

            Collider containerCollider = GetComponent<Collider>();
            if (containerCollider != null)
            {
                containerCollider.enabled = false;
            }

            if (MovingPart == null)
            {
                return;
            }

            if (Motion == MotionKind.Drawer)
            {
                MovingPart.localPosition += SlideOffset;
            }
            else
            {
                MovingPart.localRotation *= Quaternion.Euler(SwingEuler);
            }
        }

        private IEnumerator OpenRoutine()
        {
            animating = true;

            if (MovingPart != null)
            {
                Vector3 startPosition = MovingPart.localPosition;
                Quaternion startRotation = MovingPart.localRotation;
                Vector3 endPosition = startPosition +
                    (Motion == MotionKind.Drawer ? SlideOffset : Vector3.zero);
                Quaternion endRotation = startRotation *
                    (Motion == MotionKind.Cabinet ? Quaternion.Euler(SwingEuler) : Quaternion.identity);
                float duration = Mathf.Max(0.05f, Duration);

                for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    MovingPart.localPosition = Vector3.Lerp(startPosition, endPosition, t);
                    MovingPart.localRotation = Quaternion.Slerp(startRotation, endRotation, t);
                    yield return null;
                }

                MovingPart.localPosition = endPosition;
                MovingPart.localRotation = endRotation;
            }

            animating = false;
            SpawnRandomLoot();
        }

        /// <summary>
        /// 打开后把道具摆进内部各个点位：每个点位放一件，
        /// 最多 MaxLootItems 件；唯一道具已在别处生成时会自动换下一件。
        /// </summary>
        private void SpawnRandomLoot()
        {
            List<ItemSpawnPoint> slots = CollectSlots();

            if (slots.Count == 0)
            {
                return;
            }

            int wanted = Mathf.Clamp(MaxLootItems, 1, slots.Count);
            var used = new HashSet<string>();
            var found = new List<string>();
            int firstSlot = 0;

            // 必定出现的道具（例如随机藏在某个柜子里的手电筒）先占住第一个点位。
            if (!string.IsNullOrEmpty(GuaranteedItemId))
            {
                ItemSpawnPoint point = slots[0];
                point.ItemId = GuaranteedItemId;
                PickupItem guaranteed = point.Spawn();

                if (guaranteed != null)
                {
                    used.Add(GuaranteedItemId);
                    found.Add(ResolveName(GuaranteedItemId));
                    firstSlot = 1;
                }
            }

            List<string> pool = BuildLootPool();
            PlayerInventory inventory = FindObjectOfType<PlayerInventory>();

            for (int slotIndex = firstSlot; slotIndex < wanted; slotIndex++)
            {
                ItemSpawnPoint point = slots[slotIndex];

                foreach (string itemId in pool)
                {
                    if (used.Contains(itemId))
                    {
                        continue;
                    }

                    ItemDefinition definition = ItemDatabase.Get(itemId);
                    if (definition == null)
                    {
                        continue;
                    }

                    // 唯一道具已经刷新过或已经在玩家身上时跳过，避免读档后重复出现。
                    if (definition.Unique &&
                        (ItemSpawnDirector.IsReserved(itemId) ||
                         (inventory != null && inventory.Contains(itemId))))
                    {
                        continue;
                    }

                    point.ItemId = itemId;
                    PickupItem pickup = point.Spawn();

                    if (pickup == null)
                    {
                        continue;
                    }

                    used.Add(itemId);
                    found.Add(definition.DisplayName);
                    break;
                }
            }

            if (found.Count > 0)
            {
                GameManager.Instance?.Toast(DisplayName + "里找到了 · " +
                                            string.Join("、", found.ToArray()));
            }
            else
            {
                GameManager.Instance?.Toast(DisplayName + "里只剩下一层灰。");
            }
        }

        private static string ResolveName(string itemId)
        {
            ItemDefinition definition = ItemDatabase.Get(itemId);
            return definition != null ? definition.DisplayName : itemId;
        }

        // 通用杂物：不依赖场景重新烘焙，运行时混入所有普通/稀有掉落池，
        // 避免翻来覆去只出硬币、空酒瓶。
        private static readonly string[] ExtraJunkLoot =
        {
            ItemIds.Battery, ItemIds.Bandage, ItemIds.PillBottle,
            ItemIds.Newspaper, ItemIds.Matchbox, ItemIds.Soap
        };

        /// <summary>
        /// 构建本次开箱的掉落池：原有配置 + 通用杂物，并打乱顺序逐个尝试。
        /// 只配置了一件道具的容器（钥匙、通行卡等主线掉落）保持原样，不混入杂物。
        /// </summary>
        private List<string> BuildLootPool()
        {
            var pool = new List<string>();

            if (LootItemIds != null)
            {
                pool.AddRange(LootItemIds);
            }

            if (pool.Count > 1)
            {
                foreach (string junk in ExtraJunkLoot)
                {
                    if (!pool.Contains(junk))
                    {
                        pool.Add(junk);
                    }
                }
            }

            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                string temp = pool[i];
                pool[i] = pool[j];
                pool[j] = temp;
            }

            return pool;
        }

        private List<ItemSpawnPoint> CollectSlots()
        {
            var slots = new List<ItemSpawnPoint>();

            if (LootSpawnPoints != null)
            {
                foreach (ItemSpawnPoint point in LootSpawnPoints)
                {
                    if (point != null)
                    {
                        slots.Add(point);
                    }
                }
            }

            if (slots.Count == 0 && LootSpawnPoint != null)
            {
                slots.Add(LootSpawnPoint);
            }

            return slots;
        }

        /// <summary>抽屉/柜子里还剩几件没被拿走的道具。</summary>
        public int RemainingLoot
        {
            get
            {
                int count = 0;

                foreach (ItemSpawnPoint point in CollectSlots())
                {
                    foreach (PickupItem pickup in point.GetComponentsInChildren<PickupItem>(true))
                    {
                        if (pickup != null && pickup.gameObject.activeSelf)
                        {
                            count++;
                        }
                    }
                }

                return count;
            }
        }
    }
}
