using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace KenyaBusSim
{
    /// <summary>
    /// Manages switching between Kenyan fleet vehicles (ENA Coach 8-Wheeler & Isuzu FRR 6-Wheeler)
    /// and binds the active vehicle to the camera and HUD.
    /// </summary>
    public class KenyaFleetManager : MonoBehaviour
    {
        public List<KenyaVehicleController> vehicles = new List<KenyaVehicleController>();
        public KenyaSimCamera simCamera;
        public int activeIndex = 0;

        public KenyaVehicleController ActiveVehicle =>
            (vehicles != null && vehicles.Count > 0 && activeIndex >= 0 && activeIndex < vehicles.Count)
                ? vehicles[activeIndex]
                : null;

        private void Start()
        {
            if (simCamera == null)
            {
#if UNITY_6000_0_OR_NEWER
                simCamera = FindFirstObjectByType<KenyaSimCamera>();
#else
                simCamera = FindObjectOfType<KenyaSimCamera>();
#endif
            }

            SelectVehicle(activeIndex, keepPosition: false);
        }

        private void Update()
        {
            bool switchPressed = false;
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null && kb.vKey.wasPressedThisFrame) switchPressed = true;
#else
            if (Input.GetKeyDown(KeyCode.V)) switchPressed = true;
#endif
            if (switchPressed)
            {
                SwitchNextVehicle();
            }
        }

        public void SwitchNextVehicle()
        {
            if (vehicles == null || vehicles.Count <= 1) return;
            int next = (activeIndex + 1) % vehicles.Count;
            SelectVehicle(next, keepPosition: true);
        }

        public void SelectVehicle(int index, bool keepPosition = true)
        {
            if (vehicles == null || vehicles.Count == 0) return;
            index = Mathf.Clamp(index, 0, vehicles.Count - 1);

            Vector3 spawnPos = Vector3.zero;
            Quaternion spawnRot = Quaternion.identity;
            bool hasPrevious = keepPosition && ActiveVehicle != null;
            if (hasPrevious)
            {
                spawnPos = ActiveVehicle.transform.position + Vector3.up * 0.35f;
                spawnRot = Quaternion.Euler(0f, ActiveVehicle.transform.eulerAngles.y, 0f);
            }

            for (int i = 0; i < vehicles.Count; i++)
            {
                if (vehicles[i] == null) continue;
                bool isSelected = (i == index);
                vehicles[i].gameObject.SetActive(isSelected);
                vehicles[i].IsPlayerControlled = isSelected;

                if (isSelected)
                {
                    if (hasPrevious)
                    {
                        vehicles[i].transform.SetPositionAndRotation(spawnPos, spawnRot);
                        Rigidbody rb = vehicles[i].GetComponent<Rigidbody>();
                        if (rb != null)
                        {
#if UNITY_6000_0_OR_NEWER
                            rb.linearVelocity = Vector3.zero;
#else
                            rb.velocity = Vector3.zero;
#endif
                            rb.angularVelocity = Vector3.zero;
                        }
                    }

                    vehicles[i].ResetCondition();
                    vehicles[i].ShowAlert("Switched to " + vehicles[i].displayName, 2.5f);
                }
            }

            activeIndex = index;
            if (simCamera != null && ActiveVehicle != null)
            {
                simCamera.SetTarget(ActiveVehicle);
            }
        }
    }
}
