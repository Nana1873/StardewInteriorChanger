# Interior-Packs erstellen

Interior-Packs sind native SMAPI-Content-Packs für `StardewInteriorChanger.Core`. Sie registrieren vollständige, auswählbare Varianten mit eindeutiger Zielart und klarer Dateigrenze. Sie sind **keine** automatischen Adapter für bestehende Content-Patcher-Replacer.

> Das Verzeichnis `examples/ExampleInteriorPack` ist nur ein Schema-Beispiel. Die referenzierte Map fehlt absichtlich. Nicht in `Mods` kopieren und nicht als Release-Pack veröffentlichen.

## Ordnerstruktur

Ein echtes Pack kann beispielsweise so aufgebaut sein:

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

Alle Dateien, die das tatsächliche Gameplay oder die Map verändern, gehören unter den jeweiligen `GameplayRoot`. Ein Preview gehört bewusst außerhalb davon.

## `manifest.json`

Das Manifest folgt dem normalen SMAPI-Format. Entscheidend ist `ContentPackFor`:

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

Die `UniqueID` des Packs wird Teil jeder globalen Varianten-ID und darf nach Veröffentlichung nicht geändert werden.

## `interiors.json`

Schema-Version 1:

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

### Felder

| Feld | Pflicht | Vertrag |
| --- | --- | --- |
| `FormatVersion` | ja | Ganzzahl; im MVP exakt `1`. |
| `Interiors` | ja | Array der Variantendefinitionen. |
| `Id` | ja | Stabile lowercase-ASCII-ID nach `[a-z0-9][a-z0-9._-]{0,63}`; `vanilla` ist reserviert. Globale ID: `<PackUniqueID>/<Id>`. Eine veröffentlichte ID nie für ein anderes Layout wiederverwenden. |
| `DisplayName` | ja | Sichtbarer Name. Rein kosmetisch und nicht Teil des Gameplay-Hashes. |
| `Target` | ja | Im MVP exakt `Greenhouse` oder `DeluxeBarn`. |
| `GameplayRoot` | ja | Relativer Ordner unter dem Pack-Root. Sämtliche Dateien darin fließen in den Gameplay-Hash ein. |
| `Map` | ja | Pfad relativ zu `GameplayRoot`. Die Datei muss innerhalb dieses Ordners auflösbar und als unterstützte Map ladbar sein. |
| `Preview` | nein | Pfad relativ zum Pack-Root. Außerhalb von `GameplayRoot` rein kosmetisch und nicht Teil des Gameplay-Hashes; liegt die Datei innerhalb von `GameplayRoot`, wird sie wie jede Gameplay-Datei mitgehasht. |
| `Anchors` | nein | Für zukünftige, dokumentierte semantische Anker reserviert. Bis ein Zielvertrag konkrete Namen veröffentlicht, sollte das Feld weggelassen werden. |

Absolute Pfade, `..`-Segmente oder andere Ausbrüche aus Pack- beziehungsweise Gameplay-Root sind ungültig.

## Hash- und Multiplayer-Grenze

Der Core berechnet den Gameplay-Hash; Autoren tragen ihn nicht in JSON ein. Der SHA-256-Hash umfasst die kanonische gameplay-relevante Definition und ausnahmslos alle Dateien unter `GameplayRoot`. Dazu zählen insbesondere Map, externe TSX-Dateien und eigene Tilesheets. Das interne Byte-Framing ist Teil des versionierten Core-Protokolls und kein Pack-Feld.

Für eine Custom-Variante müssen alle Peers dieselbe globale Varianten-ID und exakt denselben Gameplay-Hash besitzen. Eine gleiche Manifest-Version allein genügt nicht. `DisplayName` und ein Preview außerhalb von `GameplayRoot` dürfen lokalisiert oder kosmetisch verschieden sein, weil sie nicht in den Hash eingehen.

## Map- und Save-Sicherheit

