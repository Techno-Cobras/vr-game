using System;
using VrGame.Data.Items;

namespace VrGame.Data.Crafting
{
    public readonly struct RecipeId : IEquatable<RecipeId>, IComparable<RecipeId>
    {
        private const string Prefix = "recipe.";
        private readonly string value;

        public RecipeId(string value)
        {
            if (!StableId.IsValid(value, Prefix))
                throw new RecipeDataValidationException(RecipeDataValidationError.InvalidRecipeId,
                    $"Recipe ID должен быть lowercase ASCII namespaced-значением с префиксом '{Prefix}'.");
            this.value = value;
        }

        public string Value => value ?? string.Empty;

        public static bool TryParse(string value, out RecipeId id)
        {
            if (!StableId.IsValid(value, Prefix))
            {
                id = default;
                return false;
            }
            id = new RecipeId(value);
            return true;
        }

        public int CompareTo(RecipeId other) => StringComparer.Ordinal.Compare(Value, other.Value);
        public bool Equals(RecipeId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is RecipeId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value;
        public static bool operator ==(RecipeId left, RecipeId right) => left.Equals(right);
        public static bool operator !=(RecipeId left, RecipeId right) => !left.Equals(right);
    }
}
