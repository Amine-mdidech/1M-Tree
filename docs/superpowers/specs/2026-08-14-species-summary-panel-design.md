# Species Summary Panel — Design

*Stage 4 of the animal-release refactor (stage 1: species roster cleanup +
tabbed Laboratory; stage 2: manual, criteria-gated release; stage 3: HUD
category/species picker + tap-to-place release — all done, see the
`2026-08-14-*-design.md` specs for those). This stage adds a new panel
summarizing every species/tree currently present in the world, grouped by
category and expandable per category.*

## Purpose

The player currently has no single place to see "what's alive in my world
right now" across trees, herbivores, and predators at a glance. Individual
counts exist scattered across the Codex (static info pages, not live
counts) and world observation (tapping an animal shows its name). This
stage adds a dedicated panel that answers "what do I have, and how many"
in one place, grouped by Trees/Herbivores/Predators, each group
independently expandable.

## Current state

- `ScreenNavigator.cs` (104 lines) is a flat, uniform router: one
  `[SerializeField]` panel reference each for `codexUI`/`laboratoryUI`/
  `profileUI`/`demoUI`/`settingsUI`/`mapUI`, plus `hudUI`. Each `OpenX()`
  method calls `CloseOtherPanels(thisPanel)` then `thisPanel.SetOpen(true)`
  then `SetHomeToolbarVisible(false)`. `CloseOtherPanels(except)` closes
  every other registered panel via `SetOpen(false)`, skipping `except` by
  reference.
- Every existing panel (`Profile.uxml` is the clearest example, 55 lines)
  follows the same UXML shape: a `{name}-root` element (classes
  `{name}-root theme-scope`) containing a `{name}-frame` child, which
  holds a `{name}-header` (title `Label` + `close-button` "X") and a
  content area. Two `<ui:Style>` refs: shared `Theme.uss` +
  panel-specific `{Name}.uss`. Opening/closing goes through static
  `PanelAnimator.Open(root, frame)` / `.Close(root, frame)` —
  `Open` sets `display:Flex`, adds then removes a `.panel-enter` class
  after a 10ms delay (pop-in); `Close` adds `.panel-enter` then hides
  after 180ms. Each panel's own `-frame` class declares a matching
  `transition-property` for `.panel-enter` to animate against.
- No expandable/accordion/foldout UI pattern exists anywhere in this
  codebase yet (`Assets/UI/`, `Assets/Scripts/UI/`) — this stage
  introduces it for the first time. The closest existing precedent for
  "grouped by category" is `LaboratoryUI.cs`'s tab system
  (Plants/Herbivores/Predators tab buttons toggling a
  `lab-tab-button--active` class, each tab driving a dynamically-built
  `ScrollView` via `BuildTree()`) — that's category-*switching*, not
  simultaneous expand/collapse, but is the closest three-category
  grouping UI to model the section-header interaction against.
- `EcosystemState.cs` (51 lines, static class) exposes: `TotalTreeCount`
  (int, aggregate across all vegetation types), `DistinctVegetationTypeCount`,
  `HasVegetationType(string vegetationId)`, `GetSpeciesPopulation(string speciesId)`
  (int, 0 if absent), `GetAllPopulations()`
  (`IEnumerable<KeyValuePair<string,int>>`, every species at once). There
  is **no per-vegetation tree count** — that has to come from iterating
  `PlantedTree.ActiveTrees` and grouping by `tree.Vegetation.id`.
- `VegetationDefinition.cs:39` has `public Sprite heroSprite` — an
  optional fal.ai-rendered distinct sprite per vegetation type (Oak
  already has one, from the "oak hero tree" work). When `heroSprite` is
  null, `PlantedTree.cs:227` falls back to a procedural silhouette via
  `PixelArtUtility.CreateVegetationSprite(64, vegetation.plantSilhouette, foliageColor, trunkColor, seed)`
  — still visually distinct per vegetation type even without a hand-made
  asset.
- `SpeciesDefinition.cs` has **no** `Sprite` field of its own.
  `EmergenceSystem.cs:460` `GetCreatureFrames(SpeciesDefinition)` is
  `private static`, returns `Sprite[]` (2 walk-cycle frames), cached in
  `private static readonly Dictionary<string, Sprite[]> creatureFramesBySpeciesId`.
  Built via `BuildCreatureFrames` → `PixelArtUtility.CreateQuadrupedSprite`.
  Not currently exposed outside the class — this stage needs a small
  public accessor added.
