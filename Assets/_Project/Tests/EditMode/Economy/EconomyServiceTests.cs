using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using VrGame.Domain.Economy;

namespace VrGame.Tests.EditMode.Economy
{
    public sealed class EconomyServiceTests
    {
        [Test]
        public void Constructor_ConfiguresReadOnlyStartingBalance()
        {
            var service = new EconomyService(125, new RecordingSink());
            var balanceProperty = typeof(EconomyService).GetProperty(nameof(EconomyService.Balance));

            Assert.That(service.Balance, Is.EqualTo(125));
            Assert.That(service.Version, Is.Zero);
            Assert.That(service.GetSnapshot().Ledger, Is.Empty);
            Assert.That(balanceProperty.SetMethod, Is.Not.Null);
            Assert.That(balanceProperty.SetMethod.IsPublic, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new EconomyService(-1, new RecordingSink()));
        }

        [Test]
        public void CreditAndExactSpend_ChangeBalanceAndPublishOnce()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(5, sink);

            var credit = service.Credit(7, Context("economy.reward", 1));
            var spend = service.TrySpend(12, Context("economy.purchase", 2));

            Assert.That(credit.Succeeded, Is.True);
            Assert.That(spend.Succeeded, Is.True);
            Assert.That(service.Balance, Is.Zero);
            Assert.That(service.Version, Is.EqualTo(2));
            Assert.That(sink.Events, Has.Count.EqualTo(2));
            Assert.That(sink.Events.Select(change => change.Version), Is.EqualTo(new[] { 1L, 2L }));
            Assert.That(sink.Events[0].Delta, Is.EqualTo(7));
            Assert.That(sink.Events[1].Delta, Is.EqualTo(-12));
        }

        [Test]
        public void TrySpend_InsufficientFundsDoesNotMutate()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(4, sink);

            var result = service.TrySpend(5, Context("economy.purchase", 1));

            Assert.That(result.Rejection, Is.EqualTo(EconomyRejection.InsufficientFunds));
            Assert.That(result.RequestedAmount, Is.EqualTo(5));
            Assert.That(result.AvailableBalance, Is.EqualTo(4));
            AssertUnchanged(service, sink, 4);
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidAmount_IsRejectedForCreditAndSpend(int amount)
        {
            var sink = new RecordingSink();
            var service = new EconomyService(10, sink);

            var credit = service.Credit(amount, Context("economy.credit", 1));
            var spend = service.TrySpend(amount, Context("economy.spend", 2));

            Assert.That(credit.Rejection, Is.EqualTo(EconomyRejection.InvalidAmount));
            Assert.That(spend.Rejection, Is.EqualTo(EconomyRejection.InvalidAmount));
            AssertUnchanged(service, sink, 10);
        }

        [Test]
        public void Credit_ArithmeticOverflowDoesNotMutate()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(int.MaxValue, sink);

            var result = service.Credit(1, Context("economy.reward", 1));

