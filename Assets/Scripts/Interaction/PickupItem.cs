using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 可拾取的世界物品。实际展示模型由 ItemSpawnPoint 生成。
    /// </summary>
    public class PickupItem : Interactable
    {
        public string ItemId;
        public string DisplayName;

        private string ResolvedName
        {
            get
            {
                if (!string.IsNullOrEmpty(DisplayName))
                {
                    return DisplayName;
                }

                ItemDefinition definition = ItemDatabase.Get(ItemId);
                return definition != null ? definition.DisplayName : ItemId;
            }
        }

        public override string Prompt => "拾取 " + ResolvedName;

        public override void Interact(PlayerInteractor interactor)
        {
            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            if (!inventory.Add(ItemId))
            {
                GameManager.Instance?.Toast("物品栏已满，无法拾取。");
                AudioManager.Instance?.PlayError();
                return;
            }

            GameManager.Instance?.NotifyItemPicked(ItemId);
            GameManager.Instance?.Toast("获得物品 · " + ResolvedName);
            AudioManager.Instance?.PlayPickup();

            gameObject.SetActive(false);
        }
    }
}
