using System.Collections;
using System.Collections.Generic;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.AI
{
    /// <summary>
    /// 疗养院怪物生成器。
    /// 疗养院是「B1 + 1~4F」的五层结构，一张平面网格无法跨层寻路，
    /// 所以按楼层分别建立导航网格；玩家换层后把怪物转移到当前楼层的远处，
    /// 让怪物始终和玩家在同一层，不会出现「怪在一层、玩家在四层」的死局。
    /// </summary>
    public class SanatoriumMonsterSpawner : MonoBehaviour
    {
        public string PrefabResourcePath = "Monster/MonsterModel";

        [Tooltip("导航网格覆盖的地面范围（以一层中心为基准，每层自动按楼层高度平移）。")]
        public Vector3 NavigationCenter = new Vector3(0f, 0f, 4.5f);
        public Vector3 NavigationSize = new Vector3(84f, 1f, 33f);

        [Tooltip("楼层高度，与 SanatoriumLevelBuilder 保持一致。")]
        public float FloorHeight = 4.2f;

        [Tooltip("首次生成时与玩家的最小距离。")]
        public float MinSpawnDistance = 22f;

        [Tooltip("换层后把怪物转过来时与玩家的最小距离。")]
        public float RelocateMinDistance = 18f;

        private readonly Dictionary<int, MonsterNavigationGrid> grids =
            new Dictionary<int, MonsterNavigationGrid>();

        private MonsterNpc npc;
        private Transform player;
        private int currentFloor = int.MinValue;
        private float nextFloorCheck;
        private bool spawning;
        private float nextRespawnTime;

        private void Start()
        {
            StartCoroutine(SpawnRoutine());
        }

        private IEnumerator SpawnRoutine()
        {
            if (spawning)
            {
                yield break;
            }

            spawning = true;

            while (player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");
                if (found != null)
                {
                    player = found.transform;
                    break;
                }

                yield return new WaitForSeconds(0.4f);
            }

            // 等场景完全初始化（Bootstrap 已经把玩家、环境都建好）再采样，
            // 否则会在场景切换/初始化的中间态里跑几千次物理查询。
            while (GameManager.Instance == null || !GameManager.Instance.HasStarted)
            {
                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(1f);

            if (!DifficultySettings.MonstersEnabled || npc != null)
            {
                spawning = false;
                yield break;
            }

            GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (prefab == null)
            {
                Debug.LogWarning("[PuzzleEscape] 未找到怪物模型：Resources/" + PrefabResourcePath +
                                 "，请先运行菜单「PuzzleEscape → 生成怪物 Prefab」。");
                nextRespawnTime = Time.time + 30f;
                spawning = false;
                yield break;
            }

            int floor = FloorOf(player.position.y);
            Debug.Log("[PuzzleEscape] 疗养院怪物：开始构建 " + FloorName(floor) + " 导航网格…");

            MonsterNavigationGrid grid = GetGrid(floor);

            while (!grid.Ready)
            {
                yield return null;
            }

            if (!DifficultySettings.MonstersEnabled || npc != null)
            {
                spawning = false;
                yield break;
            }

            Debug.Log("[PuzzleEscape] 疗养院怪物：导航网格就绪，开始选出生点…");

            Vector3 spawn = PickPoint(grid, player.position, MinSpawnDistance);
            Debug.Log("[PuzzleEscape] 疗养院怪物：出生点 " + spawn + "，开始生成…");

            CreateMonster(prefab, spawn, grid);

            currentFloor = floor;
            spawning = false;
            Debug.Log("[PuzzleEscape] 疗养院怪物已生成：" + FloorName(floor) + " " + spawn);
        }

        private void Update()
        {
            if (player == null)
            {
                return;
            }

            // 和平难度不保留怪物；切回其他难度时自动重新生成。
            if (!DifficultySettings.MonstersEnabled)
            {
                return;
            }

            if (npc == null)
            {
                if (!spawning && Time.time >= nextRespawnTime)
                {
                    nextRespawnTime = Time.time + 3f;
                    StartCoroutine(SpawnRoutine());
                }

                return;
            }

            if (Time.time < nextFloorCheck)
            {
                return;
            }

            nextFloorCheck = Time.time + 1f;

            // 正在循声查看时不要把它拉回玩家所在楼层，否则跨层追踪会立刻被打断。
            if (npc.IsInvestigating && !npc.IsChasing)
            {
                return;
            }

            int floor = FloorOf(player.position.y);
            if (floor == currentFloor)
            {
                return;
            }

            currentFloor = floor;
            StartCoroutine(SwitchFloorRoutine(GetGrid(floor), floor));
        }

        private IEnumerator SwitchFloorRoutine(MonsterNavigationGrid grid, int floor)
        {
            while (!grid.Ready)
            {
                yield return null;
            }

            npc.SetGrid(grid);

            float floorY = (floor - 1) * FloorHeight;
            if (Mathf.Abs(npc.transform.position.y - floorY) < 2.5f)
            {
                Debug.Log("[PuzzleEscape] 怪物跟上玩家，进入 " + FloorName(floor) + "。");
                yield break;
            }

            Vector3 target = PickPoint(grid, player.position, RelocateMinDistance);
            npc.RelocateTo(target);
            Debug.Log("[PuzzleEscape] 玩家进入 " + FloorName(floor) + "，怪物已转到同一层。");
        }

        private void CreateMonster(GameObject prefab, Vector3 position, MonsterNavigationGrid grid)
        {
            var root = new GameObject("Monster");
            root.layer = 2;
            root.transform.position = position + Vector3.up * 0.05f;

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.95f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.05f;

            GameObject visual = Instantiate(prefab, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Collider playerCollider = player.GetComponent<Collider>();
            if (playerCollider != null)
            {
                // 避免怪物把玩家顶着走或推穿墙
                Physics.IgnoreCollision(controller, playerCollider);
            }

            Vector3 look = player.position - position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
            {
                root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
            }

            npc = root.AddComponent<MonsterNpc>();
            npc.CrossFloorHandoff = HandleNoiseOnOtherFloor;
            npc.Initialize(grid, player);
        }

        /// <summary>
        /// 声源不在怪物当前所在楼层：把它送到声源所在层的附近，再由 MonsterNpc 自行走过去。
        /// 返回 false 表示无法处理（例如楼层超界）。
        /// </summary>
        private bool HandleNoiseOnOtherFloor(Vector3 noisePosition)
        {
            if (npc == null)
            {
                return false;
            }

            int floor = FloorOf(noisePosition.y);
            if (floor == currentFloor)
            {
                return true;
            }

            StartCoroutine(SendMonsterToFloor(floor, noisePosition));
            return true;
        }

        private IEnumerator SendMonsterToFloor(int floor, Vector3 noisePosition)
        {
            MonsterNavigationGrid grid = GetGrid(floor);

            while (!grid.Ready)
            {
                yield return null;
            }

            npc.SetGrid(grid);

            // 落在声源附近 6–14 m 的可站立点，让玩家能看到它“赶过来”的过程。
            Vector3 spot = grid.RandomWalkablePoint(noisePosition, 6f, 14f);

            if (!TryGroundPoint(spot, out Vector3 grounded))
            {
                grounded = spot;
            }

            npc.RelocateTo(grounded);
            currentFloor = floor;
            nextFloorCheck = Time.time + 1.5f;

            Debug.Log("[PuzzleEscape] 怪物循声跨层，来到 " + FloorName(floor) + "：" + grounded);
        }

        /// <summary>按楼层取（或创建）导航网格，每层只构建一次。</summary>
        private MonsterNavigationGrid GetGrid(int floor)
        {
            if (grids.TryGetValue(floor, out MonsterNavigationGrid cached))
            {
                return cached;
            }

            float baseY = (floor - 1) * FloorHeight;

            // 采样胶囊的底端 = originY + CapsuleBottom - CapsuleRadius，
            // 地板顶面正好在 baseY。必须把采样面抬高 0.25 m，
            // 否则胶囊会压进地板，整层每个格子都会被判定为“被挡住”。
            Vector3 center = new Vector3(NavigationCenter.x, baseY + 0.75f, NavigationCenter.z);

            // 保险：坐标异常时不要建网格，否则 Bounds 会算出超大数组直接把内存打爆
            if (float.IsNaN(center.x) || float.IsNaN(center.y) || float.IsNaN(center.z) ||
                Mathf.Abs(center.y) > 200f)
            {
                Debug.LogWarning("[PuzzleEscape] 导航网格坐标异常，跳过构建：" + center);
                center = new Vector3(0f, 0.5f, NavigationCenter.z);
                floor = 1;
            }

            MonsterNavigationGrid grid = MonsterNavigationGrid.Create(new Bounds(center, NavigationSize));

            grids[floor] = grid;
            StartCoroutine(grid.BuildAsync());

            return grid;
        }

        /// <summary>在指定楼层找一个离玩家足够远的可站立点。</summary>
        private Vector3 PickPoint(MonsterNavigationGrid grid, Vector3 from, float minDistance)
        {
            Vector3 fallback = new Vector3(NavigationCenter.x, (FloorOf(from.y) - 1) * FloorHeight,
                NavigationCenter.z);

            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 candidate = grid.RandomWalkablePoint(from, minDistance, 40f);

                if (FlatDistance(candidate, from) < minDistance * 0.6f)
                {
                    continue;
                }

                if (TryGroundPoint(candidate, out Vector3 grounded))
                {
                    return grounded;
                }
            }

            return fallback;
        }

        private bool TryGroundPoint(Vector3 point, out Vector3 grounded)
        {
            grounded = point;

            if (!Physics.Raycast(point + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4.5f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // 别把怪物放到下一层的地板上（楼梯井、楼板缝隙会打穿）。
            if (hit.point.y < point.y - 0.5f)
            {
                return false;
            }

            grounded = new Vector3(point.x, hit.point.y, point.z);
            return true;
        }

        private int FloorOf(float y)
        {
            return Mathf.Clamp(Mathf.RoundToInt(y / FloorHeight) + 1, 0, 4);
        }

        private static string FloorName(int floor)
        {
            return floor == 0 ? "B1" : floor + "F";
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }
    }
}
