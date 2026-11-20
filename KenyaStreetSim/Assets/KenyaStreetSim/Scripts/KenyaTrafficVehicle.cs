using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Passing Left-Hand Traffic (LHT) AI vehicle (Kenyan Matatus, Sedans/Probox, Delivery Lorries).
    /// Cruises along boulevard or cross-street lanes, smoothly yields at red traffic lights,
    /// brakes behind slower vehicles/player/crossing pedestrians, and spins its wheels.
    /// </summary>
    public class KenyaTrafficVehicle : MonoBehaviour
    {
        public enum RouteAxis { BoulevardNorthSouth, CrossStreetEastWest }

        [Header("Route")]
        public RouteAxis axis = RouteAxis.BoulevardNorthSouth;
        [Tooltip("+1 for North (+Z) or East (+X), -1 for South (-Z) or West (-X)")]
        public int directionSign = 1;
        public float laneCoordinate = -3.5f;
        public float minLoopCoord = -75f;
        public float maxLoopCoord = 535f;

        [Header("Driving")]
        public float cruiseSpeed = 12f; // m/s (~43 km/h)
        public float vehicleHalfLength = 2.6f;
        public float safeStopDistance = 7.5f;
        public float wheelRadius = 0.36f;
        public Transform[] wheelVisuals;

        private float currentSpeed;
        private Vector3 moveDir;
        private float fixedCrossCoord;

        private void Start()
        {
            currentSpeed = cruiseSpeed * 0.7f;
            if (axis == RouteAxis.BoulevardNorthSouth)
            {
                moveDir = directionSign > 0 ? Vector3.forward : Vector3.back;
                fixedCrossCoord = transform.position.x;
            }
            else
            {
                moveDir = directionSign > 0 ? Vector3.right : Vector3.left;
                fixedCrossCoord = transform.position.z;
            }
            transform.rotation = Quaternion.LookRotation(moveDir, Vector3.up);
        }

        private void Update()
        {
            float targetSpeed = cruiseSpeed;

            if (ShouldStopForTrafficLight())
            {
                targetSpeed = 0f;
            }
            else if (CheckObstacleAhead(out float obstacleDist))
            {
                float ratio = Mathf.InverseLerp(safeStopDistance * 0.45f, safeStopDistance * 1.9f, obstacleDist);
                targetSpeed = cruiseSpeed * ratio;
                if (obstacleDist < safeStopDistance * 0.6f)
                {
                    targetSpeed = 0f;
                }
            }

            float accelRate = targetSpeed < currentSpeed ? 14f : 5.5f;
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accelRate * Time.deltaTime);

            Vector3 pos = transform.position + moveDir * (currentSpeed * Time.deltaTime);

            // Wrap seamlessly along corridor
            if (axis == RouteAxis.BoulevardNorthSouth)
            {
                pos.x = laneCoordinate;
                if (directionSign > 0 && pos.z > maxLoopCoord)
                {
                    pos.z = minLoopCoord;
                }
                else if (directionSign < 0 && pos.z < minLoopCoord)
                {
                    pos.z = maxLoopCoord;
                }
            }
            else
            {
                pos.z = fixedCrossCoord;
                if (directionSign > 0 && pos.x > maxLoopCoord)
                {
                    pos.x = minLoopCoord;
                }
                else if (directionSign < 0 && pos.x < minLoopCoord)
                {
                    pos.x = maxLoopCoord;
                }
            }

            transform.position = pos;

            // Spin visual wheels
            if (wheelVisuals != null && wheelRadius > 0.05f && currentSpeed > 0.05f)
            {
                float deg = (currentSpeed * Time.deltaTime / wheelRadius) * Mathf.Rad2Deg;
                for (int i = 0; i < wheelVisuals.Length; i++)
                {
                    if (wheelVisuals[i] != null)
                    {
                        wheelVisuals[i].Rotate(Vector3.right, deg, Space.Self);
                    }
                }
            }
        }

        private bool ShouldStopForTrafficLight()
        {
            var lights = StreetTrafficLight.ActiveIntersections;
            if (lights.Count == 0) return false;

            Vector3 pos = transform.position;
            for (int i = 0; i < lights.Count; i++)
            {
                var light = lights[i];
                if (light == null) continue;

                if (axis == RouteAxis.BoulevardNorthSouth)
                {
                    if (!light.IsMainBoulevardStop) continue;
                    // Stop line is 10.5m before intersection center
                    float stopLineZ = light.intersectionZ - directionSign * 10.5f;
                    float distToStop = (stopLineZ - pos.z) * directionSign;
                    if (distToStop > 0.2f && distToStop < 11.5f)
                    {
                        return true;
                    }
                }
                else
                {
                    if (Mathf.Abs(pos.z - light.intersectionZ) > 9f) continue;
                    if (!light.IsCrossStreetStop) continue;
                    float stopLineX = -directionSign * 10.5f;
                    float distToStop = (stopLineX - pos.x) * directionSign;
                    if (distToStop > 0.2f && distToStop < 11.5f)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CheckObstacleAhead(out float nearestDist)
        {
            nearestDist = float.MaxValue;
            Vector3 origin = transform.position + Vector3.up * 1.05f + moveDir * (vehicleHalfLength + 0.35f);
            float checkRange = safeStopDistance * 2.0f;

            if (Physics.BoxCast(
                    origin,
                    new Vector3(0.95f, 0.7f, 0.25f),
                    moveDir,
                    out RaycastHit hit,
                    transform.rotation,
                    checkRange,
                    ~0,
                    QueryTriggerInteraction.Ignore))
            {
                if (!hit.transform.IsChildOf(transform))
                {
                    nearestDist = hit.distance;
                    return true;
                }
            }

            return false;
        }
    }
}
