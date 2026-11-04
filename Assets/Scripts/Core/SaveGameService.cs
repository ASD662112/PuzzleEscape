using System.Collections.Generic;
using System.IO;
using PuzzleEscape.Data;
using PuzzleEscape.Interaction;
using PuzzleEscape.Player;
using PuzzleEscape.Puzzle;
using PuzzleEscape.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 三个独立存档位：保存玩家、物品栏、主线旗标、容器、刷新点、谜题和案件档案。
    /// 读档时重新加载当前场景，再按快照恢复状态，避免手动重置所有程序化对象。
    /// 每个槽位一个 JSON 文件；自动存档写入当前选中的槽位。
    /// </summary>
    public static class SaveGameService
    {
        public const int SlotCount = 3;

        private const string ActiveSlotKey = "PuzzleEscape.ActiveSlot";
        private const string LegacyFileName = "puzzle_escape_save.json";

        private static SaveGameData pendingLoad;
        private static bool applying;
        private static bool legacyChecked;

        public static int ActiveSlot
        {
            get
            {
                return ClampSlot(PlayerPrefs.GetInt(ActiveSlotKey, 0));
            }
            set
            {
                PlayerPrefs.SetInt(ActiveSlotKey, ClampSlot(value));
                PlayerPrefs.Save();
            }
        }

        public static string SavePath => GetSlotPath(ActiveSlot);
        public static bool HasPendingLoad => pendingLoad != null;
        public static bool IsApplying => applying;

        public static string GetSlotPath(int slot)
        {
            return Path.Combine(Application.persistentDataPath,
                "puzzle_escape_save_slot" + (ClampSlot(slot) + 1) + ".json");
        }

        public static bool HasSave(int slot)
        {
            EnsureLegacyMigrated();
            return File.Exists(GetSlotPath(slot));
        }

        public static bool HasAnySave()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (HasSave(i))
                {
                    return true;
                }
            }

            return false;
        }

        public static int FirstEmptySlot()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                if (!HasSave(i))
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 新游戏默认写入第一个空槽位；三个槽位都满时使用槽位 1。
        /// </summary>
        public static int SelectSlotForNewGame()
        {
            int empty = FirstEmptySlot();
            ActiveSlot = empty >= 0 ? empty : 0;
            return ActiveSlot;
        }

        public static void DeleteSave(int slot)
        {
            string path = GetSlotPath(slot);
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            if (ActiveSlot == ClampSlot(slot))
            {
                int empty = FirstEmptySlot();
                if (empty < 0)
                {
                    empty = 0;
                }

                ActiveSlot = empty;
            }
        }

        public static SaveSlotInfo GetSlotInfo(int slot)
        {
            EnsureLegacyMigrated();
            int index = ClampSlot(slot);
            var info = new SaveSlotInfo { Slot = index };
            string path = GetSlotPath(index);

            if (!File.Exists(path))
            {
                return info;
            }

            info.Exists = true;
            try
            {
                SaveGameData data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
                if (data == null)
                {
                    info.Corrupt = true;
                    return info;
                }

                info.Elapsed = data.Elapsed;
                info.Notes = data.CaseNoteIds != null ? data.CaseNoteIds.Count : 0;
                info.Puzzles = data.CasePuzzleIds != null ? data.CasePuzzleIds.Count : 0;
                info.Rooms = data.VisitedRooms != null ? data.VisitedRooms.Count : 0;
                info.SavedAt = File.GetLastWriteTime(path).ToString("yyyy/MM/dd HH:mm");
                info.MapId = data.MapId;
                info.Active = index == ActiveSlot;
            }
            catch (System.Exception)
            {
                info.Corrupt = true;
            }

            return info;
        }

        public static void SaveGame(bool showToast = true)
        {
            SaveGame(ActiveSlot, showToast);
        }

        public static void SaveGame(int slot, bool showToast = true)
        {
            if (applying)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game == null || !game.HasStarted || game.IsWon)
            {
                if (showToast)
                {
                    game?.Toast("尚未进入游戏，无法保存。");
                }

                return;
            }

            SaveGameData data = Capture();
            if (data == null)
            {
                return;
            }

            int index = ClampSlot(slot);
            try
            {
                File.WriteAllText(GetSlotPath(index), JsonUtility.ToJson(data, true));
                ActiveSlot = index;
                if (showToast)
                {
                    game.Toast("游戏已保存到槽位 " + (index + 1) + "。");
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Save] 写入失败：" + exception.Message);
                if (showToast)
                {
                    game.Toast("存档写入失败，请检查磁盘权限。");
                }
            }
        }

        public static void LoadFromMenu()
        {
            LoadFromMenu(ActiveSlot);
        }

        public static void LoadFromMenu(int slot)
        {
            if (!HasSave(slot))
            {
                GameManager.Instance?.Toast("这个槽位还没有存档。");
                return;
            }

            int index = ClampSlot(slot);
            try
            {
                string json = File.ReadAllText(GetSlotPath(index));
                SaveGameData data = JsonUtility.FromJson<SaveGameData>(json);
                if (data == null)
                {
                    GameManager.Instance?.Toast("存档格式无效。");
                    return;
                }

                pendingLoad = data;
                ActiveSlot = index;

                // 存档可能属于另一张地图：先把地图选择切过去，再加载对应场景。
                GameMap target = GameMapCatalog.Get(data.MapId);
                GameMapCatalog.Select(target.Id);
                SceneManager.LoadScene(target.SceneName);
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Save] 读取失败：" + exception.Message);
                GameManager.Instance?.Toast("存档读取失败。");
            }
        }

        /// <summary>由 Bootstrap 在场景重新加载后调用。</summary>
        public static void ApplyPendingLoad()
        {
            if (pendingLoad == null)
            {
                return;
            }

            applying = true;
            SaveGameData data = pendingLoad;

            if (!string.IsNullOrEmpty(data.MapId) && data.MapId != GameMapCatalog.CurrentId)
            {
                pendingLoad = null;
                applying = false;
                GameManager.Instance?.Toast("该存档属于另一张地图，已跳过读取。");
                return;
            }

            try
            {
                UIRoot root = UIRoot.Instance;
                if (root != null)
                {
                    root.StartGame(false);
                }

                GameManager.Instance?.RestoreProgress(data);
                CaseFileSystem.RestoreSave(data);
                PlayerInventory inventory = FindPlayerInventory();
                inventory?.RestoreFromIds(data.InventoryItems);

                RestoreItemPoints(data);
                RestoreDroppedItems(data);
                RestoreContainers(data);
                RestoreDrawers(data);
                RestoreKeypads(data);
                RestoreLabPuzzles(data);
                RestoreHatches(data);
                RestoreGates(data);
                MapSystem.Instance?.RestoreSave(data);
                RestorePlayer(data);

                if (root != null)
                {
                    root.SetObjective(GameManager.Instance != null
                        ? GameManager.Instance.CurrentObjective
                        : "搜查书房，寻找线索");
                }

                GameManager.Instance?.Toast("已读取存档。");
            }
            catch (System.Exception exception)
            {
                Debug.LogError("[Save] 应用存档失败：" + exception.Message);
            }
            finally
            {
                pendingLoad = null;
                applying = false;
            }
        }

        private static SaveGameData Capture()
        {
            GameManager game = GameManager.Instance;
            SaveGameData data = new SaveGameData
            {
                Version = 1,
                MapId = GameMapCatalog.CurrentId,
                Started = game != null && game.HasStarted,
                Elapsed = game != null ? Mathf.Max(0f, Time.time - game.StartedAt) : 0f,
                DeskNoteRead = game != null && game.DeskNoteRead,
                KeyPicked = game != null && game.KeyPicked,
                DrawerOpened = game != null && game.DrawerOpened,
                ClockRead = game != null && game.ClockRead,
                PowerOn = game != null && game.PowerOn,
                InstalledFuses = game != null ? game.InstalledFuses : 0,
                BasementKeyPicked = game != null && game.BasementKeyPicked,
                LanternPicked = game != null && game.LanternPicked,
                BasementOpened = game != null && game.BasementOpened,
                ElevatorRepaired = game != null && game.ElevatorRepaired,
                ElevatorUsed = game != null && game.ElevatorUsed,
                ArchiveUnlocked = game != null && game.ArchiveUnlocked,
                LabSolved = game != null && game.LabSolved,
                FinalGateUnlocked = game != null && game.FinalGateUnlocked,
                SealSunCollected = game != null && game.SealSunCollected,
                SealMoonCollected = game != null && game.SealMoonCollected,
                SealStarCollected = game != null && game.SealStarCollected,
                SealShadowCollected = game != null && game.SealShadowCollected
            };

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                data.PlayerPosition = player.transform.position;
                data.PlayerYaw = player.transform.eulerAngles.y;
                FirstPersonController controller = player.GetComponent<FirstPersonController>();
                data.PlayerPitch = controller != null ? controller.Pitch : 0f;

                PlayerInventory inventory = player.GetComponent<PlayerInventory>();
                if (inventory != null)
                {
                    for (int i = 0; i < inventory.Slots.Count; i++)
                    {
                        if (inventory.Slots[i] != null)
                        {
                            data.InventoryItems.Add(inventory.Slots[i].Id);
                        }
                    }
                }
            }

            foreach (ItemSpawnPoint point in Object.FindObjectsOfType<ItemSpawnPoint>(true))
            {
                PickupItem pickup = point.GetComponentInChildren<PickupItem>(true);
                data.ItemPoints.Add(new ItemPointState
                {
                    Path = GetPath(point.transform),
                    ItemId = point.ItemId,
                    HasSpawned = point.HasSpawned,
                    Active = pickup != null && pickup.gameObject.activeSelf,
                    SpawnOnStart = point.SpawnOnStart
                });
            }

            foreach (OpenableContainer container in Object.FindObjectsOfType<OpenableContainer>(true))
            {
                data.Containers.Add(new ContainerState
                {
                    Path = GetPath(container.transform),
                    Opened = container.IsOpened
                });
            }

            foreach (LockedDrawer drawer in Object.FindObjectsOfType<LockedDrawer>(true))
            {
                data.Drawers.Add(new DrawerState
                {
                    Path = GetPath(drawer.transform),
                    Opened = drawer.IsOpened
                });
            }

            foreach (KeypadPuzzle keypad in Object.FindObjectsOfType<KeypadPuzzle>(true))
            {
                data.Keypads.Add(new PuzzleState
                {
                    Path = GetPath(keypad.transform),
                    Solved = keypad.IsSolved
                });
            }

            foreach (LabSequencePuzzle puzzle in Object.FindObjectsOfType<LabSequencePuzzle>(true))
            {
                data.LabPuzzles.Add(new PuzzleState
                {
                    Path = GetPath(puzzle.transform),
                    Solved = puzzle.IsSolved
                });
            }

            foreach (BasementHatch hatch in Object.FindObjectsOfType<BasementHatch>(true))
            {
                data.Hatches.Add(new HatchState
                {
                    Path = GetPath(hatch.transform),
                    Opened = hatch.IsOpened
                });
            }

            foreach (SealGate gate in Object.FindObjectsOfType<SealGate>(true))
            {
                data.Gates.Add(new GateState
                {
                    Path = GetPath(gate.transform),
                    Unlocked = gate.IsUnlocked
                });
            }

            foreach (DroppedItemPickup dropped in Object.FindObjectsOfType<DroppedItemPickup>(true))
            {
                if (dropped == null || !dropped.gameObject.activeInHierarchy ||
                    string.IsNullOrEmpty(dropped.ItemId))
                {
                    continue;
                }

                data.DroppedItems.Add(new DroppedItemState
                {
                    ItemId = dropped.ItemId,
                    Position = dropped.transform.position,
                    Euler = dropped.transform.eulerAngles,
                    Settled = dropped.IsSettled
                });
            }

            CaseFileSystem.CaptureSave(data);
            MapSystem.Instance?.CaptureSave(data);
            return data;
        }

        private static void RestoreItemPoints(SaveGameData data)
        {
            if (data.ItemPoints == null)
            {
                return;
            }

            PlayerInventory inventory = FindPlayerInventory();
            bool CheckInventory(string itemId)
            {
                return inventory != null && inventory.Contains(itemId);
            }

            foreach (ItemPointState state in data.ItemPoints)
            {
                Transform transform = FindTransform(state.Path);
                ItemSpawnPoint point = transform != null ? transform.GetComponent<ItemSpawnPoint>() : null;
                if (point == null)
                {
                    continue;
                }

                bool spawnActive = state.Active;
                ItemDefinition definition = ItemDatabase.Get(state.ItemId);
                if (spawnActive && definition != null && definition.Unique && CheckInventory(state.ItemId))
                {
                    // 存档里同时存在“身上有”和“世界中仍刷新”的唯一物品时，以物品栏为准，避免复制。
                    spawnActive = false;
                }

                point.RestoreState(state.ItemId, state.HasSpawned, spawnActive, state.SpawnOnStart);
            }
        }

        private static void RestoreDroppedItems(SaveGameData data)
        {
            foreach (DroppedItemPickup dropped in Object.FindObjectsOfType<DroppedItemPickup>(true))
            {
                if (dropped != null)
                {
                    Object.Destroy(dropped.gameObject);
                }
            }

            if (data.DroppedItems == null)
            {
                return;
            }

            PlayerInventory inventory = FindPlayerInventory();

            foreach (DroppedItemState state in data.DroppedItems)
            {
                ItemDefinition definition = ItemDatabase.Get(state.ItemId);
                if (definition == null)
                {
                    continue;
                }

                if (definition.Unique && inventory != null && inventory.Contains(state.ItemId))
                {
                    continue;
                }

                if (definition.Unique && !ItemSpawnDirector.TryReserve(state.ItemId))
                {
                    continue;
                }

                ItemSpawnPoint.CreateDroppedItem(state.ItemId, state.Position, state.Euler,
                    Vector3.zero, !state.Settled);
            }
        }

        private static void RestoreContainers(SaveGameData data)
        {
            if (data.Containers == null)
            {
                return;
            }

            foreach (ContainerState state in data.Containers)
            {
                Transform transform = FindTransform(state.Path);
                transform?.GetComponent<OpenableContainer>()?.RestoreOpened(state.Opened);
            }
        }

        private static void RestoreDrawers(SaveGameData data)
        {
            if (data.Drawers == null)
            {
                return;
            }

            foreach (DrawerState state in data.Drawers)
            {
                Transform transform = FindTransform(state.Path);
                transform?.GetComponent<LockedDrawer>()?.RestoreOpened(state.Opened);
            }
        }

        private static void RestoreKeypads(SaveGameData data)
        {
            if (data.Keypads == null)
            {
                return;
            }

            foreach (PuzzleState state in data.Keypads)
            {
                Transform transform = FindTransform(state.Path);
                transform?.GetComponent<KeypadPuzzle>()?.RestoreSolved(state.Solved);
            }
        }

        private static void RestoreLabPuzzles(SaveGameData data)
        {
            if (data.LabPuzzles == null)
            {
                return;
            }

            foreach (PuzzleState state in data.LabPuzzles)
            {
                Transform transform = FindTransform(state.Path);
                transform?.GetComponent<LabSequencePuzzle>()?.RestoreSolved(state.Solved);
            }
        }

        private static void RestoreHatches(SaveGameData data)
        {
            if (data.Hatches == null)
            {
                return;
            }

            foreach (HatchState state in data.Hatches)
            {
                Transform transform = FindTransform(state.Path);
                transform?.GetComponent<BasementHatch>()?.RestoreOpened(state.Opened);
            }
        }

        private static void RestoreGates(SaveGameData data)
        {
            if (data.Gates == null)
            {
                return;
            }

            foreach (GateState state in data.Gates)
            {
                Transform transform = FindTransform(state.Path);
                transform?.GetComponent<SealGate>()?.RestoreUnlocked(state.Unlocked);
            }
        }

        private static void RestorePlayer(SaveGameData data)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player == null)
            {
                return;
            }

            FirstPersonController controller = player.GetComponent<FirstPersonController>();
            Vector3 position = ValidateRestorePosition(data.PlayerPosition);

            if (controller != null)
            {
                controller.Teleport(position, data.PlayerYaw);
                controller.SetPitch(data.PlayerPitch);
            }
            else
            {
                player.transform.position = position;
                player.transform.rotation = Quaternion.Euler(0f, data.PlayerYaw, 0f);
            }
        }

        /// <summary>
        /// 存档里的坐标可能来自异常状态（卡在天花板、墙顶或家具里），
        /// 读档时先验证：必须站在地板/楼梯上，且胶囊体没有被占住，否则回到出生点。
        /// </summary>
        private static Vector3 ValidateRestorePosition(Vector3 saved)
        {
            Vector3 bottom = saved + Vector3.up * 0.35f;
            Vector3 top = saved + Vector3.up * 1.5f;
            bool blocked = Physics.CheckCapsule(bottom, top, 0.28f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);

            bool onFloor = Physics.Raycast(saved + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit,
                1.2f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) &&
                hit.collider != null &&
                (hit.collider.name.Contains("Floor") || hit.collider.name.Contains("Stair") ||
                 hit.collider.name.Contains("Step"));

            if (!blocked && onFloor)
            {
                return saved;
            }

            Vector3 spawn = GameMapCatalog.Current.SpawnPosition;
            Debug.LogWarning("[PuzzleEscape] 存档位置无效（blocked=" + blocked + "，onFloor=" + onFloor +
                             "），已改为出生点：" + spawn);
            return spawn;
        }

        private static PlayerInventory FindPlayerInventory()
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            return player != null ? player.GetComponent<PlayerInventory>() : null;
        }

        public static string GetPath(Transform transform)
        {
            var parts = new List<string>();
            Transform current = transform;

            while (current != null)
            {
                parts.Add(current.name + "#" + current.GetSiblingIndex());
                current = current.parent;
            }

            for (int i = 0; i < parts.Count / 2; i++)
            {
                string temp = parts[i];
                int mirror = parts.Count - 1 - i;
                parts[i] = parts[mirror];
                parts[mirror] = temp;
            }

            return string.Join("/", parts.ToArray());
        }

        public static Transform FindTransform(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            string[] parts = path.Split('/');
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();
            Transform current = FindChild(roots, parts[0]);
            if (current == null)
            {
                return null;
            }

            for (int i = 1; i < parts.Length; i++)
            {
                current = FindChild(current, parts[i]);
                if (current == null)
                {
                    return null;
                }
            }

            return current;
        }

        private static Transform FindChild(GameObject[] roots, string segment)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (Matches(roots[i].transform, segment))
                {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private static Transform FindChild(Transform parent, string segment)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (Matches(child, segment))
                {
                    return child;
                }
            }

            return null;
        }

        private static int ClampSlot(int slot)
        {
            return Mathf.Clamp(slot, 0, SlotCount - 1);
        }

        /// <summary>
        /// 旧版本只有一个 puzzle_escape_save.json；首次访问存档系统时迁移到第一个空槽位。
        /// </summary>
        private static void EnsureLegacyMigrated()
        {
            if (legacyChecked)
            {
                return;
            }

            legacyChecked = true;
            string legacyPath = Path.Combine(Application.persistentDataPath, LegacyFileName);
            if (!File.Exists(legacyPath))
            {
                return;
            }

            for (int i = 0; i < SlotCount; i++)
            {
                string destination = GetSlotPath(i);
                if (File.Exists(destination))
                {
                    continue;
                }

                try
                {
                    File.Copy(legacyPath, destination, false);
                    File.Delete(legacyPath);
                    Debug.Log("[Save] 旧存档已迁移到槽位 " + (i + 1) + "。");
                }
                catch (System.Exception exception)
                {
                    Debug.LogWarning("[Save] 旧存档迁移失败：" + exception.Message);
                }

                return;
            }
        }

        private static bool Matches(Transform transform, string segment)
        {
            int hash = segment.LastIndexOf('#');
            if (hash < 0)
            {
                return transform.name == segment;
            }

            string name = segment.Substring(0, hash);
            int index;
            if (!int.TryParse(segment.Substring(hash + 1), out index))
            {
                return false;
            }

            return transform.name == name && transform.GetSiblingIndex() == index;
        }
    }

    /// <summary>
    /// 存档槽位的只读摘要，用于存档界面显示；读取失败时 Corrupt 为 true。
    /// </summary>
    public class SaveSlotInfo
    {
        public int Slot;
        public bool Exists;
        public bool Corrupt;
        public bool Active;
        public float Elapsed;
        public int Notes;
        public int Puzzles;
        public int Rooms;
        public string SavedAt;
        public string MapId;
    }
}
