using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Domain.Plants;
using VrGame.Presentation.Indicators;

namespace VrGame.Bootstrap
{
    public sealed class PlantingSlotIndicatorProjectionSource : MonoBehaviour,
        IPlantingSlotEventSink,
        IIndicatorProjectionSource
    {
        [SerializeField]
        private IndicatorTarget target;

        private PlantingSlot slot;
        private IndicatorKey key;

        public event Action<IndicatorProjection> ProjectionChanged;

        public void Configure(PlantingSlot plantingSlot, IndicatorTarget indicatorTarget = null)
        {
            if (indicatorTarget != null)
                target = indicatorTarget;
            slot = plantingSlot ?? throw new ArgumentNullException(nameof(plantingSlot));
            if (target == null)
                throw new InvalidOperationException("Цель индикатора полива не настроена.");
            key = new IndicatorKey($"planting-slot:{slot.Id.Value:D}:water");
            PublishCurrent();
        }

        public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
        {
            if (plantingSlotChanged == null || slot == null || plantingSlotChanged.After.SlotId != slot.Id)
                return false;

            Publish(CreateProjection(plantingSlotChanged.After));
            return true;
        }

        public IReadOnlyList<IndicatorProjection> GetSnapshot()
        {
            if (slot == null)
                return Array.Empty<IndicatorProjection>();
            return new[] { CreateProjection(slot.GetSnapshot()) };
        }

        private void OnEnable() => PublishCurrent();

        private void OnDisable() => PublishHidden();

        private void OnDestroy() => PublishHidden();

        private IndicatorProjection CreateProjection(PlantingSlotSnapshot snapshot) =>
            new IndicatorProjection(
                key,
                IndicatorVariant.Water,
                target,
                isActiveAndEnabled && snapshot.State == PlantingSlotState.NeedsWater);

        private void PublishCurrent()
        {
            if (slot != null)
                Publish(CreateProjection(slot.GetSnapshot()));
        }

        private void PublishHidden()
        {
            if (slot != null)
                Publish(new IndicatorProjection(key, IndicatorVariant.Water, target, false));
        }

        private void Publish(IndicatorProjection projection)
        {
            var handlers = ProjectionChanged?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<IndicatorProjection> handler in handlers)
            {
                try { handler(projection); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }
    }
}
