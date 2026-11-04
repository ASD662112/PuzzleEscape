using System.Collections;
using UnityEngine;

namespace PuzzleEscape.Audio
{
    /// <summary>不同房间类型的循环环境音。</summary>
    public enum RoomTone
    {
        None,
        Basement,
        Library,
        Greenhouse,
        Kitchen,
        Bedroom
    }

    /// <summary>
    /// 运行时合成简单音效，避免依赖外部音频资源。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const int SampleRate = 44100;

        private AudioSource source;
        private AudioSource ambientSource;
        private AudioSource roomSource;
        private AudioClip ambient;
        private AudioClip click;
        private AudioClip error;
        private AudioClip success;
        private AudioClip pickup;
        private AudioClip drop;
        private AudioClip power;
        private AudioClip woodSlide;
        private AudioClip doorOpen;
        private AudioClip doorClose;
        private AudioSource footstepSource;
        private AudioClip roomBasement;
        private AudioClip roomLibrary;
        private AudioClip roomGreenhouse;
        private AudioClip roomKitchen;
        private AudioClip roomBedroom;
        private AudioClip scare;
        private RoomTone currentRoomTone = RoomTone.None;
        private Coroutine roomToneRoutine;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = PlayerPrefs.GetFloat("PuzzleEscape.MasterVolume", 0.7f);

            click = MakeUiClick("ui_click", 0.08f, 0.5f);
            error = MakeTone("ui_error", 0.28f, 220f, 130f, 0.55f);
            success = MakeTone("ui_success", 0.45f, 660f, 1320f, 0.5f);
            pickup = MakeTone("pickup", 0.2f, 620f, 1240f, 0.45f);
            drop = MakeTone("drop_thud", 0.26f, 300f, 80f, 0.55f);
            power = MakeTone("power", 0.7f, 90f, 480f, 0.5f);
            woodSlide = MakeWoodSlide("wood_slide", 0.5f, 0.45f);

            // 开关门优先使用 Resources/Audio 下的真实录音，缺失时才回退到合成音效。
            doorOpen = LoadClip("Audio/DoorOpen");
            if (doorOpen == null)
            {
                doorOpen = MakeDoorOpen("door_open", 0.9f, 0.5f);
            }

            doorClose = LoadClip("Audio/DoorClose");
            if (doorClose == null)
            {
                doorClose = MakeDoorClose("door_close", 0.6f, 0.55f);
            }
            // 脚步录音：移动时循环播放，停下暂停、继续移动接着播。
            footstepSource = gameObject.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false;
            footstepSource.loop = true;
            footstepSource.spatialBlend = 0f;
            footstepSource.clip = LoadClip("Audio/Footstep");
            footstepSource.volume = source.volume * 0.8f;

            // 跳杀音效优先使用真实录音，缺失时才回退到合成尖啸。
            scare = LoadClip("Audio/MonsterScare");
            if (scare == null)
            {
                scare = MakeScare("monster_scare", 1.4f);
            }

            ambient = MakeAmbient("ambient_room", 8f);
            ambientSource = gameObject.AddComponent<AudioSource>();
            ambientSource.playOnAwake = false;
            ambientSource.loop = true;
            ambientSource.spatialBlend = 0f;
            ambientSource.clip = ambient;
            ambientSource.volume = source.volume * 0.12f;
            ambientSource.Play();

            roomBasement = MakeRoomTone("room_basement", 12f, RoomTone.Basement);
            roomLibrary = MakeRoomTone("room_library", 10f, RoomTone.Library);
            roomGreenhouse = MakeRoomTone("room_greenhouse", 10f, RoomTone.Greenhouse);
            roomKitchen = MakeRoomTone("room_kitchen", 12f, RoomTone.Kitchen);
            roomBedroom = MakeRoomTone("room_bedroom", 12f, RoomTone.Bedroom);

            roomSource = gameObject.AddComponent<AudioSource>();
            roomSource.playOnAwake = false;
            roomSource.loop = true;
            roomSource.spatialBlend = 0f;
            roomSource.volume = 0f;
            roomSource.clip = roomBasement;
            roomSource.Play();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void PlayClick()
        {
            Play(click);
        }

