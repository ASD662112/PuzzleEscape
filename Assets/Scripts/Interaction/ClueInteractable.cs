using PuzzleEscape.Core;
using PuzzleEscape.Player;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 可阅读的纸面线索（便签、挂钟等）。
    /// </summary>
    public class ClueInteractable : Interactable
    {
        [TextArea] public string Body;
        public string Title;
        public string Label = "查看";
        public string ClueId;

        public override string Prompt => Label;

        public override void Interact(PlayerInteractor interactor)
        {
            CaseFileSystem.RecordNote(this);
            GameManager.Instance?.NotifyClueRead(ClueId);
            UIRoot.Instance?.ShowClue(Title, Body);
        }
    }
}
