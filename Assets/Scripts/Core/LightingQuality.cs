using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 光影质量三挡设置：
    /// 1 低——沿用原有效果：点光源无实时阴影、环境光较亮、8 盏逐像素灯；
    /// 2 中——玩家附近 2 盏主灯开启软阴影，环境光降低、灯光更集中、16 盏逐像素灯；
    /// 3 高——附近 4 盏主灯软阴影 + 更高阴影分辨率与距离 + 更浓雾效 + 32 盏逐像素灯。
    /// 挡位之间（尤其低 → 高）的明暗对比、阴影与雾效差异明显。
    /// </summary>
    public class LightingQuality : MonoBehaviour
    {
        public const string PrefKey = "PuzzleEscape.LightingQuality";

        public static readonly string[] LevelNames = { "低", "中", "高" };

        public static LightingQuality Instance { get; private set; }

        /// <summary>1 / 2 / 3。</summary>
        public int Level { get; private set; } = 1;

        public event Action<int> Changed;

        private Light[] lights = new Light[0];
        private bool[] scalable = new bool[0];
        private bool[] shadowEligible = new bool[0];
        private float[] baseIntensity = new float[0];
        private Light[] candidates = new Light[0];

        private readonly List<Light> shadowed = new List<Light>();
        private Transform player;
        private float nextShadowRefresh;
        private bool initialized;
        private bool darkZone;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            Level = Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 1), 1, 3);
        }

        private void Start()
        {
            EnsureInitialized();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (Level <= 1)
            {
                return;
            }

            if (Time.time < nextShadowRefresh)
            {
                return;
            }

            nextShadowRefresh = Time.time + 0.4f;
            RefreshShadowLights(false);
        }

        public void EnsureInitialized()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            CaptureLights();
            ApplyToScene(true);
        }

        /// <summary>
        /// 切换挡位（1~3），立即应用并写入 PlayerPrefs。
        /// </summary>
        public void SetLevel(int level)
        {
            level = Mathf.Clamp(level, 1, 3);

            if (!initialized)
            {
                Level = level;
                PlayerPrefs.SetInt(PrefKey, level);
                PlayerPrefs.Save();
                EnsureInitialized();
                Changed?.Invoke(Level);
                return;
            }

            bool changed = level != Level;
            Level = level;
            PlayerPrefs.SetInt(PrefKey, level);
            PlayerPrefs.Save();

            if (changed)
            {
                ApplyToScene(true);
                Changed?.Invoke(Level);
            }
        }

        /// <summary>
        /// 地下室等黑暗区域由 LightingZone 调用：临时压低环境光与雾效，挡位变化时不要覆盖。
        /// </summary>
        public void EnterDarkZone(Color ambient, Color fogColor, float fogDensity)
        {
            darkZone = true;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = ambient;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogDensity = fogDensity;
        }

        public void ExitDarkZone()
        {
            darkZone = false;
            ApplySurfaceRendering();
        }

        private void CaptureLights()
        {
            Light[] found = UnityEngine.Object.FindObjectsOfType<Light>(true);
            var picked = new List<Light>();

            foreach (Light light in found)
            {
                if (light == null || light.type != LightType.Point && light.type != LightType.Spot)
                {
                    continue;
                }

                // 排除烛光、道具自带小灯等小范围光源。
                if (light.range < 5f)
                {
                    continue;
                }

                // 玩家手提灯与蜡烛由 PlayerLantern 管理，不参与挡位亮度与阴影切换。
                if (light.gameObject.name.Contains("PlayerLantern") ||
                    light.gameObject.name.Contains("PlayerCandle"))
                {
                    continue;
                }

                picked.Add(light);
            }

            lights = picked.ToArray();
            scalable = new bool[lights.Length];
            shadowEligible = new bool[lights.Length];
            baseIntensity = new float[lights.Length];

            for (int i = 0; i < lights.Length; i++)
            {
                baseIntensity[i] = lights[i].intensity;
                // FlickerLight 每帧自行改写亮度，不参与挡位倍率，避免相互覆盖。
                scalable[i] = lights[i].GetComponent<FlickerLight>() == null;
                // 餐厅/宴会厅主光位于吊灯内部，投影会被灯架遮挡，只作为补光使用。
                string lightName = lights[i].gameObject.name;
                shadowEligible[i] = !lightName.Contains("DiningLight") &&
                                    !lightName.Contains("BallroomLight");
            }

            candidates = lights;
        }

        private void ApplyToScene(bool forceShadowRefresh)
        {
            switch (Level)
            {
                case 2:
                    QualitySettings.pixelLightCount = 18;
                    QualitySettings.shadowDistance = 30f;
                    QualitySettings.shadowResolution = ShadowResolution.Medium;
                    QualitySettings.shadowCascades = 2;
                    break;
                case 3:
                    QualitySettings.pixelLightCount = 32;
                    QualitySettings.shadowDistance = 45f;
                    QualitySettings.shadowResolution = ShadowResolution.High;
                    QualitySettings.shadowCascades = 4;
                    break;
                default:
                    QualitySettings.pixelLightCount = 8;
                    QualitySettings.shadowDistance = 20f;
                    QualitySettings.shadowResolution = ShadowResolution.Low;
                    QualitySettings.shadowCascades = 1;
                    break;
            }

            QualitySettings.shadowProjection = ShadowProjection.StableFit;

            float multiplier = Level == 1 ? 1.15f : (Level == 2 ? 1.25f : 1.38f);
            for (int i = 0; i < lights.Length; i++)
            {
                if (!scalable[i] || lights[i] == null)
                {
                    continue;
                }

                lights[i].intensity = baseIntensity[i] * multiplier;
            }

            ApplySurfaceRendering();
            RefreshShadowLights(forceShadowRefresh);
        }

        private void ApplySurfaceRendering()
        {
            if (darkZone)
            {
                return;
            }

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Level == 1
                ? new Color(0.13f, 0.13f, 0.125f)
                : (Level == 2 ? new Color(0.07f, 0.08f, 0.1f) : new Color(0.012f, 0.014f, 0.019f));

            RenderSettings.skybox = null;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Level == 1
                ? new Color(0.008f, 0.01f, 0.013f)
                : (Level == 2 ? new Color(0.010f, 0.013f, 0.018f) : new Color(0.003f, 0.004f, 0.006f));
            RenderSettings.fogDensity = Level == 1 ? 0.01f : (Level == 2 ? 0.016f : 0.024f);
        }

        private void RefreshShadowLights(bool force)
        {
            int wanted = Level == 1 ? 2 : (Level == 2 ? 6 : 8);

            Transform target = player;
            if (target == null)
            {
                GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                if (playerObject != null)
                {
                    player = playerObject.transform;
                    target = player;
                }
            }

            if (target == null && Camera.main != null)
            {
                target = Camera.main.transform;
            }

            var nearest = new List<Light>();
            if (wanted > 0 && target != null)
            {
                var distances = new List<float>();
                int index = 0;
                foreach (Light light in candidates)
                {
                    bool eligible = shadowEligible[index];
                    index++;

                    if (light == null || !eligible)
                    {
                        continue;
                    }

                    Vector3 position = light.transform.position;
                    if (Mathf.Abs(position.y - target.position.y) > 4.5f)
                    {
                        continue;
                    }

                    float distance = (position - target.position).sqrMagnitude;
                    if (distance > 400f)
                    {
                        continue;
                    }

                    int insert = distances.Count;
                    while (insert > 0 && distances[insert - 1] > distance)
                    {
                        insert--;
                    }

                    distances.Insert(insert, distance);
                    nearest.Insert(insert, light);
                }
            }

            if (nearest.Count > wanted)
            {
                nearest.RemoveRange(wanted, nearest.Count - wanted);
            }

            if (force)
            {
                foreach (Light light in candidates)
                {
                    if (light != null)
                    {
                        light.shadows = LightShadows.None;
                    }
                }
            }

            bool changed = force || nearest.Count != shadowed.Count;
            if (!changed)
            {
                for (int i = 0; i < nearest.Count; i++)
                {
                    if (nearest[i] != shadowed[i])
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
            {
                return;
            }

            foreach (Light light in shadowed)
            {
                if (light != null)
                {
                    light.shadows = LightShadows.None;
                }
            }

            shadowed.Clear();

            float strength = Level == 3 ? 0.96f : 0.88f;
            foreach (Light light in nearest)
            {
                light.shadows = LightShadows.Soft;
                light.shadowStrength = strength;
                shadowed.Add(light);
            }
        }
    }
}
