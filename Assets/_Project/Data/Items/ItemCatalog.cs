using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VrGame.Data.Items
{
    public sealed class ItemCatalog
    {
        private readonly Dictionary<ItemId, ItemDefinition> definitionsById;

        public ItemCatalog(IEnumerable<ItemDefinition> definitions)
        {
            if (definitions == null)
                throw new ItemDataValidationException(
                    ItemDataValidationError.MissingDefinition,
                    "Коллекция item definitions отсутствует.");

            definitionsById = new Dictionary<ItemId, ItemDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new ItemDataValidationException(
                        ItemDataValidationError.MissingDefinition,
                        "Каталог содержит пустую item definition.");
                if (!definitionsById.TryAdd(definition.Id, definition))
                    throw new ItemDataValidationException(
                        ItemDataValidationError.DuplicateItemId,
                        $"Item ID '{definition.Id}' встречается в каталоге больше одного раза.");
            }

            Definitions = new ReadOnlyCollection<ItemDefinition>(
                definitionsById.Values.OrderBy(definition => definition.Id).ToList());
        }

        public IReadOnlyList<ItemDefinition> Definitions { get; }

        public bool TryGet(ItemId id, out ItemDefinition definition) => definitionsById.TryGetValue(id, out definition);

        public ItemDefinition GetRequired(ItemId id)
        {
            if (TryGet(id, out var definition))
                return definition;

            throw new KeyNotFoundException($"Item ID '{id}' отсутствует в каталоге.");
        }
    }
}
