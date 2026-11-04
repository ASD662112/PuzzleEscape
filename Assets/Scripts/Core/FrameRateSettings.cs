using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 帧率上限设置：默认 60 帧，可选 30 / 45 / 60 / 90 / 120 / 160 / 240。
    /// 通过 PlayerPrefs 持久化，启动与修改时立即应用。
    /// </summary>
    public static class FrameRateSettings
    {
        public const int DefaultFrameRate = 60;
        private const string PrefsKey = "PuzzleEscape.TargetFrameRate";

        public static readonly int[] Options = { 30, 45, 60, 90, 120, 160, 240 };

        /// <summary>读取已保存的帧率；存档缺失或不在挡位列表时回退到 60。</summary>
        public static int Load()
        {
            int saved = PlayerPrefs.GetInt(PrefsKey, DefaultFrameRate);
            return IsSupported(saved) ? saved : DefaultFrameRate;
        }

        public static bool IsSupported(int fps)
        {
            for (int i = 0; i < Options.Length; i++)
            {
                if (Options[i] == fps)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>启动时应用上次保存的帧率，不重复写存档。</summary>
        public static void ApplySaved()
        {
            Apply(Load());
        }

        /// <summary>按挡位顺序循环切换：direction 为 +1 或 -1。</summary>
        public static int Step(int direction)
        {
            int current = Load();
            int index = 0;

            for (int i = 0; i < Options.Length; i++)
            {
                if (Options[i] == current)
                {
                    index = i;
                    break;
                }
            }

            index = (index + direction + Options.Length) % Options.Length;
            Set(Options[index]);
            return Options[index];
        }

        public static void Set(int fps)
        {
            if (!IsSupported(fps))
            {
                fps = DefaultFrameRate;
            }

            PlayerPrefs.SetInt(PrefsKey, fps);
            PlayerPrefs.Save();
            Apply(fps);
        }

        private static void Apply(int fps)
        {
            Application.targetFrameRate = fps;
            QualitySettings.vSyncCount = 0;
        }
    }
}
