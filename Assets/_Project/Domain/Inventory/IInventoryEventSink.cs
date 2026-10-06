namespace VrGame.Domain.Inventory
{
    public interface IInventoryEventSink
    {
        // Координатор транзакции может буферизовать событие до общего commit.
        // Реализация не должна синхронно запускать новую mutation этого инвентаря.
        bool TryPublish(InventoryChanged inventoryChanged);
    }
}
