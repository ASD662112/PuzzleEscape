using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 一张可选地图的静态信息：主界面文案、对应场景、出生点与开场目标。
    /// 新增地图时在 <see cref="GameMapCatalog.Maps"/> 里追加条目，并把对应场景加入 Build Settings。
    /// </summary>
    public class GameMap
    {
        public string Id;
        public string Title;
        public string TitleEn;
        public string Description;
        public string SceneName;
        public Vector3 SpawnPosition;
        public float SpawnYaw;

        /// <summary>开场目标；为空时使用 GameManager 的主线目标链（庄园）。</summary>
        public string Objective;

        public bool Available;
    }

    public static class GameMapCatalog
    {
        public const string PrefsKey = "PuzzleEscape.SelectedMap";

        public const string MansionId = "mansion";
        public const string SanatoriumId = "sanatorium";

        public static readonly GameMap[] Maps =
        {
            new GameMap
            {
                Id = MansionId,
                Title = "谜宅逃脱",
                TitleEn = "P U Z Z L E   E S C A P E",
                Description = "你在一座两层庄园的书房里醒来。\n找到日、月、星、影四枚徽记，打开西侧的封印大门。",
                SceneName = "Main",
                SpawnPosition = new Vector3(-4.656f, 0.5f, 4.921f),
                SpawnYaw = 180f,
                Objective = null,
                Available = true
            },
            new GameMap
            {
                Id = SanatoriumId,
                Title = "夜巡疗养院",
                TitleEn = "S A N A T O R I U M",
                Description = "停电后的疗养院只剩应急灯。\n四层病房与一间地下室，每一间都留着没写完的记录。",
                SceneName = "Sanatorium",
                SpawnPosition = new Vector3(0f, 0.5f, -0.2f),
                SpawnYaw = 0f,
                Objective = "在 1F 南侧 1 号病房的柜子里找到二层通行卡",
                Available = true
            }
        };

        /// <summary>当前选中的地图；没有配置或配置失效时回退到第一张。</summary>
        public static GameMap Current
        {
            get
            {
                return Get(PlayerPrefs.GetString(PrefsKey, Maps[0].Id));
            }
        }

        public static GameMap Get(string id)
        {
            foreach (GameMap map in Maps)
            {
                if (map.Id == id && map.Available)
                {
                    return map;
                }
            }

            return Maps[0];
        }

        public static string CurrentId => Current.Id;

        public static void Select(string id)
        {
            PlayerPrefs.SetString(PrefsKey, Get(id).Id);
            PlayerPrefs.Save();
        }

        public static GameMap ForScene(string sceneName)
        {
            GameMap map = FindForScene(sceneName);
            return map != null ? map : Maps[0];
        }

        /// <summary>按场景名查找地图；找不到返回 null（用于区分“没匹配到”和“第一张图”）。</summary>
        public static GameMap FindForScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                return null;
            }

            foreach (GameMap map in Maps)
            {
                if (map.SceneName == sceneName)
                {
                    return map;
                }
            }

            return null;
        }
    }

    /// <summary>跨场景的一次性启动意图：从主界面切换地图后自动跳过主界面直接开始。</summary>
    public static class GameSession
    {
        public static bool AutoStartNewGame;
    }
}
