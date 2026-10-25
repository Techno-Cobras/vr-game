using System;
using System.Collections.Generic;
using UnityEngine;
using VrGame.Data.Plants;
using VrGame.DataAssets.Plants;
using VrGame.Domain.Plants;

namespace VrGame.VRInteraction.Plants
{
    public sealed class PlantGrowthController : MonoBehaviour
    {
        [SerializeField]
        private Transform visualRoot;

        [SerializeField, Min(.1f)]
        private float updateIntervalSeconds = .1f;

        private readonly Dictionary<PlantRepresentationKey, GameObject> stageObjects =
            new Dictionary<PlantRepresentationKey, GameObject>();
        private PlantingSlot slot;
        private RuntimePlantCatalog catalog;
        private IPlantGrowthTimeSource timeSource;
        private PlantGrowthTimer timer;
        private PlantRepresentationKey? activeRepresentation;
        private double nextTickAt;
        private bool applicationPaused;
        private bool applicationFocused = true;

        public PlantRepresentationKey? ActiveRepresentation => activeRepresentation;
        public GameObject ActiveStageObject =>
            activeRepresentation.HasValue ? stageObjects[activeRepresentation.Value] : null;
        public float StageSurfaceWorldY => visualRoot == null ? 0f : visualRoot.position.y;
        public int PrecreatedStageCount => stageObjects.Count;

        public void RefreshVisual()
        {
            if (slot != null)
                RenderStage();
        }

        public void Configure(
            PlantingSlot plantingSlot,
            RuntimePlantCatalog runtimeCatalog,
            IPlantGrowthTimeSource growthTimeSource = null)
        {
            if (timer != null)
                throw new InvalidOperationException("Контроллер роста уже настроен.");

            slot = plantingSlot ?? throw new ArgumentNullException(nameof(plantingSlot));
            catalog = runtimeCatalog ?? throw new ArgumentNullException(nameof(runtimeCatalog));
            timeSource = growthTimeSource ?? new UnityGrowthTimeSource();
            if (visualRoot == null)
                throw new InvalidOperationException("Корень визуальных стадий растения не настроен.");

            PrecreateStages();
            timer = new PlantGrowthTimer(slot, catalog.Plants, timeSource);
            RefreshActivity();
            RenderStage();
        }

        private void OnEnable() => RefreshActivity();

        private void OnDisable()
        {
            PauseAndRender();
        }

        private void OnApplicationPause(bool paused)
        {
            SetApplicationPaused(paused);
        }

        private void OnApplicationFocus(bool focused)
        {
            SetApplicationFocused(focused);
        }

        private void Update() => ProcessScheduledUpdate();

        public void SetApplicationPaused(bool paused)
        {
            applicationPaused = paused;
            RefreshActivity();
        }

        public void SetApplicationFocused(bool focused)
        {
            applicationFocused = focused;
            RefreshActivity();
        }

        public void ProcessScheduledUpdate()
        {
            if (timer == null || !timer.IsRunning)
                return;

            var now = timeSource.TimeSeconds;
            if (now < nextTickAt)
                return;

            nextTickAt = now + Math.Max(.1f, updateIntervalSeconds);
            timer.Tick();
            RenderStage();
        }

        private void RefreshActivity()
        {
            if (timer == null)
                return;

            if (isActiveAndEnabled && !applicationPaused && applicationFocused)
            {
                timer.Resume();
                nextTickAt = timeSource.TimeSeconds;
            }
            else
            {
                PauseAndRender();
            }
        }

        private void PauseAndRender()
        {
            if (timer == null)
                return;

            timer.Pause();
            RenderStage();
        }

        private void PrecreateStages()
        {
            foreach (var definition in catalog.Plants.Definitions)
            {
                foreach (var stage in definition.Stages)
                {
                    if (stageObjects.ContainsKey(stage.Representation))
                        continue;

                    var instance = Instantiate(
                        catalog.GetRequiredRepresentation(stage.Representation),
                        visualRoot,
                        false);
                    instance.name = $"Стадия {stage.Representation.Value}";
                    AlignBottomToSurface(instance);
                    instance.SetActive(false);
                    stageObjects.Add(stage.Representation, instance);
                }
            }
        }

        private void AlignBottomToSurface(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return;

            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            instance.transform.position += Vector3.up * (visualRoot.position.y - bounds.min.y);
        }

        private void RenderStage()
        {
            var snapshot = slot.GetSnapshot();
            if (!snapshot.PlantTypeId.HasValue || snapshot.State == PlantingSlotState.Empty)
            {
                SetActiveStage(null);
                return;
            }

            var definition = catalog.Plants.GetRequired(snapshot.PlantTypeId.Value);
            var stage = PlantGrowthStageResolver.Resolve(definition, snapshot.NormalizedProgress);
            SetActiveStage(stage.Representation);
        }

        private void SetActiveStage(PlantRepresentationKey? representation)
        {
            if (activeRepresentation.Equals(representation))
                return;

            if (activeRepresentation.HasValue)
                stageObjects[activeRepresentation.Value].SetActive(false);
            if (representation.HasValue)
                stageObjects[representation.Value].SetActive(true);
            activeRepresentation = representation;
        }

        private sealed class UnityGrowthTimeSource : IPlantGrowthTimeSource
        {
            public double TimeSeconds => Time.realtimeSinceStartupAsDouble;
        }
    }
}
