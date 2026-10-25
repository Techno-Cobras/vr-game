using System;
using VrGame.Domain.Fertilizers;

namespace VrGame.VRInteraction.Fertilizers
{
    public sealed class FertilizerApplicationAdapter
    {
        private readonly FertilizerApplicationCoordinator coordinator;
        public FertilizerApplicationAdapter(FertilizerApplicationCoordinator value) => coordinator = value ?? throw new ArgumentNullException(nameof(value));
        public FertilizerApplicationResult Apply(FertilizerItemInstance item, FertilizerCommandContext context)
        {
            if (item == null || !item.IsInitialized || item.IsConsumed) return FertilizerApplicationResult.InvalidItem();
            var result = coordinator.Apply(item.Grade, item.ItemId, context);
            if (result.Succeeded && !result.IsReplay) item.Consume();
            return result;
        }
    }
}
