using UnityEngine;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 手机端输入桥：虚拟摇杆写入移动向量，右侧拖拽写入视角增量。
    /// </summary>
    public static class MobileInput
    {
        public static Vector2 Move { get; set; }

        /// <summary>手机端奔跑模式开关，由 HUD 上的奔跑按钮切换。</summary>
        public static bool Sprint { get; set; }

        private static Vector2 lookDelta;

        public static void AddLookDelta(Vector2 delta)
        {
            lookDelta += delta;
        }

        public static Vector2 ConsumeLookDelta()
        {
            Vector2 delta = lookDelta;
            lookDelta = Vector2.zero;
            return delta;
        }

        public static void Reset()
        {
            Move = Vector2.zero;
            lookDelta = Vector2.zero;
        }
    }
}
