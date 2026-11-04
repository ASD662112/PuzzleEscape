using System.Collections;
using PuzzleEscape.AI;
using PuzzleEscape.Audio;
using PuzzleEscape.Data;
using PuzzleEscape.Interaction;
using PuzzleEscape.Player;
using PuzzleEscape.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 场景入口：创建管理器、程序化两层庄园、唯一道具与玩家 HUD。
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Bootstrap : MonoBehaviour
    {
        private void Awake()
        {
            // 先按当前场景确定地图：直接打开 Sanatorium 场景调试时，
            // PlayerPrefs 里可能还记着庄园，会导致加错怪物生成器（庄园怪跑进疗养院）。
            GameMap map = ResolveMap();
            if (GameMapCatalog.CurrentId != map.Id)
            {
                GameMapCatalog.Select(map.Id);
            }

            FrameRateSettings.ApplySaved();

            if (GameManager.Instance == null)
            {
                gameObject.AddComponent<GameManager>();
            }

            if (AudioManager.Instance == null)
            {
                gameObject.AddComponent<AudioManager>();
            }

            if (LightingQuality.Instance == null)
            {
                gameObject.AddComponent<LightingQuality>();
            }

            if (UIRoot.Instance == null)
            {
                gameObject.AddComponent<UIRoot>();
            }

            if (CaseFileUI.Instance == null)
            {
                gameObject.AddComponent<CaseFileUI>();
            }

            if (ItemDetailUI.Instance == null)
            {
                gameObject.AddComponent<ItemDetailUI>();
            }

            if (SaveSlotUI.Instance == null)
            {
                gameObject.AddComponent<SaveSlotUI>();
            }

            if (MapSelectUI.Instance == null)
            {
                gameObject.AddComponent<MapSelectUI>();
            }

            if (TutorialUI.Instance == null)
            {
                gameObject.AddComponent<TutorialUI>();
            }

            if (MapSystem.Instance == null)
            {
                gameObject.AddComponent<MapSystem>();
            }

            // 暗道与隐藏密码锁属于庄园内容，其他地图不生成，避免出现无关几何。
            bool isMansion = GameMapCatalog.CurrentId == GameMapCatalog.MansionId;

            if (isMansion && GetComponent<ExpansionPuzzles>() == null)
            {
                gameObject.AddComponent<ExpansionPuzzles>();
            }

            if (GetComponent<AutoSaveGame>() == null)
            {
                gameObject.AddComponent<AutoSaveGame>();
            }

            if (GetComponent<RoomAmbienceDirector>() == null)
            {
                gameObject.AddComponent<RoomAmbienceDirector>();
            }

            if (isMansion && GetComponent<MonsterSpawner>() == null)
            {
                gameObject.AddComponent<MonsterSpawner>();
            }

            // 疗养院是五层结构，用按楼层建导航网格的生成器
            if (!isMansion && GetComponent<SanatoriumMonsterSpawner>() == null)
            {
                gameObject.AddComponent<SanatoriumMonsterSpawner>();
            }

            // 跳杀演出两张地图共用
            if (GetComponent<MonsterCatchSequence>() == null)
            {
                gameObject.AddComponent<MonsterCatchSequence>();
            }

            if (isMansion && GetComponent<SecretPassage>() == null)
            {
                gameObject.AddComponent<SecretPassage>();
            }

            // 疗养院有自己的通关顺序引导（通行卡 → 供电 → 钥匙 → 主门钥匙 → 正门）
            if (!isMansion && GetComponent<SanatoriumObjectives>() == null)
            {
                gameObject.AddComponent<SanatoriumObjectives>();
            }

            if (GetComponent<PhotoMode>() == null)
            {
                gameObject.AddComponent<PhotoMode>();
            }
        }

        private void Start()
        {
            Application.runInBackground = true;
            QualitySettings.pixelLightCount = 8;
            UIScaleSettings.ApplyAll();

            bool loading = SaveGameService.HasPendingLoad;

            CaseFileSystem.ResetForNewGame();
            CaseFileSystem.Initialize(FindObjectsOfType<ClueInteractable>().Length);

            GameMap map = ResolveMap();
            Debug.Log("[PuzzleEscape] 进入地图：" + map.Title + "（场景 " + map.SceneName + "）");

            // 场景里已经烘焙了环境时直接复用，避免重复生成。
            if (GameObject.Find("Environment") == null)
            {
                if (map.Id == GameMapCatalog.SanatoriumId)
                {
                    SanatoriumLevelBuilder.Build();
                }
                else
                {
                    MansionLevelBuilder.Build();
                }
            }

            if (map.Id == GameMapCatalog.SanatoriumId)
            {
                RestoreSanatoriumDoorAutoClose();
            }

            ItemSpawnDirector.ResetRegistry();
            if (!loading)
            {
                ItemSpawnDirector.SpawnAll();
            }

            PlayerRig rig = PlayerFactory.Create(map.SpawnPosition, map.SpawnYaw);
            Debug.Log("[PuzzleEscape] 玩家生成位置：" + rig.Root.transform.position);
            StartCoroutine(PinSpawnForOneSecond(rig, map.SpawnPosition, map.SpawnYaw));

            UIRoot.Instance.BindPlayer(rig);

            if (map.Id == GameMapCatalog.MansionId)
            {
                // 从疗养院切回庄园：清掉疗养院的目标覆盖，恢复庄园自己的任务链
                GameManager.Instance.ClearMapObjectiveOverride();
                GameManager.Instance.RefreshObjective();
            }
            else if (!string.IsNullOrEmpty(map.Objective))
            {
                GameManager.Instance.SetMapObjective(map.Objective);
            }

            UIRoot.Instance.SetObjective(GameManager.Instance.CurrentObjective);

            if (loading)
            {
                StartCoroutine(ApplyLoadAfterStart());
            }
            else
            {
                SeedSanatoriumLoot();
            }

            // 开场提示在玩家点击主界面的「开始游戏」后再显示，避免被主界面遮住。
        }

        /// <summary>
        /// 疗养院旧版场景里的房门都保存成 AutoCloseDelay = 0（保持开启）。
        /// 运行时统一恢复成 5 秒自动关闭；一层正门是出口，保持开启。
        /// </summary>
        private static void RestoreSanatoriumDoorAutoClose()
        {
            foreach (HingedDoor door in Object.FindObjectsOfType<HingedDoor>(true))
            {
                if (door == null || door.name == "MainDoor")
                {
                    continue;
                }

                if (door.AutoCloseDelay <= 0f)
                {
                    door.AutoCloseDelay = 5f;
                }
            }
        }

        /// <summary>
        /// 开始后的 1 秒内把玩家固定在出生点，避免出生瞬间被碰撞体顶飞或落到别处。
        /// 期间关闭 CharacterController，1 秒后恢复，让重力正常生效。
        /// </summary>
        private IEnumerator PinSpawnForOneSecond(PlayerRig rig, Vector3 spawn, float yaw)
        {
            if (rig == null || rig.Root == null)
            {
                yield break;
            }

            CharacterController character = rig.Root.GetComponent<CharacterController>();
            if (character != null)
            {
                character.enabled = false;
            }

            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            float end = Time.time + 1f;
            while (Time.time < end)
            {
                rig.Root.transform.SetPositionAndRotation(spawn, rotation);
                yield return null;
            }

            if (character != null)
            {
                character.enabled = true;
            }

            Debug.Log("[PuzzleEscape] 出生点固定结束，当前位置：" + rig.Root.transform.position);
        }

        private IEnumerator ApplyLoadAfterStart()
        {
            // 等所有 Start（包括隐藏密码锁和地图初始化）执行完，再应用存档。
            yield return null;
            SaveGameService.ApplyPendingLoad();
            SeedSanatoriumLoot();
        }

        /// <summary>
        /// 当前地图以“实际打开的是哪个场景”为准，场景没有登记时才回退到 PlayerPrefs。
        /// 否则在编辑器里直接打开 Sanatorium 调试时，PlayerPrefs 里可能还记着庄园，
        /// 会误加庄园的怪物生成器与机关，导致导航网格按庄园的尺寸构建、怪物撞墙。
        /// </summary>
        private static GameMap ResolveMap()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            GameMap byScene = GameMapCatalog.FindForScene(sceneName);
            return byScene != null ? byScene : GameMapCatalog.Current;
        }

        /// <summary>
        /// 疗养院没有固定的手电筒刷新点：每次开局随机挑一个尚未打开的抽屉/柜子放一把。
        /// 全图只会有一把，读档时若已经拿到或场上已有则跳过。
        /// </summary>
        private static void SeedSanatoriumLoot()
        {
            if (GameMapCatalog.CurrentId != GameMapCatalog.SanatoriumId)
            {
                return;
            }

            PlayerInventory inventory = Object.FindObjectOfType<PlayerInventory>(true);
            if (inventory != null && inventory.Contains(ItemIds.Lantern))
            {
                return;
            }

            ItemSpawnDirector.AssignUniqueToRandomContainer(ItemIds.Lantern);
        }
    }
}