            Assert.That(result.Rejection, Is.EqualTo(EconomyRejection.ArithmeticOverflow));
            AssertUnchanged(service, sink, int.MaxValue);
        }

        [Test]
        public void InvalidContext_IsRejectedWithoutMutation()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(10, sink);

            var result = service.Credit(1, default);

            Assert.That(result.Rejection, Is.EqualTo(EconomyRejection.InvalidContext));
            AssertUnchanged(service, sink, 10);
        }

        [Test]
        public void Event_ContainsCommittedStateAndContext()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(10, sink);
            var context = Context("economy.order.reward", 42);
            var observedBalance = -1;
            sink.OnPublish = _ => observedBalance = service.Balance;

            var result = service.Credit(15, context);
            var changed = result.Change;

            Assert.That(result.EventPublished, Is.True);
            Assert.That(changed.Kind, Is.EqualTo(EconomyMutationKind.Credit));
            Assert.That(changed.Before, Is.EqualTo(10));
            Assert.That(changed.After, Is.EqualTo(25));
            Assert.That(changed.Delta, Is.EqualTo(15));
            Assert.That(changed.Reason, Is.EqualTo("economy.order.reward"));
            Assert.That(changed.CommandId, Is.EqualTo(context.CommandId));
            Assert.That(changed.CorrelationId, Is.EqualTo(context.CorrelationId));
            Assert.That(observedBalance, Is.EqualTo(25));
        }

        [Test]
        public void RepeatedCommand_ReturnsPreviousResultWithoutSecondMutation()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(0, sink);
            var context = Context("economy.reward", 1);

            var first = service.Credit(10, context);
            var replay = service.Credit(10, context);

            Assert.That(first.IsReplay, Is.False);
            Assert.That(replay.Succeeded, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(replay.Change, Is.SameAs(first.Change));
            Assert.That(service.Balance, Is.EqualTo(10));
            Assert.That(service.Version, Is.EqualTo(1));
            Assert.That(sink.Events, Has.Count.EqualTo(1));
            Assert.That(service.GetSnapshot().Ledger, Has.Count.EqualTo(1));
        }

        [Test]
        public void RejectedCommand_IsAlsoIdempotent()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(2, sink);
            var spendContext = Context("economy.purchase", 1);

            var rejected = service.TrySpend(3, spendContext);
            service.Credit(10, Context("economy.reward", 2));
            var replay = service.TrySpend(3, spendContext);

            Assert.That(rejected.Rejection, Is.EqualTo(EconomyRejection.InsufficientFunds));
            Assert.That(replay.Rejection, Is.EqualTo(EconomyRejection.InsufficientFunds));
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(service.Balance, Is.EqualTo(12));
            Assert.That(service.Version, Is.EqualTo(1));
            Assert.That(sink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void ReusedCommandWithDifferentPayload_IsRejectedAsConflict()
        {
            var sink = new RecordingSink();
            var service = new EconomyService(0, sink);
            var context = Context("economy.reward", 1);
            service.Credit(10, context);

            var conflict = service.Credit(11, context);

            Assert.That(conflict.Rejection, Is.EqualTo(EconomyRejection.CommandConflict));
            Assert.That(service.Balance, Is.EqualTo(10));
            Assert.That(service.Version, Is.EqualTo(1));
            Assert.That(sink.Events, Has.Count.EqualTo(1));
        }

        [Test]
        public void EventSinkFailure_DoesNotHideCommittedMutation()
        {
            var service = new EconomyService(0, new ThrowingSink());

            var result = service.Credit(2, Context("economy.reward", 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.EventPublished, Is.False);
            Assert.That(result.Change.After, Is.EqualTo(2));
            Assert.That(service.Balance, Is.EqualTo(2));
            Assert.That(service.Version, Is.EqualTo(1));
        }

        [Test]
        public void EventSink_CannotMutateEconomyReentrantly()
        {
            var sink = new ReentrantSink();
            var service = new EconomyService(10, sink);
            sink.Service = service;

            var result = service.Credit(2, Context("economy.reward", 1));

            Assert.That(result.Succeeded, Is.True);
            Assert.That(sink.CreditResult.Rejection, Is.EqualTo(EconomyRejection.ReentrantMutation));
            Assert.That(sink.SpendResult.Rejection, Is.EqualTo(EconomyRejection.ReentrantMutation));
            Assert.That(service.Balance, Is.EqualTo(12));
            Assert.That(service.Version, Is.EqualTo(1));
            Assert.That(sink.PublishedVersions, Is.EqualTo(new[] { 1L }));
        }

        [Test]
        public void Snapshot_LedgerIsReadOnlyAndIndependent()
        {
            var service = new EconomyService(0, new RecordingSink());
            service.Credit(2, Context("economy.reward", 1));
            var snapshot = service.GetSnapshot();
            service.Credit(3, Context("economy.reward", 2));

            Assert.That(snapshot.Balance, Is.EqualTo(2));
            Assert.That(snapshot.Version, Is.EqualTo(1));
            Assert.That(snapshot.Ledger, Has.Count.EqualTo(1));
            Assert.Throws<NotSupportedException>(() =>
                ((System.Collections.IList)snapshot.Ledger).Add(snapshot.Ledger[0]));
            Assert.That(service.GetSnapshot().Ledger, Has.Count.EqualTo(2));
        }

        private static EconomyChangeContext Context(string reason, int seed)
        {
            var commandId = new Guid(seed, 0, 0, new byte[8]);
            var correlationId = new Guid(seed, 1, 0, new byte[8]);
            return new EconomyChangeContext(reason, commandId, correlationId);
        }

        private static void AssertUnchanged(EconomyService service, RecordingSink sink, int expectedBalance)
        {
            Assert.That(service.Balance, Is.EqualTo(expectedBalance));
            Assert.That(service.Version, Is.Zero);
            Assert.That(service.GetSnapshot().Ledger, Is.Empty);
            Assert.That(sink.Events, Is.Empty);
        }

        private sealed class RecordingSink : IEconomyEventSink
        {
            public List<BalanceChanged> Events { get; } = new List<BalanceChanged>();

            public Action<BalanceChanged> OnPublish { get; set; }

            public bool TryPublish(BalanceChanged balanceChanged)
            {
                Events.Add(balanceChanged);
                OnPublish?.Invoke(balanceChanged);
                return true;
            }
        }

        private sealed class ThrowingSink : IEconomyEventSink
        {
            public bool TryPublish(BalanceChanged balanceChanged) =>
                throw new InvalidOperationException("Test publication failure.");
        }

        private sealed class ReentrantSink : IEconomyEventSink
        {
            public EconomyService Service { get; set; }

            public EconomyMutationResult CreditResult { get; private set; }

            public EconomyMutationResult SpendResult { get; private set; }

            public List<long> PublishedVersions { get; } = new List<long>();

            public bool TryPublish(BalanceChanged balanceChanged)
            {
                PublishedVersions.Add(balanceChanged.Version);
                CreditResult = Service.Credit(1, Context("economy.reentrant.credit", 98));
                SpendResult = Service.TrySpend(1, Context("economy.reentrant.spend", 99));
                return true;
            }
        }
    }
}