Ein Pack darf nicht voraussetzen, dass der Core gespeicherte Inhalte löscht oder automatisch umsortiert. Vor dem Wechsel prüft der Core, ob die Variante zum Ziel passt und ob vorhandene Zustände sicher bleiben. Wenn dies nicht bewiesen werden kann, wird der Wechsel abgelehnt.

Pack-Autoren sollten deshalb:

- Ein- und Ausgang, begehbare Bereiche und benötigte Map-Eigenschaften eindeutig gestalten;
- bestehende Objekt-, Tier-, Möbel- und Pflanzenpositionen berücksichtigen;
- keine neue Bedeutung unter einer bereits veröffentlichten `Id` ausliefern;
- stark abweichende Layouts nicht als automatisch migrationssicher bewerben;
- Multiplayer immer mit Host und mindestens einem Farmhand testen.

Der aktuelle Runtime-Vertrag verlangt für beide Targets gleich große, positive `Back`-, `Buildings`- und `Front`-Layer sowie vollständige Fünfergruppen in der map-level `Warp`-Property. Der erste Warp muss mit nichtnegativen Zielkoordinaten zur `Farm` führen; sein Einstieg liegt ein Tile nördlich der Quelle und muss innerhalb der Map nach Stardews Tile-Regeln begehbar sein. Dabei blockiert `Passable` auf `Back`, während ein vorhandenes `Buildings`-Tile nur mit `Passable` oder `Shadow` begehbar ist. Persistierende One-way-Properties wie `Outdoors`, `IsFarm`, `IsGreenhouse`, `TreatAsOutdoors`, `forceLoadPathLayerLights`, `indoorWater`, `LocationContext` und `SeasonOverride` sind nicht zulässig. Für `DeluxeBarn` kommen die gegen Stardew 1.6.15 verifizierten Vanilla-Verträge hinzu: ein nichtleeres map-level `AutoFeed`, ein vollständig innerhalb der Map liegendes `ProduceArea` mit mindestens zwölf nach denselben Regeln begehbaren Tiles und mindestens zwölf Tiles mit der `Back`-Layer-Property `Trough`.

Alle pack- oder mod-lokalen TMX-/TSX-/Tilesheet-Abhängigkeiten müssen innerhalb des Varianten-`GameplayRoot` auflösbar sein. Vanilla-Tilesheets dürfen weiterhin über ihre Spiel-Asset-Namen referenziert werden. Symlinks und Junctions sind im Pack-Dateibaum nicht zulässig.

## Fremdassets und Adapter

Veröffentliche nur Dateien, die du selbst erstellt hast oder für deren Nutzung, Bearbeitung und Weitergabe du eine passende Erlaubnis besitzt. Namensnennung allein ersetzt keine Erlaubnis. Bewahre schriftliche Zustimmungen auf und dokumentiere Credits sowie Abhängigkeiten.

Ein Adapter für eine andere Mod soll deren Dateien nicht kopieren. Er wird nur mit Zustimmung des ursprünglichen Autors veröffentlicht, verweist auf die separat installierte Original-Mod und enthält ausschließlich notwendige Metadaten beziehungsweise Integrationslogik. Details zu den aktuellen Plattformregeln stehen in den [Nexus Mods File Submission Guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines).

## Existing Content Patcher packs / English note

An Interior Changer pack is an explicit variant registry, not a generic Content Patcher map replacement. Content Patcher patches are conditional, ordered asset operations; the resolved map does not retain enough reliable metadata to recover independent selectable variants. Existing mods therefore need an opt-in native pack or a permissioned adapter.

### Compact schema reference (English)

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

## Offizielle Referenzen

- [SMAPI: Content Packs](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Content_Packs)
- [SMAPI: Manifest](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest)
- [Content Patcher: `Load`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-load.md)
- [Content Patcher: `EditMap`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-editmap.md)
- [Content Patcher: multiple patches](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md#how-do-multiple-patches-interact)
- [Content Patcher: Multiplayer](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/README.md#multiplayer)
