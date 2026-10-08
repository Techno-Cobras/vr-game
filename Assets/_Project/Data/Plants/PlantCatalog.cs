using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Data.Plants
{
    public sealed class PlantCatalog
    {
        private readonly Dictionary<PlantTypeId, PlantDefinition> definitionsById;

        public PlantCatalog(ItemCatalog itemCatalog, IEnumerable<PlantDefinition> definitions)
        {
            if (itemCatalog == null)
                throw new PlantDataValidationException(PlantDataValidationError.MissingItemCatalog, "Item catalog для растений отсутствует.");
            if (definitions == null)
                throw new PlantDataValidationException(PlantDataValidationError.MissingDefinition, "Коллекция plant definitions отсутствует.");

            definitionsById = new Dictionary<PlantTypeId, PlantDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new PlantDataValidationException(PlantDataValidationError.MissingDefinition, "Каталог содержит пустую plant definition.");
                if (!definitionsById.TryAdd(definition.Id, definition))
                    throw new PlantDataValidationException(PlantDataValidationError.DuplicatePlantTypeId, $"Plant type ID '{definition.Id}' встречается больше одного раза.");

                ValidateItem(itemCatalog, definition.SeedItemId, ItemCategory.Seed,
                    PlantDataValidationError.UnknownSeedItem, PlantDataValidationError.InvalidSeedItemCategory, definition.Id);
                ValidateItem(itemCatalog, definition.HarvestItemId, ItemCategory.HarvestedPlant,
                    PlantDataValidationError.UnknownHarvestItem, PlantDataValidationError.InvalidHarvestItemCategory, definition.Id);
            }

            Definitions = new ReadOnlyCollection<PlantDefinition>(definitionsById.Values.OrderBy(value => value.Id).ToList());
        }

        public IReadOnlyList<PlantDefinition> Definitions { get; }
        public bool TryGet(PlantTypeId id, out PlantDefinition definition) => definitionsById.TryGetValue(id, out definition);

        public PlantDefinition GetRequired(PlantTypeId id)
        {
            if (TryGet(id, out var definition))
                return definition;
            throw new KeyNotFoundException($"Plant type ID '{id}' отсутствует в каталоге.");
        }

        private static void ValidateItem(ItemCatalog catalog, ItemId id, ItemCategory category,
            PlantDataValidationError unknownError, PlantDataValidationError categoryError, PlantTypeId plantId)
        {
            if (!catalog.TryGet(id, out var item))
                throw new PlantDataValidationException(unknownError, $"У растения '{plantId}' указан неизвестный item '{id}'.");
            if (item.Category != category)
                throw new PlantDataValidationException(categoryError, $"Item '{id}' растения '{plantId}' должен иметь категорию '{category}'.");
        }

    }
}
