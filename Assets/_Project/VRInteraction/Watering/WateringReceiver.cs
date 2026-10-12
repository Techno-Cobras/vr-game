using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Domain.Plants;

namespace VrGame.VRInteraction.Watering
{
    [RequireComponent(typeof(Collider))]
    public sealed class WateringReceiver : MonoBehaviour
    {
        [SerializeField]
        private Collider triggerZone;

        private readonly Dictionary<Collider, int> contacts = new Dictionary<Collider, int>();
        private WateringSource source;
        private WateringAdapter adapter;
        private bool subscribed;
        private bool applicationFocused = true;
        private bool applicationPaused;

        public event Action<PlantingSlotMutationResult> WateringCompleted;

        public bool CanReceive =>
            isActiveAndEnabled && applicationFocused && !applicationPaused &&
            source != null && adapter != null && triggerZone != null && triggerZone.enabled;

        public int ContactCount => contacts.Count;

        public void Configure(Collider zone, WateringSource wateringSource, PlantingSlot plantingSlot)
        {
            if (zone == null)
                throw new ArgumentNullException(nameof(zone));
            if (!zone.isTrigger)
                throw new ArgumentException("Зона полива должна быть trigger collider.", nameof(zone));

            Unsubscribe();
            contacts.Clear();
            triggerZone = zone;
            source = wateringSource ?? throw new ArgumentNullException(nameof(wateringSource));
            adapter = new WateringAdapter(plantingSlot ?? throw new ArgumentNullException(nameof(plantingSlot)));
            Subscribe();
        }

        public PlantingSlotMutationResult ProcessActivation(WateringActivation activation)
        {
            if (!CanReceive || activation.Source != source || activation.Outlet != source.Outlet ||
                !source.CanActivate || !contacts.ContainsKey(activation.Outlet.OutletCollider) ||
                !source.IsSelectedBy(activation.Interactor) ||
                !Overlaps(triggerZone, activation.Outlet.OutletCollider))
                return null;

            var result = adapter.Water(activation.ActivationId);
            if (result.Succeeded && !result.IsReplay)
                source.PlaySuccessFeedback(activation.Interactor);
            InvokeCompleted(result);
            return result;
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            contacts.Clear();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            contacts.Clear();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                contacts.Clear();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
                contacts.Clear();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!CanReceive || source?.Outlet == null || other != source.Outlet.OutletCollider)
                return;
            contacts.TryGetValue(other, out var count);
            contacts[other] = count + 1;
        }

        private void OnTriggerExit(Collider other)
        {
            if (other == null || !contacts.TryGetValue(other, out var count))
                return;
            if (count <= 1)
                contacts.Remove(other);
            else
                contacts[other] = count - 1;
        }

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || source == null)
                return;
            source.Activated += HandleActivation;
            source.AvailabilityLost += ClearContacts;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (source != null)
            {
                source.Activated -= HandleActivation;
                source.AvailabilityLost -= ClearContacts;
            }
            subscribed = false;
        }

        private void HandleActivation(WateringActivation activation) => ProcessActivation(activation);
        private void ClearContacts() => contacts.Clear();

        private void InvokeCompleted(PlantingSlotMutationResult result)
        {
            var handlers = WateringCompleted?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<PlantingSlotMutationResult> handler in handlers)
            {
                try { handler(result); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private static bool Overlaps(Collider first, Collider second)
        {
            if (first == null || second == null || !first.enabled || !second.enabled ||
                !first.gameObject.activeInHierarchy || !second.gameObject.activeInHierarchy)
                return false;

            return Physics.ComputePenetration(
                first, first.transform.position, first.transform.rotation,
                second, second.transform.position, second.transform.rotation,
                out _, out _);
        }
    }
}
