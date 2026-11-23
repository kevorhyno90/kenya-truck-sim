using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Unity 6 UI Toolkit Editor Window ("Kenya Street Sim -> Commercial Ownership & Traffic Manager"):
    /// - Shows 100% transparent Commercial License Audit (Owned/CC0 vs Prototype models to replace before paid sale)
    /// - Lets you open the auto-discovered RealTraffic/ drop folder for Unity Asset Store / Blender / CC0 cars
    /// - Toggle Commercial-Safe Original Kenyan Brands (zero trademark risk for Steam / Play Store / itch.io!)
    /// - One-click Rebuild 16-Town Kenya Map & Build Standalone Windows .exe!
    /// </summary>
    public class KenyaCommercialManagerWindow : EditorWindow
    {
        public const string CommercialBrandsPrefKey = "KenyaStreetSim_UseCommercialSafeBrands";
        public const string RealTrafficFolder = "Assets/KenyaStreetSim/ImportedModels/RealTraffic";

        public static bool UseCommercialSafeBrands
        {
            get => EditorPrefs.GetBool(CommercialBrandsPrefKey, false);
            set => EditorPrefs.SetBool(CommercialBrandsPrefKey, value);
        }

        [MenuItem("Kenya Street Sim/Commercial Ownership & Traffic Manager")]
        public static void OpenWindow()
        {
            var win = GetWindow<KenyaCommercialManagerWindow>("Kenya Commercial & Traffic Manager");
            win.minSize = new Vector2(560f, 520f);
            win.Show();
        }

        private Label trafficCountLabel;
        private Label statusLabel;

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 14;
            root.style.paddingBottom = 14;
            root.style.paddingLeft = 16;
            root.style.paddingRight = 16;
            root.style.backgroundColor = new Color(0.10f, 0.13f, 0.18f);

            var title = new Label("KENYA STREET SIM — COMMERCIAL OWNERSHIP & TRAFFIC MANAGER");
            title.style.fontSize = 18;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new Color(0.98f, 0.76f, 0.16f);
            title.style.marginBottom = 6;
            root.Add(title);

            var sub = new Label("Path A: Your 100% Owned Standalone Kenyan Driving Game (Steam / PC / Play Store / itch.io Ready)");
            sub.style.color = new Color(0.76f, 0.84f, 0.92f);
            sub.style.marginBottom = 14;
            root.Add(sub);

            // Section 1: Auto-Drop RealTraffic Folder
            var trafficCard = CreateCard(root, "1. REAL 3D PASSING TRAFFIC (AUTO-DISCOVERY FOLDER)");
            trafficCountLabel = new Label();
            trafficCountLabel.style.color = Color.white;
            trafficCountLabel.style.marginBottom = 8;
            trafficCard.Add(trafficCountLabel);

            var dropHint = new Label("Drop any Commercial / Unity Asset Store / Blender / CC0 vehicles (.glb, .fbx, .obj, .prefab) into:\n" +
                                     RealTrafficFolder + "\nThey automatically join passing Kenyan town & highway traffic on rebuild!");
            dropHint.style.color = new Color(0.72f, 0.80f, 0.88f);
            dropHint.style.whiteSpace = WhiteSpace.Normal;
            dropHint.style.marginBottom = 10;
            trafficCard.Add(dropHint);

            var btnRow1 = new VisualElement();
            btnRow1.style.flexDirection = FlexDirection.Row;
            btnRow1.style.flexWrap = Wrap.Wrap;
            var openFolderBtn = CreateActionBtn("Open RealTraffic Folder", new Color(0.10f, 0.56f, 0.72f), () =>
            {
                if (!Directory.Exists(RealTrafficFolder)) Directory.CreateDirectory(RealTrafficFolder);
                EditorUtility.RevealInFinder(Path.GetFullPath(RealTrafficFolder));
            });
            var openBlendBtn = CreateActionBtn("Open Blender Showroom (.blend)", new Color(0.86f, 0.45f, 0.08f), () =>
            {
                string blendPath = Path.GetFullPath("BlenderStudio/Kenya_Owned_Commercial_Traffic_Pack.blend");
                if (File.Exists(blendPath))
                {
                    EditorUtility.OpenWithDefaultApp(blendPath);
                    statusLabel.text = $"Opened in Blender: {blendPath}";
                }
                else
                {
                    statusLabel.text = $"Blender showroom not found yet at: {blendPath}";
                }
            });
            var revealBlendBtn = CreateActionBtn("Reveal Blender Folder", new Color(0.32f, 0.36f, 0.46f), () =>
            {
                string blendPath = Path.GetFullPath("BlenderStudio/Kenya_Owned_Commercial_Traffic_Pack.blend");
                string target = File.Exists(blendPath) ? blendPath : Path.GetFullPath("BlenderStudio");
                if (File.Exists(target) || Directory.Exists(target)) EditorUtility.RevealInFinder(target);
            });
            var refreshBtn = CreateActionBtn("Refresh Count", new Color(0.24f, 0.30f, 0.40f), RefreshCounts);
            btnRow1.Add(openFolderBtn);
            btnRow1.Add(openBlendBtn);
            btnRow1.Add(revealBlendBtn);
            btnRow1.Add(refreshBtn);
            trafficCard.Add(btnRow1);

            // Section 2: Commercial-Safe Brands Toggle
            var brandCard = CreateCard(root, "2. TRADEMARK-SAFE ORIGINAL KENYAN BRANDS");
            var brandToggle = new Toggle("Use 100% Trademark-Safe Original Brands (Savanna Coach, SafariTel, M-Pay, Rubix Energy, Nava Mart)");
            brandToggle.value = UseCommercialSafeBrands;
            brandToggle.style.color = Color.white;
            brandToggle.RegisterValueChangedCallback(evt =>
            {
                UseCommercialSafeBrands = evt.newValue;
                statusLabel.text = evt.newValue
                    ? "Trademark-Safe Commercial Brands ENABLED — click Rebuild 16-Town Map to apply!"
                    : "Prototype Real-World Kenyan Sign Names active — toggle ON before selling publicly.";
            });
            brandCard.Add(brandToggle);

            // Section 3: Commercial Ownership Audit
            var auditCard = CreateCard(root, "3. ASSET OWNERSHIP AUDIT FOR COMMERCIAL SALE");
            var ownedLbl = new Label(
                "✓ 100% OWNED / COMMERCIAL-SAFE RIGHT NOW:\n" +
                "  • All C# Game Scripts, 16-Town Highway World, Routes, Police Roadblocks & Weather\n" +
                "  • Sculpted Original Kenya Police LC79 + 6 Sculpted Owned Traffic Vehicles (Saloon, SUV, Pickup, Matatu, Coach, Canter)\n" +
                "  • Poly Haven CC0 2K PBR Textures & 3 African Highland Skyboxes\n" +
                "  • Wikimedia Public Domain / CC0 / CC BY 4.0 Audio (credited in AUDIO_CREDITS.txt)\n\n" +
                "! PROTOTYPE IMPORTS (Replace with Asset Store / Blender / CC0 before Paid Store Launch):\n" +
                "  • ImportedModels/ScaniaTouring2026/, Isuzu_FRR, Isuzu_TX, Marcopolo_G7, Yutong_ETS2, RangeRover_Sport");
            ownedLbl.style.color = new Color(0.84f, 0.90f, 0.95f);
            ownedLbl.style.whiteSpace = WhiteSpace.Normal;
            auditCard.Add(ownedLbl);

            // Section 4: One-Click Rebuild & Export Windows .exe
            var actionRow = new VisualElement();
            actionRow.style.flexDirection = FlexDirection.Row;
            actionRow.style.marginTop = 10;

            var rebuildBtn = CreateActionBtn("Rebuild 16-Town Kenya Map", new Color(0.14f, 0.64f, 0.34f), () =>
            {
                if (EditorApplication.isPlaying)
                {
                    statusLabel.text = "Stop Play Mode first before rebuilding the scene!";
                    return;
                }
                string res = KenyaStreetTownBuilder.BuildKenyaTownScene();
                statusLabel.text = res;
                RefreshCounts();
            });
            var buildExeBtn = CreateActionBtn("Build Standalone Windows .exe", new Color(0.82f, 0.24f, 0.18f), BuildWindowsExe);

            actionRow.Add(rebuildBtn);
            actionRow.Add(buildExeBtn);
            root.Add(actionRow);

            statusLabel = new Label("Ready. See COMMERCIAL_RELEASE_CHECKLIST.md for full Steam/itch.io/Play Store release steps.");
            statusLabel.style.color = new Color(0.98f, 0.78f, 0.22f);
            statusLabel.style.whiteSpace = WhiteSpace.Normal;
            statusLabel.style.marginTop = 10;
            root.Add(statusLabel);

            RefreshCounts();
        }

        private void RefreshCounts()
        {
            int customReal = 0;
            if (Directory.Exists(RealTrafficFolder))
            {
                foreach (var f in Directory.GetFiles(RealTrafficFolder))
                {
                    string ext = Path.GetExtension(f).ToLowerInvariant();
                    if (ext == ".glb" || ext == ".gltf" || ext == ".fbx" || ext == ".obj" || ext == ".prefab")
                        customReal++;
                }
            }
            if (trafficCountLabel != null)
            {
                trafficCountLabel.text = $"Built-in 100%-Owned Sculpted Kenyan Vehicles: 6 types (48 paint/livery variants)  •  Drop-in RealTraffic Models: {customReal}";
            }
        }

        private void BuildWindowsExe()
        {
            if (EditorApplication.isPlaying)
            {
                statusLabel.text = "Stop Play Mode first before building Windows .exe!";
                return;
            }
            string outDir = Path.GetFullPath("Builds/KenyaStreetSim");
            Directory.CreateDirectory(outDir);
            string exePath = Path.Combine(outDir, "KenyaStreetSim.exe");
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/KenyaStreetSim/Scenes/KenyaTownStreets.unity" },
                locationPathName = exePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };
            var report = BuildPipeline.BuildPlayer(opts);
            statusLabel.text = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded
                ? $"SUCCESS! Built Windows game at: {exePath}"
                : $"Build ended with: {report.summary.result} ({report.summary.totalErrors} errors)";
        }

        private static VisualElement CreateCard(VisualElement parent, string headerText)
        {
            var box = new VisualElement();
            box.style.backgroundColor = new Color(0.14f, 0.18f, 0.25f);
            box.style.borderTopWidth = 1; box.style.borderBottomWidth = 1; box.style.borderLeftWidth = 1; box.style.borderRightWidth = 1;
            Color bCol = new Color(0.28f, 0.36f, 0.48f);
            box.style.borderTopColor = bCol; box.style.borderBottomColor = bCol; box.style.borderLeftColor = bCol; box.style.borderRightColor = bCol;
            box.style.paddingTop = 10; box.style.paddingBottom = 10; box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.marginBottom = 10;

            var hdr = new Label(headerText);
            hdr.style.unityFontStyleAndWeight = FontStyle.Bold;
            hdr.style.color = new Color(0.14f, 0.88f, 1.0f);
            hdr.style.marginBottom = 6;
            box.Add(hdr);

            parent.Add(box);
            return box;
        }

        private static Button CreateActionBtn(string text, Color bg, System.Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.backgroundColor = bg;
            btn.style.color = Color.white;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.height = 32;
            btn.style.marginRight = 8;
            btn.style.paddingLeft = 14;
            btn.style.paddingRight = 14;
            return btn;
        }
    }
}
