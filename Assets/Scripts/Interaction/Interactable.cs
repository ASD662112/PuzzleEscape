using PuzzleEscape.Player;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 所有可交互物体的基类。派生类只需要提供提示文案与交互逻辑。
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        public abstract string Prompt { get; }
        public virtual bool CanInteract => true;
        public abstract void Interact(PlayerInteractor interactor);
    }
}
