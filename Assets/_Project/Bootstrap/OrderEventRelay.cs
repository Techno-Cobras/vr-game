using System;
using UnityEngine;
using VrGame.Domain.Orders;
using VrGame.Presentation.Orders;

namespace VrGame.Bootstrap
{
    public sealed class OrderEventRelay : IOrderEventSink, IOrderChangeSource
    {
        public event Action<OrderChanged> Changed;

        public bool TryPublish(OrderChanged orderChanged)
        {
            var handlers = Changed?.GetInvocationList();
            if (handlers == null)
                return true;
            foreach (Action<OrderChanged> handler in handlers)
            {
                try { handler(orderChanged); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            return true;
        }
    }
}
