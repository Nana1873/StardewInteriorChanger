# Changelog

Alle nennenswerten Änderungen an diesem Projekt werden hier dokumentiert. Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/); veröffentlichte Versionen sollen [Semantic Versioning](https://semver.org/lang/de/) folgen.

## [Unreleased]

### Added

- Initialer Projektvertrag für einen SMAPI-basierten Interior-Selector.
- MVP-Grenze mit `Greenhouse` und `DeluxeBarn`.
- Versioniertes Interior-Pack-Schema `FormatVersion: 1`.
- Host-autorisierter Multiplayer-Vertrag mit exakten Gameplay-Hashes auf allen Peers.
- Fail-closed-Sicherheitsgrenzen für Layoutwechsel und fehlende beziehungsweise abweichende Packs.
- Nicht installierbares Schema-Beispiel ohne Spiel- oder Fremdassets.
- Dokumentierte Lizenz- und Adaptergrenze für Drittanbieter-Inhalte.
- Spielunabhängige Core-Domäne für Registry, Pfadsicherheit, kanonische Gameplay-Hashes, Auswahlzustand und Peer-Kompatibilität.
- Nativer SMAPI-Content-Pack-Lader mit isolierten Fehlern pro Variante und struktureller Map-Prüfung.
- Versionierte Auswahl pro Gebäudeinstanz und exaktem Zielvertrag in `Building.modData`.
- Host-autoritative Farmhand-Requests, Varianten-Fingerprint-Handshake und Client-Reconcile nach NetField-Synchronisierung.
- Konsolenbefehle `sic targets`, `sic list`, `sic current`, `sic set` und `sic vanilla`.
- Strenger Leere-Raum-Check vor expliziten Wechseln; keine automatischen Moves oder Deletes.
- Golden-Master-Vertrag für Deluxe Barn: `Warp`, `AutoFeed`, `ProduceArea` und zwölf `Trough`-Tiles gegen Stardew 1.6.15 verifiziert.
- Isoliertes, vollständig eigenes Smoke-Fixture für Content-Pack-Discovery und Map-Laden.
- Core-eigene Managed-Map-Proxies mit Vanilla-Fallback vor erfolgreichem Farmhand-Handshake und Zugangs-Quarantäne bei fehlender Hash-Parität.
- Atomarer Map-/Auswahl-Apply mit Rollback sowie erneuter Validierung der tatsächlich aufgelösten Runtime-Map.
- Vollständiger Warp-, Barn-Kapazitäts- und One-way-Location-Flag-Vertrag.
- Fail-closed Schutz gegen Pack-Symlinks/Junctions und Gameplay-Abhängigkeiten außerhalb von `GameplayRoot`.
- OS-konsistente physische Pfadgrenzen sowie frühe Prüfung fehlender lokaler TMX-/TSX-Bilddateien bei weiterhin erlaubten Vanilla-GameContent-Tilesheets.
- SaveLoaded-spezifischer exakter Custom-Restore mit persistenter Leerraum-Quarantäne nach fehlenden, geänderten oder fehlgeschlagenen Varianten.
- Korrekte Host-Autorisierung für lokalen Split-Screen und erzwungener lokaler Farmhand-Reload ohne Wiederverwendung von Stardews Multiplayer-Map-Cache.
- Gebäudeinstanz-spezifische Proxy-Keys und Client-Attestierung verhindern Cache-Reloads und Freigaben über mehrere Scheunen hinweg; der Farmhand-Zugangs-Guard läuft fail-closed pro Tick.

### Changed

- Live-Tests, Fixtures und Reviews wurden vollständig auf das öffentliche SDVKit migriert; dabei gefundene generische Fixture-Lücken wurden upstream in SDVKit behoben. `single` bleibt Standard und `network-2` ausdrücklich angeforderten Multiplayer-Abnahmen vorbehalten.

### Removed

- Den ehemaligen projektspezifischen Ingame-QA-Harness samt `sicqa`-Befehlen entfernt, nachdem SDVKit dessen Save-, Fixture-, Prozess-, Screenshot- und Multiplayer-Aufgaben übernommen hat.

### Fixed

- Frisch gebaute Deluxe-Scheunen werden nicht länger durch Stardews fest eingebauten Feed Hopper `(BC)99` als vermeintlich belegter Innenraum blockiert; echte platzierte Objekte bleiben Blocker.
