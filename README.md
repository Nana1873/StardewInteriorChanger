# Stardew Interior Changer

Stardew Interior Changer is a SMAPI framework that lets players select registered interior variants for supported farm buildings. The building and its saved game state remain intact; only the registered interior map is replaced.

> **Project status:** functional MVP prototype; no mod release has been published yet. The Release build, 94 Core tests, real Greenhouse/Deluxe Barn changes, save/process restore, and the positive Host/Farmhand path are verified. Live smoke tests, fixtures, and reviews run canonically through the public SDVKit version pinned below. Negative multiplayer parity cases such as a missing pack or mismatched gameplay hash have not yet been validated live with two processes. The pack under `examples/` remains a non-installable schema example only.

## MVP scope

- Only interiors belonging to buildings on the player's farm are selectable; town buildings and arbitrary world locations are outside the scope.
- `Greenhouse` as the farm's single greenhouse interior.
- `DeluxeBarn` as a separately selectable interior for each building instance.
- Development contracts for `Shed` and `BigShed`, retaining canonical wallpaper and flooring regions; focused live acceptance is still pending.
- Base interior as an explicit, safe selection through the building's normal game asset path.
- Native interior packs with a small, versioned schema.
- Host-authorized selection stored in the shared save.
- Safety validation before every change, with no silent deletions or relocation.
- Multiplayer parity for the Core mod and the exact gameplay hash of every selected variant.

Farmhouse conversions, automatic migration of arbitrary layouts, and automatic import of existing Content Patcher or XNB replacers are outside the MVP scope.

The first installed-source adapter supports Ellie's Ideal Greenhouse 1.5.0 with the reviewed Content Patcher 2.9.1 build. Its configured interior becomes a menu choice without converting or editing the original pack. GMCM changes require explicit application; old selected snapshots remain available during the session. Historical configurations are not persisted by SIC, so changing source settings before a restart can leave the old saved selection quarantined until safely resolved. See [real-mod compatibility](docs/real-mod-compatibility.md) for exact requirements and limits.

Oasis Greenhouse 1.9.4 can be installed alongside Ellie and selected independently, with or without its cellar entrance. Its textures, messages and minecart network are captured per snapshot; the return route is available only for the active safe cellar layout. Original-pack switching and minecart round trips have passed isolated live review. Keg/Cask placement, processing continuation across a real restart, occupied-switch rejection and normal item recovery also passed. Full maturation, Jukebox, exhaustive room traversal and uninterrupted GMCM updates remain open acceptance checks; resolved localized text currently makes Oasis fingerprints locale-dependent.

The next development priority is completing real-mod compatibility acceptance, followed by additional farm-building targets and eventually a separate farmhouse contract. The [roadmap](ROADMAP.md) tracks the validation steps.

## Multiplayer contract

The selection is shared world state, so only the Host confirms and stores it. Farmhands send a selection request; only the Host validates the map, content, and peer parity before changing the shared save.

To use a custom variant, every **peer** must have:

1. Stardew Interior Changer Core with a compatible protocol version;
2. the same global variant ID (`<PackUniqueID>/<Id>`);
3. the same gameplay hash calculated by the Core.

The hash covers the canonical gameplay-relevant variant definition and every file under `GameplayRoot`. `DisplayName` and a preview outside `GameplayRoot` are local/cosmetic and excluded; a preview file inside the root is always hashed. A missing variant or mismatched hash prevents a custom change and never silently alters the stored selection.

Custom maps are not synchronized in the save using a content pack's internal asset key. The Core assigns stable proxy keys per building instance so, for example, two barns using the same variant remain independent without sharing cache reloads. A Farmhand with the Core installed resolves a validated Vanilla fallback map until the Host handshake succeeds; if a variant is missing or mismatched, the fallback remains active and access to the affected custom interior is blocked. The Core must therefore be installed on every peer before joining. The positive path has been verified with two real local SMAPI processes: Farmhand join, registry handshake, request to the Host, Host-authorized apply, and reconciliation to the same Vanilla or proxy map key. Missing-pack and hash-mismatch cases are covered by Core tests but have not yet been validated live with two processes.

