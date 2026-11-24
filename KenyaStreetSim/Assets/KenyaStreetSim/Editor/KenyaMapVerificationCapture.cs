using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Renders crisp 1280x720 URP visual audit postcards across Kenya's verified roundabouts,
    /// ecological forests, continuous tea hills, sugarcane belts, Tsavo baobabs & coast.
    /// </summary>
    public static class KenyaMapVerificationCapture
    {
        private const string OutDir = "C:/Users/USER/.gemini/antigravity/brain/1f115163-e25f-4fe7-8681-12dc36b17000/scratch/map_verify_v2";

        [MenuItem("Kenya Street Sim/Capture 6-Region Kenya Map Audit")]
        public static void CaptureAllRegions()
        {
            Directory.CreateDirectory(OutDir);

            // Position spline AI traffic cleanly at their initial highway poses for Edit-Mode audit
            foreach (var ai in Object.FindObjectsByType<KenyaSplineTraffic>())
            {
                if (ai.highwayIndex < 0 || ai.highwayIndex >= KenyaNetworkData.Highways.Length) continue;
                var pts = KenyaNetworkData.SampleHighway(KenyaNetworkData.Highways[ai.highwayIndex], 6f);
                if (pts == null || pts.Count < 4) continue;
                float t = ai.limitToSubRange
                    ? Mathf.Lerp(ai.rangeMinNorm, ai.rangeMaxNorm, Mathf.Clamp01(ai.startNormalizedProgress))
                    : Mathf.Clamp01(ai.startNormalizedProgress);
                int idx = Mathf.Clamp(Mathf.RoundToInt(t * (pts.Count - 1)), 1, pts.Count - 2);
                Vector3 fwd = (pts[idx + 1] - pts[idx - 1]);
                if (!ai.forwardDirection) fwd = -fwd;
                Vector3 flatFwd = new Vector3(fwd.x, 0f, fwd.z).normalized;
                Vector3 left = Vector3.Cross(flatFwd, Vector3.up).normalized;
                Vector3 p = pts[idx] + left * ai.laneOffsetMeters;
                p.y = KenyaNetworkData.GetElevationAt(p.x, p.z);
                ai.transform.SetPositionAndRotation(p, Quaternion.LookRotation(fwd.normalized, Vector3.up));
            }

            foreach (var jet in Object.FindObjectsByType<KenyaAirportPlaneAI>())
            {
                Vector3 p = jet.airportCenter + new Vector3(28f, 24f, -15f);
                jet.transform.SetPositionAndRotation(p, Quaternion.Euler(-8f, -35f, -14f));
            }

            // 1. Total / Mau Summit Roundabout (ZERO stations on roundabout!) + Mau Forest Complex!
            SnapAndSave("01_Total_Roundabout_And_MauForest.png",
                AtGround(-715f, 738f, 18f), AtGround(-800f, 768f, 3f));

            // 2. Kericho Continuous Sculpted 3D Tea Plantations & Emerald Tea Biome Hills!
            SnapAndSave("02_Kericho_Emerald_Tea_Hills.png",
                AtGround(-1085f, 415f, 24f), AtGround(-1005f, 495f, 2f));

            // 3. Timboroa Highland Montane Conifer Forest flanking A104 Climbing Lane!
            SnapAndSave("03_Timboroa_Montane_Forest.png",
                AtGround(-815f, 895f, 18f), AtGround(-865f, 990f, 6f));

            // 4. Kakamega Tropical Equatorial Rainforest towering along A1!
            SnapAndSave("04_Kakamega_Tropical_Rainforest.png",
                AtGround(-1405f, 1085f, 19f), AtGround(-1355f, 1185f, 8f));

            // 5. Tsavo Red-Earth Laterite Corridor with Iconic Bulbous Kenyan Baobab Trees!
            SnapAndSave("05_Tsavo_Baobabs_RedEarth.png",
                AtGround(480f, -95f, 14f), AtGround(575f, -155f, 4f));

            // 6. Kilifi – Arabuko-Sokoke Coastal Forest, Palms, Golden Sand & Indian Ocean!
            SnapAndSave("06_Kilifi_Arabuko_Coast.png",
                AtGround(1125f, -195f, 22f), AtGround(1210f, -45f, 5f));

            // 7. Awendo South Nyanza Sugarcane Belt along A1!
            SnapAndSave("07_Awendo_Sugarcane_Belt.png",
                AtGround(-1640f, -145f, 15f), AtGround(-1665f, -245f, 3f));

            Debug.Log($"[KenyaMapVerificationCapture] Saved 7 ecological & roundabout audit PNGs to {OutDir}");
        }

        [MenuItem("Kenya Street Sim/Debug/Start Run In PlayMode")]
        public static void StartRunInPlayMode()
        {
            if (Application.isPlaying && StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.StartRun();
            }
        }

        [MenuItem("Kenya Street Sim/Debug/Watch Nairobi Roundabout In PlayMode")]
        public static void WatchNairobiRoundaboutInPlayMode()
        {
            if (!Application.isPlaying) return;
            StreetGameLoop.Instance?.StartRun();
            if (Camera.main != null)
            {
                var follow = Camera.main.GetComponent<KenyaStreetCamera>();
                if (follow != null) follow.enabled = false;
                float ry = KenyaNetworkData.GetElevationAt(0f, 170f);
                Camera.main.transform.position = new Vector3(-42f, ry + 28f, 128f);
                Camera.main.transform.LookAt(new Vector3(0f, ry + 1f, 170f));
            }
        }

        [MenuItem("Kenya Street Sim/Debug/Open Full 41-Town Map In PlayMode")]
        public static void OpenFullMapInPlayMode()
        {
            if (Application.isPlaying)
            {
                StreetGameLoop.Instance?.StartRun();
                var map = Object.FindAnyObjectByType<KenyaRouteMapUI>();
                map?.SetFullMapOpen(true);
            }
        }

        [MenuItem("Kenya Street Sim/Debug/Verify Cockpit View In PlayMode")]
        public static void VerifyCockpitInPlayMode()
        {
            if (!Application.isPlaying) return;
            StreetGameLoop.Instance?.StartRun();
            KenyaBusCareerManager.Instance?.CloseOffice();
            var cam = Object.FindAnyObjectByType<KenyaStreetCamera>();
            cam?.SetMode(KenyaStreetCamera.CameraMode.CockpitRHD);
            cam?.SnapToTarget();
        }

        [MenuItem("Kenya Street Sim/Debug/Verify Business Office In PlayMode")]
        public static void VerifyOfficeInPlayMode()
        {
            if (!Application.isPlaying) return;
            StreetGameLoop.Instance?.StartRun();
            if (KenyaBusCareerManager.Instance != null && !KenyaBusCareerManager.Instance.IsOfficeOpen)
            {
                KenyaBusCareerManager.Instance.ToggleOffice();
            }
        }

        private static Vector3 AtGround(float x, float z, float yAbove)
        {
            return new Vector3(x, KenyaNetworkData.GetElevationAt(x, z) + yAbove, z);
        }

        private static void SnapAndSave(string filename, Vector3 camPos, Vector3 lookAt)
        {
            var camGo = new GameObject("TempVerifyCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 52f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 1800f;
            cam.allowHDR = true;
            var urp = camGo.AddComponent<UniversalAdditionalCameraData>();
            urp.renderPostProcessing = true;
            camGo.transform.position = camPos;
            camGo.transform.LookAt(lookAt);

            int w = 1280, h = 720;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(Path.Combine(OutDir, filename), tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo);
        }
    }
}
