using PuzzleEscape.Core;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 走廊尽头的逃脱触发区。
    /// </summary>
    public class ExitZone : MonoBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                GameManager.Instance?.Win();
            }
        }
    }
}
