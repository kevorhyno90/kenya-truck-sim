using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Procedural audio system using synthesized PCM AudioClips (with 5 ms fade envelopes)
    /// and a unified static gain layer (MusicVolume, SfxVolume, pause ducking).
    /// Covers street ambient bed, turbo-diesel Scania/Isuzu engine loop, pneumatic air-brake hiss,
    /// turn-indicator relay tick-tock, boarding blip, milestone sting, Kenyan horns, impact thud, and UI click.
    /// </summary>
    public class KenyaStreetAudio : MonoBehaviour
    {
        public static KenyaStreetAudio Instance { get; private set; }

        [Header("Gain Layer (0..1)")]
        [Range(0f, 1f)] public float musicVolume = 0.75f;
        [Range(0f, 1f)] public float sfxVolume = 0.85f;

        private bool isPausedDuck;
        private const float PauseDuckMultiplier = 0.5f; // ~ -6 dB

        private AudioSource ambientSource;
        private AudioSource engineSource;
        private AudioSource sirenSource;
        private AudioSource sfxSource;
        private AudioSource uiSource;

        private AudioClip ambientClip;
        private AudioClip engineClip;
        private AudioClip policeSirenClip;
        private AudioClip pickupBlipClip;
        private AudioClip milestoneStingClip;
        private AudioClip uiClickClip;
        private AudioClip enaHornClip;
        private AudioClip isuzuHornClip;
        private AudioClip bumpClip;
        private AudioClip airBrakeClip;
        private AudioClip indicatorTickClip;

        private float currentEngineVolumeFactor = 0.22f;
        private float currentEnginePitch = 0.85f;
        private bool isHornPlaying;
        private float lastAirBrakeTime = -5f;

        public AudioSource AmbientSource => ambientSource;
        public AudioSource EngineSource => engineSource;
        public AudioSource SfxSource => sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildClips();
            BuildSources();
            ApplyVolumes();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildSources()
        {
            ambientSource = CreateSource("AmbientBed", loop: true, spatial: false);
            ambientSource.clip = ambientClip;
            ambientSource.Play();

            engineSource = CreateSource("EngineLoop", loop: true, spatial: false);
            engineSource.clip = engineClip;
            engineSource.pitch = 0.85f;
            engineSource.Play();

            sirenSource = CreateSource("PoliceSirenLoop", loop: true, spatial: false);
            sirenSource.clip = policeSirenClip;
            sirenSource.volume = 0f;

            sfxSource = CreateSource("SfxOneShot", loop: false, spatial: false);
            uiSource = CreateSource("UiOneShot", loop: false, spatial: false);
        }

        private AudioSource CreateSource(string childName, bool loop, bool spatial)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = loop;
            src.spatialBlend = spatial ? 1f : 0f;
            return src;
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetSfxVolume(float value)
        {
            sfxVolume = Mathf.Clamp01(value);
            ApplyVolumes();
        }

        public void SetPausedDuck(bool paused)
        {
            isPausedDuck = paused;
            ApplyVolumes();
        }

        public void ApplyVolumes()
        {
            float duck = isPausedDuck ? PauseDuckMultiplier : 1f;
            if (ambientSource != null)
            {
                ambientSource.volume = 0.28f * musicVolume * duck;
            }
            if (engineSource != null)
            {
                engineSource.volume = currentEngineVolumeFactor * sfxVolume * duck;
                engineSource.pitch = currentEnginePitch;
            }
            if (sirenSource != null)
            {
                sirenSource.volume = sirenSource.isPlaying ? 0.36f * sfxVolume * duck : 0f;
            }
            if (sfxSource != null)
            {
                sfxSource.volume = sfxVolume * duck;
            }
            if (uiSource != null)
            {
                uiSource.volume = sfxVolume;
            }
        }

        public void UpdateEngine(float speedKmh, float throttleAbs, bool isRunning, float rpmNormalized = 0f)
        {
            if (engineSource == null) return;
            float driveLoad = Mathf.Max(Mathf.Clamp01(speedKmh / 85f) * 0.6f, rpmNormalized * 0.85f);
            float targetPitch = isRunning
                ? Mathf.Lerp(0.70f, 1.82f, driveLoad * 0.78f + throttleAbs * 0.22f)
                : 0.66f;
            float targetFactor = isRunning
                ? Mathf.Lerp(0.18f, 0.46f, driveLoad * 0.60f + throttleAbs * 0.40f)
                : 0.08f;

            currentEnginePitch = Mathf.Lerp(currentEnginePitch, targetPitch, Time.unscaledDeltaTime * 7f);
            currentEngineVolumeFactor = Mathf.Lerp(currentEngineVolumeFactor, targetFactor, Time.unscaledDeltaTime * 7f);
            ApplyVolumes();
        }

        public void SetPoliceSiren(bool active)
        {
            if (sirenSource == null || policeSirenClip == null) return;
            if (active && !sirenSource.isPlaying)
            {
                sirenSource.Play();
            }
            else if (!active && sirenSource.isPlaying)
            {
                sirenSource.Stop();
            }
            ApplyVolumes();
        }

        public void PlayAirBrakeHiss()
        {
            if (sfxSource == null || airBrakeClip == null) return;
            if (Time.time - lastAirBrakeTime < 2.2f) return;
            lastAirBrakeTime = Time.time;
            sfxSource.pitch = Random.Range(0.94f, 1.06f);
            sfxSource.PlayOneShot(airBrakeClip, 0.58f);
        }

        public void PlayIndicatorTick(bool highTone)
        {
            if (uiSource == null || indicatorTickClip == null) return;
            uiSource.pitch = highTone ? 1.18f : 0.86f;
            uiSource.PlayOneShot(indicatorTickClip, 0.40f);
        }

        public void PlayPickupBlip()
        {
            if (sfxSource == null || pickupBlipClip == null) return;
            sfxSource.pitch = Random.Range(0.92f, 1.08f);
            sfxSource.PlayOneShot(pickupBlipClip, 0.85f);
        }

        public void PlayMilestoneSting()
        {
            if (sfxSource == null || milestoneStingClip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(milestoneStingClip, 0.95f);
        }

        public void PlayUiClick()
        {
            if (uiSource == null || uiClickClip == null) return;
            uiSource.pitch = Random.Range(0.96f, 1.04f);
            uiSource.PlayOneShot(uiClickClip, 0.65f);
        }

        public void PlayHorn(bool isCoach)
        {
            if (sfxSource == null || isHornPlaying) return;
            AudioClip clip = isCoach ? enaHornClip : isuzuHornClip;
            if (clip == null) return;
            sfxSource.pitch = 1f;
            sfxSource.PlayOneShot(clip, 0.85f);
            isHornPlaying = true;
            Invoke(nameof(ResetHornCooldown), 0.55f);
        }

        private void ResetHornCooldown() => isHornPlaying = false;

        public void PlayBump(float intensity = 1f)
        {
            if (sfxSource == null || bumpClip == null) return;
            sfxSource.pitch = Random.Range(0.88f, 1.06f);
            sfxSource.PlayOneShot(bumpClip, Mathf.Clamp(intensity, 0.35f, 1f));
        }

        private void BuildClips()
        {
            const int sr = 22050;
            ambientClip = SynthesizeAmbientBed(sr, 6.0f);
            engineClip = SynthesizeTurboDieselEngine(sr, 2.0f);
            policeSirenClip = SynthesizePoliceSiren(sr, 2.4f);
            pickupBlipClip = SynthesizePickupBlip(sr, 0.11f);
            milestoneStingClip = SynthesizeMilestoneSting(sr);
            uiClickClip = SynthesizeUiClick(sr, 0.025f);
            enaHornClip = SynthesizeCoachHorn(sr, 0.55f);
            isuzuHornClip = SynthesizeTruckHorn(sr, 0.50f);
            bumpClip = SynthesizeBump(sr, 0.18f);
            airBrakeClip = SynthesizeAirBrake(sr, 0.42f);
            indicatorTickClip = SynthesizeIndicatorTick(sr, 0.018f);
        }

        private static void ApplyFade(float[] samples, int sampleRate, float fadeSeconds = 0.005f)
        {
            int fadeCount = Mathf.Min(samples.Length / 2, Mathf.Max(1, Mathf.RoundToInt(sampleRate * fadeSeconds)));
            for (int i = 0; i < fadeCount; i++)
            {
                float w = (float)i / fadeCount;
                samples[i] *= w;
                samples[samples.Length - 1 - i] *= w;
            }
        }

        private static AudioClip CreateClip(string name, float[] data, int sr)
        {
            var clip = AudioClip.Create(name, data.Length, 1, sr, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip SynthesizeAmbientBed(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            var rng = new System.Random(104729);
            float brown = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                brown = Mathf.Clamp((brown + 0.025f * white) * 0.992f, -1f, 1f);
                float swell = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * t / duration);
                float warmPad = 0.10f * Mathf.Sin(2f * Mathf.PI * 110f * t)
                              + 0.06f * Mathf.Sin(2f * Mathf.PI * 165f * t);
                data[i] = Mathf.Clamp((brown * 0.68f + warmPad) * swell * 0.55f, -0.9f, 0.9f);
            }
            ApplyFade(data, sr, 0.08f);
            return CreateClip("KenyaStreetAmbient", data, sr);
        }

        private static AudioClip SynthesizeTurboDieselEngine(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            var rng = new System.Random(4201);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float baseFreq = 38f; // Deep Scania/Isuzu 6-cylinder diesel firing rumble
                float rumble = 0.52f * Mathf.Sin(2f * Mathf.PI * baseFreq * t)
                             + 0.30f * Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t)
                             + 0.16f * Mathf.Sin(2f * Mathf.PI * baseFreq * 3f * t)
                             + 0.10f * Mathf.Sin(2f * Mathf.PI * baseFreq * 4.5f * t);
                // Subtle turbocharger whistle overtone
                float turbo = 0.045f * Mathf.Sin(2f * Mathf.PI * 1180f * t);
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = lp * 0.86f + white * 0.14f;
                float pistonPulse = 0.82f + 0.18f * Mathf.Sin(2f * Mathf.PI * 14f * t);
                data[i] = Mathf.Clamp((rumble * 0.66f + lp * 0.30f + turbo) * pistonPulse * 0.48f, -0.92f, 0.92f);
            }
            ApplyFade(data, sr, 0.015f);
            return CreateClip("TurboDieselEngineLoop", data, sr);
        }

        private static AudioClip SynthesizePoliceSiren(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                // Kenyan Police Patrol wail + rapid yelp sweep (640 Hz <-> 1420 Hz)
                float wail = 0.5f - 0.5f * Mathf.Cos(2f * Mathf.PI * t / (duration * 0.5f));
                float freq = Mathf.Lerp(640f, 1420f, wail);
                phase += 2f * Mathf.PI * freq / sr;
                float tone = 0.72f * Mathf.Sin(phase) + 0.22f * Mathf.Sin(phase * 2f) + 0.08f * Mathf.Sin(phase * 3f);
                data[i] = Mathf.Clamp(tone * 0.55f, -0.9f, 0.9f);
            }
            ApplyFade(data, sr, 0.015f);
            return CreateClip("KenyaPoliceSiren", data, sr);
        }

        private static AudioClip SynthesizeAirBrake(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            var rng = new System.Random(25401);
            float prev = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                // High-pass filtered pneumatic hiss
                float hp = white - prev * 0.72f;
                prev = white;
                float env = Mathf.Sin(Mathf.Min(1f, u * 14f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - u, 1.5f);
                data[i] = Mathf.Clamp(hp * env * 0.68f, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 0.006f);
            return CreateClip("PneumaticAirBrake", data, sr);
        }

        private static AudioClip SynthesizeIndicatorTick(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float t = (float)i / sr;
                float env = Mathf.Pow(1f - u, 2.8f);
                data[i] = Mathf.Sin(2f * Mathf.PI * 1450f * t) * env * 0.65f;
            }
            ApplyFade(data, sr, 0.002f);
            return CreateClip("IndicatorRelayTick", data, sr);
        }

        private static AudioClip SynthesizePickupBlip(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float freq = Mathf.Lerp(880f, 1320f, u);
                phase += 2f * Mathf.PI * freq / sr;
                float env = Mathf.Pow(1f - u, 1.4f);
                data[i] = Mathf.Sin(phase) * env * 0.65f;
            }
            ApplyFade(data, sr, 0.005f);
            return CreateClip("PickupBlip", data, sr);
        }

        private static AudioClip SynthesizeMilestoneSting(int sr)
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float step = 0.115f;
            float noteDur = 0.22f;
            float totalDur = step * (notes.Length - 1) + noteDur + 0.05f;
            int n = Mathf.RoundToInt(sr * totalDur);
            var data = new float[n];

            for (int k = 0; k < notes.Length; k++)
            {
                int startIdx = Mathf.RoundToInt(k * step * sr);
                int len = Mathf.RoundToInt(noteDur * sr);
                float freq = notes[k];
                for (int j = 0; j < len && startIdx + j < n; j++)
                {
                    float u = (float)j / len;
                    float t = (float)j / sr;
                    float env = Mathf.Sin(Mathf.Min(1f, u * 10f) * Mathf.PI * 0.5f) * Mathf.Pow(1f - u, 1.6f);
                    float wave = 0.75f * Mathf.Sin(2f * Mathf.PI * freq * t)
                               + 0.25f * Mathf.Sin(2f * Mathf.PI * freq * 2f * t);
                    data[startIdx + j] = Mathf.Clamp(data[startIdx + j] + wave * env * 0.45f, -0.9f, 0.9f);
                }
            }
            ApplyFade(data, sr, 0.006f);
            return CreateClip("MilestoneSting", data, sr);
        }

        private static AudioClip SynthesizeUiClick(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            var rng = new System.Random(773);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                lp = lp * 0.55f + white * 0.45f;
                float env = Mathf.Pow(1f - u, 2.2f);
                data[i] = lp * env * 0.7f;
            }
            ApplyFade(data, sr, 0.003f);
            return CreateClip("UiClick", data, sr);
        }

        private static AudioClip SynthesizeCoachHorn(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float u = (float)i / n;
                float f1 = 349.23f;
                float f2 = 440.00f;
                float s1 = Mathf.Sin(2f * Mathf.PI * f1 * t) + 0.35f * Mathf.Sin(4f * Mathf.PI * f1 * t);
                float s2 = Mathf.Sin(2f * Mathf.PI * f2 * t) + 0.30f * Mathf.Sin(4f * Mathf.PI * f2 * t);
                float env = Mathf.Sin(Mathf.Min(1f, u * 12f) * Mathf.PI * 0.5f) * Mathf.Clamp01((1f - u) * 7f);
                data[i] = Mathf.Clamp((s1 + s2) * 0.28f * env, -0.9f, 0.9f);
            }
            ApplyFade(data, sr, 0.008f);
            return CreateClip("EnaCoachHorn", data, sr);
        }

        private static AudioClip SynthesizeTruckHorn(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / sr;
                float u = (float)i / n;
                float f1 = 146.83f;
                float f2 = 220.00f;
                float s = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * f1 * t)) * 0.32f
                        + Mathf.Sin(2f * Mathf.PI * f2 * t) * 0.45f;
                float env = Mathf.Sin(Mathf.Min(1f, u * 10f) * Mathf.PI * 0.5f) * Mathf.Clamp01((1f - u) * 6f);
                data[i] = Mathf.Clamp(s * 0.55f * env, -0.9f, 0.9f);
            }
            ApplyFade(data, sr, 0.008f);
            return CreateClip("IsuzuTruckHorn", data, sr);
        }

        private static AudioClip SynthesizeBump(int sr, float duration)
        {
            int n = Mathf.RoundToInt(sr * duration);
            var data = new float[n];
            var rng = new System.Random(9012);
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float u = (float)i / n;
                float freq = Mathf.Lerp(145f, 48f, u);
                phase += 2f * Mathf.PI * freq / sr;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.25f * (1f - u);
                float env = Mathf.Pow(1f - u, 1.8f);
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.8f + noise) * env, -0.95f, 0.95f);
            }
            ApplyFade(data, sr, 0.005f);
            return CreateClip("TrafficBump", data, sr);
        }
    }
}
