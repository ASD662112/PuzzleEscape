using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 玩家丢弃的物品：先按刚体向前抛出、受重力落地，停稳后再恢复拾取与旋转浮动。
    /// </summary>
    public class DroppedItemPickup : MonoBehaviour
    {
        public string ItemId { get; private set; }

        public bool IsSettled { get; private set; }

        private PickupItem pickup;
        private WorldItemSpin spin;
        private Rigidbody body;
        private float elapsed;
        private float stillTime;

        public void Initialize(PickupItem pickupItem, WorldItemSpin spinItem)
        {
            pickup = pickupItem;
            spin = spinItem;
            body = GetComponent<Rigidbody>();
            ItemId = pickupItem != null ? pickupItem.ItemId : string.Empty;
        }

        /// <summary>读档恢复的掉落物直接处于静止可拾取状态。</summary>
        public void MarkSettled()
        {
            IsSettled = true;

            if (pickup != null)
            {
                pickup.enabled = true;
            }

            if (spin != null)
            {
                spin.enabled = true;
            }
        }

        private void Update()
        {
            if (IsSettled || body == null)
            {
                return;
            }

            elapsed += Time.deltaTime;

            if (body.velocity.sqrMagnitude < 0.02f && body.angularVelocity.sqrMagnitude < 0.05f)
            {
                stillTime += Time.deltaTime;
            }
            else
            {
                stillTime = 0f;
            }

            // 停稳 0.25 秒，或超过 5 秒兜底，直接固定在地面上。
            if (stillTime > 0.25f || elapsed > 5f)
            {
                Settle();
            }
        }

        private void Settle()
        {
            IsSettled = true;

            if (body != null)
            {
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
            }

            if (pickup != null)
            {
                pickup.enabled = true;
            }

            if (spin != null)
            {
                spin.enabled = true;
            }
        }
    }
}
