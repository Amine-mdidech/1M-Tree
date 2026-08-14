# Species Roster Cleanup & Tabbed Laboratory — Design

*Stage 1 of a larger refactor toward manual, criteria-gated animal release
(replacing automatic emergence). This stage is data/roster cleanup and the
Laboratory UI restructuring it motivates; later stages (release mechanic, HUD
picker redesign, species summary panel, Demo/Codex updates) are separate specs.*

## Purpose

Cut the species roster down to a small, deliberate set — one free starter and
one researchable species per category — and give the Laboratory a real
Plants/Herbivores/Predators tab structure to present that roster in, instead
of one flat scrollable tech tree. This is the foundation later stages (the
release mechanic, the HUD category/species picker) build on: they need a
final, stable roster and a category-first UI to hook into.

## Current state

- `SpeciesDefinition` (`Assets/Scripts/Species/SpeciesDefinition.cs`) already
  has `enum SpeciesCategory { Insect, Herbivore, Predator }`, plus
  `unlockedByDefault`, `preySpeciesIds`/`preyThreshold`/`preyToPredatorRatio`,
  `requiredVegetationIds`, `researchDurationSeconds`,
  `documentaryTitle`/`documentaryText`. No structural changes needed to this
  type for this stage.
- 16 species assets exist in `Assets/Data/Species/`: Antelope, Boar, Buffalo,
  Deer, Eagle, Elephant, Fox, Gazelle, Giraffe, Goat, Hare, Lion, Moose,
  Rabbit, Wolf, Zebra. No Squirrel asset exists yet.
- 11 vegetation assets exist: Bamboo, BerryBush, Birch, Fern, Mangrove, Maple,
  Oak, Pine, Reed, Wildflower, Willow, each with a matching
  `Assets/Data/Research/*Research.asset` for the non-default ones.
- Wolf is `unlockedByDefault`, no research asset, `preySpeciesIds: [deer]`
  (stale — changed from `gazelle` earlier this session, before this roster
  was decided).
- Fox requires research (`FoxResearch.asset`, prerequisite `rabbit_research`),
  `preySpeciesIds: [rabbit, hare]`.
- Gazelle is `unlockedByDefault`, no research asset.
- Pine requires research (`PineResearch.asset`, prerequisite
  `berry_bush_research`).
- `LaboratoryUI.cs` renders one flat scrollable tech-tree canvas; nodes are
  already color-coded by category via `GetBranchRank()`, but there's no tab
  or section boundary — vegetation, herbivore, and predator nodes all share
  one canvas and one set of tier columns.
- `EmergenceSystem.cs` has three separate species-name switch statements
  (~line 382, 456, 525) driving procedural sprite generation
  (`CreateQuadrupedSprite`/`CreateBirdSprite`), with cases for every current
  species. No `"squirrel"` case exists; an unhandled name falls through to
  each switch's `default:` case (a generic quadruped shape).
- `TreePlanter` and `EmergenceSystem` each hold scene-serialized lists
  (`plantableVegetation`/`unlockedVegetation`,
  `unlockedNonPredatorSpecies`/`unlockedPredatorSpecies`) wired in the
  Inspector on `SampleScene.unity` — these are the actual source of "what
  exists in this game," independent of which asset files are on disk.
- `CodexUI.cs` builds its pages dynamically per category
  (`AddSpeciesPages(Herbivore)`, `AddSpeciesPages(Predator)`, then
  vegetation) from these same lists — expected to need no separate changes
  once the lists above are updated, but this will be confirmed during
  implementation, not assumed.

## Scope

**In:**
- Final roster: Oak (free) + Pine (researchable) vegetation; Gazelle (free) +
  Squirrel (researchable, new) herbivores; Wolf (free) + Fox (researchable)
  predators.
- Deleting the 13 other animal species and 9 other vegetation species, their
  research assets, and repairing what breaks (Wolf/Fox prey references,
  Pine/Fox prerequisite chains).
- Creating the new Squirrel species (data asset + a dedicated procedural
  sprite case, not a generic fallback).
- Restructuring the Laboratory panel into three tabs (Plants / Herbivores /
  Predators), each showing only that category's nodes.
- Updating the scene-wired lists on `TreePlanter`/`EmergenceSystem` to match.

**Out (later stages):**
- Anything about *how* animals get released into the world (automatic
  emergence stays exactly as-is mechanically in this stage — same tick-based
  system, just running over a much smaller roster). Replacing it with a
  manual criteria-gated release action is stage 2.
