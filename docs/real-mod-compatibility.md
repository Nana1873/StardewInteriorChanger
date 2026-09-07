# Real interior mod compatibility

The intended player experience is to install supported interiors once and switch between them in the selection menu. Two different mods for the same target must remain independent choices. Greenhouse and Deluxe Barn are the first targets; only farm-owned buildings are in scope.

## Why a compatibility layer is needed

Existing Content Patcher packs commonly replace `Maps/Greenhouse` or `Maps/Barn3`. These are shared assets, not independent registered interiors. Content Patcher defaults `Load` patches to `Exclusive`; when multiple exclusive patches target the same asset, none applies and an error is reported. Increasing a separate mod's load priority does not provide a public API for disabling those patches. See [Content Patcher Load priorities](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-load.md).

An adapter must preserve the selected source map's own tilesheets, configuration, map properties, actions, and dependent data. It must not offer a raw map as complete support when that mod also requires overlays, strings, minecart destinations, or other locations. Ordinary coexistence of two active replacers is not proof of selectable variants.

## Current real-mod candidates

### First installed-source adapter

SIC includes a narrowly scoped adapter for Ellie's Ideal Greenhouse 1.5.0 with the reviewed Content Patcher 2.9.1 build. Install the original pack alongside SIC; no conversion pack or changes to its files are required. Other CP interior mods, including Oasis, are not registered automatically by this adapter.

The adapter checks the CP assembly and internal method contracts, the source version, and the reviewed `content.json` recipe. Unsupported combinations produce a diagnostic instead of registering an incomplete variant. Native SIC packs continue to work independently. Supported Ellie map patches are isolated from the shared greenhouse asset; Base uses that shared asset with any remaining patches.

GMCM continues to edit Ellie's original configuration. SIC captures the configured map and its allowed tilesheets as immutable content, including configuration and adapter metadata in the gameplay fingerprint. A changed configuration becomes a new menu choice. If the building still uses the old configuration, select the new choice and use **Apply updated settings**. Existing player, object, crop, furniture, and animal checks still apply.

The old selected snapshot is retained during the process lifetime. This initial adapter does not persist historical snapshots: after a process restart, a saved selection whose old configuration is no longer available follows the existing missing-variant quarantine path. It must never silently become the newly configured layout. Keeping the source configuration unchanged restores the same fingerprint. Independent per-building configuration presets are deferred.

The adapter supports only the reviewed self-contained map recipe and an explicit allowlist of vanilla tilesheet references. It captures the effective texture pixels behind those references, so later changes cannot mutate an already selected snapshot. Additional source actions, foreign tilesheets, and arbitrary global gameplay patches require their own adapter support.

### Installed Ellie acceptance, 2026-09-07

Public SDVKit v0.8.0 single-player review loaded the original Ellie 1.5.0 recipe with Content Patcher 2.9.1, StardewUI Continued 0.6.4, and GMCM 1.16.0. The frozen SIC artifact registered Spacious automatically; menu selection, natural entry, coherent viewport textures, and rejection while a player remained inside passed. A real GMCM Default action saved Modest and SIC registered a new fingerprint.

The GMCM save changed the staged `config.json`, triggering SDVKit's ownership guard. The review preserved those emitted bytes, stopped through the public tool, copied them into the private test source, and restarted without resetting the work save. The unavailable saved Spacious request remained quarantined; explicit Modest selection then succeeded. After saving and restarting the identical SIC artifact with unchanged Modest settings, the exact selection and fingerprint restored. Natural entry and return to Base (`Maps/Greenhouse`) passed. Final SIC diagnostics were empty, all 21 original source files matched their pre-review hashes, and final stop plus fixture reset succeeded.

This is partial GMCM acceptance: uninterrupted post-save menu refresh, the **Apply updated settings** action, and retention of the old occupied snapshot across that live change still require a review surface that supports legitimate configuration writes. The test did not bypass the ownership guard. Public v0.8.0 bounded smoke also lacks the required companion support and is not reported as passed. Two installed-source switching, Oasis gameplay, and multiplayer are separate uncompleted gates. Private evidence is retained under `.sdvkit/verification/installed-ellie/`.

