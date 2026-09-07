# Development roadmap

This roadmap groups work by independently deliverable player capabilities. It is not a release-readiness claim. The approved live runtime and its exact version are pinned in the [README](README.md#development-and-validation-with-sdvkit).

## 1. A usable interior selection menu

Replace the prototype renderer with StardewUI Continued while preserving the existing selection and safety path. Players should be able to identify a building, compare compatible interiors, inspect a preview, and explicitly apply a choice without confusing the previewed option with the saved selection.

- Keep F8 and `sic menu [buildingId]` as entry points.
- Separate building navigation, variant selection, preview information, and the Apply/status area.
- Keep Base interior first and retain missing-pack, changed-hash, and invalid-save warnings.
- Preserve host authority, pending-request handling, and safe rejection through the existing Core and submission path.
- Ship view assets and document the released framework dependency.

Acceptance requires a packaged build and a public SDVKit single-player review of mouse, keyboard, controller A/B, scrolling, compact layout, preview fallback, successful Apply, and occupancy rejection. Inspect actual viewport captures. Previous native-menu evidence does not validate the replacement renderer. Multiplayer UI acceptance remains separate and requires an explicitly requested `network-2` review.

## 2. An installable first interior pack

Deliver one small, original pack that a player can install and select. The existing schema example remains non-installable and the smoke fixture remains test data until a separately reviewed pack is ready.

Acceptance includes a validated map contract, packaged assets, preview, author instructions, and a real change/save/restart/Base-restore cycle. Third-party interiors require their author's permission and a documented adapter contract; ordinary Content Patcher replacements are not automatically selectable variants.

## 3. Verified multiplayer failure behavior

Complete the missing-pack, gameplay-hash mismatch, peer-without-Core, and delayed-handshake cases against released tooling. Reuse accepted unchanged positive-path evidence; verify changed menu behavior separately.

Acceptance must distinguish a rejected request, unchanged stored selection, fallback/quarantine behavior, and blocked unsafe entry. Two-process tests run only when explicitly requested. Unit tests alone do not close these live gates.

## 4. Additional building support

Evaluate one additional building type at a time after the first two capabilities are accepted. Each target needs its own map contract, persistent-state inventory, independent building identity where applicable, content fixture, and save/restart proof.

Farmhouse conversions and migration of occupied interiors are separate design work. The current empty-interior requirement must not be relaxed as an incidental part of UI or target expansion.
