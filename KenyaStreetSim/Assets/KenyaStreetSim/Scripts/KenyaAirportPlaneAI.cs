using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Animates a 3D Kenyan commercial passenger jet executing realistic low approaches,
    /// banked turns, and takeoffs over Nairobi (JKIA), Kisumu Intl, Eldoret Intl & Mombasa (Moi Intl).
    /// </summary>
    public class KenyaAirportPlaneAI : MonoBehaviour
    {
        public Vector3 airportCenter;
        public float orbitRadiusX = 165f;
        public float orbitRadiusZ = 105f;
        public float cruiseHeightAboveAirport = 46f;
        public float minApproachHeight = 16f;
        public float speedRadPerSec = 0.22f;
        public float phaseOffset;
        public Renderer strobeRenderer;

        private float angle;
        private MaterialPropertyBlock mpb;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Start()
        {
            angle = phaseOffset;
            mpb = new MaterialPropertyBlock();
            UpdatePose(0f);
        }

        private void Update()
        {
            angle += speedRadPerSec * Time.deltaTime;
            UpdatePose(Time.time);
        }

        private void UpdatePose(float t)
        {
            Vector3 pos = SampleFlightPos(angle);
            Vector3 ahead = SampleFlightPos(angle + 0.045f);
            Vector3 fwd = (ahead - pos).normalized;

            // Realistic banking angle toward turn center + slight pitch matching climb/descent
            Vector3 radialIn = (airportCenter - new Vector3(pos.x, airportCenter.y, pos.z)).normalized;
            float bankDeg = Mathf.Clamp(Vector3.Dot(Vector3.Cross(Vector3.up, fwd), radialIn) * 26f, -28f, 28f);

            Quaternion look = Quaternion.LookRotation(fwd, Vector3.up);
            transform.SetPositionAndRotation(pos, look * Quaternion.Euler(0f, 0f, bankDeg));

            if (strobeRenderer != null && mpb != null)
            {
                bool flash = ((t * 1.6f + phaseOffset) % 1f) < 0.14f;
                strobeRenderer.GetPropertyBlock(mpb);
                mpb.SetColor(EmissionColorId, flash ? new Color(1.0f, 0.25f, 0.20f) * 5f : Color.black);
                strobeRenderer.SetPropertyBlock(mpb);
            }
        }

        private Vector3 SampleFlightPos(float a)
        {
            // Wide oval circuit that sweeps low right across the airport & nearby city highway, then climbs smoothly!
            float x = airportCenter.x + Mathf.Cos(a) * orbitRadiusX;
            float z = airportCenter.z + Mathf.Sin(a) * orbitRadiusZ;
            float lowFactor = 0.5f + 0.5f * Mathf.Sin(a - 0.6f); // 0 = low flyover approach, 1 = high climb
            float y = airportCenter.y + Mathf.Lerp(minApproachHeight, cruiseHeightAboveAirport, lowFactor * lowFactor);
            return new Vector3(x, y, z);
        }
    }
}
