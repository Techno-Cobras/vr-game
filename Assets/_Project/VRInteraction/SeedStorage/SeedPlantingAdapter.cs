using System;
using VrGame.Domain.SeedStorage;

namespace VrGame.VRInteraction.SeedStorage
{
    public sealed class SeedPlantingAdapter
    {
        private readonly PlantSeedCoordinator coordinator;

        public SeedPlantingAdapter(PlantSeedCoordinator coordinator)
        {
            this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
        }

        public PlantSeedResult Plant(
            SeedItemInstance seed,
            PlantSeedContext context)
        {
            if (seed == null || !seed.IsInitialized || seed.IsConsumed)
                return PlantSeedResult.InvalidSeed();

            var result = coordinator.Plant(seed.PlantTypeId, seed.SeedItemId, context);
            if (result.Succeeded && !result.IsReplay)
                seed.Consume();
            return result;
        }
    }
}
