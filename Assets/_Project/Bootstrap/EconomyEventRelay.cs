using System;
using UnityEngine;
using VrGame.Domain.Economy;
using VrGame.Presentation.Shop;

namespace VrGame.Bootstrap
{
    public sealed class EconomyEventRelay : IEconomyEventSink, IEconomyChangeSource
    {
        public event Action<BalanceChanged> Changed;
        public bool TryPublish(BalanceChanged change)
        {
            var success = true;
            if (Changed != null)
                foreach (Action<BalanceChanged> handler in Changed.GetInvocationList())
                    try
                    {
                        handler(change);
                    }
                    catch (Exception exception)
                    {
                        success = false;
                        Debug.LogException(exception);
                    }

            return success;
        }
    }
}
