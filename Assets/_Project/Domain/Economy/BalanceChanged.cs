using System;

namespace VrGame.Domain.Economy
{
    public sealed class BalanceChanged
    {
        internal BalanceChanged(
            long version,
            EconomyMutationKind kind,
            int before,
            int after,
            EconomyChangeContext context)
        {
            Version = version;
            Kind = kind;
            Before = before;
            After = after;
            Reason = context.Reason;
            CommandId = context.CommandId;
            CorrelationId = context.CorrelationId;
        }

        public long Version { get; }

        public EconomyMutationKind Kind { get; }

        public int Before { get; }

        public int After { get; }

        public int Delta => After - Before;

        public string Reason { get; }

        public Guid CommandId { get; }

        public Guid CorrelationId { get; }
    }
}
