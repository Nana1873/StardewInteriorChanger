# Original Barn and Coop integration

This capability remains a draft and is blocked by the first real-source review. Candidate 2 cannot offer the complete original-mod switching workflow. Build and contract tests do not establish compatibility in a running save; the adapters must not yet be described as supported.

## Player workflow

The intended setup is to install SIC, its documented dependencies, and the original packs normally. No conversion pack or edits to the original mod folders are required. Each farm-owned Barn or Coop instance has its own selected interior. The adapter captures all six tiers from each reviewed source before offering any new variants or redirecting that source's shared map replacements.

The initial exact sources are Coop and Barn Facelift 1.2 (`nykachu.coopbarnfacelift`) and Green Coops and Barns 1.0.4 (`vikich3rry.GreenCoopsBarns`), with Content Patcher 2.9.1. Source versions and `content.json` recipes are checked. Changed or unsupported recipes remain under the original mod's control and produce a diagnostic; SIC does not promise to resolve conflicts between unsupported replacers.

## Configuration and texture ownership

The original GMCM options remain authoritative. A configured source becomes six immutable choices, one per tier. A change offers new choices for explicit application while old selected snapshots remain available for the current process. As with the Greenhouse adapters, historical configurations are not persisted by SIC across a restart. Missing exact saved hashes remain quarantined.

Coop and Barn Facelift applies its selected wall image and then wood image to an isolated vanilla `coopTiles` baseline, using SMAPI's image-patching semantics. The compatibility recipe adds the current game's `AutoFeed` property only to its Deluxe Barn and Deluxe Coop snapshots. The source files are unchanged.

Green captures its selected `coopTiles` replacement and window textures directly from that source. Its original location-dependent recolor condition is interpreted for the selected managed animal-house target, so capture does not depend on where the player is standing. Green's `IsGreenhouse` behavior is an explicit, reversible capability of its reviewed snapshots. Leaving Green restores the destination's ordinary state; failures must restore the previous map and flag together. Native packs do not gain permission to set otherwise prohibited location flags.

Map tilesheet pixels are private to each snapshot. Original global object-sprite options are still global: Nykachu's craftable and hay recolors can affect objects outside the selected building. Unrelated global `coopTiles` edits are not composed into these source-specific snapshots. Optional Green patches for external premium, mega and giant building assets remain active, but those buildings are not registered by this six-tier adapter.

Green with Vanilla Plus Professions is explicitly unsupported because its expanded maps depend on current-player profession state. Legacy PyTK image scaling before 1.24.0 is also outside the reviewed texture recipe. The adapter does not silently omit these differences.

## State and switching

All six vanilla animal-house tiers have distinct contracts for capacity, feeding area, troughs and built-in equipment. The selector remains limited to buildings on the farm.

The normal occupancy rules remain: players, resident or assigned animals, decoration, machines, crops and other persistent contents block a layout change. A canonical, unchanged Feed Hopper is retained at its original tile. Big and Deluxe Coops may also retain the canonical empty incubator. Active eggs, ready eggs, pending naming or birth events, altered equipment and changed machine behavior block new selections. Normal inert input history from a completed hatch may remain; SIC never erases it to make a switch pass.

Exact saved-map restoration is a separate operation. It preserves processing and ready eggs on the same selected hash instead of requiring the player to empty a room before loading a save. The incubator, hopper, animals and parent building retain their identities. Destination geometry must keep fixed equipment usable, and rollback must retain all state.

An upgrade changes the target contract. A custom choice from another tier stays visible as an unresolved request until the player explicitly chooses a compatible layout for the current tier.

## Recorded live result: blocked

The 2026-09-08 single-player review used the README-pinned public SDVKit 0.8.0 and candidate code commit `a268ac0`, after correcting the comparison with SMAPI's normalized Nykachu version `1.2.0`. The staged target build identity was `sha256:5c3769221aff1a772d7eeb7ea0edcc15d39f37ed7b6c15e55fd1b1066ff77261`. The exact disposable fixture loaded with verified identity, but this environment readiness was not functional acceptance.

The game reported `Installed Coop and Barn Facelift cohort is unavailable: Unsupported Coop and Barn Facelift tilesheet 'Maps/townInterior'.` SIC retained the original source patches instead of publishing a partial six-tier cohort. Green registered all six source variants; registration alone does not prove their textures or switching behavior in use. With Nykachu's original replacements still active, safe Base preparation failed for `Maps/Barn3` and `Maps/Coop3`: the Deluxe Barn and Deluxe Coop contracts require a non-empty map-level `AutoFeed` property which those resolved maps did not provide. The intended twelve-variant, both-original workflow therefore failed its initial live gate.

No fix or additional runtime experiment was made after this blocker; development was stopped at the user's request. PR #15 remains a draft. The acceptance gates below remain open, including both-direction switching and Base recovery across all tiers, instance independence, source-specific visual inspection, feeding, Green planting/season and state reversal, GMCM refresh, incubation rejection/hatching, and exact save/stop/restart preservation. No new original-mod compatibility or multiplayer acceptance is claimed.

Private review evidence is retained under `.sdvkit/verification/animal-originals/`: `start-candidate2.json`, `ready-candidate2.json`, and `candidate2.log` record the exact build, fixture readiness and failures; `final-stop.json` and `final-reset.json` confirm the owned process stopped, staging was removed and the disposable fixture was reset. `source-integrity-final.json` compares the original source folders with the pre-review baseline: all 69 files are unchanged, with no additional files. These local evidence files and third-party assets are not committed.

## Open acceptance gates

- Load both original packs together and register twelve independent variants without exclusive-map conflicts.
- Switch both directions and back to Base for all six tiers, with two separate instances of one tier.
- Inspect each source's coherent textures from outside and after entry; changing location must not alter a retained snapshot.
- Verify normal feeding, Green planting/season behavior and reversal to a non-Green map.
- Reject active, ready and full-house waiting eggs; preserve an exact processing/ready save through stop and restart, then complete normal hatching and naming.
- Retain the used-but-empty incubator's complete history across a safe switch.
- Reject occupied changes and incompatible upgrades without altering the saved request or contents.
- Verify source configuration updates through released review tooling, original source hashes, and final owned stop/reset.

The tests must use isolated copies of the actual originals through the README-pinned public SDVKit. Synthetic fixtures establish only the native target contract. No original maps, textures or preview screenshots are redistributed in this repository.
