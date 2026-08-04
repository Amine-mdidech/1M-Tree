# Technical Specs — Data Structures & Formulas

*Companion to game-design-document.md. Reflects the current implemented build (Unity `ScriptableObject` data assets under `Assets/Data/`).*

---

## 1. Species data

Each species is a `SpeciesDefinition` ScriptableObject (`Assets/Scripts/Species/SpeciesDefinition.cs`):

| Field | Type | Notes |
|---|---|---|
| `id` | string | unique key, e.g. `"gazelle"` |
| `displayName` | string | e.g. "Gazelle" |
| `category` | enum: `Insect`, `Herbivore`, `Predator` | `Insect` is a legacy unused value — insects were removed from the game entirely; kept only so removing it doesn't shift the serialized indices of `Herbivore`/`Predator` on existing assets |
| `unlockedByDefault` | bool | true only for Gazelle and Wolf |
| `requiredVegetationIds` | string[] | vegetation that must be present for this species to ever appear; supported by code but currently empty on every species (unused) |
| `baseEmergenceWeight` | float | per-species tunable weight |
| `treesPerIndividualCap` | int | **non-predators** — population ceiling as `currentTreeCount / treesPerIndividualCap`; `0` = no cap (only used by Wolf, which is capped via prey ratio instead) |
| `preySpeciesIds` | string[] | **predators only** |
| `preyThreshold` | int | **predators only** — minimum total prey population before this predator can appear at all |
| `preyToPredatorRatio` | int | **predators only** — how many prey support one predator |
| `researchDurationSeconds` | int | informational; actual research timing lives on the linked `ResearchProjectDefinition` (section 6) |
| `documentaryTitle` / `documentaryText` | string | short real-world fact + conservation note, shown in Codex |

### Starting species (unlocked by default)

| id | displayName | category | baseEmergenceWeight | treesPerIndividualCap |
|---|---|---|---|---|
| `gazelle` | Gazelle | Herbivore | 1.0 | 20 |
| `wolf` | Wolf | Predator | 0.6 | — (see prey ratio below) |

### Researchable herbivores (11)

| id | displayName | baseEmergenceWeight | treesPerIndividualCap | research duration |
|---|---|---|---|---|
| `rabbit` | Rabbit | 1.3 | 12 | 180s |
| `hare` | Hare | 1.2 | 14 | 180s |
| `goat` | Goat | 1.1 | 16 | 240s |
| `antelope` | Antelope | 1.0 | 20 | 300s |
| `deer` | Deer | 1.0 | 20 | 300s |
| `zebra` | Zebra | 0.9 | 24 | 360s |
| `boar` | Boar | 0.8 | 28 | 360s |
| `buffalo` | Buffalo | 0.7 | 32 | 420s |
| `moose` | Moose | 0.7 | 32 | 420s |
| `giraffe` | Giraffe | 0.6 | 40 | 480s |
| `elephant` | Elephant | 0.4 | 60 | 600s |

Cap is roughly inverse to rarity/size: common small grazers (Rabbit) support dense populations, rare large ones (Elephant) need much more room per individual.

### Researchable predators (3)

| id | displayName | baseEmergenceWeight | preySpeciesIds | preyThreshold | preyToPredatorRatio | research duration |
|---|---|---|---|---|---|---|
| `fox` | Fox | 0.7 | rabbit, hare | 10 | 4 | 360s |
| `eagle` | Eagle | 0.6 | rabbit, hare, goat | 12 | 5 | 480s |
| `lion` | Lion | 0.5 | gazelle, zebra, antelope | 20 | 6 | 600s |

Starting predator:

| id | preySpeciesIds | preyThreshold | preyToPredatorRatio |
|---|---|---|---|
| `wolf` | gazelle | 15 | 8 |

All research projects use `gemSkipCostPerRemainingMinute: 2`.

---

## 2. Vegetation (tree) data

`VegetationDefinition` ScriptableObject (`Assets/Scripts/Vegetation/VegetationDefinition.cs`):

| Field | Type | Notes |
|---|---|---|
| `id` | string | e.g. `"oak"` |
| `displayName` | string | |
| `unlockedByDefault` | bool | true only for Oak |
| `growthStages` | array of `{stageName, durationSeconds}` | always Seedling → Sapling → Mature |
| `biomeId` | string | `"temperate_forest"` (single biome) |

### All 11 vegetation types

