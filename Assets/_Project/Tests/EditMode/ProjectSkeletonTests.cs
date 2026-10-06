using System.IO;
using System.Linq;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Bootstrap;

namespace VrGame.Tests.EditMode
{
    public sealed class ProjectSkeletonTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Startup.unity";
        private const string InputPath = "Assets/_Project/Input/VRControls.inputactions";

        [Test]
        public void Manifest_FixesRequiredPackageVersions()
        {
            var manifest = File.ReadAllText("Packages/manifest.json");
            StringAssert.Contains("\"com.unity.inputsystem\": \"1.20.1\"", manifest);
            StringAssert.Contains("\"com.unity.render-pipelines.universal\": \"17.3.0\"", manifest);
            StringAssert.Contains("\"com.unity.xr.interaction.toolkit\": \"3.6.1\"", manifest);
            StringAssert.Contains("\"com.unity.xr.management\": \"4.7.0\"", manifest);
            StringAssert.Contains("\"com.unity.xr.openxr\": \"1.18.0\"", manifest);
        }

        [Test]
        public void InputAsset_HasActionMapsAndBindings()
        {
            var input = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputPath);
            Assert.That(input, Is.Not.Null);
            Assert.That(input.actionMaps.Count, Is.GreaterThan(0));
            Assert.That(input.bindings.Any(), Is.True);
        }

        [Test]
        public void StartupScene_HasSingleRigAndBasicInteractions()
        {
            EditorSceneManager.OpenScene(ScenePath);
            Assert.That(Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None), Is.Not.Empty);
            var bootstrap = Object.FindFirstObjectByType<VrBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.InputActions, Is.Not.Null);
        }
    }
}
