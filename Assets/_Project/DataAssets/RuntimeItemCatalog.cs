using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Items;

namespace VrGame.DataAssets
{
    public sealed class RuntimeItemCatalog
    {
        private readonly IReadOnlyDictionary<ItemRepresentationKey, GameObject> representations;

        internal RuntimeItemCatalog(
            ItemCatalog items,
            IReadOnlyDictionary<ItemRepresentationKey, GameObject> representations)
        {
            Items = items;
            this.representations = representations;
        }

        public ItemCatalog Items { get; }

        public bool TryGetRepresentation(ItemRepresentationKey key, out GameObject prefab) =>
            representations.TryGetValue(key, out prefab);

        public GameObject GetRequiredRepresentation(ItemRepresentationKey key)
        {
            if (TryGetRepresentation(key, out var prefab))
                return prefab;

            throw new KeyNotFoundException($"Representation key '{key}' отсутствует в каталоге.");
        }
    }
}
