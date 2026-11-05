using System;
using System.Collections.Generic;
using UnityEngine;

namespace PuzzleEscape.Core
{
    public class AchievementDefinition
    {
        public string Id;
        public string Title;
        public string Description;
    }

    /// <summary>
    /// 跨存档的成就与统计：数据保存在 PlayerPrefs 里，换存档不会丢失。
    /// 由 Bootstrap 挂到常驻对象上，各系统通过 Notify* 接口上报事件。
    /// </summary>
    public class AchievementSystem : MonoBehaviour
    {
        public static AchievementSystem Instance { get; private set; }

        public static readonly AchievementDefinition[] Definitions =
        {
            new AchievementDefinition { Id = "first_escape", Title = "初次逃脱", Description = "第一次成功离开封闭的宅邸。" },
            new AchievementDefinition { Id = "flawless", Title = "全身而退", Description = "在一局中没有被怪物抓住并成功逃脱。" },
            new AchievementDefinition { Id = "silent", Title = "无声潜行", Description = "一局中从未奔跑，仍然逃了出去。" },
            new AchievementDefinition { Id = "demon", Title = "恶魔幸存者", Description = "在恶魔难度下成功逃脱。" },
            new AchievementDefinition { Id = "speed", Title = "与时间赛跑", Description = "在 15 分钟内完成一次逃脱。" },
            new AchievementDefinition { Id = "close_call", Title = "九死一生", Description = "在同一局里被抓两次后仍然逃脱。" },
            new AchievementDefinition { Id = "two_maps", Title = "两处噩梦", Description = "庄园与疗养院都成功逃脱过。" },
            new AchievementDefinition { Id = "veteran", Title = "老练的逃亡者", Description = "累计逃脱 3 次。" },
            new AchievementDefinition { Id = "collector", Title = "收藏家", Description = "累计收集 35 种不同的道具。" },
            new AchievementDefinition { Id = "hoarder", Title = "搜刮成性", Description = "累计打开 50 个柜子或抽屉。" },
            new AchievementDefinition { Id = "hider", Title = "柜中常客", Description = "累计躲进藏身处 10 次。" },
            new AchievementDefinition { Id = "codebreaker", Title = "破译者", Description = "累计解开 5 个密码锁。" },
            new AchievementDefinition { Id = "first_catch", Title = "第一次失手", Description = "被怪物抓住一次。" },
            new AchievementDefinition { Id = "careless", Title = "执迷不悟", Description = "累计被怪物抓住 10 次。" },
            new AchievementDefinition { Id = "night_owl", Title = "夜行者", Description = "累计游玩 1 小时。" }
        };

        /// <summary>解锁时触发，用于弹出提示。</summary>
        public event Action<AchievementDefinition> Unlocked;

        private const string KeyPrefix = "PuzzleEscape.Achievement.";
        private const float SaveInterval = 20f;

        private int totalEscapes;
        private int totalDeaths;
        private int totalCaught;
        private int totalContainers;
        private int totalPickups;
        private int totalHides;
        private int totalKeypads;
        private float totalPlaySeconds;

        private readonly HashSet<string> collectedItems = new HashSet<string>();
        private readonly HashSet<string> escapedMaps = new HashSet<string>();
        private readonly HashSet<string> unlocked = new HashSet<string>();

        private bool runSprinted;
        private float nextSaveTime;

        public int TotalEscapes => totalEscapes;
        public int TotalDeaths => totalDeaths;
        public int TotalCaught => totalCaught;
        public int TotalContainers => totalContainers;
        public int TotalPickups => totalPickups;
        public int TotalHides => totalHides;
        public int TotalKeypads => totalKeypads;
        public float TotalPlaySeconds => totalPlaySeconds;
        public int CollectedItemCount => collectedItems.Count;

