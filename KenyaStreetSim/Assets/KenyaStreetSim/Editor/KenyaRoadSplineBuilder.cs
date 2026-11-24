using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Extrudes 3D-elevation-aware, variable-width PBR Kenyan highway tarmac ribbons:
    /// - 4–6 Lane Divided City Expressways (Nairobi-Thika, Kisumu, Nakuru, Eldoret, Mombasa) with inner dashed lanes & concrete median
    /// - 3-Lane Dedicated Climbing-Lane Escarpments (Mai Mahiu, Salgaa-Total, Timboroa, Kericho, Sondu) with extra slow-vehicle lane
    /// - 2-Lane Flat & Highland Trunk Highways with murram embankments sealed smoothly into 3D terrain
    /// - All 10 Elevation-Matched Multi-Exit Kenyan Roundabouts (ZERO speed bumps, ZERO Z-fighting).
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

            // 1. Extrude all 15 interconnected Kenyan highways along 3D Catmull-Rom splines
            for (int i = 0; i < KenyaNetworkData.Highways.Length; i++)
            {
                var def = KenyaNetworkData.Highways[i];
                var pts = KenyaNetworkData.SampleHighway(def, 6.0f);

                // Precompute smooth variable per-point road half-width (7.0m trunk -> 9.0m climbing -> 10.8m 6-lane city!)
                var halfWidths = new float[pts.Count];
                for (int k = 0; k < pts.Count; k++)
                {
                    halfWidths[k] = KenyaNetworkData.GetRoadHalfWidthAt(def, new Vector2(pts[k].x, pts[k].z));
                }

                // Smooth 3D tarmac ribbon (yOffset = 0.00f) — cleanly clips inside roundabout center islands!
                var tarmacMesh = SaveRoadMesh($"RoadTarmac_{def.id}", BuildVariableRibbonMesh(pts, halfWidths, extraMargin: 0f, yOffset: 0.0f, outerEdgeDrop: 0.0f, uvWidthRepeat: 2.5f, uvMetersPerRepeat: 7f, clipRoundaboutRadiusOffset: -2.0f, clipToInnerIslandOnly: true));
                var tarmacGo = CreateMeshObj($"Tarmac_{def.id}", root, tarmacMesh, matTarmac, addMeshCollider: true);
                tarmacGo.isStatic = true;

                // Wide Kenyan red murram road shoulder embankment — cleanly clipped outside all roundabouts so ZERO brown strips cross roundabout tarmac!
                var shoulderMesh = SaveRoadMesh($"RoadShoulder_{def.id}", BuildVariableRibbonMesh(pts, halfWidths, extraMargin: 9.5f, yOffset: -0.028f, outerEdgeDrop: -0.35f, uvWidthRepeat: 3.5f, uvMetersPerRepeat: 12f, clipRoundaboutRadiusOffset: 2.5f, clipToInnerIslandOnly: false));
                CreateMeshObj($"MurramShoulder_{def.id}", root, shoulderMesh, matMurram, addMeshCollider: true).isStatic = true;

                // Left & Right continuous crisp white tarmac edge stripes hugging variable road width!
                var edgeL = SaveRoadMesh($"EdgeL_{def.id}", BuildVariableEdgeStripMesh(pts, halfWidths, sideSign: -1f, insetFromEdge: 0.45f, halfWidth: 0.12f, yOffset: 0.016f));
                var edgeR = SaveRoadMesh($"EdgeR_{def.id}", BuildVariableEdgeStripMesh(pts, halfWidths, sideSign:  1f, insetFromEdge: 0.45f, halfWidth: 0.12f, yOffset: 0.016f));
                CreateMeshObj($"EdgeLineL_{def.id}", root, edgeL, matWhiteLine, false).isStatic = true;
                CreateMeshObj($"EdgeLineR_{def.id}", root, edgeR, matWhiteLine, false).isStatic = true;

                // Road-class specific Kenyan lane markings & medians:
                if (def.roadClass == KenyaNetworkData.RoadClass.CityExpressway6Lane)
                {
                    // 6-Lane Divided Dual Carriageway (Nairobi CBD, Thika Superhighway, Mombasa Rd):
                    // Double continuous yellow center + low raised concrete central median + 4 dashed white lane lines!
                    var median = SaveRoadMesh($"Median_{def.id}", BuildRaisedMedianMesh(pts, halfWidth: 0.36f, height: 0.28f));
                    CreateMeshObj($"CityMedian_{def.id}", root, median, matCurb, false).isStatic = true;

                    var yL = SaveRoadMesh($"CenterYL_{def.id}", BuildOffsetStripMesh(pts, halfWidths, -0.58f, 0.10f, 0.017f, dashed: false, minRequiredHalfW: 8.5f));
                    var yR = SaveRoadMesh($"CenterYR_{def.id}", BuildOffsetStripMesh(pts, halfWidths,  0.58f, 0.10f, 0.017f, dashed: false, minRequiredHalfW: 8.5f));
                    CreateMeshObj($"CenterYellowL_{def.id}", root, yL, matYellowLine, false).isStatic = true;
                    CreateMeshObj($"CenterYellowR_{def.id}", root, yR, matYellowLine, false).isStatic = true;

                    // 3 lanes per direction -> inner dashed white dividers at ±3.6m and ±7.1m!
                    float[] laneOffsets = { -7.05f, -3.55f, 3.55f, 7.05f };
                    for (int l = 0; l < laneOffsets.Length; l++)
                    {
                        float minReq = Mathf.Abs(laneOffsets[l]) + 1.8f;
                        var laneMesh = SaveRoadMesh($"CityLane_{def.id}_{l}", BuildOffsetStripMesh(pts, halfWidths, laneOffsets[l], 0.09f, 0.017f, dashed: true, minRequiredHalfW: minReq));
                        CreateMeshObj($"CityLane_{def.id}_{l}", root, laneMesh, matWhiteLine, false).isStatic = true;
                    }
                }
                else if (def.roadClass == KenyaNetworkData.RoadClass.Climbing3Lane)
                {
                    // Steep Kenyan Escarpment 3-Lane Climbing Road (Mai Mahiu, Salgaa-Total, Timboroa, Kericho, Sondu):
                    // Solid double yellow center divider + dashed white climbing-lane dividers on both slow outer lanes!
                    var yL = SaveRoadMesh($"CenterYL_{def.id}", BuildOffsetStripMesh(pts, halfWidths, -0.18f, 0.10f, 0.017f, dashed: false, minRequiredHalfW: 5f));
                    var yR = SaveRoadMesh($"CenterYR_{def.id}", BuildOffsetStripMesh(pts, halfWidths,  0.18f, 0.10f, 0.017f, dashed: false, minRequiredHalfW: 5f));
                    CreateMeshObj($"CenterDoubleL_{def.id}", root, yL, matYellowLine, false).isStatic = true;
                    CreateMeshObj($"CenterDoubleR_{def.id}", root, yR, matYellowLine, false).isStatic = true;

                    // Dedicated climbing lane dashed white stripes (and 6-lane outer stripes when entering Kisumu/Nakuru/Eldoret!)
                    var climbL = SaveRoadMesh($"ClimbLaneL_{def.id}", BuildOffsetStripMesh(pts, halfWidths, -4.45f, 0.10f, 0.017f, dashed: true, minRequiredHalfW: 8.2f));
                    var climbR = SaveRoadMesh($"ClimbLaneR_{def.id}", BuildOffsetStripMesh(pts, halfWidths,  4.45f, 0.10f, 0.017f, dashed: true, minRequiredHalfW: 8.2f));
                    CreateMeshObj($"ClimbLaneL_{def.id}", root, climbL, matWhiteLine, false).isStatic = true;
                    CreateMeshObj($"ClimbLaneR_{def.id}", root, climbR, matWhiteLine, false).isStatic = true;
                }
                else
                {
                    // Standard 2-Lane Trunk Highway (automatically adds extra dashed white city lanes inside major cities!)
                    var centerYellow = SaveRoadMesh($"CenterLine_{def.id}", BuildOffsetStripMesh(pts, halfWidths, 0f, 0.11f, 0.017f, dashed: true, minRequiredHalfW: 5f));
                    CreateMeshObj($"CenterLine_{def.id}", root, centerYellow, matYellowLine, false).isStatic = true;

                    // Extra dashed white city lanes appear seamlessly whenever this highway widens inside Kisumu/Nakuru/Eldoret/Mombasa!
                    var cityL = SaveRoadMesh($"WideLaneL_{def.id}", BuildOffsetStripMesh(pts, halfWidths, -3.75f, 0.09f, 0.017f, dashed: true, minRequiredHalfW: 9.2f));
                    var cityR = SaveRoadMesh($"WideLaneR_{def.id}", BuildOffsetStripMesh(pts, halfWidths,  3.75f, 0.09f, 0.017f, dashed: true, minRequiredHalfW: 9.2f));
                    if (cityL.vertexCount > 0) CreateMeshObj($"WideLaneL_{def.id}", root, cityL, matWhiteLine, false).isStatic = true;
                    if (cityR.vertexCount > 0) CreateMeshObj($"WideLaneR_{def.id}", root, cityR, matWhiteLine, false).isStatic = true;
                }
            }

            // 2. Build all 10 Elevation-Matched Multi-Exit Kenyan Roundabouts!
            for (int r = 0; r < KenyaNetworkData.Roundabouts.Length; r++)
            {
                var rb = KenyaNetworkData.Roundabouts[r];
                float centerY = KenyaNetworkData.GetElevationAt(rb.center);
                var rGo = new GameObject(rb.name).transform;
                rGo.SetParent(root, false);
                rGo.position = new Vector3(rb.center.x, centerY, rb.center.y);

                // Smooth circular drivable tarmac disc conforming seamlessly to exact 3D highway elevation (+2.8m outer skirt merges cleanly into every highway!)
                var ringMesh = SaveRoadMesh($"RoundaboutTarmac_{r}", BuildElevationAnnulusMesh(rb.center, centerY, rb.innerRadius - 0.5f, rb.outerRadius + 2.8f, 0.026f, 48));
                CreateMeshObj("RoundaboutTarmac", rGo, ringMesh, matTarmac, addMeshCollider: true).isStatic = true;

                // Outer shoulder apron under roundabout
                var rShoulderMesh = SaveRoadMesh($"RoundaboutShoulder_{r}", BuildElevationAnnulusMesh(rb.center, centerY, 0f, rb.outerRadius + 11.5f, -0.028f, 48));
                CreateMeshObj("RoundaboutShoulder", rGo, rShoulderMesh, matMurram, addMeshCollider: true).isStatic = true;

                // White & yellow roundabout ring lane markings
                var rMarkOuter = SaveRoadMesh($"RoundaboutOuterMark_{r}", BuildElevationAnnulusMesh(rb.center, centerY, rb.outerRadius - 0.7f, rb.outerRadius - 0.48f, 0.038f, 48));
                var rMarkMid = SaveRoadMesh($"RoundaboutMidMark_{r}", BuildElevationAnnulusMesh(rb.center, centerY, (rb.innerRadius + rb.outerRadius) * 0.5f - 0.10f, (rb.innerRadius + rb.outerRadius) * 0.5f + 0.10f, 0.038f, 48));
                CreateMeshObj("RoundaboutOuterLine", rGo, rMarkOuter, matWhiteLine, false).isStatic = true;
                CreateMeshObj("RoundaboutMidLine", rGo, rMarkMid, matYellowLine, false).isStatic = true;

                // Central raised circular Kenyan landscaped island (curb + lush grass dome)
                var islandCurb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                islandCurb.name = "IslandCurb";
                islandCurb.transform.SetParent(rGo, false);
                islandCurb.transform.localPosition = new Vector3(0f, 0.18f, 0f);
                islandCurb.transform.localScale = new Vector3(rb.innerRadius * 2f, 0.22f, rb.innerRadius * 2f);
                islandCurb.GetComponent<Renderer>().sharedMaterial = matCurb;

                var islandGrass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                islandGrass.name = "IslandLawn";
                islandGrass.transform.SetParent(rGo, false);
                islandGrass.transform.localPosition = new Vector3(0f, 0.29f, 0f);
                islandGrass.transform.localScale = new Vector3((rb.innerRadius - 0.55f) * 2f, 0.16f, (rb.innerRadius - 0.55f) * 2f);
                islandGrass.GetComponent<Renderer>().sharedMaterial = matGrass;
                Object.DestroyImmediate(islandGrass.GetComponent<Collider>());
            }

            AssetDatabase.SaveAssets();
        }

        private static bool IsInsideAnyRoundaboutClip(Vector2 xz, float radiusOffset, bool innerIslandOnly)
        {
            for (int r = 0; r < KenyaNetworkData.Roundabouts.Length; r++)
            {
                var rb = KenyaNetworkData.Roundabouts[r];
                float rad = (innerIslandOnly ? rb.innerRadius : rb.outerRadius) + radiusOffset;
                if (Vector2.Distance(xz, rb.center) < rad) return true;
            }
            return false;
        }

        private static Mesh BuildVariableRibbonMesh(List<Vector3> pts, float[] halfWidths, float extraMargin, float yOffset, float outerEdgeDrop, float uvWidthRepeat, float uvMetersPerRepeat, float clipRoundaboutRadiusOffset, bool clipToInnerIslandOnly)
        {
            int n = pts.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new List<int>((n - 1) * 6);

            float dist = 0f;
            for (int i = 0; i < n; i++)
            {
                if (i > 0) dist += Vector3.Distance(pts[i - 1], pts[i]);
                Vector3 fwd = GetHorizontalForward(pts, i);
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                float hw = halfWidths[i] + extraMargin;

                Vector3 p = pts[i];
                Vector3 vL = p - right * hw;
                Vector3 vR = p + right * hw;
                vL.y = p.y + yOffset + outerEdgeDrop;
                vR.y = p.y + yOffset + outerEdgeDrop;

                verts[i * 2] = vL;
                verts[i * 2 + 1] = vR;

                float vCoord = dist / Mathf.Max(0.5f, uvMetersPerRepeat);
                uvs[i * 2] = new Vector2(0f, vCoord);
                uvs[i * 2 + 1] = new Vector2(uvWidthRepeat, vCoord);
            }

            for (int i = 0; i < n - 1; i++)
            {
                Vector2 mid = new Vector2((pts[i].x + pts[i + 1].x) * 0.5f, (pts[i].z + pts[i + 1].z) * 0.5f);
                if (IsInsideAnyRoundaboutClip(mid, clipRoundaboutRadiusOffset, clipToInnerIslandOnly)) continue;

                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;

                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }

            return FinalizeMesh(verts, uvs, tris.ToArray());
        }

        private static Mesh BuildVariableEdgeStripMesh(List<Vector3> pts, float[] halfWidths, float sideSign, float insetFromEdge, float halfWidth, float yOffset)
        {
            int n = pts.Count;
            var verts = new Vector3[n * 2];
            var uvs = new Vector2[n * 2];
            var tris = new List<int>((n - 1) * 6);

            for (int i = 0; i < n; i++)
            {
                Vector3 fwd = GetHorizontalForward(pts, i);
                Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
                float lat = sideSign * Mathf.Max(2f, halfWidths[i] - insetFromEdge);

                Vector3 c = pts[i] + right * lat;
                c.y = pts[i].y + yOffset;
                verts[i * 2] = c - right * halfWidth;
                verts[i * 2 + 1] = c + right * halfWidth;
                uvs[i * 2] = new Vector2(0f, i);
                uvs[i * 2 + 1] = new Vector2(1f, i);
            }

            for (int i = 0; i < n - 1; i++)
            {
                Vector2 mid = new Vector2((pts[i].x + pts[i + 1].x) * 0.5f, (pts[i].z + pts[i + 1].z) * 0.5f);
                if (IsInsideAnyRoundaboutClip(mid, 1.5f, false)) continue;

                int i0 = i * 2;
                int i1 = i0 + 1;
                int i2 = i0 + 2;
                int i3 = i0 + 3;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }
            return FinalizeMesh(verts, uvs, tris.ToArray());
        }

        private static Mesh BuildOffsetStripMesh(List<Vector3> pts, float[] halfWidths, float lateralOffset, float halfWidth, float yOffset, bool dashed, float minRequiredHalfW)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            float dist = 0f;
            for (int i = 0; i < pts.Count - 1; i++)
            {
                float segLen = Vector3.Distance(pts[i], pts[i + 1]);
                Vector2 mid = new Vector2((pts[i].x + pts[i + 1].x) * 0.5f, (pts[i].z + pts[i + 1].z) * 0.5f);
                bool wideEnough = halfWidths[i] >= minRequiredHalfW && halfWidths[i + 1] >= minRequiredHalfW;
                bool draw = wideEnough && !IsInsideAnyRoundaboutClip(mid, 1.5f, false) && (!dashed || (((dist / 9.5f) % 1f) < 0.46f));
                dist += segLen;
                if (!draw) continue;

                Vector3 fwdA = GetHorizontalForward(pts, i);
                Vector3 rightA = Vector3.Cross(Vector3.up, fwdA).normalized;
                Vector3 fwdB = GetHorizontalForward(pts, i + 1);
                Vector3 rightB = Vector3.Cross(Vector3.up, fwdB).normalized;

                Vector3 cA = pts[i] + rightA * lateralOffset; cA.y = pts[i].y + yOffset;
                Vector3 cB = pts[i + 1] + rightB * lateralOffset; cB.y = pts[i + 1].y + yOffset;

                int idx = verts.Count;
                verts.Add(cA - rightA * halfWidth);
                verts.Add(cA + rightA * halfWidth);
                verts.Add(cB - rightB * halfWidth);
                verts.Add(cB + rightB * halfWidth);

                uvs.Add(new Vector2(0, 0));
                uvs.Add(new Vector2(1, 0));
                uvs.Add(new Vector2(0, 1));
                uvs.Add(new Vector2(1, 1));

                tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 1);
                tris.Add(idx + 1); tris.Add(idx + 2); tris.Add(idx + 3);
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            if (verts.Count > 0)
            {
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRaisedMedianMesh(List<Vector3> pts, float halfWidth, float height)
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            // Leave clean openings around roundabouts and Nairobi CBD crossing so vehicles never hit a median at junctions
            for (int i = 1; i < pts.Count - 2; i++)
            {
                Vector2 pMid = new Vector2((pts[i].x + pts[i + 1].x) * 0.5f, (pts[i].z + pts[i + 1].z) * 0.5f);
                bool nearRbt = false;
                for (int r = 0; r < KenyaNetworkData.Roundabouts.Length; r++)
                {
                    if (Vector2.Distance(pMid, KenyaNetworkData.Roundabouts[r].center) < KenyaNetworkData.Roundabouts[r].outerRadius + 16f)
                    {
                        nearRbt = true;
                        break;
                    }
                }
                if (nearRbt || (Mathf.Abs(pMid.x) < 12f && pMid.y > -10f && pMid.y < 80f)) continue;

                Vector3 rA = Vector3.Cross(Vector3.up, GetHorizontalForward(pts, i)).normalized;
                Vector3 rB = Vector3.Cross(Vector3.up, GetHorizontalForward(pts, i + 1)).normalized;

                Vector3 aBL = pts[i] - rA * halfWidth;
                Vector3 aBR = pts[i] + rA * halfWidth;
                Vector3 aTL = aBL + Vector3.up * height;
                Vector3 aTR = aBR + Vector3.up * height;

                Vector3 bBL = pts[i + 1] - rB * halfWidth;
                Vector3 bBR = pts[i + 1] + rB * halfWidth;
                Vector3 bTL = bBL + Vector3.up * height;
                Vector3 bTR = bBR + Vector3.up * height;

                AddQuad(verts, uvs, tris, aTL, bTL, bTR, aTR); // Top
                AddQuad(verts, uvs, tris, aBL, bBL, bTL, aTL); // Left face
                AddQuad(verts, uvs, tris, aTR, bTR, bBR, aBR); // Right face
            }

            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            if (verts.Count > 0)
            {
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();
            }
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddQuad(List<Vector3> verts, List<Vector2> uvs, List<int> tris, Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
        {
            int idx = verts.Count;
            verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, 0));
            tris.Add(idx); tris.Add(idx + 1); tris.Add(idx + 2);
            tris.Add(idx); tris.Add(idx + 2); tris.Add(idx + 3);
        }

        private static Mesh BuildElevationAnnulusMesh(Vector2 worldCenter, float centerY, float innerR, float outerR, float yOffset, int segments)
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

                float ix = c * innerR, iz = s * innerR;
                float ox = c * outerR, oz = s * outerR;
                float iy = KenyaNetworkData.GetElevationAt(worldCenter.x + ix, worldCenter.y + iz) - centerY + yOffset;
                float oy = KenyaNetworkData.GetElevationAt(worldCenter.x + ox, worldCenter.y + oz) - centerY + yOffset;

                verts[i * 2] = new Vector3(ix, iy, iz);
                verts[i * 2 + 1] = new Vector3(ox, oy, oz);
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
                tris[ti++] = i0; tris[ti++] = i2; tris[ti++] = i1;
                tris[ti++] = i1; tris[ti++] = i2; tris[ti++] = i3;
            }

            return FinalizeMesh(verts, uvs, tris);
        }

        private static Mesh FinalizeMesh(Vector3[] verts, Vector2[] uvs, int[] tris)
        {
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Vector3 GetHorizontalForward(List<Vector3> pts, int i)
        {
            Vector3 d = i <= 0 ? (pts[1] - pts[0])
                      : i >= pts.Count - 1 ? (pts[pts.Count - 1] - pts[pts.Count - 2])
                      : (pts[i + 1] - pts[i - 1]);
            d.y = 0f;
            return d.sqrMagnitude > 0.0001f ? d.normalized : Vector3.forward;
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
