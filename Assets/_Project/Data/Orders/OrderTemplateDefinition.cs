using VrGame.Data.Items;

namespace VrGame.Data.Orders
{
    public sealed class OrderTemplateDefinition
    {
        public OrderTemplateDefinition(
            OrderTemplateId id,
            string displayName,
            ItemId medicineItemId,
            int quantity,
            int reward)
        {
            if (string.IsNullOrEmpty(id.Value))
                throw new OrderDataValidationException(
                    OrderDataValidationError.InvalidTemplateId,
                    "У шаблона заказа отсутствует стабильный ID.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new OrderDataValidationException(
                    OrderDataValidationError.MissingDisplayName,
                    $"У шаблона заказа '{id}' отсутствует display name.");
            if (string.IsNullOrEmpty(medicineItemId.Value))
                throw new OrderDataValidationException(
                    OrderDataValidationError.InvalidMedicineItem,
                    $"У шаблона заказа '{id}' отсутствует medicine item.");
            if (quantity <= 0)
                throw new OrderDataValidationException(
                    OrderDataValidationError.InvalidQuantity,
                    $"Количество лекарства в шаблоне '{id}' должно быть положительным.");
            if (reward <= 0)
                throw new OrderDataValidationException(
                    OrderDataValidationError.InvalidReward,
                    $"Награда в шаблоне '{id}' должна быть положительной.");

            Id = id;
            DisplayName = displayName;
            MedicineItemId = medicineItemId;
            Quantity = quantity;
            Reward = reward;
        }

        public OrderTemplateId Id { get; }
        public string DisplayName { get; }
        public ItemId MedicineItemId { get; }
        public int Quantity { get; }
        public int Reward { get; }
    }
}
