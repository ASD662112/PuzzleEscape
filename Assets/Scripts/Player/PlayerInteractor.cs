using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Interaction;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Player
{
    /// <summary>
    /// 从相机中心发射射线寻找可交互物，并在 HUD 上显示提示。
    /// </summary>
    public class PlayerInteractor : MonoBehaviour
    {
        public float Distance = 3f;

        private Transform cameraTransform;
        private Interactable current;

        public Interactable Current => current;

        public void Initialize(Transform camera)
        {
            cameraTransform = camera;
        }

        private void Update()
        {
            if (cameraTransform == null || UIRoot.Instance == null)
            {
                return;
            }

            bool blocked = UIRoot.Instance.IsModalOpen ||
                           (GameManager.Instance != null && GameManager.Instance.IsWon);

            if (blocked)
            {
                ClearCurrent();
                return;
            }

            PlayerHiding hiding = PlayerHiding.Instance;
            if (hiding != null && hiding.IsHidden)
            {
                current = null;
                string hint = UIRoot.Instance.IsMobileUI ? "离开藏身处" : "[F] 离开藏身处";
                UIRoot.Instance.ShowPrompt(hint);

                if (!UIRoot.Instance.IsMobileUI && Input.GetKeyDown(KeyCode.F))
                {
                    hiding.Exit();
                }

                return;
            }

            current = FindInteractable();

            if (current != null)
            {
                string prefix = UIRoot.Instance.IsMobileUI ? string.Empty : "[F] ";
                UIRoot.Instance.ShowPrompt(prefix + current.Prompt);

                if (!UIRoot.Instance.IsMobileUI && Input.GetKeyDown(KeyCode.F))
                {
                    RequestInteract();
                }
            }
            else
            {
                // 手里选中的是蜡烛时提示可以放到地上。
                ItemDefinition selected = UIRoot.Instance.SelectedItem;
                if (!UIRoot.Instance.IsMobileUI && selected != null && selected.Id == ItemIds.Candle)
                {
                    UIRoot.Instance.ShowPrompt("[鼠标右键] 放置蜡烛");
                }
                else
                {
                    UIRoot.Instance.HidePrompt();
                }
            }
        }

        /// <summary>
        /// 供手机端“交互”按钮调用。
        /// </summary>
        public void RequestInteract()
        {
            if (UIRoot.Instance == null || UIRoot.Instance.IsModalOpen)
            {
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.IsWon)
            {
                return;
            }

            // 躲藏状态下，手机端的交互按钮就是“离开藏身处”。
            PlayerHiding hiding = PlayerHiding.Instance;
            if (hiding != null && hiding.IsHidden)
            {
                hiding.Exit();
                return;
            }

            if (current == null || !current.isActiveAndEnabled || !current.CanInteract)
            {
                return;
            }

            current.Interact(this);
        }

        private Interactable FindInteractable()
        {
            if (!Physics.Raycast(cameraTransform.position, cameraTransform.forward, out RaycastHit hit, Distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return null;
            }

            Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
            if (interactable == null || !interactable.isActiveAndEnabled || !interactable.CanInteract)
            {
                return null;
            }

            return interactable;
        }

        private void ClearCurrent()
        {
            current = null;
            UIRoot.Instance.HidePrompt();
        }
    }
}
