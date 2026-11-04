using UnityEngine;

namespace PuzzleEscape.Player
{
    /// <summary>
    /// 估算玩家所在位置的受光强度（0 = 全暗，1 = 明亮），用于「阴影庇护」：
    /// 玩家站在暗处且没有开灯时，怪物的发现距离会缩短。
    /// 只统计附近的点光与聚光，不处理烘焙光照贴图，结果每 0.2 秒刷新一次。
    /// </summary>
    public static class PlayerLightExposure
    {
        private const float EvaluateInterval = 0.2f;
        private const float RefreshLightsInterval = 4f;

        /// <summary>累计亮度达到这个值就算「完全暴露」。</summary>
        private const float FullExposure = 2.2f;

        private static Light[] lights = new Light[0];
        private static float nextRefreshTime;
        private static float nextEvaluateTime;
        private static float cachedExposure;
        private static Vector3 cachedPosition = new Vector3(99999f, 99999f, 99999f);

        public static float Evaluate(Vector3 position)
        {
            if (Time.time < nextEvaluateTime && (position - cachedPosition).sqrMagnitude < 1f)
            {
                return cachedExposure;
            }

            nextEvaluateTime = Time.time + EvaluateInterval;
            cachedPosition = position;

            if (lights.Length == 0 || Time.time >= nextRefreshTime)
            {
                lights = Object.FindObjectsOfType<Light>();
                nextRefreshTime = Time.time + RefreshLightsInterval;
            }

            // 取胸口高度，避免地板上的灯把玩家整体算得过亮。
            Vector3 sample = position + Vector3.up * 1.1f;
            float total = 0f;

            for (int i = 0; i < lights.Length; i++)
            {
                Light light = lights[i];
                if (light == null || !light.isActiveAndEnabled || light.range <= 0.01f)
                {
                    continue;
                }

                if (light.type != LightType.Point && light.type != LightType.Spot)
                {
                    continue;
                }

                Vector3 delta = light.transform.position - sample;
                float distance = delta.magnitude;
                float range = light.range;

                if (distance >= range)
                {
                    continue;
                }

                if (light.type == LightType.Spot)
                {
                    float angle = Vector3.Angle(light.transform.forward, -delta.normalized);
                    if (angle > light.spotAngle * 0.5f)
                    {
                        continue;
                    }
                }

                float falloff = 1f - distance / range;
                total += light.intensity * falloff * falloff;
            }

            cachedExposure = Mathf.Clamp01(total / FullExposure);
            return cachedExposure;
        }
    }
}
