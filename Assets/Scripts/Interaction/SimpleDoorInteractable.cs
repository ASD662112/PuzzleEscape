using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 普通室内门：按 F 推开，绕铰链旋开。
    /// </summary>
    public class SimpleDoorInteractable : Interactable
    {
        public HingedDoor Door;

        public override string Prompt => "推开房门";
        public override bool CanInteract => Door != null && !Door.IsOpen;

        public override void Interact(PlayerInteractor interactor)
        {
            Door?.Open();
        }
    }
}
