# Development roadmap

The primary player goal is to install supported interior mods and switch between their layouts from one menu. Real third-party mod compatibility takes priority over an original showcase pack. Work is grouped by independently deliverable capabilities; this roadmap does not claim release readiness.

## 1. Select interiors from real installed mods

Start with Greenhouse and Deluxe Barn. The acceptance target is two different supported mods for the same building type installed together, each available as an independent choice. Test through the public SDVKit runtime pinned in the README, using isolated copies of the actual published mods.

- Use explicit, versioned compatibility definitions where existing Content Patcher patches do not expose independent variants.
- Preserve the install-and-switch workflow for players; private conversion fixtures are validation tools, not the chosen user setup. Evaluate a scoped runtime compatibility bridge before requiring manual conversion or edits to original mod folders.
- Keep original mod installations intact. Do not distribute their maps, tilesheets, or other assets without permission.
- Account for configuration, custom tilesheets, map properties, auxiliary locations, and global patches; merely listing a mod is not compatibility.
- Identify unsupported or changed versions clearly instead of silently offering an incomplete layout.
- Validate switching A to B and back, Base behavior, occupied-room rejection, save/restart restoration, and original-asset integrity.
- Previews may be generated locally from supported maps. Bundled previews require a documented right to redistribute the depicted content; taking a screenshot alone does not establish that right.

Ellie and Oasis now have two-original selection and routing evidence. A focused follow-up also verified normal Keg/Cask processing across a full process restart, occupied-switch rejection, and recovery of both products and devices. Remaining real-mod acceptance includes uninterrupted GMCM updates, Jukebox and exhaustive room actions; see [real-mod compatibility](docs/real-mod-compatibility.md).

Generic Content Patcher auto-import is not assumed. A reviewed compatibility definition should make supported mods seamless to use while failing clearly for unsupported combinations. Permissions and integration limits belong in the compatibility documentation.

## 2. Finish selection-menu acceptance

The StardewUI menu is implemented and merged. Mouse selection, previews, Apply, rejection, and the default viewport have live evidence. Remaining controller, compact-layout, and multiplayer-menu cases stay explicit in [UI acceptance](docs/ui.md#current-acceptance-status).

Dependency-aware smoke and reliable synthetic input belong in SDVKit. Reuse accepted evidence for unchanged capabilities; a tooling gap must not be hidden with product input delays or custom staging scripts.

## 3. Add more farm-building interiors

Support only interiors owned by buildings on the player's farm. Existing building types placed on other maps must not become implicit targets. Review one additional building type at a time, such as Coop or Shed, with its own map contract, state inventory, fixture, and real-mod acceptance.

Farmhouse support follows the other farm-building targets. It needs a separate contract for upgrades, beds, kitchen, cellar access, spouse areas, renovation state, and wallpaper/flooring. Town interiors and arbitrary world locations are outside the product scope.

## 4. Optional safe animal relocation

Players and placed decoration, machines, furniture, and crops must be outside an interior before a layout switch. The current implementation also blocks barns with resident animals tracked by home association, even if those animals are temporarily outdoors.

A later explicit option may relocate resident animals onto distinct valid tiles near the new interior's center. The geometric center is not automatically walkable. Before committing a switch, validate enough reachable, unoccupied tiles, preserve animal identity and home ownership, and prepare rollback of both map and animal positions. Do not stack all animals on one tile or relax the current gate before this behavior has its own tests and live proof.

## 5. Complete multiplayer failure acceptance

Verify missing pack, gameplay-hash mismatch, peer without Core, and delayed-handshake cases against released tooling. Acceptance distinguishes rejected requests, unchanged saved selection, fallback/quarantine behavior, and unsafe entry prevention. Two-process validation runs only when explicitly requested.

The existing example and smoke pack remain schema/test material. An original demonstration pack is useful later, but does not substitute for real-mod compatibility.
