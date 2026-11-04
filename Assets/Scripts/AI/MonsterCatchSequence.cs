using System.Collections;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.AI
{
    /// <summary>
    /// 怪物抓到玩家后的跳杀演出：相机瞬间贴到怪物脸前并轻微晃动，
    /// 约 2 秒后黑幕、把玩家送回出生点、怪物被重置到远处，再恢复操作。
    /// </summary>
    public class MonsterCatchSequence : MonoBehaviour
    {
        public static MonsterCatchSequence Instance { get; private set; }

        public bool IsPlaying { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Play(MonsterNpc monster, Transform player, FirstPersonController controller, Camera camera)
        {
            if (IsPlaying || monster == null || player == null || controller == null || camera == null)
            {
                return;
            }

            StartCoroutine(PlayRoutine(monster, player, controller, camera));
        }

        private IEnumerator PlayRoutine(MonsterNpc monster, Transform player,
            FirstPersonController controller, Camera camera)
        {
            IsPlaying = true;
            monster.enabled = false;
            controller.enabled = false;

            AudioManager.Instance?.PlayScare();

            float originalFov = camera.fieldOfView;
            Transform cameraTransform = camera.transform;
            cameraTransform.SetParent(null, true);

            Debug.Log("[PuzzleEscape] 怪物抓到玩家，播放跳杀演出。");

            bool restored = false;
            try
            {
                // 贴脸阶段：相机固定在怪物脸部前方 0.38 m，持续轻微抖动。
                const float faceDuration = 2f;
                for (float elapsed = 0f; elapsed < faceDuration; elapsed += Time.deltaTime)
                {
                    Vector3 head = monster.transform.position + Vector3.up * 1.92f;
                    Vector3 basePosition = head + monster.transform.forward * 0.38f - Vector3.up * 0.06f;
                    float shake = Mathf.Lerp(0.012f, 0.05f, elapsed / faceDuration);
                    Vector3 jitter = new Vector3(
                        Random.Range(-shake, shake),
                        Random.Range(-shake, shake),
                        Random.Range(-shake, shake));

                    cameraTransform.position = Vector3.Lerp(cameraTransform.position,
                        basePosition + jitter, 20f * Time.deltaTime);

                    Vector3 look = head - cameraTransform.position;
                    if (look.sqrMagnitude > 0.0001f)
                    {
                        cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation,
                            Quaternion.LookRotation(look.normalized, Vector3.up), 22f * Time.deltaTime);
                    }

                    camera.fieldOfView = Mathf.Lerp(camera.fieldOfView, 50f, 8f * Time.deltaTime);
                    yield return null;
                }

                yield return new WaitForSeconds(0.3f);

                UIRoot ui = UIRoot.Instance;

                for (float t = 0f; t < 0.22f; t += Time.deltaTime)
                {
                    ui?.SetFadeAlpha(t / 0.22f);
                    yield return null;
                }

                ui?.SetFadeAlpha(1f);

                // 计数在完全黑屏之后再触发，避免失败面板提前盖在黑幕上。
                GameManager game = GameManager.Instance;
                bool runOver = game != null && game.NotifyCaught();

                if (runOver)
                {
                    // 第三次被抓：保持冻结，失败面板由 GameManager.Lost 交给 UIRoot 显示。
                    yield return new WaitForSeconds(0.7f);
                    ui?.SetFadeAlpha(0f);
                    ui?.SetFadeLabel(string.Empty);
                }
                else
                {
                    int remaining = game != null ? Mathf.Max(0, GameManager.MaxCatches - game.CaughtCount) : 0;
                    ui?.SetFadeLabel("你被抓住了 · 剩余机会 " + remaining);
                    yield return new WaitForSeconds(0.9f);

                    // 复位相机、玩家与怪物。
                    cameraTransform.SetParent(player, false);
                    cameraTransform.localPosition = new Vector3(0f, 1.58f, 0f);
                    cameraTransform.localRotation = Quaternion.identity;
                    camera.fieldOfView = originalFov;

                    GameMap map = GameMapCatalog.Current;
                    controller.Teleport(FindFreeSpawn(map.SpawnPosition), map.SpawnYaw);
                    monster.ResetAfterCatch();

                    yield return new WaitForSeconds(0.2f);
                    ui?.SetFadeLabel(string.Empty);

                    for (float t = 0f; t < 0.55f; t += Time.deltaTime)
                    {
                        ui?.SetFadeAlpha(1f - t / 0.55f);
                        yield return null;
                    }

                    ui?.SetFadeAlpha(0f);
                    restored = true;
                }
            }
            finally
            {
                // 无论演出是否被中断，都必须把相机、玩家控制和怪物恢复回来。
                if (!restored)
                {
                    UIRoot ui = UIRoot.Instance;
                    ui?.SetFadeAlpha(0f);
                    ui?.SetFadeLabel(string.Empty);

                    if (cameraTransform.parent != player)
                    {
                        cameraTransform.SetParent(player, false);
                        cameraTransform.localPosition = new Vector3(0f, 1.58f, 0f);
                        cameraTransform.localRotation = Quaternion.identity;
                        camera.fieldOfView = originalFov;
                    }
                }

                controller.enabled = true;
                monster.enabled = true;
                IsPlaying = false;

                if (restored)
                {
                    Debug.Log("[PuzzleEscape] 跳杀演出结束，已恢复玩家控制与怪物。");
                }
            }
        }
        /// <summary>出生点若被摆件/碰撞体占住，就近找一个能站人的位置，避免复位后卡住或视角变高。</summary>
        private static Vector3 FindFreeSpawn(Vector3 spawn)
        {
            if (IsSpawnFree(spawn))
            {
                return spawn;
            }

            for (int radius = 1; radius <= 4; radius++)
            {
                for (int step = 0; step < 16; step++)
                {
                    float angle = step * Mathf.PI * 2f / 16f;
                    Vector3 candidate = spawn + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                    if (IsSpawnFree(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return spawn;
        }

        private static bool IsSpawnFree(Vector3 position)
        {
            Vector3 bottom = position + Vector3.up * 0.35f;
            Vector3 top = position + Vector3.up * 1.5f;
            return !Physics.CheckCapsule(bottom, top, 0.3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
    }
}
