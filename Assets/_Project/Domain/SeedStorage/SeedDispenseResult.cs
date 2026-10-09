using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.SeedStorage
{
    public sealed class SeedDispenseResult
    {
        private SeedDispenseResult(
            SeedDispenseRejection rejection,
            PlantTypeId plantTypeId,
            ItemId seedItemId,
            int quantity,
            int availableQuantity,
            InventoryTransferResult transferResult,
            bool isReplay)
        {
            Rejection = rejection;
            PlantTypeId = plantTypeId;
            SeedItemId = seedItemId;
            Quantity = quantity;
            AvailableQuantity = availableQuantity;
            TransferResult = transferResult;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == SeedDispenseRejection.None;
        public SeedDispenseRejection Rejection { get; }
        public PlantTypeId PlantTypeId { get; }
        public ItemId SeedItemId { get; }
        public int Quantity { get; }
        public int AvailableQuantity { get; }
        public InventoryTransferResult TransferResult { get; }
        public bool IsReplay { get; }

        internal static SeedDispenseResult Success(
            PlantTypeId plantTypeId,
            ItemId seedItemId,
            int quantity,
            InventoryTransferResult transferResult) =>
            new SeedDispenseResult(
                SeedDispenseRejection.None,
                plantTypeId,
                seedItemId,
                quantity,
                transferResult.SourceChange.Changes[0].After,
                transferResult,
                false);

        internal static SeedDispenseResult Reject(
            SeedDispenseRejection rejection,
            PlantTypeId plantTypeId = default,
            ItemId seedItemId = default,
            int quantity = 0,
            int availableQuantity = 0,
            InventoryTransferResult transferResult = null) =>
            new SeedDispenseResult(
                rejection,
                plantTypeId,
                seedItemId,
                quantity,
                availableQuantity,
                transferResult,
                false);

        internal SeedDispenseResult AsReplay() =>
            new SeedDispenseResult(
                Rejection,
                PlantTypeId,
                SeedItemId,
                Quantity,
                AvailableQuantity,
                TransferResult,
                true);
    }
}
