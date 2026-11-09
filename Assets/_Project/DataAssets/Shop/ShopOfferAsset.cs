using UnityEngine;
using VrGame.Data.Shop;

namespace VrGame.DataAssets.Shop
{
    [CreateAssetMenu(menuName = "VR Game/Shop/Offer", fileName = "ShopOffer")]
    public sealed class ShopOfferAsset : ScriptableObject
    {
        [SerializeField]
        private string id;
        [SerializeField]
        private string displayName;
        [SerializeField, Min(1)]
        private int price = 1;
        [SerializeField]
        private ItemDefinitionAsset item;
        [SerializeField, Min(1)]
        private int quantity = 1;
        public ShopOfferDefinition ToDefinition()
        {
            if (item == null)
                throw new System.InvalidOperationException("Shop offer item missing.");
            var definition = item.ToDefinition();
            return new ShopOfferDefinition(new ProductId(id), displayName, price, definition.Id, quantity, definition.Representation);
        }
    }
}
