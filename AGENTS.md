# Agent workflow

- Das frisch öffentlich veröffentlichte SDVKit ist die kanonische Umgebung für Live-Tests, Fixtures und Reviews. Die exakte freigegebene Version wird ausschließlich im README gepinnt; eine lokale SDVKit-Source-Version ist keine Ersatzlaufzeit.
- Generische Save-, Profil-, Staging-, Prozess-, AlwaysOn-, Command-, Screenshot-, Fixture- und Multiplayer-Funktionen gehören in SDVKit. Mod-spezifische Logik, Unit-Tests, Content-Packs und fachliche Assertions bleiben in diesem Repository.
- Normale Stardew-Saves sowie normale oder mod-manager-eigene Mods bleiben unangetastet. Alle generierten Builds, Pakete, Profile, Logs, Fixtures, Screenshots und Review-Daten liegen unter `.sdvkit/`.
- `single` ist die Standardtopologie für Smoke und Review. `network-2` wird nur für ausdrücklich verlangte Multiplayer-Abnahmen verwendet.
- Funktionale und visuelle Abnahmen folgen dem öffentlichen SDVKit-Skill `sdv-project-review`; der begrenzte automatische Smoke folgt `sdv-project-smoke`.
- Die kanonischen relativen Ziele sind Mod `src\StardewInteriorChanger`, Tests `tests\StardewInteriorChanger.Core.Tests` und Review-Pack `tests\fixtures\SmokeGreenhousePack`.
