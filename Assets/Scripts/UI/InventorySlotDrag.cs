using UnityEngine;
using UnityEngine.EventSystems;

namespace PuzzleEscape.UI
{
    /// <summary>
    /// 背包格子的点击与拖拽：点击查看详情，拖到另一个格子交换物品。
    /// </summary>
    public class InventorySlotDrag : MonoBehaviour, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerClickHandler
    {
        public UIRoot Owner;
        public int Index;

        private bool dragging;
        private bool suppressClick;

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = false;
            suppressClick = false;

            if (Owner != null)
            {
                dragging = Owner.BeginSlotDrag(Index, eventData.position);
            }

            if (dragging)
            {
                suppressClick = true;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging && Owner != null)
            {
                Owner.UpdateSlotDrag(eventData.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (dragging && Owner != null)
            {
                Owner.EndSlotDrag(eventData.position);
            }

            dragging = false;

            // 只有松手仍在原格子上时才会继续触发一次点击，需要把它吞掉。
            suppressClick = eventData.pointerCurrentRaycast.gameObject == gameObject;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // 拖拽结束后的抬起不应再触发“打开详情”。
            if (suppressClick)
            {
                suppressClick = false;
                return;
            }

            Owner?.ClickSlot(Index);
        }
    }
}
