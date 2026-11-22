using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace KenyaStreetSim.Editor
{
    /// <summary>
    /// Automatically configures the Unity Editor Game View to crisp native Full HD (1920x1080),
    /// disables Low Resolution Aspect Ratios (which otherwise halves pixel clarity on laptop screens),
    /// and enables Maximize On Play so clicking Play delivers a sharp, full-screen Kenyan highway view.
    /// </summary>
    [InitializeOnLoad]
    public static class KenyaHdGameViewConfigurator
    {
        static KenyaHdGameViewConfigurator()
        {
            EditorApplication.delayCall += () => ApplyFullHdCrispGameView(maximizeOnPlay: false, silent: true);
        }

        [MenuItem("Kenya Street Sim/Enable Full HD 1920x1080 Crisp Game View")]
        public static void ApplyFullHdMenu()
        {
            ApplyFullHdCrispGameView(maximizeOnPlay: true, silent: false);
        }

        public static string ApplyFullHdCrispGameView(bool maximizeOnPlay = true, bool silent = false)
        {
            try
            {
                var asm = typeof(UnityEditor.Editor).Assembly;
                var gvType = asm.GetType("UnityEditor.GameView");
                var sizesType = asm.GetType("UnityEditor.GameViewSizes");
                var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
                var sizesInstance = singleType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                var currentGroupProp = sizesType.GetProperty("currentGroup", BindingFlags.Public | BindingFlags.Instance);
                var currentGroup = currentGroupProp?.GetValue(sizesInstance);

                int targetIdx = -1;
                if (currentGroup != null)
                {
                    var getTotal = currentGroup.GetType().GetMethod("GetTotalCount", BindingFlags.Public | BindingFlags.Instance);
                    var getSize = currentGroup.GetType().GetMethod("GetGameViewSize", BindingFlags.Public | BindingFlags.Instance);
                    int total = (int)getTotal.Invoke(currentGroup, null);
                    for (int i = 0; i < total; i++)
                    {
                        var sz = getSize.Invoke(currentGroup, new object[] { i });
                        int w = (int)sz.GetType().GetProperty("width").GetValue(sz);
                        int h = (int)sz.GetType().GetProperty("height").GetValue(sz);
                        string disp = sz.GetType().GetProperty("displayText")?.GetValue(sz) as string ?? "";
                        if (w == 1920 && h == 1080)
                        {
                            targetIdx = i;
                            break;
                        }
                        if (disp.Contains("1920x1080") || disp.Contains("Full HD"))
                        {
                            targetIdx = i;
                        }
                    }

                    if (targetIdx < 0)
                    {
                        var gvSizeType = asm.GetType("UnityEditor.GameViewSize");
                        var gvSizeTypeEnum = asm.GetType("UnityEditor.GameViewSizeType");
                        var fixedResEnum = Enum.Parse(gvSizeTypeEnum, "FixedResolution");
                        var ctor = gvSizeType.GetConstructor(new[] { gvSizeTypeEnum, typeof(int), typeof(int), typeof(string) });
                        var newSize = ctor.Invoke(new[] { fixedResEnum, 1920, 1080, "Full HD 1920x1080" });
                        var addCustom = currentGroup.GetType().GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);
                        addCustom.Invoke(currentGroup, new[] { newSize });
                        targetIdx = (int)getTotal.Invoke(currentGroup, null) - 1;
                    }
                }

                var gv = EditorWindow.GetWindow(gvType, false, null, false);
                if (gv != null)
                {
                    if (targetIdx >= 0)
                    {
                        var selProp = gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        selProp?.SetValue(gv, targetIdx);
                        var sizeCallback = gvType.GetMethod("SizeSelectionCallback", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        sizeCallback?.Invoke(gv, new object[] { targetIdx, null });
                    }

                    var lowResProp = gvType.GetProperty("lowResolutionForAspectRatios", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    lowResProp?.SetValue(gv, false);

                    var maxProp = gvType.GetProperty("maximizeOnPlay", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (maxProp != null && maximizeOnPlay)
                    {
                        maxProp.SetValue(gv, true);
                    }

                    gv.Repaint();
                }

                string msg = $"GameView HD Configured: 1920x1080 (idx={targetIdx}) | lowRes=false | maximizeOnPlay={maximizeOnPlay}";
                if (!silent) Debug.Log(msg);
                return msg;
            }
            catch (Exception ex)
            {
                return $"GameView config skipped: {ex.Message}";
            }
        }
    }
}
