using PuzzleEscape.AI;
using PuzzleEscape.UI;
using UnityEngine;

namespace PuzzleEscape.Core
{
    /// <summary>
    /// 现实声音检测：读取系统麦克风，把输入音量换算成 0~190 dB。
    /// 超过阈值（<see cref="DifficultySettings.NoiseThresholdDb"/>，默认 40 dB）时，
    /// 通知所有怪物以追逐速度赶往玩家当时所在的位置。
    /// 只在玩家已经开始游戏、当前难度有怪物且本局开启了「现实声音检测」时工作。
    /// </summary>
    public class MicrophoneNoiseDetector : MonoBehaviour
    {
        public static MicrophoneNoiseDetector Instance { get; private set; }

        /// <summary>响度上限（0~190 dB）。</summary>
        public const float MaxDb = 190f;

        /// <summary>当前响度（0~190 dB）。</summary>
        public float LevelDb { get; private set; }

        /// <summary>麦克风是否正在录音。</summary>
        public bool IsListening { get; private set; }

        /// <summary>两次触发之间的最短间隔，避免持续的噪音让怪物反复重算路线。</summary>
        private const float TriggerCooldown = 2.5f;

        /// <summary>计算响度时取样的时间窗口（秒）。窗口越短，显示越跟手。</summary>
        private const float WindowSeconds = 0.08f;

        private const float AnalysisInterval = 0.02f;

        /// <summary>每轮分析后向真实值回落的比例，让短促的声音也能被捕捉到。</summary>
        private const float ReleaseLerp = 0.35f;

        /// <summary>
        /// 输入下限：-60 dBFS 记为 0 dB，0 dBFS 记为 <see cref="MaxDb"/>。
        /// 响度按指数曲线放大，使小声说话落在 15~45，正常说话 60~100，喊叫接近 190。
        /// </summary>
        private const float FloorDbfs = -60f;
        private const float LoudnessExponent = 2.8f;

        private const int PreferredFrequency = 44100;

        private string device;
        private AudioClip clip;
        private float[] samples;
        private float nextAnalysisTime;
        private float nextStartAttempt;
        private float nextTriggerTime;
        private bool warnedUnavailable;
        private Transform player;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
        }

        private void OnDestroy()
        {
            StopListening();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (!ShouldListen())
            {
                if (IsListening)
                {
                    StopListening();
                }

                return;
            }

            if (!IsListening)
            {
                StartListening();
            }

            if (!IsListening)
            {
                return;
            }

            Analyze();
            CheckTrigger();
        }

        private static bool ShouldListen()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.HasStarted || game.IsWon || game.IsLost)
            {
                return false;
            }

            return DifficultySettings.MonstersEnabled && DifficultySettings.MicNoiseEnabled;
        }

        private void StartListening()
        {
            if (Time.time < nextStartAttempt)
            {
                return;
            }

            // 没有麦克风（或系统没给权限）时过几秒再试，避免每帧刷日志。
            nextStartAttempt = Time.time + 3f;

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                WarnUnavailable("没有检测到可用的麦克风。");
                return;
            }

            string selected = Microphone.devices[0];
            int frequency = PreferredFrequency;
            Microphone.GetDeviceCaps(selected, out int minFrequency, out int maxFrequency);

            if (maxFrequency > 0 && maxFrequency < frequency)
            {
                frequency = maxFrequency;
            }
            else if (minFrequency > frequency)
            {
                frequency = minFrequency;
            }

            frequency = Mathf.Clamp(frequency, 8000, 48000);

            AudioClip recorded = Microphone.Start(selected, true, 1, frequency);
            if (recorded == null || recorded.samples <= 0)
            {
                WarnUnavailable("麦克风启动失败。");
                return;
            }

            device = selected;
            clip = recorded;
            samples = new float[clip.samples];
            LevelDb = 0f;
            nextAnalysisTime = 0f;
            nextTriggerTime = Time.time + 1f;
            IsListening = true;

            Debug.Log("[PuzzleEscape] 现实声音检测已启动：" + device +
                      "（阈值 " + DifficultySettings.NoiseThresholdDb + " dB）");
        }

        private void StopListening()
        {
            if (!string.IsNullOrEmpty(device))
            {
                Microphone.End(device);
            }

            device = null;
            clip = null;
            samples = null;
            LevelDb = 0f;
            IsListening = false;
        }

        private void WarnUnavailable(string message)
        {
            if (warnedUnavailable)
            {
                return;
            }

            warnedUnavailable = true;
            Debug.LogWarning("[PuzzleEscape] " + message + "现实声音检测已跳过。");
        }

        /// <summary>按 RMS 计算最近一小段录音的响度，并做快速上升、缓慢回落的平滑。</summary>
        private void Analyze()
        {
            if (clip == null || samples == null)
            {
                return;
            }

            float now = Time.time;
            if (now < nextAnalysisTime)
            {
                return;
            }

            nextAnalysisTime = now + AnalysisInterval;

            int position = Microphone.GetPosition(device);
            if (position <= 0 || !clip.GetData(samples, 0))
            {
                return;
            }

            int length = samples.Length;
            int count = Mathf.Clamp(Mathf.RoundToInt(clip.frequency * WindowSeconds), 64, length);
            int start = position - count;
            float sum = 0f;

            for (int i = 0; i < count; i++)
            {
                int index = (start + i) % length;
                if (index < 0)
                {
                    index += length;
                }

                float value = samples[index];
                sum += value * value;
            }

            float rms = Mathf.Sqrt(sum / count);

            // 先换算成归一化的 dBFS，再按指数曲线映射到 0~190 dB：
            // 小声说话 15~45，正常说话 60~100，大声喊叫接近 190。
            float raw = 0f;
            if (rms > 0.00001f)
            {
                float dbfs = 20f * Mathf.Log10(rms);
                float normalized = Mathf.Clamp01((dbfs - FloorDbfs) / -FloorDbfs);
                raw = MaxDb * Mathf.Pow(normalized, LoudnessExponent);
            }

            // 起音立即跟上，回落稍慢，避免读数跳动过快。
            LevelDb = raw > LevelDb ? raw : Mathf.Lerp(LevelDb, raw, ReleaseLerp);
        }

        private void CheckTrigger()
        {
            if (LevelDb < DifficultySettings.NoiseThresholdDb || Time.time < nextTriggerTime)
            {
                return;
            }

            // 菜单/背包打开时怪物本来就停止思考，这里也不触发，避免关掉菜单后立刻被围。
            UIRoot ui = UIRoot.Instance;
            if (ui != null && ui.IsModalOpen)
            {
                return;
            }

            Transform target = ResolvePlayer();
            if (target == null)
            {
                return;
            }

            nextTriggerTime = Time.time + TriggerCooldown;

            Vector3 position = target.position;
            Debug.Log("[PuzzleEscape] 现实声音 " + LevelDb.ToString("F0") + " dB 超过阈值 " +
                      DifficultySettings.NoiseThresholdDb + " dB，怪物赶往玩家位置。");

            MonsterNpc.ReportNoise(position);
        }

        private Transform ResolvePlayer()
        {
            if (player != null)
            {
                return player;
            }

            GameObject found = GameObject.FindGameObjectWithTag("Player");
            if (found != null)
            {
                player = found.transform;
            }

            return player;
        }
    }
}
