using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Extrudes smooth curved PBR-textured Kenyan highway tarmac ribbons, murram road shoulders,
    /// white edge stripes, dashed yellow center lines, and circular multi-exit roundabouts
    /// across the full Kenya ENA Coach Road Network (ZERO speed bumps, ZERO Z-fighting).
    /// </summary>
    public static class KenyaRoadSplineBuilder
    {
        private const string RoadMeshFolder = "Assets/KenyaStreetSim/Meshes/Roads";

        public static void BuildAllHighwayAndRoundaboutGeometry(
            Transform parent,
            Material matTarmac,
            Material matMurram,
            Material matWhiteLine,
            Material matYellowLine,
            Material matCurb,
            Material matGrass)
        {
            if (!Directory.Exists(RoadMeshFolder))
            {
                Directory.CreateDirectory(RoadMeshFolder);
            }

            var root = new GameObject("KenyaHighwayNetworkMeshes").transform;
            root.SetParent(parent, false);

            // 1. Extrude all 6 connected Kenyan highways along smooth Catmull-Rom splines
            for (int i = 0; i < KenyaNetworkData.Highways.Length; i++)
            {
                var def = KenyaNetworkData.Highways[i];
                var pts = KenyaNetworkData.SampleHighway(def, 5.5f);

                // Smooth 14m wide tarmac strip (y = 0.00f) with MeshCollider for silky WheelCollider driving!
                var tarmacMesh = SaveRoadMesh($"RoadTarmac_{def.id}", BuildRibbonMesh(pts, halfWidth: 7.0f, yHeight: 0.0f, uvWidthRepeat: 2f, uvMetersPerRepeat: 7f));
                var tarmacGo = CreateMeshObj($"Tarmac_{def.id}", root, tarmacMesh, matTarmac, addMeshCollider: true);
                tarmacGo.isStatic = true;

                // Wide 28m red Kenyan murram highway shoulder apron (y = -0.025f) so tarmac blends naturally into hills!
                var shoulderMesh = SaveRoadMesh($"RoadShoulder_{def.id}", BuildRibbonMesh(pts, halfWidth: 15.5f, yHeight: -0.025f, uvWidthRepeat: 3f, uvMetersPerRepeat: 12f));
                CreateMeshObj($"MurramShoulder_{def.id}", root, shoulderMesh, matMurram, addMeshCollider: true).isStatic = true;

                // Left & Right continuous crisp white tarmac edge stripes (y = +0.015f)
                var edgeL = SaveRoadMesh($"EdgeL_{def.id}", BuildOffsetStripMesh(pts, lateralOffset: -6.55f, halfWidth: 0.11f, yHeight: 0.015f, dashed: false));
                var edgeR = SaveRoadMesh($"EdgeR_{def.id}", BuildOffsetStripMesh(pts, lateralOffset:  6.55f, halfWidth: 0.11f, yHeight: 0.015f, dashed: false));
                CreateMeshObj($"EdgeLineL_{def.id}", root, edgeL, matWhiteLine, false).isStatic = true;
                CreateMeshObj($"EdgeLineR_{def.id}", root, edgeR, matWhiteLine, false).isStatic = true;

                // Kenyan yellow dashed center divider along every highway curve!
                var centerYellow = SaveRoadMesh($"CenterLine_{def.id}", BuildOffsetStripMesh(pts, lateralOffset: 0f, halfWidth: 0.11f, yHeight: 0.016f, dashed: true));
                CreateMeshObj($"CenterLine_{def.id}", root, centerYellow, matYellowLine, false).isStatic = true;
            }

            // 2. Build both multi-exit circular Kenyan Roundabouts (Nairobi Interchange & Kisii Junction!)
            for (int r = 0; r < KenyaNetworkData.Roundabouts.Length; r++)
            {
                var rb = KenyaNetworkData.Roundabouts[r];
                var rGo = new GameObject(rb.name).transform;
                rGo.SetParent(root, false);
                rGo.position = new Vector3(rb.center.x, 0f, rb.center.y);

                // Smooth circular drivable tarmac disc (y = +0.005f) spanning all approach junctions cleanly
                var ringMesh = SaveRoadMesh($"RoundaboutTarmac_{r}", BuildAnnulusMesh(rb.innerRadius - 0.5f, rb.outerRadius, 0.005f, 48));
                CreateMeshObj("RoundaboutTarmac", rGo, ringMesh, matTarmac, addMeshCollider: true).isStatic = true;

                // Outer shoulder apron under roundabout
                var rShoulderMesh = SaveRoadMesh($"RoundaboutShoulder_{r}", BuildAnnulusMesh(0f, rb.outerRadius + 9f, -0.022f, 48));
                CreateMeshObj("RoundaboutShoulder", rGo, rShoulderMesh, matMurram, addMeshCollider: true).isStatic = true;

                // White roundabout ring lane markings
                var rMarkOuter = SaveRoadMesh($"RoundaboutOuterMark_{r}", BuildAnnulusMesh(rb.outerRadius - 0.7f, rb.outerRadius - 0.48f, 0.018f, 48));
                var rMarkMid = SaveRoadMesh($"RoundaboutMidMark_{r}", BuildAnnulusMesh((rb.innerRadius + rb.outerRadius) * 0.5f - 0.10f, (rb.innerRadius + rb.outerRadius) * 0.5f + 0.10f, 0.018f, 48));
                CreateMeshObj("RoundaboutOuterLine", rGo, rMarkOuter, matWhiteLine, false).isStatic = true;
                CreateMeshObj("RoundaboutMidLine", rGo, rMarkMid, matYellowLine, false).isStatic = true;

                // Central raised circular Kenyan landscaped island (curb + lush grass dome)
                var islandCurb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                islandCurb.name = "IslandCurb";
                islandCurb.transform.SetParent(rGo, false);
                islandCurb.transform.localPosition = new Vector3(0f, 0.16f, 0f);
                islandCurb.transform.localScale = new Vector3(rb.innerRadius * 2f, 0.18f, rb.innerRadius * 2f);
                islandCurb.GetComponent<Renderer>().sharedMaterial = matCurb;

                var islandGrass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                islandGrass.name = "IslandLawn";
                islandGrass.transform.SetParent(rGo, false);
                islandGrass.transform.localPosition = new Vector3(0f, 0.25f, 0f);
                islandGrass.transform.localScale = new Vector3((rb.innerRadius - 0.55f) * 2f, 0.16f, (rb.innerRadius - 0.55f) * 2f);
                islandGrass.GetComponent<Renderer>().sharedMaterial = matGrass;
                Object.DestroyImmediate(islandGrass.GetComponent<Collider>());
            }

            AssetDatabase.SaveAssets();
        }

        private static Mesh BuildRibbonMesh(List<Vector3> pts, float halfWidth, float yHeight, float uvWidthRepeat, float uvMetersPerRepeat)
        {
            int n = pts.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new int[(n - 1) * 6];

            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist += Vector3.Distance(pts[i - 1], pts[i]);
                Vector3 fwd = GetForward(pts, i);
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

                Vector3 p = pts[i];
                p.y = yHeight;
                verts[i * 2] = p - right * halfWidth;
                verts[i * 2 + 1] = p + right * halfWidth;

                float vCoord = dist / Mathf.Max(0.5f, uvMetersPerRepeat);
                uvs[i * 2] = new Vector2(0f, vCoord);
                uvs[i * 2 + 1] = new Vector2(uvWidthRepeat, vCoord);
            }

            int ti = 0;
            for (int i = 0; i < n - 1; i++)
            {
                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                // Clockwise viewed from +Y (left -> forwardLeft -> forwardRight) => upward +Y normal!
                tris[ti++] = i0;
                tris[ti++] = i2;
                tris[ti++] = i1;

                tris[ti++] = i1;
                tris[ti++] = i2;
                tris[ti++] = i3;
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildOffsetStripMesh(List<Vector3> pts, float lateralOffset, float halfWidth, float yHeight, bool dashed)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float dist = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float segLen = Vector3.Distance(pts[i], pts[i + 1]);
                bool draw = !dashed || (((dist / 9.5f) % 1f) < 0.46f);
                dist += segLen;
                if (!draw) continue;

                Vector3 fwdA = GetForward(pts, i);
                Vector3 rightA = Vector3.Cross(Vector3.up, fwdA).normalized;
                Vector3 fwdB = GetForward(pts, i + 1);
                Vector3 rightB = Vector3.Cross(Vector3.up, fwdB).normalized;

                Vector3 cA = pts[i] + rightA * lateralOffset; cA.y = yHeight;
                Vector3 cB = pts[i + 1] + rightB * lateralOffset; cB.y = yHeight;

                int idx = verts.Count;
                verts.Add(cA - rightA * halfWidth);
                verts.Add(cA + rightA * halfWidth);
                verts.Add(cB - rightB * halfWidth);
                verts.Add(cB + rightB * halfWidth);

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));

                tris.Add(idx);
                tris.Add(idx + 2);
                tris.Add(idx + 1);

                tris.Add(idx + 1);
                tris.Add(idx + 2);
                tris.Add(idx + 3);
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildAnnulusMesh(float innerR, float outerR, float yHeight, int segments)
        {
            var verts = new Vector3[(segments + 1) * 2];
            var uvs = new Vector2[verts.Length];
            var tris = new int[segments * 6];

            for (int i = 0; i <= segments; i++)
            {
                float u = (float)i / segments;
                float ang = u * Mathf.PI * 2f;
                float c = Mathf.Cos(ang);
                float s = Mathf.Sin(ang);

                verts[i * 2] = new Vector3(c * innerR, yHeight, s * innerR);
                verts[i * 2 + 1] = new Vector3(c * outerR, yHeight, s * outerR);
                uvs[i * 2] = new Vector2(0f, u * 16f);
                uvs[i * 2 + 1] = new Vector2(2f, u * 16f);
            }

            int ti = 0;
            for (int i = 0; i < segments; i++)
            {
                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                // Verified Cross((0,0,+dz), (+dr,0,0)) = +Y upward normal!
                tris[ti++] = i0;
                tris[ti++] = i2;
                tris[ti++] = i1;

                tris[ti++] = i1;
                tris[ti++] = i2;
                tris[ti++] = i3;
            }

            var mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 GetForward(List<Vector3> pts, int i)
        {
            if (i <= 0) return (pts[1] - pts[0]).normalized;
            if (i >= pts.Count - 1) return (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
            return (pts[i + 1] - pts[i - 1]).normalized;
        }

        private static GameObject CreateMeshObj(string name, Transform parent, Mesh mesh, Material mat, bool addMeshCollider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (addMeshCollider)
            {
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
            return go;
        }

        private static Mesh SaveRoadMesh(string name, Mesh mesh)
        {
            mesh.name = name;
            string path = $"{RoadMeshFolder}/{name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                existing.Clear();
                existing.indexFormat = mesh.indexFormat;
                existing.vertices = mesh.vertices;
                existing.uv = mesh.uv;
                existing.triangles = mesh.triangles;
                existing.normals = mesh.normals;
                existing.tangents = mesh.tangents;
                existing.RecalculateBounds();
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }
    }
}
