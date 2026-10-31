using System;
using UnityEngine;
using VrGame.Domain.Inventory;
using VrGame.Presentation.Crafting;

namespace VrGame.Bootstrap
{
    public sealed class InventoryEventRelay : IInventoryEventSink, IInventoryChangeSource
    {
        public event Action<InventoryChanged> Changed;

        public bool TryPublish(InventoryChanged inventoryChanged)
        {
            var handlers = Changed?.GetInvocationList();
            if (handlers == null)
                return true;
            foreach (Action<InventoryChanged> handler in handlers)
            {
                try { handler(inventoryChanged); }
                catch (Exception exception) { Debug.LogException(exception); }
            }
            return true;
        }
    }
}
