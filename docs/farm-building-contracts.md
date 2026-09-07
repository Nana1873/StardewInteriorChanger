# Farm-building interior contracts

SIC discovers selectable targets only through the buildings on the player's farm. A familiar building type placed elsewhere does not become a selectable target. Each building instance retains its own GUID, selected variant and target contract.

## Sheds

`Shed` and `BigShed` are separate contracts for the game's `Shed` and `Big Shed` building types. Their Base assets are `Maps/Shed` and `Maps/Shed2`. A pack for one tier is not automatically compatible with the other, and an upgrade must not silently reuse the previous tier's selection.

Sheds contain saved wall and floor choices as well as ordinary placed contents. The initial contract supports the canonical `Wall` and `Floor` region IDs. A map must declare those IDs and provide corresponding usable decoration surfaces. Additional IDs, aliases and implicit remapping of existing patterns are rejected. This allows region coordinates to change while retaining their meaning.

Applying a map must preserve the saved wallpaper and floor dictionaries. Transient tile-coordinate caches must be rebuilt for the destination before saved patterns are applied; stale coordinates from the previous layout must not modify the new map. Rollback and exact saved-map restoration follow the same cache lifecycle. Whole-location reset helpers are not a substitute because they can also move the player or change unrelated state.

Players, machines, objects, furniture, crops and other persistent contents retain the existing empty-room requirements for a layout change. An exact, available saved variant may restore through the existing save-load path without moving or deleting its contents. Invalid or unavailable saved layouts remain quarantined.

## Animal-house extensions

Additional Barn and Coop tiers are a separate capability. Their capacities, feeding properties and fixed equipment differ. In particular, Big and Deluxe Coops have an incubator whose active egg, processing state and pending hatch must block a new layout. An ordinary machine never becomes exempt merely because it has the same item ID as fixed equipment.

The two reviewed original animal-house packs replace all six tier assets and share a tilesheet. Their adapter must handle those collisions together, preserve source-specific configuration and effects, and keep source files unchanged. Native target registration alone does not establish original-mod compatibility. See [issue #11](https://github.com/Nana1873/StardewInteriorChanger/issues/11).

## Farmhouse

The main farmhouse requires its own map lifecycle and state contract. The game derives its map from upgrades and marriage state; simply assigning a managed map path does not make that selection persistent. Beds, kitchen and fridge, cellar access, spouse areas, renovations and decoration regions need explicit validation and preservation.

The farmhouse is not enabled by the Shed contracts. Cabins and non-farm houses are not implicit extensions. See [issue #12](https://github.com/Nana1873/StardewInteriorChanger/issues/12).

## Validation

Each new target needs game-free contract tests, a full local build/package and a focused review through the public SDVKit version pinned in the README. Validate separate instances, occupied rejection, map-specific anchors, state preservation, Base restoration and a real save/stop/restart of the same artifact. Synthetic native packs prove bounded target behavior; supported third-party sources require their own real-mod review.

Adding target kinds changes what peers can safely resolve. A protocol minor-version negotiation is insufficient when an older peer cannot recognize the new target's map proxy. The protocol must reject such peers explicitly; static mismatch tests do not replace an authorized multiplayer live review.

### Recorded Shed candidate 2 review

The isolated single-player review used public SDVKit 0.8.0 and the same frozen build before and after a real save/stop/restart: `sha256:43d190a2858abab2e378918c4b8291e7249904ce505352afb029a0e839f7fb02`. The candidate passed 139 Core tests, the Release build and public packaging. These results cover the private synthetic Shed fixture, not arbitrary third-party Shed maps.

Confirmed gates:

- Two Sheds and one Big Shed retained separate building GUIDs, instance map paths and selections. One Shed switched A -> B -> A; the Big Shed switched A -> B.
- Non-default wallpaper `Wall=10` and flooring `Floor=5` survived the changed decoration coordinates, restart and return to Base. Screenshots recorded the destination patterns; sealed saves independently confirmed the saved dictionaries.
- New selections were rejected while a player was inside and while a placed Keg occupied the Shed.
- Spring 2 and Spring 3 sealed saves contained identical selection metadata, full content hashes, decoration dictionaries and object payloads for each building. The retained empty Keg at `(1,1)` had byte-identical serialized data. This does not establish processing, output or timer behavior.
- Returning the first Shed and Big Shed to Base removed their custom content hashes and preserved their patterns. The second Shed's custom selection and hash remained unchanged. The owned empty Keg was intentionally removed before the Base switch.
- Final stop and fixture reset completed with staging removed and no reported cleanup problems.

The local evidence bundle is `.sdvkit/verification/shed-contract/`: `state-comparison.md`, the three sealed saves, before/after restart logs, public build/package results and stop/reset records. Screenshots remain in the isolated profile's `Screenshots` directory. Serialized comparisons establish persistence; they do not independently prove visual appearance or in-memory object identity.

The same-GUID Shed -> Big Shed upgrade gate remains incomplete. Robin's construction menu was opened, but the carousel was only partially inspected; no upgrade was selected, initiated or completed. Tier quarantine after an actual upgrade and post-upgrade decoration persistence therefore remain unproven. This is not evidence of a missing blueprint or a construction prerequisite defect. PR #14 remains a draft with this gate open. No multiplayer live review was performed.
