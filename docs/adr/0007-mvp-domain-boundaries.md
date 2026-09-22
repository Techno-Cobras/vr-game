# ADR 0007: MVP domain boundaries and state ownership

- Status: Accepted
- Date: 2026-09-22
- Decision owners: gameplay architecture contributors
- Related issue: [#7](https://github.com/Techno-Cobras/vr-game/issues/7)

## Context

The MVP gameplay systems will be implemented in parallel, before an engine or XR
framework has been committed. They need stable, engine-independent boundaries so
that a scene object, UI presenter, or integration service cannot become a second
source of truth.

This decision covers items, inventory, plants, crafting, orders, economy, shop,
delivery, and indicators. It defines ownership and contracts, but not concrete
classes, engine assets, persistence, threading, or an event-bus implementation.

## Decision

### Layers and dependency direction

| Layer | Responsibility | May depend on |
| --- | --- | --- |
| DATA | Immutable, validated definitions and catalogs | Stable value types and other DATA definitions |
| DOMAIN / GAME LOGIC | Aggregates, services, command handlers, queries, events, and transaction coordinators | DATA contracts and narrow runtime-neutral ports such as time, random, event sink, and session transaction |
| VR INTERACTION | Converts input, grab, collision, and tool actions into one domain command; materializes physical representations | Public domain commands, queries, results, and events |
| UI / PRESENTATION | Read models, world-space indicators, feedback, and screens | Public domain queries, results, and events; commands only to express player intent |

The composition root may see every layer for construction and lifecycle wiring,
but contains no gameplay rules.

```text
VR INTERACTION -----+
                    +--> DOMAIN / GAME LOGIC --> DATA
UI / PRESENTATION --+             |
       ^                           |
       +------- results/events ----+
```

The following dependencies are forbidden:

- DOMAIN or DATA depending on engine, XR, scene, VR, or UI types;
- UI or VR adapters writing directly to an aggregate or state store;
- one domain system reaching into another system's internal state;
- DATA definitions containing mutable runtime state;
- events acting as hidden synchronous commands between state owners.

A cross-system workflow uses a named transaction coordinator and the owners'
narrow public contracts. A coordinator owns orchestration and idempotency only;
it never copies the balance, quantities, lifecycle, or queue it coordinates.

### State ownership

Every mutable field has exactly one authoritative owner.

| System | Authoritative owner | Owned state | Explicitly not authoritative |
| --- | --- | --- | --- |
| Items | Item Catalog | Immutable item definitions, categories, stack rules, and representation keys | Display names, scene objects, and physical item instances |
| Inventory | One Inventory aggregate per player or container, accessed through Inventory Service | `ItemId -> quantity`, optional capacity, and aggregate version | Physical objects, container UI, crafting, shop, or delivery presenters |
| Plants | One Planting Slot aggregate per slot; Plant Catalog owns definitions only | Occupancy, plant type, lifecycle, elapsed/progress, water threshold/request, fertilizer modifier, and version | Plant visuals, indicators, tools, and shared plant definitions |
| Crafting | Recipe Catalog owns definitions; Crafting Service coordinates a transaction | Only bounded command idempotency/in-flight state when required | Ingredient or output quantities, which remain owned by Inventory |
| Orders | Order Service and its Order aggregates | Generated orders, lifecycle, active order identity, and the single-active-order invariant | Order Board, waypoint, inventory quantities, and money |
| Economy | Economy Service | Non-negative integer balance, reasoned ledger entries, and command idempotency | UI text, orders, shop, or rewards configuration |
| Shop | Shop Catalog owns offers; Purchase Service coordinates a transaction | Immutable offers and bounded purchase-command idempotency respectively | Balance and delivery entries |
| Delivery | Delivery Queue | FIFO entries, item, quantity, status, order, version, and claim state | Delivery Box slots and spawned physical objects |
| Indicators | Indicator Presenter | Visual handle, bound target, and currently rendered variant | Water need, harvest readiness, active order, or delivery state |

Derived values are not duplicated state. For example, `ReadyForDelivery` is a
query over the active order and inventory, while indicator visibility is a
projection that can be rebuilt from queries and events.

### Stable identifiers

Definition identifiers are strongly typed and author-defined, for example
`ItemId`, `PlantTypeId`, `RecipeId`, `OrderTemplateId`, and `ShopOfferId`.
Runtime identifiers include `InventoryId`, `PlantingSlotId`, `OrderId`, and
`DeliveryEntryId`.

- Serialized definition IDs use canonical lowercase ASCII namespaced values,
  such as `item.seed.basil`, with ordinal case-sensitive comparison.
- An ID is unique within its type, immutable after publication, and never reused
  for a different meaning.
- Display names, collection positions, filesystem paths, scene paths, and engine
  instance IDs are never domain identifiers.
- Catalog validation rejects duplicate IDs, missing references, incompatible
  categories, and invalid definitions before gameplay starts.
- Runtime IDs are unique for the session. Persistence and save migration remain
  outside MVP scope until explicitly designed.
- Re-entry-prone commands carry a `CommandId`; related operations and events
  carry a `CorrelationId`. Repeating a completed `CommandId` returns the prior
  result and does not repeat mutation.

### Quantities and money

- Item quantity is an integer value: stored counts are at least zero and command
  inputs, production amounts, and consumption amounts are greater than zero.
- A missing item has quantity zero. Negative quantities and floating-point item
  counts are invalid; signed deltas are allowed only as event facts.
- Batch operations normalize duplicate item IDs, check arithmetic overflow,
  validate every input, and commit all changes or none.
- Currency is a separate non-negative integer minor-unit value. It is not an
  item quantity, and only Economy Service may mutate it.
- Growth progress in `[0, 1]`, elapsed time, duration, and modifiers use distinct
  value types and are never represented as item quantities.
- A physical representation normally represents one item unless its definition
  explicitly supports a stack representation. Authority remains with Inventory
  or a Delivery Queue entry, never with the transform or scene object.

### Commands, queries, and events

**Commands** express imperative intent and target one public application API.
Each command has exactly one handler, returns a typed success or rejection, and
changes nothing on rejection. Commands that can be produced twice by VR input
or callbacks use a `CommandId`; stale physical callbacks may also provide an
expected aggregate version. Command execution is serialized within a gameplay
session.

**Queries** are read-only and return immutable snapshots or read models. They do
not expose aggregates or mutable collections. A query never reserves, consumes,
or advances state.

**Events** are immutable, past-tense facts published only after a successful
commit. An event identifies its owner/entity, aggregate version or sequence,
reason, correlation/command ID, and the before/after value or delta required by
observers. Consumers tolerate replay and duplication. Events update projections
or schedule a later explicit command; they do not provide a backdoor for nested
owner mutation.

Representative events include `InventoryChanged`, `PlantStateChanged`,
`WaterRequested`, `PlantReady`, `CraftCompleted`, `OrderAccepted`,
`OrderCompleted`, `BalanceChanged`, `PurchaseCompleted`, and
`DeliveryEntryChanged`. Names are conceptual until an implementation language
and naming convention are committed.

### Public cross-system contracts

These are conceptual capability boundaries, not prescribed interface names:

| Capability | Commands | Queries/events |
| --- | --- | --- |
| Catalogs | Validate at startup | Resolve item, plant, recipe, order-template, and shop-offer definitions by stable ID |
| Inventory | Atomic transfer, add, consume, and transform | Quantity/capacity snapshot; `InventoryChanged` |
| Plants | Plant, advance time, water, apply fertilizer, harvest/reset through a coordinator | Slot snapshot; state, water, and readiness events |
| Crafting | Craft recipe once | Available recipes, requirements, typed result; `CraftCompleted` |
| Orders | Generate, accept, complete, or cancel according to lifecycle | Available/active order and readiness snapshots; lifecycle events |
| Economy | `TrySpend` and `Credit` with reason and command identity | Balance snapshot; `BalanceChanged` |
| Shop | Purchase an offer once | Offer catalog and typed purchase result |
| Delivery | Enqueue, mark available after materialization, and claim once | Ordered queue snapshot; `DeliveryEntryChanged` |
| Indicators | Show, rebind, reconcile, and hide visual projections | Consumes domain queries/events; emits no gameplay mutation |

Runtime-neutral ports include an injected time source, random source, domain
event sink, and session transaction boundary. Their concrete implementations are
deferred until the engine and execution model are selected.

### Atomic cross-owner workflows

The named handlers below prevalidate every participant, commit through a session
transaction/unit of work (or a proven no-fail commit under the serialized
dispatcher), and publish events only after the whole operation succeeds.

| Handler | Coordinated owners and commit rule |
| --- | --- |
| Plant Seed | Inventory consumes one seed and Planting Slot moves `Empty -> Growing`, or neither changes |
| Apply Fertilizer | Planting Slot accepts one modifier and Inventory consumes one fertilizer, or neither changes |
| Harvest | Inventory accepts the configured output before Planting Slot resets, or the ready plant remains intact |
| Craft Recipe | Inventory atomically transforms all recipe inputs into configured outputs |
| Deliver Order | Inventory consumes the exact medicine, Order completes once, and Economy credits once, or none changes |
| Purchase | Economy spends the exact price and Delivery Queue enqueues the exact product, or neither changes |
| Claim Delivery | Inventory accepts the item and Delivery Queue marks the entry claimed, or the entry remains retryable |

No generic `GameManager`, global mutable service locator, or aggregate spanning
all systems is permitted. New cross-system flows receive a narrow named handler
rather than expanding an existing coordinator into a God Object.

## Scenario ownership audit

The issue scenarios are walked through here to verify that every mutation has a
single owner.

### Scenario A: seed to harvested plant ([#32](https://github.com/Techno-Cobras/vr-game/issues/32))

1. The seed-station adapter requests a transfer between container and player
   Inventory owners only after successful handoff; a spawn failure loses nothing.
2. Plant Seed atomically consumes one seed in Inventory and moves the target
   Planting Slot from `Empty` to `Growing`. An occupied slot changes neither.
3. Only Planting Slot advances progress, selects and stores its water threshold,
   enters `NeedsWater`, applies one fertilizer modifier, reaches
   `ReadyToHarvest`, and resumes after valid watering.
4. Water and ready indicators merely project the slot snapshot/events.
5. Harvest atomically adds the configured output to Inventory and then resets
   the Planting Slot. A failed add preserves the ready plant.

### Scenario B: plants to medicine ([#33](https://github.com/Techno-Cobras/vr-game/issues/33))

1. The Crafting Table reads Recipe Catalog and an Inventory snapshot, then sends
   one craft command with `CommandId`, `RecipeId`, and `InventoryId`.
2. Crafting Service resolves immutable recipe data and asks the Inventory owner
   for one atomic input-to-output transform.
3. Missing ingredients, insufficient output capacity, or a duplicate command
   leaves all quantities unchanged. UI observes only the typed result/event.

### Scenario C: accepted order to payment ([#34](https://github.com/Techno-Cobras/vr-game/issues/34))

1. The Order Board sends `AcceptOrder`; only Order Service changes the active
   order and lifecycle. The waypoint projects that state.
2. A wrong or partial delivery is rejected without mutation.
3. Deliver Order atomically consumes exact medicine in Inventory, completes the
   Order once, and credits the reward through Economy Service once.
4. Repeated delivery returns a prior/already-completed result. The waypoint hides
   in response to the committed lifecycle, not by changing the order.

### Scenario D: tablet purchase to resource use ([#35](https://github.com/Techno-Cobras/vr-game/issues/35))

1. Tablet UI queries Shop Catalog and Economy, then sends one purchase command.
2. Purchase atomically spends through Economy Service and enqueues through
   Delivery Queue. Invalid products, insufficient funds, or duplicate commands
   do not create a partial spend or a second entry.
3. Delivery Box materializes pending entries, but Delivery Queue remains the
   source of truth when capacity or spawning fails.
4. Claim Delivery transfers the exact item to Inventory and marks the queue entry
   `Claimed` only after successful handoff; otherwise it stays retryable.
5. Planting or fertilizing later consumes the resource through the corresponding
   named handler, not through the physical representation.

### Scenario E: repeatable gameplay loop ([#36](https://github.com/Techno-Cobras/vr-game/issues/36))

Compose A, B, C, and D in one session. At every boundary assert exact Inventory
quantities, one Planting Slot lifecycle, one active/completed Order, the exact
Economy delta, and one Delivery Queue enqueue/claim. Begin the second cultivation
cycle with the purchased resource without direct state injection. Scenario E is
an ownership audit across existing handlers, not a new orchestrating service.

## Consequences

- Gameplay state remains testable without VR or engine objects.
- Parallel subsystem work can rely on stable ownership and transaction seams.
- UI, indicators, and physical objects can be destroyed and rebuilt without
  losing authoritative state.
- Cross-owner workflows require explicit transaction and idempotency design,
  which adds contracts but prevents partial state and duplicate rewards/items.
- Future implementation should add dependency checks proving that Domain has no
  engine/UI/VR references and that presentation cannot write state stores.

## Deferred decisions

Engine and version, XR runtime/framework, input phases, physics/lifecycle APIs,
threading, concrete serialization, persistence/save migration, transaction
implementation, target headset, and frame-time budgets are intentionally
unresolved. Later ADRs may map these boundaries to a chosen stack, but changing
state ownership requires an ADR that supersedes this one.
