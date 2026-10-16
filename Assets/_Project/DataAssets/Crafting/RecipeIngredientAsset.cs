using System;
using UnityEngine;

namespace VrGame.DataAssets.Crafting
{
    [Serializable]
    public sealed class RecipeIngredientAsset
    {
        [SerializeField]
        private ItemDefinitionAsset item;

        [SerializeField, Min(1)]
        private int quantity = 1;

        public ItemDefinitionAsset Item => item;
        public int Quantity => quantity;
    }
}
