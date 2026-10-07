using System;
using System.Collections.Generic;

namespace VrGame.Domain.Economy
{
    public sealed class EconomyService
    {
        private readonly IEconomyEventSink eventSink;
        private readonly List<BalanceChanged> ledger = new List<BalanceChanged>();
        private readonly Dictionary<CommandKey, CompletedCommand> completedCommands =
            new Dictionary<CommandKey, CompletedCommand>();
        private bool isPublishingEvent;

        public EconomyService(int startingBalance, IEconomyEventSink eventSink)
        {
            if (startingBalance < 0)
                throw new ArgumentOutOfRangeException(nameof(startingBalance), "Стартовый баланс не может быть отрицательным.");

            Balance = startingBalance;
            this.eventSink = eventSink ?? throw new ArgumentNullException(nameof(eventSink));
        }

        public int Balance { get; private set; }

        public long Version { get; private set; }

        public EconomySnapshot GetSnapshot() => new EconomySnapshot(Balance, Version, ledger);

        public EconomyMutationResult Credit(int amount, EconomyChangeContext context) =>
            Execute(EconomyMutationKind.Credit, amount, context);

        public EconomyMutationResult TrySpend(int amount, EconomyChangeContext context) =>
            Execute(EconomyMutationKind.Spend, amount, context);

        private EconomyMutationResult Execute(
            EconomyMutationKind kind,
            int amount,
            EconomyChangeContext context)
        {
            if (isPublishingEvent)
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
