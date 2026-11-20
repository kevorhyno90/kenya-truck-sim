using UnityEngine;

namespace KenyaBusSim
{
    /// <summary>
    /// Trigger zone placed before Kenyan highway speed bumps to display
    /// the "PUNGUZA MWENDO: SPEED BUMP AHEAD!" warning on the HUD.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class SpeedBumpWarningZone : MonoBehaviour
    {
        private void Reset()
        {
            BoxCollider col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            KenyaVehicleController vehicle = other.GetComponentInParent<KenyaVehicleController>();
            if (vehicle != null && vehicle.IsPlayerControlled)
            {
                vehicle.NearSpeedBump = true;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            KenyaVehicleController vehicle = other.GetComponentInParent<KenyaVehicleController>();
            if (vehicle != null && vehicle.IsPlayerControlled)
            {
                vehicle.NearSpeedBump = false;
            }
        }
    }
}
