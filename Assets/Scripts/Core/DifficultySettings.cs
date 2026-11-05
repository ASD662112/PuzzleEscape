using UnityEngine;

namespace PuzzleEscape.Core
{
    public enum GameDifficulty
    {
        Peace = 0,
        Easy = 1,
        Normal = 2,
        Hard = 3,
        Demon = 4
    }

    /// <summary>
    /// 五档难度：和平 / 简单 / 正常 / 困难 / 恶魔。
    /// 困难沿用原有的怪物手感；恶魔追逐速度比玩家奔跑快十分之一；和平不生成怪物。
    /// 参数以玩家移动速度为基准换算，速度存在 PlayerPrefs 里。
    /// </summary>
    public static class DifficultySettings
    {
        public const string PrefKey = "PuzzleEscape.Difficulty";

        /// <summary>现实声音（麦克风）检测的开关，开始新游戏时和难度一起选定。</summary>
        public const string MicPrefKey = "PuzzleEscape.MicNoise";

        /// <summary>现实声音检测的触发阈值（0~190 dB）：超过这个响度怪物就会赶来。</summary>
        public const float NoiseThresholdDb = 40f;

        /// <summary>默认沿用之前的数值（困难）。</summary>
        public const GameDifficulty Default = GameDifficulty.Hard;

        public static readonly string[] Names = { "和平", "简单", "正常", "困难", "恶魔" };

        private static bool loaded;
        private static GameDifficulty current;
        private static bool micLoaded;
        private static bool micEnabled;

        /// <summary>每次难度变化自增，怪物用它判断是否需要刷新参数。</summary>
        public static int Revision { get; private set; }

        public static GameDifficulty Current
        {
            get
            {
                if (!loaded)
                {
                    Load();
                }

                return current;
            }
        }

        public static bool MonstersEnabled => Current != GameDifficulty.Peace;

        /// <summary>
        /// 本局是否用麦克风读取玩家现实中的音量。和平难度没有怪物，运行时不会真正监听。
        /// </summary>
        public static bool MicNoiseEnabled
        {
            get
            {
                if (!micLoaded)
                {
                    LoadMic();
                }

                return micEnabled;
            }
        }

        /// <summary>追逐速度相对玩家奔跑速度的比例。恶魔 = 1.1（快十分之一）。</summary>
        public static float ChaseRatio
        {
            get
            {
                switch (Current)
                {
                    case GameDifficulty.Easy:
                        return 0.72f;
                    case GameDifficulty.Normal:
                        return 0.8f;
                    case GameDifficulty.Demon:
                        return 1.1f;
                    case GameDifficulty.Peace:
                        return 0.7f;
                    default:
                        return 7f / 8f;
                }
            }
        }

        /// <summary>散步速度相对玩家步行速度的比例。</summary>
        public static float PatrolRatio
        {
            get
            {
                switch (Current)
                {
                    case GameDifficulty.Easy:
                        return 0.24f;
                    case GameDifficulty.Normal:
                        return 0.3f;
                    case GameDifficulty.Demon:
                        return 0.42f;
                    default:
                        return 1f / 3f;
                }
            }
        }

        /// <summary>怪物视野距离（米）。</summary>
        public static float ViewDistance
        {
            get
            {
                switch (Current)
                {
                    case GameDifficulty.Easy:
                        return 11f;
                    case GameDifficulty.Normal:
                        return 14f;
                    case GameDifficulty.Demon:
                        return 20f;
                    case GameDifficulty.Peace:
                        return 12f;
                    default:
                        return 16f;
                }
            }
        }

        public static void Set(GameDifficulty difficulty)
        {
            int index = Mathf.Clamp((int)difficulty, 0, Names.Length - 1);
            difficulty = (GameDifficulty)index;

            if (!loaded)
            {
                Load();
            }

            if (current == difficulty)
            {
                return;
            }

            current = difficulty;
            Revision++;
            PlayerPrefs.SetInt(PrefKey, index);
            PlayerPrefs.Save();
        }

        /// <summary>在五档之间前后切换，越界时停在两端。</summary>
        public static void Step(int delta)
        {
            Set((GameDifficulty)((int)Current + delta));
        }

        /// <summary>开始新游戏时和难度一起保存，本局内不再改变。</summary>
        public static void SetMicNoise(bool enabled)
        {
            if (!micLoaded)
            {
                LoadMic();
            }

            micEnabled = enabled;
            PlayerPrefs.SetInt(MicPrefKey, enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        private static void LoadMic()
        {
            micLoaded = true;
            micEnabled = PlayerPrefs.GetInt(MicPrefKey, 1) == 1;
        }

        private static void Load()
        {
            loaded = true;
            int saved = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, (int)Default), 0, Names.Length - 1);
            current = (GameDifficulty)saved;
        }
    }
}
