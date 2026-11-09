using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using VrGame.Data.Items;

namespace VrGame.Data.Shop
{
    public readonly struct ProductId : IEquatable<ProductId>
    {
        public ProductId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("Product ID required.");
            Value = value;
        }

        public string Value { get; }

        public bool Equals(ProductId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ProductId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
    }

    public sealed class ShopOfferDefinition
    {
        public ShopOfferDefinition(ProductId id, string displayName, int price, ItemId itemId, int quantity, ItemRepresentationKey representation)
        {
            if (string.IsNullOrWhiteSpace(id.Value) || string.IsNullOrWhiteSpace(displayName))
                throw new ArgumentException("Product ID and name required.");
            if (price <= 0 || quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(price));
            Id = id;
            DisplayName = displayName;
            Price = price;
            ItemId = itemId;
            Quantity = quantity;
            Representation = representation;
        }

        public ProductId Id { get; }
        public string DisplayName { get; }
        public int Price { get; }
        public ItemId ItemId { get; }
        public int Quantity { get; }
        public ItemRepresentationKey Representation { get; }
    }

    public sealed class ShopCatalog
    {
        private readonly Dictionary<ProductId, ShopOfferDefinition> offers = new Dictionary<ProductId, ShopOfferDefinition>();
        public ShopCatalog(ItemCatalog items, IEnumerable<ShopOfferDefinition> definitions)
        {
            if (items == null || definitions == null)
                throw new ArgumentNullException();
            foreach (var offer in definitions)
            {
                if (offer == null)
                    throw new ArgumentException("Missing offer.");
                var item = items.GetRequired(offer.ItemId);
                if ((item.Category != ItemCategory.Seed && item.Category != ItemCategory.Fertilizer) || !item.Representation.Equals(offer.Representation))
                    throw new ArgumentException("Offer must reference a seed/fertilizer and its representation.");
                if (!offers.TryAdd(offer.Id, offer))
                    throw new ArgumentException("Duplicate product ID.");
            }

            Definitions = new ReadOnlyCollection<ShopOfferDefinition>(offers.Values.OrderBy(x => x.Id.Value, StringComparer.Ordinal).ToList());
        }

        public IReadOnlyList<ShopOfferDefinition> Definitions { get; }

        public bool TryGet(ProductId id, out ShopOfferDefinition offer) => offers.TryGetValue(id, out offer);
    }
}
