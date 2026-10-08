using UnityEngine;
using VrGame.Data.Items;

namespace VrGame.DataAssets
{
    [CreateAssetMenu(menuName = "VR Game/Items/Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinitionAsset : ScriptableObject
    {
        [SerializeField]
        private string id;

        [SerializeField]
        private string displayName;

        [SerializeField]
        private ItemCategory category;

        [SerializeField]
        private ItemRepresentationAsset representation;

        [SerializeField, Min(1)]
        private int maxStackSize = 1;

        [SerializeField]
        private bool allowsStackRepresentation;

        public ItemRepresentationAsset Representation => representation;

        public ItemId GetId() => new ItemId(id);

        public ItemDefinition ToDefinition()
        {
            if (representation == null)
                throw new ItemDataValidationException(
                    ItemDataValidationError.MissingRepresentation,
                    $"У item asset '{name}' отсутствует representation asset.");

            return new ItemDefinition(
                GetId(),
                displayName,
                category,
                representation.GetKey(),
                new ItemStackRules(maxStackSize, allowsStackRepresentation));
        }
    }
}
