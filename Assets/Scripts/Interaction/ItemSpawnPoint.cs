using System.Collections.Generic;
using PuzzleEscape.Data;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 地图上的道具刷新点。每个点位绑定一个唯一道具 ID，默认在关卡生成时刷新。
    /// 需要由解谜解锁的点位可关闭 SpawnOnStart，由机关在合适的时机调用 Spawn()。
    /// </summary>
    public class ItemSpawnPoint : MonoBehaviour
    {
        public string ItemId;
        public bool SpawnOnStart = true;

        public bool HasSpawned { get; private set; }

        /// <summary>最近一次批量刷新中使用真实模型预制体的数量，用于构建自检日志。</summary>
        public static int RealVisualCount { get; private set; }

        public static void ResetRealVisualCount()
        {
            RealVisualCount = 0;
        }

        /// <summary>读取存档时按保存状态重建刷新点。</summary>
        public void RestoreState(string itemId, bool hasSpawned, bool active, bool spawnOnStart)
        {
            ItemId = itemId;
            SpawnOnStart = spawnOnStart;

            if (!hasSpawned)
            {
                if (spawnOnStart)
                {
                    Spawn();
                }

                return;
            }

            if (active)
            {
                Spawn();
            }
            else
            {
                // 已被拾取：保持“已刷新”状态，避免容器或机关再次生成同一件唯一道具。
                HasSpawned = true;
            }
        }

        public PickupItem Spawn()
        {
            if (HasSpawned)
            {
                return null;
            }
            ItemDefinition definition = ItemDatabase.Get(ItemId);
            if (definition == null)
            {
                Debug.LogWarning("[ItemSpawnPoint] 未找到物品定义：" + ItemId);
                return null;
            }

            if (definition.Unique && !ItemSpawnDirector.TryReserve(ItemId))
            {
                Debug.LogWarning("[ItemSpawnPoint] 物品已存在，忽略重复刷新：" + ItemId);
                return null;
            }

            HasSpawned = true;

            var root = new GameObject("Item_" + ItemId);
            root.transform.SetParent(transform, false);
            root.transform.localPosition = Vector3.zero;

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.34f, 0.4f, 0.34f);
            collider.center = new Vector3(0f, 0.1f, 0f);

            var pickup = root.AddComponent<PickupItem>();
            pickup.ItemId = ItemId;
            pickup.DisplayName = definition.DisplayName;

            GameObject visual = CreateVisual(root.transform, definition);
            FitCollider(root.transform, collider, visual);
            return pickup;
        }

        /// <summary>
        /// 优先使用编辑器烘焙好的真实物品预制体（Assets/Resources/Items），
        /// 缺少资源时回退到原来的程序化占位几何。
        /// </summary>
        public static GameObject CreateVisual(Transform parent, ItemDefinition definition)
        {
            GameObject prefab = Resources.Load<GameObject>("Items/" + definition.Id);
            if (prefab != null)
            {
                RealVisualCount++;
                GameObject realVisual = Object.Instantiate(prefab, parent);
                realVisual.name = "Visual";
                realVisual.transform.localPosition = new Vector3(0f, 0.022f, 0f);
                realVisual.transform.localRotation = Quaternion.identity;

                WorldItemSpin spin = realVisual.GetComponent<WorldItemSpin>();
                if (spin == null)
                {
                    spin = realVisual.AddComponent<WorldItemSpin>();
                }

                spin.BobHeight = 0.012f;
                spin.BobSpeed = 1.5f;
                return realVisual;
            }

            PrimitiveType type = PrimitiveType.Cube;
            Vector3 scale = new Vector3(0.18f, 0.18f, 0.18f);
            Quaternion rotation = Quaternion.Euler(45f, 0f, 45f);

            if (definition.Id.StartsWith("seal_"))
            {
                type = PrimitiveType.Sphere;
                scale = new Vector3(0.2f, 0.2f, 0.2f);
                rotation = Quaternion.identity;
            }
            else if (definition.Id.StartsWith("fuse"))
            {
                type = PrimitiveType.Cylinder;
                scale = new Vector3(0.08f, 0.13f, 0.08f);
                rotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else if (definition.Id.EndsWith("_key") || definition.Id == ItemIds.BrassKey)
            {
                scale = new Vector3(0.24f, 0.05f, 0.1f);
                rotation = Quaternion.Euler(0f, 0f, 20f);
            }
            else if (definition.Id == ItemIds.GateCrank)
            {
                type = PrimitiveType.Cube;
                scale = new Vector3(0.3f, 0.05f, 0.05f);
                rotation = Quaternion.Euler(0f, 0f, 45f);
            }
            else if (definition.Id == ItemIds.FireAxe)
            {
                type = PrimitiveType.Cube;
                scale = new Vector3(0.06f, 0.34f, 0.14f);
                rotation = Quaternion.Euler(0f, 0f, 18f);
            }
            else if (definition.Id == ItemIds.Lantern)
            {
                type = PrimitiveType.Cube;
                scale = new Vector3(0.075f, 0.17f, 0.05f);
                rotation = Quaternion.Euler(0f, 0f, 0f);
            }
            else if (definition.Id == ItemIds.ElevatorGear)
            {
                type = PrimitiveType.Cylinder;
                scale = new Vector3(0.17f, 0.04f, 0.17f);
                rotation = Quaternion.identity;
            }

            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            visual.transform.localScale = scale;
            visual.transform.localRotation = rotation;

            Collider visualCollider = visual.GetComponent<Collider>();
            if (visualCollider != null)
            {
                visualCollider.enabled = false;
                Object.Destroy(visualCollider);
            }

            Shader shader = Shader.Find("Standard");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader != null)
            {
                var material = new Material(shader);
                material.name = "ItemMat_" + definition.Id;
                material.color = definition.Color;

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", definition.Color * 0.55f);
                }

                visual.GetComponent<Renderer>().sharedMaterial = material;
            }

            visual.AddComponent<WorldItemSpin>();
            return visual;
        }

        /// <summary>
        /// 物品根节点的交互碰撞体按真实模型包围盒收缩，避免小件（硬币、钥匙）沿用旧的 34cm 大盒子，
        /// 同时给极小的物件保留最小拾取范围。
        /// </summary>
        private static void FitCollider(Transform root, BoxCollider collider, GameObject visual)
        {
            if (collider == null || visual == null)
            {
                return;
            }

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                worldBounds.Encapsulate(renderers[i].bounds);
            }

            Vector3 localMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 localMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = new Vector3(
                    ((i & 1) == 0 ? worldBounds.min.x : worldBounds.max.x),
                    ((i & 2) == 0 ? worldBounds.min.y : worldBounds.max.y),
                    ((i & 4) == 0 ? worldBounds.min.z : worldBounds.max.z));
                Vector3 local = root.InverseTransformPoint(corner);
                localMin = Vector3.Min(localMin, local);
                localMax = Vector3.Max(localMax, local);
            }

            Vector3 size = localMax - localMin;
            size.x = Mathf.Max(size.x, 0.11f);
            size.y = Mathf.Max(size.y, 0.11f);
            size.z = Mathf.Max(size.z, 0.11f);
            collider.size = size;
            collider.center = (localMin + localMax) * 0.5f;
        }

        /// <summary>
        /// 生成一件掉在地上的物品。withPhysics 为 true 时带刚体，按 launchVelocity 抛出后落地；
        /// 为 false 时直接在指定位置静止生成，用于读档恢复。
        /// </summary>
        public static PickupItem CreateDroppedItem(string itemId, Vector3 position, Vector3 euler,
            Vector3 launchVelocity, bool withPhysics)
        {
            ItemDefinition definition = ItemDatabase.Get(itemId);
            if (definition == null)
            {
                Debug.LogWarning("[ItemSpawnPoint] 未找到物品定义：" + itemId);
                return null;
            }

            var root = new GameObject("Dropped_" + itemId);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(euler));

            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.22f, 0.22f, 0.22f);
            collider.center = new Vector3(0f, 0.11f, 0f);

            GameObject visual = CreateVisual(root.transform, definition);
            FitCollider(root.transform, collider, visual);

            WorldItemSpin spin = visual.GetComponent<WorldItemSpin>();
            if (spin != null && withPhysics)
            {
                spin.enabled = false;
            }

            var pickup = root.AddComponent<PickupItem>();
            pickup.ItemId = itemId;
            pickup.DisplayName = definition.DisplayName;
            pickup.enabled = !withPhysics;

            if (definition.Unique)
            {
                ItemSpawnDirector.TryReserve(itemId);
            }

            if (withPhysics)
            {
                var body = root.AddComponent<Rigidbody>();
                body.mass = 0.35f;
                body.drag = 0.15f;
                body.angularDrag = 0.8f;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.collisionDetectionMode = CollisionDetectionMode.Continuous;
                body.velocity = launchVelocity;
                body.angularVelocity = new Vector3(Random.Range(-6f, 6f), Random.Range(-8f, 8f),
                    Random.Range(-6f, 6f));
            }

            var dropped = root.AddComponent<DroppedItemPickup>();
            dropped.Initialize(pickup, spin);

            if (!withPhysics)
            {
                dropped.MarkSettled();
            }

            return pickup;
        }
    }

    public static class ItemSpawnDirector
    {
        private static readonly HashSet<string> ReservedItemIds = new HashSet<string>();

        public static void ResetRegistry()
        {
            ReservedItemIds.Clear();
        }

        public static bool TryReserve(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && ReservedItemIds.Add(itemId);
        }

        /// <summary>该唯一道具是否已经刷新在世界上（含已被拾取的情况）。</summary>
        public static bool IsReserved(string itemId)
        {
            return !string.IsNullOrEmpty(itemId) && ReservedItemIds.Contains(itemId);
        }

        public static void SpawnAll()
        {
            ItemSpawnPoint.ResetRealVisualCount();
            int spawned = 0;
            ItemSpawnPoint[] points = Object.FindObjectsOfType<ItemSpawnPoint>(true);
            foreach (ItemSpawnPoint point in points)
            {
                if (point.SpawnOnStart && point.Spawn() != null)
                {
                    spawned++;
                }
            }

            Debug.Log("[PuzzleEscape] 物品刷新完成：" + spawned + " 件，其中真实模型 " +
                      ItemSpawnPoint.RealVisualCount + " 件。");
        }

        /// <summary>
        /// 随机挑一个还没打开的抽屉/柜子，把唯一道具登记为它的“必定掉落”，
        /// 玩家打开时才会真正刷新。场景里已经有同一件道具（固定刷新点、已刷新的拾取物，
        /// 或已经分配给别的容器）时不做任何事，以保证全图只存在一份。
        /// </summary>
        public static bool AssignUniqueToRandomContainer(string itemId)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                return false;
            }

            foreach (PickupItem pickup in Object.FindObjectsOfType<PickupItem>(true))
            {
                if (pickup != null && pickup.ItemId == itemId && pickup.gameObject.activeInHierarchy)
                {
                    return false;
                }
            }

            var containers = new List<OpenableContainer>();
            foreach (OpenableContainer container in Object.FindObjectsOfType<OpenableContainer>(true))
            {
                if (container == null)
                {
                    continue;
                }

                if (container.GuaranteedItemId == itemId)
                {
                    return false;   // 本次开局已经分配给某个容器了
                }

                if (container.IsOpened || FindFreeLootSlot(container) == null)
                {
                    continue;
                }

                containers.Add(container);
            }

            if (containers.Count == 0)
            {
                return false;
            }

            OpenableContainer chosen = containers[Random.Range(0, containers.Count)];
            chosen.GuaranteedItemId = itemId;
            return true;
        }

        private static ItemSpawnPoint FindFreeLootSlot(OpenableContainer container)
        {
            if (container.LootSpawnPoints != null)
            {
                foreach (ItemSpawnPoint point in container.LootSpawnPoints)
                {
                    if (point != null && !point.HasSpawned)
                    {
                        return point;
                    }
                }
            }

            if (container.LootSpawnPoint != null && !container.LootSpawnPoint.HasSpawned)
            {
                return container.LootSpawnPoint;
            }

            return null;
        }
    }
}
