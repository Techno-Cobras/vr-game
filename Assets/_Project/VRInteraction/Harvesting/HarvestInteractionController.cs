using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Domain.Harvesting;
using VrGame.VRInteraction.Plants;

namespace VrGame.VRInteraction.Harvesting
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class HarvestInteractionController : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private ParticleSystem successParticles;
        [SerializeField] private ParticleSystem rejectionParticles;
        [SerializeField, Range(0f, 1f)] private float successHapticAmplitude = .55f;
        [SerializeField, Range(0f, 1f)] private float rejectionHapticAmplitude = .2f;
        [SerializeField, Min(0f)] private float hapticDuration = .08f;

        private HarvestCoordinator coordinator;
        private PlantGrowthController growthController;
        private XRSimpleInteractable subscribedInteractable;
        private IXRSelectInteractor currentInteractor;
        private HarvestContext currentContext;
        private bool hasCurrentContext;
        private bool applicationFocused = true;
        private bool applicationPaused;

        public event Action<HarvestResult> HarvestCompleted;
        public bool CanHarvest =>
            isActiveAndEnabled && applicationFocused && !applicationPaused &&
            coordinator != null && interactable != null;
        public XRSimpleInteractable Interactable => interactable;

        public void Configure(
            XRSimpleInteractable harvestInteractable,
            HarvestCoordinator harvestCoordinator,
            PlantGrowthController plantGrowthController,
            ParticleSystem success = null,
            ParticleSystem rejection = null)
        {
            if (harvestInteractable == null)
                throw new ArgumentNullException(nameof(harvestInteractable));
            if (harvestCoordinator == null)
                throw new ArgumentNullException(nameof(harvestCoordinator));
            if (plantGrowthController == null)
                throw new ArgumentNullException(nameof(plantGrowthController));

            Unsubscribe();
            interactable = harvestInteractable;
            coordinator = harvestCoordinator;
            growthController = plantGrowthController;
            if (success != null)
                successParticles = success;
            if (rejection != null)
                rejectionParticles = rejection;
            Subscribe();
        }

        public HarvestResult TryHarvest(IXRSelectInteractor selectingInteractor)
        {
            if (!CanHarvest || selectingInteractor == null || !interactable.isSelected ||
                !interactable.interactorsSelecting.Contains(selectingInteractor))
                return null;

            if (!hasCurrentContext || !ReferenceEquals(currentInteractor, selectingInteractor))
            {
                currentInteractor = selectingInteractor;
                currentContext = new HarvestContext(
                    "VR-сбор созревшего растения",
                    Guid.NewGuid(),
                    Guid.NewGuid());
                hasCurrentContext = true;
            }

            var result = coordinator.Harvest(currentContext);
            if (result.Succeeded)
            {
                growthController.RefreshVisual();
                if (!result.IsReplay)
                    PlayFeedback(successParticles, selectingInteractor, successHapticAmplitude);
            }
            else
            {
                PlayFeedback(rejectionParticles, selectingInteractor, rejectionHapticAmplitude);
            }
            InvokeIsolated(HarvestCompleted, result);
            return result;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            ClearIntent();
        }

        private void OnDestroy() => Unsubscribe();

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                ClearIntent();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
                ClearIntent();
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || interactable == null || subscribedInteractable == interactable)
                return;
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
            subscribedInteractable = interactable;
        }

        private void Unsubscribe()
        {
            if (subscribedInteractable == null)
                return;
            subscribedInteractable.selectEntered.RemoveListener(OnSelectEntered);
            subscribedInteractable.selectExited.RemoveListener(OnSelectExited);
            subscribedInteractable = null;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (args?.interactorObject is IXRSelectInteractor selectingInteractor &&
                ReferenceEquals(args.interactableObject, interactable))
                TryHarvest(selectingInteractor);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (args?.interactorObject != null && ReferenceEquals(args.interactorObject, currentInteractor))
                ClearIntent();
        }

        private void ClearIntent()
        {
            currentInteractor = null;
            currentContext = default;
            hasCurrentContext = false;
        }

        private void PlayFeedback(
            ParticleSystem particles,
            IXRSelectInteractor selectingInteractor,
            float amplitude)
        {
            if (particles != null)
                particles.Play(true);
            if (selectingInteractor is XRBaseInputInteractor inputInteractor)
                inputInteractor.SendHapticImpulse(amplitude, hapticDuration);
        }

        private void InvokeIsolated(Action<HarvestResult> callbacks, HarvestResult result)
        {
            var handlers = callbacks?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<HarvestResult> handler in handlers)
            {
                try { handler(result); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
