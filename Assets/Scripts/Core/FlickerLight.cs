using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 地下室灯具的低频闪烁，营造不稳定电源的氛围。
    /// </summary>
    [RequireComponent(typeof(Light))]
    public class FlickerLight : MonoBehaviour
    {
        public float Amount = 0.35f;
        public float Speed = 7f;

        private Light target;
        private float baseIntensity;
        private float seed;

        private void Awake()
        {
            target = GetComponent<Light>();
            baseIntensity = target.intensity;
            seed = Random.Range(0f, 100f);
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            float noise = Mathf.PerlinNoise(seed + Time.time * Speed, 0f) * 2f - 1f;
            target.intensity = Mathf.Max(0.02f, baseIntensity + noise * Amount);
        }
    }
}
