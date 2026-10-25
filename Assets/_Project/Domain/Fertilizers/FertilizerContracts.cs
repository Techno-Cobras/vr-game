using System;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;

namespace VrGame.Domain.Fertilizers
{
    public enum FertilizerDispenseRejection
    {
        None = 0,
        InvalidContext,
        UnknownGrade,
        InsufficientStock,
        MaterializationFailed,
        InventoryRejected,
        CommandConflict,
        ReentrantCommand
    }

    public enum FertilizerApplicationRejection
    {
        None = 0,
        InvalidContext,
        UnknownGrade,
        ItemIdentityMismatch,
        ItemUnavailable,
        SlotRejected,
        CommandConflict,
        ReentrantCommand
    }

    public readonly struct FertilizerCommandContext
    {
        public FertilizerCommandContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Причина обязательна.", nameof(reason));
            if (commandId == Guid.Empty) throw new ArgumentException("Command ID не может быть пустым.", nameof(commandId));
            if (correlationId == Guid.Empty) throw new ArgumentException("Correlation ID не может быть пустым.", nameof(correlationId));
            Reason = reason;
            CommandId = commandId;
            CorrelationId = correlationId;
        }

        public string Reason { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        internal bool IsValid => !string.IsNullOrWhiteSpace(Reason) && CommandId != Guid.Empty && CorrelationId != Guid.Empty;
    }

    public readonly struct FertilizerMaterializationRequest
    {
        public FertilizerMaterializationRequest(FertilizerGrade grade, ItemId itemId)
        {
            Grade = grade;
            ItemId = itemId;
        }
        public FertilizerGrade Grade { get; }
        public ItemId ItemId { get; }
    }

    public interface IFertilizerMaterializer
    {
        bool TryStage(FertilizerMaterializationRequest request, out IStagedFertilizerItem item);
    }

    public interface IStagedFertilizerItem
    {
        bool TryRelease();
        bool TryCommit();
        void Rollback();
    }

    public interface IFertilizerContainerView
    {
        void Render(FertilizerContainerSnapshot snapshot, int selectedIndex, FertilizerDispenseResult lastResult);
    }

    public interface IFertilizerApplicationView
    {
        void RenderApplication(FertilizerApplicationResult result);
    }
}
