using System.Collections;
using System.Collections.Generic;
using PuzzleEscape.Core;
using PuzzleEscape.Interaction;
using PuzzleEscape.Player;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.AI
{
    /// <summary>
    /// 庄园里的怪物 NPC：
    /// 散步时以玩家速度的 1/3 随机寻路，玩家进入视野后以玩家速度的 7/8 追逐；
    /// 玩家连续 5 秒不在视野内则停止追逐、回到散步。需要穿过门时会自己把门推开。
    /// </summary>
    public class MonsterNpc : MonoBehaviour
    {
        [Tooltip("散步速度相对玩家步行速度的比例。")]
        public float PatrolSpeedRatio = 1f / 3f;

        [Tooltip("追逐速度相对玩家奔跑速度的比例。")]
        public float ChaseSpeedRatio = 7f / 8f;

        public float ViewDistance = 16f;
        public float ViewAngle = 110f;
        public float LoseSightSeconds = 5f;

        [Tooltip("距离门多近时自动开门。")]
        public float DoorOpenDistance = 1.7f;

        [Tooltip("追到多近判定为抓到玩家。")]
        public float CatchDistance = 1.5f;

        private const float RepathIntervalChase = 0.45f;
        private const float RepathIntervalPatrol = 0.9f;

        /// <summary>听到声音后前往查看的持续时间。</summary>
        private const float InvestigateSeconds = 14f;

        /// <summary>离声源多近算“已经查看过”。</summary>
        private const float InvestigateArriveDistance = 1.5f;

        private CharacterController controller;
        private Transform player;
        private FirstPersonController playerController;
        private MonsterNavigationGrid grid;
        private Coroutine gridRoutine;
        private readonly List<Vector3> path = new List<Vector3>();

        private int pathIndex;
        private float nextRepathTime;
        private float lastSeenTime = -999f;
        private float verticalVelocity;
        private float stuckTimer;
        private Vector3 lastPosition;
        private float walkSpeed = 3.3f;
        private float sprintSpeed = 5.2f;
        private float idleTime;
        private int stuckCount;
        private bool chasing;
        private bool investigating;
        private Vector3 investigateTarget;
        private float investigateUntil;
        private float holdUntil;
        private int pathFailCount;
        private bool initialised;
        private Vector3 homePosition;
        private float catchCooldownUntil;
        private int difficultyRevision = -1;

        public bool IsChasing => chasing;

        /// <summary>听到声音、正在赶往声源查看。</summary>
        public bool IsInvestigating => investigating;

        /// <summary>
        /// 跨层声源处理回调：由按楼层管理导航网格的生成器注入。
        /// 返回 true 表示已经接手（怪物会被送到声源所在楼层）。
        /// </summary>
        public System.Func<Vector3, bool> CrossFloorHandoff;

        /// <summary>
        /// 通知同一张地图里的所有怪物有声音，不限制距离；
        /// 不同楼层由 <see cref="CrossFloorHandoff"/> 交给楼层管理器处理。
        /// </summary>
        public static void ReportNoise(Vector3 position)
        {
            MonsterNpc[] monsters = Object.FindObjectsOfType<MonsterNpc>(true);

            foreach (MonsterNpc monster in monsters)
            {
                if (monster == null || !monster.isActiveAndEnabled)
                {
                    continue;
                }

                monster.Investigate(position);
            }
        }

        /// <summary>记录一个需要查看的声源位置；路上看到玩家会立刻切换成追逐。</summary>
        public void Investigate(Vector3 position)
        {
            if (!initialised)
            {
                return;
            }

            // 声源在别的楼层：交给楼层管理器把怪物送过去，等待期间先原地不动。
            if (Mathf.Abs(transform.position.y - position.y) > 3f)
            {
                if (CrossFloorHandoff == null || !CrossFloorHandoff(position))
                {
                    return;
                }

                holdUntil = Time.time + 1.5f;
            }

            investigating = true;
            investigateTarget = position;

            // 距离越远给的赶路时间越长，保证真的能走到声源。
            float distance = FlatDistance(transform.position, position);
            float speed = Mathf.Max(0.5f, CurrentSpeed());
            investigateUntil = Time.time + Mathf.Max(InvestigateSeconds, distance / speed * 1.6f + 8f);

            path.Clear();
            pathIndex = 0;
            nextRepathTime = 0f;
            idleTime = 0f;

            Debug.Log("[PuzzleEscape] 怪物听到声音，前往查看：" + position +
                      "（距离 " + distance.ToString("F1") + " m）");
        }

        private void UpdateInvestigation()
        {
            if (!investigating || chasing)
            {
                return;
            }

            bool arrived = FlatDistance(transform.position, investigateTarget) <=
                           InvestigateArriveDistance;

            if (arrived || Time.time > investigateUntil)
            {
                investigating = false;
                path.Clear();
                pathIndex = 0;
                nextRepathTime = 0f;
            }
        }

        public void Initialize(MonsterNavigationGrid navigation, Transform playerTransform)
        {
            grid = navigation;
            player = playerTransform;
            playerController = playerTransform != null
                ? playerTransform.GetComponent<FirstPersonController>()
                : null;

            controller = GetComponent<CharacterController>();
            lastPosition = transform.position;
            homePosition = transform.position;
            initialised = true;

            if (playerController != null)
            {
                walkSpeed = playerController.WalkSpeed;
                sprintSpeed = playerController.SprintSpeed;
            }

            ApplyDifficulty();

            if (grid != null && !grid.Ready)
            {
                gridRoutine = StartCoroutine(BuildGridRoutine());
            }
        }

        private IEnumerator BuildGridRoutine()
        {
            yield return grid.BuildAsync();
            SnapToNearestWalkable();
        }

        /// <summary>切换楼层时换成新的导航网格（疗养院按层建网格）。</summary>
        public void SetGrid(MonsterNavigationGrid navigation)
        {
            grid = navigation;
            path.Clear();
            pathIndex = 0;
            nextRepathTime = 0f;
            stuckTimer = 0f;
            idleTime = 0f;
            stuckCount = 0;

            if (grid != null && !grid.Ready)
            {
                gridRoutine = StartCoroutine(BuildGridRoutine());
            }
        }

        /// <summary>把怪物直接转移到指定位置（换层/长时间卡死时使用）。</summary>
        public void RelocateTo(Vector3 position)
        {
            TeleportTo(position);
            homePosition = position;
            lastPosition = transform.position;
            path.Clear();
            pathIndex = 0;
            nextRepathTime = 0f;
            stuckTimer = 0f;
            idleTime = 0f;
            stuckCount = 0;
        }

        /// <summary>导航网格建好后，如果出生点落在墙里，就近挪到可站立格子。</summary>
        private void SnapToNearestWalkable(bool force = false)
        {
            if (grid == null || !grid.Ready)
            {
                return;
            }

            if (!force && grid.IsWalkable(transform.position))
            {
                return;
            }

            for (int radius = 1; radius <= 8; radius++)
            {
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI * 2f / 16f;
                    Vector3 candidate = transform.position +
                                        new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;

                    if (grid.IsWalkable(candidate))
                    {
                        // 同样要吸附到真实地面，避免落到没有地板的格子里继续往下掉。
                        if (TryGroundPoint(candidate, out Vector3 grounded))
                        {
                            candidate = grounded;
                        }

                        verticalVelocity = 0f;
                        transform.position = candidate;

                        if (player != null)
                        {
                            Vector3 look = player.position - candidate;
                            look.y = 0f;
                            if (look.sqrMagnitude > 0.01f)
                            {
                                transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
                            }
                        }

                        return;
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (gridRoutine != null)
            {
                StopCoroutine(gridRoutine);
            }
        }

        private void Update()
        {
            if (!initialised || grid == null || !grid.Ready || controller == null)
            {
                return;
            }

            // 和平难度：场上不保留怪物。
            if (!DifficultySettings.MonstersEnabled)
            {
                Destroy(gameObject);
                return;
            }

            ApplyDifficulty();

            if (player == null)
            {
                GameObject found = GameObject.FindGameObjectWithTag("Player");
                player = found != null ? found.transform : null;
                if (player == null)
                {
                    return;
                }
            }

            if (!CanThink())
            {
                return;
            }

            UpdatePerception();
            UpdateInvestigation();

            // 跨层声源：等楼层管理器把怪物转移过去，期间先原地不动。
            if (Time.time < holdUntil)
            {
                return;
            }

            UpdatePath();
            MoveAlongPath();
            UpdateStuckDetection();

            // 掉出本层地面（楼梯井、地下室）时拉回本层，避免怪物永远卡在下面。
            // 用相对 homePosition 的高度判断，否则地下室（y≈-4.2）会被误判成掉楼。
            if (transform.position.y < homePosition.y - 6f)
            {
                SnapToNearestWalkable(true);
            }

            TryCatchPlayer();
        }

        /// <summary>追逐状态下贴近玩家时触发跳杀演出。</summary>
        private void TryCatchPlayer()
        {
            if (!chasing || player == null)
            {
                return;
            }

            MonsterCatchSequence sequence = MonsterCatchSequence.Instance;
            if (sequence == null || sequence.IsPlaying || Time.time < catchCooldownUntil)
            {
                return;
            }

            Vector3 delta = player.position - transform.position;
            delta.y = 0f;
            if (delta.magnitude > CatchDistance)
            {
                return;
            }

            var playerController = player.GetComponent<FirstPersonController>();
            sequence.Play(this, player, playerController, Camera.main);
        }

        /// <summary>
        /// 跳杀结束后清空追逐状态。怪物默认留在被抓的位置继续巡逻，
        /// 只有离玩家复活点太近时才挪到附近空位，避免玩家觉得怪物「消失了」或一复活就被抓。
        /// </summary>
        public void ResetAfterCatch()
        {
            chasing = false;
            investigating = false;
            path.Clear();
            pathIndex = 0;
            nextRepathTime = 0f;
            lastSeenTime = -999f;
            stuckTimer = 0f;
            catchCooldownUntil = Time.time + 3f;

            Vector3 playerPosition = player != null
                ? player.position
                : GameMapCatalog.Current.SpawnPosition;

            Vector3 target = transform.position;

            // 玩家已经被送回出生点，怪物留在原地即可；只有贴得太近才挪到 10–18 m 外的空位。
            // 找不到真正有地板的落点时就保持原地，宁可离玩家近一点，也不要把怪物送到夹层或井里。
            if (FlatDistance(target, playerPosition) < 10f && grid != null && grid.Ready)
            {
                if (TryFindRespawnSpot(playerPosition, out Vector3 far))
                {
                    target = far;
                }
                else if (TryGroundPoint(homePosition, out Vector3 groundedHome) &&
                         FlatDistance(groundedHome, playerPosition) >= 8f)
                {
                    target = groundedHome;
                }
            }

            if (TryGroundPoint(target, out target))
            {
                TeleportTo(target);
            }

            lastPosition = transform.position;

            Debug.Log("[PuzzleEscape] 怪物已重置到：" + transform.position);
        }

        /// <summary>
        /// 在玩家复活点周围找一个真正站得住、并且与怪物本层高度一致的落点。
        /// 只随机一两次很容易落在楼层间的空隙里，所以多试几次并逐项校验。
        /// </summary>
        private bool TryFindRespawnSpot(Vector3 playerPosition, out Vector3 result)
        {
            result = playerPosition;

            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 candidate = grid.RandomWalkablePoint(playerPosition, 10f, 18f);

                // RandomWalkablePoint 找不到合适格子时会原样返回参考点。
                if (candidate == playerPosition)
                {
                    continue;
                }

                if (!TryGroundPoint(candidate, out Vector3 grounded))
                {
                    continue;
                }

                if (FlatDistance(grounded, playerPosition) < 10f)
                {
                    continue;
                }

                // 必须和怪物当前楼层同高，避免被放到地下室顶板或别层的地板上。
                if (Mathf.Abs(grounded.y - homePosition.y) > 1f)
                {
                    continue;
                }

                result = grounded;
                return true;
            }

            return false;
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            return new Vector2(a.x - b.x, a.z - b.z).magnitude;
        }

        /// <summary>把点吸附到本层地面；脚下没有地板（墙外、楼梯井）时返回 false。</summary>
        private bool TryGroundPoint(Vector3 point, out Vector3 grounded)
        {
            grounded = point;

            // 正常落点从自身略上方往下打，只认同一层的地面；
            // 已经掉到楼下的则从本层高度重新找地面，方便把怪物拉回一楼。
            bool onThisFloor = point.y > homePosition.y - 1f;
            Vector3 origin = onThisFloor
                ? new Vector3(point.x, point.y + 1.5f, point.z)
                : new Vector3(point.x, homePosition.y + 2f, point.z);
            float distance = onThisFloor ? 3f : 6f;

            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // 楼梯井、楼层之间的空隙没有地板，射线会打到下面一层的顶板（例如地下室的顶面）。
            // 落点比采样面低太多就当作“没有地板”，否则怪物会被放进夹层里再也走不回来。
            if (hit.point.y < point.y - 0.5f)
            {
                return false;
            }

            grounded = new Vector3(point.x, hit.point.y, point.z);
            return true;
        }

        private void TeleportTo(Vector3 position)
        {
            verticalVelocity = 0f;

            if (controller == null)
            {
                transform.position = position;
                return;
            }

            controller.enabled = false;
            transform.position = new Vector3(position.x, position.y + 0.05f, position.z);
            controller.enabled = true;
        }

        private bool CanThink()
        {
            UIRoot ui = UIRoot.Instance;
            if (ui != null && ui.IsModalOpen)
            {
                return false;
            }

            GameManager game = GameManager.Instance;
            return game == null || (!game.IsWon && !game.IsLost && game.HasStarted);
        }

        private void UpdatePerception()
        {
            if (CanSeePlayer())
            {
                if (!chasing)
                {
                    Debug.Log("[PuzzleEscape] 怪物发现玩家，开始追逐。");
                }

                lastSeenTime = Time.time;
                chasing = true;

                // 看到玩家后立刻放弃查看声源，转为追逐。
                investigating = false;
            }
            else if (chasing && Time.time - lastSeenTime > LoseSightSeconds)
            {
                chasing = false;
                path.Clear();
                nextRepathTime = 0f;
                Debug.Log("[PuzzleEscape] 怪物跟丢了玩家，回到散步。");
            }
        }

        private bool CanSeePlayer()
        {
            if (player == null)
            {
                return false;
            }

            // 玩家躲进柜子后完全不可见：既不会被发现，也不会被抓。
            if (PlayerHiding.Instance != null && PlayerHiding.Instance.IsHidden)
            {
                return false;
            }

            Vector3 eye = transform.position + Vector3.up * 1.55f;
            Vector3 target = player.position + Vector3.up * 1.2f;
            Vector3 toPlayer = target - eye;
            float distance = toPlayer.magnitude;

            // 阴影庇护：玩家躲在暗处时，怪物的实际发现距离会缩短。
            if (distance > ViewDistance * ShadowSightMultiplier())
            {
                return false;
            }

            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            if (flat.sqrMagnitude > 0.0001f && distance > 2.5f)
            {
                float angle = Vector3.Angle(transform.forward, flat.normalized);
                if (angle > ViewAngle * 0.5f)
                {
                    return false;
                }
            }

            // Ignore Raycast 层同时包含玩家与怪物自身，射线只检测墙体/门/家具。
            if (Physics.Raycast(eye, toPlayer.normalized, out RaycastHit hit,
                    distance - 0.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.distance >= distance - 0.3f;
            }

            return true;
        }

        /// <summary>难度档位变化时刷新速度比例与视野距离。</summary>
        private void ApplyDifficulty()
        {
            if (difficultyRevision == DifficultySettings.Revision)
            {
                return;
            }

            difficultyRevision = DifficultySettings.Revision;
            PatrolSpeedRatio = DifficultySettings.PatrolRatio;
            ChaseSpeedRatio = DifficultySettings.ChaseRatio;
            ViewDistance = DifficultySettings.ViewDistance;
        }

        /// <summary>
        /// 阴影庇护：玩家站在暗处、没有开灯时发现距离减半；
        /// 在他身边点亮手电筒或蜡烛，暴露度回升到正常。
        /// </summary>
        private float ShadowSightMultiplier()
        {
            float exposure = PlayerLightExposure.Evaluate(player.position);
            return Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(exposure / 0.45f));
        }

        private void UpdatePath()
        {
            float now = Time.time;
            if (now < nextRepathTime && path.Count > 0)
            {
                return;
            }

            nextRepathTime = now + (chasing || investigating
                ? RepathIntervalChase
                : RepathIntervalPatrol);

            Vector3 start = transform.position;
            Vector3 goal;

            if (chasing && player != null)
            {
                goal = player.position;
            }
            else if (investigating)
            {
                goal = investigateTarget;
            }
            else
            {
                goal = PickPatrolPoint(start);

                // 巡逻目标离自己太近（房间太小/附近没有空位）时随便换一个远处的格子，
                // 否则怪物会站在原地等下一轮重新选点。
                if (FlatDistance(goal, start) < 3f)
                {
                    goal = grid.RandomAnyWalkablePoint();
                }
            }

            if (grid.TryFindPath(start, goal, path))
            {
                pathIndex = 0;
                pathFailCount = 0;
                SmoothPath(start);
                return;
            }

            // 连续失败说明所在格子与目标不连通，打印一次方便定位是哪块区域。
            pathFailCount++;
            if (pathFailCount >= 5)
            {
                pathFailCount = 0;
                Debug.LogWarning("[PuzzleEscape] 怪物连续找不到路径：" + start + " → " + goal);
            }

            // 找不到路：查看声源时换一个靠近声源的落点再试，巡逻时下一轮直接换目标。
            if (investigating)
            {
                NudgeInvestigateTarget();
            }

            path.Clear();
            if (!chasing)
            {
                nextRepathTime = now + 0.2f;
            }
        }

        /// <summary>声源被家具挡住时，换个附近的可站立点作为新的查看目标。</summary>
        private void NudgeInvestigateTarget()
        {
            if (grid == null || !grid.Ready)
            {
                return;
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector3 spot = grid.RandomWalkablePoint(investigateTarget, 1f, 8f);

                if (!TryGroundPoint(spot, out Vector3 grounded))
                {
                    continue;
                }

                if (FlatDistance(grounded, transform.position) < 2f)
                {
                    continue;
                }

                investigateTarget = grounded;
                investigateUntil = Mathf.Max(investigateUntil, Time.time + InvestigateSeconds);
                return;
            }
        }

        private Vector3 PickPatrolPoint(Vector3 from)
        {
            // 巡逻目标围绕玩家所在区域挑选（6–18 m），让玩家更容易在路上遇到怪物。
            Vector3 anchor = player != null ? player.position : from;

            // 前几次在附近找（更容易被玩家遇到），后几次放宽到整层，
            // 避免一直在同一个房间来回打转。
            for (int attempt = 0; attempt < 10; attempt++)
            {
                bool wide = attempt >= 5;
                float minDistance = wide ? 14f : 7f;
                float maxDistance = wide ? 34f : 20f;

                Vector3 candidate = grid.RandomWalkablePoint(anchor, minDistance, maxDistance);

                if (!TryGroundPoint(candidate, out candidate))
                {
                    continue;
                }

                // 太近的目标走两步就到，会站着不动，换一个。
                if (FlatDistance(candidate, from) < 4f)
                {
                    continue;
                }

                return candidate;
            }

            // 附近完全挑不出合适点：随便取一个可站立格子，别把目标设成自己。
            return grid != null && grid.Ready ? grid.RandomAnyWalkablePoint() : from;
        }

        /// <summary>路径平滑：能直接走到后面某个点就跳过中间的格子。</summary>
        private void SmoothPath(Vector3 from)
        {
            if (path.Count <= 1)
            {
                return;
            }

            // 用胶囊体而不是射线做拉直检查：射线可以擦着桌子角过去，
            // 但怪物有 0.35 m 半径，实际会被家具挡住，直接走直线就会卡住。
            Vector3 low = from + Vector3.up * 0.45f;
            Vector3 high = from + Vector3.up * 1.55f;
            int keep = 0;

            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 next = path[i + 1] + Vector3.up * 0.35f;
                Vector3 direction = next - low;

                if (direction.magnitude < 0.01f)
                {
                    keep = i + 1;
                    continue;
                }

                bool blocked = Physics.CapsuleCast(low, high, 0.5f, direction.normalized,
                    direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

                if (blocked)
                {
                    keep = i;
                    break;
                }

                keep = i + 1;
            }

            if (keep > 0)
            {
                path.RemoveRange(0, keep);
                pathIndex = 0;
            }
        }

        private float CurrentSpeed()
        {
            float patrolBase = walkSpeed > 0.1f ? walkSpeed : 3.3f;
            float chaseBase = sprintSpeed > 0.1f ? sprintSpeed : 5.2f;

            // 跑去查看声源时用追逐速度，制造“它听见了、正在赶过来”的压迫感。
            return chasing || investigating
                ? chaseBase * ChaseSpeedRatio
                : patrolBase * PatrolSpeedRatio;
        }

        private void MoveAlongPath()
        {
            if (path.Count == 0 || pathIndex >= path.Count)
            {
                // 走完一段路立刻重新规划，避免在终点干站着等下一个重规划周期。
                if (path.Count > 0)
                {
                    path.Clear();
                    pathIndex = 0;
                    nextRepathTime = 0f;
                }

                return;
            }

            // 一帧内可以连续吃掉多个已经到达的路点，路过拐角时不会停顿。
            while (pathIndex < path.Count)
            {
                Vector3 waypoint = path[pathIndex];
                Vector3 toWaypoint = new Vector3(waypoint.x - transform.position.x, 0f,
                    waypoint.z - transform.position.z);

                if (toWaypoint.magnitude >= 0.45f)
                {
                    break;
                }

                pathIndex++;
            }

            if (pathIndex >= path.Count)
            {
                path.Clear();
                pathIndex = 0;
                nextRepathTime = 0f;
                return;
            }

            float speed = CurrentSpeed();
            Vector3 target = path[pathIndex];
            Vector3 flat = new Vector3(target.x - transform.position.x, 0f,
                target.z - transform.position.z);
            Vector3 direction = AvoidObstacles(flat.normalized, out bool blocked);

            // 前方完全被挡住：直接放弃这条路径重新规划，别再顶着墙推。
            if (blocked)
            {
                path.Clear();
                pathIndex = 0;
                nextRepathTime = 0f;
                return;
            }

            TryOpenDoorAhead(direction);

            if (controller.isGrounded && verticalVelocity < 0f)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += Physics.gravity.y * Time.deltaTime;
            }

            Vector3 velocity = direction * speed + Vector3.up * verticalVelocity;
            controller.Move(velocity * Time.deltaTime);

            Quaternion look = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, 6f * Time.deltaTime);
        }

        /// <summary>
        /// 前进方向被家具/墙角挡住时，向左右各偏一点再试，
        /// 让怪物贴着障碍滑过去，而不是一直顶着家具原地踏步。
        /// 用 SphereCast 而不是射线：怪物有体积，射线擦角穿过不代表能走过去。
        /// </summary>
        private Vector3 AvoidObstacles(Vector3 desired, out bool blocked)
        {
            blocked = false;

            if (desired.sqrMagnitude < 0.0001f)
            {
                return desired;
            }

            // 胸部高度、按怪物半径做球形探测，能覆盖门框、立柱和家具边角。
            Vector3 origin = transform.position + Vector3.up * 0.9f;
            const float radius = 0.36f;
            const float distance = 1.25f;

            if (!Physics.SphereCast(origin, radius, desired, out RaycastHit hit, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                hit.collider == null)
            {
                return desired;
            }

            // 门扇挡路不算障碍：TryOpenDoorAhead 会把它推开。
            if (hit.collider.GetComponentInParent<HingedDoor>() != null)
            {
                return desired;
            }

            // 贴着障碍侧面走：把方向投影到障碍表面，形成自然的沿墙滑行。
            if (hit.distance > 0.05f)
            {
                Vector3 slide = Vector3.ProjectOnPlane(desired, hit.normal);

                if (slide.sqrMagnitude > 0.04f)
                {
                    slide.y = 0f;

                    if (slide.sqrMagnitude > 0.04f)
                    {
                        return slide.normalized;
                    }
                }
            }

            for (float angle = 35f; angle <= 70f; angle += 35f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 candidate = Quaternion.Euler(0f, angle * side, 0f) * desired;

                    if (!Physics.SphereCast(origin, radius, candidate, out RaycastHit sideHit,
                            distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                        sideHit.collider == null)
                    {
                        return candidate;
                    }
                }
            }

            // 三个方向都被挡住：告诉调用方放弃这条路径。
            blocked = true;
            return desired;
        }

        private void TryOpenDoorAhead(Vector3 direction)
        {
            Vector3 origin = transform.position + Vector3.up * 1.2f;
            if (Physics.Raycast(origin, direction, out RaycastHit hit, DoorOpenDistance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                HingedDoor door = hit.collider.GetComponentInParent<HingedDoor>();
                if (door != null && !door.IsOpen)
                {
                    door.Open();
                }
            }
        }

        /// <summary>
        /// 防呆：疗养院房间和走廊家具多，容易被模型顶住。
        /// 连续 1 秒几乎没位移就换目标重新规划；再次卡住则直接挪到附近的可通行格子。
        /// </summary>
        private void UpdateStuckDetection()
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer < 0.5f)
            {
                return;
            }

            float moved = Vector3.Distance(transform.position, lastPosition);
            stuckTimer = 0f;
            lastPosition = transform.position;

            if (moved > 0.15f)
            {
                idleTime = 0f;
                stuckCount = 0;
                return;
            }

            idleTime += 0.5f;

            // 玩家就在旁边时不重选目标，否则会错过抓人
            if (chasing && player != null &&
                FlatDistance(player.position, transform.position) < 6f)
            {
                idleTime = 0f;
                return;
            }

            // 1 秒几乎没动：立刻换目标重新规划，不要继续顶着家具。
            if (idleTime < 1f)
            {
                return;
            }

            idleTime = 0f;
            stuckCount++;
            path.Clear();
            pathIndex = 0;
            nextRepathTime = 0f;

            // 查看声源时被挡住，就换一个靠近声源的落点，而不是反复撞同一件家具。
            if (investigating)
            {
                NudgeInvestigateTarget();
            }

            if (stuckCount < 2 || grid == null || !grid.Ready)
            {
                return;
            }

            stuckCount = 0;
            Vector3 spot = grid.RandomWalkablePoint(transform.position, 2f, 9f);

            if (spot == transform.position || !TryGroundPoint(spot, out Vector3 grounded))
            {
                spot = grid.RandomAnyWalkablePoint();
                TryGroundPoint(spot, out grounded);
            }

            if (FlatDistance(grounded, transform.position) > 1.5f)
            {
                TeleportTo(grounded);
                lastPosition = transform.position;
                Debug.Log("[PuzzleEscape] 怪物被模型卡住，已挪到附近的可通行位置。");
            }
        }

        /// <summary>把怪物放到导航网格上的合法位置，避免生成在墙里。</summary>
        public void SnapToGround()
        {
            if (Physics.Raycast(transform.position + Vector3.up * 3f, Vector3.down,
                    out RaycastHit hit, 8f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                transform.position = hit.point + Vector3.up * 0.05f;
            }
        }
    }
}
