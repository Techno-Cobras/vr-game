using System;
using UnityEngine;
using VrGame.Domain.Crafting;

namespace VrGame.Bootstrap
{
    public sealed class CraftingEventRelay : ICraftingEventSink
    {
        public event Action<CraftCompleted> Completed;

        public bool TryPublish(CraftCompleted craftCompleted)
        {
            var handlers = Completed?.GetInvocationList();
            if (handlers == null)
                return true;
            foreach (Action<CraftCompleted> handler in handlers)
            {
                try { handler(craftCompleted); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            return true;
        }
    }
}
