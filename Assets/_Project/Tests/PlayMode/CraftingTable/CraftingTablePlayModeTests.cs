using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;
using VrGame.Bootstrap;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;
using VrGame.Presentation.Crafting;

namespace VrGame.Tests.PlayMode.CraftingTable
{
    public sealed class CraftingTablePlayModeTests
    {
        [UnityTest]
        public IEnumerator Startup_WiresOneReadableCraftingTableAndSharedInventoryFlow()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var controllers = UnityEngine.Object.FindObjectsByType<CraftingTableController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(composition, Is.Not.Null);
            Assert.That(controllers, Has.Length.EqualTo(1));
            Assert.That(composition.CraftingController, Is.SameAs(controllers[0]));
            Assert.That(composition.Crafting, Is.Not.Null);
            Assert.That(composition.Recipes.Definitions, Has.Count.EqualTo(2));

            var canvas = controllers[0].GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
            Assert.That(canvas.GetComponent<TrackedDeviceGraphicRaycaster>(), Is.Not.Null);
            var buttons = controllers[0].GetComponentsInChildren<Button>(true);
            Assert.That(buttons, Has.Length.EqualTo(3));
            foreach (var button in buttons)
                Assert.That(button.onClick.GetPersistentEventCount(), Is.EqualTo(1));

            var basil = new ItemId("item.harvest.basil");
            var medicine = new ItemId("item.medicine.basil-tonic");
            var add = composition.PlayerInventory.Add(
                basil,
                2,
                new InventoryChangeContext("playmode урожай", Guid.NewGuid(), Guid.NewGuid()));
            Assert.That(add.Succeeded, Is.True);
            Assert.That(controllers[0].Snapshot.SelectedRecipe.CanCraft, Is.True);
            var version = composition.PlayerInventory.Version;

            var crafted = controllers[0].CraftSelected();
            var duplicate = controllers[0].CraftSelected();

            Assert.That(crafted.Succeeded, Is.True);
            Assert.That(duplicate, Is.SameAs(crafted));
            Assert.That(composition.PlayerInventory.Version, Is.EqualTo(version + 1));
            Assert.That(composition.PlayerInventory.GetQuantity(basil), Is.Zero);
            Assert.That(composition.PlayerInventory.GetQuantity(medicine), Is.EqualTo(1));
            Assert.That(controllers[0].Snapshot.SelectedRecipe.OwnedOutputQuantity, Is.EqualTo(1));
            Assert.That(controllers[0].Snapshot.Feedback, Is.EqualTo(CraftingTableFeedback.Success));

            yield return null;
        }
    }
}
