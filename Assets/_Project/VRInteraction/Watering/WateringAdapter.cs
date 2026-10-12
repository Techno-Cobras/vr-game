using System;
using VrGame.Domain.Plants;

namespace VrGame.VRInteraction.Watering
{
    public sealed class WateringAdapter
    {
        private readonly PlantingSlot slot;

        public WateringAdapter(PlantingSlot plantingSlot)
        {
            slot = plantingSlot ?? throw new ArgumentNullException(nameof(plantingSlot));
        }

        public PlantingSlotMutationResult Water(Guid activationId)
        {
            if (activationId == Guid.Empty)
                throw new ArgumentException("Activation ID не может быть пустым.", nameof(activationId));

            return slot.Water(new PlantingSlotChangeContext(
                "полив растения физической лейкой",
                activationId,
                activationId));
        }
    }
}
