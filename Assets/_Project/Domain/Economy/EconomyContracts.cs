namespace VrGame.Domain.Economy
{
    public enum EconomyMutationKind
    {
        Credit = 1,
        Spend = 2
    }

    public enum EconomyRejection
    {
        None = 0,
        InvalidContext,
        InvalidAmount,
        InsufficientFunds,
        ArithmeticOverflow,
        ReentrantMutation,
        CommandConflict
    }
}
