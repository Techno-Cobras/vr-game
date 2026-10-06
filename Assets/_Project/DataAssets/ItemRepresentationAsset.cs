using UnityEngine;
using VrGame.Data.Items;

namespace VrGame.DataAssets
{
    [CreateAssetMenu(menuName = "VR Game/Items/Representation", fileName = "ItemRepresentation")]
    public sealed class ItemRepresentationAsset : ScriptableObject
    {
        [SerializeField]
        private string key;

        [SerializeField]
        private GameObject prefab;

        public GameObject Prefab => prefab;

        public ItemRepresentationKey GetKey()
        {
            if (prefab == null)
                throw new ItemDataValidationException(
                    ItemDataValidationError.MissingRepresentation,
                    $"У representation '{key}' отсутствует prefab.");

            return new ItemRepresentationKey(key);
        }
    }
}
