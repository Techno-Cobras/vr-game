using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Bootstrap;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Harvesting;
using VrGame.Domain.Plants;

namespace VrGame.Tests.PlayMode.Harvesting
{
    public sealed class HarvestInteractionPlayModeTests
    {
        private static readonly PlantTypeId Basil = new PlantTypeId("plant.basil");
        private static readonly ItemId BasilHarvest = new ItemId("item.harvest.basil");

        [UnityTest]
        public IEnumerator Startup_HarvestRequiresSelectionAndReadyState_ThenRefreshesFeedbackAndAllowsReplant()
        {
            SceneManager.LoadScene("Startup");
            yield return null;

            var composition = UnityEngine.Object.FindFirstObjectByType<SeedStorageCompositionRoot>();
            var manager = UnityEngine.Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            Assert.That(composition, Is.Not.Null);
            Assert.That(composition.Harvesting, Is.Not.Null);
            Assert.That(composition.HarvestController, Is.Not.Null);
            Assert.That(composition.HarvestController.Interactable, Is.Not.Null);
            Assert.That(manager, Is.Not.Null);

            var controller = composition.HarvestController;
            var successParticles = controller.transform.Find("Успешный сбор").GetComponent<ParticleSystem>();
            var rejectionParticles = controller.transform.Find("Сбор отклонён").GetComponent<ParticleSystem>();
            successParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rejectionParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Assert.That(controller.TryHarvest(null), Is.Null, "Harvest requires an active selecting interactor.");

            var interactorObject = new GameObject("Harvest test interactor");
            var interactor = interactorObject.AddComponent<XRDirectInteractor>();
            interactor.interactionManager = manager;
            composition.PlantingSlot.Plant(Basil, Context("посадка для rejected harvest"));

            HarvestResult completed = null;
            controller.HarvestCompleted += result => completed = result;
            manager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)controller.Interactable);
            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.Rejection, Is.EqualTo(HarvestRejection.SlotRejected));
            Assert.That(completed.SlotRejection, Is.EqualTo(PlantingSlotRejection.NotReady));
            Assert.That(rejectionParticles.isPlaying, Is.True);
            Assert.That(composition.PlayerInventory.GetQuantity(BasilHarvest), Is.Zero);
            manager.SelectExit((IXRSelectInteractor)interactor, (IXRSelectInteractable)controller.Interactable);

            completed = null;
            controller.SendMessage("OnApplicationPause", true);
            manager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)controller.Interactable);
            Assert.That(completed, Is.Null, "Paused application must gate harvest commands.");
            manager.SelectExit((IXRSelectInteractor)interactor, (IXRSelectInteractable)controller.Interactable);
            controller.SendMessage("OnApplicationPause", false);

            MoveToReady(composition.PlantingSlot);
            composition.GrowthController.RefreshVisual();
            Assert.That(composition.GrowthController.ActiveStageObject, Is.Not.Null);
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.EqualTo(1));
            successParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            completed = null;

            manager.SelectEnter((IXRSelectInteractor)interactor, (IXRSelectInteractable)controller.Interactable);

            Assert.That(completed, Is.Not.Null);
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(completed.HarvestItemId, Is.EqualTo(BasilHarvest));
            Assert.That(completed.Quantity, Is.EqualTo(3));
            Assert.That(composition.PlayerInventory.GetQuantity(BasilHarvest), Is.EqualTo(3));
            Assert.That(composition.PlantingSlot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.Empty));
            Assert.That(composition.GrowthController.ActiveStageObject, Is.Null,
                "Successful harvest must clear the representation immediately.");
            Assert.That(composition.IndicatorPresenter.ActiveCount, Is.Zero,
                "Successful harvest must hide the ready indicator immediately.");
            Assert.That(successParticles.isPlaying, Is.True);
            manager.SelectExit((IXRSelectInteractor)interactor, (IXRSelectInteractable)controller.Interactable);

            var replant = composition.PlantingSlot.Plant(Basil, Context("повторная посадка"));
            composition.GrowthController.RefreshVisual();
            Assert.That(replant.Succeeded, Is.True);
            Assert.That(composition.GrowthController.ActiveStageObject, Is.Not.Null);

            UnityEngine.Object.Destroy(interactorObject);
            yield return null;
        }

        private static void MoveToReady(PlantingSlot slot)
        {
            var snapshot = slot.GetSnapshot();
            if (snapshot.State == PlantingSlotState.Growing)
                slot.AdvanceProgress(1f, slot.Version, Context("достижение запроса воды"));
            if (slot.GetSnapshot().State == PlantingSlotState.NeedsWater)
                slot.Water(Context("полив"));
            slot.AdvanceProgress(1f, slot.Version, Context("созревание"));
            Assert.That(slot.GetSnapshot().State, Is.EqualTo(PlantingSlotState.ReadyToHarvest));
        }

        private static PlantingSlotChangeContext Context(string reason) =>
            new PlantingSlotChangeContext(reason, Guid.NewGuid(), Guid.NewGuid());
    }
}
