using UnityEngine;
using VrGame.Data.Orders;

namespace VrGame.DataAssets.Orders
{
    [CreateAssetMenu(menuName = "VR Game/Orders/Template Definition", fileName = "OrderTemplateDefinition")]
    public sealed class OrderTemplateDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ItemDefinitionAsset medicineItem;
        [SerializeField, Min(1)] private int quantity = 1;
        [SerializeField, Min(1)] private int reward = 1;

        public OrderTemplateDefinition ToDefinition()
        {
            if (medicineItem == null)
                throw new OrderDataValidationException(
                    OrderDataValidationError.InvalidMedicineItem,
                    $"У order template asset '{name}' отсутствует medicine item asset.");

            return new OrderTemplateDefinition(
                new OrderTemplateId(id),
                displayName,
                medicineItem.GetId(),
                quantity,
                reward);
        }
    }
}
