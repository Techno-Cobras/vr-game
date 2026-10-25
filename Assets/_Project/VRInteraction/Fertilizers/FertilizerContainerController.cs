using System;
using UnityEngine;
using VrGame.Domain.Fertilizers;

namespace VrGame.VRInteraction.Fertilizers
{
    public sealed class FertilizerContainerController : MonoBehaviour
    {
        private FertilizerDispenseCoordinator coordinator;
        private IFertilizerContainerView view;
        private FertilizerContainerSnapshot snapshot;
        private FertilizerDispenseResult lastResult;
        private int selectedIndex;
        private bool applicationPaused;
        private bool applicationFocused = true;
        private int intentFrame = -1;
        private FertilizerCommandContext intent;

        public event Action<FertilizerDispenseResult> DispenseCompleted;
        public FertilizerContainerSnapshot Snapshot => snapshot;
        public int SelectedIndex => selectedIndex;
        public bool CanInteract => isActiveAndEnabled && applicationFocused && !applicationPaused && coordinator != null && view != null;

        public void Configure(FertilizerDispenseCoordinator value, IFertilizerContainerView containerView)
        { coordinator = value ?? throw new ArgumentNullException(nameof(value)); view = containerView ?? throw new ArgumentNullException(nameof(containerView)); RefreshView(); }
        public void RefreshView()
        {
            if (coordinator == null || view == null) return;
            snapshot = coordinator.GetSnapshot();
            selectedIndex = snapshot.Entries.Count == 0 ? 0 : Mathf.Clamp(selectedIndex, 0, snapshot.Entries.Count - 1);
            view.Render(snapshot, selectedIndex, lastResult);
        }
        public void SelectNextGrade() { if (!CanInteract || snapshot.Entries.Count == 0) return; selectedIndex = (selectedIndex + 1) % snapshot.Entries.Count; ResetIntent(); view.Render(snapshot, selectedIndex, lastResult); }
        public void SelectPreviousGrade() { if (!CanInteract || snapshot.Entries.Count == 0) return; selectedIndex = (selectedIndex - 1 + snapshot.Entries.Count) % snapshot.Entries.Count; ResetIntent(); view.Render(snapshot, selectedIndex, lastResult); }
        public FertilizerDispenseResult DispenseSelected()
        {
            if (!CanInteract || snapshot.Entries.Count == 0) return null;
            if (intentFrame != Time.frameCount) { intentFrame = Time.frameCount; intent = new FertilizerCommandContext("VR-выдача удобрения", Guid.NewGuid(), Guid.NewGuid()); }
            lastResult = coordinator.Dispense(snapshot.Entries[selectedIndex].Grade, intent);
            RefreshView();
            DispenseCompleted?.Invoke(lastResult);
            return lastResult;
        }
        public void RequestDispenseSelected() => DispenseSelected();
        private void OnEnable() => RefreshView();
        private void OnDisable() => ResetIntent();
        private void OnApplicationPause(bool value) { applicationPaused = value; if (value) ResetIntent(); else RefreshView(); }
        private void OnApplicationFocus(bool value) { applicationFocused = value; if (!value) ResetIntent(); else RefreshView(); }
        private void ResetIntent() => intentFrame = -1;
    }
}