| id | displayName | unlocked by default | Seedling | Sapling | Mature | research duration |
|---|---|---|---|---|---|---|
| `oak` | Oak | **yes** | 60s | 300s | 900s | — |
| `wildflower` | Wildflower | no | — | — | — | 120s |
| `fern` | Fern | no | — | — | — | 150s |
| `bamboo` | Bamboo | no | 20s | 90s | 300s | 120s |
| `birch` | Birch | no | — | — | — | 180s |
| `reed` | Reed | no | — | — | — | 150s |
| `berry_bush` | Berry Bush | no | — | — | — | 180s |
| `willow` | Willow | no | — | — | — | 180s |
| `mangrove` | Mangrove | no | — | — | — | 300s |
| `pine` | Pine | no | 45s | 240s | 720s | 180s |
| `maple` | Maple | no | 70s | 320s | 850s | 240s |

(Growth-stage seconds not listed above follow the same three-stage pattern with per-species tuning; see the individual `.asset` files under `Assets/Data/Vegetation/` for exact values.)

Offline progress: on app resume, each planted tree's timer advances by elapsed real-world seconds since last close, capped at 24h.

---

## 3. Emergence formulas

Runs on a fixed tick (`tickIntervalSeconds`, default 60s, configurable on `EmergenceSystem`).

**Non-predator species (Herbivore):**
```
vegetationDiversityMultiplier = 1 + (distinctVegetationTypesPlanted - 1) * 0.1
emergenceChance = baseEmergenceWeight * (currentTreeCount / 50) * vegetationDiversityMultiplier
emergenceChance = clamp(emergenceChance, 0, 0.8)   // per-tick ceiling

// population cap, checked before rolling:
if treesPerIndividualCap > 0:
    populationCapacity = currentTreeCount / treesPerIndividualCap
    skip this species this tick if currentPopulation >= populationCapacity
```
Roll once per tick per unlocked, vegetation-satisfied, under-cap species; success spawns one individual.

**Predators:**
```
totalPrey = sum of current population for all species in preySpeciesIds
predatorCapacity = floor(totalPrey / preyToPredatorRatio)

if totalPrey >= preyThreshold AND currentPredatorPopulation < predatorCapacity:
    emergenceChance = baseEmergenceWeight * min(totalPrey / preyThreshold, 2.0)
    emergenceChance = clamp(emergenceChance, 0, 0.5)
else:
    emergenceChance = 0
```
Presence-gating only — predators don't hunt/reduce prey population mechanically (see section 9 for the cosmetic hunting behavior, which is unrelated to this formula).

---

## 4. Daily grant / tree cap

```
dailyTreeCap = 30 + sum(unlocked milestone cap bonuses)
treesPlantedToday resets to 0 at local midnight
player cannot plant if treesPlantedToday >= dailyTreeCap
```

---

## 5. Milestones

| id | requirementType | requirementValue | rewardDailyCapBonus | rewardGems |
|---|---|---|---|---|
| `first_10_trees` | TotalTreesPlanted | 10 | 5 | 0 |
| `first_50_trees` | TotalTreesPlanted | 50 | 10 | 10 |
| `first_predator` | FirstPredatorEmerged | 1 | 10 | 20 |
| `all_starters_present` | SpeciesUnlockedCount | 2 | 5 | 5 |
| `first_200_trees` | TotalTreesPlanted | 200 | 15 | 25 |

(`all_starters_present` requires 2, not 3 — the original 3rd starter, Bee, was removed along with all insect content.)

---

## 6. Laboratory / research — branching tech tree

`ResearchProjectDefinition` ScriptableObject (`Assets/Scripts/Research/ResearchProjectDefinition.cs`):

| Field | Type | Notes |
|---|---|---|
| `id` | string | |
| `unlocksSpeciesId` | string | set for species-unlock projects |
| `unlocksVegetationId` | string | set for vegetation-unlock projects |
| `prerequisiteProjectIds` | string[] | other research project ids that must be completed first |
| `baseDurationSeconds` | int | |
| `gemSkipCostPerRemainingMinute` | int | `2` for every project |

24 total research projects (10 vegetation + 14 species — Oak, Gazelle, and Wolf need no research, they start unlocked). `ResearchManager.CanStartResearch` requires: not already unlocked, nothing else currently researching, and every prerequisite already unlocked.

### Dependency graph (tier = 1 + max(prerequisite tier), 0 if no prerequisites)

- **Tier 0** (no prerequisites): `wildflower`, `fern`, `bamboo`, `rabbit`, `hare`, `goat`
- **Tier 1**: `birch`←wildflower, `reed`←fern, `berry_bush`←bamboo, `deer`←fern, `zebra`←wildflower, `antelope`←wildflower, `fox`←rabbit, `eagle`←rabbit
- **Tier 2**: `willow`←birch, `mangrove`←reed, `pine`←berry_bush, `boar`←birch, `buffalo`←reed, `lion`←zebra, `elephant`←bamboo+berry_bush
- **Tier 3**: `moose`←willow, `maple`←willow+pine
- **Tier 4**: `giraffe`←maple

