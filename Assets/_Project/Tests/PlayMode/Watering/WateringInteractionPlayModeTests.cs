using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Bootstrap;
using VrGame.Data.Plants;
using VrGame.Domain.Plants;
using VrGame.VRInteraction.Watering;

namespace VrGame.Tests.PlayMode.Watering
{
    public sealed class WateringInteractionPlayModeTests
    {
        [UnityTest]
        public IEnumerator Startup_WiresPhysicalWateringAndImmediateIndicatorLifecycle()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            Assert.That(composition, Is.Not.Null);
            Assert.That(composition.WateringSource, Is.Not.Null);
            Assert.That(composition.WateringReceiver, Is.Not.Null);
            Assert.That(composition.IndicatorSource, Is.Not.Null);
            Assert.That(composition.IndicatorPresenter, Is.Not.Null);
            Assert.That(composition.WateringSource.GetComponent<Rigidbody>(), Is.Not.Null);
            Assert.That(composition.WateringSource.GetComponent<XRGrabInteractable>(), Is.Not.Null);
            Assert.That(composition.WateringSource.Outlet.transform.parent, Is.EqualTo(composition.WateringSource.transform));
            Assert.That(composition.WateringSource.Outlet.OutletCollider.gameObject,
                Is.EqualTo(composition.WateringSource.Outlet.gameObject));
            Assert.That(composition.WateringSource.transform.Find("Корпус").GetComponent<Collider>(), Is.Not.Null);

            MoveToNeedsWater(composition.PlantingSlot);
            yield return null;
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.EqualTo(1));

            composition.PlantingSlot.Water(Context("контрольное скрытие indicator"));
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.Zero);

            composition.IndicatorPresenter.enabled = false;
            composition.IndicatorPresenter.enabled = true;
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.Zero);
        }

        [UnityTest]
        public IEnumerator Watering_RequiresHeldExactOverlappingOutlet_AndMutatesOnce()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var slot = composition.PlantingSlot;
            var source = composition.WateringSource;
            var receiver = composition.WateringReceiver;
            var outletCollider = source.Outlet.OutletCollider;
            var zone = receiver.GetComponent<Collider>();
            var manager = UnityEngine.Object.FindFirstObjectByType<XRInteractionManager>();
            var interactorObject = new GameObject("Тестовый selecting interactor");
            var interactor = interactorObject.AddComponent<XRDirectInteractor>();
            interactor.interactionManager = manager;
            Assert.That(manager, Is.Not.Null);
            MoveToNeedsWater(slot);

            var bodyCollider = source.transform.Find("Корпус").GetComponent<Collider>();
            receiver.SendMessage("OnTriggerEnter", bodyCollider);
            Assert.That(receiver.ContactCount, Is.Zero);

            var rootDelta = zone.bounds.center - outletCollider.bounds.center;
            source.transform.position += rootDelta;
            Physics.SyncTransforms();
            receiver.SendMessage("OnTriggerEnter", outletCollider);
            Assert.That(receiver.ContactCount, Is.EqualTo(1));

            var notHeldVersion = slot.Version;
            var notHeld = receiver.ProcessActivation(new WateringActivation(
                Guid.NewGuid(), source, source.Outlet, interactor));
            Assert.That(notHeld, Is.Null);
            Assert.That(slot.Version, Is.EqualTo(notHeldVersion));

            manager.SelectEnter((IXRSelectInteractor)interactor, source.Interactable);
            Assert.That(source.IsSelectedBy(interactor), Is.True);

            source.transform.position += Vector3.up * 2f;
            Physics.SyncTransforms();
            var staleContact = receiver.ProcessActivation(new WateringActivation(
                Guid.NewGuid(), source, source.Outlet, interactor));
            Assert.That(staleContact, Is.Null);
            Assert.That(slot.Version, Is.EqualTo(notHeldVersion));

            source.transform.position -= Vector3.up * 2f;
            Physics.SyncTransforms();
            var particles = source.GetComponentInChildren<ParticleSystem>(true);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            PlantingSlotMutationResult first = null;
            receiver.WateringCompleted += result => first = result;
            source.Interactable.activated.Invoke(new ActivateEventArgs
            {
                interactorObject = interactor,
                interactableObject = source.Interactable
            });
            Assert.That(first, Is.Not.Null);
            Assert.That(first.Succeeded, Is.True);
            Assert.That(first.IsReplay, Is.False);
            Assert.That(slot.Version, Is.EqualTo(notHeldVersion + 1));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Growing));
            Assert.That(particles.isPlaying, Is.True);

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            PlantingSlotMutationResult repeated = null;
            receiver.WateringCompleted += result => repeated = result;
            source.Interactable.activated.Invoke(new ActivateEventArgs
            {
                interactorObject = interactor,
                interactableObject = source.Interactable
            });
            Assert.That(repeated.Succeeded, Is.False);
            Assert.That(repeated.Rejection, Is.EqualTo(PlantingSlotRejection.WaterNotNeeded));
            Assert.That(slot.Version, Is.EqualTo(notHeldVersion + 1));
            Assert.That(particles.isPlaying, Is.False);

            receiver.SendMessage("OnApplicationPause", true);
            Assert.That(receiver.ContactCount, Is.Zero);
            receiver.SendMessage("OnApplicationPause", false);
            Assert.That(receiver.ProcessActivation(new WateringActivation(
                Guid.NewGuid(), source, source.Outlet, interactor)), Is.Null);

            manager.SelectExit((IXRSelectInteractor)interactor, source.Interactable);
            UnityEngine.Object.Destroy(interactorObject);
        }

        private static void MoveToNeedsWater(PlantingSlot slot)
        {
            if (slot.GetSnapshot().State == PlantingSlotState.Empty)
                slot.Plant(new PlantTypeId("plant.basil"), Context("тестовая посадка"));
            var threshold = slot.GetSnapshot().WaterRequestThreshold.Value;
            slot.AdvanceProgress(threshold, slot.Version, Context("достижение порога воды"));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.NeedsWater));
        }

        private static PlantingSlotChangeContext Context(string reason) =>
            new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());
    }
}
