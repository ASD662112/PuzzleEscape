using PuzzleEscape.Data;
using UnityEngine;

namespace PuzzleEscape.Interaction
{
    /// <summary>
    /// 放在地上的半截蜡烛：暖色点光照亮周围 5 米，燃烧时间与手持时共用同一根蜡烛的剩余时间，
    /// 燃尽后熄灭并消失。
    /// </summary>
    public class PlacedCandle : MonoBehaviour
    {
        public const float LightRange = 5f;

        private const float LightHeight = 0.14f;
        private const float BaseIntensity = 1.8f;
        private const float FadeSeconds = 6f;

        private Light flame;
        private float fuel;

        /// <param name="position">落地点（地面或桌面上）。</param>
        /// <param name="fuelSeconds">这根蜡烛剩余的可燃烧时间。</param>
        public static PlacedCandle Create(Vector3 position, float fuelSeconds)
        {
            var root = new GameObject("PlacedCandle");
            root.transform.position = position;

            ItemDefinition definition = ItemDatabase.Get(ItemIds.Candle);
            if (definition != null)
            {
                GameObject visual = ItemSpawnPoint.CreateVisual(root.transform, definition);
                if (visual != null)
                {
                    // 放下的蜡烛不再悬浮自转，也不需要碰撞体，避免把玩家顶开。
                    var spin = visual.GetComponent<WorldItemSpin>();
                    if (spin != null)
                    {
                        Destroy(spin);
                    }

                    foreach (Collider visualCollider in visual.GetComponentsInChildren<Collider>(true))
                    {
                        visualCollider.enabled = false;
                    }
                }
            }

            var lightGo = new GameObject("PlacedCandleLight");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, LightHeight, 0f);

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.63f, 0.36f);
            light.range = LightRange;
            light.intensity = BaseIntensity;
            light.shadows = LightShadows.None;

            var candle = root.AddComponent<PlacedCandle>();
            candle.flame = light;
            candle.fuel = Mathf.Max(1f, fuelSeconds);
            return candle;
        }

        private void Update()
        {
            fuel -= Time.deltaTime;

            if (fuel <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (flame == null)
            {
                return;
            }

            // 轻微摇曳；最后几秒逐渐变暗，提示即将燃尽。
            float flicker = 0.92f + Mathf.PerlinNoise(Time.time * 7f, 0.37f) * 0.16f;
            float fade = fuel < FadeSeconds ? Mathf.Clamp01(fuel / FadeSeconds) : 1f;
            flame.intensity = BaseIntensity * flicker * fade;
        }
    }
}
