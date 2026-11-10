using System;
using System.Collections.Generic;
using VrGame.Data.Shop;
using VrGame.Domain.Delivery;
using VrGame.Domain.Economy;

namespace VrGame.Domain.Shop
{
    public enum PurchaseRejection
    {
        None,
        InvalidContext,
        UnknownProduct,
        EconomyRejected,
        ReentrantCommand,
        CommandConflict
    }

    public sealed class PurchaseResult
    {
        internal PurchaseResult(PurchaseRejection rejection, EconomyRejection economyRejection = EconomyRejection.None, PendingDeliveryEntry entry = null, bool replay = false)
        {
            Rejection = rejection;
            EconomyRejection = economyRejection;
            Entry = entry;
            IsReplay = replay;
        }

        public bool Succeeded => Rejection == PurchaseRejection.None;
        public PurchaseRejection Rejection { get; }
        public EconomyRejection EconomyRejection { get; }
        public PendingDeliveryEntry Entry { get; }
        public bool IsReplay { get; }
        public bool BalanceEventPublished { get; internal set; }
        public bool DeliveryEventPublished { get; internal set; }

        internal PurchaseResult AsReplay() => new PurchaseResult(Rejection, EconomyRejection, Entry, true)
        {
            BalanceEventPublished = BalanceEventPublished,
            DeliveryEventPublished = DeliveryEventPublished
        };
    }

    public sealed class PurchaseService
    {
        private readonly ShopCatalog catalog;
        private readonly EconomyService economy;
        private readonly PendingDeliveryLedger deliveries;
        private readonly Dictionary<Guid, CompletedPurchase> completed = new Dictionary<Guid, CompletedPurchase>();
        private bool executing;
        public PurchaseService(ShopCatalog catalog, EconomyService economy, PendingDeliveryLedger deliveries)
        {
            this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            this.economy = economy ?? throw new ArgumentNullException(nameof(economy));
            this.deliveries = deliveries ?? throw new ArgumentNullException(nameof(deliveries));
        }

        public PurchaseResult Purchase(ProductId productId, EconomyChangeContext context)
        {
            if (executing)
                return new PurchaseResult(PurchaseRejection.ReentrantCommand);
            if (string.IsNullOrWhiteSpace(context.Reason) || context.CommandId == Guid.Empty || context.CorrelationId == Guid.Empty)
                return new PurchaseResult(PurchaseRejection.InvalidContext);
            if (completed.TryGetValue(context.CommandId, out var previous))
                return previous.Matches(productId, context) ? previous.Result.AsReplay() : new PurchaseResult(PurchaseRejection.CommandConflict);
            executing = true;
            try
            {
                if (!catalog.TryGet(productId, out var offer))
                    return Remember(productId, context, new PurchaseResult(PurchaseRejection.UnknownProduct));
                if (!economy.TryAcquireCoordinatedMutation())
                    return new PurchaseResult(PurchaseRejection.ReentrantCommand);
                try
                {
                    if (!deliveries.TryAcquire())
                        return new PurchaseResult(PurchaseRejection.ReentrantCommand);
                    try
                    {
                        var rejection = economy.TryPrepareCoordinatedSpend(
                            offer.Price,
                            context,
                            out var preparedSpend);
                        if (rejection != EconomyRejection.None)
                            return Remember(productId, context, new PurchaseResult(PurchaseRejection.EconomyRejected, rejection));
                        var entry = deliveries.Prepare(offer, context.CommandId);
                        var balanceChange = economy.CommitPreparedSpend(preparedSpend);
                        deliveries.Commit(entry);
                        var result = Remember(productId, context, new PurchaseResult(PurchaseRejection.None, entry: entry));
                        result.BalanceEventPublished = economy.PublishPrepared(balanceChange);
                        economy.CompletePreparedCommand(
                            preparedSpend,
                            balanceChange,
                            result.BalanceEventPublished);
                        result.DeliveryEventPublished = deliveries.Publish(entry);
                        return result;
                    }
                    finally
                    {
                        deliveries.Release();
                    }
                }
                finally
                {
                    economy.ReleaseCoordinatedMutation();
                }
            }
            finally
            {
                executing = false;
            }
        }

        private PurchaseResult Remember(ProductId productId, EconomyChangeContext context, PurchaseResult result)
        {
            completed.Add(context.CommandId, new CompletedPurchase(productId, context, result));
            return result;
        }

        private sealed class CompletedPurchase
        {
            private readonly ProductId productId;
            private readonly EconomyChangeContext context;
            public CompletedPurchase(ProductId productId, EconomyChangeContext context, PurchaseResult result)
            {
                this.productId = productId;
                this.context = context;
                Result = result;
            }

            public PurchaseResult Result { get; }

            public bool Matches(ProductId id, EconomyChangeContext candidate) => productId.Equals(id) && context.Reason == candidate.Reason && context.CorrelationId == candidate.CorrelationId;
        }
    }
}
