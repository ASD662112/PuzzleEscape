#if UNITY_EDITOR
using System.Collections.Generic;
using PuzzleEscape.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace PuzzleEscape.EditorTools
{
    /// <summary>
    /// 摆件整理：把浮空、插进墙体、互相穿模、挡在门口/走廊的 Real_* 摆件
    /// 重新摆到同房间靠墙的空位，保持原朝向，避免房间中间堆满家具。
    /// </summary>
    public static class PropPlacementAudit
    {
        private class PropInfo
        {
            public Transform Root;
            public Bounds Bounds;

            public void Refresh()
            {
                Bounds bounds = new Bounds(Root.position, Vector3.zero);
                bool has = false;
                foreach (Renderer renderer in Root.GetComponentsInChildren<Renderer>(true))
                {
                    if (!has)
                    {
                        bounds = renderer.bounds;
                        has = true;
                    }
                    else
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }
                }

                Bounds = bounds;
            }
        }

        [MenuItem("PuzzleEscape/清理出生点周围摆件", false, 18)]
        public static void ClearSpawnAreaMenu()
        {
            OpenMainScene();
            ClearSpawnArea();
        }

        /// <summary>把出生点 2 m 内的真实模型摆件挪开，避免玩家一进游戏就被家具顶住。</summary>
        public static void ClearSpawnArea()
        {
            GameObject environment = GameObject.Find("Environment");
            if (environment == null)
            {
                Debug.LogWarning("[PuzzleEscape] 场景里没有 Environment。");
                return;
            }

            var spawns = new[]
            {
                new Vector3(-9f, 0.05f, 7.2f),   // 庄园书房
                new Vector3(0f, 0.05f, 5.2f)     // 疗养院大厅
            };

            int moved = 0;
            foreach (PropInfo prop in CollectProps(environment.transform))
            {
                prop.Refresh();

                bool nearSpawn = false;
                foreach (Vector3 spawn in spawns)
                {
                    float distance = new Vector2(prop.Bounds.center.x - spawn.x,
                        prop.Bounds.center.z - spawn.z).magnitude;
                    if (distance < 2.2f)
                    {
                        nearSpawn = true;
                        break;
                    }
                }

                if (!nearSpawn)
                {
                    continue;
                }

                // 以外扩 3 m 的八个方向找第一个不插墙、不带其他摆件的位置。
                for (int step = 0; step < 8; step++)
                {
                    float angle = step * Mathf.PI * 2f / 8f;
                    Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 3f;
                    Vector3 candidate = prop.Root.position + offset;
                    var bounds = new Bounds(
                        new Vector3(candidate.x, prop.Bounds.center.y, candidate.z), prop.Bounds.size);

                    if (HasStructureOverlap(bounds))
                    {
                        continue;
                    }

                    prop.Root.position += offset;
                    prop.Refresh();
                    moved++;
                    Debug.Log("[PuzzleEscape] 出生点摆件已挪开：" + prop.Root.name);
                    break;
                }
            }

            if (moved > 0)
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            Debug.Log("[PuzzleEscape] 出生点清理完成：挪开 " + moved + " 件摆件。");
        }

        [MenuItem("PuzzleEscape/检查摆件位置", false, 16)]
        public static void AuditMenu()
        {
            OpenMainScene();
            Run(false);
        }

        [MenuItem("PuzzleEscape/整理摆件位置", false, 17)]
        public static void FixMenu()
        {
            OpenMainScene();
            Run(true);
        }

        public static void AuditMainScene()
        {
            OpenMainScene();
            Run(false);
        }

        public static void FixMainScene()
        {
            OpenMainScene();
            Run(true);
        }

        private static void OpenMainScene()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Main.unity", OpenSceneMode.Single);
        }

        private static void Run(bool applyFixes)
        {
            GameObject environment = GameObject.Find("Environment");
            if (environment == null)
            {
                Debug.LogWarning("[PuzzleEscape] 场景里没有 Environment，跳过摆件检查。");
                return;
            }

            var props = CollectProps(environment.transform);
            foreach (PropInfo prop in props)
            {
                prop.Refresh();
            }

            var doors = Object.FindObjectsOfType<HingedDoor>(true);
            var problem = new List<PropInfo>();
            int floating = 0;
            int wallClash = 0;
            int overlapping = 0;
            int doorBlocked = 0;

            foreach (PropInfo prop in props)
            {
                bool bad = false;

                if (!IsWallOrCeilingMounted(prop.Root.name))
                {
                    float floorY = FindFloorY(prop);
                    float gap = prop.Bounds.min.y - floorY;
                    if (gap > 0.05f && gap < 0.6f && !HasSupportBelow(prop))
                    {
                        floating++;
                        bad = true;
                    }
                }

                if (TryGetStructurePenetration(prop))
                {
                    wallClash++;
                    bad = true;
                }

                if (IsNearDoor(prop, doors))
                {
                    doorBlocked++;
                    bad = true;
                }

                // 玩家出生点周围 2.2 m 保持空着，否则会一进游戏就被家具顶住。
                if (IsNearPlayerSpawn(prop))
                {
                    bad = true;
                }

                if (bad)
                {
                    problem.Add(prop);
                }
            }

            // 穿模单独统计（互相穿模的摆件都参与重新摆放）。
            for (int i = 0; i < props.Count; i++)
            {
                for (int j = i + 1; j < props.Count; j++)
                {
                    if (!TryGetOverlap(props[i].Bounds, props[j].Bounds))
                    {
                        continue;
                    }

                    overlapping++;
                    if (!problem.Contains(props[i]))
                    {
                        problem.Add(props[i]);
                    }

                    if (!problem.Contains(props[j]))
                    {
                        problem.Add(props[j]);
                    }
                }
            }

            Debug.Log(string.Format(
                "[PuzzleEscape] 摆件检查：共 {0} 件；浮空 {1}、插进结构 {2}、互相穿模 {3}、挡门口 {4}；需整理 {5} 件。",
                props.Count, floating, wallClash, overlapping, doorBlocked, problem.Count));

            if (!applyFixes || problem.Count == 0)
            {
                return;
            }

            int moved = 0;
            var placed = new List<Bounds>();
            foreach (PropInfo prop in props)
            {
                if (!problem.Contains(prop))
                {
                    placed.Add(prop.Bounds);
                }
            }

            foreach (PropInfo prop in problem)
            {
                if (TryRelocate(prop, placed, doors))
                {
                    moved++;
                    placed.Add(prop.Bounds);
                }
                else if (TryPushOut(prop))
                {
                    moved++;
                    placed.Add(prop.Bounds);
                }
            }

            var scene = EditorSceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(string.Format(
                "[PuzzleEscape] 摆件整理完成：重新摆放 {0} / {1} 件，已保存 {2}。",
                moved, problem.Count, scene.path));
        }

        /// <summary>沿房间墙边寻找空位：不挡门、不压其他摆件、不插进墙体。</summary>
        private static bool TryRelocate(PropInfo prop, List<Bounds> placed, HingedDoor[] doors)
        {
            if (!TryGetRoomFloor(prop.Root, out Bounds roomFloor))
            {
                return false;
            }

            Vector3 size = prop.Bounds.size;
            float halfX = Mathf.Max(0.15f, size.x * 0.5f);
            float halfZ = Mathf.Max(0.15f, size.z * 0.5f);
            float halfY = size.y * 0.5f;

            const float wallInset = 0.12f;
            float marginX = halfX + wallInset;
            float marginZ = halfZ + wallInset;

            float minX = roomFloor.min.x + marginX;
            float maxX = roomFloor.max.x - marginX;
            float minZ = roomFloor.min.z + marginZ;
            float maxZ = roomFloor.max.z - marginZ;
            if (minX >= maxX || minZ >= maxZ)
            {
                return false;
            }

            float bestScore = float.MaxValue;
            Vector3 bestPosition = Vector3.zero;
            bool found = false;

            const float step = 0.6f;
            for (float x = minX; x <= maxX + 0.01f; x += step)
            {
                TryCandidate(new Vector3(x, 0f, minZ), prop, roomFloor, placed, doors, halfX, halfZ,
                    ref bestScore, ref bestPosition, ref found);
                TryCandidate(new Vector3(x, 0f, maxZ), prop, roomFloor, placed, doors, halfX, halfZ,
                    ref bestScore, ref bestPosition, ref found);
            }

            for (float z = minZ; z <= maxZ + 0.01f; z += step)
            {
                TryCandidate(new Vector3(minX, 0f, z), prop, roomFloor, placed, doors, halfX, halfZ,
                    ref bestScore, ref bestPosition, ref found);
                TryCandidate(new Vector3(maxX, 0f, z), prop, roomFloor, placed, doors, halfX, halfZ,
                    ref bestScore, ref bestPosition, ref found);
            }

            if (!found)
            {
                return false;
            }

            float floorY = roomFloor.max.y;
            prop.Root.position = new Vector3(bestPosition.x, floorY, bestPosition.z);
            prop.Refresh();
            return true;
        }

        private static void TryCandidate(Vector3 point, PropInfo prop, Bounds roomFloor, List<Bounds> placed,
            HingedDoor[] doors, float halfX, float halfZ,
            ref float bestScore, ref Vector3 bestPosition, ref bool found)
        {
            Vector3 size = prop.Bounds.size;
            Vector3 center = new Vector3(point.x, roomFloor.max.y + size.y * 0.5f, point.z);
            var candidate = new Bounds(center, new Vector3(size.x, size.y, size.z));

            foreach (HingedDoor door in doors)
            {
                Vector3 delta = door.transform.position - center;
                delta.y = 0f;
                if (delta.sqrMagnitude < 1.8f * 1.8f)
                {
                    return;
                }
            }

            foreach (Bounds other in placed)
            {
                if (candidate.Intersects(other))
                {
                    return;
                }
            }

            if (HasStructureOverlap(candidate))
            {
                return;
            }

            // 分数：离原位置越近越好，贴墙的候选天然在边上。
            float score = (new Vector2(point.x - prop.Bounds.center.x, point.z - prop.Bounds.center.z)).sqrMagnitude;
            if (score < bestScore)
            {
                bestScore = score;
                bestPosition = point;
                found = true;
            }
        }

        private static bool TryPushOut(PropInfo prop)
        {
            if (!TryGetStructurePenetrationDirection(prop, out Vector3 push))
            {
                return false;
            }

            prop.Root.position += push;
            prop.Refresh();
            return true;
        }

        private static bool TryGetRoomFloor(Transform prop, out Bounds floorBounds)
        {
            floorBounds = new Bounds(prop.position, Vector3.zero);
            Transform parent = prop.parent;
            if (parent == null)
            {
                return false;
            }

            bool found = false;
            float bestVolume = 0f;
            foreach (Collider collider in parent.GetComponentsInChildren<Collider>(true))
            {
                if (collider == null || !collider.name.Contains("Floor"))
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                float volume = bounds.size.x * bounds.size.z;
                if (volume > bestVolume)
                {
                    bestVolume = volume;
                    floorBounds = bounds;
                    found = true;
                }
            }

            return found && floorBounds.size.x > 0.5f && floorBounds.size.z > 0.5f;
        }

        private static List<PropInfo> CollectProps(Transform environment)
        {
            var props = new List<PropInfo>();
            foreach (Transform t in environment.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Real_"))
                {
                    continue;
                }

                if (t.GetComponentInChildren<Renderer>(true) == null)
                {
                    continue;
                }

                if (t.GetComponentInChildren<ItemSpawnPoint>(true) != null ||
                    t.GetComponentInChildren<OpenableContainer>(true) != null)
                {
                    continue;
                }

                props.Add(new PropInfo { Root = t });
            }

            return props;
        }

        private static float FindFloorY(PropInfo prop)
        {
            Vector3 origin = new Vector3(prop.Bounds.center.x, prop.Bounds.min.y + 0.05f, prop.Bounds.center.z);
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float best = prop.Bounds.min.y;
            float bestDistance = float.MaxValue;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(prop.Root))
                {
                    continue;
                }

                if (hit.distance < bestDistance)
                {
                    bestDistance = hit.distance;
                    best = hit.point.y;
                }
            }

            return best;
        }

        private static bool IsNearPlayerSpawn(PropInfo prop)
        {
            // 与 GameMapCatalog 里庄园/疗养院的出生点保持一致。
            Vector3 mansion = new Vector3(-9f, 0.05f, 7.2f);
            Vector3 sanatorium = new Vector3(0f, 0.05f, 5.2f);

            foreach (Vector3 spawn in new[] { mansion, sanatorium })
            {
                Vector2 a = new Vector2(prop.Bounds.center.x, prop.Bounds.center.z);
                Vector2 b = new Vector2(spawn.x, spawn.z);
                if (a.x - prop.Bounds.extents.x < b.x + 2.2f &&
                    a.x + prop.Bounds.extents.x > b.x - 2.2f &&
                    a.y - prop.Bounds.extents.z < b.y + 2.2f &&
                    a.y + prop.Bounds.extents.z > b.y - 2.2f)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNearDoor(PropInfo prop, HingedDoor[] doors)
        {
            foreach (HingedDoor door in doors)
            {
                Vector3 doorCenter = door.transform.position + Vector3.up * 1.1f;
                var doorBounds = new Bounds(doorCenter, new Vector3(2.2f, 2.2f, 2.2f));
                if (prop.Bounds.Intersects(doorBounds))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetStructurePenetration(PropInfo prop)
        {
            return TryGetStructurePenetrationDirection(prop, out _);
        }

        private static bool TryGetStructurePenetrationDirection(PropInfo prop, out Vector3 push)
        {
            push = Vector3.zero;
            Vector3 half = prop.Bounds.extents * 0.9f;
            Collider[] hits = Physics.OverlapBox(prop.Bounds.center, half, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            float bestDepth = 0f;
            Vector3 bestPush = Vector3.zero;

            foreach (Collider hit in hits)
            {
                if (hit == null || hit.isTrigger || hit.transform.IsChildOf(prop.Root) ||
                    prop.Root.IsChildOf(hit.transform))
                {
                    continue;
                }

                if (!IsStructureName(hit.gameObject.name))
                {
                    continue;
                }

                Bounds wall = hit.bounds;
                float penX = Mathf.Min(prop.Bounds.max.x, wall.max.x) - Mathf.Max(prop.Bounds.min.x, wall.min.x);
                float penZ = Mathf.Min(prop.Bounds.max.z, wall.max.z) - Mathf.Max(prop.Bounds.min.z, wall.min.z);
                if (penX <= 0f || penZ <= 0f)
                {
                    continue;
                }

                float depth = Mathf.Min(penX, penZ);
                if (depth <= bestDepth)
                {
                    continue;
                }

                bestDepth = depth;
                bool pushX = penX < penZ;
                float direction = pushX
                    ? Mathf.Sign(prop.Bounds.center.x - wall.center.x)
                    : Mathf.Sign(prop.Bounds.center.z - wall.center.z);
                if (direction == 0f)
                {
                    direction = 1f;
                }

                bestPush = pushX
                    ? new Vector3(direction * (depth + 0.06f), 0f, 0f)
                    : new Vector3(0f, 0f, direction * (depth + 0.06f));
            }

            push = bestPush;
            return bestDepth > 0.02f;
        }

        private static bool HasStructureOverlap(Bounds bounds)
        {
            Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents * 0.9f, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (hit != null && !hit.isTrigger && IsStructureName(hit.gameObject.name))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsStructureName(string name)
        {
            return name.Contains("Wall") || name.Contains("Floor") || name.Contains("Ceiling") ||
                   name.Contains("Column") || name.Contains("Beam") || name.Contains("Trim") ||
                   name.Contains("Stair") || name.Contains("Skirting");
        }

        private static bool TryGetOverlap(Bounds a, Bounds b)
        {
            Vector3 min = Vector3.Max(a.min, b.min);
            Vector3 max = Vector3.Min(a.max, b.max);
            Vector3 size = max - min;
            if (size.x <= 0.02f || size.y <= 0.02f || size.z <= 0.02f)
            {
                return false;
            }

            float minHeight = Mathf.Min(a.size.y, b.size.y);
            if (minHeight > 0.001f && size.y / minHeight < 0.5f)
            {
                return false;
            }

            float volumeA = a.size.x * a.size.y * a.size.z;
            float volumeB = b.size.x * b.size.y * b.size.z;
            float smaller = Mathf.Min(volumeA, volumeB);
            float larger = Mathf.Max(volumeA, volumeB);
            if (larger > smaller * 3f)
            {
                return false;
            }

            float overlapVolume = size.x * size.y * size.z;
            return overlapVolume >= 0.01f && overlapVolume >= smaller * 0.12f;
        }

        private static bool IsWallOrCeilingMounted(string name)
        {
            return name.Contains("Lantern") || name.Contains("Sconce") ||
                   name.Contains("Mirror") || name.Contains("Chandelier") ||
                   name.Contains("Painting") || name.Contains("Curtain");
        }

        private static bool HasSupportBelow(PropInfo prop)
        {
            Vector3 center = prop.Bounds.center;
            center.y = prop.Bounds.min.y - 0.04f;
            Vector3 half = new Vector3(
                Mathf.Max(0.05f, prop.Bounds.size.x * 0.4f), 0.04f,
                Mathf.Max(0.05f, prop.Bounds.size.z * 0.4f));

            Collider[] hits = Physics.OverlapBox(center, half, Quaternion.identity,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (hit != null && !hit.isTrigger && !hit.transform.IsChildOf(prop.Root))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
#endif
