using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Authentic ETS2-style damped spring-pendulum physics for hanging Kenyan Flag Pennant & Wooden Beads
    /// mounted on the interior rearview mirror inside [C] RHD Cockpit View!
    /// Reacts naturally to cornering G-force, braking pitch, acceleration, and diesel engine vibration.
    /// </summary>
    public class KenyaCockpitPhysicsSway : MonoBehaviour
    {
        public KenyaStreetVehicle vehicle;
        public float lateralSwayScale = 24f;
        public float foreAftSwayScale = 14f;
        public float springStiffness = 28f;
        public float springDamping = 5.2f;

        private Quaternion baseLocalRot;
        private Vector3 prevVelocity;
        private Vector2 angleDeg;
        private Vector2 angVelDeg;
        private bool initialized;

        private void OnEnable()
        {
            if (!initialized)
            {
                baseLocalRot = transform.localRotation;
                initialized = true;
            }
            if (vehicle == null) vehicle = GetComponentInParent<KenyaStreetVehicle>();
            if (vehicle != null && vehicle.Rb != null)
            {
                prevVelocity = vehicle.Rb.linearVelocity;
            }
        }

        private void LateUpdate()
        {
            if (!initialized) return;
            float dt = Mathf.Clamp(Time.deltaTime, 0.001f, 0.05f);

            float targetRoll = 0f;
            float targetPitch = 0f;

            if (vehicle != null && vehicle.Rb != null)
            {
                Vector3 vel = vehicle.Rb.linearVelocity;
                Vector3 worldAccel = (vel - prevVelocity) / dt;
                prevVelocity = vel;

                Vector3 localAccel = vehicle.transform.InverseTransformDirection(worldAccel);
                float steerKick = vehicle.SteerInput * Mathf.Clamp01(vehicle.SpeedKmh / 45f) * 10f;
                targetRoll = Mathf.Clamp(-localAccel.x * 2.2f - steerKick, -lateralSwayScale, lateralSwayScale);
                targetPitch = Mathf.Clamp(localAccel.z * 1.6f, -foreAftSwayScale, foreAftSwayScale);

                // Subtle Kenyan diesel idle & tarmac road micro-shudder
                float vibe = Mathf.Sin(Time.time * 17.5f) * (0.45f + vehicle.RpmNormalized * 0.9f);
                targetRoll += vibe;
            }

            Vector2 target = new Vector2(targetPitch, targetRoll);
            Vector2 force = (target - angleDeg) * springStiffness - angVelDeg * springDamping;
            angVelDeg += force * dt;
            angleDeg += angVelDeg * dt;

            transform.localRotation = baseLocalRot * Quaternion.Euler(angleDeg.x, 0f, angleDeg.y);
        }
    }
}
