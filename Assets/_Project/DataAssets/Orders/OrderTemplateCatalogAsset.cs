using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Orders;

namespace VrGame.DataAssets.Orders
{
    [CreateAssetMenu(menuName = "VR Game/Orders/Template Catalog", fileName = "OrderTemplateCatalog")]
    public sealed class OrderTemplateCatalogAsset : ScriptableObject
    {
        [SerializeField]
        private OrderTemplateDefinitionAsset[] definitions = Array.Empty<OrderTemplateDefinitionAsset>();

        public OrderTemplateCatalog Build(RuntimeItemCatalog itemCatalog)
        {
            if (itemCatalog == null)
                throw new OrderDataValidationException(
                    OrderDataValidationError.MissingItemCatalog,
                    "Runtime item catalog для шаблонов заказов отсутствует.");
            if (definitions == null)
                throw new OrderDataValidationException(
                    OrderDataValidationError.MissingDefinition,
                    $"У order template catalog asset '{name}' отсутствует коллекция definitions.");

            var built = new List<OrderTemplateDefinition>(definitions.Length);
            foreach (var definition in definitions)
            {
                if (definition == null)
                    throw new OrderDataValidationException(
                        OrderDataValidationError.MissingDefinition,
                        $"Order template catalog asset '{name}' содержит пустую definition.");
                built.Add(definition.ToDefinition());
            }
            return new OrderTemplateCatalog(itemCatalog.Items, built);
        }
    }
}
