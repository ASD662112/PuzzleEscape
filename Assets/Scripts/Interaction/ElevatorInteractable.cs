using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Data;
using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 电梯面板：通电并修复传动齿轮后，把玩家传送到另一层的走廊。
    /// </summary>
    public class ElevatorInteractable : Interactable
    {
        public Vector3 Destination;
        public float DestinationYaw = 270f;
        public bool GoingUp = true;
        public string DisplayName = "电梯";

        public override string Prompt
        {
            get
            {
                if (GameManager.Instance == null || !GameManager.Instance.PowerOn)
                {
                    return "检查" + DisplayName + "（没有通电）";
                }

                if (!GameManager.Instance.ElevatorRepaired)
                {
                    return "检查" + DisplayName + "（传动箱缺少齿轮）";
                }

                return GoingUp
                    ? "使用" + DisplayName + "（前往二层）"
                    : "使用" + DisplayName + "（返回一层）";
            }
        }

        public override void Interact(PlayerInteractor interactor)
        {
            if (GameManager.Instance == null || !GameManager.Instance.PowerOn)
            {
                GameManager.Instance?.Toast("电梯没有通电，按钮毫无反应。");
                AudioManager.Instance?.PlayError();
                return;
            }

            if (!GameManager.Instance.ElevatorRepaired)
            {
                PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();

                if (inventory == null || !inventory.Contains(ItemIds.ElevatorGear))
                {
                    GameManager.Instance.Toast("电梯传动箱缺一枚齿轮。也许地下室能找到。");
                    AudioManager.Instance?.PlayError();
                    return;
                }

                inventory.Remove(ItemIds.ElevatorGear);
                GameManager.Instance.NotifyElevatorRepaired();
                GameManager.Instance.Toast("齿轮装好了，电梯恢复运转。");
                AudioManager.Instance?.PlaySuccess();
                return;
            }

            FirstPersonController controller = interactor.GetComponent<FirstPersonController>();
            if (controller == null)
            {
                return;
            }

            controller.Teleport(Destination, DestinationYaw);
            AudioManager.Instance?.PlayPower();

            if (GoingUp)
            {
                GameManager.Instance.NotifyElevatorUsed();
                GameManager.Instance.Toast("电梯抵达二层。");
            }
            else
            {
                GameManager.Instance.Toast("电梯返回一层。");
            }
        }
    }
}