The Laboratory UI lays these out as a scrollable node graph (columns = tiers, rows grouped by branch: vegetation / herbivore / predator), with connector lines between a project and its prerequisites. Locked nodes show "🔒 ???"; nodes whose prerequisites are met but something else is researching show dimmed with their real name; the one active project is highlighted; completed ones show a checkmark.

The Laboratory itself is a clickable building placed in the world (not just a menu) — trees cannot be planted within a small radius around it.

---

## 7. Biodiversity / ecosystem score

Not currently implemented as a displayed number. If added, the original formula still applies conceptually:
```
ecosystemScore = (herbivoreCount present * 1) + (predatorCount present * 3)
```
(No insect term — insects were removed. Predators weighted higher since they're harder to achieve.)

---

## 8. Player save state (shape)

```json
{
  "treesPlantedToday": 0,
  "dailyTreeCap": 30,
  "lastGrantResetTimestamp": "2026-08-01T00:00:00Z",
  "lastSessionTimestamp": "2026-08-01T09:14:00Z",
  "gems": 0,
  "plantedTrees": [ { "vegetationId": "oak", "plantedAt": "2026-08-01T08:00:00Z", "stage": "Sapling" } ],
  "speciesPopulation": { "gazelle": 4, "wolf": 0 },
  "unlockedSpeciesIds": ["gazelle", "wolf"],
  "unlockedVegetationIds": ["oak"],
  "completedMilestoneIds": [],
  "activeResearch": null
}
```
`unlockedVegetationIds` was added alongside the vegetation-research system — it's the vegetation-side equivalent of `unlockedSpeciesIds`.

---

## 9. Map, camera, and world interaction

- The playable map is a fixed-size world (`mapHalfWidth`/`mapHalfHeight` on `CameraPanController`, currently 30×30 half-extents = 60×60 total) larger than the screen. Drag to pan the camera, clamped so the viewport never scrolls past the map edge.
- A press that moves less than ~20 screen pixels before release is treated as a tap; anything past that threshold is a pan (no plant/interact action fires).
- Tap resolution order: laboratory building (if tapped, opens the tech tree) → an existing tree within a small radius (if immature, opens the growth-boost popup; if mature, tap is absorbed and nothing happens) → otherwise, plant the currently-selected vegetation type at that point.

**Tree growth boost**: any planted, immature tree can be tapped to open a popup showing remaining growth time and a cost of `1 gem × ceil(remainingSeconds / 60)` (minimum 1 gem) to finish it instantly.

**Predator hunting (cosmetic)**: each time a creature's wander AI picks a new target (every 2–5s), if it's a predator with a valid prey species within a 3-unit radius, there's a 10% chance it moves toward that prey instead of wandering randomly. Purely visual — does not affect emergence formulas or population counts.

---

## 10. Platform

- Landscape orientation only (Player Settings: `defaultInterfaceOrientation = LandscapeLeft`, portrait and upside-down portrait disabled, both landscape directions allowed).
- All UI (HUD, Codex, Laboratory, tree-boost popup, Profile, Demo tools) built with UI Toolkit, reference resolution 1920×1080.
- All visuals (trees, creatures, buildings, UI icons) are generated procedurally at runtime via `PixelArtUtility` — no external art/image assets.

---

## 11. UI design system

`Assets/UI/Theme/Theme.uss` defines every color/spacing/radius/border/font-size value used across every panel as a USS custom property, scoped to a `.theme-scope` class (not `:root` — a plain `:root` selector was tried but a class applied to each panel's own root element is the pattern confirmed to resolve custom properties reliably at runtime in this Unity version). Every panel's UXML imports `Theme.uss` before its own local stylesheet and adds `theme-scope` to its root element's class list. Full token list and component patterns: `docs/ui-style-guide.md`.

Each panel is its own `UIDocument`/`PanelSettings` pair (they don't share a runtime panel), so both the stylesheet import and the `theme-scope` class must be present on every individual panel — custom properties don't cross panel boundaries automatically.

---

## 12. Profile panel

`ProfileUI` (HUD toolbar, person icon). Opens a snapshot stats view, refreshed on open (not continuously):

| Stat | Source |
|---|---|
| Gems | `MilestoneManager.Gems` |
| Trees planted | `EcosystemState.TotalTreeCount` (lifetime, never decremented) |
| Species unlocked | `EmergenceSystem.UnlockedSpeciesCount` / total species list length |
| Plants unlocked | `TreePlanter.GetUnlockedVegetationIds().Count` / total vegetation list length |
| Milestones | `MilestoneManager.CompletedMilestoneIds.Count` / `MilestoneManager.TotalMilestoneCount` |
| Today's planting | `DailyGrant.TreesPlantedToday` / `DailyGrant.DailyTreeCap` |

---

## 13. Demo tools panel

`DemoUI` (HUD top-right, gear icon, separate from the main action toolbar). Each button calls a public method exposed on an existing manager for exactly this purpose (several were made public specifically to support this panel — `MilestoneManager.AddGems`, `EmergenceSystem.ForceEmergenceTick`, `TreePlanter.DebugPlantBatch`, `ResearchManager.DebugInstantComplete`, `SaveManager.DebugDeleteSave`):

| Button | Effect |
|---|---|
| +100 Gems | `MilestoneManager.AddGems(100)` |
| Unlock All Species | loops the full species list, calling `EmergenceSystem.AddUnlockedSpecies` on each not-yet-unlocked one |
| Unlock All Plants | loops the full vegetation list, calling `TreePlanter.AddUnlockedVegetation` on each not-yet-unlocked one |
| Finish Active Research | `ResearchManager.DebugInstantComplete()` (no-ops if nothing is researching) |
| Mature All Trees | finds every `PlantedTree` in the scene and calls `CompleteGrowthInstantly()` on each immature one |
| Force Emergence Tick | `EmergenceSystem.ForceEmergenceTick()` |
| Plant a Batch of Trees | `TreePlanter.DebugPlantBatch()` |
| Delete Save (Reset Game) | `SaveManager.DebugDeleteSave()` — deletes the save file; a fresh game requires actually restarting Play mode / relaunching the app, this button does not reset already-loaded in-memory state |

Not gated behind a build flag — it's reachable in any build right now. Should be locked behind a debug/dev flag (or removed) before a real release build.

---

## 14. Unit tests

Unity Test Framework, EditMode. Game code lives in the `Grove.Runtime` assembly (`Assets/Scripts/Grove.Runtime.asmdef`, covering all of `Assets/Scripts/` except `Tests/`); tests live in `Grove.Tests` (`Assets/Scripts/Tests/Grove.Tests.asmdef`), which references `Grove.Runtime`. This split exists because assembly-definition-based assemblies compile *before* the implicit default assembly, so a test asmdef can't reference plain scripts that don't have their own asmdef — `Grove.Runtime` was added specifically to make the game code referenceable from tests.

36 tests across 7 files, all passing as of this writing:

| File | Covers |
|---|---|
| `PlantedTreeTests` | growth-stage advancement, remaining-time math, instant-complete |
| `DailyGrantTests` | cap enforcement, consume/reset, cap bonuses |
| `MilestoneManagerTests` | gem add/spend, insufficient-funds and negative-amount rejection |
| `TreePlanterTests` | vegetation unlock tracking, cycling, planting + daily cap integration |
| `EmergenceSystemTests` | species unlock tracking, predator-emerged detection |
| `ResearchManagerTests` | prerequisite gating end-to-end (a locked project becomes startable once its prerequisite completes) |
| `CameraPanControllerTests` | map-pan clamping math |

Private fields on `MonoBehaviour`s that don't have public setters (e.g. `ResearchManager.availableResearchProjects`) are set via reflection in test `SetUp` — a pragmatic choice given there's no dependency-injection layer, not a pattern to imitate for new production code.

Not covered: UI Toolkit visual/layout behavior (panel rendering, theme token resolution) and the probabilistic emergence-tick roll itself — both are exercised manually via Play mode instead, as documented throughout this session's manual testing.

Run via Unity's Test Runner window (Window > General > Test Runner), or `run_tests`/`get_test_job` through the MCP bridge with `assembly_names: ["Grove.Tests"]`.

---

## Build order this was implemented in
1. Vegetation planting + growth-stage timer
2. Daily tree cap + reset logic
3. Herbivore emergence formula, wired to tree count
4. Predator emergence formula, wired to prey population
5. Milestone checks + daily cap increases
6. Codex UI
7. Single research project in the lab (later expanded into the full tech tree — section 6)
8. Save/load using the state shape above
9. Content expansion: 16 species, 11 vegetation types
10. Placeholder creature/tree visuals, then a full procedural-art pass
11. Mobile readiness: touch input, then landscape lock
12. Bigger pannable map, world-space Laboratory building, tree growth boost, predator hunting behavior
13. Vegetation research-gating + population caps + full prerequisite tech tree
14. Shared UI design-token system, Profile panel, Demo tools panel
15. Unit test suite (Unity Test Framework, EditMode)