        public void SetMasterVolume(float volume)
        {
            volume = Mathf.Clamp01(volume);

            if (source != null)
            {
                source.volume = volume;
            }

            if (ambientSource != null)
            {
                ambientSource.volume = volume * 0.12f;
            }

            if (roomSource != null && currentRoomTone != RoomTone.None)
            {
                roomSource.volume = volume * 0.2f;
            }

            if (footstepSource != null)
            {
                footstepSource.volume = volume * 0.8f;
            }

            PlayerPrefs.SetFloat("PuzzleEscape.MasterVolume", volume);
            PlayerPrefs.Save();
        }

        public void PlayError()
        {
            Play(error);
        }

        public void PlaySuccess()
        {
            Play(success);
        }

        public void PlayPickup()
        {
            Play(pickup);
        }

        /// <summary>物品落地/被丢下时的闷响，同时也是怪物能听见的“声音线索”。</summary>
        public void PlayDrop()
        {
            Play(drop);
        }

        public void PlayPower()
        {
            Play(power);
        }

        /// <summary>普通房门开启时的木质吱呀声。</summary>
        public void PlayDoorOpen()
        {
            Play(doorOpen);
        }

        /// <summary>普通房门关闭时的闷响与门锁咔哒声。</summary>
        public void PlayDoorClose()
        {
            Play(doorClose);
        }

        /// <summary>抽屉、柜门、地窖盖板等木质滑动的通用音效。</summary>
        public void PlayDoor()
        {
            Play(woodSlide);
        }

        /// <summary>怪物扑到脸前时的跳杀音效：下滑尖啸 + 噪声冲击。</summary>
        public void PlayScare()
        {
            Play(scare);
        }

        /// <summary>
        /// 玩家移动时循环播放脚步录音；停下时暂停并保留进度，再次移动从暂停处继续，
        /// 播到结尾后自动从头循环。
        /// </summary>
        public void SetFootstepsMoving(bool moving)
        {
            if (footstepSource == null || footstepSource.clip == null)
            {
                return;
            }

            if (moving)
            {
                if (!footstepSource.isPlaying)
                {
                    footstepSource.UnPause();
                    if (!footstepSource.isPlaying)
                    {
                        footstepSource.Play();
                    }
                }
            }
            else if (footstepSource.isPlaying)
            {
                footstepSource.Pause();
            }
        }

        /// <summary>切换房间环境音；None 表示只保留全图低频底噪。</summary>
        public void SetRoomTone(RoomTone tone)
        {
            if (tone == currentRoomTone)
            {
                return;
            }

            currentRoomTone = tone;
            AudioClip clip = GetRoomToneClip(tone);
            float targetVolume = clip != null && source != null ? source.volume * 0.2f : 0f;

            if (roomToneRoutine != null)
            {
                StopCoroutine(roomToneRoutine);
            }

            roomToneRoutine = StartCoroutine(CrossfadeRoomTone(clip, targetVolume));
        }

        private AudioClip GetRoomToneClip(RoomTone tone)
        {
            switch (tone)
            {
                case RoomTone.Basement:
                    return roomBasement;
                case RoomTone.Library:
                    return roomLibrary;
                case RoomTone.Greenhouse:
                    return roomGreenhouse;
                case RoomTone.Kitchen:
                    return roomKitchen;
                case RoomTone.Bedroom:
                    return roomBedroom;
                default:
                    return null;
            }
        }

        private IEnumerator CrossfadeRoomTone(AudioClip clip, float targetVolume)
        {
            float startVolume = roomSource.volume;
            float fade = 0.9f;
            float time = 0f;

            while (time < fade)
            {
                time += Time.unscaledDeltaTime;
                roomSource.volume = Mathf.Lerp(startVolume, 0f, time / fade);
                yield return null;
            }

            roomSource.volume = 0f;

            if (clip == null)
            {
                roomSource.Stop();
                roomToneRoutine = null;
                yield break;
            }

            roomSource.Stop();
            roomSource.clip = clip;
            roomSource.Play();

            time = 0f;
            while (time < fade)
            {
                time += Time.unscaledDeltaTime;
                roomSource.volume = Mathf.Lerp(0f, targetVolume, time / fade);
                yield return null;
            }

            roomSource.volume = targetVolume;
            roomToneRoutine = null;
        }

