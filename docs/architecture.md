# Architecture

This document describes the target architecture and binding invariants of the MVP. It does not claim that every described component is already implemented or verified in-game.

## Core concept

A building or stored `GameLocation` owns the persistent game state. The map is the loaded asset for layout, tilesheets, and tile properties. Stardew Interior Changer changes a registered map without replacing the location's identity or its stored entities.

This separation follows the official Content Patcher documentation: the location contains objects, furniture, crops, NPCs, players, and other state, while the map describes tiles and properties. See [Maps vs locations](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/custom-locations.md#maps-vs-locations).

## Components

### Core mod

`StardewInteriorChanger.Core` is the only active code component. It discovers its own SMAPI content packs, validates their schemas and files, builds the variant registry, and coordinates saves, changes, and multiplayer.

### Variant registry

Every variant receives the global ID:

```text
<PackManifest.UniqueID>/<Interior.Id>
```

`Id` remains stable within a published pack. In the MVP, `Target` is exactly `Greenhouse` or `DeluxeBarn`; dimensions and filenames are never guessed to identify a target.

### Gameplay hash

The Core calculates a SHA-256 hash from:

- the canonical gameplay-relevant variant definition;
- every file under `GameplayRoot`, using deterministic path and file handling.

Pack authors do not provide a hash. `DisplayName` and a `Preview` outside `GameplayRoot` are excluded so translations and preview images may vary locally. If the preview file is inside the root, it is hashed like every other gameplay file. The exact byte framing remains an internal protocol detail versioned together with protocol tests.

### Selection state

The Host owns the authoritative mapping from each building instance to a global variant ID. The Greenhouse has one unique target instance; every Deluxe Barn requires a stable building identity. Farmhands may send requests but cannot directly modify the registry or saved selection.

### Native selection menu

The optional player interface is a native Stardew `IClickableMenu`; it adds no UI framework or required configuration-mod dependency. Its viewport-derived layout contains independent scrollable building and variant lists plus an optional preview. Mouse, keyboard, and controller snapping all select only local menu state. The shared selection path is entered only by the explicit Apply action.

Base interior is always the first choice and resolves the building's normal game asset path, so compatible Content Patcher changes may be part of the resolved result. A saved custom choice is current only when its global variant ID and gameplay hash both match an installed entry. Invalid data, a missing entry, or a changed hash remains a visible warning and is never represented as Base interior.

## Flow

### Startup and pack discovery

1. SMAPI loads the Core and its content packs.
2. The Core reads `interiors.json` with `FormatVersion: 1`.
3. The schema, global IDs, targets, and all resolved paths are validated.
4. The Core calculates the gameplay hash for every valid variant.
5. Only fully valid variants enter the registry.

### Loading a save

The stored selection state is checked against the local registry and stored gameplay hash. Stardew does not persist `GameLocation.mapPath`, so only the immediate `SaveLoaded` path may restore an exactly matching stored custom selection together with its existing contents. A missing, changed, or unloadable variant never silently changes the selection, but it sets a persistent `RequiresEmptyRestore` quarantine on the Host. If the pack returns later, the full empty-interior check applies again. Runtime drift outside `SaveLoaded` is handled the same way.

Farmhands resolve the same Core-owned proxy to a validated Vanilla map until the Host handshake succeeds. When parity remains missing, the fallback stays active and the Core blocks entry to the affected custom interior. Local split-screen players instead run on the Host machine and share its authorized registry and map resolution.

### Requested change

1. A local command, the in-game menu's explicit Apply action, or a Farmhand sends a change request.
2. The Host resolves the target building and target variant from its registry.
3. The Host validates multiplayer parity and every safety condition.
4. Only after successful validation is the map changed and the selection stored.
5. On failure, the existing state remains unchanged and the reason is logged or displayed clearly.

An accepted request changes the map immediately. Sleeping only persists the already committed shared selection; the Core does not maintain a deferred sleep queue. Farmhand menu requests use the existing selection request/result messages and block a second Apply while a response is pending. Results are correlated by the existing building and variant fields; a result received after the menu closes is logged and shown through Stardew's HUD.

## Multiplayer protocol

Every peer requires the Core. During connection setup, at least the protocol version and variant tuple are compared:

```text
(GlobalVariantId, Target, GameplayHash)
```

A custom variant is usable only when every connected peer reports the same global ID and exact gameplay hash. A missing Core, unknown ID, or mismatched hash excludes the variant from changes. The Host remains responsible for the decision and persistence.

The hash boundary is stricter than a version number alone: two packs with the same manifest version but different maps or tilesheets are incompatible. Conversely, localized display names and preview images outside `GameplayRoot` do not change the gameplay hash.

For custom maps, the save contains only Core-owned managed-map keys, never directly synchronized content-pack asset keys. A peer with the Core can therefore fall back safely to the target-appropriate Vanilla map before the parity decision. A peer without the Core lacks this loader and is unsupported for a save with active custom interiors.

The quarantine marker can be set only while the Core itself is loaded. The Core cannot later distinguish a save that was continued and stored without it from a normal exact restore. That case therefore remains outside the automatic safety contract and requires an empty interior or explicit adoption of Vanilla before reactivation.

### Verification status

The positive protocol path was verified with a Host and a real second Farmhand: New Farmhand join, registry handshake, Farmhand request, Host-authorized apply, and reconciliation to the same Vanilla or building-instance-specific proxy key. Missing-pack, hash-mismatch, and peer-without-Core cases are covered statically and by Core tests but have not yet been validated live as negative two-process paths.

Future live validation runs exclusively through the public SDVKit. `single` is the default smoke-test and review topology; use `network-2` only for explicitly requested multiplayer validation. Its review lifecycle consists of start and role validation, stop, restart with exactly the same selection, another stop, and a final SDVKit reset. The project has no custom save, staging, screenshot, or process control.

## Fail-closed safety invariants

- No explicit change, including a change to Vanilla, without successful target validation.
- No deletion or automatic relocation of persistent entities.
- No activation of a variant with a missing or mismatched peer hash.
- No silent change to the stored selection when a pack is missing.
- No loading of `Map` outside the declared `GameplayRoot`.
- No pack-local or mod-local TMX, TSX, or tilesheet dependency outside `GameplayRoot`.
- No resolution of `GameplayRoot` or `Preview` outside the pack root.
- Absolute paths, traversal (`..`), and filesystem escapes are rejected.
- Symlinks and junctions in the pack tree are rejected fail-closed.
- An invalid variant is disabled in isolation and logged with its pack ID, variant ID, and cause.
- Unsafe or unprovable states result in rejection, not a best-effort migration.

Change validation includes at least the target type and upgrade level, map loadability, required layers/properties, valid anchors, and whether existing players and persistent entities remain valid on the target map. Concrete migration rules are outside the MVP.

## Content Patcher boundary

Content Patcher combines `Load` and `EditMap` patches based on priority, load order, tokens, and conditions. The resulting asset state does not automatically carry the metadata of a selectable interior pack. The Core therefore does not import arbitrary replacers or read third-party pack directories as implicit variants.

A future, explicitly versioned registry-asset integration for Content Patcher is possible but is not a prerequisite for the native MVP pack format.

## Deliberately open

- Negative two-process validation for a missing pack, hash mismatch, a peer without the Core, and a delayed handshake.
- Remote-player occupancy gate in a real Host/Farmhand session.
- Authorized migration descriptions between substantially different layouts.
- Additional targets after real verification of the Greenhouse and Deluxe Barn.
- Farmhouse, Farm Cave, Coop, Shed, and Slime Hutch.
