using System.Collections;
using UnityEngine;

namespace KenyaStreetSim
{
    /// <summary>
    /// Kenyan roadside ENA Coach Bus Stage pickup zone along the left curb.
    /// Highlights active target with pulsing golden floor strip + bobbing beacon,
    /// boards waiting commuters when the player slows/stops inside, and fires a scale-pop + confetti feel burst.
    /// </summary>
    public class BusStageZone : MonoBehaviour
    {
        [Header("Stage Config")]
        public int stageIndex;
        public string stageName = "CBD Stage";
        public int rewardKes = 1200;
        public int passengerCount = 8;
        public float requiredHoldSeconds = 1.6f;

        [Header("Visuals")]
        public Transform beaconVisual;
        public Renderer floorPadRenderer;
        public ParticleSystem confettiParticles;
        public KenyaPedestrianAI[] waitingCommuters;

        public bool IsCompleted { get; private set; }
        public bool IsCurrentActive { get; private set; }
        public float BoardingProgress { get; private set; }
        public bool PlayerInside { get; private set; }

        private Vector3 beaconBaseLocalPos;
        private Vector3 beaconBaseScale;
        private MaterialPropertyBlock padMpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            padMpb = new MaterialPropertyBlock();
            if (beaconVisual != null)
            {
                beaconBaseLocalPos = beaconVisual.localPosition;
                beaconBaseScale = beaconVisual.localScale;
            }
        }

        public void SetStageState(bool active, bool completed)
        {
            IsCurrentActive = active;
            IsCompleted = completed;
            if (!active)
            {
                BoardingProgress = 0f;
                PlayerInside = false;
            }
            UpdateVisualTint();
        }

        public void ResetStage(bool active)
        {
            IsCompleted = false;
            IsCurrentActive = active;
            BoardingProgress = 0f;
            PlayerInside = false;
            if (beaconVisual != null)
            {
                beaconVisual.localScale = beaconBaseScale;
            }
            if (waitingCommuters != null)
            {
                for (int i = 0; i < waitingCommuters.Length; i++)
                {
                    if (waitingCommuters[i] != null) waitingCommuters[i].ResetCommuter();
                }
            }
            UpdateVisualTint();
        }

        private void Update()
        {
            if (beaconVisual != null)
            {
                beaconVisual.gameObject.SetActive(IsCurrentActive && !IsCompleted);
                if (IsCurrentActive && !IsCompleted)
                {
                    float bob = Mathf.Sin(Time.time * 3.2f) * 0.35f;
                    beaconVisual.localPosition = beaconBaseLocalPos + Vector3.up * bob;
                    beaconVisual.Rotate(Vector3.up, 75f * Time.deltaTime, Space.Self);
                }
            }

            if (!IsCurrentActive || IsCompleted) return;
            if (StreetGameLoop.Instance == null || StreetGameLoop.Instance.State != StreetGameLoop.RunState.Running) return;

            var vehicle = StreetGameLoop.Instance.ActiveVehicle;
            if (vehicle == null) return;

            // Check proximity cleanly against stage center (forgiving bus-bay box so stopping near curb feels great)
            Vector3 localDelta = transform.InverseTransformPoint(vehicle.transform.position);
            PlayerInside = Mathf.Abs(localDelta.x) <= 5.2f && Mathf.Abs(localDelta.z) <= 9.5f;

            if (PlayerInside && vehicle.SpeedKmh <= 12f)
            {
                // STRICT VEHICLE JOB SEPARATION:
                // ONLY Buses board waiting 3D passengers! Kenya Police Patrol secures checkpoints (ZERO passengers),
                // and Isuzu FRR / TX Lorries load/deliver parcel cargo (ZERO passengers)!
                if (vehicle.jobRole == KenyaStreetVehicle.VehicleJobRole.BusPassenger)
                {
                    var doorSys = vehicle.GetComponent<KenyaVehicleDoorAndWipers>();
                    if (doorSys != null && doorSys.DoorOpen && waitingCommuters != null)
                    {
                        Vector3 doorWorld = doorSys.GetDoorWorldPoint();
                        for (int i = 0; i < waitingCommuters.Length; i++)
                        {
                            if (waitingCommuters[i] != null && waitingCommuters[i].gameObject.activeSelf)
                            {
                                waitingCommuters[i].BeginBoardingToDoor(doorWorld);
                            }
                        }
                    }
                }

                float holdDur = vehicle.jobRole == KenyaStreetVehicle.VehicleJobRole.BusPassenger
                    ? Mathf.Max(2.4f, requiredHoldSeconds)
                    : 1.6f;
                BoardingProgress = Mathf.Clamp01(BoardingProgress + Time.deltaTime / holdDur);
                if (BoardingProgress >= 1f)
                {
                    CompleteBoarding();
                }
            }
            else if (!PlayerInside)
            {
                BoardingProgress = Mathf.MoveTowards(BoardingProgress, 0f, Time.deltaTime * 0.8f);
            }
        }

        public void CompleteBoarding()
        {
            if (IsCompleted) return;
            IsCompleted = true;
            IsCurrentActive = false;
            BoardingProgress = 1f;
            UpdateVisualTint();

            var activeVeh = StreetGameLoop.Instance != null ? StreetGameLoop.Instance.ActiveVehicle : null;
            bool isPassengerBus = activeVeh == null || activeVeh.jobRole == KenyaStreetVehicle.VehicleJobRole.BusPassenger;

            // ONLY celebrate/board commuters when an actual passenger Bus arrives (never a Police Land Cruiser or Cargo Lorry!)
            if (isPassengerBus && waitingCommuters != null)
            {
                for (int i = 0; i < waitingCommuters.Length; i++)
                {
                    if (waitingCommuters[i] != null) waitingCommuters[i].TriggerBoardCelebration();
                }
            }

            if (confettiParticles != null)
            {
                confettiParticles.Play();
            }

            if (beaconVisual != null)
            {
                StartCoroutine(PopScaleRoutine(beaconVisual, beaconBaseScale));
            }

            if (StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.OnStageCompleted(this);
            }
        }

        private IEnumerator PopScaleRoutine(Transform target, Vector3 baseline)
        {
            yield return Ease.Tween(0.16f, t =>
            {
                if (target != null) target.localScale = baseline * Mathf.Lerp(1f, 1.32f, t);
            }, Ease.OutBack, unscaled: true);

            yield return Ease.Tween(0.16f, t =>
            {
                if (target != null) target.localScale = baseline * Mathf.Lerp(1.32f, 1f, t);
            }, Ease.OutCubic, unscaled: true);
        }

        private void UpdateVisualTint()
        {
            if (floorPadRenderer == null) return;
            Color col = IsCompleted
                ? Palette.StageZoneComplete
                : (IsCurrentActive ? Palette.StageZoneAccent : Palette.CurbStone);
            Color emissive = IsCurrentActive
                ? Palette.StageZoneAccent * 1.15f
                : (IsCompleted ? Palette.StageZoneComplete * 0.65f : Color.black);

            floorPadRenderer.GetPropertyBlock(padMpb);
            padMpb.SetColor(BaseColorId, col);
            padMpb.SetColor(EmissionColorId, emissive);
            floorPadRenderer.SetPropertyBlock(padMpb);
        }
    }
}
