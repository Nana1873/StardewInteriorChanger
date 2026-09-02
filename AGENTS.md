# Agent workflow

- Write repository-facing documentation, code comments, issue and pull-request text, and user-visible project text in English.
- The publicly released SDVKit build is the canonical environment for live tests, fixtures, and reviews. The exact approved version is pinned only in the README; a local SDVKit source build is not a substitute runtime.
- Generic save, profile, staging, process, AlwaysOn, command, screenshot, fixture, and multiplayer functionality belongs in SDVKit. Mod-specific logic, unit tests, content packs, and domain assertions remain in this repository.
- Normal Stardew saves and normal or mod-manager-owned mods must remain untouched. All generated builds, packages, profiles, logs, fixtures, screenshots, and review data belong under `.sdvkit/`.
- `single` is the default topology for smoke tests and reviews. Use `network-2` only for explicitly requested multiplayer validation.
- Functional and visual validation follows the public SDVKit skill `sdv-project-review`; the bounded automated smoke test follows `sdv-project-smoke`.
- The canonical relative targets are the mod at `src\StardewInteriorChanger`, the tests at `tests\StardewInteriorChanger.Core.Tests`, and the review pack at `tests\fixtures\SmokeGreenhousePack`.