| Mod | Locally inspected version | First bounded case | Current constraint |
| --- | --- | --- | --- |
| [Ellie's Ideal Greenhouse](https://www.nexusmods.com/stardewvalley/mods/7497) | 1.5.0 | Modest Greenhouse, 28 x 28 | Explicit vanilla tilesheet references need normalization for the native dependency contract. |
| [Oasis Greenhouse](https://www.nexusmods.com/stardewvalley/mods/3969) | 1.9.4 | Main Greenhouse, 52 x 116, cellar enabled | Requires its strings and minecart behavior as well as local tilesheets; the no-cellar file is an overlay, not a standalone map. |
| Coop and Barn Facelift | 1.2 | Deluxe Barn source | The inspected map lacks the current contract's required AutoFeed property. |
| Green Coops and Barns | 1.0.4 | Deluxe Barn source | The inspected map sets IsGreenhouse, which the current reversible Barn contract rejects. |

Except for the bounded Ellie adapter described above, these entries identify candidates rather than supported adapters. Local source folders were matched by manifest ID/version and their files hashed; original download archives with publisher digests are unavailable. Their exact local bytes are recorded in private review evidence.

## First proof and packaging boundary

The first proof uses private native variants generated under `.sdvkit/` from the two greenhouse candidates. Their original folders remain unchanged, and the original Content Patcher replacer packs are not active in that switch-test profile. Explicit transformations and dependencies are recorded and hashed during import. Core does not revalidate the external Oasis support companion against those recorded hashes, so this is not multiplayer parity evidence. The native Core's map and occupancy validators remain enabled.

This is a technical validation path, not the chosen player workflow. The product goal remains installing SIC and supported original mods, then switching in the menu without manual conversion. Automatic integration may require a narrowly scoped, version-checked runtime compatibility layer because the public CP API cannot suppress competing patches. Such a layer must be evaluated separately and must not edit original mod files. Neither this private proof nor a raw-map registry establishes compatibility with arbitrary active CP replacers.

No third-party maps, tilesheets, translations, derived preview images, or private recipes are included in the repository or release package. Public adapters follow the permission requirements in the README. Locally generated screenshots can verify the UI and map, but are not automatically cleared for redistribution.

### Private switch proof, 2026-09-07

The public SDVKit v0.8.0 single-player review validated both converted greenhouse variants, Ellie to Oasis to Ellie switching, rejection with a player inside, persisted Ellie selection after saving and restarting, and return to `Maps/Greenhouse`. Both layouts were inspected through map and viewport captures. Final attributed diagnostics for SIC, Content Patcher, and StardewUI were zero, source map hashes were unchanged, and the review was stopped and reset.

The restart rebuilt SIC with a different build identity while runtime source and content-pack identities remained unchanged; this establishes persisted-state compatibility across those builds, not a bit-identical restart. Oasis casks, pool behavior, minecart traversal, and self-warps were not exercised. The isolated Oasis minecart network does not add a return destination to ordinary vanilla minecarts. This evidence therefore covers layout switching, not complete Oasis feature compatibility. Original CP replacers were inactive throughout this proof.

The private report is `.sdvkit/verification/real-mods/pair-proof1/acceptance.md`; source transformations and equivalence checks are retained under `.sdvkit/compatibility/`.

## Automatic integration direction

The inspected Content Patcher 2.9.1 implementation gathers loaders and editors before checking for conflicting exclusive loaders. A narrowly scoped runtime bridge can therefore remap private SIC asset lookups to an approved source target, filter by source pack identity, and suppress only those approved map patches on the shared target. This can preserve installed original files and unrelated patches.

This uses internal CP implementation methods rather than its public integration API. It requires exact compatibility checks, explicit supported-source profiles, correct initialization after CP is ready, cache invalidation, and failure behavior that leaves native SIC support usable when the bridge cannot attach. It must not change CP's patch collections or globally override readiness flags.

Simple self-contained map replacers can share a common adapter pattern. Shared tilesheet edits, extra rooms, configuration overlays, and global data changes require explicit dependency handling. Two building instances cannot safely request contradictory versions of one global game asset; those combinations must be isolated or rejected. The private prototype is an evaluation step, not a shipped runtime bridge or a guarantee that arbitrary mods work automatically.

### Active-original runtime probe, 2026-09-07

A separate public SDVKit v0.8.0 single session loaded the unchanged original Ellie 1.5.0 and Oasis 1.9.4 packs together with Content Patcher 2.9.1 and a private Harmony probe. SIC and the converted packs were absent from this session. The probe checked the exact CP assembly hash and internal method signatures before attaching.

Both owner-scoped asset loads succeeded: Ellie's configured original map was 71 x 41 and Oasis was 52 x 116. The shared greenhouse remained 20 x 24 with an unrelated control editor's marker preserved; neither private map received that unrelated marker. The recorded fingerprints were identical before and after explicit cache invalidation, and no competing-exclusive-load error occurred. This demonstrates source isolation with both original packs active, without editing their files or applying the probe maps to a building.

This does not yet register the original mods in SIC's menu, establish dependency-complete gameplay fingerprints, or isolate Oasis's global data changes. The private probe and review evidence remain under `.sdvkit/compatibility/runtime-hook-probe/` and `.sdvkit/verification/real-mods/runtime-hook-probe/`.

### First production integration boundary

Begin with one reviewed, self-contained source profile before adding Oasis-specific global behavior. Resolving a source map through CP is only the first step: the effective map, allowed dependencies, relevant configuration, and adapter recipe must form an immutable snapshot with a gameplay fingerprint. A manifest version or a hash of the original TMX alone cannot represent the result of conditional patches.

The resolved snapshot must enter the existing map-contract validator and per-building map pipeline. It must not bypass occupancy checks, saved-selection hash matching, or peer compatibility. Changes to source configuration or dependencies invalidate the snapshot; an occupied building must not silently acquire the newly resolved layout. Native packs retain their current file and dependency contract.

Tests must cover deterministic fingerprints, changes to effective map/configuration/dependencies, missing or escaping dependencies, unsupported CP signatures, and source invalidation while a selected interior is occupied. Public live acceptance must then demonstrate discovery and menu switching with the original source pack active, followed by the two-source coexistence case. A successful private proxy load alone does not complete these gates.

## Acceptance criteria

- Both real-source variants are independently registered in the same isolated test save.
- Switch A to B and back, proving the map dimensions/layout and stored variant after each change.
- Save and restart the same work copy; restore the exact selected variant, then return to Base safely.
- Reject a switch while a player or persistent content occupies the target, retaining the previous selection.
- Confirm required strings/actions/dependencies, and report any untested source-mod feature separately.
- Confirm source-file hashes are unchanged, then stop and reset the public SDVKit review.

Animal repositioning and new target types are separate roadmap items. Adapter work must not widen target discovery beyond the farm or weaken the current animal/occupancy rules.
