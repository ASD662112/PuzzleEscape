using System;
using System.Collections.Generic;
using System.Text;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Core
{
    public enum GameLanguage
    {
        ChineseSimplified = 0,
        English = 1,
        Japanese = 2,
        Korean = 3,
        ChineseTraditional = 4
    }

    /// <summary>
    /// 运行时本地化。所有文本以简体中文原文为键，从 Resources/Localization 读取译文；
    /// 缺少译文时回退中文，保证任何语言下都不会出现空白。
    /// 繁体中文使用 OpenCC 的常用字映射自动转换，不需要单独的翻译文件。
    /// </summary>
    public static class Localization
    {
        public const string PrefKey = "PuzzleEscape.Language";

        public static readonly string[] Names = { "简体中文", "English", "日本語", "한국어", "繁體中文" };

        /// <summary>每种语言对应的翻译文件（简体中文与繁体中文不读文件）。</summary>
        private static readonly string[] FileNames = { null, "en", "ja", "ko", null };

        private static bool initialized;
        private static bool tableLoaded;
        private static GameLanguage current;
        private static Dictionary<string, string> table;
        private static Dictionary<string, string> cache;
        private static Dictionary<char, char> traditionalMap;
        private static string[] prefixKeys;
        private static Dictionary<string, string> englishTable;
        private static string[] englishPrefixKeys;
        private static bool englishLoaded;
        private static bool missingFileWarned;

        /// <summary>语言切换后触发，供需要手动刷新的系统使用。</summary>
        public static event Action Changed;

        public static GameLanguage Current
        {
            get
            {
                EnsureInitialized();
                return current;
            }
        }

        public static void SetLanguage(GameLanguage language)
        {
            EnsureInitialized();
            language = (GameLanguage)Mathf.Clamp((int)language, 0, Names.Length - 1);

            if (current == language)
            {
                return;
            }

            current = language;
            PlayerPrefs.SetInt(PrefKey, (int)language);
            PlayerPrefs.Save();

            tableLoaded = false;
            cache = null;
            table = null;
            prefixKeys = null;
            LoadTable();

            Changed?.Invoke();
            LocalizedText.RefreshAll();
        }

        /// <summary>把简体中文原文翻译成当前语言；找不到时返回原文。</summary>
        public static string T(string source)
        {
            if (string.IsNullOrEmpty(source))
            {
                return source;
            }

            EnsureInitialized();

            if (current == GameLanguage.ChineseSimplified)
            {
                return source;
            }

            if (current == GameLanguage.ChineseTraditional)
            {
                return ToTraditional(source);
            }

            if (!tableLoaded)
            {
                LoadTable();
            }

            if (table == null || table.Count == 0)
            {
                return source;
            }

            if (cache != null && cache.TryGetValue(source, out string cached))
            {
                return cached;
            }

            string result = Translate(source, 0);

            // 日语/韩语还没翻译到的长文本先用英文兜底，避免直接跳回中文。
            if (result == source && current != GameLanguage.English)
            {
                result = TranslateEnglish(source, 0);
            }

            if (cache == null)
            {
                cache = new Dictionary<string, string>();
            }

            if (cache.Count > 4096)
            {
                cache.Clear();
            }

            cache[source] = result;
            return result;
        }

        /// <summary>先翻译模板，再用 string.Format 填入数值。</summary>
        public static string Format(string template, params object[] args)
        {
            string translated = T(template);
            if (args == null || args.Length == 0)
            {
                return translated;
            }

            try
            {
                return string.Format(translated, args);
            }
            catch (FormatException)
            {
                return translated;
            }
        }

        private static string Translate(string source, int depth)
        {
            return TranslateWith(source, table, prefixKeys, depth);
        }

        private static string TranslateEnglish(string source, int depth)
        {
            EnsureEnglishTable();
            if (englishTable == null || englishTable.Count == 0)
            {
                return source;
            }

            return TranslateWith(source, englishTable, englishPrefixKeys, depth);
        }

        private static string TranslateWith(string source, Dictionary<string, string> dict,
            string[] keys, int depth)
        {
            if (dict == null)
            {
                return source;
            }

            if (dict.TryGetValue(source, out string exact))
            {
                return exact;
            }

            // 纯英文/数字的字符串不需要再处理。
            if (!HasHan(source) || depth > 2)
            {
                return source;
            }

            string result = TranslateByPrefix(source, dict, keys, depth);
            if (result != null)
            {
                return result;
            }

            result = TranslateBySuffix(source, dict, keys, depth);
            return result ?? source;
        }

        private static string TranslateByPrefix(string source, Dictionary<string, string> dict,
            string[] keys, int depth)
        {
            if (keys == null)
            {
                return null;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (source.Length <= key.Length || !source.StartsWith(key, StringComparison.Ordinal))
                {
                    continue;
                }

                string translation = dict[key];
                string rest = source.Substring(key.Length);
                string restTranslated = TranslateWith(rest, dict, keys, depth + 1);

                // 组合里还剩中文说明这一段没翻到，交给上层做英文兜底，避免中英混杂。
                if (restTranslated == rest && HasHan(rest))
                {
                    return null;
                }

                return translation + restTranslated;
            }

            return null;
        }

        private static string TranslateBySuffix(string source, Dictionary<string, string> dict,
            string[] keys, int depth)
        {
            if (keys == null)
            {
                return null;
            }

            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i];
                if (source.Length <= key.Length || !source.EndsWith(key, StringComparison.Ordinal))
                {
                    continue;
                }

                string head = source.Substring(0, source.Length - key.Length);
                string headTranslated = TranslateWith(head, dict, keys, depth + 1);

                if (headTranslated == head && HasHan(head))
                {
                    return null;
                }

                return headTranslated + dict[key];
            }

            return null;
        }

        private static void BuildPrefixKeys()
        {
            prefixKeys = BuildKeys(table);
        }

        private static string[] BuildKeys(Dictionary<string, string> dict)
        {
            var keys = new List<string>(dict.Keys.Count);
            foreach (string key in dict.Keys)
            {
                // 太短的词容易把整句拆坏；至少两个字才参与组合翻译。
                if (key.Length >= 2 && HasHan(key))
                {
                    keys.Add(key);
                }
            }

            keys.Sort((a, b) => b.Length.CompareTo(a.Length));
            return keys.ToArray();
        }

        private static void EnsureEnglishTable()
        {
            if (englishLoaded)
            {
                return;
            }

            englishLoaded = true;
            TextAsset[] assets = Resources.LoadAll<TextAsset>("Localization/en");
            if (assets == null || assets.Length == 0)
            {
                return;
            }

            var loaded = new Dictionary<string, string>();
            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));
            foreach (TextAsset asset in assets)
            {
                if (asset != null)
                {
                    ParseEntries(asset.text, loaded);
                }
            }

            englishTable = loaded;
            englishPrefixKeys = BuildKeys(loaded);
        }

        private static void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            int saved = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, Names.Length - 1);
            current = (GameLanguage)saved;
            LoadTable();
        }

        private static void LoadTable()
        {
            tableLoaded = true;
            table = null;
            prefixKeys = null;
            cache = null;

            if (current == GameLanguage.ChineseSimplified)
            {
                return;
            }

            if (current == GameLanguage.ChineseTraditional)
            {
                LoadTraditionalMap();
                return;
            }

            string fileName = FileNames[(int)current];
            if (string.IsNullOrEmpty(fileName))
            {
                return;
            }

            TextAsset[] assets = Resources.LoadAll<TextAsset>("Localization/" + fileName);
            if (assets == null || assets.Length == 0)
            {
                if (!missingFileWarned)
                {
                    missingFileWarned = true;
                    Debug.LogWarning("[PuzzleEscape] 缺少语言文件：Resources/Localization/" +
                                     fileName + "/，未翻译的文本将显示中文。");
                }

                return;
            }

            var loaded = new Dictionary<string, string>();

            Array.Sort(assets, (a, b) => string.CompareOrdinal(a.name, b.name));

            foreach (TextAsset asset in assets)
            {
                if (asset == null)
                {
                    continue;
                }

                ParseEntries(asset.text, loaded);
            }

            table = loaded;
            BuildPrefixKeys();

            Debug.Log("[PuzzleEscape] 语言已切换：" + Names[(int)current] + "，条目 " + loaded.Count + " 条。");
        }

        private static void ParseEntries(string content, Dictionary<string, string> target)
        {
            string[] lines = content.Split('\n');

            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int split = line.IndexOf('\t');
                if (split <= 0 || split >= line.Length - 1)
                {
                    continue;
                }

                string key = Unescape(line.Substring(0, split));
                string value = Unescape(line.Substring(split + 1));
                target[key] = value;
            }
        }

        private static void LoadTraditionalMap()
        {
            if (traditionalMap != null)
            {
                return;
            }

            TextAsset asset = Resources.Load<TextAsset>("Localization/zh_hant_chars");
            traditionalMap = new Dictionary<char, char>();

            if (asset == null)
            {
                return;
            }

            string[] lines = asset.text.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd('\r');
                if (line.Length < 3)
                {
                    continue;
                }

                int split = line.IndexOf('\t');
                if (split != 1 || split + 1 >= line.Length)
                {
                    continue;
                }

                traditionalMap[line[0]] = line[split + 1];
            }
        }

        private static string ToTraditional(string source)
        {
            if (traditionalMap == null)
            {
                LoadTraditionalMap();
            }

            if (traditionalMap == null || traditionalMap.Count == 0)
            {
                return source;
            }

            var builder = new StringBuilder(source.Length);
            foreach (char c in source)
            {
                builder.Append(traditionalMap.TryGetValue(c, out char traditional) ? traditional : c);
            }

            return builder.ToString();
        }

        private static bool HasHan(string text)
        {
            foreach (char c in text)
            {
                if (c >= '\u4e00' && c <= '\u9fff')
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>数据文件里用 \n、\t、\" 表示换行与引号。</summary>
        private static string Unescape(string text)
        {
            if (text.IndexOf('\\') < 0)
            {
                return text;
            }

            var builder = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '\\' || i + 1 >= text.Length)
                {
                    builder.Append(text[i]);
                    continue;
                }

                char next = text[i + 1];
                switch (next)
                {
                    case 'n':
                        builder.Append('\n');
                        i++;
                        break;
                    case 't':
                        builder.Append('\t');
                        i++;
                        break;
                    case '"':
                        builder.Append('"');
                        i++;
                        break;
                    case '\\':
                        builder.Append('\\');
                        i++;
                        break;
                    default:
                        builder.Append('\\');
                        break;
                }
            }

            return builder.ToString();
        }
    }
}