- The HUD picker (category-toggle button + family-cycle label) — stage 3.
  Vegetation planting keeps working exactly as it does today in this stage.
- The new species summary panel — stage 4.
- Demo panel button changes — stage 5 (its buttons already operate generically
  over whatever roster exists, so they should keep working unchanged through
  this stage, but will need a look once the release mechanic replaces
  emergence in stage 2).

## Design

### Roster and data fixes

| Category | Free starter | Researchable |
|---|---|---|
| Plants | Oak | Pine |
| Herbivores | Gazelle | Squirrel (new) |
| Predators | Wolf | Fox |

- Delete species assets: Antelope, Boar, Buffalo, Deer, Eagle, Elephant,
  Giraffe, Goat, Hare, Lion, Moose, Rabbit, Zebra (and their `.meta` files,
  via `manage_asset delete` so GUIDs/references are cleaned up properly, not
  raw filesystem deletion).
- Delete vegetation assets: Bamboo, BerryBush, Birch, Fern, Mangrove, Maple,
  Reed, Wildflower, Willow.
- Delete the now-orphaned research assets for everything above.
- Fix `Wolf.asset`: `preySpeciesIds` → `[gazelle]` (reverting the stale
  `deer` change).
- Fix `Fox.asset`: `preySpeciesIds` → `[squirrel]`.
- Fix `PineResearch.asset` and `FoxResearch.asset`: remove their
  prerequisite (`berry_bush_research`/`rabbit_research`) — both become
  researchable with no prerequisite once the Laboratory has the relevant tab
  unlocked (matches the "no prerequisites, simplest" decision — with only one
  researchable item per category left, a prerequisite chain has nothing left
  to chain from).
- Create `Squirrel.asset`: category Herbivore, `unlockedByDefault: false`,
  `preySpeciesIds: []`, `requiredVegetationIds: []`. Stats
  (`baseEmergenceWeight`, `treesPerIndividualCap`) set close to Gazelle's
  existing values (same emergence system, same category) rather than
  invented from scratch.
- Create `SquirrelResearch.asset`: no prerequisite, duration/cost in the same
  range as `PineResearch.asset` (~180s) since it's the equivalent
  "researchable tier-1" item for its category.

### Squirrel visual

Add a dedicated `"squirrel"` case to each of the three switch statements in
`EmergenceSystem.cs`, following the same per-species-case pattern the
existing entries already use (no restructuring of the switches themselves) —
small body, bushy tail silhouette, brown/tan fur — distinguishing it from the
generic default quadruped shape, matching the quality bar the other kept
species (Gazelle, Wolf, Fox) already have.

### Laboratory tabs

- Three tab buttons (Plants / Herbivores / Predators) added above the
  existing tech-tree canvas in `Laboratory.uxml`, styled consistently with
  the game's existing tab/toggle-button patterns (none exist yet in this
  panel specifically, so this introduces the pattern — semantic button
  coloring per `ui-style-guide.md`, active tab visually distinguished from
  inactive ones).
- `LaboratoryUI.cs` filters which nodes get built into the canvas based on
  the selected tab, reusing the existing node-building/branch-color logic
  rather than replacing it — the canvas rendering code doesn't need to
  change, just what subset of `ResearchProjectDefinition`s it's given.
- Default tab on open: Plants (matches reading order top-to-bottom of the
  category enum, and plants are the game's original/primary mechanic).
- Each tab now renders a very small tree (1 owned starter node + 1
  researchable node) — no pagination/scrolling concerns at this roster size,
  but the canvas sizing logic doesn't need to assume a fixed count, so larger
  future rosters per category still work.

## Testing

- Extend or add EditMode tests confirming: `TreePlanter`'s unlocked/plantable
  vegetation lists only contain Oak/Pine after the scene update; the
  emergence species lists only contain Gazelle/Squirrel/Wolf/Fox; Wolf's
  prey resolves to Gazelle and Fox's to Squirrel (data assertions, not
  behavior the tests need to simulate).
- Manual verification in the Editor: open the Laboratory, confirm three tabs
  each show the right starter (already-owned) and researchable node, research
  Pine/Squirrel/Fox successfully with no prerequisite blocking them, and
  confirm Codex only lists the surviving six species with no broken
  references (missing sprite, null species, etc.) left over from the deleted
  ones.
