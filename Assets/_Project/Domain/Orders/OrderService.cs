using System;
using System.Collections.Generic;
using VrGame.Data.Orders;
using VrGame.Domain.Inventory;

namespace VrGame.Domain.Orders
{
    public sealed class OrderService
    {
        private readonly OrderTemplateCatalog templates;
        private readonly IOrderTemplateSelector selector;
        private readonly IOrderIdSource idSource;
        private readonly IOrderEventSink eventSink;
        private readonly List<OrderEntry> orderedOrders = new List<OrderEntry>();
        private readonly Dictionary<OrderId, OrderEntry> ordersById = new Dictionary<OrderId, OrderEntry>();
        private readonly Dictionary<CommandKey, CompletedCommand> completedCommands =
            new Dictionary<CommandKey, CompletedCommand>();
        private OrderId? activeOrderId;
        private bool isExecuting;

        public OrderService(
            OrderTemplateCatalog templates,
            IOrderTemplateSelector selector,
            IOrderEventSink eventSink)
            : this(templates, selector, new GuidOrderIdSource(), eventSink)
        {
        }

        public OrderService(
            OrderTemplateCatalog templates,
            IOrderTemplateSelector selector,
            IOrderIdSource idSource,
            IOrderEventSink eventSink)
        {
            this.templates = templates ?? throw new ArgumentNullException(nameof(templates));
            this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
            this.idSource = idSource ?? throw new ArgumentNullException(nameof(idSource));
            this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        }

        public long Version { get; private set; }
        public OrderId? ActiveOrderId => activeOrderId;

        public OrderServiceSnapshot GetSnapshot()
        {
            var snapshots = new List<OrderSnapshot>(orderedOrders.Count);
            foreach (var order in orderedOrders)
                snapshots.Add(order.GetSnapshot());
            return new OrderServiceSnapshot(Version, snapshots, activeOrderId);
        }

        public OrderMutationResult Generate(OrderContext context) =>
            Execute(OrderMutationKind.Generate, default, context);

        public OrderMutationResult Accept(OrderId orderId, OrderContext context) =>
            Execute(OrderMutationKind.Accept, orderId, context);

        public OrderMutationResult Complete(OrderId orderId, OrderContext context) =>
            Execute(OrderMutationKind.Complete, orderId, context);

