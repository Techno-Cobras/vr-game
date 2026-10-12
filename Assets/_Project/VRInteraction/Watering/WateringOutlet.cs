using System;
using UnityEngine;

namespace VrGame.VRInteraction.Watering
{
    public sealed class WateringOutlet : MonoBehaviour
    {
        [SerializeField]
        private WateringSource source;

        [SerializeField]
        private Collider outletCollider;

        public WateringSource Source => source;
        public Collider OutletCollider => outletCollider;
        public bool IsAvailable =>
            isActiveAndEnabled &&
            source != null &&
            outletCollider != null &&
            outletCollider.enabled &&
            outletCollider.gameObject.activeInHierarchy;

        public void Configure(WateringSource wateringSource, Collider collider)
        {
            source = wateringSource ?? throw new ArgumentNullException(nameof(wateringSource));
            outletCollider = collider ?? throw new ArgumentNullException(nameof(collider));
            if (outletCollider.gameObject != gameObject)
                throw new ArgumentException("Collider выхода воды должен находиться на том же объекте.", nameof(collider));
        }

        private void OnDisable()
        {
            if (source != null)
                source.NotifyOutletUnavailable(this);
        }

        private void OnDestroy()
        {
            if (source != null)
                source.NotifyOutletUnavailable(this);
        }
    }
}
