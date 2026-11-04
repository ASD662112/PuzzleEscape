using System.Collections;
using PuzzleEscape.Audio;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 绕铰链旋转的房门。默认打开 5 秒后自动关闭；AutoCloseDelay 设为 0 时保持开启。
    /// </summary>
    public class HingedDoor : MonoBehaviour
    {
        public Transform Pivot;
        public float OpenAngle = -105f;
        public float Duration = 0.9f;

        [Tooltip("开门后自动关闭的等待时间，0 表示不自动关闭。")]
        public float AutoCloseDelay = 5f;

        public bool IsOpen { get; private set; }

        private Quaternion closedRotation;

        public void Open()
        {
            if (IsOpen || Pivot == null)
            {
                return;
            }

            IsOpen = true;
            closedRotation = Pivot.localRotation;
            AudioManager.Instance?.PlayDoorOpen();
            StartCoroutine(RotateRoutine(closedRotation * Quaternion.Euler(0f, OpenAngle, 0f)));

            if (AutoCloseDelay > 0f)
            {
                StartCoroutine(AutoCloseRoutine());
            }
        }

        private IEnumerator AutoCloseRoutine()
        {
            yield return new WaitForSeconds(AutoCloseDelay);

            if (!IsOpen || Pivot == null)
            {
                yield break;
            }

            AudioManager.Instance?.PlayDoorClose();
            yield return RotateRoutine(closedRotation);
            IsOpen = false;
        }

        private IEnumerator RotateRoutine(Quaternion target)
        {
            Quaternion start = Pivot.localRotation;
            float duration = Mathf.Max(0.05f, Duration);

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                Pivot.localRotation = Quaternion.Slerp(start, target, t);
                yield return null;
            }

            Pivot.localRotation = target;
        }
    }
}
