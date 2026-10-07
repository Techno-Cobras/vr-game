namespace VrGame.Domain.Economy
{
    public sealed class EconomyMutationResult
    {
        private EconomyMutationResult(
            EconomyRejection rejection,
            int requestedAmount,
            int availableBalance,
            BalanceChanged change,
            bool eventPublished,
            bool isReplay)
        {
            Rejection = rejection;
            RequestedAmount = requestedAmount;
            AvailableBalance = availableBalance;
            Change = change;
            EventPublished = eventPublished;
            IsReplay = isReplay;
        }

        public bool Succeeded => Rejection == EconomyRejection.None;

        public EconomyRejection Rejection { get; }

        public int RequestedAmount { get; }

        public int AvailableBalance { get; }

        public BalanceChanged Change { get; }

        public bool EventPublished { get; }

        public bool IsReplay { get; }

        internal static EconomyMutationResult Success(BalanceChanged change, bool eventPublished) =>
            new EconomyMutationResult(EconomyRejection.None, 0, 0, change, eventPublished, false);

        internal static EconomyMutationResult Reject(
            EconomyRejection rejection,
            int requestedAmount = 0,
            int availableBalance = 0) =>
            new EconomyMutationResult(rejection, requestedAmount, availableBalance, null, false, false);

        internal EconomyMutationResult AsReplay() =>
            new EconomyMutationResult(
                Rejection,
                RequestedAmount,
                AvailableBalance,
                Change,
                EventPublished,
                true);
    }
}
