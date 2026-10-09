using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using VrGame.DataAssets;
using VrGame.Domain.SeedStorage;
using Object = UnityEngine.Object;

namespace VrGame.VRInteraction.SeedStorage
{
    public sealed class SeedBatchMaterializer : MonoBehaviour, ISeedDispenseMaterializer
    {
        [SerializeField]
        private Transform spawnRoot;

        [SerializeField]
        private Vector3 itemSpacing = new Vector3(0.12f, 0f, 0f);

        private RuntimeItemCatalog runtimeCatalog;

        public void Configure(RuntimeItemCatalog catalog, Transform root = null)
        {
            runtimeCatalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            if (root != null)
                spawnRoot = root;
        }

        public bool TryStage(SeedMaterializationRequest request, out IStagedSeedBatch batch)
        {
            batch = null;
            if (runtimeCatalog == null || request.Quantity <= 0)
                return false;
            if (!runtimeCatalog.Items.TryGet(request.SeedItemId, out var definition))
                return false;
            if (!runtimeCatalog.TryGetRepresentation(definition.Representation, out var prefab) || prefab == null)
                return false;
            if (!HasRequiredInteractionComponents(prefab))
                return false;

            GameObject batchRoot = null;
            try
            {
                batchRoot = new GameObject($"Партия саженцев: {request.PlantTypeId}");
                batchRoot.SetActive(false);
                batchRoot.transform.SetParent(spawnRoot != null ? spawnRoot : transform, false);

                for (var index = 0; index < request.Quantity; index++)
                {
                    var instance = Instantiate(prefab, batchRoot.transform, false);
                    // Prefab может храниться выключенным, чтобы не регистрировать
                    // interactable до атомарного Release всей партии.
                    instance.SetActive(true);
                    instance.transform.localPosition = itemSpacing * index;
                    var seed = instance.GetComponent<SeedItemInstance>();
                    if (seed == null)
                        seed = instance.AddComponent<SeedItemInstance>();
                    seed.Initialize(request.PlantTypeId, request.SeedItemId);
                }

                batch = new StagedSeedBatch(batchRoot);
                return true;
            }
            catch (Exception)
            {
                DestroySafely(batchRoot);
                return false;
            }
        }

        private static bool HasRequiredInteractionComponents(GameObject prefab) =>
            prefab.GetComponent<Rigidbody>() != null &&
            prefab.GetComponentInChildren<Collider>(true) != null &&
            prefab.GetComponent<XRGrabInteractable>() != null;

        private static void DestroySafely(Object target)
        {
            if (target == null)
                return;

            if (Application.isPlaying)
                Destroy(target);
            else
                DestroyImmediate(target);
        }

        private sealed class StagedSeedBatch : IStagedSeedBatch
        {
            private GameObject root;
            private bool released;
            private bool committed;

            public StagedSeedBatch(GameObject root)
            {
                this.root = root;
            }

            public bool TryRelease()
            {
                if (committed || released)
                    return root != null || committed;
                if (root == null)
                    return false;

                try
                {
                    root.transform.SetParent(null, true);
                    root.SetActive(true);
                    released = true;
                    return true;
                }
                catch (Exception)
                {
                    Rollback();
                    return false;
                }
            }

            public void Commit()
            {
                if (committed || !released)
                    return;

                committed = true;
                root = null;
            }

            public void Rollback()
            {
                if (committed || root == null)
                    return;

                root.SetActive(false);
                DestroySafely(root);
                root = null;
                released = false;
            }
        }
    }
}
