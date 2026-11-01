using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Data.Orders
{
    public sealed class OrderTemplateCatalog
    {
        private readonly Dictionary<OrderTemplateId, OrderTemplateDefinition> definitionsById;

        public OrderTemplateCatalog(ItemCatalog itemCatalog, IEnumerable<OrderTemplateDefinition> definitions)
        {
            if (itemCatalog == null)
                throw new OrderDataValidationException(
                    OrderDataValidationError.MissingItemCatalog,
                    "Item catalog для шаблонов заказов отсутствует.");
            if (definitions == null)
                throw new OrderDataValidationException(
                    OrderDataValidationError.MissingDefinition,
                    "Коллекция order template definitions отсутствует.");

            definitionsById = new Dictionary<OrderTemplateId, OrderTemplateDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new OrderDataValidationException(
                        OrderDataValidationError.MissingDefinition,
                        "Каталог содержит пустую order template definition.");
                if (!definitionsById.TryAdd(definition.Id, definition))
                    throw new OrderDataValidationException(
                        OrderDataValidationError.DuplicateTemplateId,
                        $"Order template ID '{definition.Id}' встречается больше одного раза.");
                if (!itemCatalog.TryGet(definition.MedicineItemId, out var medicine))
                    throw new OrderDataValidationException(
                        OrderDataValidationError.UnknownMedicineItem,
                        $"Шаблон '{definition.Id}' содержит неизвестное лекарство '{definition.MedicineItemId}'.");
                if (medicine.Category != ItemCategory.Medicine)
                    throw new OrderDataValidationException(
                        OrderDataValidationError.InvalidMedicineCategory,
                        $"Предмет '{medicine.Id}' в шаблоне '{definition.Id}' должен иметь категорию Medicine.");
            }

            if (definitionsById.Count == 0)
                throw new OrderDataValidationException(
                    OrderDataValidationError.MissingDefinition,
                    "Каталог шаблонов заказов не может быть пустым.");

            Definitions = new ReadOnlyCollection<OrderTemplateDefinition>(
                definitionsById.Values.OrderBy(value => value.Id).ToList());
        }

        public IReadOnlyList<OrderTemplateDefinition> Definitions { get; }
        public bool TryGet(OrderTemplateId id, out OrderTemplateDefinition definition) =>
            definitionsById.TryGetValue(id, out definition);

        public OrderTemplateDefinition GetRequired(OrderTemplateId id)
        {
            if (TryGet(id, out var definition))
                return definition;
            throw new KeyNotFoundException($"Order template ID '{id}' отсутствует в каталоге.");
        }
    }
}
