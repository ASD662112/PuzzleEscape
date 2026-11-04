using UnityEngine;
using UnityEngine.EventSystems;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 圆形虚拟摇杆，输出归一化方向到 MobileInput.Move。
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public RectTransform Background;
        public RectTransform Knob;
        public float Radius = 78f;

        private Vector2 value;

        public void OnPointerDown(PointerEventData eventData)
        {
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Background == null || Knob == null)
            {
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            // 背景矩形的 pivot 在左下角，localPoint 是相对 pivot 的坐标。
            // 必须先减去矩形中心，否则点哪儿都会得到一个固定的「右上」偏移。
            Vector2 offset = localPoint - Background.rect.center;
            value = Vector2.ClampMagnitude(offset / Radius, 1f);
            Knob.anchoredPosition = value * Radius;
            MobileInput.Move = value;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            value = Vector2.zero;
            MobileInput.Move = Vector2.zero;

            if (Knob != null)
            {
                Knob.anchoredPosition = Vector2.zero;
            }
        }

        private void OnDisable()
        {
            value = Vector2.zero;
            MobileInput.Move = Vector2.zero;

            if (Knob != null)
            {
                Knob.anchoredPosition = Vector2.zero;
            }
        }
    }
}