- `EmergenceSystem.CanRelease(SpeciesDefinition)` (already exists, stage 2)
  is the source of truth for release criteria: for predators, checks
  `preyThreshold`/`preyToPredatorRatio` against total prey population; for
  herbivores, checks `requiredVegetationIds` and `treesPerIndividualCap`
  against `EcosystemState.TotalTreeCount`. This stage surfaces that same
  criteria as human-readable text — it does not add new criteria logic.
- `Hud.uxml`'s `action-cluster` currently holds 4 icon buttons (`lab-button`,
  `codex-button`, `profile-button`, `settings-button`) plus the separate
  `demo-button` (positioned independently, top-right). A 5th button fits
  the same `action-cluster` row.

## Scope

**In:**
- A new `SpeciesSummaryUI` panel (`Assets/UI/SpeciesSummary/SpeciesSummary.uxml`/`.uss`,
  `Assets/Scripts/UI/SpeciesSummaryUI.cs`), following the standard
  root/frame/header + `PanelAnimator` shape every other panel uses.
- A new `species-summary-button` icon in the HUD's `action-cluster`,
  wired through `ScreenNavigator`'s existing pattern: a new
  `[SerializeField] private SpeciesSummaryUI speciesSummaryUI;` field, a
  new `OpenSpeciesSummary()` method, and an entry in `CloseOtherPanels`.
- Three accordion sections — Trees, Herbivores, Predators — each with a
  header showing a live count (e.g. "Trees (3)") that independently
  toggles its row list open/closed on tap. Multiple sections can be open
  at once.
- Trees section: one row per vegetation type with ≥1 currently-planted
  tree (`PlantedTree.ActiveTrees` grouped by `tree.Vegetation.id`), row =
  icon (`heroSprite` or the same procedural fallback `PlantedTree`
  already uses) + display name + tree count. Not tappable.
- Herbivores/Predators sections: one row per species with
  `EcosystemState.GetSpeciesPopulation(id) > 0`, row = icon (new public
  `EmergenceSystem` accessor exposing frame 0 of the existing procedural
  creature sprite) + display name + population count. Tappable — opens a
  small popup with that species' release criteria in human-readable form,
  reusing `CanRelease`'s existing math (not new criteria logic).
- Live refresh while the panel is open, matching `HudUI`'s existing
  Update-tick refresh pattern (`RefreshGemCount`/`RefreshXp`) — counts and
  section membership update without needing to close and reopen the panel.

**Out:**
- Any change to `CanRelease`'s criteria math itself — this stage only
  displays it.
- Any change to the Codex, Demo panel, or HUD picker (stages 1-3,
  untouched here).
- Locked/never-unlocked or zero-population species — per the "currently
  alive/planted only" presence rule, these simply don't appear as rows.
- Tree row tap interaction — tree rows show icon/name/count only, no tap
  behavior (planting has no `CanRelease`-style criteria to surface).

## Design

### Panel shape and entry point

`SpeciesSummaryUI` is a `[RequireComponent(typeof(UIDocument))]`
`MonoBehaviour` following the exact shape of `ProfileUI`/`CodexUI`: a
`species-summary-root` (classes `species-summary-root theme-scope`)
containing `species-summary-frame` → `species-summary-header` (title
Label "Species" + `close-button`) and a `ScrollView` content area.
`SetOpen(bool)` delegates to `PanelAnimator.Open`/`.Close` exactly like
every other panel.

