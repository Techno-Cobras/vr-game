using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VrGame.VRInteraction.Watering
{
    public sealed class WateringSource : MonoBehaviour
    {
        [SerializeField]
        private XRGrabInteractable interactable;

        [SerializeField]
        private WateringOutlet outlet;

        [SerializeField]
        private ParticleSystem successParticles;

        [SerializeField, Range(0f, 1f)]
        private float hapticAmplitude = .45f;

        [SerializeField, Min(0f)]
        private float hapticDuration = .08f;

        private bool subscribed;
        private bool applicationFocused = true;
        private bool applicationPaused;

        public event Action<WateringActivation> Activated;
        public event Action AvailabilityLost;

        public XRGrabInteractable Interactable => interactable;
        public WateringOutlet Outlet => outlet;
        public bool CanActivate =>
            isActiveAndEnabled && applicationFocused && !applicationPaused &&
            interactable != null && outlet != null && outlet.IsAvailable;

        public bool IsSelectedBy(IXRSelectInteractor interactor) =>
            CanActivate && interactor != null && interactable.isSelected &&
            interactable.interactorsSelecting.Contains(interactor);

        public void Configure(
            XRGrabInteractable grabInteractable,
            WateringOutlet wateringOutlet,
            ParticleSystem particles)
        {
            Unsubscribe();
            interactable = grabInteractable ?? throw new ArgumentNullException(nameof(grabInteractable));
            outlet = wateringOutlet ?? throw new ArgumentNullException(nameof(wateringOutlet));
            successParticles = particles;
            if (outlet.Source != this)
                throw new ArgumentException("Выход воды принадлежит другому источнику.", nameof(wateringOutlet));
            Subscribe();
        }

        public void PlaySuccessFeedback(IXRSelectInteractor activatingInteractor)
        {
            if (successParticles != null)
                successParticles.Play(true);

            if (activatingInteractor is XRBaseInputInteractor inputInteractor)
                inputInteractor.SendHapticImpulse(hapticAmplitude, hapticDuration);
        }

        internal void NotifyOutletUnavailable(WateringOutlet unavailableOutlet)
        {
            if (unavailableOutlet == outlet)
                InvokeIsolated(AvailabilityLost);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            InvokeIsolated(AvailabilityLost);
        }

        private void OnDestroy()
        {
            Unsubscribe();
            InvokeIsolated(AvailabilityLost);
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                InvokeIsolated(AvailabilityLost);
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
                InvokeIsolated(AvailabilityLost);
        }

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || interactable == null)
                return;
            interactable.activated.AddListener(OnActivated);
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (interactable != null)
                interactable.activated.RemoveListener(OnActivated);
            subscribed = false;
        }

        private void OnActivated(ActivateEventArgs args)
        {
            if (!CanActivate || args?.interactorObject == null ||
                !ReferenceEquals(args.interactableObject, interactable) || !interactable.isSelected)
                return;

            var selectingInteractor = args.interactorObject as IXRSelectInteractor;
            if (!IsSelectedBy(selectingInteractor))
                return;

            var activation = new WateringActivation(Guid.NewGuid(), this, outlet, selectingInteractor);
            var handlers = Activated?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<WateringActivation> handler in handlers)
            {
                try { handler(activation); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void InvokeIsolated(Action callbacks)
        {
            var handlers = callbacks?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action handler in handlers)
            {
                try { handler(); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
