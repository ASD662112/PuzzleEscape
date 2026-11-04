using UnityEngine;
using UnityEngine.EventSystems;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 手机端全屏视角拖拽层。位于所有按钮下方，拖拽增量交给相机。
    /// </summary>
    public class MobileLookPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public void OnPointerDown(PointerEventData eventData)
        {
            MobileInput.AddLookDelta(Vector2.zero);
        }

        public void OnDrag(PointerEventData eventData)
        {
            MobileInput.AddLookDelta(eventData.delta);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
        }
    }
}
