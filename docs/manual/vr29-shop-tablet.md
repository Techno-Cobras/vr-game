# VR shop tablet (#29)

Startup composition builds shared Economy, PurchaseService and pending delivery ledger
from the configured Shop catalog; starting balance is serialized (100 by default).
A runtime factory creates one wrist canvas on the serialized Left Controller anchor.
This intentionally avoids needing generated tablet prefab data before Startup can use it.
The Editor scene builder mirrors the serialized references. Existing XR UI Press selects
the always-visible Магазин tab and buttons; no new bindings or XR/EventSystem owners.
Left-hand tracking loss hides the canvas and blocks commands. Pause/focus loss closes it.

Quest 3 walkthrough: open Startup, raise left wrist, select Магазин with right XR ray.
Verify five products with quantities/prices and current balance. Navigate, confirm an
affordable product, and verify exact one spend/pending entry and success feedback.
Repeat confirmation callbacks in the same frame: one purchase. Separate intentional
clicks in later frames may each purchase. Spend down then select an unaffordable offer:
no spend/entry, distinct insufficient-funds feedback. External economy credits refresh
balance via events. Close/reopen; suspend/resume and lose tracking: no commands until
focus/tracking return, panel stays closed. Check ray readability and wrist orientation.
Record commit/APK/device and expected/actual balance/queue counts. Physical delivery #31
is separate; purchases only enqueue resources here.

EditMode controller tests and PlayMode Startup wiring tests added. Unity imports,
EditMode/PlayMode, smoke, Android build and Quest walkthrough NOT RUN: pinned Editor
6000.3.25f1 and device unavailable. Runtime UI layout/pose must be verified in headset.
