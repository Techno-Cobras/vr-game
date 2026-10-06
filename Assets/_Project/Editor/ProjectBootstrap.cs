using System;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
using VrGame.Bootstrap;

namespace VrGame.Editor
{
    public static class ProjectBootstrap
    {
        private const string StarterAssetsRoot = "Assets/Samples/XR Interaction Toolkit/3.6.1/Starter Assets";
        private const string InputActionsPath = "Assets/_Project/Input/VRControls.inputactions";
        private const string ScenePath = "Assets/_Project/Scenes/Startup.unity";
        private const string PipelineAssetPath = "Assets/_Project/Settings/QuestURP.asset";
        private const string RendererAssetPath = "Assets/_Project/Settings/QuestRenderer.asset";

        [MenuItem("VR Game/Настроить проект")]
        public static void Configure()
        {
            EnsureFolder("Assets/_Project/Settings");
            ImportStarterAssets();
            ConfigureSerializationAndInput();
            ConfigurePlayer();
            ConfigureRenderPipeline();
            ConfigureOpenXr();
            CreateStartupScene();
            RemoveUnusedDemoAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("VR_GAME_BOOTSTRAP_OK");
        }

        private static void ImportStarterAssets()
        {
            if (!AssetDatabase.IsValidFolder(StarterAssetsRoot))
            {
                var sample = Sample.FindByPackage("com.unity.xr.interaction.toolkit", "3.6.1")
                    .FirstOrDefault(candidate => candidate.displayName == "Starter Assets");
                if (string.IsNullOrWhiteSpace(sample.displayName) || !sample.Import(Sample.ImportOptions.OverridePreviousImports))
                    throw new InvalidOperationException("Не удалось импортировать XRI Starter Assets.");
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var sampleInputPath = StarterAssetsRoot + "/XRI Default Input Actions.inputactions";
            if (AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath) == null)
            {
                var moveError = AssetDatabase.MoveAsset(sampleInputPath, InputActionsPath);
                if (!string.IsNullOrEmpty(moveError))
                    throw new InvalidOperationException("Не удалось переместить Input Action Asset: " + moveError);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }

        private static void ConfigureSerializationAndInput()
        {
            EditorSettings.serializationMode = SerializationMode.ForceText;
            VersionControlSettings.mode = "Visible Meta Files";
            Time.fixedDeltaTime = 1f / 72f;
            Time.maximumDeltaTime = 0.05f;

            var timeSettings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TimeManager.asset")[0];
            EditorUtility.SetDirty(timeSettings);

            var playerSettings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            var activeInputHandler = playerSettings.FindProperty("activeInputHandler");
            if (activeInputHandler != null)
            {
                activeInputHandler.intValue = 1;
                playerSettings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ConfigurePlayer()
        {
            PlayerSettings.companyName = "Techno Cobras";
            PlayerSettings.productName = "VR Game";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.technocobras.vrgame");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
        }

        private static void ConfigureRenderPipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelineAssetPath);
            if (pipeline == null)
            {
                var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererAssetPath);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelineAssetPath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
        }

        private static void ConfigureOpenXr()
        {
            const string generalSettingsPath = "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset";
            var settingsPerBuildTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(generalSettingsPath);
            if (settingsPerBuildTarget == null)
            {
                settingsPerBuildTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                AssetDatabase.CreateAsset(settingsPerBuildTarget, generalSettingsPath);
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.settingsKey, settingsPerBuildTarget, true);
            }

            if (!settingsPerBuildTarget.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android))
                settingsPerBuildTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);

            var generalSettings = settingsPerBuildTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            if (generalSettings?.Manager == null)
                throw new InvalidOperationException("Не удалось создать XR Plug-in Management settings для Android.");

            generalSettings.InitManagerOnStart = true;
            if (!XRPackageMetadataStore.AssignLoader(
                    generalSettings.Manager,
                    "UnityEngine.XR.OpenXR.OpenXRLoader",
                    BuildTargetGroup.Android))
                throw new InvalidOperationException("Не удалось назначить OpenXR loader для Android.");

            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
                throw new InvalidOperationException("OpenXR settings для Android отсутствуют.");

