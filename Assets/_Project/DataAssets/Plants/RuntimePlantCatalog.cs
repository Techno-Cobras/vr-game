using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Plants;

namespace VrGame.DataAssets.Plants
{
    public sealed class RuntimePlantCatalog
    {
        private readonly IReadOnlyDictionary<PlantRepresentationKey, GameObject> representations;

        internal RuntimePlantCatalog(PlantCatalog plants, IReadOnlyDictionary<PlantRepresentationKey, GameObject> representations)
        {
            Plants = plants;
            this.representations = representations;
        }

        public PlantCatalog Plants { get; }
        public bool TryGetRepresentation(PlantRepresentationKey key, out GameObject prefab) => representations.TryGetValue(key, out prefab);

        public GameObject GetRequiredRepresentation(PlantRepresentationKey key)
        {
            if (TryGetRepresentation(key, out var prefab))
                return prefab;
            throw new KeyNotFoundException($"Plant representation key '{key}' отсутствует в каталоге.");
        }
    }
}
