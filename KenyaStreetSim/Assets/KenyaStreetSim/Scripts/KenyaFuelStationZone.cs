using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Interactive Kenyan Highway Petrol Station refuel forecourt zone.
    /// Pull under the canopy and slow down (< 14 km/h) to top up your diesel tank to 100%!
    /// </summary>
    public class KenyaFuelStationZone : MonoBehaviour
    {
        public string stationName = "Kenya Highway Service Station";
        public float refuelRadius = 11.5f;
        public float refuelRatePerSec = 0.38f;

        private bool notifiedFull;
        private bool wasInside;

        private void Update()
        {
            var loop = StreetGameLoop.Instance;
            if (loop == null || loop.State != StreetGameLoop.RunState.Running || loop.ActiveVehicle == null)
            {
                wasInside = false;
                return;
            }

            var veh = loop.ActiveVehicle;
            Vector3 diff = veh.transform.position - transform.position;
            diff.y = 0f;
            bool inside = diff.sqrMagnitude <= refuelRadius * refuelRadius;

            if (!inside)
            {
                wasInside = false;
                notifiedFull = false;
                return;
            }

            if (!wasInside)
            {
                wasInside = true;
                if (veh.fuelNormalized < 0.99f)
                {
                    loop.hud?.ShowToast($"AT {stationName.ToUpperInvariant()} — SLOW DOWN TO REFUEL DIESEL");
                }
            }

            if (veh.SpeedKmh < 14f && veh.fuelNormalized < 0.999f)
            {
                veh.RefuelStep(refuelRatePerSec * Time.deltaTime);
                if (veh.fuelNormalized >= 0.999f && !notifiedFull)
                {
                    notifiedFull = true;
                    KenyaStreetAudio.Instance?.PlayPickupBlip();
                    loop.hud?.ShowToast($"REFUELED AT {stationName.ToUpperInvariant()} — 100% FULL TANK!");
                }
            }
        }
    }
}
