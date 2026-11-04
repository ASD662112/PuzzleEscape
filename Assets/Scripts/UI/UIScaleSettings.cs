using UnityEngine;
using UnityEngine.UI;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 全局界面缩放：修改所有 ScaleWithScreenSize 画布的参考分辨率。
    /// 85% / 100% / 115% / 130% 四挡，用于无障碍与大屏阅读。
    /// </summary>
    public static class UIScaleSettings
    {
        private const string Key = "PuzzleEscape.UIScale";
        private static readonly float[] Levels = { 0.85f, 1f, 1.15f, 1.3f };

        public static float Current
        {
            get { return Mathf.Clamp(PlayerPrefs.GetFloat(Key, 1f), 0.85f, 1.3f); }
        }

        public static void ApplyAll()
        {
            float scale = Current;
            CanvasScaler[] scalers = Object.FindObjectsOfType<CanvasScaler>(true);
            for (int i = 0; i < scalers.Length; i++)
            {
                if (scalers[i] == null ||
                    scalers[i].uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
                {
                    continue;
                }

                scalers[i].referenceResolution = new Vector2(1920f / scale, 1080f / scale);
                scalers[i].matchWidthOrHeight = 0.5f;
            }
        }

        public static void Step(int direction)
        {
            float current = Current;
            int index = 1;
            for (int i = 0; i < Levels.Length; i++)
            {
                if (Mathf.Abs(Levels[i] - current) < 0.01f)
                {
                    index = i;
                    break;
                }
            }

            index = Mathf.Clamp(index + direction, 0, Levels.Length - 1);
            PlayerPrefs.SetFloat(Key, Levels[index]);
            PlayerPrefs.Save();
            ApplyAll();
        }

        public static int Percentage
        {
            get { return Mathf.RoundToInt(Current * 100f); }
        }
    }
}
