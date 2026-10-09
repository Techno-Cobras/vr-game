using System;
using UnityEngine;
using VrGame.Data.Plants;
using VrGame.Domain.SeedStorage;

namespace VrGame.VRInteraction.SeedStorage
{
    public sealed class SeedStorageStationController : MonoBehaviour
    {
        [SerializeField, Min(1)]
        private int requestedQuantity = 1;

        private SeedDispenseCoordinator coordinator;
        private ISeedStorageStationView view;
        private SeedStorageSnapshot snapshot;
        private SeedDispenseResult lastResult;
        private int selectedIndex;
        private bool applicationFocused = true;
        private bool applicationPaused;
        private int lastIntentFrame = -1;
        private PlantTypeId lastIntentPlant;
        private int lastIntentQuantity;
        private SeedDispenseContext lastIntentContext;

        public event Action<SeedStorageSnapshot> SnapshotChanged;
        public event Action<SeedDispenseResult> DispenseCompleted;

        public bool CanInteract =>
            isActiveAndEnabled &&
            applicationFocused &&
            !applicationPaused &&
            coordinator != null &&
            view != null;

        public int SelectedIndex => selectedIndex;
        public int RequestedQuantity => requestedQuantity;
        public SeedStorageSnapshot Snapshot => snapshot;

        public void Configure(SeedDispenseCoordinator value, ISeedStorageStationView stationView)
        {
            coordinator = value ?? throw new ArgumentNullException(nameof(value));
            view = stationView ?? throw new ArgumentNullException(nameof(stationView));
            RefreshView();
        }

        public void RefreshView()
        {
            if (coordinator == null || view == null)
                return;

            snapshot = coordinator.GetSnapshot();
            NormalizeSelection();
            view.Render(snapshot, selectedIndex, requestedQuantity, lastResult);
            SnapshotChanged?.Invoke(snapshot);
        }

        public void SelectNextPlant()
        {
            if (!CanInteract || snapshot.Entries.Count == 0)
                return;

            selectedIndex = (selectedIndex + 1) % snapshot.Entries.Count;
            ResetIntent();
            view.Render(snapshot, selectedIndex, requestedQuantity, lastResult);
        }

        public void SelectPreviousPlant()
        {
            if (!CanInteract || snapshot.Entries.Count == 0)
                return;

            selectedIndex = (selectedIndex - 1 + snapshot.Entries.Count) % snapshot.Entries.Count;
            ResetIntent();
            view.Render(snapshot, selectedIndex, requestedQuantity, lastResult);
        }

        public void SelectPlant(string plantTypeId)
        {
            if (!CanInteract || !PlantTypeId.TryParse(plantTypeId, out var parsed))
                return;

            for (var index = 0; index < snapshot.Entries.Count; index++)
            {
                if (snapshot.Entries[index].PlantTypeId != parsed)
                    continue;

                selectedIndex = index;
                ResetIntent();
                view.Render(snapshot, selectedIndex, requestedQuantity, lastResult);
                return;
            }
        }

        public void SetQuantity(float value)
        {
            if (!CanInteract)
                return;

            requestedQuantity = Mathf.Clamp(
                Mathf.RoundToInt(value),
                1,
                coordinator.MaxBatchQuantity);
            ResetIntent();
            view.Render(snapshot, selectedIndex, requestedQuantity, lastResult);
        }

        public void IncreaseQuantity() => SetQuantity(requestedQuantity + 1);
        public void DecreaseQuantity() => SetQuantity(requestedQuantity - 1);

        public SeedDispenseResult DispenseSelected()
        {
            if (!CanInteract || snapshot.Entries.Count == 0)
                return null;

            var entry = snapshot.Entries[selectedIndex];
            var context = GetIntentContext(entry.PlantTypeId, requestedQuantity);
            lastResult = coordinator.Dispense(entry.PlantTypeId, requestedQuantity, context);
            snapshot = coordinator.GetSnapshot();
            NormalizeSelection();
            view.Render(snapshot, selectedIndex, requestedQuantity, lastResult);
            SnapshotChanged?.Invoke(snapshot);
            DispenseCompleted?.Invoke(lastResult);
            return lastResult;
        }

        public void RequestDispenseSelected() => DispenseSelected();

        private void OnEnable() => RefreshView();

        private void OnDisable() => ResetIntent();

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                ResetIntent();
            else
                RefreshView();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
                ResetIntent();
            else
                RefreshView();
        }

        private SeedDispenseContext GetIntentContext(PlantTypeId plantTypeId, int quantity)
        {
            if (lastIntentFrame == Time.frameCount &&
                lastIntentPlant == plantTypeId &&
                lastIntentQuantity == quantity)
                return lastIntentContext;

            lastIntentFrame = Time.frameCount;
            lastIntentPlant = plantTypeId;
            lastIntentQuantity = quantity;
            lastIntentContext = new SeedDispenseContext(
                "VR-выдача саженцев со станции",
                Guid.NewGuid(),
                Guid.NewGuid());
            return lastIntentContext;
        }

        private void NormalizeSelection()
        {
            if (snapshot == null || snapshot.Entries.Count == 0)
                selectedIndex = 0;
            else
                selectedIndex = Mathf.Clamp(selectedIndex, 0, snapshot.Entries.Count - 1);
            requestedQuantity = coordinator == null
                ? Mathf.Max(1, requestedQuantity)
                : Mathf.Clamp(requestedQuantity, 1, coordinator.MaxBatchQuantity);
        }

        private void ResetIntent() => lastIntentFrame = -1;
    }
}