            EnableFeature<MetaQuestFeature>(settings);
            EnableFeature<OculusTouchControllerProfile>(settings);
            settings.latencyOptimization = OpenXRSettings.LatencyOptimization.PrioritizeInputPolling;
            EditorUtility.SetDirty(settings);
        }

        private static void EnableFeature<T>(OpenXRSettings settings) where T : UnityEngine.XR.OpenXR.Features.OpenXRFeature
        {
            var feature = settings.GetFeature<T>();
            if (feature == null)
                throw new InvalidOperationException($"OpenXR feature {typeof(T).Name} отсутствует.");
            feature.enabled = true;
            EditorUtility.SetDirty(feature);
        }

        private static void CreateStartupScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var interactionManager = new GameObject("XR Interaction Manager");
            interactionManager.AddComponent<XRInteractionManager>();

            var rigPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarterAssetsRoot + "/Prefabs/XR Origin (XR Rig).prefab");
            if (rigPrefab == null)
                throw new InvalidOperationException("Prefab XR Origin из Starter Assets не найден.");
            var rig = (GameObject)PrefabUtility.InstantiatePrefab(rigPrefab, scene);
            rig.name = "XR Origin";

            var inputAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputAsset == null)
                throw new InvalidOperationException("Основной Input Action Asset не найден.");

            var bootstrapObject = new GameObject("VR Bootstrap");
            var bootstrap = bootstrapObject.AddComponent<VrBootstrap>();
            var serializedBootstrap = new SerializedObject(bootstrap);
            serializedBootstrap.FindProperty("inputActions").objectReferenceValue = inputAsset;
            serializedBootstrap.ApplyModifiedPropertiesWithoutUndo();

            CreateEnvironment();
            CreateWorldSpaceUi();

            if (UnityEngine.Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None).Length != 1)
                throw new InvalidOperationException("Стартовая сцена должна содержать ровно один XR Origin.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        private static void CreateEnvironment()
        {
            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Floor";
            floor.transform.localScale = new Vector3(2f, 1f, 2f);
            floor.GetComponent<Renderer>().sharedMaterial = CreateMaterial("FloorMaterial", new Color(0.12f, 0.16f, 0.2f));

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Grab Cube";
            cube.transform.SetPositionAndRotation(new Vector3(0f, 1.2f, 1.2f), Quaternion.identity);
            cube.transform.localScale = Vector3.one * 0.25f;
            cube.AddComponent<Rigidbody>();
            cube.AddComponent<XRGrabInteractable>();
            cube.GetComponent<Renderer>().sharedMaterial = CreateMaterial("GrabMaterial", new Color(0.1f, 0.55f, 0.95f));
        }

        private static void CreateWorldSpaceUi()
        {
            var eventSystemObject = new GameObject("Event System", typeof(EventSystem), typeof(XRUIInputModule));

            var canvasObject = new GameObject("World Space UI", typeof(Canvas), typeof(CanvasScaler), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rect = canvasObject.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(600f, 240f);
            rect.localScale = Vector3.one * 0.0015f;
            rect.SetPositionAndRotation(new Vector3(0f, 1.5f, 2f), Quaternion.Euler(0f, 180f, 0f));

            var panel = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            panel.transform.SetParent(canvasObject.transform, false);
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.1f, 0.92f);

            var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            label.transform.SetParent(panel.transform, false);
            var text = label.GetComponent<Text>();
            text.text = "VR GAME — STARTUP READY";
            text.alignment = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 42;
            text.color = Color.white;
            var labelRect = label.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        private static Material CreateMaterial(string name, Color color)
        {
            var path = $"Assets/_Project/Settings/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
                return material;

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("URP Lit shader не найден.");
            material = new Material(shader) { name = name, color = color };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void RemoveUnusedDemoAssets()
        {
            AssetDatabase.DeleteAsset(StarterAssetsRoot + "/DemoAssets");
            AssetDatabase.DeleteAsset(StarterAssetsRoot + "/DemoScene.unity");
        }

        private static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }
    }
}
