using System;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VrGame.VRInteraction.Watering
{
    public readonly struct WateringActivation
    {
        public WateringActivation(
            Guid activationId,
            WateringSource source,
            WateringOutlet outlet,
            IXRSelectInteractor interactor)
        {
            if (activationId == Guid.Empty)
                throw new ArgumentException("Activation ID не может быть пустым.", nameof(activationId));

            ActivationId = activationId;
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Outlet = outlet ?? throw new ArgumentNullException(nameof(outlet));
            Interactor = interactor ?? throw new ArgumentNullException(nameof(interactor));
        }

        public Guid ActivationId { get; }
        public WateringSource Source { get; }
        public WateringOutlet Outlet { get; }
        public IXRSelectInteractor Interactor { get; }
    }
}
