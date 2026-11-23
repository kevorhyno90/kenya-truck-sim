using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Animates Kenyan savanna wildlife (Zebras, Giraffes, Gazelles around Narok/Maasai Mara
    /// and Elephants/Zebras around Voi/Tsavo) calmly grazing or walking across highway wildlife corridors.
    /// Automatically yields cleanly if the player vehicle is right in front so driving stays realistic & smooth.
    /// </summary>
    public class KenyaWildlifeAI : MonoBehaviour
    {
        public Vector3 startPoint;
        public Vector3 endPoint;
        public float walkSpeed = 1.9f;
        public float phaseOffset;
        public bool isGrazerOnly;
        public Transform[] legs;
        public Transform neckPivot;

        private float progress;
        private float direction = 1f;
        private float pauseTimer;

        private void Start()
        {
            progress = Mathf.Clamp01(phaseOffset % 1f);
            direction = (phaseOffset >= 1f) ? -1f : 1f;
            if (!isGrazerOnly)
            {
                Vector3 p = Vector3.Lerp(startPoint, endPoint, progress);
                p.y = KenyaNetworkData.GetElevationAt(p.x, p.z);
                transform.position = p;
            }
        }

        private void Update()
        {
            if (isGrazerOnly)
            {
                // Gentle grazing head/neck bob beside the road
                if (neckPivot != null)
                {
                    float graze = Mathf.Sin(Time.time * 1.15f + phaseOffset * 4f) * 14f;
                    neckPivot.localRotation = Quaternion.Euler(graze + 12f, 0f, 0f);
                }
                return;
            }

            Vector3 target = direction > 0f ? endPoint : startPoint;
            Vector3 toTarget = target - transform.position;
            toTarget.y = 0f;

            // Yield gently if player's bus/truck is very close ahead along the crossing!
            bool yieldToPlayer = false;
            var loop = StreetGameLoop.Instance;
            if (loop != null && loop.ActiveVehicle != null)
            {
                Vector3 toVeh = loop.ActiveVehicle.transform.position - transform.position;
                toVeh.y = 0f;
                float d = toVeh.magnitude;
                if (d < 13.5f && Vector3.Dot(toVeh.normalized, transform.forward) > 0.25f)
                {
                    yieldToPlayer = true;
                }
            }

            bool walking = false;
            if (pauseTimer > 0f)
            {
                pauseTimer -= Time.deltaTime;
            }
            else if (!yieldToPlayer)
            {
                float pathLen = Mathf.Max(2f, Vector3.Distance(startPoint, endPoint));
                progress += (direction * walkSpeed / pathLen) * Time.deltaTime;
                if (progress >= 1f)
                {
                    progress = 1f;
                    direction = -1f;
                    pauseTimer = 3.5f;
                }
                else if (progress <= 0f)
                {
                    progress = 0f;
                    direction = 1f;
                    pauseTimer = 3.5f;
                }
                walking = true;
            }

            Vector3 worldPos = Vector3.Lerp(startPoint, endPoint, progress);
            worldPos.y = KenyaNetworkData.GetElevationAt(worldPos.x, worldPos.z);
            transform.position = worldPos;

            Vector3 moveDir = (direction > 0f ? (endPoint - startPoint) : (startPoint - endPoint));
            moveDir.y = 0f;
            if (moveDir.sqrMagnitude > 0.01f)
            {
                Quaternion wantRot = Quaternion.LookRotation(moveDir.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, wantRot, Time.deltaTime * 4f);
            }

            // Articulated leg stride & gentle neck motion
            float stridePhase = walking ? Time.time * 5.2f + phaseOffset * 3f : 0f;
            if (legs != null)
            {
                for (int i = 0; i < legs.Length; i++)
                {
                    if (legs[i] == null) continue;
                    float swing = walking ? Mathf.Sin(stridePhase + (i % 2 == 0 ? 0f : Mathf.PI)) * 18f : 0f;
                    legs[i].localRotation = Quaternion.Euler(swing, 0f, 0f);
                }
            }
            if (neckPivot != null)
            {
                float nod = walking ? Mathf.Sin(stridePhase * 0.5f) * 5f : (Mathf.Sin(Time.time * 1.2f + phaseOffset) * 12f + 10f);
                neckPivot.localRotation = Quaternion.Euler(nod, 0f, 0f);
            }
        }
    }
}
