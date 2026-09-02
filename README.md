# Stardew Interior Changer

Stardew Interior Changer ist ein SMAPI-Framework, mit dem Spieler registrierte Innenraumvarianten für unterstützte Farmgebäude auswählen können. Das Gebäude und sein gespeicherter Spielzustand bleiben dabei erhalten; ausgetauscht wird ausschließlich die dafür registrierte Innenraum-Map.

> **Projektstatus:** funktionaler MVP-Prototyp, noch kein veröffentlichter Mod-Release. Release-Build, 38 Core-Tests, echte Greenhouse-/Deluxe-Barn-Wechsel, Save-/Prozess-Restore und die positive Host/Farmhand-Strecke sind verifiziert. Live-Smoke, Fixtures und Reviews laufen kanonisch über die weiter unten im README gepinnte öffentliche SDVKit-Version. Negative Multiplayer-Paritätsfälle wie ein fehlendes Pack oder ein abweichender Gameplay-Hash sind noch nicht live mit zwei Prozessen abgenommen. Das Pack unter `examples/` bleibt ausschließlich ein nicht installierbares Schema-Beispiel.

## Ziel des MVP

- `Greenhouse` als einmaliger Innenraum der Farm.
- `DeluxeBarn` als separat auswählbarer Innenraum pro Gebäudeinstanz.
- Vanilla als explizite, sichere Auswahl.
- Native Interior-Packs mit einem kleinen, versionierten Schema.
- Host-autorisierte Auswahl und Speicherung im gemeinsamen Save.
- Sicherheitsprüfung vor jedem Wechsel; keine stillen Löschungen oder Verschiebungen.
- Multiplayer-Abgleich von Core-Mod und exaktem Gameplay-Hash jeder verwendeten Variante.

Nicht Teil des MVP sind Farmhaus-Umbauten, die automatische Migration beliebiger Layouts und der automatische Import bestehender Content-Patcher- oder XNB-Replacer.

## Multiplayer-Vertrag

Die Auswahl ist gemeinsamer Weltzustand und wird deshalb ausschließlich vom Host bestätigt und gespeichert. Farmhands senden eine Auswahl-Anfrage; nur der Host validiert Map, Inhalt und Peer-Parität und ändert anschließend den gemeinsamen Save.

Für eine benutzerdefinierte Variante müssen auf **allen Peers** vorhanden sein:

1. Stardew Interior Changer Core in einer kompatiblen Protokollversion;
2. dieselbe globale Varianten-ID (`<PackUniqueID>/<Id>`);
3. derselbe vom Core berechnete Gameplay-Hash.

Der Hash umfasst die kanonische gameplay-relevante Variantendefinition und alle Dateien unter `GameplayRoot`. `DisplayName` und ein Preview außerhalb von `GameplayRoot` sind rein lokal/kosmetisch und werden nicht einbezogen; eine Preview-Datei innerhalb des Roots wird ausnahmslos mitgehasht. Bei fehlender Variante oder abweichendem Hash wird kein benutzerdefinierter Wechsel freigegeben und die gespeicherte Auswahl nicht stillschweigend geändert.

Custom-Maps werden nicht mit dem internen Asset-Key eines Content-Packs im Save synchronisiert. Der Core vergibt stabile eigene Proxy-Keys pro Gebäudeinstanz, damit etwa zwei Scheunen mit derselben Variante unabhängig und ohne gemeinsamen Cache-Reload behandelt werden. Ein Farmhand mit installiertem Core erhält bis zum erfolgreichen Host-Handshake eine geprüfte Vanilla-Fallback-Map; bei fehlender oder abweichender Variante bleibt dieser Fallback aktiv und der Zugang zum betroffenen Custom-Interior wird blockiert. Der Core muss deshalb vor dem Beitritt auf jedem Peer installiert sein. Die positive Strecke ist mit zwei echten lokalen SMAPI-Prozessen verifiziert: Farmhand-Beitritt, Registry-Handshake, Request an den Host, host-autorisierter Apply und Reconcile auf denselben Vanilla- beziehungsweise Proxy-Map-Key. Missing-Pack- und Hash-Mismatch-Fälle sind durch Core-Tests abgedeckt, aber noch nicht live mit zwei Prozessen abgenommen.

