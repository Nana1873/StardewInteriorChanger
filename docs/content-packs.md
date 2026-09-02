# Creating interior packs

Interior packs are native SMAPI content packs for `StardewInteriorChanger.Core`. They register complete, selectable variants with an explicit target type and clear file boundary. They are **not** automatic adapters for existing Content Patcher replacers.

> The `examples/ExampleInteriorPack` directory is a schema example only. The referenced map is intentionally missing. Do not copy it into `Mods` or publish it as a release pack.

## Directory structure

A real pack can use a structure like this:

```text
[IC] My Greenhouse/
├─ manifest.json
├─ interiors.json
├─ i18n/
│  └─ default.json
└─ assets/
   ├─ previews/
   │  └─ roomy-greenhouse.png
   └─ gameplay/
      └─ roomy-greenhouse/
         ├─ map.tmx
         └─ custom-tilesheet.png
```

Every file that changes actual gameplay or the map belongs under the corresponding `GameplayRoot`. Previews should intentionally remain outside it.

## `manifest.json`

The manifest follows the normal SMAPI format. `ContentPackFor` is the key field:

```json
{
  "Name": "My Greenhouse Interiors",
  "Author": "YourName",
  "Version": "1.0.0",
  "Description": "Adds selectable greenhouse interiors.",
  "UniqueID": "YourName.MyGreenhouseInteriors",
  "UpdateKeys": [],
  "ContentPackFor": {
    "UniqueID": "StardewInteriorChanger.Core",
    "MinimumVersion": "0.1.0"
  }
}
```

The pack's `UniqueID` becomes part of every global variant ID and must not change after publication.

## `interiors.json`

Schema version 1:

```json
{
  "FormatVersion": 1,
  "Interiors": [
    {
      "Id": "roomy-greenhouse",
      "DisplayName": "Roomy Greenhouse",
      "Target": "Greenhouse",
      "GameplayRoot": "assets/gameplay/roomy-greenhouse",
      "Map": "map.tmx",
      "Preview": "assets/previews/roomy-greenhouse.png"
    }
  ]
}
```

### Fields

| Field | Required | Contract |
| --- | --- | --- |
| `FormatVersion` | yes | Integer; exactly `1` in the MVP. |
| `Interiors` | yes | Array of variant definitions. |
| `Id` | yes | Stable lowercase ASCII ID matching `[a-z0-9][a-z0-9._-]{0,63}`; `vanilla` is reserved. Global ID: `<PackUniqueID>/<Id>`. Never reuse a published ID for a different layout. |
| `DisplayName` | yes | Visible name. Purely cosmetic and excluded from the gameplay hash. |
| `Target` | yes | Exactly `Greenhouse` or `DeluxeBarn` in the MVP. |
| `GameplayRoot` | yes | Directory relative to the pack root. Every file under it contributes to the gameplay hash. |
| `Map` | yes | Path relative to `GameplayRoot`. The file must resolve within that directory and load as a supported map. |
| `Preview` | no | Path relative to the pack root. Purely cosmetic and excluded from the gameplay hash when outside `GameplayRoot`; if the file is inside `GameplayRoot`, it is hashed like every gameplay file. |
| `Anchors` | no | Reserved for future documented semantic anchors. Omit the field until a target contract publishes concrete names. |

Absolute paths, `..` segments, or other escapes from the pack root or gameplay root are invalid.

## Hash and multiplayer boundary

The Core calculates the gameplay hash; authors do not provide it in JSON. The SHA-256 hash covers the canonical gameplay-relevant definition and every file under `GameplayRoot` without exception. This includes the map, external TSX files, and custom tilesheets. The internal byte framing is part of the versioned Core protocol, not a pack field.

For a custom variant, every peer must have the same global variant ID and exact gameplay hash. A matching manifest version alone is insufficient. `DisplayName` and a preview outside `GameplayRoot` may be localized or cosmetically different because they are excluded from the hash.

## Map and save safety

A pack must not assume that the Core deletes or automatically rearranges stored content. Before a change, the Core validates that the variant matches the target and that existing state remains safe. If safety cannot be proven, the change is rejected.

Pack authors should therefore:

- define the entrance, exit, walkable areas, and required map properties unambiguously;
- account for existing object, animal, furniture, and crop positions;
- never ship a new meaning under an already published `Id`;
- never advertise substantially different layouts as automatically migration-safe;
- always test multiplayer with a Host and at least one Farmhand.

The current runtime contract requires `Back`, `Buildings`, and `Front` layers with identical positive dimensions for both targets, plus complete groups of five values in the map-level `Warp` property. The first warp must lead to `Farm` with non-negative destination coordinates; its entry point is one tile north of the source and must be walkable within the map under Stardew's tile rules. `Passable` on `Back` blocks movement, while a present `Buildings` tile is walkable only with `Passable` or `Shadow`. Persistent one-way properties such as `Outdoors`, `IsFarm`, `IsGreenhouse`, `TreatAsOutdoors`, `forceLoadPathLayerLights`, `indoorWater`, `LocationContext`, and `SeasonOverride` are not allowed. `DeluxeBarn` also requires the Vanilla contracts verified against Stardew 1.6.15: a non-empty map-level `AutoFeed`, a `ProduceArea` fully inside the map with at least twelve tiles walkable under the same rules, and at least twelve tiles with the `Trough` property on the `Back` layer.

Every pack-local or mod-local TMX, TSX, or tilesheet dependency must resolve within the variant's `GameplayRoot`. Vanilla tilesheets may continue to be referenced by their game asset names. Symlinks and junctions are not allowed in the pack tree.

## Third-party assets and adapters

Publish only files you created yourself or for which you have appropriate permission to use, modify, and redistribute. Attribution alone does not replace permission. Retain written consent and document credits and dependencies.

An adapter for another mod should not copy its files. Publish it only with the original author's consent, reference the separately installed original mod, and include only required metadata or integration logic. See the [Nexus Mods File Submission Guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines) for details on the current platform rules.

## Existing Content Patcher packs

An Interior Changer pack is an explicit variant registry, not a generic Content Patcher map replacement. Content Patcher patches are conditional, ordered asset operations; the resolved map does not retain enough reliable metadata to recover independent selectable variants. Existing mods therefore need an opt-in native pack or a permissioned adapter.

### Compact schema reference

- `FormatVersion`: required integer, currently `1`.
- `Interiors`: required array.
- `Id`: required lowercase ASCII slug matching `[a-z0-9][a-z0-9._-]{0,63}`; `vanilla` is reserved; global ID is `<PackUniqueID>/<Id>`.
- `DisplayName`: required cosmetic label; excluded from the gameplay hash.
- `Target`: required; MVP values are `Greenhouse` and `DeluxeBarn`.
- `GameplayRoot`: required pack-relative directory; every file below it is gameplay-hashed.
- `Map`: required path relative to `GameplayRoot`.
- `Preview`: optional pack-relative cosmetic image; excluded only when it is outside `GameplayRoot`, otherwise hashed like every gameplay file.
- `Anchors`: reserved for future documented target contracts; omit it until a concrete anchor name is published.
- Every multiplayer peer needs the Core and the exact same global variant ID/gameplay hash.

## Official references

- [SMAPI: Content Packs](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Content_Packs)
- [SMAPI: Manifest](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest)
- [Content Patcher: `Load`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-load.md)
- [Content Patcher: `EditMap`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-editmap.md)
- [Content Patcher: multiple patches](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md#how-do-multiple-patches-interact)
- [Content Patcher: Multiplayer](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/README.md#multiplayer)
