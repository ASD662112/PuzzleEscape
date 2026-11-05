using System.Collections;
using System.Collections.Generic;
using PuzzleEscape.Interaction;
using UnityEngine;

namespace PuzzleEscape.AI
{
    /// <summary>
    /// 怪物用的地面网格导航：在庄园一层按 1m 采样可站立格子，
    /// 门扇所在格子视为「可通行 + 需要开门」，用于巡逻随机寻路与追逐时的最短路。
    /// </summary>
    public class MonsterNavigationGrid
    {
        private const float CellSize = 1f;

        // 采样半径要和怪物 CharacterController 的半径接近（0.35），
        // 太大（0.42+）会把 1.8 m 的门洞挤成不可通行，房间之间断开后怪物只能原地站着。
        private const float CapsuleRadius = 0.38f;
        private const float CapsuleBottom = 0.35f;
        private const float CapsuleTop = 1.6f;

        private Vector3 origin;
        private int width;
        private int depth;
        private bool[] walkable;
        private static readonly Collider[] OverlapBuffer = new Collider[32];

        public bool Ready { get; private set; }
        public int WalkableCount { get; private set; }

        public static MonsterNavigationGrid Create(Bounds area)
        {
            var grid = new MonsterNavigationGrid
            {
                origin = new Vector3(area.min.x, area.min.y, area.min.z),
                width = Mathf.CeilToInt(area.size.x / CellSize),
                depth = Mathf.CeilToInt(area.size.z / CellSize)
            };

            grid.walkable = new bool[Mathf.Max(1, grid.width * grid.depth)];
            return grid;
        }

        /// <summary>分帧构建，避免一次性几千次胶囊检测造成卡顿。</summary>
        public IEnumerator BuildAsync(int cellsPerFrame = 120)
        {
            Vector3 bottomOffset = Vector3.up * CapsuleBottom;
            Vector3 topOffset = Vector3.up * CapsuleTop;
            int perFrame = Mathf.Max(1, cellsPerFrame);

            for (int z = 0; z < depth; z++)
            {
                for (int x = 0; x < width; x++)
                {
                    Vector3 center = CellCenter(x, z);
                    Vector3 bottom = center + bottomOffset;
                    Vector3 top = center + topOffset;

                    // 只有空气还不够：楼层之间的空隙上方也没有碰撞体，
                    // 但脚下没有地板，怪物走进去会掉到地下室的顶板上卡死。
                    if (IsFree(bottom, top, out _) && HasFloor(center))
                    {
                        walkable[x + z * width] = true;
                        WalkableCount++;
                    }

                    if ((x + z * width) % perFrame == 0)
                    {
                        yield return null;
                    }
                }
            }

            Ready = true;
            Debug.Log("[PuzzleEscape] 怪物导航网格完成：可站立格子 " + WalkableCount + " 个。");
        }

