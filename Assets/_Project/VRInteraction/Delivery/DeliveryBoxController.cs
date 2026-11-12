using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Domain.Delivery;
using VrGame.Domain.Inventory;

namespace VrGame.VRInteraction.Delivery
{
    public interface IDeliveryProjection
    {
        Guid EntryId { get; }
        bool IsAlive { get; }
        void Rollback();
    }

    public interface IDeliveryItemMaterializer
    {
        bool TryMaterialize(
            PendingDeliveryEntry entry,
            Transform slot,
            PendingDeliveryLedger ledger,
            Inventory inventory,
            Action<IDeliveryProjection> projectionReleased,
            out IDeliveryProjection projection);
    }

    /// <summary>
    /// Проекция авторитетной очереди доставки в конечный набор физических слотов.
    /// Callback очереди только помечает reconciliation: сама очередь не изменяется
    /// синхронно из её событий.
    /// </summary>
    public sealed class DeliveryBoxController : MonoBehaviour
    {
        [SerializeField, Min(.05f)] private float retryIntervalSeconds = .25f;

        private PendingDeliveryLedger ledger;
        private Inventory inventory;
        private IDeliveryItemMaterializer materializer;
        private Transform[] slots = Array.Empty<Transform>();
        private SlotProjection[] projections = Array.Empty<SlotProjection>();
        private bool subscribed;
        private bool reconciliationRequested;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private float nextRetryAt;

        public int Capacity => slots.Length;
        public int ActiveProjectionCount
        {
            get
            {
                var count = 0;
                for (var index = 0; index < projections.Length; index++)
                    if (projections[index].Projection != null && projections[index].Projection.IsAlive)
                        count++;
                return count;
            }
        }

        public bool CanReconcile =>
            isActiveAndEnabled && applicationFocused && !applicationPaused &&
            ledger != null && inventory != null && materializer != null && slots.Length > 0;

        public void Configure(
            PendingDeliveryLedger deliveryLedger,
            Inventory playerInventory,
            IDeliveryItemMaterializer itemMaterializer,
            IReadOnlyList<Transform> controlledSlots)
        {
            if (deliveryLedger == null) throw new ArgumentNullException(nameof(deliveryLedger));
            if (playerInventory == null) throw new ArgumentNullException(nameof(playerInventory));
            if (itemMaterializer == null) throw new ArgumentNullException(nameof(itemMaterializer));
            if (controlledSlots == null || controlledSlots.Count == 0)
                throw new ArgumentException("Delivery Box должен иметь хотя бы один контролируемый слот.", nameof(controlledSlots));

            var configuredSlots = new Transform[controlledSlots.Count];
            for (var index = 0; index < controlledSlots.Count; index++)
                configuredSlots[index] = controlledSlots[index] != null
                    ? controlledSlots[index]
                    : throw new ArgumentException($"Слот Delivery Box #{index + 1} отсутствует.", nameof(controlledSlots));

            Unsubscribe();
            RollbackAll();
            ledger = deliveryLedger;
            inventory = playerInventory;
            materializer = itemMaterializer;
            slots = configuredSlots;
            projections = new SlotProjection[slots.Length];
            reconciliationRequested = true;
            Subscribe();
        }

        public bool IsProjected(Guid entryId)
        {
            for (var index = 0; index < projections.Length; index++)
                if (projections[index].EntryId == entryId &&
                    projections[index].Projection != null && projections[index].Projection.IsAlive)
                    return true;
            return false;
        }

        /// <summary>Явная точка шага для тестов и следующего Unity tick.</summary>
        public void ReconcileNow()
        {
            if (!CanReconcile)
                return;

            reconciliationRequested = false;
            var needsRetry = RemoveLostProjections();
            var snapshot = ledger.GetSnapshot();

            // Сначала восстанавливаем уже Available: физический объект не является
            // источником истины и мог быть уничтожен до получения.
            for (var index = 0; index < snapshot.Count; index++)
            {
                var entry = snapshot[index];
                if (entry.Status == DeliveryStatus.Available && !IsProjected(entry.Id))
                    needsRetry |= !TryProject(entry, markAvailable: false);
            }

            // Затем занимаем оставшиеся слоты Pending-записями в порядке snapshot/FIFO.
            for (var index = 0; index < snapshot.Count; index++)
            {
                var entry = snapshot[index];
                if (entry.Status != DeliveryStatus.Pending)
                    continue;
                if (!HasFreeSlot())
                    break;
                if (!TryProject(entry, markAvailable: true))
                {
                    // Временная ошибка первого Pending не должна позволять
                    // более поздней покупке обогнать его в FIFO.
                    needsRetry = true;
                    break;
                }
            }

            if (needsRetry)
            {
                reconciliationRequested = true;
                nextRetryAt = Time.unscaledTime + retryIntervalSeconds;
            }
        }

