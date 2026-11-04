using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 世界物品的缓慢旋转与上下浮动，便于在场景中辨认。
    /// </summary>
    public class WorldItemSpin : MonoBehaviour
    {
        public float DegreesPerSecond = 55f;
        public float BobHeight = 0.025f;
        public float BobSpeed = 1.6f;

        private Vector3 startLocalPosition;

        private void Start()
        {
            startLocalPosition = transform.localPosition;
        }

        private void Update()
        {
            transform.Rotate(0f, DegreesPerSecond * Time.deltaTime, 0f, Space.World);
            transform.localPosition = startLocalPosition +
                                      Vector3.up * (Mathf.Sin(Time.time * BobSpeed) * BobHeight);
        }
    }
}
