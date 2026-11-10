using System;

namespace VrGame.Domain.Economy
{
    public sealed partial class EconomyService
    {
        internal bool TryAcquireCoordinatedMutation()
        {
            if (isPublishingEvent)
                return false;
            isPublishingEvent = true;
            return true;
        }

        internal void ReleaseCoordinatedMutation() => isPublishingEvent = false;

        internal EconomyRejection TryPrepareCoordinatedCredit(
            int amount,
            EconomyChangeContext context,
            out PreparedEconomyCredit prepared)
        {
            prepared = default;
            if (!context.IsValid)
                return EconomyRejection.InvalidContext;
            if (amount <= 0)
                return EconomyRejection.InvalidAmount;
            if (amount > int.MaxValue - Balance || Version == long.MaxValue)
                return EconomyRejection.ArithmeticOverflow;

            var key = new CommandKey(EconomyMutationKind.Credit, context.CommandId);
            if (completedCommands.ContainsKey(key))
                return EconomyRejection.CommandConflict;

            prepared = new PreparedEconomyCredit(amount, Balance, Version, context);
            return EconomyRejection.None;
        }

        internal EconomyRejection TryPrepareCoordinatedSpend(
            int amount,
            EconomyChangeContext context,
            out PreparedEconomySpend prepared)
        {
            prepared = default;
            if (!context.IsValid)
                return EconomyRejection.InvalidContext;
            if (amount <= 0)
                return EconomyRejection.InvalidAmount;
            if (amount > Balance)
                return EconomyRejection.InsufficientFunds;
            if (Version == long.MaxValue)
                return EconomyRejection.ArithmeticOverflow;

            var key = new CommandKey(EconomyMutationKind.Spend, context.CommandId);
            if (completedCommands.ContainsKey(key))
                return EconomyRejection.CommandConflict;

            prepared = new PreparedEconomySpend(amount, Balance, Version, context);
            return EconomyRejection.None;
        }

        internal BalanceChanged CommitPreparedCredit(PreparedEconomyCredit prepared)
        {
            if (Balance != prepared.Before || Version != prepared.ExpectedVersion)
                throw new InvalidOperationException("Подготовленное начисление Economy устарело.");

            Balance = prepared.Before + prepared.Amount;
            Version++;
            var change = new BalanceChanged(
                Version,
                EconomyMutationKind.Credit,
                prepared.Before,
                Balance,
                prepared.Context);
            ledger.Add(change);
            return change;
        }

        internal BalanceChanged CommitPreparedSpend(PreparedEconomySpend prepared)
        {
            if (Balance != prepared.Before || Version != prepared.ExpectedVersion)
                throw new InvalidOperationException("Подготовленное списание Economy устарело.");

            Balance = prepared.Before - prepared.Amount;
            Version++;
            var change = new BalanceChanged(
                Version,
                EconomyMutationKind.Spend,
                prepared.Before,
                Balance,
                prepared.Context);
            ledger.Add(change);
            return change;
        }

        internal bool PublishPrepared(BalanceChanged change)
        {
            try
            {
                return eventSink.TryPublish(change);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal EconomyMutationResult CompletePreparedCommand(
            PreparedEconomyCredit prepared,
            BalanceChanged change,
            bool eventPublished)
        {
            var result = EconomyMutationResult.Success(change, eventPublished);
            var key = new CommandKey(EconomyMutationKind.Credit, prepared.Context.CommandId);
            completedCommands.Add(key, new CompletedCommand(prepared.Amount, prepared.Context, result));
            return result;
        }

        internal EconomyMutationResult CompletePreparedCommand(
            PreparedEconomySpend prepared,
            BalanceChanged change,
            bool eventPublished)
        {
            var result = EconomyMutationResult.Success(change, eventPublished);
            var key = new CommandKey(EconomyMutationKind.Spend, prepared.Context.CommandId);
            completedCommands.Add(key, new CompletedCommand(prepared.Amount, prepared.Context, result));
            return result;
        }

        internal readonly struct PreparedEconomyCredit
        {
            public PreparedEconomyCredit(
                int amount,
                int before,
                long expectedVersion,
                EconomyChangeContext context)
            {
                Amount = amount;
                Before = before;
                ExpectedVersion = expectedVersion;
                Context = context;
            }

            public int Amount { get; }
            public int Before { get; }
            public long ExpectedVersion { get; }
            public EconomyChangeContext Context { get; }
        }

        internal readonly struct PreparedEconomySpend
        {
            public PreparedEconomySpend(
                int amount,
                int before,
                long expectedVersion,
                EconomyChangeContext context)
            {
                Amount = amount;
                Before = before;
                ExpectedVersion = expectedVersion;
                Context = context;
            }

            public int Amount { get; }
            public int Before { get; }
            public long ExpectedVersion { get; }
            public EconomyChangeContext Context { get; }
        }
    }
}
