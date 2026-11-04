using System.Collections;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.AI
{
    /// <summary>
    /// 在庄园一层生成怪物 NPC：从 Resources 读取烘焙好的模型，套上 CharacterController 与怪物 AI。
    /// 其他地图不添加该组件。
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        public Vector3 SpawnPosition = new Vector3(16f, 0.05f, 0f);
        public string PrefabResourcePath = "Monster/MonsterModel";

        [Tooltip("导航网格覆盖的地面范围（世界坐标）：主楼 + 东西侧翼 + 南廊。")]
        public Vector3 NavigationCenter = new Vector3(0f, 0f, -7f);
        public Vector3 NavigationSize = new Vector3(70f, 1f, 44f);

        private void Start()
        {
            StartCoroutine(SpawnRoutine());
        }

        private MonsterNpc npc;
        private bool spawning;
        private float nextRespawnTime;

        /// <summary>和平难度不保留怪物；切回其他难度时自动重新生成。</summary>
        private void Update()
        {
            if (!DifficultySettings.MonstersEnabled || npc != null || spawning)
            {
                return;
            }

            if (Time.time < nextRespawnTime)
            {
                return;
            }

            nextRespawnTime = Time.time + 3f;
            StartCoroutine(SpawnRoutine());
        }

        /// <summary>等场景完全初始化后再采样导航网格，避免在切换场景的中间态里跑物理查询。</summary>
        private IEnumerator SpawnRoutine()
        {
            if (spawning)
            {
                yield break;
            }

            spawning = true;

            while (GameManager.Instance == null || !GameManager.Instance.HasStarted)
            {
                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(1f);
            Spawn();
            spawning = false;
        }

        private void Spawn()
        {
            if (npc != null || !DifficultySettings.MonstersEnabled)
            {
                return;
            }

            GameObject modelPrefab = Resources.Load<GameObject>(PrefabResourcePath);
            if (modelPrefab == null)
            {
                Debug.LogWarning("[PuzzleEscape] 未找到怪物模型：Resources/" + PrefabResourcePath +
                                 "，请先运行菜单「PuzzleEscape → 生成怪物 Prefab」。");
                nextRespawnTime = Time.time + 30f;
                return;
            }

            var root = new GameObject("Monster");
            root.layer = 2;
            root.transform.position = SpawnPosition;

            var controller = root.AddComponent<CharacterController>();
            controller.height = 1.9f;
            controller.radius = 0.35f;
            controller.center = new Vector3(0f, 0.95f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.35f;
            controller.skinWidth = 0.05f;

            GameObject visual = Object.Instantiate(modelPrefab, root.transform);
            visual.name = "Model";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;

            Transform player = null;
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.transform;
                Collider playerCollider = playerObject.GetComponent<Collider>();
                if (playerCollider != null)
                {
                    // 避免怪物把玩家顶着走或推穿墙。
                    Physics.IgnoreCollision(controller, playerCollider);
                }

                Vector3 look = player.position - SpawnPosition;
                look.y = 0f;
                if (look.sqrMagnitude > 0.01f)
                {
                    root.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                }
            }

            float walkSpeed = 3.3f;
            float sprintSpeed = 5.2f;
            if (player != null)
            {
                var firstPerson = player.GetComponent<FirstPersonController>();
                if (firstPerson != null)
                {
                    walkSpeed = firstPerson.WalkSpeed;
                    sprintSpeed = firstPerson.SprintSpeed;
                }
            }

            // 导航网格贴着玩家所在楼层采样，避免格子落在地板以下。
            float floorY = player != null ? player.position.y : NavigationCenter.y;
            Vector3 navCenter = new Vector3(NavigationCenter.x, floorY + 0.5f, NavigationCenter.z);
            var grid = MonsterNavigationGrid.Create(new Bounds(navCenter, NavigationSize));
            npc = root.AddComponent<MonsterNpc>();
            npc.Initialize(grid, player);

            Debug.Log(string.Format(
                "[PuzzleEscape] 怪物已生成：散步 {0:F2} m/s，追逐 {1:F2} m/s，视野 {2} m / {3}°。",
                walkSpeed * npc.PatrolSpeedRatio,
                sprintSpeed * npc.ChaseSpeedRatio,
                npc.ViewDistance,
                npc.ViewAngle));
        }
    }
}
