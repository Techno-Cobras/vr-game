using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Domain.Fertilizers;

namespace VrGame.VRInteraction.Fertilizers
{
    [RequireComponent(typeof(Collider))]
    public sealed class FertilizerApplicationReceiver : MonoBehaviour
    {
        [SerializeField] private Collider triggerZone;
        private FertilizerApplicationAdapter adapter;
        private bool applicationPaused;
        private bool applicationFocused = true;

        public event Action<FertilizerApplicationResult> ApplicationCompleted;
        public bool CanReceive => isActiveAndEnabled && applicationFocused && !applicationPaused && adapter != null;

        public void Configure(Collider zone, FertilizerApplicationCoordinator coordinator)
        {
            triggerZone = zone != null ? zone : throw new ArgumentNullException(nameof(zone));
            if (!triggerZone.isTrigger) throw new ArgumentException("Зона применения удобрения должна быть trigger.", nameof(zone));
            adapter = new FertilizerApplicationAdapter(coordinator ?? throw new ArgumentNullException(nameof(coordinator)));
        }

        private void OnApplicationPause(bool paused) => applicationPaused = paused;
        private void OnApplicationFocus(bool focused) => applicationFocused = focused;
        private void OnTriggerEnter(Collider other) => TryApply(other);

        public FertilizerApplicationResult TryApply(Collider other)
        {
            if (!CanReceive || other == null) return null;
            var item = other.GetComponentInParent<FertilizerItemInstance>();
            var grab = item == null ? null : item.GetComponent<XRGrabInteractable>();
            if (item == null || grab == null || !grab.isSelected) return null;
            var result = adapter.Apply(item, new FertilizerCommandContext(
                "применение удерживаемого удобрения к растению", Guid.NewGuid(), Guid.NewGuid()));
            InvokeCompleted(result);
            return result;
        }

        private void InvokeCompleted(FertilizerApplicationResult result)
        {
            var handlers = ApplicationCompleted?.GetInvocationList();
            if (handlers == null)
                return;

            foreach (Action<FertilizerApplicationResult> handler in handlers)
            {
                try
                {
                    handler(result);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
            }
        }
    }
}
