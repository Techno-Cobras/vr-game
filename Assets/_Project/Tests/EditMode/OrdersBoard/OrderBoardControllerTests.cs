using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using VrGame.Bootstrap;
using VrGame.Data.Items;
using VrGame.Data.Orders;
using VrGame.Domain.Orders;
using VrGame.Presentation.Indicators;
using VrGame.Presentation.Orders;

namespace VrGame.Tests.EditMode.OrdersBoard
{
    public sealed class OrderBoardControllerTests
    {
        private static readonly ItemId Tonic = new ItemId("item.medicine.tonic");
        private static readonly ItemId Blend = new ItemId("item.medicine.blend");
        private readonly List<GameObject> objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var gameObject in objects)
                if (gameObject != null)
                    UnityEngine.Object.DestroyImmediate(gameObject);
            objects.Clear();
        }

        [Test]
        public void Configure_BuildsStableAuthoritativeListWithExactDetails()
        {
            var fixture = CreateFixture();

            var snapshot = fixture.Controller.Snapshot;

            Assert.That(snapshot.Orders, Has.Count.EqualTo(2));
            Assert.That(snapshot.Orders[0].DisplayName, Is.EqualTo("Заказ на настой"));
            Assert.That(snapshot.Orders[0].MedicineDisplayName, Is.EqualTo("Настой базилика"));
            Assert.That(snapshot.Orders[0].Quantity, Is.EqualTo(1));
            Assert.That(snapshot.Orders[0].Reward, Is.EqualTo(25));
            Assert.That(snapshot.Orders[1].MedicineDisplayName, Is.EqualTo("Травяная смесь"));
            Assert.That(snapshot.ActiveOrder, Is.Null);
            Assert.That(snapshot.CanAcceptSelected, Is.True);
            Assert.That(fixture.View.LastSnapshot, Is.SameAs(snapshot));
        }

        [Test]
        public void AcceptAndSameFrameDuplicate_CreateOneMutationAndPersistentActiveOrder()
        {
            var fixture = CreateFixture();
            var version = fixture.Service.Version;

            var accepted = fixture.Controller.AcceptSelected();
            var duplicate = fixture.Controller.AcceptSelected();

            Assert.That(accepted.Succeeded, Is.True);
            Assert.That(duplicate, Is.SameAs(accepted));
            Assert.That(fixture.Service.Version, Is.EqualTo(version + 1));
            Assert.That(fixture.EventCount, Is.EqualTo(1));
            Assert.That(fixture.Controller.Snapshot.ActiveOrder, Is.Not.Null);
            Assert.That(fixture.Controller.Snapshot.ActiveOrder.OrderId, Is.EqualTo(accepted.TargetOrderId.Value));
            Assert.That(fixture.Controller.Snapshot.Feedback, Is.EqualTo(OrderBoardFeedback.Accepted));

            fixture.Host.SetActive(false);
            fixture.Host.SetActive(true);
            Assert.That(fixture.Controller.Snapshot.ActiveOrder.OrderId, Is.EqualTo(accepted.TargetOrderId.Value));
        }

        [Test]
        public void SecondOrderWhileActive_IsBlockedWithExplanationAndNoMutation()
        {
            var fixture = CreateFixture();
            fixture.Controller.AcceptSelected();
            fixture.Controller.SelectNextOrder();
            var version = fixture.Service.Version;

            var blocked = fixture.Controller.AcceptSelected();

            Assert.That(blocked, Is.Null);
            Assert.That(fixture.Service.Version, Is.EqualTo(version));
            Assert.That(fixture.Controller.Snapshot.Feedback, Is.EqualTo(OrderBoardFeedback.ActiveOrderExists));
            Assert.That(fixture.Controller.Snapshot.CanAcceptSelected, Is.False);
        }

        [Test]
        public void PauseFocusAndDisable_BlockCommandsAndResetTransientIntent()
        {
            var fixture = CreateFixture();
            var version = fixture.Service.Version;

            fixture.Controller.SetApplicationPaused(true);
            Assert.That(fixture.Controller.AcceptSelected(), Is.Null);
            fixture.Controller.SetApplicationPaused(false);
            fixture.Controller.SetApplicationFocused(false);
            Assert.That(fixture.Controller.AcceptSelected(), Is.Null);
            fixture.Controller.SetApplicationFocused(true);
            fixture.Host.SetActive(false);
            Assert.That(fixture.Controller.AcceptSelected(), Is.Null);
            Assert.That(fixture.Service.Version, Is.EqualTo(version));
        }

        [Test]
        public void DeliveryProjection_FollowsAuthoritativeActiveOrderAndCompletion()
        {
            var fixture = CreateFixture();
            var targetObject = Track(new GameObject("Delivery target"));
            var target = targetObject.AddComponent<IndicatorTarget>();
            var sourceObject = Track(new GameObject("Delivery source"));
            var source = sourceObject.AddComponent<OrderDeliveryIndicatorProjectionSource>();
            source.Configure(fixture.Service, fixture.Events, target);

            Assert.That(source.GetSnapshot().Single().Visible, Is.False);
            var accepted = fixture.Controller.AcceptSelected();
            Assert.That(source.GetSnapshot().Single().Visible, Is.True);

            var completed = fixture.Service.Complete(
                accepted.TargetOrderId.Value,
                Context("доставка"));
            Assert.That(completed.Succeeded, Is.True);
            Assert.That(source.GetSnapshot().Single().Visible, Is.False);
            Assert.That(fixture.Controller.Snapshot.ActiveOrder, Is.Null);
        }

        [Test]
        public void CompositeSource_MergesPlantAndDeliveryAndIsolatesSubscriberExceptions()
        {
            var targetA = Track(new GameObject("A")).AddComponent<IndicatorTarget>();
            var targetB = Track(new GameObject("B")).AddComponent<IndicatorTarget>();
            var plant = new FakeProjectionSource(new IndicatorProjection(
                new IndicatorKey("plant.ready"), IndicatorVariant.ReadyToHarvest, targetA, true));
            var delivery = new FakeProjectionSource(new IndicatorProjection(
                new IndicatorKey("order.delivery"), IndicatorVariant.Delivery, targetB, false));
            using (var composite = new CompositeIndicatorProjectionSource(plant, delivery))
            {
                var received = 0;
                composite.ProjectionChanged += _ => throw new InvalidOperationException("observer");
                composite.ProjectionChanged += _ => received++;

                delivery.Emit(new IndicatorProjection(
                    new IndicatorKey("order.delivery"), IndicatorVariant.Delivery, targetB, true));

                Assert.That(received, Is.EqualTo(1));
                Assert.That(composite.GetSnapshot().Select(value => value.Key.Value),
                    Is.EquivalentTo(new[] { "plant.ready", "order.delivery" }));
            }
        }

        private Fixture CreateFixture()
        {
            var items = new ItemCatalog(new[]
            {
                Item(Tonic, "Настой базилика"),
                Item(Blend, "Травяная смесь")
            });
            var templates = new OrderTemplateCatalog(items, new[]
            {
                new OrderTemplateDefinition(
                    new OrderTemplateId("order-template.a-tonic"),
                    "Заказ на настой",
                    Tonic,
                    1,
                    25),
                new OrderTemplateDefinition(
                    new OrderTemplateId("order-template.b-blend"),
                    "Заказ на смесь",
                    Blend,
                    2,
                    60)
            });
            var events = new OrderEventRelay();
            var eventCount = 0;
            events.Changed += _ => eventCount++;
            var service = new OrderService(
                templates,
                new SequenceSelector(0, 1),
                new SequenceIdSource(
                    new OrderId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
                    new OrderId(Guid.Parse("22222222-2222-2222-2222-222222222222"))),
                events);
            Assert.That(service.Generate(Context("первый")).Succeeded, Is.True);
            Assert.That(service.Generate(Context("второй")).Succeeded, Is.True);
            eventCount = 0;

            var host = Track(new GameObject("Order Board test"));
            var controller = host.AddComponent<OrderBoardController>();
            var view = new RecordingView();
            controller.Configure(service, items, events, view);
            return new Fixture(host, controller, service, events, view, () => eventCount);
        }

        private GameObject Track(GameObject gameObject)
        {
            objects.Add(gameObject);
            return gameObject;
        }

        private static ItemDefinition Item(ItemId id, string displayName) => new ItemDefinition(
            id,
            displayName,
            ItemCategory.Medicine,
            new ItemRepresentationKey("representation." + id.Value),
            new ItemStackRules(16, true));

        private static OrderContext Context(string reason) =>
            new OrderContext(reason, Guid.NewGuid(), Guid.NewGuid());

        private sealed class RecordingView : IOrderBoardView
        {
            public OrderBoardSnapshot LastSnapshot { get; private set; }
            public void Render(OrderBoardSnapshot snapshot) => LastSnapshot = snapshot;
        }

        private sealed class SequenceSelector : IOrderTemplateSelector
        {
            private readonly Queue<int> values;
            public SequenceSelector(params int[] values) => this.values = new Queue<int>(values);
            public int SelectIndex(int exclusiveMax) => values.Dequeue();
        }

        private sealed class SequenceIdSource : IOrderIdSource
        {
            private readonly Queue<OrderId> values;
            public SequenceIdSource(params OrderId[] values) => this.values = new Queue<OrderId>(values);
            public OrderId NextId() => values.Dequeue();
        }

        private sealed class FakeProjectionSource : IIndicatorProjectionSource
        {
            private IndicatorProjection projection;
            public FakeProjectionSource(IndicatorProjection projection) => this.projection = projection;
            public event Action<IndicatorProjection> ProjectionChanged;
            public IReadOnlyList<IndicatorProjection> GetSnapshot() => new[] { projection };
            public void Emit(IndicatorProjection value)
            {
                projection = value;
                ProjectionChanged?.Invoke(value);
            }
        }

        private sealed class Fixture
        {
            private readonly Func<int> eventCount;
            public Fixture(
                GameObject host,
                OrderBoardController controller,
                OrderService service,
                OrderEventRelay events,
                RecordingView view,
                Func<int> eventCount)
            {
                Host = host;
                Controller = controller;
                Service = service;
                Events = events;
                View = view;
                this.eventCount = eventCount;
            }
            public GameObject Host { get; }
            public OrderBoardController Controller { get; }
            public OrderService Service { get; }
            public OrderEventRelay Events { get; }
            public RecordingView View { get; }
            public int EventCount => eventCount();
        }
    }
}
