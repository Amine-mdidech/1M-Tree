# Manual, Criteria-Gated Animal Release — Design

*Stage 2 of the animal-release refactor (stage 1: species roster cleanup +
tabbed Laboratory, done — see
`docs/superpowers/specs/2026-08-14-species-roster-cleanup-design.md`).
This stage replaces automatic emergence with a manual release mechanic.
Later stages (the HUD category/species picker, tap-to-place placement, the
species summary panel, further Demo/Codex updates) are separate specs.*

## Purpose

Animals currently "emerge" on their own, on a timer, via a probability
roll. The player has no agency over it beyond having unlocked/researched
the species. This stage replaces that with a deterministic, player-
triggered action: given a species is unlocked, releasing it succeeds or
fails based on the same ecological criteria the game already models
(enough prey for a predator, enough trees for a herbivore) — no dice roll,
no waiting on a timer.

## Current state

`EmergenceSystem.cs`:
- `Update()` calls `AdvanceTicks(Time.deltaTime)` every frame; `SaveManager`
  also calls `AdvanceTicks` once on load with real-world elapsed seconds,
  to catch up on emergence that would have happened while the app was
  closed.
- `ConsumeTicks` accumulates a timer and returns how many `tickIntervalSeconds`-
  sized ticks have elapsed; `RunEmergenceTick` runs that many times.
- `RunEmergenceTick` iterates `unlockedNonPredatorSpecies`: for each,
  checks `IsVegetationSatisfied` and a population cap
  (`currentTreeCount / treesPerIndividualCap`), then rolls
  `Random.value < emergenceChance` where `emergenceChance` is derived from
  `baseEmergenceWeight` and tree count. `RunPredatorEmergence` does the
  analogous thing for `unlockedPredatorSpecies`, gated by
  `totalPrey >= preyThreshold` and `currentPredatorPopulation < totalPrey / preyToPredatorRatio`.
- A successful roll calls `SpawnIndividual`, which increments
  `EcosystemState`'s population count for that species and spawns a
  `CreatureVisual` at `GetRandomTreePosition` (a random existing tree's
  position).
- `SpawnMissingCreatureVisuals` (called once after `SaveManager` restores
  trees/populations on load) re-creates the `CreatureVisual` GameObjects a
  fresh scene doesn't have, for whatever population count was saved — this
  is independent of the tick mechanism and stays exactly as-is.
- `DemoUI` has a "Force Emergence Tick" button calling
  `EmergenceSystem.ForceEmergenceTick()` → `RunEmergenceTick()` once.
- `MilestoneManager` depends on `EmergenceSystem.HasAnyPredatorEmerged()`
  and `.UnlockedSpeciesCount` — both read `EcosystemState`/the unlocked-
  species lists, not the tick mechanism, so they're unaffected by removing
  ticking.
- `EmergenceSystemTests.cs` has 5 tests (`AdvanceTicks_*`) exercising the
  tick-timer/`ConsumeTicks` mechanics directly, and 5 tests
  (`SpawnMissingCreatureVisuals_*`) exercising visual restoration — 4 of
  the latter are currently failing, a pre-existing issue predating this
  plan (confirmed at every review checkpoint in stage 1).

## Scope

**In:**
- A deterministic `CanRelease(SpeciesDefinition)` query and
  `TryReleaseSpecies(SpeciesDefinition)` action on `EmergenceSystem`,
  reusing the exact criteria math the tick used, minus the probability
  roll.
- Removing the tick mechanism entirely: `Update()`'s automatic firing,
  `ConsumeTicks`, `AdvanceTicks`, `tickIntervalSeconds`,
  `EmergenceTickCount`, and `SaveManager`'s offline catch-up call.
- A minimal test trigger: `DemoUI`'s "Force Emergence Tick" button becomes
  four "Release [Species]" buttons (Gazelle/Squirrel/Wolf/Fox), each
  calling `TryReleaseSpecies` directly.
- Release feedback (sound/VFX/XP) matching tree-planting's existing
  pattern.
- Test suite updates: remove the 5 now-obsolete tick tests, add tests for
  the new release API's success/failure paths per criteria.
- Opportunistic investigation (not a commitment) of the 4 pre-existing
  `SpawnMissingCreatureVisuals` failures, since this stage works directly
  in the surrounding code — fixed if it's a quick, clearly-related fix;
  left alone and still tracked as pre-existing/unrelated otherwise.

