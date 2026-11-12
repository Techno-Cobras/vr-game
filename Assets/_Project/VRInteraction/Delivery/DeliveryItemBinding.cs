using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Data.Items;
using VrGame.Domain.Delivery;
using VrGame.Domain.Inventory;
using VrGame.VRInteraction.Fertilizers;

namespace VrGame.VRInteraction.Delivery
{
    /// <summary>Связывает один физический объект с одной записью очереди.</summary>
    public sealed class DeliveryItemBinding : MonoBehaviour, IDeliveryProjection
    {
        private Guid entryId;
        private Transform slot;
        private PendingDeliveryLedger ledger;
        private Inventory inventory;
        private XRGrabInteractable interactable;
        private FertilizerItemInstance fertilizerIdentity;
        private Action<ItemId> cleanupFertilizerOwnership;
        private Action<IDeliveryProjection> projectionReleased;
        private InventoryChangeContext claimContext;
        private XRGrabInteractable subscribedInteractable;
        private Rigidbody body;
        private bool originalKinematic;
        private bool originalUseGravity;
        private bool configured;
        private bool completed;
        private bool suppressReleaseNotification;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private bool bodyStateRestored;

        public Guid EntryId => entryId;
        public bool IsAlive => this != null && gameObject != null && !completed;
        public DeliveryMutationResult LastResult { get; private set; }
        public bool CanClaim => configured && !completed && isActiveAndEnabled &&
                                applicationFocused && !applicationPaused;

        public void Configure(
            Guid deliveryEntryId,
            Transform controlledSlot,
            PendingDeliveryLedger deliveryLedger,
            Inventory playerInventory,
            XRGrabInteractable grabInteractable,
            FertilizerItemInstance fertilizer,
            Action<ItemId> fertilizerCleanup,
            Action<IDeliveryProjection> released)
        {
            if (deliveryEntryId == Guid.Empty) throw new ArgumentException("Entry ID не может быть пустым.", nameof(deliveryEntryId));
            slot = controlledSlot != null ? controlledSlot : throw new ArgumentNullException(nameof(controlledSlot));
            ledger = deliveryLedger ?? throw new ArgumentNullException(nameof(deliveryLedger));
            inventory = playerInventory ?? throw new ArgumentNullException(nameof(playerInventory));
            interactable = grabInteractable != null ? grabInteractable : throw new ArgumentNullException(nameof(grabInteractable));
            projectionReleased = released ?? throw new ArgumentNullException(nameof(released));
            if (fertilizer != null && fertilizerCleanup == null)
                throw new ArgumentNullException(nameof(fertilizerCleanup));

            Unsubscribe();
            entryId = deliveryEntryId;
            fertilizerIdentity = fertilizer;
            cleanupFertilizerOwnership = fertilizerCleanup;
            claimContext = new InventoryChangeContext(
                "получение покупки из Delivery Box",
                Guid.NewGuid(),
                deliveryEntryId);
            body = GetComponent<Rigidbody>();
            if (body == null)
                throw new InvalidOperationException("Физическое представление доставки требует Rigidbody.");
            originalKinematic = body.isKinematic;
            originalUseGravity = body.useGravity;
            bodyStateRestored = false;
            HoldInSlot();
            configured = true;
            Subscribe();
        }

        public DeliveryMutationResult TryClaim()
        {
            if (!CanClaim)
                return null;

            LastResult = ledger.Claim(entryId, inventory, claimContext);
            if (!LastResult.Succeeded)
            {
                ReturnToSlotAfterRejectedClaim();
                return LastResult;
            }

            CompleteHandoff();
            return LastResult;
        }

        public void Rollback()
        {
            if (completed || this == null)
                return;
            suppressReleaseNotification = true;
            Unsubscribe();
            gameObject.SetActive(false);
            DestroySafely(gameObject);
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            // Если сам binding выключен во время активного handoff, оставляем
            // listener до XRI Drop: иначе исходную физику будет некому вернуть
            // после того, как XRGrabInteractable восстановит slot-state.
            if (completed && interactable != null && interactable.isSelected)
                return;
            Unsubscribe();
            if (completed)
                RestoreBodyState();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (completed)
                RestoreBodyState();
            if (!completed && !suppressReleaseNotification)
                NotifyReleased();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused && !completed)
                ReturnToSlotAfterRejectedClaim();
            else if (!paused && !completed && interactable != null && applicationFocused)
                interactable.enabled = true;
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused && !completed)
                ReturnToSlotAfterRejectedClaim();
            else if (focused && !completed && interactable != null && !applicationPaused)
                interactable.enabled = true;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (ReferenceEquals(args?.interactableObject, interactable))
                TryClaim();
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (completed && ReferenceEquals(args?.interactableObject, interactable))
                RestoreBodyAndDispose();
        }

        private void Subscribe()
        {
            if (!configured || !isActiveAndEnabled || interactable == null || subscribedInteractable == interactable)
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

        private void CompleteHandoff()
        {
            if (completed)
                return;
            if (fertilizerIdentity != null)
                fertilizerIdentity.MarkIssued(cleanupFertilizerOwnership);
            completed = true;
            suppressReleaseNotification = true;
            transform.SetParent(null, true);
            var released = projectionReleased;
            projectionReleased = null;
            released?.Invoke(this);

            // XRGrabInteractable запомнил slot-state (kinematic/no-gravity) в
            // OnSelectEntering и восстановит его во время Drop. Исходную физику
            // можно вернуть только после selectExited, когда Drop завершён.
            if (interactable == null || !interactable.isSelected)
                RestoreBodyAndDispose();
        }

        private void ReturnToSlotAfterRejectedClaim()
        {
            if (!configured || completed || slot == null)
                return;
            if (interactable != null && interactable.enabled)
            {
                interactable.enabled = false;
                if (isActiveAndEnabled)
                    StartCoroutine(ReenableInteractableNextFrame());
            }
            HoldInSlot();
        }

        private IEnumerator ReenableInteractableNextFrame()
        {
            yield return null;
            if (!completed && interactable != null && applicationFocused && !applicationPaused)
                interactable.enabled = true;
        }

        private void HoldInSlot()
        {
            transform.SetParent(slot, false);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.useGravity = false;
                body.isKinematic = true;
            }
        }

        private void RestoreBodyAndDispose()
        {
            RestoreBodyState();
            Unsubscribe();
            DestroySafely(this);
        }

        private void RestoreBodyState()
        {
            if (bodyStateRestored || body == null)
                return;
            body.isKinematic = originalKinematic;
            body.useGravity = originalUseGravity;
            bodyStateRestored = true;
        }

        private void NotifyReleased()
        {
            var released = projectionReleased;
            projectionReleased = null;
            released?.Invoke(this);
        }

        private static void DestroySafely(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
