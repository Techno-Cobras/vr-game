using System;
using System.Linq;
using UnityEngine;
using VrGame.Data.Shop;

namespace VrGame.DataAssets.Shop
{
    [CreateAssetMenu(menuName = "VR Game/Shop/Catalog", fileName = "ShopCatalog")]
    public sealed class ShopCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private ShopOfferAsset[] offers = Array.Empty<ShopOfferAsset>();
        public ShopCatalog Build(RuntimeItemCatalog items)
        {
            if (items == null || offers == null || offers.Any(x => x == null))
                throw new ArgumentException("Missing shop catalog data.");
            return new ShopCatalog(items.Items, offers.Select(x => x.ToDefinition()));
        }
    }
}
