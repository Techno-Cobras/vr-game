using System;

namespace VrGame.Data.Orders
{
    public enum OrderDataValidationError
    {
        None = 0,
        InvalidTemplateId,
        MissingDisplayName,
        InvalidMedicineItem,
        InvalidQuantity,
        InvalidReward,
        MissingItemCatalog,
        MissingDefinition,
        DuplicateTemplateId,
        UnknownMedicineItem,
        InvalidMedicineCategory
    }

    public sealed class OrderDataValidationException : Exception
    {
        public OrderDataValidationException(OrderDataValidationError error, string message)
            : base(message)
        {
            Error = error;
        }

        public OrderDataValidationError Error { get; }
    }
}
