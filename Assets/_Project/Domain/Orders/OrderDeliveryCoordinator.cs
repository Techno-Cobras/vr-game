using System;
using System.Collections.Generic;
using VrGame.Data.Items;
using VrGame.Domain.Economy;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Orders
{
    public enum OrderDeliveryRejection
    {
        None = 0,
        InvalidContext,
        NoActiveOrder,
        WrongMedicine,
        WrongQuantity,
        InventoryRejected,
        OrderRejected,
        EconomyRejected,
        ReentrantCommand,
        CommandConflict
    }

    public readonly struct OrderDeliveryContext
    {
        public OrderDeliveryContext(string reason, Guid commandId, Guid correlationId)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Причина доставки обязательна.", nameof(reason));
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
            !string.IsNullOrWhiteSpace(Reason) &&
            CommandId != Guid.Empty &&
            CorrelationId != Guid.Empty;
    }

    public sealed class OrderDeliveryResult
    {
        private OrderDeliveryResult(
            OrderDeliveryRejection rejection,
            InventoryRejection inventoryRejection,
            OrderRejection orderRejection,
            EconomyRejection economyRejection,
            InventoryChanged inventoryChange,
            OrderChanged orderChange,
            BalanceChanged balanceChange,
            bool inventoryEventPublished,
            bool orderEventPublished,
            bool economyEventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            InventoryRejection = inventoryRejection;
            OrderRejection = orderRejection;
            EconomyRejection = economyRejection;
            InventoryChange = inventoryChange;
            OrderChange = orderChange;
            BalanceChange = balanceChange;
            InventoryEventPublished = inventoryEventPublished;
            OrderEventPublished = orderEventPublished;
            EconomyEventPublished = economyEventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == OrderDeliveryRejection.None;
        public OrderDeliveryRejection Rejection { get; }
        public InventoryRejection InventoryRejection { get; }
        public OrderRejection OrderRejection { get; }
        public EconomyRejection EconomyRejection { get; }
        public InventoryChanged InventoryChange { get; }
        public OrderChanged OrderChange { get; }
        public BalanceChanged BalanceChange { get; }
        public bool InventoryEventPublished { get; }
        public bool OrderEventPublished { get; }
        public bool EconomyEventPublished { get; }
        public bool IsReplay { get; }

        internal static OrderDeliveryResult Success(
            InventoryChanged inventoryChange,
            OrderChanged orderChange,
            BalanceChanged balanceChange,
            bool inventoryEventPublished,
            bool orderEventPublished,
            bool economyEventPublished) =>
            new OrderDeliveryResult(
                OrderDeliveryRejection.None,
                InventoryRejection.None,
                OrderRejection.None,
                EconomyRejection.None,
                inventoryChange,
                orderChange,
                balanceChange,
                inventoryEventPublished,
                orderEventPublished,
                economyEventPublished,
                false);

        internal static OrderDeliveryResult Reject(
            OrderDeliveryRejection rejection,
            InventoryRejection inventoryRejection = InventoryRejection.None,
            OrderRejection orderRejection = OrderRejection.None,
            EconomyRejection economyRejection = EconomyRejection.None) =>
            new OrderDeliveryResult(
                rejection,
                inventoryRejection,
                orderRejection,
                economyRejection,
                null,
                null,
                null,
                false,
                false,
                false,
                false);

        internal OrderDeliveryResult AsReplay() => new OrderDeliveryResult(
            Rejection,
            InventoryRejection,
            OrderRejection,
            EconomyRejection,
            InventoryChange,
            OrderChange,
            BalanceChange,
            InventoryEventPublished,
            OrderEventPublished,
            EconomyEventPublished,
            true);
    }

    public sealed class OrderDeliveryCoordinator
    {
        private readonly Inventory.Inventory inventory;
        private readonly OrderService orders;
        private readonly EconomyService economy;
        private readonly Dictionary<Guid, CompletedCommand> completedCommands =
            new Dictionary<Guid, CompletedCommand>();
        private bool isExecuting;

        public OrderDeliveryCoordinator(
            Inventory.Inventory inventory,
            OrderService orders,
            EconomyService economy)
        {
            this.inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
            this.orders = orders ?? throw new ArgumentNullException(nameof(orders));
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
        }

        public OrderDeliveryResult Deliver(
            ItemId submittedMedicine,
            int submittedQuantity,
            OrderDeliveryContext context)
        {
            if (isExecuting)
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.ReentrantCommand);
            if (!context.IsValid)
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.InvalidContext);

            var payload = new CommandPayload(
                submittedMedicine,
                submittedQuantity,
                context.Reason,
                context.CorrelationId);
            if (completedCommands.TryGetValue(context.CommandId, out var completed))
            {
                if (!completed.Matches(payload))
                    return OrderDeliveryResult.Reject(OrderDeliveryRejection.CommandConflict);
                return completed.Result.AsReplay();
            }

            isExecuting = true;
            try
            {
                var result = Execute(submittedMedicine, submittedQuantity, context);
                completedCommands.Add(context.CommandId, new CompletedCommand(payload, result));
                return result;
            }
            finally
            {
                isExecuting = false;
            }
        }

        private OrderDeliveryResult Execute(
            ItemId submittedMedicine,
            int submittedQuantity,
            OrderDeliveryContext context)
        {
            var snapshot = orders.GetSnapshot();
            if (!snapshot.ActiveOrderId.HasValue)
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.NoActiveOrder);

            OrderSnapshot active = null;
            foreach (var order in snapshot.Orders)
            {
                if (order.OrderId == snapshot.ActiveOrderId.Value)
                {
                    active = order;
                    break;
                }
            }
            if (active == null)
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.NoActiveOrder);
            if (submittedMedicine != active.MedicineItemId)
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.WrongMedicine);
            if (submittedQuantity != active.Quantity)
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.WrongQuantity);

            if (!inventory.TryAcquireCoordinatedMutation())
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.ReentrantCommand);
            if (!orders.TryAcquireCoordinatedMutation())
            {
                inventory.ReleaseCoordinatedMutation();
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.ReentrantCommand);
            }
            if (!economy.TryAcquireCoordinatedMutation())
            {
                orders.ReleaseCoordinatedMutation();
                inventory.ReleaseCoordinatedMutation();
                return OrderDeliveryResult.Reject(OrderDeliveryRejection.ReentrantCommand);
            }

            try
            {
                var inventoryContext = new InventoryChangeContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var inventoryRejection = inventory.TryPrepareCoordinatedRemove(
                    active.MedicineItemId,
                    active.Quantity,
                    inventoryContext,
                    out var preparedInventory);
                if (inventoryRejection != InventoryRejection.None)
                    return OrderDeliveryResult.Reject(
                        OrderDeliveryRejection.InventoryRejected,
                        inventoryRejection: inventoryRejection);

                var orderContext = new OrderContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var orderRejection = orders.TryPrepareCoordinatedCompletion(
                    active.OrderId,
                    orderContext,
                    out var preparedOrder);
                if (orderRejection != OrderRejection.None)
                    return OrderDeliveryResult.Reject(
                        OrderDeliveryRejection.OrderRejected,
                        orderRejection: orderRejection);

                var economyContext = new EconomyChangeContext(
                    context.Reason,
                    context.CommandId,
                    context.CorrelationId);
                var economyRejection = economy.TryPrepareCoordinatedCredit(
                    active.Reward,
                    economyContext,
                    out var preparedEconomy);
                if (economyRejection != EconomyRejection.None)
                    return OrderDeliveryResult.Reject(
                        OrderDeliveryRejection.EconomyRejected,
                        economyRejection: economyRejection);

                var inventoryChange = inventory.CommitPreparedRemove(preparedInventory);
                var orderChange = orders.CommitPreparedCompletion(preparedOrder);
                var balanceChange = economy.CommitPreparedCredit(preparedEconomy);

                var inventoryPublished = inventory.PublishPrepared(inventoryChange);
                var economyPublished = economy.PublishPrepared(balanceChange);
                var orderPublished = orders.PublishPrepared(orderChange);
                orders.CompletePreparedCommand(preparedOrder, orderChange, orderPublished);
                economy.CompletePreparedCommand(preparedEconomy, balanceChange, economyPublished);
                return OrderDeliveryResult.Success(
                    inventoryChange,
                    orderChange,
                    balanceChange,
                    inventoryPublished,
                    orderPublished,
                    economyPublished);
            }
            finally
            {
                economy.ReleaseCoordinatedMutation();
                orders.ReleaseCoordinatedMutation();
                inventory.ReleaseCoordinatedMutation();
            }
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(
                ItemId medicine,
                int quantity,
                string reason,
                Guid correlationId)
            {
                Medicine = medicine;
                Quantity = quantity;
                Reason = reason;
                CorrelationId = correlationId;
            }

            private ItemId Medicine { get; }
            private int Quantity { get; }
            private string Reason { get; }
            private Guid CorrelationId { get; }
            public bool Equals(CommandPayload other) =>
                Medicine == other.Medicine &&
                Quantity == other.Quantity &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) &&
                CorrelationId == other.CorrelationId;
        }

        private sealed class CompletedCommand
        {
            private readonly CommandPayload payload;
            public CompletedCommand(CommandPayload payload, OrderDeliveryResult result)
            {
                this.payload = payload;
                Result = result;
            }
            public OrderDeliveryResult Result { get; }
            public bool Matches(CommandPayload candidate) => payload.Equals(candidate);
        }
    }
}