        public int UnlockedCount
        {
            get
            {
                int count = 0;
                for (int i = 0; i < Definitions.Length; i++)
                {
                    if (unlocked.Contains(Definitions[i].Id))
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsUnlocked(string id)
        {
            return !string.IsNullOrEmpty(id) && unlocked.Contains(id);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Load();
            nextSaveTime = Time.unscaledTime + SaveInterval;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Save();
                Instance = null;
            }
        }

        private void OnApplicationQuit()
        {
            Save();
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && game.HasStarted && !game.IsWon && !game.IsLost)
            {
                totalPlaySeconds += Time.unscaledDeltaTime;
            }

            if (Time.unscaledTime >= nextSaveTime)
            {
                nextSaveTime = Time.unscaledTime + SaveInterval;
                Evaluate();
                Save();
            }
        }

        // ------------------------------------------------------------ 事件上报

        /// <summary>新的一局开始（重置本局临时状态）。</summary>
        public void NotifyRunStarted()
        {
            runSprinted = false;
        }

        public void NotifySprint()
        {
            runSprinted = true;
        }

        public void NotifyItemPicked(string itemId)
        {
            totalPickups++;

            if (!string.IsNullOrEmpty(itemId))
            {
                collectedItems.Add(itemId);
            }

            Evaluate();
            ScheduleSave();
        }

        public void NotifyContainerOpened()
        {
            totalContainers++;
            Evaluate();
            ScheduleSave();
        }

        public void NotifyHiding()
        {
            totalHides++;
            Evaluate();
            ScheduleSave();
        }

        public void NotifyKeypadSolved()
        {
            totalKeypads++;
            Evaluate();
            ScheduleSave();
        }

        public void NotifyCaught()
        {
            totalCaught++;
            Evaluate();
            ScheduleSave();
        }

        public void NotifyLost()
        {
            totalDeaths++;
            Evaluate();
            Save();
        }

        /// <summary>一局通关：结算与"单局"相关的成就。</summary>
        public void NotifyWin(string mapId, GameDifficulty difficulty, float elapsedSeconds, int catches)
        {
            totalEscapes++;

            if (!string.IsNullOrEmpty(mapId))
            {
                escapedMaps.Add(mapId);
            }

            if (catches <= 0)
            {
                Unlock("flawless");
            }

            if (!runSprinted)
            {
                Unlock("silent");
            }

            if (difficulty == GameDifficulty.Demon)
            {
                Unlock("demon");
            }

            if (elapsedSeconds <= 900f)
            {
                Unlock("speed");
            }

            if (catches >= 2)
            {
                Unlock("close_call");
            }

            Evaluate();
            Save();
        }

        // ------------------------------------------------------------ 内部

        private void Evaluate()
        {
            if (totalEscapes >= 1)
            {
                Unlock("first_escape");
            }

            if (totalEscapes >= 3)
            {
                Unlock("veteran");
            }

            if (escapedMaps.Count >= 2)
            {
                Unlock("two_maps");
            }

            if (collectedItems.Count >= 35)
            {
                Unlock("collector");
            }

            if (totalContainers >= 50)
            {
                Unlock("hoarder");
            }

            if (totalHides >= 10)
            {
                Unlock("hider");
            }

            if (totalKeypads >= 5)
            {
                Unlock("codebreaker");
            }

            if (totalCaught >= 1)
            {
                Unlock("first_catch");
            }

            if (totalCaught >= 10)
            {
                Unlock("careless");
            }

            if (totalPlaySeconds >= 3600f)
            {
                Unlock("night_owl");
            }
        }

        private void Unlock(string id)
        {
            if (string.IsNullOrEmpty(id) || unlocked.Contains(id))
            {
                return;
            }

            unlocked.Add(id);
            PlayerPrefs.SetInt(KeyPrefix + "Unlocked." + id, 1);

            AchievementDefinition definition = Find(id);
            if (definition != null)
            {
                Debug.Log("[PuzzleEscape] 成就解锁：" + definition.Title);
                Unlocked?.Invoke(definition);
            }

            Save();
        }

        private static AchievementDefinition Find(string id)
        {
            for (int i = 0; i < Definitions.Length; i++)
            {
                if (Definitions[i].Id == id)
                {
                    return Definitions[i];
                }
            }

            return null;
        }

        private void ScheduleSave()
        {
            if (Time.unscaledTime >= nextSaveTime - SaveInterval * 0.5f)
            {
                Save();
            }
        }

        private void Save()
        {
            PlayerPrefs.SetInt(KeyPrefix + "Escapes", totalEscapes);
            PlayerPrefs.SetInt(KeyPrefix + "Deaths", totalDeaths);
            PlayerPrefs.SetInt(KeyPrefix + "Caught", totalCaught);
            PlayerPrefs.SetInt(KeyPrefix + "Containers", totalContainers);
            PlayerPrefs.SetInt(KeyPrefix + "Pickups", totalPickups);
            PlayerPrefs.SetInt(KeyPrefix + "Hides", totalHides);
            PlayerPrefs.SetInt(KeyPrefix + "Keypads", totalKeypads);
            PlayerPrefs.SetFloat(KeyPrefix + "PlaySeconds", totalPlaySeconds);
            PlayerPrefs.SetString(KeyPrefix + "Items", Join(collectedItems));
            PlayerPrefs.SetString(KeyPrefix + "Maps", Join(escapedMaps));

            foreach (string id in unlocked)
            {
                PlayerPrefs.SetInt(KeyPrefix + "Unlocked." + id, 1);
            }

            PlayerPrefs.Save();
        }

        private void Load()
        {
            totalEscapes = PlayerPrefs.GetInt(KeyPrefix + "Escapes", 0);
            totalDeaths = PlayerPrefs.GetInt(KeyPrefix + "Deaths", 0);
            totalCaught = PlayerPrefs.GetInt(KeyPrefix + "Caught", 0);
            totalContainers = PlayerPrefs.GetInt(KeyPrefix + "Containers", 0);
            totalPickups = PlayerPrefs.GetInt(KeyPrefix + "Pickups", 0);
            totalHides = PlayerPrefs.GetInt(KeyPrefix + "Hides", 0);
            totalKeypads = PlayerPrefs.GetInt(KeyPrefix + "Keypads", 0);
            totalPlaySeconds = PlayerPrefs.GetFloat(KeyPrefix + "PlaySeconds", 0f);

            SplitInto(collectedItems, PlayerPrefs.GetString(KeyPrefix + "Items", string.Empty));
            SplitInto(escapedMaps, PlayerPrefs.GetString(KeyPrefix + "Maps", string.Empty));

            for (int i = 0; i < Definitions.Length; i++)
            {
                if (PlayerPrefs.GetInt(KeyPrefix + "Unlocked." + Definitions[i].Id, 0) == 1)
                {
                    unlocked.Add(Definitions[i].Id);
                }
            }
        }

        private static string Join(HashSet<string> values)
        {
            return string.Join("|", values);
        }

        private static void SplitInto(HashSet<string> target, string raw)
        {
            target.Clear();

            if (string.IsNullOrEmpty(raw))
            {
                return;
            }

            foreach (string part in raw.Split('|'))
            {
                if (!string.IsNullOrEmpty(part))
                {
                    target.Add(part);
                }
            }
        }
    }
}