Lokaler Split-Screen läuft auf dem Host-Rechner und verwendet deshalb direkt dessen geprüfte Registry statt der Quarantäne für entfernte Farmhands. Der Core muss auch beim Laden und Speichern des Saves installiert bleiben: Eine komplette Session ohne Core kann naturgemäß nicht als unsicher markiert werden. Wurde ein Save ohne Core weitergespielt, sollte ein Custom-Interior vor der Reaktivierung geleert oder die bereits geladene Vanilla-Map mit `sic vanilla` übernommen werden.

Diese strengere Regel ist Absicht: Content Patcher lädt Maps lokal. Ohne dieselben Maps können Spieler unterschiedliche Geometrie und Begrenzungen sehen. Siehe [Content Patcher: Multiplayer](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/README.md#multiplayer).

## Sicherheitsgrenzen

- Ein Wechsel findet nur nach erfolgreicher Validierung statt.
- Der Core löscht und verschiebt keine Tiere, Objekte, Maschinen, Möbel, Pflanzen oder andere gespeicherte Entitäten automatisch.
- Kann die Ziel-Map den vorhandenen Zustand nicht sicher aufnehmen, wird der Wechsel abgelehnt.
- Auch der Wechsel zurück zu Vanilla durchläuft dieselbe Validierung.
- Pack-Pfade müssen innerhalb des jeweiligen Pack-Verzeichnisses bleiben; absolute Pfade und `..`-Ausbrüche werden abgelehnt.
- Symlinks und Junctions innerhalb eines Pack-Dateibaums werden fail-closed abgelehnt.
- Pack-lokale TMX-, TSX- und Tilesheet-Abhängigkeiten müssen unter `GameplayRoot` liegen und werden damit gehasht.
- Fehlende oder ungültige Pack-Dateien deaktivieren die betroffene Variante, nicht den gesamten Save.
- Ein Content-Pack ist Dateninhalt, kein Mechanismus zum Nachladen oder Ausführen fremden Codes.

Die technische Aufteilung und die bewusst noch offenen Runtime-Entscheidungen stehen in [docs/architecture.md](docs/architecture.md).

## Entwicklung und Prüfung mit SDVKit

Voraussetzungen für den aktuellen Projektvertrag sind Stardew Valley 1.6.15, SMAPI 4.5.2 und zum Bauen das .NET 8 SDK. Der Mod selbst zielt passend zum Spiel auf .NET 6. Live-Prüfungen verwenden ausschließlich das frisch heruntergeladene öffentliche [SDVKit v0.5.3](https://github.com/Nana1873/SDVKit/releases/tag/v0.5.3) mit dem erwarteten ZIP-SHA-256 `54cb3d93bc46599fba339962a2a4f20f27c3ea2f92a0a29e60c57e712cb3cd1a`; ein lokaler SDVKit-Source-Build ist keine Ersatzlaufzeit. Das Kommando `sdvkit` in den folgenden Beispielen bezeichnet die unter `.sdvkit/` entpackte öffentliche Binärdatei.

Die kanonischen relativen Ziele sind:

- Mod: `src\StardewInteriorChanger`
- Unit-Tests: `tests\StardewInteriorChanger.Core.Tests`
- Review-Pack: `tests\fixtures\SmokeGreenhousePack`

Die GitHub-CI führt ausschließlich die spieldateifreien Core-Tests aus. Vollständiger Mod-Build, Packaging, SMAPI-Load und Ingame-Reviews bleiben lokale SDVKit-Prüfungen.

Automatische Prüfschritte bleiben getrennt bewertbar:

```powershell
sdvkit doctor --json
sdvkit project inspect .\src\StardewInteriorChanger --json
dotnet test .\StardewInteriorChanger.sln -c Release
sdvkit project build .\src\StardewInteriorChanger --json
sdvkit project package .\src\StardewInteriorChanger --json
sdvkit project smoke .\src\StardewInteriorChanger --topology single --json
```

SDVKit hält Builds, Pakete, Profile, Saves, Staging, Logs, Screenshots und Prozesszustand unter `.sdvkit/`. Normale Saves, normale beziehungsweise mod-manager-eigene Mods und Vortex-Staging bleiben außerhalb des Workflows.

## Aktuelle Entwicklerbefehle

Nach dem Laden eines Saves stehen in der SMAPI-Konsole bereit:

```text
sic targets
sic list
sic current
sic set <variantId> [buildingId]
sic vanilla [buildingId]
```

`sic targets` zeigt die stabilen Gebäude-IDs. Ein echter Kartenwechsel wird nur bei leerem Innenraum ausgeführt: kein Spieler, Tier, platziertes Objekt, Möbelstück, Crop oder anderer persistenter Inhalt darf sich darin befinden. Stardews fest eingebauter Feed Hopper `(BC)99` in Tierhäusern zählt dabei als Gebäudeausstattung und nicht als platziertes Spielerobjekt; er bleibt beim Mapwechsel erhalten. Dadurch löscht oder verschiebt der MVP niemals Save-Inhalt. Bereits gespeicherte Custom-Maps werden beim normalen Save-Load nur dann automatisch wiederhergestellt, wenn ID und gespeicherter Gameplay-Hash exakt zum installierten Pack passen. Hat der Core zwischenzeitlich ein fehlendes, geändertes oder nicht ladbares Pack gesehen, setzt er eine persistente Quarantänemarkierung; eine spätere Wiederherstellung verlangt dann ebenfalls einen leeren Innenraum. Ein reines Übernehmen der bereits geladenen Vanilla-Map verändert keine Map und kann diese Markierung sicher per `sic vanilla` auflösen.

Der lokale Spielpfad liegt in einer von Git ignorierten `.csproj.user`-Datei. Auf einem anderen Rechner kann er einmalig dort oder mit `-p:GamePath="..."` angegeben werden. Automatisches Deploy in den normalen `Mods`-Ordner ist im Projekt deaktiviert.

## Funktionale und visuelle Review

Die lokale Ingame-Abnahme folgt dem öffentlichen SDVKit-Skill `sdv-project-review`. Das SDVKit-eigene Testsave wird vorbereitet und anschließend ausdrücklich für den Review ausgewählt. Das vorhandene ConsoleCommands-Mod wird nur als expliziter Companion für `debug sleep` mitgegeben:

```powershell
sdvkit lab test-save --topology single --json
sdvkit project review start .\src\StardewInteriorChanger `
  --topology single `
  --test-save `
  --companion <bereites-ConsoleCommands-Modverzeichnis> `
  --content-pack .\tests\fixtures\SmokeGreenhousePack `
  --json
sdvkit project review status --topology single --json
```

Generischer Weltzustand wird ausschließlich über die begrenzten `sdvkit fixture ...`-Konsolenbefehle vorbereitet. Die eigentliche Modfunktion wird über `sic targets`, `sic list`, `sic current`, `sic set` und `sic vanilla` geprüft. Screenshots entstehen ausschließlich über `sdvkit screenshot <label>`; akzeptiert werden sie erst nach konkreter AlwaysOn-Bestätigung, vorhandener PNG und echter visueller Prüfung. `commandWritten=true` belegt nur die Zustellung eines Konsolenbefehls.

Ein Review mit Testsave umfasst einen echten Prozess-Neustart mit derselben Work-Copy. Nach finalem Save und Stop wird ausschließlich über SDVKit zurückgesetzt:

```powershell
sdvkit project review stop --topology single --json
sdvkit project review start .\src\StardewInteriorChanger `
  --topology single `
  --test-save `
  --companion <bereites-ConsoleCommands-Modverzeichnis> `
  --content-pack .\tests\fixtures\SmokeGreenhousePack `
  --json
# Persistenz und Vanilla-Restore prüfen, speichern und erneut stoppen.
sdvkit project review stop --topology single --json
sdvkit project review reset --topology single --json
```

`single` ist der Standard. `network-2` wird nur für ausdrücklich verlangte Multiplayer-Abnahmen verwendet und folgt ausschließlich dem SDVKit-Lifecycle: mit der expliziten Auswahl starten, beide Rollen prüfen, stoppen, mit exakt derselben Auswahl neu starten, erneut stoppen und abschließend `project review reset --topology network-2 --json` ausführen. Es gibt in diesem Repository keinen eigenen Stager, Launcher oder Zwei-Prozess-Controller.

Verifiziert sind fachlich:

- Greenhouse und zwei voneinander unabhängige Deluxe Barns mit Vanilla- und Custom-Wechseln;
- Blocker für echte platzierte Objekte, Spieler und Tiere bei unverändert erhaltenem eingebautem Feed Hopper;
- Save, kompletter Prozess-Neustart und exakter Restore der gewählten Maps und gespeicherten Entitäten;
- ein echter zweiter Farmhand über Stardews New-Farmhand-Flow;
- Farmhand-Requests für Vanilla und Custom, host-autorisierter Apply sowie identische Ziel-Map auf Host und Client.

Nicht als live bewiesen gelten bisher Missing-Pack-/Hash-Mismatch-Fälle, ein Peer ohne Core, ein verzögerter Handshake bei bereits belegtem Custom-Interior und das Remote-Spieler-Occupancy-Gate.

## Interior-Packs

Interior-Packs sind native SMAPI-Content-Packs für `StardewInteriorChanger.Core`, keine gewöhnlichen Content-Patcher-Replacer. Einstieg, Schema und Hash-Grenze sind in [docs/content-packs.md](docs/content-packs.md) dokumentiert.

SMAPI definiert dafür `manifest.json` und `ContentPackFor`; das eigentliche Pack-Schema gehört dem Core-Mod. Siehe die offiziellen SMAPI-Seiten zu [Content-Packs](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Content_Packs) und zum [Manifest](https://stardewvalleywiki.com/Modding:Modder_Guide/APIs/Manifest).

## Warum bestehende Replacer nicht automatisch auswählbar sind

Content-Patcher-Packs stellen keinen Katalog eigenständiger Innenräume bereit. Sie patchen zur Laufzeit gemeinsame Assets:

- `Load` ersetzt ein vollständiges Asset; bei konkurrierenden Loadern wird nur einer ausgewählt.
- `EditMap` kann Tiles, Eigenschaften und Warps überlagern.
- Bedingungen, Tokens, Konfiguration, Abhängigkeiten und Lade-Reihenfolge können das Ergebnis verändern.
- Spätere Patches erhalten bereits das zusammengeführte Ergebnis der vorherigen Patches.

Der Core könnte daher höchstens den aktuell aufgelösten Endzustand sehen. Daraus lassen sich die ursprünglichen Varianten, zulässigen Gebäudetypen, Ein-/Ausgänge, Abhängigkeiten und Rechte nicht zuverlässig rekonstruieren. Bestehende Innenräume benötigen ein natives Pack oder einen geprüften, erlaubten Adapter.

Offizielle Details: [Content Patcher `Load`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-load.md), [`EditMap`](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/action-editmap.md) und [Zusammenspiel mehrerer Patches](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide.md#how-do-multiple-patches-interact).

## Fremdassets, Lizenzen und Adapter

Core und offizielle Beispiel-Packs enthalten nur eigene Assets oder Inhalte, deren Lizenz die konkrete Nutzung und Weitergabe ausdrücklich erlaubt. Maps, Tilesheets, Vorschaubilder oder andere Dateien fremder Mods werden nicht ohne dokumentierte Erlaubnis kopiert oder neu veröffentlicht. Ein Credit oder Link ersetzt keine Erlaubnis.

Ein veröffentlichter Drittanbieter-Adapter soll:

- die Zustimmung des ursprünglichen Autors besitzen;
- die Original-Mod nach Möglichkeit als separate Abhängigkeit voraussetzen;
- nur notwendige Metadaten und Integrationslogik enthalten;
- unterstützte Originalversionen und Abhängigkeiten dokumentieren;
- keine Fremdassets enthalten, sofern deren Lizenz oder schriftliche Erlaubnis dies nicht eindeutig gestattet.

Ist die Erlaubnis unklar, wird kein öffentlicher Adapter ausgeliefert. Nexus Mods verlangt für bestehende Nutzerinhalte eine Erlaubnis und stellt ausdrücklich klar, dass Namensnennung allein nicht genügt. Siehe [Nexus Mods File Submission Guidelines](https://help.nexusmods.com/article/28-file-submission-guidelines).

## Lizenz

Die eigenen Inhalte dieses Repositorys stehen unter der [MIT-Lizenz](LICENSE). Diese Lizenz gilt ausschließlich für die eigenen Inhalte dieses Repositorys und überträgt keine Rechte an Stardew Valley oder fremden Content-Packs. Die Kompatibilität eines Interior-Packs mit diesem Core überträgt oder erweitert keine Rechte an dessen Assets; Pack-Autoren bleiben für Lizenz, Erlaubnisse und Credits ihrer Inhalte verantwortlich.
