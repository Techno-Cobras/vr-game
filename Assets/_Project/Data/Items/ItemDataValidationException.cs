using System;

namespace VrGame.Data.Items
{
    public enum ItemDataValidationError
    {
        InvalidItemId,
        InvalidRepresentationKey,
        MissingDisplayName,
        InvalidCategory,
        InvalidStackRules,
        MissingDefinition,
        MissingRepresentation,
        DuplicateItemId,
        ConflictingRepresentation
    }

    public sealed class ItemDataValidationException : ArgumentException
    {
        public ItemDataValidationException(ItemDataValidationError error, string message)
            : base(message)
        {
            Error = error;
        }

        public ItemDataValidationError Error { get; }
    }
}
