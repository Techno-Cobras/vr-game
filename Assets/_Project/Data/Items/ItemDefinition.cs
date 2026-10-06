using System;

namespace VrGame.Data.Items
{
    public sealed class ItemDefinition
    {
        public ItemDefinition(
            ItemId id,
            string displayName,
            ItemCategory category,
            ItemRepresentationKey representation,
            ItemStackRules stackRules)
        {
            if (string.IsNullOrEmpty(id.Value))
                throw new ItemDataValidationException(
                    ItemDataValidationError.InvalidItemId,
                    "У item definition отсутствует стабильный ID.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw new ItemDataValidationException(
                    ItemDataValidationError.MissingDisplayName,
                    $"У предмета '{id}' отсутствует display name.");
            if (!Enum.IsDefined(typeof(ItemCategory), category))
                throw new ItemDataValidationException(
                    ItemDataValidationError.InvalidCategory,
                    $"У предмета '{id}' указана неизвестная категория.");
            if (string.IsNullOrEmpty(representation.Value))
                throw new ItemDataValidationException(
                    ItemDataValidationError.MissingRepresentation,
                    $"У предмета '{id}' отсутствует representation key.");
            if (stackRules.MaxStackSize <= 0)
                throw new ItemDataValidationException(
                    ItemDataValidationError.InvalidStackRules,
                    $"У предмета '{id}' отсутствуют корректные stack rules.");

            Id = id;
            DisplayName = displayName;
            Category = category;
            Representation = representation;
            StackRules = stackRules;
        }

        public ItemId Id { get; }

        public string DisplayName { get; }

        public ItemCategory Category { get; }

        public ItemRepresentationKey Representation { get; }

        public ItemStackRules StackRules { get; }
    }
}
