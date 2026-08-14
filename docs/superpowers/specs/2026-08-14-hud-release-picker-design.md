# HUD Category/Species Picker & Tap-to-Place Release — Design

*Stage 3 of the animal-release refactor (stage 1: species roster cleanup +
tabbed Laboratory; stage 2: manual, criteria-gated release, both done —
see the `2026-08-14-*-design.md` specs for those). This stage replaces the
Demo-panel release buttons (a deliberate stand-in from stage 2) with the
real player-facing picker and lets the player choose where a released
animal appears, mirroring tree planting.*

## Purpose

Today the HUD's single round icon button only ever cycles *which
vegetation* to plant. This stage turns it into a full category/species
picker driving both planting and animal release: the icon button switches
between Plant/Herbivore/Predator, the label pill (currently static text)
becomes clickable and switches between the two species in whichever
category is active, and tapping the world does the right thing for
whatever's selected — plants a tree, or releases an animal at that exact
spot, subject to the same ecological criteria stage 2 already built.

## Current state

- `Hud.uxml`'s `vegetation-cluster` contains `vegetation-button` (round
  icon, `icon-button vegetation-icon-button` classes) and `vegetation-label`
  (a plain `Label`, not clickable, text e.g. "Oak").
- `HudUI.cs`: `OnVegetationButtonClicked` → `treePlanter.CycleVegetation()`
  → `RefreshVegetationLabel()`. No category concept exists — it's
  implicitly always "vegetation."
- `TreePlanter.cs` owns `currentVegetationIndex` into `unlockedVegetation`,
  exposed as `CurrentVegetation` (property) and `CycleVegetation()`
  (method) — this is the pattern to mirror for animal species.
- `EmergenceSystem.cs` (post stage 2) has `CanRelease(SpeciesDefinition)`
  and `TryReleaseSpecies(SpeciesDefinition)` (random position, via
  `GetRandomTreePosition`), plus the master lists
  `unlockedNonPredatorSpecies`/`unlockedPredatorSpecies` — but no
  "currently selected species" state, and no position-aware release.
- `CameraPanController.EndPress()`'s final branch (nothing under the tap):
  ```csharp
  else if (treePlanter != null)
  {
      treePlanter.PlantTree(worldPoint);
  }
  ```
  This is the only place a world-tap turns into an action — it needs to
  branch on the active category instead of always planting.
  `CameraPanController` currently has no `EmergenceSystem` reference.
- `TreePlanter.PlantTree` is the reference pattern for tap-triggered,
  criteria-gated, position-based actions with rejection feedback: it
  checks `LandManager.Instance.IsWithinOwnedLand` (fail →
  `HudUI.Instance?.ShowLandLockedPopup()`),
  `RiverManager.Instance.IsBlocked`/`IsWithinSandBank`, its own rules, then
  on success calls `AudioManager.Instance?.PlayPlant()`,
  `PlayerLevel.Instance?.AddXp(...)`, `VfxUtility.SpawnBurst(...)`. Each
  rejection logs a reason and plays `AudioManager.Instance?.PlayError()`.
  `TryReleaseSpeciesAt` (this stage) follows the same shape.
- The Demo panel's four "Release X" buttons (stage 2) stay exactly as they
  are — a debug/test convenience, not being removed just because the real
  UI now exists.

## Scope

**In:**
- The icon button cycles category: Plant → Herbivore → Predator → Plant.
- The label pill becomes clickable and cycles species within the active
  category, remembering the last pick per category (so switching category
  and back restores your prior species there).
- `EmergenceSystem` gains its own per-category "current species" state
  (`currentHerbivoreIndex`/`currentPredatorIndex`), mirroring
  `TreePlanter`'s existing pattern.
- `EmergenceSystem.TryReleaseSpeciesAt(SpeciesDefinition, Vector3)` —
  criteria-gated, position-based release with land/river checks and
  tree-planting-equivalent feedback (success and rejection).
- `CameraPanController`'s empty-ground tap branches on the active category:
  Plant → `TreePlanter.PlantTree` (unchanged); Herbivore/Predator →
  `EmergenceSystem.TryReleaseSpeciesAt` with the currently-selected species
  for that category.
- Three new AAA-rendered category icons (plant/herbivore/predator),
  replacing the single pine-tree glyph, swapped based on active category.
- Renaming the HUD's `vegetation-*` identifiers (UXML element/class names,
  USS selectors, `HudUI.cs` field/method names) to generic `picker-*`
  names, since the button/label are no longer vegetation-specific — this
  is directly in the code being changed, not unrelated cleanup.

**Out:**
- The species summary panel — stage 4.
- Any further Demo/Codex changes — stage 5. The Demo panel's release
  buttons are untouched by this stage.
- Any change to the ecological release criteria themselves (`CanRelease`'s
  math) — unchanged from stage 2.
- Gem cost or daily cap on release — still explicitly free/uncapped, per
  stage 2's decision, unchanged here.

