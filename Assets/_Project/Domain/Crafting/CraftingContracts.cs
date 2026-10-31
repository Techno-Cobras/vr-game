using System;
using VrGame.Data.Crafting;
using VrGame.Data.Items;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Crafting
{
    public enum CraftRejection
    {
        None = 0,
        InvalidContext,
        UnknownRecipe,
        MissingIngredients,
        OutputRejected,
        InventoryRejected,
        ReentrantCommand,
        CommandConflict
    }

    public enum CraftOutputFailure
    {
        None = 0,
        UnknownItem,
        InvalidQuantity,
        ArithmeticOverflow
    }

    public readonly struct CraftContext
    {
        public CraftContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина crafting обязательна.", nameof(reason));
            if (commandId == Guid.Empty)
                throw new ArgumentException("Command ID не может быть пустым.", nameof(commandId));
            if (correlationId == Guid.Empty)
                throw new ArgumentException("Correlation ID не может быть пустым.", nameof(correlationId));
            Reason = reason;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        internal bool IsValid =>
            !string.IsNullOrWhiteSpace(Reason) && CommandId != Guid.Empty && CorrelationId != Guid.Empty;
    }

    public interface ICraftingEventSink
    {
        bool TryPublish(CraftCompleted craftCompleted);
    }

    public sealed class CraftMissingIngredient
    {
        public CraftMissingIngredient(ItemId itemId, int requiredQuantity, int availableQuantity)
        {
            ItemId = itemId;
            RequiredQuantity = requiredQuantity;
            AvailableQuantity = availableQuantity;
        }

        public ItemId ItemId { get; }
        public int RequiredQuantity { get; }
        public int AvailableQuantity { get; }
        public int MissingQuantity => RequiredQuantity - AvailableQuantity;
    }
}
