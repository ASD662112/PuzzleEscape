using System.Collections;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 锅炉房里的地窖铁门。第一次使用地下室钥匙后永久解锁，
    /// 之后可以反复交互进出地下室。
    /// </summary>
    public class BasementHatch : Interactable
    {
        public Transform LidPivot;
        public Vector3 LidOpenEuler = new Vector3(-78f, 0f, 0f);
        public float Duration = 0.55f;
        public Vector3 Destination;
        public float DestinationYaw;
        public string RequiredItemId = ItemIds.BasementKey;
        public string RequiredItemName = "地下室钥匙";
        public bool ConsumeKey = true;
        public LightingZone Zone;

        private bool opened;
        private bool descending;
        private Quaternion lidClosedRotation;

        public override string Prompt => opened ? "进入地下室" : "打开地窖铁门";
        public override bool CanInteract => !descending;
        public bool IsOpened => opened;

        private void Awake()
        {
            if (LidPivot != null)
            {
                lidClosedRotation = LidPivot.localRotation;
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
            if (controller == null)
            {
                return;
            }

            // 已经用钥匙打开过：不再检查钥匙，直接反复进出。
            if (opened)
            {
                AudioManager.Instance?.PlayDoor();
                controller.Teleport(Destination, DestinationYaw);
                Zone?.Enter();
                GameManager.Instance?.Toast("你顺着梯子再次进入地下室。");
                return;
            }

            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(RequiredItemId) && !inventory.Contains(RequiredItemId))
            {
                GameManager.Instance?.Toast("地窖铁门锁着。需要一把" + RequiredItemName + "。");
                AudioManager.Instance?.PlayError();
                return;
            }

            if (!string.IsNullOrEmpty(RequiredItemId) && ConsumeKey)
            {
                inventory.Remove(RequiredItemId);
            }

            opened = true;
            descending = true;
            AudioManager.Instance?.PlayDoor();
            GameManager.Instance?.NotifyBasementOpened();
            StartCoroutine(OpenAndDescend(controller));
        }

        private IEnumerator OpenAndDescend(FirstPersonController controller)
        {
            if (LidPivot != null)
            {
                Quaternion start = LidPivot.localRotation;
                Quaternion end = start * Quaternion.Euler(LidOpenEuler);
                float duration = Mathf.Max(0.05f, Duration);

                for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
                {
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    LidPivot.localRotation = Quaternion.Slerp(start, end, t);
                    yield return null;
                }

                LidPivot.localRotation = end;
            }

            yield return new WaitForSeconds(0.35f);
            controller.Teleport(Destination, DestinationYaw);
            Zone?.Enter();
            descending = false;
            GameManager.Instance?.Toast("你顺着梯子爬进了黑暗的地下室。");
            AudioManager.Instance?.PlayPower();
        }

        /// <summary>读取存档时恢复地窖入口已解锁状态。</summary>
        public void RestoreOpened(bool value)
        {
            opened = value;
            descending = false;

            if (opened && LidPivot != null)
            {
                LidPivot.localRotation = lidClosedRotation * Quaternion.Euler(LidOpenEuler);
            }
        }
    }
}