**Out (later stages):**
- The HUD category-toggle/species-toggle picker UI — stage 3.
- Tap-to-place-in-the-world placement for released animals — stage 3.
  This stage keeps `GetRandomTreePosition` (a random existing tree's spot)
  as the placement, exactly as automatic emergence already did.
- The species summary panel — stage 4.
- Any further Demo/Codex changes beyond the one button swap above — stage 5.
- Gem cost or a daily cap on releasing — explicitly decided against; the
  ecological criteria are themselves the gate.

## Design

### Release criteria (reused, not reinvented)

- **Herbivore** (`category != Predator`): `IsVegetationSatisfied(species)`
  (unchanged helper) AND, if `treesPerIndividualCap > 0`,
  `EcosystemState.GetSpeciesPopulation(species.id) < currentTreeCount / treesPerIndividualCap`.
  A `treesPerIndividualCap` of 0 means uncapped (always satisfied on this
  axis), matching today's behavior.
- **Predator**: `totalPrey = sum of EcosystemState.GetSpeciesPopulation(preyId) for preySpeciesIds`;
  `totalPrey >= preyThreshold` AND
  `currentPredatorPopulation < floor(totalPrey / preyToPredatorRatio)`.
- `baseEmergenceWeight` is not read by the new logic (it only ever fed the
  probability roll) — left as an unused field on `SpeciesDefinition` and
  the existing assets, not cleaned up, consistent with how stage 1 left
  other now-orphaned fields/methods (`PixelArtUtility.CreateBirdSprite`)
  in place rather than churning unrelated cleanup into a feature commit.

### API

```csharp
public bool CanRelease(SpeciesDefinition species)
public bool TryReleaseSpecies(SpeciesDefinition species)
```

`TryReleaseSpecies` calls `CanRelease` first; if false, returns false and
does nothing (no partial state change). If true, it does exactly what
`SpawnIndividual` already does today (increment `EcosystemState`
population, spawn the `CreatureVisual`, log), plus release feedback:
`AudioManager.Instance?.PlayPlant()` and `VfxUtility.SpawnBurst` (same
calls `TreePlanter.PlantTree` already makes for a successful plant) and an
XP grant via `PlayerLevel.Instance?.AddXp(...)` — a new
`PlayerLevel.XpPerAnimalReleased` constant, matching the existing
`XpPerTreePlanted`/`XpPerUnlock` pattern rather than reusing either of
those for a conceptually different action.

### Removed

- `EmergenceSystem.Update()`'s call to `AdvanceTicks` (the method and the
  whole tick-timer field set — `tickTimer`, `tickIntervalSeconds`,
  `ConsumeTicks`, `AdvanceTicks`, `EmergenceTickCount`, the
  `[ContextMenu("Debug: Force Emergence Tick")]` method).
- `SaveManager`'s call into `AdvanceTicks` for offline catch-up:
  `Assets/Scripts/Save/SaveManager.cs:156`,
  `emergenceSystem.AdvanceTicks((float)offlineSeconds);` — delete this
  line. The following line, `emergenceSystem.SpawnMissingCreatureVisuals();`
  (line 157), stays exactly as-is.
- `RunEmergenceTick`/`RunPredatorEmergence`'s probability-roll bodies are
  replaced by `CanRelease`'s deterministic checks; the loops themselves
  (iterating `unlockedNonPredatorSpecies`/`unlockedPredatorSpecies`) go
  away since there's no more per-tick sweep — release now targets one
  named species at a time, triggered by the player.

### Kept unchanged

- `SpawnMissingCreatureVisuals` and everything it depends on
  (`SpawnCreatureVisual`, `GetCreatureFrames`, `GetQuadrupedTraits`, etc.)
  — still called once after `SaveManager` restores state on load,
  independent of how a population number got there.
- `AddUnlockedSpecies`, `IsSpeciesUnlocked`, `GetUnlockedSpeciesIds`,
  `HasAnyPredatorEmerged`, `UnlockedSpeciesCount` — untouched, still drive
  research-unlock bookkeeping and milestone checks.
- `GetRandomTreePosition` — reused as-is for release placement in this
  stage.

## Testing

- Remove `AdvanceTicks_*` (5 tests) — the mechanism they test no longer
  exists.
- Add tests for `CanRelease`/`TryReleaseSpecies` covering: herbivore
  blocked by population cap, herbivore blocked by missing required
  vegetation, herbivore succeeds and increments population; predator
  blocked by prey threshold, predator blocked by predator/prey ratio,
  predator succeeds and increments population; `TryReleaseSpecies` on an
  unmet criteria returns false and does not change population.
- Manual verification: in the Editor, use the new Demo panel buttons to
  release each of the four species under conditions that should succeed
  and conditions that should fail (e.g. release Wolf before any Gazelle
  exists — should fail; plant enough trees and release Gazelle — should
  succeed), confirming console feedback and population counts match
  expectations. Confirm `MilestoneManager`'s `FirstPredatorEmerged`
  milestone still fires correctly off a manual predator release.
