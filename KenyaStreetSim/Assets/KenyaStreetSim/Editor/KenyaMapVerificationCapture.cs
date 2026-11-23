using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Renders crisp 1280x720 URP visual audit postcards across all signature regions of the
    /// 41-Town National Kenya Highway Map (6-Lane Nairobi, Narok Wildlife, Kericho/Chebilat Tea,
    /// Salgaa-Total Climbing Semi-Trailer, Kisumu Airport & Lake, Mombasa Tusks & Coast).
    /// </summary>
    public static class KenyaMapVerificationCapture
    {
        private const string OutDir = "C:/Users/USER/.gemini/antigravity/brain/1f115163-e25f-4fe7-8681-12dc36b17000/scratch/map_verify";

        [MenuItem("Kenya Street Sim/Capture 6-Region Kenya Map Audit")]
        public static void CaptureAllRegions()
        {
            Directory.CreateDirectory(OutDir);

            // Place all spline AI traffic at their initial poses in Edit Mode so semi-trailers & cars are visible!
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

            // Place flying jets along their low scenic flyover pass so they show up vividly in airport shots!
            foreach (var jet in Object.FindObjectsByType<KenyaAirportPlaneAI>())
            {
                Vector3 p = jet.airportCenter + new Vector3(28f, 24f, -15f);
                jet.transform.SetPositionAndRotation(p, Quaternion.Euler(-8f, -35f, -14f));
            }

            // 1. Nairobi CBD 6-Lane Expressway + KICC + Savanna Coach + Petrol Station
            SnapAndSave("01_Nairobi_6Lane_CBD.png",
                AtGround(-12f, -44f, 11.5f), AtGround(2f, 48f, 6.0f));

            // 2. Narok Wildlife Crossing (Zebras, Giraffes & Gazelles on B3!)
            var narokZone = GameObject.Find("Narok_Suswa_Mara_Crossing");
            if (narokZone != null)
            {
                Vector3 c = FindFirstChildPos(narokZone.transform, new Vector3(-475f, KenyaNetworkData.GetElevationAt(-475f, 135f), 135f));
                SnapAndSave("02_Narok_Wildlife_Crossing.png",
                    new Vector3(c.x + 28f, c.y + 8.5f, c.z - 22f),
                    new Vector3(c.x - 4f, c.y + 1.8f, c.z + 2f));
            }

            // 3. Kericho & Chebilat Terraced Emerald Tea Plantations + B1 Highway
            SnapAndSave("03_Kericho_Tea_Plantations.png",
                AtGround(-1095f, 425f, 28f), AtGround(-1015f, 510f, 2f));

            // 4. Salgaa – Total 3-Lane Climbing Lane + 16.5m Semi-Trailer Transit Truck!
            var semi = GameObject.Find("HeavyTransit_NakuruSalgaaTrailer") ?? GameObject.Find("HeavyTransit_TotalEldoretTrailer");
            if (semi != null)
            {
                Vector3 s = semi.transform.position;
                Vector3 f = semi.transform.forward;
                Vector3 r = semi.transform.right;
                SnapAndSave("04_Salgaa_Climbing_SemiTrailer.png",
                    s + f * 22f + r * 11f + Vector3.up * 7.5f,
                    s + Vector3.up * 2.2f);
            }
            else
            {
                SnapAndSave("04_Salgaa_Climbing_SemiTrailer.png",
                    AtGround(-615f, 705f, 16f), AtGround(-675f, 745f, 2f));
            }

            // 5. Kisumu Lakeside City + Airport + Overhead Jet + Kibuye Market
            var kisAir = GameObject.Find("Airport_KIS_Kisumu");
            Vector3 kisTarget = kisAir != null ? kisAir.transform.position : AtGround(-1585f, 905f, 4f);
            SnapAndSave("05_Kisumu_Airport_And_City.png",
                kisTarget + new Vector3(88f, 34f, -65f),
                kisTarget + new Vector3(0f, 11f, 0f));

            // 6. Mombasa Iconic Crossed Tusks + Palms + Coast
            SnapAndSave("06_Mombasa_Tusks_Coast.png",
                AtGround(928f, -335f, 9.5f), AtGround(972f, -358f, 5.5f));

            // 7. Daraja Mbili Roadside Market (Kisii) + Vibrant Canopies & Produce Stalls!
            var mkt = GameObject.Find("Market_0_Kisii Town") ?? GameObject.Find("Market_3_Kisumu City");
            if (mkt != null)
            {
                Vector3 mp = mkt.transform.position;
                Vector3 mf = mkt.transform.forward;
                Vector3 mr = mkt.transform.right;
                SnapAndSave("07_Roadside_Market.png",
                    mp + mf * 22f + mr * 10f + Vector3.up * 8.0f,
                    mp + Vector3.up * 2.0f);
            }

            Debug.Log($"[KenyaMapVerificationCapture] Saved 7 URP regional audit PNGs to {OutDir}");
        }

        [MenuItem("Kenya Street Sim/Debug/Start Run In PlayMode")]
        public static void StartRunInPlayMode()
        {
            if (Application.isPlaying && StreetGameLoop.Instance != null)
            {
                StreetGameLoop.Instance.StartRun();
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

        private static Vector3 AtGround(float x, float z, float yAbove)
        {
            return new Vector3(x, KenyaNetworkData.GetElevationAt(x, z) + yAbove, z);
        }

        private static Vector3 FindFirstChildPos(Transform parent, Vector3 fallback)
        {
            foreach (Transform c in parent)
            {
                if (c.name.Contains("Zebra") || c.name.Contains("Giraffe") || c.name.Contains("Elephant"))
                    return c.position;
            }
            return fallback;
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
