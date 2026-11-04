using System.Collections;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using PuzzleEscape.Puzzle;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 墙上的电闸。拉下后点亮房间、激活密码键盘。
    /// </summary>
    public class PowerLever : Interactable
    {
        public Transform HandlePivot;
        public Renderer Indicator;
        public KeypadPuzzle Keypad;
        public string RequiredItemId = "fuse";
        public string RequiredItemName = "保险丝";
        public bool ConsumeRequiredItem = true;
        public float PullAngle = -55f;

        private bool powered;
        private bool animating;

        public override string Prompt => powered ? "电闸（已开启）" : "检查电闸";
        public override bool CanInteract => !powered;

        public override void Interact(PlayerInteractor interactor)
        {
            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(RequiredItemId) && !inventory.Contains(RequiredItemId))
            {
                GameManager.Instance?.Toast("电闸里缺少" + RequiredItemName + "，无法接通电源。");
                AudioManager.Instance?.PlayError();
                return;
            }

            if (!string.IsNullOrEmpty(RequiredItemId) && ConsumeRequiredItem)
            {
                inventory.Remove(RequiredItemId);
            }

            powered = true;

            if (!animating && HandlePivot != null)
            {
                StartCoroutine(AnimateHandle());
            }

            GameManager.Instance?.SetPowerOn();
            RoomLighting.Instance?.SetPowered(true);
            Keypad?.SetPowered();
            SetIndicator(new Color(0.45f, 1f, 0.62f, 1f));
            AudioManager.Instance?.PlayPower();
        }

        private IEnumerator AnimateHandle()
        {
            animating = true;

            float duration = 0.35f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                HandlePivot.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(0f, PullAngle, t));
                yield return null;
            }

            HandlePivot.localRotation = Quaternion.Euler(0f, 0f, PullAngle);
            animating = false;
        }

        private void SetIndicator(Color color)
        {
            if (Indicator == null)
            {
                return;
            }

            Material material = Indicator.material;
            material.color = color;

            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * 0.9f);
            }
        }
    }
}
