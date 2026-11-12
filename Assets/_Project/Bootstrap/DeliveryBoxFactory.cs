using System;
using UnityEngine;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.Domain.Delivery;
using VrGame.Domain.Inventory;
using VrGame.VRInteraction.Delivery;

namespace VrGame.Bootstrap
{
    public static class DeliveryBoxFactory
    {
        private const int DefaultCapacity = 3;

        public static DeliveryBoxController Create(
            Transform anchor,
            RuntimeItemCatalog items,
            PlantCatalog plants,
            FertilizerCatalog fertilizers,
            PendingDeliveryLedger deliveries,
            Inventory playerInventory,
            Action<ItemId> cleanupFertilizerOwnership,
            int capacity = DefaultCapacity)
        {
            if (anchor == null) throw new ArgumentNullException(nameof(anchor));
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (plants == null) throw new ArgumentNullException(nameof(plants));
            if (fertilizers == null) throw new ArgumentNullException(nameof(fertilizers));
            if (deliveries == null) throw new ArgumentNullException(nameof(deliveries));
            if (playerInventory == null) throw new ArgumentNullException(nameof(playerInventory));
            if (cleanupFertilizerOwnership == null) throw new ArgumentNullException(nameof(cleanupFertilizerOwnership));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));

            var host = new GameObject("Delivery Box");
            host.SetActive(false);
            host.transform.SetParent(anchor, false);
            host.transform.localPosition = new Vector3(1.05f, .55f, .25f);
            host.transform.localRotation = Quaternion.identity;

            var tray = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tray.name = "Корпус Delivery Box";
            tray.transform.SetParent(host.transform, false);
            tray.transform.localPosition = new Vector3(0f, -.09f, 0f);
            tray.transform.localScale = new Vector3(.72f, .12f, .34f);
            var renderer = tray.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material.color = new Color(.16f, .28f, .36f);

            var slots = new Transform[capacity];
            const float spacing = .22f;
            var start = -.5f * spacing * (capacity - 1);
            for (var index = 0; index < capacity; index++)
            {
                var slot = new GameObject($"Слот доставки {index + 1}");
                slot.transform.SetParent(host.transform, false);
                slot.transform.localPosition = new Vector3(start + index * spacing, .08f, 0f);
                slots[index] = slot.transform;
            }

            var materializer = host.AddComponent<DeliveryItemMaterializer>();
            materializer.Configure(items, plants, fertilizers, cleanupFertilizerOwnership);
            var controller = host.AddComponent<DeliveryBoxController>();
            controller.Configure(deliveries, playerInventory, materializer, slots);
            host.SetActive(true);
            return controller;
        }
    }
}
