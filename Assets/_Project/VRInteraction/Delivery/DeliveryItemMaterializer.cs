using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;
using VrGame.Data.Plants;
using VrGame.DataAssets;
using VrGame.Domain.Delivery;
using VrGame.Domain.Inventory;
using VrGame.VRInteraction.Fertilizers;
using VrGame.VRInteraction.SeedStorage;
using Object = UnityEngine.Object;

namespace VrGame.VRInteraction.Delivery
{
    public sealed class DeliveryItemMaterializer : MonoBehaviour, IDeliveryItemMaterializer
    {
        private RuntimeItemCatalog items;
        private PlantCatalog plants;
        private FertilizerCatalog fertilizers;
        private Action<ItemId> cleanupFertilizerOwnership;

        public void Configure(
            RuntimeItemCatalog itemCatalog,
            PlantCatalog plantCatalog,
            FertilizerCatalog fertilizerCatalog,
            Action<ItemId> fertilizerCleanup)
        {
            items = itemCatalog ?? throw new ArgumentNullException(nameof(itemCatalog));
            plants = plantCatalog ?? throw new ArgumentNullException(nameof(plantCatalog));
            fertilizers = fertilizerCatalog ?? throw new ArgumentNullException(nameof(fertilizerCatalog));
            cleanupFertilizerOwnership = fertilizerCleanup ?? throw new ArgumentNullException(nameof(fertilizerCleanup));
        }

        public bool TryMaterialize(
            PendingDeliveryEntry entry,
            Transform slot,
            PendingDeliveryLedger ledger,
            Inventory inventory,
            Action<IDeliveryProjection> projectionReleased,
            out IDeliveryProjection projection)
        {
            projection = null;
            if (entry == null || entry.Quantity != 1 || slot == null || ledger == null || inventory == null ||
                projectionReleased == null || items == null || plants == null || fertilizers == null ||
                cleanupFertilizerOwnership == null ||
                !items.Items.TryGet(entry.ItemId, out var definition) ||
                !items.TryGetRepresentation(entry.Representation, out var prefab) || prefab == null ||
                !HasRequiredInteraction(prefab))
                return false;

            GameObject stagingRoot = null;
            GameObject instance = null;
            try
            {
                stagingRoot = new GameObject("Staging доставки");
                stagingRoot.SetActive(false);
                stagingRoot.transform.SetParent(slot, false);
                instance = Instantiate(prefab, stagingRoot.transform, false);
                instance.SetActive(false);
                instance.name = $"Доставка: {definition.DisplayName}";

                FertilizerItemInstance fertilizerIdentity = null;
                if (definition.Category == ItemCategory.Seed)
                {
                    var plant = plants.Definitions.FirstOrDefault(value => value.SeedItemId == entry.ItemId);
                    if (plant == null)
                        throw new InvalidOperationException($"Для саженца '{entry.ItemId}' не найден plant definition.");
                    var seed = instance.GetComponent<SeedItemInstance>() ?? instance.AddComponent<SeedItemInstance>();
                    seed.Initialize(plant.Id, entry.ItemId);
                }
                else if (definition.Category == ItemCategory.Fertilizer)
                {
                    if (!fertilizers.TryGet(entry.ItemId, out var fertilizer))
                        throw new InvalidOperationException($"Для удобрения '{entry.ItemId}' не найден fertilizer definition.");
                    fertilizerIdentity = instance.GetComponent<FertilizerItemInstance>() ??
                                         instance.AddComponent<FertilizerItemInstance>();
                    fertilizerIdentity.Initialize(fertilizer.Grade, entry.ItemId);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Категория '{definition.Category}' пока не поддерживается Delivery Box.");
                }

                instance.transform.SetParent(slot, false);
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                var binding = instance.GetComponent<DeliveryItemBinding>() ?? instance.AddComponent<DeliveryItemBinding>();
                binding.Configure(
                    entry.Id,
                    slot,
                    ledger,
                    inventory,
                    instance.GetComponent<XRGrabInteractable>(),
                    fertilizerIdentity,
                    cleanupFertilizerOwnership,
                    projectionReleased);
                DestroySafely(stagingRoot);
                stagingRoot = null;
                instance.SetActive(true);
                projection = binding;
                return binding.IsAlive;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Не удалось материализовать delivery entry '{entry?.Id}': {exception.Message}", this);
                DestroySafely(instance);
                DestroySafely(stagingRoot);
                return false;
            }
        }

        private static bool HasRequiredInteraction(GameObject prefab) =>
            prefab.GetComponent<Rigidbody>() != null &&
            prefab.GetComponentInChildren<Collider>(true) != null &&
            prefab.GetComponent<XRGrabInteractable>() != null;

        private static void DestroySafely(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
