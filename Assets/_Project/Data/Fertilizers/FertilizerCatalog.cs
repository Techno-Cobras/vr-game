using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Data.Fertilizers
{
    public sealed class FertilizerCatalog
    {
        private readonly Dictionary<FertilizerGrade, FertilizerDefinition> byGrade;
        private readonly Dictionary<ItemId, FertilizerDefinition> byItem;

        public FertilizerCatalog(IEnumerable<FertilizerDefinition> definitions)
        {
            if (definitions == null)
                throw new FertilizerDataValidationException(
                    FertilizerDataValidationError.MissingDefinition,
                    "Коллекция fertilizer definitions отсутствует.");

            byGrade = new Dictionary<FertilizerGrade, FertilizerDefinition>();
            byItem = new Dictionary<ItemId, FertilizerDefinition>();
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new FertilizerDataValidationException(
                        FertilizerDataValidationError.MissingDefinition,
                        "Каталог удобрений содержит пустую definition.");
                if (!byGrade.TryAdd(definition.Grade, definition))
                    throw new FertilizerDataValidationException(
                        FertilizerDataValidationError.DuplicateGrade,
                        $"Fertilizer grade '{definition.Grade}' встречается больше одного раза.");
                if (!byItem.TryAdd(definition.ItemId, definition))
                    throw new FertilizerDataValidationException(
                        FertilizerDataValidationError.DuplicateItem,
                        $"Fertilizer item '{definition.ItemId}' встречается больше одного раза.");
            }

            Definitions = new ReadOnlyCollection<FertilizerDefinition>(
                byGrade.Values.OrderBy(value => value.Grade).ToList());
        }

        public IReadOnlyList<FertilizerDefinition> Definitions { get; }
        public bool TryGet(FertilizerGrade grade, out FertilizerDefinition definition) =>
            byGrade.TryGetValue(grade, out definition);
        public bool TryGet(ItemId itemId, out FertilizerDefinition definition) =>
            byItem.TryGetValue(itemId, out definition);
        public FertilizerDefinition GetRequired(FertilizerGrade grade) => byGrade.TryGetValue(grade, out var value)
            ? value
            : throw new KeyNotFoundException($"Fertilizer grade '{grade}' отсутствует в каталоге.");
    }
}
