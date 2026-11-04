using UnityEngine;
using UnityEngine.EventSystems;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 自定义控制布局用：拖动目标 UI 元素，并把它的 anchoredPosition 限制在父矩形内。
    /// </summary>
    public class UIDragHandle : MonoBehaviour, IDragHandler
    {
        public RectTransform Target;

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            if (scale <= 0.0001f)
            {
                scale = 1f;
            }

            Target.anchoredPosition += eventData.delta / scale;
            Clamp();
        }

        private void Clamp()
        {
            RectTransform parent = Target.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Rect area = parent.rect;
            Vector2 size = Target.rect.size;
            Vector2 half = size * 0.5f;

            Vector2 anchorPoint = new Vector2(
                area.xMin + area.width * Target.anchorMin.x,
                area.yMin + area.height * Target.anchorMin.y);

            Vector2 pivotPosition = anchorPoint + Target.anchoredPosition;
            Vector2 center = pivotPosition + new Vector2(
                (0.5f - Target.pivot.x) * size.x,
                (0.5f - Target.pivot.y) * size.y);

            float minX = area.xMin + half.x;
            float maxX = area.xMax - half.x;
            float minY = area.yMin + half.y;
            float maxY = area.yMax - half.y;

            if (maxX < minX)
            {
                minX = maxX = area.center.x;
            }

            if (maxY < minY)
            {
                minY = maxY = area.center.y;
            }

            Vector2 clamped = new Vector2(
                Mathf.Clamp(center.x, minX, maxX),
                Mathf.Clamp(center.y, minY, maxY));

            Target.anchoredPosition += clamped - center;
        }
    }
}
