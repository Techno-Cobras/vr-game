using System;
using System.IO;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Bootstrap;

namespace VrGame.Editor
{
    public static class BuildAutomation
    {
        private const string ScenePath = "Assets/_Project/Scenes/Startup.unity";

        public static void Smoke()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Require(UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length == 1,
                "Сцена должна содержать ровно один XR Origin.");
            Require(UnityEngine.Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None).Length == 1,
                "Сцена должна содержать ровно один XR Interaction Manager.");
            Require(UnityEngine.Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None).Length >= 1,
                "Сцена должна содержать хотя бы один grab-объект.");
            Require(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Any(button =>
                    button.GetComponentInParent<Canvas>()?.renderMode == RenderMode.WorldSpace),
                "Сцена должна содержать интерактивную world-space UI кнопку.");

            var bootstrap = UnityEngine.Object.FindFirstObjectByType<VrBootstrap>();
            Require(bootstrap != null && bootstrap.InputActions != null,
                "Bootstrap должен ссылаться на основной Input Action Asset.");
            Require(bootstrap.InputActions.actionMaps.Count > 0 && bootstrap.InputActions.bindings.Any(),
                "Input Action Asset должен содержать maps и bindings.");
            Require(EditorBuildSettings.scenes.Length == 1 && EditorBuildSettings.scenes[0].path == ScenePath,
                "В Build Settings должна быть только стартовая сцена.");

            Debug.Log("VR_GAME_SMOKE_OK");
        }

        public static void BuildAndroid()
        {
            Smoke();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Не удалось переключить build target на Android.");

            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = "Builds/Android/vr-game.apk",
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development,
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException($"Android build завершился со статусом {report.summary.result}.");

            Debug.Log($"VR_GAME_ANDROID_BUILD_OK size={report.summary.totalSize}");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
