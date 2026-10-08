using UnityEngine;
using VrGame.Data.Plants;

namespace VrGame.DataAssets.Plants
{
    [CreateAssetMenu(menuName = "VR Game/Plants/Representation", fileName = "PlantRepresentation")]
    public sealed class PlantRepresentationAsset : ScriptableObject
    {
        [SerializeField]
        private string key;

        [SerializeField]
        private GameObject prefab;

        public GameObject Prefab => prefab;

        public PlantRepresentationKey GetKey()
        {
            if (prefab == null)
                throw new PlantDataValidationException(
                    PlantDataValidationError.MissingRepresentation,
                    $"У plant representation '{key}' отсутствует prefab.");
            return new PlantRepresentationKey(key);
        }
    }
}
