using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Domain.Orders;
using VrGame.Presentation.Indicators;
using VrGame.Presentation.Orders;

namespace VrGame.Bootstrap
{
    public sealed class OrderDeliveryIndicatorProjectionSource : MonoBehaviour, IIndicatorProjectionSource
    {
        [SerializeField]
        private IndicatorTarget target;

        private static readonly IndicatorKey DeliveryKey = new IndicatorKey("order.active.delivery");
        private OrderService service;
        private IOrderChangeSource changes;
        private bool subscribed;

        public event Action<IndicatorProjection> ProjectionChanged;

        public void Configure(
            OrderService orderService,
            IOrderChangeSource changeSource,
            IndicatorTarget indicatorTarget = null)
        {
            Unsubscribe();
            service = orderService ?? throw new ArgumentNullException(nameof(orderService));
            changes = changeSource ?? throw new ArgumentNullException(nameof(changeSource));
            if (indicatorTarget != null)
                target = indicatorTarget;
            if (target == null)
                throw new InvalidOperationException("Цель delivery waypoint не настроена.");
            Subscribe();
            PublishCurrent();
        }

        public IReadOnlyList<IndicatorProjection> GetSnapshot()
        {
            if (service == null)
                return Array.Empty<IndicatorProjection>();
            return new[] { CreateProjection() };
        }

        private void OnEnable()
        {
            Subscribe();
            PublishCurrent();
        }

        private void OnDisable()
        {
            Publish(false);
            Unsubscribe();
        }

        private void OnDestroy()
        {
            Publish(false);
            Unsubscribe();
        }

        private void HandleOrderChanged(OrderChanged _) => PublishCurrent();

        private IndicatorProjection CreateProjection() => new IndicatorProjection(
            DeliveryKey,
            IndicatorVariant.Delivery,
            target,
            isActiveAndEnabled && service.GetSnapshot().ActiveOrderId.HasValue);

        private void PublishCurrent()
        {
            if (service != null)
                Publish(CreateProjection());
        }

        private void Publish(bool visible) => Publish(new IndicatorProjection(
            DeliveryKey,
            IndicatorVariant.Delivery,
            target,
            visible));

        private void Publish(IndicatorProjection projection)
        {
            var handlers = ProjectionChanged?.GetInvocationList();
            if (handlers == null)
                return;
            foreach (Action<IndicatorProjection> handler in handlers)
            {
                try { handler(projection); }
                catch (Exception exception) { Debug.LogException(exception, this); }
            }
        }

        private void Subscribe()
        {
            if (subscribed || !isActiveAndEnabled || changes == null)
                return;
            changes.Changed += HandleOrderChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
                return;
            if (changes != null)
                changes.Changed -= HandleOrderChanged;
            subscribed = false;
        }
    }
}
