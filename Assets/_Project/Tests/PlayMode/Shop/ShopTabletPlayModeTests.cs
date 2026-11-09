using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using VrGame.Bootstrap;
using VrGame.Presentation.Shop;

namespace VrGame.Tests.PlayMode.Shop
{
    public sealed class ShopTabletPlayModeTests
    {
        [UnityTest]
        public IEnumerator Startup_ComposesOneTabletWithSharedSession()
        {
            SceneManager.LoadScene("Startup");
            yield return null;
            var composition = Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var tablets = Object.FindObjectsByType<ShopTabletController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(tablets, Has.Length.EqualTo(1));
            Assert.That(composition.ShopTablet, Is.SameAs(tablets[0]));
            Assert.That(composition.Purchases, Is.Not.Null);
            Assert.That(composition.Deliveries, Is.Not.Null);
            Assert.That(tablets[0].IsOpen, Is.False);
            Assert.That(tablets[0].GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(tablets[0].transform.parent.name, Is.EqualTo("Left Controller"));
        }
    }
}
