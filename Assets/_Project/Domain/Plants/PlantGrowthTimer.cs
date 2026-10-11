using System;
using VrGame.Data.Plants;

namespace VrGame.Domain.Plants
{
    public sealed class PlantGrowthTimer
    {
        private readonly PlantingSlot slot;
        private readonly PlantCatalog catalog;
        private readonly IPlantGrowthTimeSource timeSource;
        private bool isRunning;
        private double baselineSeconds;

        public PlantGrowthTimer(
            PlantingSlot slot,
            PlantCatalog catalog,
            IPlantGrowthTimeSource timeSource)
        {
            this.slot = slot ?? throw new ArgumentNullException(nameof(slot));
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.timeSource = timeSource ?? throw new ArgumentNullException(nameof(timeSource));
        }

        public bool IsRunning => isRunning;

        public void Resume()
        {
            if (isRunning)
                return;

            baselineSeconds = ReadTime();
            isRunning = true;
        }

        public void Pause()
        {
            if (!isRunning)
                return;

            Tick();
            isRunning = false;
        }

        public PlantingSlotMutationResult Tick()
        {
            if (!isRunning)
                return null;

            var now = ReadTime();
            var delta = now - baselineSeconds;
            baselineSeconds = now;
            if (delta <= 0d)
                return null;

            var snapshot = slot.GetSnapshot();
            if (snapshot.State != PlantingSlotState.Growing || !snapshot.PlantTypeId.HasValue)
                return null;

            var definition = catalog.GetRequired(snapshot.PlantTypeId.Value);
            var duration = definition.GrowthDuration.Seconds;
            var elapsedDelta = delta * snapshot.GrowthModifier.EffectiveMultiplier;
            var elapsedTarget = Math.Min(duration, snapshot.ElapsedGrowthSeconds + elapsedDelta);
            var normalizedTarget = elapsedTarget >= duration ? 1f : (float)(elapsedTarget / duration);
            if (normalizedTarget <= snapshot.NormalizedProgress)
                return null;

            return slot.AdvanceGrowth(
                (float)elapsedTarget,
                snapshot.Version,
                new PlantingSlotChangeContext(
                    "ход таймера роста",
                    Guid.NewGuid(),
                    Guid.NewGuid()));
        }

        private double ReadTime()
        {
            var value = timeSource.TimeSeconds;
            if (double.IsNaN(value) || double.IsInfinity(value))
                throw new InvalidOperationException("Источник времени роста вернул некорректное значение.");
            return value;
        }
    }
}