        private void Update()
        {
            if (reconciliationRequested && Time.unscaledTime >= nextRetryAt)
                ReconcileNow();
            else if (CanReconcile && HasLostProjection())
            {
                reconciliationRequested = true;
                nextRetryAt = Time.unscaledTime;
            }
        }

        private void OnEnable()
        {
            Subscribe();
            reconciliationRequested = true;
        }

        private void OnDisable()
        {
            Unsubscribe();
            // Выключенный adapter не оставляет активные grab-команды. Очередь
            // сохраняет Available и восстановит проекции после OnEnable.
            RollbackAll();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            RollbackAll();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (!paused)
                RequestReconciliation();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (focused)
                RequestReconciliation();
        }

        private bool TryProject(PendingDeliveryEntry entry, bool markAvailable)
        {
            if (entry.Quantity != 1)
            {
                Debug.LogWarning(
                    $"Delivery entry '{entry.Id}' содержит quantity {entry.Quantity}; MVP Delivery Box выдаёт только одну физическую единицу и сохраняет запись в очереди.",
                    this);
                return false;
            }

            var slotIndex = FindFreeSlot();
            if (slotIndex < 0)
                return false;

            if (!materializer.TryMaterialize(
                    entry,
                    slots[slotIndex],
                    ledger,
                    inventory,
                    HandleProjectionReleased,
                    out var projection) || projection == null || !projection.IsAlive)
            {
                projection?.Rollback();
                return false;
            }

            projections[slotIndex] = new SlotProjection(entry.Id, projection);
            if (!markAvailable)
                return true;

            var result = ledger.MarkAvailable(entry.Id);
            if (result.Succeeded)
                return true;

            projection.Rollback();
            projections[slotIndex] = default;
            return false;
        }

        private void HandleProjectionReleased(IDeliveryProjection projection)
        {
            for (var index = 0; index < projections.Length; index++)
            {
                if (!ReferenceEquals(projections[index].Projection, projection))
                    continue;
                projections[index] = default;
                break;
            }
            RequestReconciliation();
        }

        private void OnQueueChanged(PendingDeliveryEntry _) => RequestReconciliation();

        private void RequestReconciliation()
        {
            reconciliationRequested = true;
            nextRetryAt = Time.unscaledTime;
        }

        private void Subscribe()
        {
            if (!isActiveAndEnabled || subscribed || ledger == null)
                return;
            ledger.Added += OnQueueChanged;
            ledger.Changed += OnQueueChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || ledger == null)
                return;
            ledger.Added -= OnQueueChanged;
            ledger.Changed -= OnQueueChanged;
            subscribed = false;
        }

        private bool HasFreeSlot() => FindFreeSlot() >= 0;

        private int FindFreeSlot()
        {
            for (var index = 0; index < projections.Length; index++)
                if (projections[index].Projection == null || !projections[index].Projection.IsAlive)
                    return index;
            return -1;
        }

        private bool HasLostProjection()
        {
            for (var index = 0; index < projections.Length; index++)
                if (projections[index].Projection != null && !projections[index].Projection.IsAlive)
                    return true;
            return false;
        }

        private bool RemoveLostProjections()
        {
            var removed = false;
            for (var index = 0; index < projections.Length; index++)
            {
                if (projections[index].Projection == null || projections[index].Projection.IsAlive)
                    continue;
                projections[index] = default;
                removed = true;
            }
            return removed;
        }

        private void RollbackAll()
        {
            for (var index = 0; index < projections.Length; index++)
            {
                projections[index].Projection?.Rollback();
                projections[index] = default;
            }
        }

        private readonly struct SlotProjection
        {
            public SlotProjection(Guid entryId, IDeliveryProjection projection)
            {
                EntryId = entryId;
                Projection = projection;
            }

            public Guid EntryId { get; }
            public IDeliveryProjection Projection { get; }
        }
    }
}
