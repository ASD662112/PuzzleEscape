using System.Collections;
using PuzzleEscape.Audio;
using PuzzleEscape.Core;
using PuzzleEscape.Player;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 需要黄铜钥匙才能打开的抽屉。打开后滑出并阅读里面的便签。
    /// </summary>
    public class LockedDrawer : Interactable
    {
        public Transform DrawerRoot;
        public string RequiredItemId = "brass_key";
        public string RequiredItemName = "黄铜钥匙";
        public Vector3 OpenOffset = new Vector3(0.5f, 0f, 0f);
        public string NoteTitle = "抽屉里的便签";
        [TextArea] public string NoteBody;
        public ItemSpawnPoint RewardSpawnPoint;

        private bool opened;
        private bool opening;

        public override string Prompt => opened ? "抽屉（已打开）" : "打开上锁的抽屉";
        public override bool CanInteract => !opened && !opening;
        public bool IsOpened => opened;

        public override void Interact(PlayerInteractor interactor)
        {
            PlayerInventory inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null || DrawerRoot == null)
            {
                return;
            }

            if (!inventory.Contains(RequiredItemId))
            {
                GameManager.Instance?.Toast("抽屉锁着。也许一把" + RequiredItemName + "能打开它。");
                AudioManager.Instance?.PlayError();
                return;
            }

            inventory.Remove(RequiredItemId);
            opened = true;

            Collider drawerCollider = GetComponent<Collider>();
            if (drawerCollider != null)
            {
                drawerCollider.enabled = false;
            }

            GameManager.Instance?.MarkDrawerOpened();
            AudioManager.Instance?.PlayDoor();
            StartCoroutine(OpenRoutine());
        }

        /// <summary>读取存档时恢复抽屉已打开状态，不自动弹出便签。</summary>
        public void RestoreOpened(bool value)
        {
            opened = value;
            opening = false;

            if (!opened)
            {
                return;
            }

            if (DrawerRoot != null)
            {
                DrawerRoot.localPosition += OpenOffset;
            }

            Collider drawerCollider = GetComponent<Collider>();
            if (drawerCollider != null)
            {
                drawerCollider.enabled = false;
            }
        }

        private IEnumerator OpenRoutine()
        {
            opening = true;
            Vector3 start = DrawerRoot.localPosition;
            Vector3 end = start + OpenOffset;
            float duration = 0.45f;

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                DrawerRoot.localPosition = Vector3.Lerp(start, end, t);
                yield return null;
            }

            DrawerRoot.localPosition = end;
            opening = false;

            RewardSpawnPoint?.Spawn();

            yield return new WaitForSecondsRealtime(0.3f);
            UIRoot.Instance?.ShowClue(NoteTitle, NoteBody);
        }
    }
}
