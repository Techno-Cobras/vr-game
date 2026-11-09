# Reliable delivery queue (#30)

PendingDeliveryLedger is the authoritative in-session queue shared with PurchaseService.
Snapshots are immutable and retain original purchase sequence, including Claimed history.
TryGetNextPending selects the earliest Pending entry. Identical purchases have distinct IDs.
Added remains compatible with purchase observers; Changed publishes additions and lifecycle
transitions, isolating each failing observer. Re-read snapshots after missed notifications.

Delivery Box adapter contract (#31): reserve a free representation slot, stage materialization,
then call MarkAvailable only after successful setup. Capacity exhaustion or failed staging
must leave the entry Pending and roll back the staged representation. Never mark an entry
available merely because a spawn attempt started. Retrying MarkAvailable on Available is
idempotent; Claimed cannot become Available again. Physical ownership, slots and rollback
belong to the adapter; this domain change does not create scene objects.

Verify and stage the player handoff without releasing ownership, then call Claim with entry ID, player Inventory and a valid
InventoryChangeContext with nonempty CommandId. Queue and inventory commit before callbacks.
Finalize the handoff and release the physical representation only after Claim succeeds. An add rejection leaves Available; retain or restore the representation and retry with the same command after fixing the cause.
A successful exact command replay returns replay without another addition; changing payload
with that command rejects. A fresh command against Claimed also rejects. The adapter must
reconcile its representation from the returned result/snapshot and avoid crediting Inventory
itself. Claims can follow player selection among Available objects; FIFO controls spawn
selection, not forced pickup order. All data is session-only; persistence is out of scope.

EditMode DeliveryQueueTests covers duplicate purchases, ordering, snapshot immutability,
lifecycle validation, claim replay/conflicts, overflow retry, callback consistency and failure.
Run scripts/Test-UnityProject.ps1 with Unity 6000.3.25f1. Unity tests and device acceptance
have NOT run locally because the pinned Editor/device are unavailable. Engine-free domain
compilation is supplemental and does not validate asset imports or physical delivery.
