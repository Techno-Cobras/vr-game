using System;
using UnityEngine;
using VrGame.Domain.Economy;

namespace VrGame.Bootstrap
{
    public sealed class EconomyEventRelay : IEconomyEventSink
    {
        public event Action<BalanceChanged> Changed;

        public bool TryPublish(BalanceChanged balanceChanged)
        {
            var handlers = Changed?.GetInvocationList();
            if (handlers == null)
                return true;
            foreach (Action<BalanceChanged> handler in handlers)
            {
                try { handler(balanceChanged); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            return true;
        }
    }
}
