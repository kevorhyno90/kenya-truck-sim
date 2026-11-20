using System.Collections.Generic;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Working 3-color intersection traffic controller (North-South boulevard vs East-West cross-street).
    /// Drives emissive red/amber/green lens renderers and exposes signal state to AI vehicles and pedestrians.
    /// </summary>
    public class StreetTrafficLight : MonoBehaviour
    {
        public static readonly List<StreetTrafficLight> ActiveIntersections = new List<StreetTrafficLight>();

        [Header("Intersection Location")]
        public float intersectionZ;
        public float phaseOffsetSeconds;

        [Header("Timing (Seconds)")]
        public float mainGreenDuration = 12f;
        public float yellowDuration = 2.4f;
        public float crossGreenDuration = 9f;

        [Header("Lens Renderers — Main Boulevard (North-South)")]
        public Renderer[] mainRedLenses;
        public Renderer[] mainYellowLenses;
        public Renderer[] mainGreenLenses;

        [Header("Lens Renderers — Cross Street (East-West)")]
        public Renderer[] crossRedLenses;
        public Renderer[] crossYellowLenses;
        public Renderer[] crossGreenLenses;

        private float timer;
        private int lastPhase = -1;
        private MaterialPropertyBlock mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Phase: 0 = Main Green / Cross Red, 1 = Main Yellow / Cross Red, 2 = Main Red / Cross Green, 3 = Main Red / Cross Yellow
        public int CurrentPhase { get; private set; }

        public bool IsMainBoulevardGreen => CurrentPhase == 0;
        public bool IsMainBoulevardStop => CurrentPhase >= 1;
        public bool IsCrossStreetGreen => CurrentPhase == 2;
        public bool IsCrossStreetStop => CurrentPhase != 2;

        private void OnEnable()
        {
            if (!ActiveIntersections.Contains(this))
            {
                ActiveIntersections.Add(this);
            }
            mpb ??= new MaterialPropertyBlock();
            timer = phaseOffsetSeconds;
            UpdatePhase(force: true);
        }

        private void OnDisable()
        {
            ActiveIntersections.Remove(this);
        }

        private void Update()
        {
            float cycle = mainGreenDuration + yellowDuration + crossGreenDuration + yellowDuration;
            timer = (timer + Time.deltaTime) % cycle;
            UpdatePhase(force: false);
        }

        private void UpdatePhase(bool force)
        {
            float t1 = mainGreenDuration;
            float t2 = t1 + yellowDuration;
            float t3 = t2 + crossGreenDuration;

            int phase = timer < t1 ? 0 : (timer < t2 ? 1 : (timer < t3 ? 2 : 3));
            if (!force && phase == lastPhase) return;

            CurrentPhase = phase;
            lastPhase = phase;

            ApplyLensGroup(mainRedLenses, phase == 2 || phase == 3, Palette.SignalRedOn);
            ApplyLensGroup(mainYellowLenses, phase == 1, Palette.SignalYellowOn);
            ApplyLensGroup(mainGreenLenses, phase == 0, Palette.SignalGreenOn);

            ApplyLensGroup(crossRedLenses, phase == 0 || phase == 1, Palette.SignalRedOn);
            ApplyLensGroup(crossYellowLenses, phase == 3, Palette.SignalYellowOn);
            ApplyLensGroup(crossGreenLenses, phase == 2, Palette.SignalGreenOn);
        }

        private void ApplyLensGroup(Renderer[] renderers, bool lit, Color activeColor)
        {
            if (renderers == null) return;
            Color baseCol = lit ? activeColor : Palette.SignalOff;
            Color emissive = lit ? activeColor * 2.4f : Color.black;

            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, baseCol);
                mpb.SetColor(EmissionColorId, emissive);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
