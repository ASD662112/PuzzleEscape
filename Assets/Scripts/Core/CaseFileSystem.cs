using System.Collections.Generic;
using PuzzleEscape.Interaction;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 案件档案：记录读过的笔记、解开的隐藏密码锁、使用过的提示与成就进度。
    /// 本系统只保存本周目内的探索记录，不负责场景/道具存档。
    /// </summary>
    public static class CaseFileSystem
    {
        public class Entry
        {
            public string Id;
            public string Title;
            public string Body;
            public string Category;
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly HashSet<string> noteIds = new HashSet<string>();
        private static readonly HashSet<string> puzzleIds = new HashSet<string>();
        private static readonly HashSet<string> secretIds = new HashSet<string>();
        private static readonly Dictionary<string, string[]> hints =
            new Dictionary<string, string[]>();

        public static int TotalNotes { get; private set; }
        public static int ReadNotes => noteIds.Count;
        public static int SolvedPuzzleCount => puzzleIds.Count;
        public static bool HintsUsed { get; private set; }
        public static IList<Entry> Entries => entries;

        public static bool HasSecret(string id)
        {
            return !string.IsNullOrEmpty(id) && secretIds.Contains(id);
        }

        public static event System.Action Changed;

        static CaseFileSystem()
        {
            hints["搜查书房，寻找线索"] = new[]
            {
                "先从书桌和书架开始搜。",
                "书桌上有便签，书架上有能拿走的东西。",
                "阅读书桌便签，并检查书架第二层的黄铜钥匙。"
            };
            hints["在书房书架上寻找黄铜钥匙"] = new[]
            {
                "钥匙应该就在书房里。",
                "检查书架，尤其是视线平齐的第二层。",
                "黄铜钥匙在书房西侧书架第二层。"
            };
            hints["用黄铜钥匙打开储藏室的抽屉"] = new[]
            {
                "储藏室在一层核心区。",
                "找一个上锁的抽屉。",
                "用黄铜钥匙打开储藏室的上锁抽屉，里面会有保险丝。"
            };
            hints["查看书房挂钟，记下四位密码"] = new[]
            {
                "书房里还有一件会走的东西。",
                "查看书桌上的便签和墙上的挂钟。",
                "挂钟停在 4:27，四位数密码是 0427。"
            };
            hints["把保险丝装进配电间的电闸"] = new[]
            {
                "需要先恢复庄园供电。",
                "配电间在一层核心区，检查电闸。",
                "把储藏室找到的保险丝装入配电间电闸。"
            };
            hints["前往东翼洗衣房，寻找地下室钥匙"] = new[]
            {
                "地下室钥匙不在核心区。",
                "去东翼的洗衣房看看。",
                "打开洗衣房洗衣柜的最下层，拿到地下室钥匙。"
            };
            hints["在锅炉房取得手电筒"] = new[]
            {
                "地下室很黑，需要照明。",
                "锅炉房里有一张小桌。",
                "锅炉房小桌上的手电筒会自动点亮。"
            };
            hints["用地下室钥匙打开锅炉房的地窖铁门"] = new[]
            {
                "地窖入口在锅炉房。",
                "找一扇需要钥匙的铁门。",
                "用地下室钥匙打开锅炉房地窖铁门，然后顺着梯子下去。"
            };
            hints["进入地下室机械间，找到电梯齿轮并修好电梯"] = new[]
            {
                "电梯缺少一枚传动齿轮。",
                "齿轮在地下室机械间的工作台上。",
                "拿到齿轮后，到东翼电梯厅面板安装。"
            };
            hints["前往东翼走廊尽头的电梯厅，乘电梯前往二层"] = new[]
            {
                "电梯在东翼走廊尽头。",
                "先在电梯厅面板安装齿轮。",
                "修复电梯后使用面板前往二层。"
            };
            hints["前往二层西端的封印大门"] = new[]
            {
                "封印大门在二层西端。",
                "需要日、月、星、影四枚徽记。",
                "把四枚徽记依次嵌入大门。"
            };
            hints["在档案室门旁输入挂钟密码"] = new[]
            {
                "档案室门上有一个密码键盘。",
                "密码来自书房挂钟。",
                "输入 0427。"
            };
            hints["在实验室按提示重启机关"] = new[]
            {
                "实验室里有一个颜色机关。",
                "按笔记里记录的颜色顺序操作。",
                "顺序是红 → 蓝 → 绿。"
            };
            hints["在一层展厅取得日之徽记"] = new[]
            {
                "日之徽记在一层展厅。",
                "检查展厅基座。",
                "拿走基座上的日之徽记。"
            };
            hints["在档案室取得月之徽记"] = new[]
            {
                "月之徽记在二层档案室。",
                "先输入 0427 打开档案室门。",
                "检查档案室基座上的月之徽记。"
            };
            hints["在实验室取得星之徽记"] = new[]
            {
                "星之徽记在实验室陈列柜里。",
                "先完成红 → 蓝 → 绿机关。",
                "机关完成后，陈列柜会刷新星之徽记。"
            };
            hints["返回地下室的封印密室，取得影之徽记"] = new[]
            {
                "影之徽记在地下室封印密室。",
                "从地窖梯子下去，沿地下室走廊东侧走。",
                "封印密室石台上放着影之徽记。"
            };
            hints["穿过封印大门，离开庄园"] = new[]
            {
                "大门已经打开。",
                "穿过西端的封印大门。",
                "继续向前走到出口区域即可通关。"
            };
            hints["你已逃出庄园"] = new[]
            {
                "你已经通关。",
                "可以重新开始，尝试收集全部笔记。",
                "也可以寻找隐藏密码锁，完成额外记录。"
            };
        }

        public static void ResetForNewGame()
        {
            entries.Clear();
            noteIds.Clear();
            puzzleIds.Clear();
            secretIds.Clear();
            TotalNotes = 0;
            HintsUsed = false;
            Changed?.Invoke();
        }

        public static void Initialize(int totalNotes)
        {
            TotalNotes = Mathf.Max(0, totalNotes);
            Changed?.Invoke();
        }

        public static void RecordNote(ClueInteractable clue)
        {
            if (clue == null)
            {
                return;
            }

            string id = string.IsNullOrEmpty(clue.ClueId) ? clue.Title : clue.ClueId;
            if (string.IsNullOrEmpty(id) || !noteIds.Add(id))
            {
                return;
            }

            AddEntry(id, clue.Title, clue.Body, "笔记");
        }

        public static void RecordSecret(string id, string title, string body)
        {
            string key = string.IsNullOrEmpty(id) ? title : id;
            if (string.IsNullOrEmpty(key) || !secretIds.Add(key))
            {
                return;
            }

            AddEntry(key, title, body, "秘密");
        }

        public static void RecordPuzzle(string id)
        {
            if (string.IsNullOrEmpty(id) || !puzzleIds.Add(id))
            {
                return;
            }

            Changed?.Invoke();
        }

        public static void MarkHintUsed()
        {
            if (HintsUsed)
            {
                return;
            }

            HintsUsed = true;
            Changed?.Invoke();
        }

        public static void CaptureSave(SaveGameData data)
        {
            if (data == null)
            {
                return;
            }

            data.CaseEntries.Clear();
            foreach (Entry entry in entries)
            {
                data.CaseEntries.Add(new CaseEntryState
                {
                    Id = entry.Id,
                    Title = entry.Title,
                    Body = entry.Body,
                    Category = entry.Category
                });
            }

            data.CaseNoteIds.Clear();
            data.CaseNoteIds.AddRange(noteIds);
            data.CaseSecretIds.Clear();
            data.CaseSecretIds.AddRange(secretIds);
            data.CasePuzzleIds.Clear();
            data.CasePuzzleIds.AddRange(puzzleIds);
            data.CaseHintsUsed = HintsUsed;
        }

        public static void RestoreSave(SaveGameData data)
        {
            if (data == null)
            {
                return;
            }

            entries.Clear();
            noteIds.Clear();
            secretIds.Clear();
            puzzleIds.Clear();

            if (data.CaseEntries != null)
            {
                foreach (CaseEntryState entry in data.CaseEntries)
                {
                    entries.Add(new Entry
                    {
                        Id = entry.Id,
                        Title = entry.Title,
                        Body = entry.Body,
                        Category = entry.Category
                    });
                }
            }

            if (data.CaseNoteIds != null)
            {
                foreach (string id in data.CaseNoteIds)
                {
                    noteIds.Add(id);
                }
            }

            if (data.CaseSecretIds != null)
            {
                foreach (string id in data.CaseSecretIds)
                {
                    secretIds.Add(id);
                }
            }

            if (data.CasePuzzleIds != null)
            {
                foreach (string id in data.CasePuzzleIds)
                {
                    puzzleIds.Add(id);
                }
            }

            HintsUsed = data.CaseHintsUsed;
            Changed?.Invoke();
        }

        public static string[] GetHints(string objective)
        {
            string[] result;
            if (!string.IsNullOrEmpty(objective) && hints.TryGetValue(objective, out result))
            {
                return result;
            }

            return new[]
            {
                "先观察当前房间，寻找便签、抽屉或异常物件。",
                "已读笔记会自动记录在档案里，可以打开档案查看。",
                "如果仍然卡住，按提示逐级查看；主线密码通常在便签或挂钟上。"
            };
        }

        public static string GetAchievementText()
        {
            GameManager game = GameManager.Instance;
            float elapsed = game != null ? Mathf.Max(0f, Time.time - game.StartedAt) : 0f;
            bool won = game != null && game.IsWon;
            bool gate = game != null && game.FinalGateUnlocked;

            return
                Mark(ReadNotes >= 8) + " " +
                Localization.Format("初窥门径  笔记 {0}/8", ReadNotes) + "\n" +
                Mark(ReadNotes >= 24) + " " +
                Localization.Format("庄园学者  笔记 {0}/24", ReadNotes) + "\n" +
                Mark(ReadNotes >= TotalNotes && TotalNotes > 0) + " " +
                Localization.Format("全部笔记  {0}/{1}", ReadNotes, TotalNotes) + "\n" +
                Mark(SolvedPuzzleCount >= 1) + " " +
                Localization.Format("第一把锁  隐藏密码锁 {0}/1", SolvedPuzzleCount) + "\n" +
                Mark(SolvedPuzzleCount >= 6) + " " +
                Localization.Format("解谜专家  隐藏密码锁 {0}/6", SolvedPuzzleCount) + "\n" +
                Mark(gate || won) + " " + Localization.T("四印归一  封印大门已开启") + "\n" +
                Mark(HasSecret("secret_passage")) + " " +
                Localization.T("暗道探索者  发现藏书室暗道") + "\n" +
                Mark(won && !HintsUsed) + " " + Localization.T("独立思考  不使用提示通关") + "\n" +
                Mark(won && elapsed <= 1800f) + " " + Localization.T("速通者  30 分钟内通关") + "\n";
        }

        private static string Mark(bool completed)
        {
            return completed ? "✓" : "○";
        }

        private static void AddEntry(string id, string title, string body, string category)
        {
            entries.Add(new Entry
            {
                Id = id,
                Title = string.IsNullOrEmpty(title) ? "未命名记录" : title,
                Body = body,
                Category = category
            });
            Changed?.Invoke();
        }
    }
}
