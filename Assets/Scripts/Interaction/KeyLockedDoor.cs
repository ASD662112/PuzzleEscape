using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 需要指定通行卡/钥匙才能推开的门。
    /// 没有道具时只提示，不消耗道具；有道具则直接开门。
    /// </summary>
    public class KeyLockedDoor : Interactable
    {
        public HingedDoor Door;
        public string RequiredItemId;
        public string RequiredItemName = "通行卡";
        public string LockedHint = "门锁着，需要对应的通行卡。";

        public override string Prompt => "检查门锁";

        public override bool CanInteract => Door != null && !Door.IsOpen;

        public override void Interact(PlayerInteractor interactor)
        {
            PlayerInventory inventory = interactor != null
                ? interactor.GetComponent<PlayerInventory>()
                : null;

            if (inventory != null && !string.IsNullOrEmpty(RequiredItemId) &&
                inventory.Contains(RequiredItemId))
            {
                Door.Open();
                bool isCard = RequiredItemId.StartsWith("card_");
                GameManager.Instance?.Toast(isCard ? "刷卡成功，门开了。" : "钥匙转动，门开了。");
                return;
            }

            GameManager.Instance?.Toast(LockedHint + "（需要：" + RequiredItemName + "）");
            AudioManager.Instance?.PlayError();
        }
    }

    /// <summary>
    /// 一层正门：既需要主门钥匙，也需要门厅恢复供电，打开后触发通关。
    /// </summary>
    public class LockedExitDoor : Interactable
    {
        public HingedDoor Door;
        public string RequiredItemId = "main_key";
        public string RequiredItemName = "主门钥匙";
        public string LockedHint = "大门被反锁了。";
        public string PowerHint = "门禁没有电，按下去只有继电器空响。得先合上 B1 的总闸。";
        public bool RequirePower = true;

        public override string Prompt => "打开大门";

        public override bool CanInteract => Door != null && !Door.IsOpen;

        public override void Interact(PlayerInteractor interactor)
        {
            if (RequirePower && GameManager.Instance != null && !GameManager.Instance.PowerOn)
            {
                GameManager.Instance.Toast(PowerHint);
                AudioManager.Instance?.PlayError();
                return;
            }

            PlayerInventory inventory = interactor != null
                ? interactor.GetComponent<PlayerInventory>()
                : null;

            if (inventory == null || string.IsNullOrEmpty(RequiredItemId) ||
                !inventory.Contains(RequiredItemId))
            {
                GameManager.Instance?.Toast(LockedHint + "（需要：" + RequiredItemName + "）");
                AudioManager.Instance?.PlayError();
                return;
            }

            Door.Open();
            GameManager.Instance?.Win();
        }
    }
}
