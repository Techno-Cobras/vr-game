namespace VrGame.Data.Items
{
    public readonly struct ItemStackRules
    {
        public ItemStackRules(int maxStackSize, bool allowsStackRepresentation)
        {
            if (maxStackSize <= 0 || (allowsStackRepresentation && maxStackSize == 1))
                throw new ItemDataValidationException(
                    ItemDataValidationError.InvalidStackRules,
                    "Max stack size должен быть положительным, а stack representation требует размер больше одного.");

            MaxStackSize = maxStackSize;
            AllowsStackRepresentation = allowsStackRepresentation;
        }

        public int MaxStackSize { get; }

        public bool AllowsStackRepresentation { get; }

        public bool IsStackable => MaxStackSize > 1;
    }
}
