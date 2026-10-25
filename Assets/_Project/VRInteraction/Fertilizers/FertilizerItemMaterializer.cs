using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.DataAssets;
using VrGame.Domain.Fertilizers;
using Object = UnityEngine.Object;

namespace VrGame.VRInteraction.Fertilizers
{
    public sealed class FertilizerItemMaterializer : MonoBehaviour, IFertilizerMaterializer
    {
        [SerializeField] private Transform spawnRoot;
        private RuntimeItemCatalog catalog;
        private Action<VrGame.Data.Items.ItemId> cleanupOwnership;

        public void Configure(
            RuntimeItemCatalog runtimeCatalog,
            Transform root,
            Action<VrGame.Data.Items.ItemId> cleanup)
        {
            catalog = runtimeCatalog ?? throw new ArgumentNullException(nameof(runtimeCatalog));
            if (root != null) spawnRoot = root;
            cleanupOwnership = cleanup ?? throw new ArgumentNullException(nameof(cleanup));
        }

        public bool TryStage(FertilizerMaterializationRequest request, out IStagedFertilizerItem item)
        {
            item = null;
            if (catalog == null || !catalog.Items.TryGet(request.ItemId, out var definition) ||
                !catalog.TryGetRepresentation(definition.Representation, out var prefab) || prefab == null ||
                prefab.GetComponent<Rigidbody>() == null || prefab.GetComponentInChildren<Collider>(true) == null ||
                prefab.GetComponent<XRGrabInteractable>() == null)
                return false;
            GameObject instance = null;
            try
            {
                instance = Instantiate(prefab, spawnRoot != null ? spawnRoot : transform, false);
                instance.SetActive(false);
                var identity = instance.GetComponent<FertilizerItemInstance>() ?? instance.AddComponent<FertilizerItemInstance>();
                identity.Initialize(request.Grade, request.ItemId);
                item = new StagedItem(instance, identity, cleanupOwnership);
                return true;
            }
            catch (Exception)
            {
                DestroySafely(instance);
                return false;
            }
        }

        private static void DestroySafely(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }

        private sealed class StagedItem : IStagedFertilizerItem
        {
            private GameObject instance;
            private FertilizerItemInstance identity;
            private readonly Action<VrGame.Data.Items.ItemId> cleanupOwnership;
            private bool released;
            private bool committed;
            public StagedItem(
                GameObject value,
                FertilizerItemInstance itemIdentity,
                Action<VrGame.Data.Items.ItemId> cleanup)
            {
                instance = value;
                identity = itemIdentity;
                cleanupOwnership = cleanup;
            }
            public bool TryRelease()
            {
                if (committed || released) return instance != null || committed;
                if (instance == null) return false;
                try { instance.transform.SetParent(null, true); instance.SetActive(true); released = true; return true; }
                catch (Exception) { Rollback(); return false; }
            }
            public bool TryCommit()
            {
                if (committed)
                    return true;
                if (!released || instance == null || identity == null)
                    return false;

                try
                {
                    identity.MarkIssued(cleanupOwnership);
                    committed = true;
                    instance = null;
                    identity = null;
                    return true;
                }
                catch (Exception)
                {
                    return false;
                }
            }

            public void Rollback()
            {
                if (committed || instance == null)
                    return;

                instance.SetActive(false);
                DestroySafely(instance);
                instance = null;
                identity = null;
                released = false;
            }
        }
    }
}
