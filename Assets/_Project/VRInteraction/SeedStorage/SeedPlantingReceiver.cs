using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Filtering;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using VrGame.Domain.SeedStorage;

namespace VrGame.VRInteraction.SeedStorage
{
    public sealed class SeedPlantingReceiver : MonoBehaviour, IXRSelectFilter
    {
        [SerializeField]
        private XRSocketInteractor socket;

        private SeedPlantingAdapter adapter;
        private XRSocketInteractor subscribedSocket;
        private SeedItemInstance currentSeed;
        private PlantSeedContext currentContext;
        private bool hasCurrentContext;
        private bool applicationFocused = true;
        private bool applicationPaused;

        public event Action<PlantSeedResult> PlantingCompleted;

        public bool canProcess => isActiveAndEnabled;

        public bool CanReceive =>
            isActiveAndEnabled &&
            applicationFocused &&
            !applicationPaused &&
            adapter != null &&
            socket != null;

        public void Configure(
            XRSocketInteractor value,
            PlantSeedCoordinator coordinator)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));
            if (coordinator == null)
                throw new ArgumentNullException(nameof(coordinator));

            Unsubscribe();
            socket = value;
            adapter = new SeedPlantingAdapter(coordinator);
            if (isActiveAndEnabled)
                Subscribe();
        }

        private void OnEnable() => Subscribe();

        private void OnDisable()
        {
            Unsubscribe();
            ClearIntent();
        }

        private void OnApplicationPause(bool paused)
        {
            applicationPaused = paused;
            if (paused)
                ClearIntent();
        }

        private void OnApplicationFocus(bool focused)
        {
            applicationFocused = focused;
            if (!focused)
                ClearIntent();
        }

        private void Subscribe()
        {
            if (socket == null || subscribedSocket == socket)
                return;

            socket.selectEntered.AddListener(OnSelectEntered);
            socket.selectExited.AddListener(OnSelectExited);
            socket.selectFilters.Add(this);
            subscribedSocket = socket;
        }

        private void Unsubscribe()
        {
            if (subscribedSocket == null)
                return;

            subscribedSocket.selectEntered.RemoveListener(OnSelectEntered);
            subscribedSocket.selectExited.RemoveListener(OnSelectExited);
            subscribedSocket.selectFilters.Remove(this);
            subscribedSocket = null;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (!CanReceive || args?.interactableObject == null)
                return;

            var seed = args.interactableObject.transform.GetComponentInParent<SeedItemInstance>();
            if (seed == null || !seed.IsInitialized || seed.IsConsumed)
                return;

            if (currentSeed != seed || !hasCurrentContext)
            {
                currentSeed = seed;
                currentContext = new PlantSeedContext(
                    "посадка саженца через VR-гнездо",
                    Guid.NewGuid(),
                    Guid.NewGuid());
                hasCurrentContext = true;
            }

            var result = adapter.Plant(seed, currentContext);
            PlantingCompleted?.Invoke(result);
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            if (args?.interactableObject == null || currentSeed == null)
                return;

            var seed = args.interactableObject.transform.GetComponentInParent<SeedItemInstance>();
            if (seed == currentSeed)
                ClearIntent();
        }

        private void ClearIntent()
        {
            currentSeed = null;
            currentContext = default;
            hasCurrentContext = false;
        }

        public bool Process(IXRSelectInteractor interactor, IXRSelectInteractable interactable)
        {
            if (!CanReceive || interactable == null)
                return false;

            var seed = interactable.transform.GetComponentInParent<SeedItemInstance>();
            return seed != null && seed.IsInitialized && !seed.IsConsumed;
        }
    }
}
