using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VrGame.Domain.Economy
{
    public sealed partial class EconomyService
    {
        private readonly IEconomyEventSink eventSink;
        private readonly List<BalanceChanged> ledger = new List<BalanceChanged>();
        private readonly Dictionary<CommandKey, CompletedCommand> completedCommands =
            new Dictionary<CommandKey, CompletedCommand>();
        private bool isPublishingEvent;
        private bool coordinatedMutation;

        public EconomyService(int startingBalance, IEconomyEventSink eventSink)
        {
            if (startingBalance < 0)
                throw new ArgumentOutOfRangeException(nameof(startingBalance), "Стартовый баланс не может быть отрицательным.");

            Balance = startingBalance;
            this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        }

        public int Balance { get; private set; }

        public long Version { get; private set; }

        public EconomySnapshot GetSnapshot() => new EconomySnapshot(Balance, Version);

        public IReadOnlyList<BalanceChanged> GetLedgerSnapshot() =>
            new ReadOnlyCollection<BalanceChanged>(ledger.ToList());

        public EconomyMutationResult Credit(int amount, EconomyChangeContext context) =>
            Execute(EconomyMutationKind.Credit, amount, context);

        public EconomyMutationResult TrySpend(int amount, EconomyChangeContext context) =>
            Execute(EconomyMutationKind.Spend, amount, context);

        internal bool TryAcquireCoordinatedMutation()
        {
            if (isPublishingEvent || coordinatedMutation)
                return false;
            coordinatedMutation = true;
            return true;
        }

        internal void ReleaseCoordinatedMutation() => coordinatedMutation = false;

        internal EconomyRejection ValidateCoordinatedSpend(int amount, EconomyChangeContext context)
        {
            if (!coordinatedMutation || !context.IsValid)
                return EconomyRejection.InvalidContext;
            if (amount <= 0)
                return EconomyRejection.InvalidAmount;
            // A purchase must never reuse a command already executed directly through Economy.
            if (completedCommands.ContainsKey(new CommandKey(EconomyMutationKind.Spend, context.CommandId)))
                return EconomyRejection.CommandConflict;
            if (amount > Balance)
                return EconomyRejection.InsufficientFunds;
            return Version == long.MaxValue ? EconomyRejection.ArithmeticOverflow : EconomyRejection.None;
        }

        internal BalanceChanged CommitCoordinatedSpend(int amount, EconomyChangeContext context)
        {
            var change = new BalanceChanged(Version + 1, EconomyMutationKind.Spend, Balance, Balance - amount, context);
            Balance -= amount;
            Version++;
            ledger.Add(change);
            completedCommands.Add(new CommandKey(EconomyMutationKind.Spend, context.CommandId), new CompletedCommand(amount, context, EconomyMutationResult.Success(change, false)));
            return change;
        }

        internal bool PublishCoordinatedSpend(BalanceChanged change)
        {
            var published = false;
            try
            {
                published = eventSink.TryPublish(change);
            }
            catch (Exception)
            {
            }

            var context = new EconomyChangeContext(change.Reason, change.CommandId, change.CorrelationId);
            completedCommands[new CommandKey(EconomyMutationKind.Spend, change.CommandId)] = new CompletedCommand(-change.Delta, context, EconomyMutationResult.Success(change, published));
            return published;
        }

        private EconomyMutationResult Execute(
            EconomyMutationKind kind,
            int amount,
            EconomyChangeContext context)
        {
            if (isPublishingEvent || coordinatedMutation)
                return EconomyMutationResult.Reject(EconomyRejection.ReentrantMutation, amount, Balance);
            if (!context.IsValid)
                return EconomyMutationResult.Reject(EconomyRejection.InvalidContext, amount, Balance);

            var key = new CommandKey(kind, context.CommandId);
            if (completedCommands.TryGetValue(key, out var completed))
            {
                if (!completed.Matches(amount, context))
                    return EconomyMutationResult.Reject(EconomyRejection.CommandConflict, amount, Balance);

                return completed.Result.AsReplay();
            }

            if (amount <= 0)
                return CompleteRejection(
                    key,
                    amount,
                    context,
                    EconomyMutationResult.Reject(EconomyRejection.InvalidAmount, amount, Balance));

            if (kind == EconomyMutationKind.Credit && amount > int.MaxValue - Balance)
                return CompleteRejection(
                    key,
                    amount,
                    context,
                    EconomyMutationResult.Reject(EconomyRejection.ArithmeticOverflow, amount, Balance));
            if (kind == EconomyMutationKind.Spend && amount > Balance)
                return CompleteRejection(
                    key,
                    amount,
                    context,
                    EconomyMutationResult.Reject(EconomyRejection.InsufficientFunds, amount, Balance));
            if (Version == long.MaxValue)
                return CompleteRejection(
                    key,
                    amount,
                    context,
                    EconomyMutationResult.Reject(EconomyRejection.ArithmeticOverflow, amount, Balance));

            var before = Balance;
            Balance = kind == EconomyMutationKind.Credit ? before + amount : before - amount;
            Version++;
            var balanceChanged = new BalanceChanged(Version, kind, before, Balance, context);
            ledger.Add(balanceChanged);

            var eventPublished = false;
            isPublishingEvent = true;
            try
            {
                eventPublished = eventSink.TryPublish(balanceChanged);
            }
            catch (Exception)
            {
                // Mutation уже зафиксирована. Событие остаётся в результате и ledger,
                // поэтому доставку можно повторить без повторной денежной операции.
            }
            finally
            {
                isPublishingEvent = false;
            }

            var result = EconomyMutationResult.Success(balanceChanged, eventPublished);
            completedCommands.Add(key, new CompletedCommand(amount, context, result));
            return result;
        }

        private EconomyMutationResult CompleteRejection(
            CommandKey key,
            int amount,
            EconomyChangeContext context,
            EconomyMutationResult result)
        {
            completedCommands.Add(key, new CompletedCommand(amount, context, result));
            return result;
        }

        private readonly struct CommandKey : IEquatable<CommandKey>
        {
            public CommandKey(EconomyMutationKind kind, Guid commandId)
            {
                Kind = kind;
                CommandId = commandId;
            }

            private EconomyMutationKind Kind { get; }

            private Guid CommandId { get; }

            public bool Equals(CommandKey other) => Kind == other.Kind && CommandId.Equals(other.CommandId);

            public override bool Equals(object obj) => obj is CommandKey other && Equals(other);

            public override int GetHashCode() => ((int)Kind * 397) ^ CommandId.GetHashCode();
        }

        private sealed class CompletedCommand
        {
            private readonly int amount;
            private readonly string reason;
            private readonly Guid correlationId;

            public CompletedCommand(int amount, EconomyChangeContext context, EconomyMutationResult result)
            {
                this.amount = amount;
                reason = context.Reason;
                correlationId = context.CorrelationId;
                Result = result;
            }

            public EconomyMutationResult Result { get; }

            public bool Matches(int candidateAmount, EconomyChangeContext context) =>
                amount == candidateAmount &&
                string.Equals(reason, context.Reason, StringComparison.Ordinal) &&
                correlationId == context.CorrelationId;
        }
    }
}