        public OrderReadiness GetReadiness(InventorySnapshot inventory)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory));
            if (!activeOrderId.HasValue || !ordersById.TryGetValue(activeOrderId.Value, out var active))
                return default;

            var available = 0;
            foreach (var item in inventory.Items)
            {
                if (item.ItemId == active.Template.MedicineItemId)
                {
                    available = item.Quantity;
                    break;
                }
            }

            return new OrderReadiness(
                true,
                active.Id,
                active.Template.MedicineItemId,
                active.Template.Quantity,
                available);
        }

        private OrderMutationResult Execute(
            OrderMutationKind kind,
            OrderId targetOrderId,
            OrderContext context)
        {
            if (isExecuting)
                return OrderMutationResult.Reject(OrderRejection.ReentrantMutation, Target(kind, targetOrderId));
            if (!context.IsValid)
                return OrderMutationResult.Reject(OrderRejection.InvalidContext, Target(kind, targetOrderId));

            var key = new CommandKey(kind, context.CommandId);
            var payload = new CommandPayload(targetOrderId, context.Reason, context.CorrelationId);
            if (completedCommands.TryGetValue(key, out var completed))
            {
                if (!completed.Matches(payload))
                    return OrderMutationResult.Reject(OrderRejection.CommandConflict, Target(kind, targetOrderId));
                return completed.Result.AsReplay();
            }

            isExecuting = true;
            try
            {
                var result = ExecuteNew(kind, targetOrderId, context);
                completedCommands.Add(key, new CompletedCommand(payload, result));
                return result;
            }
            finally
            {
                isExecuting = false;
            }
        }

        private OrderMutationResult ExecuteNew(
            OrderMutationKind kind,
            OrderId targetOrderId,
            OrderContext context)
        {
            if (kind == OrderMutationKind.Generate)
                return GenerateNew(context);

            if (!ordersById.TryGetValue(targetOrderId, out var target))
                return OrderMutationResult.Reject(OrderRejection.UnknownOrder, targetOrderId);

            var rejection = ValidateTransition(kind, target);
            if (rejection != OrderRejection.None)
                return OrderMutationResult.Reject(rejection, targetOrderId);
            if (Version == long.MaxValue || target.Version == long.MaxValue)
                return OrderMutationResult.Reject(OrderRejection.ArithmeticOverflow, targetOrderId);

            var before = target.GetSnapshot();
            var activeBefore = activeOrderId;
            if (kind == OrderMutationKind.Accept)
            {
                target.State = OrderState.InProgress;
                activeOrderId = target.Id;
            }
            else
            {
                target.State = OrderState.Completed;
                activeOrderId = null;
            }
            target.Version++;
            Version++;

            return Publish(new OrderChanged(
                Version,
                kind,
                context,
                before,
                target.GetSnapshot(),
                activeBefore,
                activeOrderId));
        }

        private OrderMutationResult GenerateNew(OrderContext context)
        {
            if (Version == long.MaxValue)
                return OrderMutationResult.Reject(OrderRejection.ArithmeticOverflow);

            var generatedId = idSource.NextId();
            if (generatedId.Value == Guid.Empty || ordersById.ContainsKey(generatedId))
                return OrderMutationResult.Reject(OrderRejection.InvalidGeneratedOrderId);

            var selectedIndex = selector.SelectIndex(templates.Definitions.Count);
            if (selectedIndex < 0 || selectedIndex >= templates.Definitions.Count)
                return OrderMutationResult.Reject(OrderRejection.InvalidTemplateSelection);

            var entry = new OrderEntry(generatedId, templates.Definitions[selectedIndex]);
            ordersById.Add(entry.Id, entry);
            orderedOrders.Add(entry);
            Version++;
            return Publish(new OrderChanged(
                Version,
                OrderMutationKind.Generate,
                context,
                null,
                entry.GetSnapshot(),
                activeOrderId,
                activeOrderId));
        }

        private OrderRejection ValidateTransition(OrderMutationKind kind, OrderEntry target)
        {
            switch (kind)
            {
                case OrderMutationKind.Accept:
                    if (target.State == OrderState.Completed)
                        return OrderRejection.AlreadyCompleted;
                    if (target.State != OrderState.Available)
                        return OrderRejection.InvalidTransition;
                    return activeOrderId.HasValue
                        ? OrderRejection.ActiveOrderExists
                        : OrderRejection.None;
                case OrderMutationKind.Complete:
                    if (target.State == OrderState.Completed)
                        return OrderRejection.AlreadyCompleted;
                    return target.State == OrderState.InProgress && activeOrderId == target.Id
                        ? OrderRejection.None
                        : OrderRejection.InvalidTransition;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private OrderMutationResult Publish(OrderChanged change)
        {
            var eventPublished = false;
            try
            {
                eventPublished = eventSink.TryPublish(change);
            }
            catch (Exception)
            {
                // Lifecycle уже зафиксирован; событие остаётся доступным в результате команды.
            }
            return OrderMutationResult.Success(change, eventPublished);
        }

        private static OrderId? Target(OrderMutationKind kind, OrderId id) =>
            kind == OrderMutationKind.Generate ? (OrderId?)null : id;

        private sealed class OrderEntry
        {
            public OrderEntry(OrderId id, OrderTemplateDefinition template)
            {
                Id = id;
                Template = template;
                State = OrderState.Available;
                Version = 1;
            }

            public OrderId Id { get; }
            public OrderTemplateDefinition Template { get; }
            public OrderState State { get; set; }
            public long Version { get; set; }

            public OrderSnapshot GetSnapshot() => new OrderSnapshot(
                Id,
                Template.Id,
                Template.DisplayName,
                Template.MedicineItemId,
                Template.Quantity,
                Template.Reward,
                State,
                Version);
        }

        private readonly struct CommandKey : IEquatable<CommandKey>
        {
            public CommandKey(OrderMutationKind kind, Guid commandId)
            {
                Kind = kind;
                CommandId = commandId;
            }

            private OrderMutationKind Kind { get; }
            private Guid CommandId { get; }
            public bool Equals(CommandKey other) => Kind == other.Kind && CommandId == other.CommandId;
            public override bool Equals(object obj) => obj is CommandKey other && Equals(other);
            public override int GetHashCode() => ((int)Kind * 397) ^ CommandId.GetHashCode();
        }

        private readonly struct CommandPayload : IEquatable<CommandPayload>
        {
            public CommandPayload(OrderId targetOrderId, string reason, Guid correlationId)
            {
                TargetOrderId = targetOrderId;
                Reason = reason;
                CorrelationId = correlationId;
            }

            private OrderId TargetOrderId { get; }
            private string Reason { get; }
            private Guid CorrelationId { get; }
            public bool Equals(CommandPayload other) =>
                TargetOrderId == other.TargetOrderId &&
                string.Equals(Reason, other.Reason, StringComparison.Ordinal) &&
                CorrelationId == other.CorrelationId;
        }

        private sealed class CompletedCommand
        {
            private readonly CommandPayload payload;
            public CompletedCommand(CommandPayload payload, OrderMutationResult result)
            {
                this.payload = payload;
                Result = result;
            }
            public OrderMutationResult Result { get; }
            public bool Matches(CommandPayload candidate) => payload.Equals(candidate);
        }

        private sealed class GuidOrderIdSource : IOrderIdSource
        {
            public OrderId NextId() => new OrderId(Guid.NewGuid());
        }
    }
}
