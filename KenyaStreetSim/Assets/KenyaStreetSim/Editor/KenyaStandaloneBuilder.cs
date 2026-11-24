using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Compiles and packages the complete standalone Windows 64-bit ETS2-Class PC Game executable
    /// into D:\KenyaStreetSim_CompleteGame\KenyaStreetSim.exe (zero Unity Editor needed to play!).
    /// </summary>
    public static class KenyaStandaloneBuilder
    {
        private const string ScenePath = "Assets/KenyaStreetSim/Scenes/KenyaTownStreets.unity";
        private const string BuildDir = "D:/KenyaStreetSim_CompleteGame";
        private const string ExePath = BuildDir + "/KenyaStreetSim.exe";

        [MenuItem("Kenya Street Sim/Build Complete Windows PC Game (.exe)")]
        public static void BuildCompleteWindowsGame()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"[KenyaStandaloneBuilder] Missing scene at {ScenePath} — build the Kenya map first!");
                return;
            }

            Directory.CreateDirectory(BuildDir);

            PlayerSettings.companyName = "Savanna Line Studios Kenya";
            PlayerSettings.productName = "Kenya Street Sim - ETS2 Class";
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = ExePath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            Debug.Log($"[KenyaStandaloneBuilder] Starting Windows 64-bit complete game build -> {ExePath}");
            BuildReport report = BuildPipeline.BuildPlayer(opts);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                double mb = summary.totalSize / (1024.0 * 1024.0);
                Debug.Log($"[KenyaStandaloneBuilder] SUCCEEDED! Complete PC Game ready at {ExePath} ({mb:0.1} MB, time={summary.totalTime.TotalSeconds:0.1}s, errors={summary.totalErrors})");
            }
            else
            {
                Debug.LogError($"[KenyaStandaloneBuilder] FAILED ({summary.result}) with {summary.totalErrors} errors!");
            }
        }
    }
}