`ScreenNavigator` gains `[SerializeField] private SpeciesSummaryUI speciesSummaryUI;`
and:
```csharp
public void OpenSpeciesSummary()
{
    CloseOtherPanels(speciesSummaryUI);
    speciesSummaryUI.SetOpen(true);
    SetHomeToolbarVisible(false);
}
```
with `speciesSummaryUI` added to `CloseOtherPanels`'s close-list. `Hud.uxml`
gains a 5th button in `action-cluster`:
```xml
<ui:Button name="species-summary-button" class="icon-button" />
```
`HudUI.cs` wires it exactly like the other four action-cluster buttons —
`OnSpeciesSummaryButtonClicked()` calling
`screenNavigator.OpenSpeciesSummary()` (or `speciesSummaryUI.SetOpen(true)`
as the direct fallback, matching the existing `screenNavigator != null ? ... : ...`
pattern every other button already uses) — with a new badged glyph icon
(`PixelArtUtility.CreateBadgedGlyphSprite`, matching the procedural-icon
style of lab/codex/profile/settings, not a new fal.ai asset — this button
isn't part of the "AAA token icon" exception the picker icons used).

### Accordion sections

Three `VisualElement` blocks, one per category, each built from a shared
helper. A section = a clickable header (`Label` "Trees (3)" +
`picking-mode` normal, or a `Button` styled as a header) plus a
`VisualElement` row-list container whose `display` toggles between
`Flex`/`None` on header tap. Each section tracks its own
`bool isExpanded` field (three independent booleans) — no shared "only one
open" state, per the accepted design.

### Building rows

On `OnEnable`/refresh, `SpeciesSummaryUI` rebuilds all three row lists:

**Trees:** group `PlantedTree.ActiveTrees` by `tree.Vegetation.id` into a
`Dictionary<string, int>` count and keep one representative
`VegetationDefinition` per id (from any tree instance's `.Vegetation`).
For each entry with count > 0, create a row: icon = `vegetation.heroSprite`
if non-null, else `PixelArtUtility.CreateVegetationSprite(...)` (same call
`PlantedTree.cs:227` already makes, reusable since the inputs —
`plantSilhouette`, `foliageColor`, `trunkColor`, a stable seed — all come
from the `VegetationDefinition` itself), name = `vegetation.displayName`,
count = the grouped count. No click handler.

**Herbivores / Predators:** iterate `EmergenceSystem.GetUnlockedSpeciesIds()`
(already exists) filtered to `EcosystemState.GetSpeciesPopulation(id) > 0`,
split by `SpeciesDefinition.category == Predator` vs. not. For each, row =
icon (new `EmergenceSystem.GetSpeciesIconSprite(SpeciesDefinition)` public
method returning `GetCreatureFrames(species)[0]`, reusing the existing
private cache) + `species.displayName` + population count. Click handler
opens a small popup (new lightweight `VisualElement`, shown/hidden the
same way `HudUI`'s `land-lock-popup-root` is — centered overlay,
`display:Flex`/`None`) with text built from `CanRelease`'s criteria:
- Predator: `"Needs {preyThreshold}+ prey present (currently {totalPrey}). Capacity: 1 per {preyToPredatorRatio} prey."`
- Herbivore with `treesPerIndividualCap > 0`: `"Needs {treesPerIndividualCap} trees planted per individual (currently {TotalTreeCount} trees)."`
- Herbivore with `requiredVegetationIds`: appends `"Requires: {comma-separated display names}."`

This popup is purely informational (no unlock/action button, unlike the
land-locked popup) — just the message and a close tap.

### Live refresh

`SpeciesSummaryUI.Update()` re-runs the row-building logic on a short
interval (matching `HudUI`'s pattern of polling every frame for
gem/XP — trees/populations change infrequently enough that even a
per-frame rebuild is cheap at this data scale, but a `RefreshInterval`
similar to the minimap's 0.5s is used to avoid rebuilding 3 `ScrollView`s
worth of rows every single frame for no visible benefit). Only rebuilds
while the panel is open (`SetOpen(true)`), mirroring how `HudUI` only
refreshes the minimap on its own timer regardless of visibility today —
here, gating on open state is the addition, since rebuilding UI rows while
the panel is hidden would be pure waste.

## Testing

- New `SpeciesSummaryTests.cs` (EditMode): a pure-logic test class (no
  `SpeciesSummaryUI` MonoBehaviour instantiation, since UI Toolkit
  element construction isn't meaningfully unit-testable) covering the
  grouping/filtering logic as extracted static or public helper methods:
  grouping `PlantedTree.ActiveTrees` by vegetation id excludes zero-count
  types; filtering species to population > 0 excludes zero-population
  ones; the criteria-text builder produces the expected string for a
  predator, a herbivore with a tree cap, and a herbivore with required
  vegetation.
- `EmergenceSystem`: one new test confirming `GetSpeciesIconSprite`
  returns a non-null sprite for an unlocked species (mirrors the existing
  `SpawnMissingCreatureVisuals_PreviouslyCachedNullFrames_SelfHealsWithValidSprite`
  self-healing coverage — reuses the same cache).
- `SceneRosterWiringTests.cs`: confirm `ScreenNavigator.speciesSummaryUI`
  and `HudUI`'s new button are wired in the scene, following the exact
  pattern of the stage-3 `CameraPanController_EmergenceSystem_IsWired`/
  `HudUI_PickerIconSprites_AreWired` additions.
- Manual verification: open the panel, confirm all three sections show
  correct live counts; plant a tree / release an animal while the panel
  is open and confirm the relevant section's count updates without
  closing/reopening; tap an animal row and confirm the criteria popup
  shows correct, current numbers; confirm tree rows are not tappable.
