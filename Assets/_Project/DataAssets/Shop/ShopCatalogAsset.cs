using System;
using System.Linq;
using UnityEngine;
using VrGame.Data.Shop;

namespace VrGame.DataAssets.Shop
{
    [CreateAssetMenu(menuName = "VR Game/Магазин/Каталог", fileName = "ShopCatalog")]
    public sealed class ShopCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private ShopOfferAsset[] offers = Array.Empty<ShopOfferAsset>();
        public ShopCatalog Build(RuntimeItemCatalog items)
        {
            if (items == null || offers == null || offers.Any(x => x == null))
                throw new ArgumentException("Данные каталога магазина настроены не полностью.");
            return new ShopCatalog(items.Items, offers.Select(x => x.ToDefinition()));
        }
    }
}
