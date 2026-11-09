# Shop catalog and atomic purchase (#28)

Build the existing `SeedStorageItemCatalog.asset`, then build
`Shop/Samples/ShopCatalog.asset` with the runtime item catalog. The five offers
reference existing Basil/Mint seeds and Cheap/Medium/Expensive fertilizer item
assets. Prices and quantities are configured on each ShopOffer asset.

Create one session EconomyService, PendingDeliveryLedger and PurchaseService.
Call Purchase with a product ID and a fresh EconomyChangeContext for each
intentional confirmation; retries reuse the entire context. Read Entry on success
and the ledger snapshot to recover delivery data if event publication fails.
Both money and pending delivery commit before callbacks; callback mutations are
rejected. No inventory item is created at purchase time.

The pending ledger deliberately only records purchases. Spawn/capacity/claim
lifecycle is #30/#31; tablet wiring is #29. Scene/bootstrap integration remains
with those features. The ledger and command replay cache are in-session only.

EditMode PurchaseServiceTests cover configured products, exact payment and
entry contents, insufficient funds, invalid product/context, replay/conflict,
ordered independent purchases, callback reentry, observer failure and direct
Economy command collisions. Run scripts/Test-UnityProject.ps1 with Editor
6000.3.25f1. Local Unity tests and smoke have NOT run: Editor is unavailable.
No Quest acceptance has been performed; this change introduces domain/data APIs.
