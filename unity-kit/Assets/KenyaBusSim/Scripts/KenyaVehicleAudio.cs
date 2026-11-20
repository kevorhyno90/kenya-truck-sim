using UnityEngine;

namespace KenyaBusSim
{
    /// <summary>
    /// Procedural diesel engine, air-horn (ENA Coach melodic chime & Isuzu FRR dual-tone blast),
    /// and suspension thud audio for Unity 6. Supports optional real AudioClip overrides in Inspector.
    /// </summary>
    [RequireComponent(typeof(KenyaVehicleController))]
    public class KenyaVehicleAudio : MonoBehaviour
    {
        [Header("Vehicle Audio Profile")]
        public bool useBusChimeHorn = true;

        [Header("Optional Real AudioClip Overrides (Drop real .wav/.mp3 here)")]
        public AudioClip customEngineLoop;
        public AudioClip customHornClip;
        public AudioClip customBumpThudClip;

        [Header("Volume & Pitch Tuning")]
        [Range(0f, 1f)] public float engineVolume = 0.28f;
        [Range(0f, 1f)] public float hornVolume = 0.65f;
        public float minEnginePitch = 0.72f;
        public float maxEnginePitch = 1.95f;

        private AudioSource engineSource;
        private AudioSource sfxSource;
        private KenyaVehicleController controller;
        private AudioClip generatedEngineClip;
        private AudioClip generatedHornClip;
        private AudioClip generatedThudClip;

        private void Awake()
        {
            controller = GetComponent<KenyaVehicleController>();

            engineSource = gameObject.AddComponent<AudioSource>();
            engineSource.loop = true;
            engineSource.spatialBlend = 0.35f;
            engineSource.volume = engineVolume;
            engineSource.playOnAwake = false;

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.spatialBlend = 0.2f;
            sfxSource.volume = hornVolume;
            sfxSource.playOnAwake = false;

            BuildClips();
        }

        private void OnEnable()
        {
            if (engineSource != null && engineSource.clip != null && !engineSource.isPlaying)
            {
                engineSource.Play();
            }
        }

        private void OnDisable()
        {
            if (engineSource != null && engineSource.isPlaying)
            {
                engineSource.Stop();
            }
        }

        private void BuildClips()
        {
            generatedEngineClip = customEngineLoop != null ? customEngineLoop : CreateDieselEngineClip(useBusChimeHorn);
            generatedHornClip = customHornClip != null
                ? customHornClip
                : (useBusChimeHorn ? CreateEnaChimeHornClip() : CreateIsuzuAirHornClip());
            generatedThudClip = customBumpThudClip != null ? customBumpThudClip : CreateSuspensionThudClip();

            engineSource.clip = generatedEngineClip;
        }

        private void Update()
        {
            if (controller == null || engineSource == null) return;

            if (!engineSource.isPlaying && isActiveAndEnabled)
            {
                engineSource.Play();
            }

            float rpmRatio = Mathf.Clamp01((controller.CurrentRpm - 700f) / 1800f);
            float throttleBoost = Mathf.Abs(controller.ThrottleInput) * 0.12f;
            float targetPitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, rpmRatio) + throttleBoost;

            engineSource.pitch = Mathf.Lerp(engineSource.pitch, targetPitch, Time.deltaTime * 6f);
            engineSource.volume = Mathf.Lerp(engineVolume * 0.7f, engineVolume * 1.15f, rpmRatio);
        }

        public void PlayHorn()
        {
            if (sfxSource != null && generatedHornClip != null)
            {
                sfxSource.PlayOneShot(generatedHornClip, hornVolume);
            }
        }

        public void PlayBumpThud(float intensity)
        {
            if (sfxSource != null && generatedThudClip != null)
            {
                sfxSource.PlayOneShot(generatedThudClip, Mathf.Clamp(intensity, 0.25f, 1f) * 0.85f);
            }
        }

        private static AudioClip CreateDieselEngineClip(bool isCoach)
        {
            const int sampleRate = 22050;
            const int lengthSamples = sampleRate; // 1-second seamless loop
            float[] samples = new float[lengthSamples];

            float baseFreq = isCoach ? 46f : 54f; // Deep Scania/MAN inline-6 vs punchier Isuzu 4HK1/6HK1
            float lp = 0f;

            for (int i = 0; i < lengthSamples; i++)
            {
                float t = (float)i / sampleRate;
                float s1 = Mathf.Sin(2f * Mathf.PI * baseFreq * t);
                float s2 = 0.55f * Mathf.Sin(2f * Mathf.PI * baseFreq * 2f * t);
                float s3 = 0.35f * Mathf.Sin(2f * Mathf.PI * baseFreq * 3f * t);
                float clatter = 0.22f * Mathf.Sin(2f * Mathf.PI * baseFreq * 6f * t);

                float raw = (s1 + s2 + s3 + clatter) * 0.38f;
                lp = lp + 0.18f * (raw - lp); // Gentle warm low-pass
                samples[i] = Mathf.Clamp(lp, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("KenyaDieselEngine", lengthSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateEnaChimeHornClip()
        {
            // Authentic Kenyan VIP Coach 4-note chime sequence (C5, E5, G5, C6)
            const int sampleRate = 22050;
            const float duration = 0.92f;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float noteStep = 0.12f;
            float noteLen = 0.38f;

            for (int n = 0; n < notes.Length; n++)
            {
                int startSample = Mathf.FloorToInt(n * noteStep * sampleRate);
                int lenSamples = Mathf.FloorToInt(noteLen * sampleRate);
                float freq = notes[n];

                for (int i = 0; i < lenSamples; i++)
                {
                    int idx = startSample + i;
                    if (idx >= totalSamples) break;

                    float localT = (float)i / sampleRate;
                    float env = Mathf.Min(1f, localT / 0.015f) * Mathf.Exp(-localT * 5.2f);
                    float wave = Mathf.Sin(2f * Mathf.PI * freq * localT) * 0.65f
                               + Mathf.Sin(2f * Mathf.PI * freq * 2f * localT) * 0.22f;
                    samples[idx] = Mathf.Clamp(samples[idx] + wave * env * 0.55f, -1f, 1f);
                }
            }

            AudioClip clip = AudioClip.Create("EnaCoachChimeHorn", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateIsuzuAirHornClip()
        {
            // Heavy pneumatic dual-tone truck air horn blast (330 Hz + 415 Hz)
            const int sampleRate = 22050;
            const float duration = 0.85f;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float env = Mathf.Min(1f, t / 0.03f) * Mathf.Clamp01((duration - t) / 0.16f);

                float saw1 = 2f * ((t * 330f) % 1f) - 1f;
                float saw2 = 2f * ((t * 415f) % 1f) - 1f;
                float sub = Mathf.Sin(2f * Mathf.PI * 165f * t);

                float mixed = (saw1 * 0.35f + saw2 * 0.35f + sub * 0.3f) * env * 0.65f;
                samples[i] = Mathf.Clamp(mixed, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create("IsuzuFrrAirHorn", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateSuspensionThudClip()
        {
            const int sampleRate = 22050;
            const float duration = 0.28f;
            int totalSamples = Mathf.CeilToInt(sampleRate * duration);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / sampleRate;
                float freq = Mathf.Lerp(95f, 34f, t / duration);
                float env = Mathf.Exp(-t * 14f);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * env * 0.9f;
            }

            AudioClip clip = AudioClip.Create("SuspensionThud", totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
