using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 疗养院的真实模型（Poly Haven CC0）摆放助手。
    /// 复用 MansionRealModels 的落地、缩放、碰撞体逻辑，保证不会浮空或陷进地面；
    /// 非编辑器环境下直接跳过（场景在构建前已经烘焙好）。
    /// </summary>
    internal static class SanatoriumProps
    {
        /// <summary>本次生成中成功摆放的真实模型数量。</summary>
        public static int Placed { get; private set; }

        public static void ResetCount()
        {
            Placed = 0;
        }

        /// <param name="parent">父节点（房间内容容器或走廊组）</param>
        /// <param name="modelId">Assets/Art/Models 下的模型文件夹名</param>
        /// <param name="localPosition">相对父节点的坐标，y = 0 表示落在当前层地面</param>
        /// <param name="yaw">相对父节点的朝向</param>
        /// <param name="height">目标世界高度，用于等比缩放</param>
        public static bool Place(Transform parent, string modelId, Vector3 localPosition,
            float yaw, float height, bool addCollider = true)
        {
#if UNITY_EDITOR
            if (parent == null || !MansionRealModels.Has(modelId))
            {
                return false;
            }

            Vector3 world = parent.TransformPoint(localPosition);
            GameObject spawned = MansionRealModels.Spawn(parent, modelId, world, yaw, height, addCollider);
            if (spawned == null)
            {
                return false;
            }

            Placed++;
            return true;
#else
            return false;
#endif
        }

        /// <summary>
        /// 按精确尺寸摆放：用于桌椅、柜子、书架这类必须匹配真实尺寸的家具，
        /// 避免按高度等比缩放后体积失真（比如书架变得过深、桌子过宽）。
        /// </summary>
        public static bool PlaceSized(Transform parent, string modelId, Vector3 localPosition,
            float yaw, Vector3 size, bool fitInside = true, bool addCollider = true)
        {
#if UNITY_EDITOR
            if (parent == null || !MansionRealModels.Has(modelId))
            {
                return false;
            }

            Vector3 world = parent.TransformPoint(localPosition);
            GameObject spawned = MansionRealModels.Spawn(parent, modelId, world, yaw, size.y, addCollider,
                0f, null, false, size, null, fitInside);
            if (spawned == null)
            {
                return false;
            }

            Placed++;
            return true;
#else
            return false;
#endif
        }
    }
}
