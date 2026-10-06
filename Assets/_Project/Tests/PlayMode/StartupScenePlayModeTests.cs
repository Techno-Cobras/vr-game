using System.Collections;
using NUnit.Framework;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Bootstrap;

namespace VrGame.Tests.PlayMode
{
    public sealed class StartupScenePlayModeTests
    {
        [UnityTest]
        public IEnumerator StartupScene_LoadsRequiredVrObjects()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            Assert.That(Object.FindObjectsByType<XROrigin>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<XRInteractionManager>(FindObjectsSortMode.None), Has.Length.EqualTo(1));
            Assert.That(Object.FindObjectsByType<XRGrabInteractable>(FindObjectsSortMode.None), Is.Not.Empty);
            Assert.That(Object.FindFirstObjectByType<VrBootstrap>(), Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator InputLifecycle_RestoresActionsAfterFocusReturns()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var bootstrap = Object.FindFirstObjectByType<VrBootstrap>();
            Assert.That(bootstrap.InputActions.enabled, Is.True);

            bootstrap.SendMessage("OnApplicationFocus", false);
            Assert.That(bootstrap.InputActions.enabled, Is.False);

            bootstrap.SendMessage("OnApplicationFocus", true);
            Assert.That(bootstrap.InputActions.enabled, Is.True);
        }

        [UnityTest]
        public IEnumerator InputLifecycle_RequiresFocusAndResumeBeforeEnablingActions()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var bootstrap = Object.FindFirstObjectByType<VrBootstrap>();
            bootstrap.SendMessage("OnApplicationPause", true);
            bootstrap.SendMessage("OnApplicationFocus", false);
            Assert.That(bootstrap.InputActions.enabled, Is.False);

            bootstrap.SendMessage("OnApplicationPause", false);
            Assert.That(bootstrap.InputActions.enabled, Is.False);

            bootstrap.SendMessage("OnApplicationFocus", true);
            Assert.That(bootstrap.InputActions.enabled, Is.True);
        }
    }
}
