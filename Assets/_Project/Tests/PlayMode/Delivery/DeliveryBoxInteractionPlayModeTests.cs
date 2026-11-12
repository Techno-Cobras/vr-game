using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Bootstrap;
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;
using VrGame.VRInteraction.Delivery;

namespace VrGame.Tests.PlayMode.Delivery
{
    public sealed class DeliveryBoxInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator GrabClaim_DropRestoresOriginalDynamicGravity_AfterXriDrop()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
            Assert.That(composition, Is.Not.Null);
            Assert.That(manager, Is.Not.Null);
            var entry = BuyFirstOffer(composition);
            composition.DeliveryBox.ReconcileNow();

            var binding = UnityEngine.Object.FindFirstObjectByType<DeliveryItemBinding>();
            Assert.That(binding, Is.Not.Null);
            Assert.That(binding.EntryId, Is.EqualTo(entry.Id));
            var grab = binding.GetComponent<XRGrabInteractable>();
            var body = binding.GetComponent<Rigidbody>();
            Assert.That(body.isKinematic, Is.True, "В slot объект должен удерживаться без физического падения.");
            Assert.That(body.useGravity, Is.False);

            var interactorObject = new GameObject("Delivery Box test interactor");
            var interactor = interactorObject.AddComponent<XRDirectInteractor>();
            interactor.interactionManager = manager;
            manager.SelectEnter((IXRSelectInteractor)interactor, grab);

            Assert.That(grab.isSelected, Is.True);
            Assert.That(composition.Deliveries.GetSnapshot().Single(value => value.Id == entry.Id).Status,
                Is.EqualTo(DeliveryStatus.Claimed));
            Assert.That(composition.PlayerInventory.GetQuantity(entry.ItemId), Is.EqualTo(1));
            Assert.That(body.isKinematic, Is.True,
                "До XRI Drop нельзя восстанавливать физику поверх активного grab.");
            Assert.That(body.useGravity, Is.False);

            manager.SelectExit((IXRSelectInteractor)interactor, grab);

            Assert.That(grab.isSelected, Is.False);
            Assert.That(body.isKinematic, Is.False,
                "После XRI Drop должна восстановиться исходная динамическая физика prefab.");
            Assert.That(body.useGravity, Is.True);
            Assert.That(composition.PlayerInventory.GetQuantity(entry.ItemId), Is.EqualTo(1));
            yield return null;
            Assert.That(body.isKinematic, Is.False);
            Assert.That(body.useGravity, Is.True);

            UnityEngine.Object.Destroy(interactorObject);
            UnityEngine.Object.Destroy(body.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseAndFocusGates_RequireBothRestored_InEitherOrder()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            BuyFirstOffer(composition);
            composition.DeliveryBox.ReconcileNow();
            var binding = UnityEngine.Object.FindFirstObjectByType<DeliveryItemBinding>();
            var grab = binding.GetComponent<XRGrabInteractable>();

            binding.SendMessage("OnApplicationPause", true);
            binding.SendMessage("OnApplicationFocus", false);
            binding.SendMessage("OnApplicationPause", false);
            Assert.That(binding.CanClaim, Is.False);
            Assert.That(grab.enabled, Is.False,
                "Resume не должен включать grab, пока focus всё ещё потерян.");
            Assert.That(binding.TryClaim(), Is.Null);
            binding.SendMessage("OnApplicationFocus", true);
            Assert.That(binding.CanClaim, Is.True);
            Assert.That(grab.enabled, Is.True);

            binding.SendMessage("OnApplicationFocus", false);
            binding.SendMessage("OnApplicationPause", true);
            binding.SendMessage("OnApplicationFocus", true);
            Assert.That(binding.CanClaim, Is.False);
            Assert.That(grab.enabled, Is.False,
                "Focus regain не должен включать grab, пока приложение paused.");
            Assert.That(binding.TryClaim(), Is.Null);
            binding.SendMessage("OnApplicationPause", false);
            Assert.That(binding.CanClaim, Is.True);
            Assert.That(grab.enabled, Is.True);

            yield return null;
            Assert.That(grab.enabled, Is.True);
        }

        private static PendingDeliveryEntry BuyFirstOffer(SeedStorageCompositionRoot composition)
        {
            var result = composition.Purchases.Purchase(
                composition.ShopCatalog.Definitions[0].Id,
                new EconomyChangeContext(
                    "PlayMode-покупка для Delivery Box",
                    Guid.NewGuid(),
                    Guid.NewGuid()));
            Assert.That(result.Succeeded, Is.True);
            return result.Entry;
        }
    }
}