## Design

### `TryReleaseSpeciesAt` and the position-aware spawn refactor

`SpawnIndividual`/`SpawnCreatureVisual` currently take
`IReadOnlyList<PlantedTree> availableTrees` and internally call
`GetRandomTreePosition` to pick a spot. This stage changes them to take an
explicit `Vector3 position` instead — callers resolve the position
themselves:
- `TryReleaseSpecies` (stage 2, random placement — still used by the Demo
  buttons): resolves `GetRandomTreePosition(PlantedTree.ActiveTrees)`
  itself, then calls the same shared spawn code.
- `SpawnMissingForList` (restoring visuals on load): still calls
  `GetRandomTreePosition(availableTrees)` per individual it restores.
- `TryReleaseSpeciesAt` (new): uses the tapped position directly.

```csharp
public bool TryReleaseSpeciesAt(SpeciesDefinition species, Vector3 position)
```
Checks, in order: `species != null`; `LandManager.Instance.IsWithinOwnedLand(position)`
(fail → log, `AudioManager.Instance?.PlayError()`,
`HudUI.Instance?.ShowLandLockedPopup()`); `RiverManager.Instance.IsBlocked(position)`
and `RiverManager.Instance.IsWithinSandBank(position)` (fail → log,
play error); `CanRelease(species)` (fail → log, play error). On success:
spawn at `position`, `AudioManager.Instance?.PlayPlant()`,
`PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased)`. Explicitly
skips the Lab-exclusion-zone and tree-spacing checks `PlantTree` has —
neither applies to animals.

### Per-category species selection on `EmergenceSystem`

```csharp
public SpeciesDefinition CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory category)
public void CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory category)
```
Backed by two new `[SerializeField] private int` fields
(`currentHerbivoreIndex`, `currentPredatorIndex`), indexing into
`unlockedNonPredatorSpecies`/`unlockedPredatorSpecies` respectively (any
non-`Predator` category maps to the non-predator list, matching
`AddUnlockedSpecies`'s existing category split — there's no `Insect`
species in the current roster, so this is exactly equivalent to
"herbivore" in practice today). `CycleReleasableSpecies` wraps with `%`,
same as `TreePlanter.CycleVegetation`.

### HUD picker (renamed from `vegetation-*` to `picker-*`)

- `HudUI.cs` gains a public nested `enum PickerCategory { Plant, Herbivore, Predator }`
  and a `currentPickerCategory` field (default `Plant`).
- Icon button click → advances `currentPickerCategory` (wrapping), then
  refreshes both the icon (swapping in the right one of the three new
  sprites) and the label.
- Label click (new — the label becomes a `Button`, not a `Label`) → calls
  `treePlanter.CycleVegetation()` (Plant) or
  `emergenceSystem.CycleReleasableSpecies(...)` (Herbivore/Predator) based
  on the active category, then refreshes the label text.
- Label text always reflects the current category's current selection:
  `CurrentVegetation.displayName` / `CurrentReleasableSpecies(...).displayName`.

### World-tap routing

`CameraPanController` gains an `[SerializeField] private EmergenceSystem emergenceSystem;`
reference. Its empty-ground tap branch becomes:
```csharp
if (HudUI.Instance's category == Plant)
    treePlanter?.PlantTree(worldPoint);
else
    // resolve the currently-selected species for the active category via
    // emergenceSystem.CurrentReleasableSpecies(...), then:
    emergenceSystem?.TryReleaseSpeciesAt(species, worldPoint);
```
using `HudUI.Instance` (already the established cross-system access
pattern in this codebase) rather than adding a new `HudUI` field.

## Testing

- `EmergenceSystemTests.cs`: `CurrentReleasableSpecies`/`CycleReleasableSpecies`
  cycle correctly and wrap for both categories; `TryReleaseSpeciesAt`
  succeeds at a valid position and increments population; fails (and
  leaves population unchanged) when `CanRelease`'s criteria aren't met.
  Confirmed by checking `TreePlanterTests.cs`: it never references
  `LandManager`/`RiverManager` at all, because `PlantTree`'s
  `if (LandManager.Instance != null && ...)` guards make those checks a
  silent no-op whenever no such singleton exists in the test's bare
  `GameObject` setup (EditMode tests don't load the full scene, so
  `Instance` stays null) — the existing suite simply doesn't exercise
  land/river rejection, and `TryReleaseSpeciesAt`'s tests won't either, by
  the same mechanism. Land/river rejection for release is exercised only
  by the manual Play-mode verification below, same as it already is for
  tree planting today.
- Manual verification: in the Editor, cycle through all three categories
  and confirm the icon and label both update correctly and remember
  per-category selection; tap the world with each category active and
  confirm a tree plants / the right animal appears at the tapped spot;
  tap outside owned land and in the river with an animal category active
  and confirm the same rejection feedback tree-planting gives.
