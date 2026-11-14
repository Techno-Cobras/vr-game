using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Data.Items;
using VrGame.Domain.Orders;

namespace VrGame.VRInteraction.Orders
{
    [RequireComponent(typeof(XRSimpleInteractable))]
    public sealed class OrderDeliveryInteractionController : MonoBehaviour
    {
        [SerializeField] private XRSimpleInteractable interactable;
        [SerializeField] private ParticleSystem successParticles;
        [SerializeField] private ParticleSystem rejectionParticles;
        [SerializeField, Range(0f, 1f)] private float successHapticAmplitude = .55f;
        [SerializeField, Range(0f, 1f)] private float rejectionHapticAmplitude = .2f;
        [SerializeField, Min(0f)] private float hapticDuration = .08f;

        private OrderDeliveryCoordinator coordinator;
        private OrderService orders;
        private XRSimpleInteractable subscribedInteractable;
        private IXRSelectInteractor currentInteractor;
        private OrderDeliveryContext currentContext;
        private ItemId currentMedicineItemId;
        private int currentQuantity;
        private bool hasCurrentContext;
        private bool applicationFocused = true;
        private bool applicationPaused;

        public event Action<OrderDeliveryResult> DeliveryCompleted;
        public XRSimpleInteractable Interactable => interactable;
        public bool CanDeliver =>
            isActiveAndEnabled && applicationFocused && !applicationPaused &&
            coordinator != null && orders != null && interactable != null;

        public void Configure(
            XRSimpleInteractable deliveryInteractable,
            OrderDeliveryCoordinator deliveryCoordinator,
            OrderService orderService,
            ParticleSystem success = null,
            ParticleSystem rejection = null)
        {
            if (deliveryInteractable == null)
                throw new ArgumentNullException(nameof(deliveryInteractable));
            if (deliveryCoordinator == null)
                throw new ArgumentNullException(nameof(deliveryCoordinator));
            if (orderService == null)
                throw new ArgumentNullException(nameof(orderService));

            Unsubscribe();
            ClearIntent();
            interactable = deliveryInteractable;
            coordinator = deliveryCoordinator;
            orders = orderService;
            if (success != null)
                successParticles = success;
            if (rejection != null)
                rejectionParticles = rejection;
            Subscribe();
        }

        public OrderDeliveryResult TryDeliver(IXRSelectInteractor selectingInteractor = null)
        {
            if (!CanDeliver)
                return null;

            if (!hasCurrentContext || !ReferenceEquals(currentInteractor, selectingInteractor))
            {
                currentInteractor = selectingInteractor;
                var active = GetActiveOrder();
                currentMedicineItemId = active?.MedicineItemId ?? default;
                currentQuantity = active?.Quantity ?? 0;
                currentContext = new OrderDeliveryContext(
                    "VR-доставка лекарства по активному заказу",
                    Guid.NewGuid(),
                    Guid.NewGuid());
                hasCurrentContext = true;
            }

            var result = coordinator.Deliver(
                currentMedicineItemId,
                currentQuantity,
                currentContext);
            if (result.Succeeded)
            {
                if (!result.IsReplay)
                    PlayFeedback(successParticles, selectingInteractor, successHapticAmplitude);
            }
            else
            {
                PlayFeedback(rejectionParticles, selectingInteractor, rejectionHapticAmplitude);
            }
            InvokeIsolated(DeliveryCompleted, result);
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
                TryDeliver(selectingInteractor);
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
            currentMedicineItemId = default;
            currentQuantity = 0;
            hasCurrentContext = false;
        }

        private OrderSnapshot GetActiveOrder()
        {
            var snapshot = orders.GetSnapshot();
            if (!snapshot.ActiveOrderId.HasValue)
                return null;

            foreach (var order in snapshot.Orders)
                if (order.OrderId == snapshot.ActiveOrderId.Value)
                    return order;

            return null;
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

        private void InvokeIsolated(Action<OrderDeliveryResult> callbacks, OrderDeliveryResult result)
        {
            var handlers = callbacks?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<OrderDeliveryResult> handler in handlers)
            {
                try { handler(result); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
