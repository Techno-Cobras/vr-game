using UnityEngine;
using VrGame.Data.Fertilizers;

namespace VrGame.DataAssets.Fertilizers
{
    [CreateAssetMenu(menuName = "VR Game/Fertilizers/Definition", fileName = "FertilizerDefinition")]
    public sealed class FertilizerDefinitionAsset : ScriptableObject
    {
        [SerializeField] private FertilizerGrade grade;
        [SerializeField] private ItemDefinitionAsset item;
        [SerializeField, Min(1.01f)] private float growthMultiplier = 1.25f;

        public FertilizerGrade Grade => grade;
        public ItemDefinitionAsset Item => item;

        public FertilizerDefinition ToDefinition(RuntimeItemCatalog items)
        {
            if (items == null)
                throw new System.ArgumentNullException(nameof(items));
            if (item == null || !items.Items.TryGet(item.GetId(), out var itemDefinition))
                throw new FertilizerDataValidationException(
                    FertilizerDataValidationError.MissingDefinition,
                    $"У fertilizer asset '{name}' отсутствует item из runtime catalog.");
            return new FertilizerDefinition(grade, itemDefinition, growthMultiplier);
        }
    }
}
