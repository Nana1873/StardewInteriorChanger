# Changelog

All notable changes to this project are documented here. The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and released versions should follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Initial project contract for a SMAPI-based interior selector.
- MVP scope limited to `Greenhouse` and `DeluxeBarn`.
- Versioned interior-pack schema with `FormatVersion: 1`.
- Host-authorized multiplayer contract with exact gameplay hashes on every peer.
- Fail-closed safety boundaries for layout changes and missing or mismatched packs.
- Non-installable schema example without game or third-party assets.
- Documented licensing and adapter boundary for third-party content.
- Game-independent Core domain for the registry, path safety, canonical gameplay hashes, selection state, and peer compatibility.
- Native SMAPI content-pack loader with isolated per-variant failures and structural map validation.
- Versioned selection per building instance with an exact target contract in `Building.modData`.
- Host-authoritative Farmhand requests, variant-fingerprint handshake, and client reconciliation after NetField synchronization.
- Console commands `sic targets`, `sic list`, `sic current`, `sic set`, and `sic vanilla`.
- Strict empty-interior check before explicit changes; no automatic moves or deletions.
- Golden-master contract for Deluxe Barn: `Warp`, `AutoFeed`, `ProduceArea`, and twelve `Trough` tiles verified against Stardew 1.6.15.
- Isolated, entirely original smoke fixture for content-pack discovery and map loading.
- Core-owned managed-map proxies with a Vanilla fallback before a successful Farmhand handshake and access quarantine when hash parity is missing.
- Atomic map-and-selection apply with rollback and renewed validation of the map resolved at runtime.
- Complete contract for warps, barn capacity, and one-way location flags.
- Fail-closed protection against pack symlinks/junctions and gameplay dependencies outside `GameplayRoot`.
- OS-consistent physical path boundaries and early validation of missing local TMX/TSX image files while continuing to allow Vanilla GameContent tilesheets.
- Exact `SaveLoaded`-specific custom restore with persistent empty-interior quarantine after missing, changed, or failed variants.
- Correct host authorization for local split-screen and forced local Farmhand reload without reusing Stardew's multiplayer map cache.
- Building-instance-specific proxy keys and client attestation prevent cache reloads and approvals from leaking across multiple barns; the Farmhand access guard fails closed on every tick.
- Native viewport-responsive in-game selection menu for the Greenhouse and each Deluxe Barn, with configurable `KeybindList` defaulting to `F8` and deterministic `sic menu [buildingId]` access.
- Scrollable mouse, keyboard, and controller navigation, optional content-pack previews with safe placeholders, explicit Apply feedback, and visible missing/invalid/hash-mismatch selection warnings.
- Farmhand menu request tracking over the existing multiplayer messages, including pending-state duplicate prevention, correlated Host results, and HUD feedback when a result arrives after the menu closes.

### Changed

- Translated repository-facing documentation, contributor guidance, and schema example labels to English.
- Live tests, fixtures, and reviews were fully migrated to the public SDVKit; generic fixture gaps found during the migration were fixed upstream in SDVKit. `single` remains the default, and `network-2` is reserved for explicitly requested multiplayer validation.

### Removed

- Removed the former project-specific in-game QA harness and its `sicqa` commands after SDVKit took over its save, fixture, process, screenshot, and multiplayer responsibilities.

### Fixed

- `sic current <buildingId>` now inspects the requested supported building instead of ignoring the optional building ID.
- Newly built Deluxe Barns are no longer treated as occupied because of Stardew's built-in Feed Hopper `(BC)99`; genuinely placed objects remain blockers.
