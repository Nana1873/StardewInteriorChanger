# Farmhouse foundation handoff

## Status

This branch preserves a game-free contract foundation for possible future main-farmhouse support. Farmhouse selection is **not enabled**. The project does not register a farmhouse target, expose it in the menu or content-pack schema, store farmhouse selections, patch the game lifecycle, or load a custom farmhouse map.

Active development has stopped. This document records the boundary for an owner or fork; it is not a commitment to complete the feature.

## What the foundation provides

`FarmHouseStateContract` accepts immutable observations supplied by a future runtime adapter and checks:

- the exact canonical main `FarmHouse`, master-player ownership, and farm association;
- upgrade levels 0 through 3 with no upgrade transition in progress;
- no NPC spouse or roommate room and no children;
- default built-in renovation and crib state for upgrade levels 2 and 3;
- a valid assigned cellar for upgrade level 3;
- the Farm exit, entry, bed, kitchen, exact fridge sprite, child-bed, crib, and rewritten cellar-return anchors required by the applicable tier;
- declared and effective wallpaper/floor regions, including saved latent region IDs which have no active tiles in the closed renovation state; and
- structural, realized overlay, anchor, and decoration fingerprints compared with a caller-provided vanilla baseline.

The result distinguishes visual topology equivalence from a layout change. This is classification only: every accepted new selection still reports `RequiresVacantLayout = true`. No occupied-house exception exists in this foundation, even for a visual skin. A separate reviewed occupancy policy would be required before any runtime could retain furniture, decorations, machines, objects, players, or other occupants.

## Trust and evidence boundary

Core does not inspect Stardew objects or maps. A future runtime would need to prove the identity flags, state values, map anchors, decoration sets, and fingerprints before calling this policy. The unit tests use synthetic snapshots based on the audited Stardew Valley 1.6.15 anchors. They prove the policy decisions and fail-closed cases; they do not prove a runtime extractor, Harmony integration, save compatibility, or in-game behavior.

The exact vanilla lifecycle and map research used for the proposal remains local under `.sdvkit/research/farm-targets`. Those files are review evidence and are not shipped by this branch.

## Work required before enabling the target

A fork which continues this work must separately implement and review:

1. Exact runtime capture of the canonical main-house identity, current house state, effective post-renovation/crib/cellar map, and saved decoration keys.
2. Trusted vanilla baselines derived from the supported game version, including structural and realized overlay fingerprints.
3. An occupancy and retained-state policy for the primary bed, fridge chest and contents, furniture, decorations, objects, machines, characters, players, pets, and cellar.
4. Location-owned selection storage, exact-hash quarantine, atomic apply/Base/rollback, and save/restart reconciliation.
5. Narrow lifecycle integration which preserves the original `FarmHouse.updateMap`, upgrade, spouse-room, renovation, crib, kitchen, fridge, cellar, reset, and decoration behavior.
6. Target registration, content-pack validation, menu behavior, protocol compatibility, diagnostics, and documentation.
7. Focused game-free tests, full CI, and a public pinned SDVKit review covering each claimed upgrade/state combination, rejection, Base restoration, and exact-artifact persistence.

Until those gates are complete, user-facing documentation and release notes must continue to treat farmhouse interiors as unsupported.
