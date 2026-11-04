using System.Collections.Generic;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 四枚徽记的封印大门。集齐并嵌入后开门。
    /// </summary>
    public class SealGate : Interactable
    {
        public string[] RequiredItemIds =
        {
            ItemIds.SealSun,
            ItemIds.SealMoon,
            ItemIds.SealStar,
            ItemIds.SealShadow
        };
        public HingedDoor Door;
        public Renderer[] SealIndicators;

        private bool unlocked;

        private void Awake()
        {
            // 最终封印大门保持开启，避免玩家被关在门后无法继续。
            if (Door != null)
            {
                Door.AutoCloseDelay = 0f;
            }
        }

        public override string Prompt => unlocked ? "封印大门（已开启）" : "检查封印大门";
        public override bool CanInteract => !unlocked;
        public bool IsUnlocked => unlocked;

        public override void Interact(PlayerInteractor interactor)
        {
            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            var missing = new List<string>();
            foreach (string itemId in RequiredItemIds)
            {
                if (!inventory.Contains(itemId))
                {
                    ItemDefinition definition = ItemDatabase.Get(itemId);
                    missing.Add(definition != null ? definition.DisplayName : itemId);
                }
            }

            if (missing.Count > 0)
            {
                GameManager.Instance?.Toast("大门仍缺少：" + string.Join("、", missing.ToArray()));
                AudioManager.Instance?.PlayError();
                return;
            }

            foreach (string itemId in RequiredItemIds)
            {
                inventory.Remove(itemId);
            }

            unlocked = true;
            SetIndicators(new Color(0.4f, 1f, 0.6f, 1f));
            Door?.Open();
            GameManager.Instance?.ConsumeSeals();
            GameManager.Instance?.NotifyFinalGateUnlocked();
            AudioManager.Instance?.PlaySuccess();
        }

        private void SetIndicators(Color color)
        {
            if (SealIndicators == null)
            {
                return;
            }

            foreach (Renderer indicator in SealIndicators)
            {
                if (indicator == null)
                {
                    continue;
                }

                Material material = indicator.material;
                material.color = color;

                if (material.HasProperty("_EmissionColor"))
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", color * 0.9f);
                }
            }
        }

        /// <summary>读取存档时恢复大门已解锁状态。</summary>
        public void RestoreUnlocked(bool value)
        {
            unlocked = value;
            if (!unlocked)
            {
                return;
            }

            SetIndicators(new Color(0.4f, 1f, 0.6f, 1f));
            Door?.Open();
        }
    }
}
