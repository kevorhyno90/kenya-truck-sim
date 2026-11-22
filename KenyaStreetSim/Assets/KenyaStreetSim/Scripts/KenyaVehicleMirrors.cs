using TMPro;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KenyaStreetSim
{
    /// <summary>
    /// Live ETS2-style rear-view Left & Right Side Mirrors + Live 3D Cockpit Dashboard Telemetry.
    /// Uses lightweight RenderTextures (256x384) and ONLY renders mirror cameras while the player
    /// is inside Cockpit view (C key) so frame rate stays smooth across the entire Kenya map.
    /// </summary>
    public class KenyaVehicleMirrors : MonoBehaviour
    {
        [Header("Mirror Render Targets")]
        public Renderer[] leftMirrorSurfaces;
        public Renderer[] rightMirrorSurfaces;
        public Transform leftMirrorCameraAnchor;
        public Transform rightMirrorCameraAnchor;

        [Header("Live 3D Dashboard Telemetry")]
        public TextMeshPro dashSpeedText;
        public TextMeshPro dashStatusText;

        private Camera leftMirrorCam;
        private Camera rightMirrorCam;
        private RenderTexture leftRt;
        private RenderTexture rightRt;
        private Material leftMirrorMat;
        private Material rightMirrorMat;
        private KenyaStreetVehicle vehicle;

        private void Awake()
        {
            vehicle = GetComponent<KenyaStreetVehicle>();
            SetupMirror("LeftMirror", leftMirrorCameraAnchor, leftMirrorSurfaces, out leftRt, out leftMirrorCam, out leftMirrorMat);
            SetupMirror("RightMirror", rightMirrorCameraAnchor, rightMirrorSurfaces, out rightRt, out rightMirrorCam, out rightMirrorMat);
        }

        private void OnDestroy()
        {
            if (leftMirrorCam != null) leftMirrorCam.targetTexture = null;
            if (rightMirrorCam != null) rightMirrorCam.targetTexture = null;
            if (leftRt != null) { leftRt.Release(); Destroy(leftRt); }
            if (rightRt != null) { rightRt.Release(); Destroy(rightRt); }
            if (leftMirrorMat != null) Destroy(leftMirrorMat);
            if (rightMirrorMat != null) Destroy(rightMirrorMat);
        }

        private void SetupMirror(
            string label,
            Transform camAnchor,
            Renderer[] surfaces,
            out RenderTexture rt,
            out Camera mirrorCam,
            out Material mat)
        {
            rt = null;
            mirrorCam = null;
            mat = null;
            if (camAnchor == null || surfaces == null || surfaces.Length == 0) return;

            rt = new RenderTexture(256, 384, 16, RenderTextureFormat.Default)
            {
                name = $"{name}_{label}_RT",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();

            var camGo = new GameObject($"{label}Camera");
            camGo.transform.SetParent(camAnchor, false);
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.identity;

            mirrorCam = camGo.AddComponent<Camera>();
            mirrorCam.targetTexture = rt;
            mirrorCam.fieldOfView = 42f;
            mirrorCam.nearClipPlane = 0.25f;
            mirrorCam.farClipPlane = 320f;
            mirrorCam.depth = -5f;
            mirrorCam.allowHDR = false;
            mirrorCam.allowMSAA = false;
            mirrorCam.enabled = false;

            var urpData = camGo.AddComponent<UniversalAdditionalCameraData>();
            urpData.renderPostProcessing = false;
            urpData.renderShadows = false;
            urpData.antialiasing = AntialiasingMode.None;

            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Unlit/Texture");
            mat = new Material(unlitShader);
            mat.SetTexture("_BaseMap", rt);
            mat.mainTexture = rt;
            // Flip horizontally so the rear-view camera reflects like a real physical vehicle mirror!
            mat.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
            mat.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            mat.mainTextureScale = new Vector2(-1f, 1f);
            mat.mainTextureOffset = new Vector2(1f, 0f);
            mat.SetColor("_BaseColor", new Color(0.95f, 0.98f, 1f, 1f));

            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] != null)
                {
                    surfaces[i].sharedMaterial = mat;
                }
            }
        }

        private void LateUpdate()
        {
            var loop = StreetGameLoop.Instance;
            bool isActiveVehicle = loop != null && loop.ActiveVehicle == vehicle;
            bool inCockpit = isActiveVehicle &&
                             loop.streetCamera != null &&
                             loop.streetCamera.CurrentMode == KenyaStreetCamera.CameraMode.CockpitRHD &&
                             loop.State == StreetGameLoop.RunState.Running;

            if (leftMirrorCam != null && leftMirrorCam.enabled != inCockpit)
            {
                leftMirrorCam.enabled = inCockpit;
            }
            if (rightMirrorCam != null && rightMirrorCam.enabled != inCockpit)
            {
                rightMirrorCam.enabled = inCockpit;
            }

            if (inCockpit && vehicle != null)
            {
                if (dashSpeedText != null)
                {
                    dashSpeedText.text = $"{Mathf.RoundToInt(vehicle.SpeedKmh):00} km/h  |  G:{vehicle.GearLabel}";
                }
                if (dashStatusText != null)
                {
                    string sig = vehicle.HazardsOn ? "◄► HAZ"
                               : vehicle.LeftSignalOn ? "◄ LEFT"
                               : vehicle.RightSignalOn ? "RIGHT ►"
                               : "ENA GPS OK";
                    string stage = loop.ActiveStage != null ? loop.ActiveStage.stageName : "KENYA";
                    if (stage.Length > 20) stage = stage.Substring(0, 20);
                    dashStatusText.text = $"{sig}\n{stage}";
                }
            }
        }
    }
}
