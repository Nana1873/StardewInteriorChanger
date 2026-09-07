# Selection UI

The selection menu uses StardewUI Continued as a separately installed dependency. Interior Changer owns the view and selection model; the framework handles layout, rendering, scrolling, and focus navigation. The entry points remain F8 and `sic menu [buildingId]`.

## Dependency

- Mod ID: `focustense.StardewUI`.
- Integration baseline: [0.6.4-unofficial-mushymato.0](https://github.com/Mushymato/StardewUI/releases/tag/0.6.4-unofficial-mushymato.0).
- GitHub release asset: `StardewUI.0.6.4-unofficial-mushymato.0.zip`; SHA-256: `27e5e3223f14cc2a990155870f0de67b7ae63fef9721793c2aaaad6ce9fa87ce`. This identifies the GitHub binary asset, not a source archive or a separately downloaded Nexus file.
- The release requires Stardew Valley 1.6.15 and SMAPI 4.3.2 or later, compatible with this project's README-pinned game contract.
- The framework and its API contract are [MIT licensed](https://github.com/Mushymato/StardewUI/blob/0.6.4-unofficial-mushymato.0/LICENSE.txt). The mod package includes the upstream license notice in `licenses/StardewUI.txt`. The framework binaries are not redistributed in the mod package.

Use the continued fork's release rather than the old upstream download links still present in some framework documentation. The code communicates through SMAPI's public mod API and packages its own StarML view assets.

## Behavior that must survive UI changes

Choosing a building or variant changes only the local selection. Apply enters the existing host-authorized submission path. The view must never write saved selections, bypass occupancy checks, recalculate gameplay hashes, or implement an alternative multiplayer protocol.

Base interior remains first. Current markers require an exact saved variant/hash match. A missing or changed saved choice remains a warning. A missing or broken cosmetic preview must not disable an otherwise valid interior.

Pending requests disable further submission. Closing and reopening the menu must retain the request state and route the eventual result to the current menu session. A replacement renderer has a different `IClickableMenu` instance, so session identity and rendered-menu identity must be handled explicitly.

## Validation

Use the released framework folder as an explicit SDVKit review companion, together with the canonical smoke content pack. Follow the public `sdv-project-review` and `sdv-project-smoke` skills. All downloaded dependencies, packages, saves, logs, and screenshots belong under `.sdvkit/`.

Required single-player checks cover opening through both entry points, keyboard and controller focus, clean A/B handling, wheel scrolling with enough rows, compact and default viewport layouts, preview fallback, explicit Apply, current-state refresh, and a real occupancy rejection. Inspect actual viewport PNGs after AlwaysOn confirms capture. Build success does not prove StarML bindings or in-game layout.

Multiplayer UI behavior requires separate explicitly requested validation. Do not infer it from single-player results or from the previous native renderer's acceptance.

The current published SDVKit `project smoke --help` exposes only project path, game path, topology, and JSON output. It cannot accept a companion for the required StardewUI mod. This is an open tooling gate; interactive review with an explicit companion does not replace bounded smoke acceptance. Companion staging belongs in SDVKit and must not be reimplemented in this repository.

## Current acceptance status

The September 7, 2026 single-player review verified game-side loading, F8 and console opening, the building label, visible wheel scrolling, an actual preview, the no-preview placeholder, a successful mouse Apply with the updated Current marker, and an occupancy rejection. The final 1280x720 view fits its close button and action area inside the viewport. A final binding check confirmed dynamic preview updates and zero matching Core or StardewUI diagnostics, followed by successful stop/reset. The unchanged Core suite passed all 45 tests. The mod builds and packages with its view and license files.

This is not full UI acceptance. Controller A navigation remains inconclusive because the public inspection surface did not expose its focus target. One synthetic Controller B action closed this menu and opened Stardew's inventory; a neutral vanilla-menu comparison also failed to produce a clean close through that surface, so controller acceptance is blocked by the current review input behavior. Complete controller/keyboard navigation, compact viewport layout, multiple-building selection, failed-preview behavior, and multiplayer pending/reopen behavior still need their appropriate live checks. The required-dependency smoke gate described above is also open.