        private void Play(AudioClip clip)
        {
            if (source != null && clip != null)
            {
                source.PlayOneShot(clip);
            }
        }

        /// <summary>读取 Resources 下的音频资源；不存在时返回 null。</summary>
        private static AudioClip LoadClip(string resourcePath)
        {
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);
            return clip != null ? clip : null;
        }

        /// <summary>
        /// 无缝循环的房间低频底噪：55Hz / 110Hz 微弱嗡鸣 + 缓慢起伏。
        /// </summary>
        private static AudioClip MakeAmbient(string clipName, float duration)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 55f * time) * 0.18f;
                hum += Mathf.Sin(2f * Mathf.PI * 110f * time) * 0.08f;
                hum += Mathf.Sin(2f * Mathf.PI * 220f * time) * 0.03f;
                float breath = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 0.125f * time);
                samples[i] = hum * breath;
            }

            AudioClip clip = CreateClip(clipName, samples);
            clip.name = clipName;
            return clip;
        }

        /// <summary>
        /// 房间环境音：全部使用整数周期正弦，首尾可无缝循环；瞬态事件在循环结束前衰减完毕。
        /// </summary>
        private static AudioClip MakeRoomTone(string clipName, float duration, RoomTone tone)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float[] noisePhases = BuildNoisePhases(5100 + (int)tone);

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float value = 0f;

                switch (tone)
                {
                    case RoomTone.Basement:
                        value += Mathf.Sin(2f * Mathf.PI * 45f * time) * 0.045f;
                        value += Mathf.Sin(2f * Mathf.PI * 90f * time) * 0.018f;
                        value += LoopNoise(time, duration, noisePhases, 0.035f);
                        value += Pulse(time, 1.5f, 1.3f, 900f, 9f, 0.06f);
                        value += Pulse(time, 4.5f, 1.1f, 760f, 7f, 0.045f);
                        value += Pulse(time, 7.5f, 1.3f, 880f, 8f, 0.05f);
                        value += Pulse(time, 10.5f, 1.2f, 1060f, 10f, 0.04f);
                        break;

                    case RoomTone.Library:
                        value += Mathf.Sin(2f * Mathf.PI * 110f * time) * 0.022f;
                        value += LoopNoise(time, duration, noisePhases, 0.028f);
                        for (int tick = 0; tick < 5; tick++)
                        {
                            value += Pulse(time, tick * 2f, 0.3f, 1380f, 46f, 0.045f);
                        }
                        break;

                    case RoomTone.Greenhouse:
                    {
                        float windBreath = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.2f * time);
                        value += Mathf.Sin(2f * Mathf.PI * 70f * time) * 0.012f;
                        value += LoopNoise(time, duration, noisePhases, 0.08f) * windBreath;
                        value += Pulse(time, 1.2f, 0.6f, 2100f, 24f, 0.04f);
                        value += Pulse(time, 5.2f, 0.6f, 2350f, 28f, 0.035f);
                        value += Pulse(time, 8.2f, 0.6f, 1950f, 20f, 0.03f);
                        break;
                    }

                    case RoomTone.Kitchen:
                        value += Mathf.Sin(2f * Mathf.PI * 55f * time) * 0.025f;
                        value += LoopNoise(time, duration, noisePhases, 0.03f);
                        for (int pop = 0; pop < 7; pop++)
                        {
                            value += Pulse(time, 1f + pop * 1.5f, 0.2f, 420f + pop * 60f, 32f, 0.045f);
                            value += Pulse(time, 1f + pop * 1.5f, 0.12f, 1200f, 55f, 0.02f);
                        }
                        break;

                    case RoomTone.Bedroom:
                        value += Mathf.Sin(2f * Mathf.PI * 130f * time) * 0.02f;
                        value += Mathf.Sin(2f * Mathf.PI * 0.1666667f * time) * 0.012f;
                        value += LoopNoise(time, duration, noisePhases, 0.022f);
                        value += Pulse(time, 3.5f, 1.8f, 180f, 3.5f, 0.035f);
                        value += Pulse(time, 9.0f, 1.6f, 150f, 4f, 0.03f);
                        break;
                }

                samples[i] = value;
            }

            AudioClip clip = CreateClip(clipName, samples);
            clip.name = clipName;
            return clip;
        }

        private static float[] BuildNoisePhases(int seed)
        {
            var rng = new System.Random(seed);
            var phases = new float[6];

            for (int k = 0; k < phases.Length; k++)
            {
                phases[k] = (float)(rng.NextDouble() * Mathf.PI * 2f);
            }

            return phases;
        }

        /// <summary>整数周期谐波伪噪声，保证在 duration 处与起点无缝衔接。</summary>
        private static float LoopNoise(float time, float duration, float[] phases, float amplitude)
        {
            float sum = 0f;

            for (int k = 1; k <= phases.Length; k++)
            {
                float frequency = k / duration;
                sum += Mathf.Sin(2f * Mathf.PI * frequency * time + phases[k - 1]) / k;
            }

            return sum * amplitude;
        }

        private static float Pulse(float time, float start, float length,
            float frequency, float decay, float amplitude)
        {
            float local = time - start;
            if (local < 0f || local > length)
            {
                return 0f;
            }

            return Mathf.Sin(2f * Mathf.PI * frequency * local) *
                   Mathf.Exp(-decay * local) * amplitude;
        }

        /// <summary>
        /// 短促、清脆的 UI 点击音：高频瞬态 + 低中频共鸣，指数衰减。
        /// </summary>
        private static AudioClip MakeUiClick(string clipName, float duration, float volume)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random rng = new System.Random(20261101);
            float lowPhase = 0f;
            float midPhase = 0f;
            float highPhase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float envelope = Mathf.Clamp01(time / 0.0025f) * Mathf.Exp(-time * 34f);
                lowPhase += 2f * Mathf.PI * 420f / SampleRate;
                midPhase += 2f * Mathf.PI * 980f / SampleRate;
                highPhase += 2f * Mathf.PI * 1960f / SampleRate;

                float body = Mathf.Sin(lowPhase) * 0.32f +
                             Mathf.Sin(midPhase) * 0.42f +
                             Mathf.Sin(highPhase) * 0.16f;
                float transient = i < 96 ? (float)(rng.NextDouble() * 2.0 - 1.0) * 0.18f : 0f;
                samples[i] = (body + transient) * envelope * volume;
            }

            return CreateClip(clipName, samples);
        }

        /// <summary>
        /// 开门音：低频起势 + 频率上滑的木质吱呀 + 轻微空气噪声。
        /// </summary>
        private static AudioClip MakeDoorOpen(string clipName, float duration, float volume)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random rng = new System.Random(77031);
            float creakPhase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float t = i / (float)sampleCount;
                float attack = Mathf.Clamp01(t / 0.06f);
                float release = Mathf.Clamp01((1f - t) / 0.22f);
                float envelope = attack * release;

                float frequency = Mathf.Lerp(170f, 300f, t) +
                                  9f * Mathf.Sin(2f * Mathf.PI * 8.5f * t);
                creakPhase += 2f * Mathf.PI * frequency / SampleRate;

                float creak = Mathf.Sin(creakPhase) * 0.5f +
                              Mathf.Sin(creakPhase * 1.97f) * 0.22f +
                              Mathf.Sin(creakPhase * 2.91f) * 0.1f;
                float tremolo = 0.62f + 0.38f * Mathf.Sin(2f * Mathf.PI * 6.2f * t);
                float thud = Mathf.Sin(2f * Mathf.PI * 68f * time) * Mathf.Exp(-time * 16f) * 0.5f;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.14f;

                samples[i] = (creak * tremolo + thud + noise) * envelope * volume;
            }

            return CreateClip(clipName, samples);
        }

        /// <summary>
        /// 关门音：低沉的木质闷响 + 门锁到位的短促咔哒。
        /// </summary>
        private static AudioClip MakeDoorClose(string clipName, float duration, float volume)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random rng = new System.Random(43127);
            float creakPhase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float t = i / (float)sampleCount;
                float attack = Mathf.Clamp01(time / 0.012f);

                creakPhase += 2f * Mathf.PI * Mathf.Lerp(260f, 150f, t) / SampleRate;
                float creak = Mathf.Sin(creakPhase) * Mathf.Exp(-time * 7f) * 0.22f;

                float thud = (Mathf.Sin(2f * Mathf.PI * 58f * time) * 0.62f +
                              Mathf.Sin(2f * Mathf.PI * 116f * time) * 0.25f +
                              Mathf.Sin(2f * Mathf.PI * 174f * time) * 0.1f) *
                             Mathf.Exp(-time * 14f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) *
                              0.2f * Mathf.Exp(-time * 18f);

                float latch = 0f;
                float latchTime = time - 0.36f;
                if (latchTime > 0f)
                {
                    latch = Mathf.Sin(2f * Mathf.PI * 1500f * latchTime) *
                            Mathf.Exp(-latchTime * 60f) * 0.35f;
                }

                samples[i] = (creak + thud * attack + noise + latch) * volume;
            }

            return CreateClip(clipName, samples);
        }

        /// <summary>抽屉/柜门滑动的木料摩擦声。</summary>
        private static AudioClip MakeWoodSlide(string clipName, float duration, float volume)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            System.Random rng = new System.Random(90210);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float attack = Mathf.Clamp01(t / 0.08f);
                float release = Mathf.Clamp01((1f - t) / 0.3f);
                float envelope = attack * release;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.18f;
                float body = Mathf.Sin(2f * Mathf.PI * 110f * (i / (float)SampleRate)) * 0.12f;
                samples[i] = (noise + body) * envelope * volume;
            }

            return CreateClip(clipName, samples);
        }

        private static AudioClip MakeTone(string clipName, float duration, float startFrequency, float endFrequency, float volume)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)sampleCount;
                float frequency = Mathf.Lerp(startFrequency, endFrequency, t);
                phase += 2f * Mathf.PI * frequency / SampleRate;

                // 正弦包络保证首尾平滑，避免爆音。
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples[i] = Mathf.Sin(phase) * envelope * volume;
            }

            return CreateClip(clipName, samples);
        }

        /// <summary>
        /// 跳杀音效：快速起音的下滑尖啸 + 噪声冲击，1.4 秒内衰减干净。
        /// </summary>
        private static AudioClip MakeScare(string clipName, float duration)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * duration));
            float[] samples = new float[sampleCount];
            var rng = new System.Random(20261101);
            float phase = 0f;

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)SampleRate;
                float progress = time / duration;

                // 起音 20ms，随后非线性衰减，尾段保证归零避免爆音。
                float attack = Mathf.Clamp01(time / 0.02f);
                float release = Mathf.Pow(1f - progress, 1.7f);
                float envelope = attack * release;

                float frequency = Mathf.Lerp(920f, 110f, Mathf.Pow(progress, 0.55f));
                phase += 2f * Mathf.PI * frequency / SampleRate;

                float shriek = Mathf.Sin(phase) * 0.55f + Mathf.Sin(phase * 2.03f) * 0.22f;
                float noise = ((float)rng.NextDouble() * 2f - 1f) * 0.5f;
                noise *= Mathf.Pow(1f - progress, 2.2f);

                samples[i] = Mathf.Clamp((shriek + noise) * envelope, -1f, 1f);
            }

            return CreateClip(clipName, samples);
        }

        private static AudioClip CreateClip(string clipName, float[] samples)
        {
            AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
