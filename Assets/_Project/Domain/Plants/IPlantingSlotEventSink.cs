namespace VrGame.Domain.Plants
{
    public interface IPlantingSlotEventSink
    {
        // Mutation уже зафиксирована до публикации. Sink не должен синхронно
        // запускать новую команду того же planting slot.
        bool TryPublish(PlantingSlotChanged plantingSlotChanged);
    }
}
