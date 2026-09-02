# Architektur

Dieses Dokument beschreibt die Zielarchitektur und die verbindlichen Invarianten des MVP. Es ist keine Behauptung, dass jeder beschriebene Baustein bereits implementiert oder im Spiel verifiziert ist.

## Leitgedanke

Ein Gebäude beziehungsweise eine gespeicherte `GameLocation` besitzt den persistenten Spielzustand. Die Map ist das geladene Asset für Layout, Tilesheets und Tile-Eigenschaften. Stardew Interior Changer wechselt eine registrierte Map, ohne die Identität der Location oder ihre gespeicherten Entitäten zu ersetzen.

Diese Trennung entspricht der offiziellen Content-Patcher-Dokumentation: Die Location enthält unter anderem Objekte, Möbel, Pflanzen, NPCs und Spieler; die Map beschreibt Tiles und Eigenschaften. Siehe [Maps vs locations](https://github.com/Pathoschild/StardewMods/blob/develop/ContentPatcher/docs/author-guide/custom-locations.md#maps-vs-locations).

## Bausteine

### Core-Mod

`StardewInteriorChanger.Core` ist der einzige aktive Codebaustein. Er entdeckt eigene SMAPI-Content-Packs, validiert deren Schema und Dateien, erstellt die Varianten-Registry und koordiniert Save, Wechsel und Multiplayer.

### Varianten-Registry

Jede Variante erhält die globale ID:

```text
<PackManifest.UniqueID>/<Interior.Id>
```

`Id` bleibt innerhalb eines veröffentlichten Packs stabil. `Target` ist im MVP exakt `Greenhouse` oder `DeluxeBarn`; Dimensionen oder Dateinamen werden nicht zur Zielerkennung geraten.

### Gameplay-Hash

Der Core berechnet einen SHA-256-Hash aus:

- der kanonischen gameplay-relevanten Variantendefinition;
- allen Dateien unter `GameplayRoot` mit deterministischer Pfad- und Dateibehandlung.

Pack-Autoren tragen keinen Hash ein. `DisplayName` und ein `Preview` außerhalb von `GameplayRoot` sind davon ausgeschlossen, damit Übersetzungen und Vorschaubilder lokal variieren dürfen. Liegt die Preview-Datei innerhalb des Roots, wird sie wie jede andere Gameplay-Datei mitgehasht. Das konkrete Byte-Framing bleibt internes Protokolldetail und wird gemeinsam mit Protokolltests versioniert.

### Auswahlzustand

Der Host besitzt die autoritative Zuordnung von Gebäudeinstanz zu globaler Varianten-ID. Für das Gewächshaus existiert eine eindeutige Zielinstanz; jede Deluxe-Scheune benötigt eine stabile Gebäudeidentität. Farmhands dürfen Anfragen stellen, aber weder Registry noch Save-Zuordnung direkt verändern.

## Ablauf

### Start und Pack-Erkennung

1. SMAPI lädt den Core und dessen eigene Content-Packs.
2. Der Core liest `interiors.json` mit `FormatVersion: 1`.
3. Schema, globale IDs, Targets und alle aufgelösten Pfade werden validiert.
4. Der Core berechnet für jede gültige Variante den Gameplay-Hash.
5. Nur vollständig gültige Varianten gelangen in die Registry.

### Laden eines Saves

Der gespeicherte Auswahlzustand wird gegen die lokale Registry und den gespeicherten Gameplay-Hash geprüft. Stardew persistiert den `GameLocation.mapPath` nicht; deshalb darf ausschließlich der unmittelbare `SaveLoaded`-Pfad eine exakt passende gespeicherte Custom-Auswahl mitsamt ihrem vorhandenen Inhalt wiederherstellen. Eine fehlende, geänderte oder nicht ladbare Variante löst keine stillschweigende Änderung der Auswahl aus, setzt auf dem Host aber eine persistente `RequiresEmptyRestore`-Quarantäne. Kehrt das Pack später zurück, gilt wieder der vollständige Leerraum-Check. Runtime-Drift außerhalb von `SaveLoaded` wird ebenso behandelt.

Farmhands lösen denselben Core-eigenen Proxy bis zum erfolgreichen Host-Handshake als geprüfte Vanilla-Map auf. Bei dauerhaft fehlender Parität bleibt der Fallback aktiv und der Core blockiert den Zutritt zum betroffenen Custom-Interior. Lokale Split-Screen-Spieler laufen dagegen auf dem Host-Rechner und teilen dessen autorisierte Registry und Map-Auflösung.

### Angeforderter Wechsel

1. Ein lokaler Befehl beziehungsweise eine spätere UI oder ein Farmhand stellt eine Wechselanfrage.
2. Der Host löst Zielgebäude und Zielvariante aus seiner Registry auf.
3. Der Host prüft Multiplayer-Parität und alle Sicherheitsbedingungen.
4. Nur nach erfolgreicher Validierung wird die Map gewechselt und die Auswahl gespeichert.
5. Bei einem Fehler bleibt der bisherige Zustand unverändert und der Grund wird verständlich protokolliert beziehungsweise angezeigt.

## Multiplayer-Protokoll

Jeder Peer benötigt den Core. Beim Verbindungsaufbau werden mindestens Protokollversion und Variantentupel abgeglichen:

```text
(GlobalVariantId, Target, GameplayHash)
```

Eine benutzerdefinierte Variante ist nur nutzbar, wenn jeder verbundene Peer dieselbe globale ID und exakt denselben Gameplay-Hash meldet. Ein fehlender Core, eine unbekannte ID oder ein abweichender Hash schließt diese Variante vom Wechsel aus. Der Host bleibt für Entscheidung und Persistenz zuständig.

Die Hash-Grenze ist enger als eine bloße Versionsnummer: Zwei Packs mit derselben Manifest-Version, aber unterschiedlichen Maps oder Tilesheets sind nicht kompatibel. Umgekehrt verändern lokalisierte Anzeigenamen und Vorschaubilder außerhalb von `GameplayRoot` den Gameplay-Hash nicht.

Der Save enthält für Custom-Maps ausschließlich Core-eigene Managed-Map-Keys, keine direkt synchronisierten Content-Pack-Asset-Keys. Ein Peer mit Core kann deshalb vor der Paritätsentscheidung sicher auf die zielgerechte Vanilla-Map zurückfallen. Ein Peer ohne Core besitzt diesen Loader nicht und wird für einen Save mit aktiven Custom-Interiors nicht unterstützt.

Die Quarantänemarkierung kann nur gesetzt werden, solange der Core selbst geladen ist. Einen ohne Core weitergespielten und gespeicherten Save kann er später nicht beweisbar von einem normalen exakten Restore unterscheiden; dieser Fall bleibt deshalb außerhalb des automatischen Sicherheitsvertrags und verlangt vor Reaktivierung einen leeren Raum oder eine explizite Übernahme von Vanilla.

### Verifikationsstatus

Die positive Protokollstrecke wurde mit Host und echtem zweitem Farmhand verifiziert: New-Farmhand-Beitritt, Registry-Handshake, Farmhand-Request, host-autorisierter Apply und Reconcile auf denselben Vanilla- beziehungsweise gebäudeinstanz-spezifischen Proxy-Key. Missing-Pack-, Hash-Mismatch- und Peer-ohne-Core-Fälle sind statisch und durch Core-Tests geprüft, aber noch nicht als negative Zwei-Prozess-Strecke live abgenommen.

Künftige Live-Abnahmen laufen ausschließlich über das öffentliche SDVKit. `single` ist der Standard-Smoke und -Review; `network-2` wird nur für ausdrücklich verlangte Multiplayer-Abnahmen verwendet. Dessen Review-Lifecycle besteht aus Start und Rollenprüfung, Stop, Neustart mit exakt derselben Auswahl, erneutem Stop und abschließendem SDVKit-Reset. Das Projekt besitzt keine eigene Save-, Staging-, Screenshot- oder Prozesssteuerung.

## Fail-closed-Sicherheitsinvarianten

- Kein expliziter Wechsel, auch nicht zu Vanilla, ohne erfolgreiche Zielvalidierung.
- Keine Löschung und keine automatische Verschiebung persistenter Entitäten.
- Keine Aktivierung einer Variante mit fehlendem oder abweichendem Peer-Hash.
- Keine stillschweigende Änderung der gespeicherten Auswahl bei fehlendem Pack.
- Kein Laden von `Map` außerhalb des deklarierten `GameplayRoot`.
- Keine pack- oder mod-lokale TMX-, TSX- oder Tilesheet-Abhängigkeit außerhalb von `GameplayRoot`.
- Kein Auflösen von `GameplayRoot` oder `Preview` außerhalb des Pack-Roots.
- Absolute Pfade, Traversal (`..`) und Dateisystem-Ausbrüche werden abgelehnt.
- Symlinks und Junctions im Pack-Dateibaum werden fail-closed abgelehnt.
- Eine fehlerhafte Variante wird isoliert deaktiviert und mit Pack-ID, Varianten-ID und Ursache geloggt.
- Unsichere oder nicht beweisbare Zustände führen zur Ablehnung, nicht zu einer bestmöglichen Migration.

Zur Wechselvalidierung gehören mindestens Zieltyp und Upgrade-Stufe, Map-Ladbarkeit, benötigte Layer/Eigenschaften, gültige Anker sowie die Frage, ob bestehende Spieler und persistente Entitäten auf der Ziel-Map gültig bleiben. Konkrete Migrationsregeln gehören nicht in den MVP.

## Content-Patcher-Grenze

Content Patcher kombiniert `Load`- und `EditMap`-Patches abhängig von Priorität, Lade-Reihenfolge, Tokens und Bedingungen. Der resultierende Assetzustand besitzt nicht automatisch die Metadaten eines auswählbaren Interior-Packs. Der Core importiert daher keine beliebigen Replacer und liest keine fremden Pack-Verzeichnisse als implizite Varianten.

Eine spätere, ausdrücklich versionierte Registry-Asset-Integration für Content Patcher ist möglich, aber nicht Voraussetzung des nativen MVP-Packformats.

## Bewusst offen

- Konkrete Ingame-Menüführung und Farmhand-Anfrage-UX.
- Negative Zwei-Prozess-Abnahme für Missing-Pack, Hash-Mismatch, Peer ohne Core und verzögerten Handshake.
- Remote-Spieler-Occupancy-Gate in einer echten Host/Farmhand-Sitzung.
- Autorisierte Migrationsbeschreibungen zwischen deutlich verschiedenen Layouts.
- Weitere Targets nach realer Verifikation von Gewächshaus und Deluxe-Scheune.
- Farmhaus, Farmhöhle, Coop, Shed und Slime Hutch.
