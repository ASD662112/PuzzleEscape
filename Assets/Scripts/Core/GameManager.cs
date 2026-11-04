using System;
using System.Collections.Generic;
using PuzzleEscape.Data;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 全局游戏状态：物品、楼层与封印大门的进度。
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public event Action<string> ObjectiveChanged;
        public event Action<string> ToastRequested;
        public event Action Won;
        public event Action Lost;

        public float StartedAt { get; private set; }
        public bool HasStarted { get; private set; }
        public string CurrentObjective { get; private set; } = "搜查书房，寻找线索";

        public bool DeskNoteRead { get; private set; }
        public bool KeyPicked { get; private set; }
        public bool DrawerOpened { get; private set; }
        public bool ClockRead { get; private set; }
        public bool PowerOn { get; private set; }

        /// <summary>总闸里已经装上的保险丝数量（需要三枚）。</summary>
        public int InstalledFuses { get; private set; }

        public const int RequiredFuses = 3;

        private readonly HashSet<string> installedFuseIds = new HashSet<string>();

        /// <summary>某一枚保险丝是否已经装进总闸。</summary>
        public bool HasInstalledFuse(string fuseId)
        {
            return !string.IsNullOrEmpty(fuseId) && installedFuseIds.Contains(fuseId);
        }
        public bool BasementKeyPicked { get; private set; }
        public bool LanternPicked { get; private set; }
        public bool BasementOpened { get; private set; }
        public bool ElevatorRepaired { get; private set; }
        public bool ElevatorUsed { get; private set; }
        public bool ArchiveUnlocked { get; private set; }
        public bool LabSolved { get; private set; }
        public bool FinalGateUnlocked { get; private set; }
        public bool IsWon { get; private set; }
        public bool IsLost { get; private set; }

        /// <summary>被怪物抓住的次数，达到上限即失败。</summary>
        public int CaughtCount { get; private set; }

        public const int MaxCatches = 3;
        public string EndingTitle { get; private set; } = "逃出庄园";
        public string EndingSubtitle { get; private set; } =
            "封印大门在你身后缓缓合拢，走廊尽头是清晨的光。";
        public string EndingRank { get; private set; } = "C";
        public float FinalElapsed { get; private set; }

        public bool DoorUnlocked => ArchiveUnlocked;
        public bool SealSunCollected { get; private set; }
        public bool SealMoonCollected { get; private set; }
        public bool SealStarCollected { get; private set; }
        public bool SealShadowCollected { get; private set; }

        public int SealCount =>
            (SealSunCollected ? 1 : 0) + (SealMoonCollected ? 1 : 0) +
            (SealStarCollected ? 1 : 0) + (SealShadowCollected ? 1 : 0);

        private bool mapObjectiveOverride;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            StartedAt = Time.time;
        }

        /// <summary>
        /// 非庄园地图使用自己的固定目标文案，避免套用庄园的主线任务链。
        /// </summary>
        public void SetMapObjective(string objective)
        {
            if (string.IsNullOrEmpty(objective))
            {
                return;
            }

            mapObjectiveOverride = true;
            CurrentObjective = objective;
            ObjectiveChanged?.Invoke(objective);
        }

        /// <summary>
        /// 切换地图时清掉「固定目标文案」。
        /// GameManager 是跨场景常驻的，从疗养院回到庄园时必须清掉，
        /// 否则庄园会一直显示疗养院的任务目标。
        /// </summary>
        public void ClearMapObjectiveOverride()
        {
            mapObjectiveOverride = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void NotifyClueRead(string clueId)
        {
            if (string.IsNullOrEmpty(clueId))
            {
                return;
            }

            if (clueId == "desk")
            {
                DeskNoteRead = true;
            }
            else if (clueId == "clock")
            {
                ClockRead = true;
            }

            RefreshObjective();
        }

        public void NotifyItemPicked(string itemId)
        {
            switch (itemId)
            {
                case ItemIds.BrassKey:
                    KeyPicked = true;
                    break;
                case ItemIds.BasementKey:
                    BasementKeyPicked = true;
                    break;
                case ItemIds.Lantern:
                    LanternPicked = true;
                    break;
                case ItemIds.SealSun:
                    SealSunCollected = true;
                    break;
                case ItemIds.SealMoon:
                    SealMoonCollected = true;
                    break;
                case ItemIds.SealStar:
                    SealStarCollected = true;
                    break;
                case ItemIds.SealShadow:
                    SealShadowCollected = true;
                    break;
            }

            RefreshObjective();
        }

        public void MarkDrawerOpened()
        {
            if (DrawerOpened)
            {
                return;
            }

            DrawerOpened = true;
            Toast("抽屉打开了，里面有一枚保险丝和一张便签。");
            RefreshObjective();
        }

        public void SetPowerOn(string message = null)
        {
            if (PowerOn)
            {
                return;
            }

            PowerOn = true;
            InstalledFuses = RequiredFuses;
            installedFuseIds.Clear();
            foreach (string fuseId in ItemIds.BreakerFuses)
            {
                installedFuseIds.Add(fuseId);
            }

            Toast(string.IsNullOrEmpty(message)
                ? "电源接通了，电梯与二层的密码锁恢复供电。"
                : message);
            RefreshObjective();
        }

        /// <summary>往总闸里装一枚保险丝；装够三枚自动通电。</summary>
        public bool InstallFuse(string fuseId)
        {
            if (PowerOn)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(fuseId))
            {
                installedFuseIds.Add(fuseId);
            }

            InstalledFuses = Mathf.Min(RequiredFuses,
                Mathf.Max(InstalledFuses + 1, installedFuseIds.Count));

            if (InstalledFuses >= RequiredFuses)
            {
                SetPowerOn("三枚保险丝归位，总闸合上——整栋楼的灯一格一格亮了起来。");
            }
            else
            {
                Toast("保险丝装好了（" + InstalledFuses + "/" + RequiredFuses +
                      "）。配电箱还差 " + (RequiredFuses - InstalledFuses) + " 枚。");
            }

            RefreshObjective();
            return true;
        }

        public void NotifyBasementOpened()
        {
            if (BasementOpened)
            {
                return;
            }

            BasementOpened = true;
            Toast("地窖铁门打开了，下面一片漆黑。");
            RefreshObjective();
        }

        public void NotifyElevatorRepaired()
        {
            if (ElevatorRepaired)
            {
                return;
            }

            ElevatorRepaired = true;
            Toast("电梯传动箱重新咬合，电梯可以使用了。");
            RefreshObjective();
        }

        public void NotifyElevatorUsed()
        {
            if (ElevatorUsed)
            {
                return;
            }

            ElevatorUsed = true;
            RefreshObjective();
        }

        public void UnlockDoor()
        {
            NotifyArchiveUnlocked();
        }

        public void NotifyArchiveUnlocked()
        {
            if (ArchiveUnlocked)
            {
                return;
            }

            ArchiveUnlocked = true;
            Toast("档案室的门锁解除了。");
            RefreshObjective();
        }

        public void NotifyLabSolved()
        {
            if (LabSolved)
            {
                return;
            }

            LabSolved = true;
            Toast("机关归位，陈列柜的玻璃罩打开了。");
            RefreshObjective();
        }

        public void NotifyFinalGateUnlocked()
        {
            if (FinalGateUnlocked)
            {
                return;
            }

            FinalGateUnlocked = true;
            Toast("三枚徽记嵌入大门，封印解除。");
            RefreshObjective();
        }

        /// <summary>
        /// 徽记嵌入大门后从物品栏消耗，同时清空持有标记。
        /// </summary>
        public void ConsumeSeals()
        {
            SealSunCollected = false;
            SealMoonCollected = false;
            SealStarCollected = false;
            SealShadowCollected = false;
            RefreshObjective();
        }

        public void Win()
        {
            Win(null, null);
        }

        /// <summary>指定结局文案（不同逃生路线用不同的结尾）。</summary>
        public void Win(string endingTitle, string endingSubtitle)
        {
            if (IsWon)
            {
                return;
            }

            FinalElapsed = Mathf.Max(0f, Time.time - StartedAt);
            bool truthEnding = CaseFileSystem.ReadNotes >= 20 && CaseFileSystem.SolvedPuzzleCount >= 6;

            if (!string.IsNullOrEmpty(endingTitle))
            {
                EndingTitle = endingTitle;
                EndingSubtitle = string.IsNullOrEmpty(endingSubtitle)
                    ? EndingSubtitle
                    : endingSubtitle;
            }
            else if (GameMapCatalog.CurrentId == GameMapCatalog.SanatoriumId)
            {
                EndingTitle = "走出疗养院";
                EndingSubtitle = "铁门在身后合上，夜雾漫过台阶，你终于没有再回头。";
            }
            else if (truthEnding)
            {
                EndingTitle = "真相结局";
                EndingSubtitle = "你带着庄园的秘密离开。封印并未消失，只是终于有人记住了它。";
            }
            else
            {
                EndingTitle = "逃脱结局";
                EndingSubtitle = "封印大门在你身后缓缓合拢，走廊尽头是清晨的光。";
            }

            EndingRank = CalculateRank(FinalElapsed);
            IsWon = true;

            if (mapObjectiveOverride)
            {
                CurrentObjective = "你已离开疗养院";
                ObjectiveChanged?.Invoke(CurrentObjective);
            }

            RefreshObjective();
            Won?.Invoke();
        }

        /// <summary>
        /// 记录一次被怪物抓住。返回 true 表示已经达到失败上限。
        /// </summary>
        public bool NotifyCaught()
        {
            if (IsLost)
            {
                return true;
            }

            CaughtCount++;
            if (CaughtCount >= MaxCatches)
            {
                Lose();
                return true;
            }

            return false;
        }

        private void Lose()
        {
            if (IsLost)
            {
                return;
            }

            FinalElapsed = Mathf.Max(0f, Time.time - StartedAt);
            EndingTitle = "你被抓住了";
            EndingSubtitle = "第三次失手，走廊的灯彻底熄灭，庄园重新安静下来。";
            IsLost = true;
            Lost?.Invoke();
        }

        private static string CalculateRank(float elapsed)
        {
            if (elapsed <= 900f)
            {
                return "S";
            }

            if (elapsed <= 1500f)
            {
                return "A";
            }

            if (elapsed <= 2100f)
            {
                return "B";
            }

            return "C";
        }

        public void Toast(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                ToastRequested?.Invoke(message);
            }
        }

        /// <summary>
        /// 主界面点击「开始游戏」时调用，让计时从进入庄园那一刻开始。
        /// </summary>
        public void StartTimer()
        {
            StartedAt = Time.time;
            HasStarted = true;
        }

        /// <summary>
        /// 读取存档时恢复进度；不触发 Toast 与通关事件。
        /// </summary>
        public void RestoreProgress(SaveGameData data)
        {
            if (data == null)
            {
                return;
            }

            DeskNoteRead = data.DeskNoteRead;
            KeyPicked = data.KeyPicked;
            DrawerOpened = data.DrawerOpened;
            ClockRead = data.ClockRead;
            PowerOn = data.PowerOn;
            InstalledFuses = data.PowerOn
                ? RequiredFuses
                : Mathf.Clamp(data.InstalledFuses, 0, RequiredFuses);

            installedFuseIds.Clear();
            for (int i = 0; i < InstalledFuses && i < ItemIds.BreakerFuses.Length; i++)
            {
                installedFuseIds.Add(ItemIds.BreakerFuses[i]);
            }
            BasementKeyPicked = data.BasementKeyPicked;
            LanternPicked = data.LanternPicked;
            BasementOpened = data.BasementOpened;
            ElevatorRepaired = data.ElevatorRepaired;
            ElevatorUsed = data.ElevatorUsed;
            ArchiveUnlocked = data.ArchiveUnlocked;
            LabSolved = data.LabSolved;
            FinalGateUnlocked = data.FinalGateUnlocked;
            SealSunCollected = data.SealSunCollected;
            SealMoonCollected = data.SealMoonCollected;
            SealStarCollected = data.SealStarCollected;
            SealShadowCollected = data.SealShadowCollected;

            IsWon = false;
            HasStarted = data.Started;
            StartedAt = Time.time - Mathf.Max(0f, data.Elapsed);
            RefreshObjective();
        }

        public void RefreshObjective()
        {
            if (mapObjectiveOverride)
            {
                return;
            }

            string objective;

            if (IsWon)
            {
                objective = "你已逃出庄园";
            }
            else if (FinalGateUnlocked)
            {
                objective = "穿过封印大门，离开庄园";
            }
            else if (!PowerOn)
            {
                if (!DeskNoteRead && !KeyPicked)
                {
                    objective = "搜查书房，寻找线索";
                }
                else if (!KeyPicked)
                {
                    objective = "在书房书架上寻找黄铜钥匙";
                }
                else if (!DrawerOpened)
                {
                    objective = "用黄铜钥匙打开储藏室的抽屉";
                }
                else if (!ClockRead)
                {
                    objective = "查看书房挂钟，记下四位密码";
                }
                else
                {
                    objective = "把保险丝装进配电间的电闸";
                }
            }
            else if (!BasementKeyPicked)
            {
                objective = "前往东翼洗衣房，寻找地下室钥匙";
            }
            else if (!LanternPicked)
            {
                objective = "在锅炉房取得手电筒";
            }
            else if (!BasementOpened)
            {
                objective = "用地下室钥匙打开锅炉房的地窖铁门";
            }
            else if (!ElevatorRepaired)
            {
                objective = "进入地下室机械间，找到电梯齿轮并修好电梯";
            }
            else if (!ElevatorUsed)
            {
                objective = "前往东翼走廊尽头的电梯厅，乘电梯前往二层";
            }
            else if (SealCount >= 4)
            {
                objective = "前往二层西端的封印大门";
            }
            else if (!ArchiveUnlocked)
            {
                objective = "在档案室门旁输入挂钟密码";
            }
            else if (!LabSolved)
            {
                objective = "在实验室按提示重启机关";
            }
            else if (!SealSunCollected)
            {
                objective = "在一层展厅取得日之徽记";
            }
            else if (!SealMoonCollected)
            {
                objective = "在档案室取得月之徽记";
            }
            else if (!SealStarCollected)
            {
                objective = "在实验室取得星之徽记";
            }
            else if (!SealShadowCollected)
            {
                objective = "返回地下室的封印密室，取得影之徽记";
            }
            else
            {
                objective = "前往二层西端的封印大门";
            }

            if (objective == CurrentObjective)
            {
                return;
            }

            CurrentObjective = objective;
            ObjectiveChanged?.Invoke(objective);
        }
    }
}
