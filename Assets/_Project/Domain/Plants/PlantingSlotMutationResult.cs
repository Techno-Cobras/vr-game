namespace VrGame.Domain.Plants
{
    public sealed class PlantingSlotMutationResult
    {
        private PlantingSlotMutationResult(
            PlantingSlotRejection rejection,
            PlantingSlotChanged change,
            bool eventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            Change = change;
            EventPublished = eventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == PlantingSlotRejection.None;
        public PlantingSlotRejection Rejection { get; }
        public PlantingSlotChanged Change { get; }
        public bool EventPublished { get; }
        public bool IsReplay { get; }

        internal static PlantingSlotMutationResult Success(PlantingSlotChanged change, bool eventPublished) =>
            new PlantingSlotMutationResult(PlantingSlotRejection.None, change, eventPublished, false);

        internal static PlantingSlotMutationResult Reject(PlantingSlotRejection rejection) =>
            new PlantingSlotMutationResult(rejection, null, false, false);

        internal PlantingSlotMutationResult AsReplay() =>
            new PlantingSlotMutationResult(Rejection, Change, EventPublished, true);
    }
}
