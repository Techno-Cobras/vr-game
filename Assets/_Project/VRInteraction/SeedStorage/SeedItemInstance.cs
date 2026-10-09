using UnityEngine;
using VrGame.Data.Items;
using VrGame.Data.Plants;

namespace VrGame.VRInteraction.SeedStorage
{
    public sealed class SeedItemInstance : MonoBehaviour
    {
        [SerializeField]
        private string plantTypeId;

        [SerializeField]
        private string seedItemId;

        private bool isConsumed;

        public bool IsInitialized =>
            PlantTypeId.TryParse(plantTypeId, out _) && ItemId.TryParse(seedItemId, out _);

        public bool IsConsumed => isConsumed;

        public PlantTypeId PlantTypeId => PlantTypeId.TryParse(plantTypeId, out var value)
            ? value
            : throw new System.InvalidOperationException("Физический саженец не настроен.");

        public ItemId SeedItemId => ItemId.TryParse(seedItemId, out var value)
            ? value
            : throw new System.InvalidOperationException("Физический саженец не настроен.");

        public void Initialize(PlantTypeId typeId, ItemId itemId)
        {
            plantTypeId = typeId.Value;
            seedItemId = itemId.Value;
            isConsumed = false;
        }

        public void Consume()
        {
            if (isConsumed)
                return;

            isConsumed = true;
            gameObject.SetActive(false);
            if (Application.isPlaying)
                Destroy(gameObject);
            else
                DestroyImmediate(gameObject);
        }
    }
}