        private static bool IsFree(Vector3 bottom, Vector3 top, out bool hasDoor)
        {
            hasDoor = false;

            // 用 NonAlloc 版本：每层要采样几千个格子，逐次分配数组在低配机器上会拖垮内存。
            int count = Physics.OverlapCapsuleNonAlloc(bottom, top, CapsuleRadius, OverlapBuffer,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            if (count >= OverlapBuffer.Length)
            {
                // 缓冲区装满说明这里极其拥挤，直接当作不可通行
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                Collider hit = OverlapBuffer[i];
                if (hit == null || hit.isTrigger)
                {
                    continue;
                }

                if (hit.GetComponentInParent<HingedDoor>() != null)
                {
                    // 门扇不算障碍：怪物走到门口会自己把门推开。
                    hasDoor = true;
                    continue;
                }

                // 门框、门套、门楣、合页这些贴边薄件也忽略：
                // 它们会把 1.8 m 的门洞挤窄，导致门口没有可走格子、房间之间断开。
                if (hit.gameObject.name.Contains("Door"))
                {
                    hasDoor = true;
                    continue;
                }

                return false;
            }

            return true;
        }

        /// <summary>
        /// 格子脚下必须有一层贴着采样面的地板。往下打短射线，
        /// 命中点低于采样面 0.5 m 就说明下面是楼层空隙或地下室顶板，不算可走。
        /// </summary>
        private static bool HasFloor(Vector3 center)
        {
            if (!Physics.Raycast(center + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit,
                    1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hit.point.y >= center.y - 0.5f;
        }

        private Vector3 CellCenter(int x, int z)
        {
            return origin + new Vector3((x + 0.5f) * CellSize, 0f, (z + 0.5f) * CellSize);
        }

        private bool TryGetCell(Vector3 position, out int x, out int z)
        {
            x = Mathf.FloorToInt((position.x - origin.x) / CellSize);
            z = Mathf.FloorToInt((position.z - origin.z) / CellSize);

            if (x < 0 || x >= width || z < 0 || z >= depth)
            {
                return false;
            }

            return true;
        }

        public bool IsWalkable(Vector3 position)
        {
            if (!Ready || !TryGetCell(position, out int x, out int z))
            {
                return false;
            }

            return walkable[x + z * width];
        }

        public Vector3 RandomWalkablePoint(Vector3 near, float minDistance, float maxDistance)
        {
            if (!Ready || WalkableCount == 0)
            {
                return near;
            }

            for (int attempt = 0; attempt < 40; attempt++)
            {
                int index = Random.Range(0, walkable.Length);
                if (!walkable[index])
                {
                    continue;
                }

                int x = index % width;
                int z = index / width;
                Vector3 candidate = CellCenter(x, z);
                float distance = Vector3.Distance(
                    new Vector3(candidate.x, 0f, candidate.z),
                    new Vector3(near.x, 0f, near.z));

                if (distance >= minDistance && distance <= maxDistance)
                {
                    return candidate;
                }
            }

            return near;
        }

        /// <summary>
        /// 随便取一个可站立格子（不限制与参考点的距离）。
        /// 用于“附近找不到合适目标”时的兜底，避免怪物站在原地看着自己。
        /// </summary>
        public Vector3 RandomAnyWalkablePoint()
        {
            if (!Ready || WalkableCount == 0)
            {
                return origin;
            }

            for (int attempt = 0; attempt < 40; attempt++)
            {
                int index = Random.Range(0, walkable.Length);
                if (walkable[index])
                {
                    return CellCenter(index % width, index / width);
                }
            }

            return origin;
        }

        /// <summary>
        /// 广度优先搜索一条到目标点的格子路径。目标被墙挡住时取最近的可行走格子。
        /// </summary>
        public bool TryFindPath(Vector3 start, Vector3 goal, List<Vector3> result)
        {
            result.Clear();

            if (!Ready || !TryGetCell(start, out int sx, out int sz))
            {
                return false;
            }

            if (!TryGetCell(goal, out int gx, out int gz))
            {
                // 目标在导航范围外（侧翼、南廊等）：夹到最近的边界格子，
                // 让怪物至少朝那个方向追过去，而不是原地不动。
                gx = Mathf.Clamp(Mathf.FloorToInt((goal.x - origin.x) / CellSize), 0, width - 1);
                gz = Mathf.Clamp(Mathf.FloorToInt((goal.z - origin.z) / CellSize), 0, depth - 1);
            }

            int startIndex = sx + sz * width;
            if (!walkable[startIndex])
            {
                startIndex = FindNearestWalkable(sx, sz);
                if (startIndex < 0)
                {
                    return false;
                }
            }

            int goalIndex = gx + gz * width;
            if (!walkable[goalIndex])
            {
                goalIndex = FindNearestWalkable(gx, gz);
                if (goalIndex < 0)
                {
                    return false;
                }
            }

            if (startIndex == goalIndex)
            {
                result.Add(CellCenter(goalIndex % width, goalIndex / width));
                return true;
            }

            var previous = new int[walkable.Length];
            for (int i = 0; i < previous.Length; i++)
            {
                previous[i] = -1;
            }

            var queue = new Queue<int>();
            queue.Enqueue(startIndex);
            previous[startIndex] = startIndex;
            bool found = false;

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                if (current == goalIndex)
                {
                    found = true;
                    break;
                }

                int cx = current % width;
                int cz = current / width;

                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = cx + (direction == 0 ? 1 : direction == 1 ? -1 : 0);
                    int nz = cz + (direction == 2 ? 1 : direction == 3 ? -1 : 0);

                    if (nx < 0 || nx >= width || nz < 0 || nz >= depth)
                    {
                        continue;
                    }

                    int next = nx + nz * width;
                    if (!walkable[next] || previous[next] != -1)
                    {
                        continue;
                    }

                    previous[next] = current;
                    queue.Enqueue(next);
                }
            }

            if (!found)
            {
                return false;
            }

            int step = goalIndex;
            var reversed = new List<Vector3>();
            while (step != startIndex)
            {
                reversed.Add(CellCenter(step % width, step / width));
                step = previous[step];
            }

            for (int i = reversed.Count - 1; i >= 0; i--)
            {
                result.Add(reversed[i]);
            }

            return result.Count > 0;
        }

        private int FindNearestWalkable(int x, int z)
        {
            for (int radius = 1; radius <= 4; radius++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx;
                        int nz = z + dz;
                        if (nx < 0 || nx >= width || nz < 0 || nz >= depth)
                        {
                            continue;
                        }

                        if (walkable[nx + nz * width])
                        {
                            return nx + nz * width;
                        }
                    }
                }
            }

            return -1;
        }
    }
}
