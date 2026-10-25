using System;
using UnityEngine;
using VrGame.Data.Fertilizers;
using VrGame.Data.Items;

namespace VrGame.VRInteraction.Fertilizers
{
    public sealed class FertilizerItemInstance : MonoBehaviour
    {
        [SerializeField] private FertilizerGrade grade;
        [SerializeField] private string itemId;
        private bool consumed;
        private bool ownsInventoryItem;
        private Action<ItemId> cleanupOwnership;

        public bool IsInitialized => System.Enum.IsDefined(typeof(FertilizerGrade), grade) && ItemId.TryParse(itemId, out _);
        public bool IsConsumed => consumed;
        public FertilizerGrade Grade => grade;
        public ItemId ItemId => ItemId.TryParse(itemId, out var value) ? value : throw new System.InvalidOperationException("Физическое удобрение не настроено.");

        public void Initialize(FertilizerGrade value, ItemId id)
        {
            grade = value;
            itemId = id.Value;
            consumed = false;
            ownsInventoryItem = false;
            cleanupOwnership = null;
        }

        public void MarkIssued(Action<ItemId> cleanup)
        {
            if (!IsInitialized || consumed)
                throw new InvalidOperationException("Нельзя назначить ownership ненастроенному или использованному удобрению.");

            cleanupOwnership = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
            ownsInventoryItem = true;
        }

        public void Consume()
        {
            if (consumed) return;
            consumed = true;
            ownsInventoryItem = false;
            cleanupOwnership = null;
            gameObject.SetActive(false);
            if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
        }

        private void OnDestroy()
        {
            if (!ownsInventoryItem || consumed || cleanupOwnership == null || !ItemId.TryParse(itemId, out var id))
                return;

            ownsInventoryItem = false;
            var cleanup = cleanupOwnership;
            cleanupOwnership = null;
            try
            {
                cleanup(id);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
