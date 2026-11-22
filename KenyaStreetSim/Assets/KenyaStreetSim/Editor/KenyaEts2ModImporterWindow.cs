using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Modern Unity 6 UI Toolkit Editor Window:
    /// Imports unlocked Euro Truck Simulator 2 (.scs ZIP or unpacked mod folder) vehicle models
    /// (.pmg / .pmd / .mat / .tobj / .dds) directly into KenyaStreetSim as playable glTF (.glb) vehicles!
    /// </summary>
    public class KenyaEts2ModImporterWindow : EditorWindow
    {
        private TextField sourcePathField;
        private TextField outputNameField;
        private TextField pmgOverrideField;
        private IntegerField lookIndexField;
        private PopupField<int> maxTexDropdown;
        private Toggle flipVToggle;
        private Label statusLabel;

        [MenuItem("Kenya Street Sim/Import ETS2 Mod (.scs)...")]
        public static void OpenWindow()
        {
            var win = GetWindow<KenyaEts2ModImporterWindow>();
            win.titleContent = new GUIContent("ETS2 Mod Importer");
            win.minSize = new Vector2(520f, 380f);
            win.Show();
        }

        public void CreateGUI()
        {
            var root = rootVisualElement;
            root.style.paddingTop = 12;
            root.style.paddingBottom = 12;
            root.style.paddingLeft = 14;
            root.style.paddingRight = 14;

            var title = new Label("Kenya Street Sim — ETS2 (.scs) Mod Importer");
            title.style.fontSize = 17;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 4;
            root.Add(title);

            var sub = new Label("Converts unlocked ETS2 .scs (ZIP) or unpacked mod folders (.pmg/.pmd/.dds) directly into playable Unity .glb vehicles with automatic wheel locator detection.");
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.marginBottom = 12;
            sub.style.opacity = 0.85f;
            root.Add(sub);

            // Source row
            var srcRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 6 } };
            sourcePathField = new TextField("ETS2 .scs / Folder") { style = { flexGrow = 1 } };
            srcRow.Add(sourcePathField);

            var browseFileBtn = new Button(() =>
            {
                string picked = EditorUtility.OpenFilePanel("Select Unlocked ETS2 .scs Mod", "", "scs,zip");
                if (!string.IsNullOrEmpty(picked))
                {
                    sourcePathField.value = picked;
                    outputNameField.value = SanitizeModName(Path.GetFileNameWithoutExtension(picked));
                }
            }) { text = ".scs File", style = { width = 74 } };
            srcRow.Add(browseFileBtn);

            var browseDirBtn = new Button(() =>
            {
                string picked = EditorUtility.OpenFolderPanel("Select Unpacked ETS2 Mod Folder", "", "");
                if (!string.IsNullOrEmpty(picked))
                {
                    sourcePathField.value = picked;
                    outputNameField.value = SanitizeModName(Path.GetFileName(picked));
                }
            }) { text = "Folder", style = { width = 62 } };
            srcRow.Add(browseDirBtn);
            root.Add(srcRow);

            outputNameField = new TextField("Output Vehicle Name") { value = "Custom_ETS2_Vehicle" };
            outputNameField.style.marginBottom = 6;
            root.Add(outputNameField);

            pmgOverrideField = new TextField("PMG Path (blank = auto)") { value = "" };
            pmgOverrideField.style.marginBottom = 6;
            root.Add(pmgOverrideField);

            var optRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 10 } };
            lookIndexField = new IntegerField("Livery Look #") { value = 0, style = { width = 160, marginRight = 12 } };
            maxTexDropdown = new PopupField<int>("Max Texture", new System.Collections.Generic.List<int> { 512, 1024, 2048 }, 1024) { style = { width = 200, marginRight = 12 } };
            flipVToggle = new Toggle("Flip UV V") { value = false };
            optRow.Add(lookIndexField);
            optRow.Add(maxTexDropdown);
            optRow.Add(flipVToggle);
            root.Add(optRow);

            // Action buttons
            var btnRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginBottom = 10 } };
            var scanBtn = new Button(RunScanMod) { text = "1. Scan .pmg Models", style = { flexGrow = 1, height = 30 } };
            var convertBtn = new Button(RunConvertMod) { text = "2. Convert & Import into Fleet", style = { flexGrow = 1, height = 30 } };
            var rebuildBtn = new Button(() =>
            {
                string res = KenyaStreetTownBuilder.BuildKenyaTownScene();
                SetStatus($"Scene Rebuilt:\n{res}");
            }) { text = "3. Rebuild Kenya Scene", style = { flexGrow = 1, height = 30 } };
            btnRow.Add(scanBtn);
            btnRow.Add(convertBtn);
            btnRow.Add(rebuildBtn);
            root.Add(btnRow);

            var scroll = new ScrollView { style = { flexGrow = 1, backgroundColor = new Color(0.11f, 0.13f, 0.16f), paddingTop = 8, paddingBottom = 8, paddingLeft = 8, paddingRight = 8 } };
            statusLabel = new Label("Ready. Pick an unlocked ETS2 .scs vehicle mod (e.g., Yutong, Scania, Isuzu) to convert into KenyaStreetSim.")
            {
                style = { whiteSpace = WhiteSpace.Normal, color = new Color(0.88f, 0.92f, 0.96f) }
            };
            scroll.Add(statusLabel);
            root.Add(scroll);
        }

        private string GetConverterScriptPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? "";
            string repoRoot = Directory.GetParent(projectRoot)?.FullName ?? projectRoot;
            string p1 = Path.Combine(repoRoot, "Tools", "ets2_scs_to_glb.py");
            if (File.Exists(p1)) return p1;
            return Path.Combine(projectRoot, "Tools", "ets2_scs_to_glb.py");
        }

        private void RunScanMod()
        {
            string src = sourcePathField.value?.Trim();
            if (string.IsNullOrEmpty(src)) { SetStatus("Please select an ETS2 .scs file or unpacked mod folder first."); return; }
            string script = GetConverterScriptPath();
            if (!File.Exists(script)) { SetStatus($"Converter script missing at:\n{script}"); return; }

            var (code, output) = RunPython($"\"{script}\" \"{src}\" --list");
            SetStatus(code == 0 ? $"Found .pmg models:\n{output}" : $"Scan Error:\n{output}");
        }

        private void RunConvertMod()
        {
            string src = sourcePathField.value?.Trim();
            if (string.IsNullOrEmpty(src)) { SetStatus("Please select an ETS2 .scs file or unpacked mod folder first."); return; }
            string script = GetConverterScriptPath();
            if (!File.Exists(script)) { SetStatus($"Converter script missing at:\n{script}"); return; }

            string safeName = SanitizeModName(outputNameField.value);
            if (!safeName.EndsWith("_ETS2", StringComparison.OrdinalIgnoreCase)) safeName += "_ETS2";
            string outAssetRel = $"Assets/KenyaStreetSim/ImportedModels/Fleet/{safeName}.glb";
            string outFull = Path.GetFullPath(outAssetRel);

            string pmgArg = string.IsNullOrWhiteSpace(pmgOverrideField.value) ? "" : $" --pmg \"{pmgOverrideField.value.Trim()}\"";
            string flipArg = flipVToggle.value ? " --flip-v" : "";
            string args = $"\"{script}\" \"{src}\" --out \"{outFull}\" --look {Mathf.Max(0, lookIndexField.value)} --max-tex {maxTexDropdown.value}{pmgArg}{flipArg}";

            SetStatus("Converting ETS2 .scs mod to glTF 2.0 (.glb)...");
            var (code, output) = RunPython(args);
            if (code == 0)
            {
                AssetDatabase.Refresh();
                SetStatus($"SUCCESS! Imported into {outAssetRel}\nClick '3. Rebuild Kenya Scene' to drive it in-game!\n\n{output}");
            }
            else
            {
                SetStatus($"Conversion Failed:\n{output}");
            }
        }

        private static (int, string) RunPython(string arguments)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "python",
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                string stdout = proc.StandardOutput.ReadToEnd();
                string stderr = proc.StandardError.ReadToEnd();
                proc.WaitForExit();
                string combined = string.IsNullOrWhiteSpace(stderr) ? stdout : $"{stdout}\n{stderr}";
                return (proc.ExitCode, combined.Trim());
            }
            catch (Exception ex)
            {
                return (-1, $"Could not launch python: {ex.Message}");
            }
        }

        private static string SanitizeModName(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "Custom_ETS2_Vehicle";
            var chars = raw.Trim().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                if (!char.IsLetterOrDigit(chars[i]) && chars[i] != '_') chars[i] = '_';
            }
            string cleaned = new string(chars).Trim('_');
            return string.IsNullOrEmpty(cleaned) ? "Custom_ETS2_Vehicle" : cleaned;
        }

        private void SetStatus(string msg)
        {
            if (statusLabel != null) statusLabel.text = msg;
        }
    }
}