Local split-screen runs on the Host machine and therefore uses its validated registry directly instead of the quarantine applied to remote Farmhands. The Core must remain installed while loading and saving: a full session played without the Core cannot be marked unsafe. If a save was continued without the Core, empty a custom interior before reactivating it or explicitly adopt the already loaded Vanilla map with `sic vanilla`.

This stricter rule is intentional: Content Patcher loads maps locally. Without identical maps, players can see different geometry and boundaries. See [Content Patcher: Multiplayer](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/README.md#multiplayer).

## Safety boundaries

- A change happens only after successful validation.
- The Core never automatically deletes or moves animals, objects, machines, furniture, crops, or other stored entities.
- If the target map cannot safely contain the existing state, the change is rejected.
- Changing back to Vanilla passes through the same validation.
- Pack paths must stay within their pack directory; absolute paths and `..` escapes are rejected.
- Symlinks and junctions within a pack tree are rejected fail-closed.
- Pack-local TMX, TSX, and tilesheet dependencies must be under `GameplayRoot` and are therefore hashed.
- Missing or invalid pack files disable the affected variant, not the entire save.
- A content pack is data, not a mechanism for loading or executing foreign code.

The technical structure and deliberately open runtime decisions are documented in [docs/architecture.md](docs/architecture.md).

## Development and validation with SDVKit

The current project contract requires Stardew Valley 1.6.15, SMAPI 4.5.2, and the .NET 8 SDK for builds. The mod itself targets .NET 6 to match the game. Live validation exclusively uses a fresh download of public [SDVKit v0.8.0](https://github.com/Nana1873/SDVKit/releases/tag/v0.8.0) with the expected ZIP SHA-256 `0c721ceaf6cc03ff69626fb2ee263d82a7f8f50ac14f77076ebf24fa3c92f35b`; a local SDVKit source build is not a substitute runtime. In the examples below, `sdvkit` refers to the public binary extracted under `.sdvkit/`.

The canonical relative targets are:

- Mod: `src\StardewInteriorChanger`
- Unit tests: `tests\StardewInteriorChanger.Core.Tests`
- Review pack: `tests\fixtures\SmokeGreenhousePack`

GitHub CI runs only the game-free Core tests. Full mod builds, packaging, SMAPI loading, and in-game reviews remain local SDVKit checks.

Automated validation steps remain independently assessable:

```powershell
sdvkit doctor --json
sdvkit project inspect .\src\StardewInteriorChanger --json
dotnet test .\StardewInteriorChanger.sln -c Release --artifacts-path .\.sdvkit\dotnet-artifacts
sdvkit project build .\src\StardewInteriorChanger --json
sdvkit project package .\src\StardewInteriorChanger --json
```

The published `project smoke` command in the README-pinned SDVKit version has no companion-mod option. It therefore cannot supply the new required StardewUI dependency; bounded smoke acceptance remains blocked until released SDVKit supports that capability. Use `project review start` with an explicit StardewUI companion for interactive validation, but do not report it as a passed bounded smoke test.

SDVKit keeps builds, packages, profiles, saves, staging, logs, screenshots, and process state under `.sdvkit/`. Normal saves, normal or mod-manager-owned mods, and Vortex staging remain outside the workflow.

## In-game selection menu

The menu uses [StardewUI Continued](https://www.nexusmods.com/stardewvalley/mods/43861). Install its released `0.6.4-unofficial-mushymato.0` build as a separate mod alongside Interior Changer; newer compatible versions require their own validation. The framework is not bundled. Integration details and dependency provenance are in [docs/ui.md](docs/ui.md); upcoming capabilities are tracked in the [development roadmap](ROADMAP.md).

Press `F8` while a save is loaded and the player is free to open the Interior Changer menu. The binding is stored as SMAPI's `KeybindList` in `config.json` under `OpenMenu`, so single keys and key combinations can be configured without a separate configuration mod. The deterministic console and SDVKit entry point is `sic menu [buildingId]`; the optional ID selects that supported building directly.

The menu lists the Greenhouse and each supported farm-building instance as separate targets. Choosing a row only changes the previewed choice. The map-change request is sent only after selecting **Apply**, and an accepted request changes the interior immediately. Sleeping saves the selection that has already been applied; there is no deferred sleep queue.

`Base interior` uses Stardew's normal asset path for that building. It can therefore include compatible Content Patcher replacements active for that asset and should not be interpreted as an unmodified Vanilla file. Missing or changed saved variants remain visible as warnings instead of being presented as Base interior. Optional content-pack previews are cosmetic: Base interior, variants without a preview, and preview load failures use a placeholder, while the actual variant remains available.

## Current developer commands

After loading a save, the following commands are available in the SMAPI console:

```text
sic targets
sic list
sic current [buildingId]
sic set <variantId> [buildingId]
sic vanilla [buildingId]
sic menu [buildingId]
```

`sic targets` displays the stable building IDs. `sic current` inspects the supported interior the player is currently inside; pass a building ID to inspect that target from anywhere. A real map change runs only when the interior is empty: it must contain no player, animal, placed object, furniture, crop, or other persistent content. Only the single unchanged built-in Feed Hopper `(BC)99` at `(6,3)` in a Deluxe Barn is exempt from the placed-object gate. Additional, relocated or stateful hoppers block a change. The target map must retain its usable position and reachable access; SIC never moves it. The MVP therefore never deletes or moves save content. Stored custom maps are restored automatically during a normal save load only when the ID and stored gameplay hash exactly match the installed pack. If the Core previously encountered a missing, changed, or unloadable pack, it sets a persistent quarantine marker; a later restore then also requires an empty interior. Explicitly adopting the already loaded Vanilla map does not change the map and can safely clear this marker with `sic vanilla`.

The local game path is stored in a Git-ignored `.csproj.user` file. On another machine, set it once there or pass it with `-p:GamePath="..."`. Automatic deployment to the normal `Mods` directory is disabled in the project.

## Functional and visual review

Local in-game validation follows the public SDVKit skill `sdv-project-review`. Prepare SDVKit's own test save, then explicitly select it for the review. The existing ConsoleCommands mod is supplied only as an explicit companion for `debug sleep`:

```powershell
sdvkit lab test-save --topology single --json
sdvkit project review start .\src\StardewInteriorChanger `
  --topology single `
  --test-save `
  --companion <prepared-ConsoleCommands-mod-directory> `
  --companion <prepared-StardewUI-mod-directory> `
  --content-pack .\tests\fixtures\SmokeGreenhousePack `
  --json
sdvkit project review status --topology single --json
```

Generic world state is prepared exclusively through the bounded `sdvkit fixture ...` console commands. Validate the mod itself through `sic targets`, `sic list`, `sic current`, `sic set`, `sic vanilla`, and `sic menu`. Create screenshots only through SDVKit's published screenshot surface; map captures use `sdvkit screenshot <label>`, while a published SDVKit version that supports viewport capture can record menus and HUD. Accept a screenshot only after explicit AlwaysOn confirmation, a present PNG, and real visual inspection. `commandWritten=true` proves only that a console command was delivered.

A review using the test save includes a real process restart with the same work copy. After the final save and stop, reset exclusively through SDVKit:

```powershell
sdvkit project review stop --topology single --json
sdvkit project review start .\src\StardewInteriorChanger `
  --topology single `
  --test-save `
  --companion <prepared-ConsoleCommands-mod-directory> `
  --companion <prepared-StardewUI-mod-directory> `
  --content-pack .\tests\fixtures\SmokeGreenhousePack `
  --json
# Verify persistence and Vanilla restore, save, and stop again.
sdvkit project review stop --topology single --json
sdvkit project review reset --topology single --json
```

`single` is the default. Use `network-2` only for explicitly requested multiplayer validation and follow the SDVKit lifecycle exclusively: start with the explicit selection, validate both roles, stop, restart with exactly the same selection, stop again, and finally run `project review reset --topology network-2 --json`. This repository has no custom stager, launcher, or two-process controller.

The following behavior is verified:

- a Greenhouse and two independent Deluxe Barns with Vanilla and custom changes;
- blockers for genuinely placed objects, players, and animals while preserving the built-in Feed Hopper unchanged;
- save, full process restart, and exact restore of the selected maps and stored entities;
- a real second Farmhand through Stardew's New Farmhand flow;
- Farmhand requests for Vanilla and custom variants, Host-authorized apply, and an identical target map on Host and client.

The previous native-menu review in [PR #5](https://github.com/Nana1873/StardewInteriorChanger/pull/5) also verified rejection while the remote Host occupied the target, followed by a successful Farmhand retry after the Host left. That evidence covers the unchanged selection path, not the replacement StardewUI renderer. Missing-pack and hash-mismatch cases, a peer without the Core, and a delayed handshake while a custom interior is already occupied are not yet considered proven live.

## Interior packs

Interior packs are native SMAPI content packs for `StardewInteriorChanger.Core`, not ordinary Content Patcher replacers. Setup, schema, and the hash boundary are documented in [docs/content-packs.md](docs/content-packs.md).

SMAPI defines `manifest.json` and `ContentPackFor`; the actual pack schema belongs to the Core mod. See the official SMAPI pages for [content packs](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Content_Packs) and the [manifest](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest).

## Why existing replacers cannot be selected automatically

Content Patcher packs do not expose a catalog of independent interiors. They patch shared assets at runtime:

- `Load` replaces an entire asset; only one loader is selected when loaders conflict.
- `EditMap` can overlay tiles, properties, and warps.
- Conditions, tokens, configuration, dependencies, and load order can change the result.
- Later patches receive the already combined result of earlier patches.

The Core could therefore see only the currently resolved final state. It cannot reliably reconstruct the original variants, allowed building types, entrances/exits, dependencies, or permissions. Existing interiors require a native pack or a validated installed-source integration. Distributing third-party content also requires permission for that content.

Official details: [Content Patcher `Load`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-load.md), [`EditMap`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-editmap.md), and [how multiple patches interact](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md#how-do-multiple-patches-interact).

## Third-party assets, licensing, and adapters

The Core and official example packs contain only original assets or content whose license explicitly permits the specific use and redistribution. Maps, tilesheets, preview images, or other files from third-party mods are not copied or republished without documented permission. Credit or a link does not replace permission.

Original compatibility code which reads a separately installed mod is distinct from a pack containing third-party content. SIC's installed-source bridge distributes its own integration logic and supported-version metadata, without bundling or editing the original mod. This does not assert the original author's endorsement or grant permission to redistribute their work.

A published pack containing third-party content or derived assets must:

- have the original author's consent;
- require the original mod as a separate dependency whenever possible;
- contain only the required metadata and integration logic;
- document supported original versions and dependencies;
- contain no third-party assets unless their license or written permission clearly allows it.

If permission for third-party content is unclear, that content is not shipped. This includes derived preview images. Follow the applicable distribution platform's rules; see the [Nexus Mods File Submission Guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines).

## License

This repository's original content is available under the [MIT License](LICENSE). The license applies only to this repository's original content and grants no rights to Stardew Valley or third-party content packs. Compatibility between an interior pack and this Core does not transfer or expand rights to the pack's assets; pack authors remain responsible for licensing, permissions, and credits for their content.
