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
        private IndicatorKey waterKey;
        private IndicatorKey readyKey;

        public event Action<IndicatorProjection> ProjectionChanged;

        public void Configure(PlantingSlot plantingSlot, IndicatorTarget indicatorTarget = null)
        {
            if (indicatorTarget != null)
                target = indicatorTarget;
            slot = plantingSlot ?? throw new ArgumentNullException(nameof(plantingSlot));
            if (target == null)
                throw new InvalidOperationException("Цель индикатора полива не настроена.");
            waterKey = new IndicatorKey($"planting-slot:{slot.Id.Value:D}:water");
            readyKey = new IndicatorKey($"planting-slot:{slot.Id.Value:D}:ready");
            PublishCurrent();
        }

        public bool TryPublish(PlantingSlotChanged plantingSlotChanged)
        {
            if (plantingSlotChanged == null || slot == null || plantingSlotChanged.After.SlotId != slot.Id)
                return false;

            Publish(CreateWaterProjection(plantingSlotChanged.After));
            if ((plantingSlotChanged.Before.State == PlantingSlotState.ReadyToHarvest) !=
                (plantingSlotChanged.After.State == PlantingSlotState.ReadyToHarvest))
                Publish(CreateReadyProjection(plantingSlotChanged.After));
            return true;
        }

        public IReadOnlyList<IndicatorProjection> GetSnapshot()
        {
            if (slot == null)
                return Array.Empty<IndicatorProjection>();
            var snapshot = slot.GetSnapshot();
            return new[] { CreateWaterProjection(snapshot), CreateReadyProjection(snapshot) };
        }

        private void OnEnable() => PublishCurrent();

        private void OnDisable() => PublishHidden();

        private void OnDestroy() => PublishHidden();

        private IndicatorProjection CreateWaterProjection(PlantingSlotSnapshot snapshot) =>
            new IndicatorProjection(
                waterKey,
                IndicatorVariant.Water,
                target,
                isActiveAndEnabled && snapshot.State == PlantingSlotState.NeedsWater);

        private IndicatorProjection CreateReadyProjection(PlantingSlotSnapshot snapshot) =>
            new IndicatorProjection(
                readyKey,
                IndicatorVariant.ReadyToHarvest,
                target,
                isActiveAndEnabled && snapshot.State == PlantingSlotState.ReadyToHarvest);

        private void PublishCurrent()
        {
            if (slot != null)
            {
                var snapshot = slot.GetSnapshot();
                Publish(CreateWaterProjection(snapshot));
                Publish(CreateReadyProjection(snapshot));
            }
        }

        private void PublishHidden()
        {
            if (slot != null)
            {
                Publish(new IndicatorProjection(waterKey, IndicatorVariant.Water, target, false));
                Publish(new IndicatorProjection(readyKey, IndicatorVariant.ReadyToHarvest, target, false));
            }
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
