# Main Menu — Design

## Purpose

Today the game has no menu at all: launching it drops the player straight
into `SampleScene`, `SaveManager.Start()` silently loads `save.json` if one
exists, and that's it — no title, no way to see progress before committing
to a session, no deliberate "start a new game" action (deleting the save
file is currently a debug-only `DemoUI`/F1 button). This adds a real main
menu: a separate title screen shown before gameplay, offering
Continue/Play, New Game (with a confirmation, since it destroys the save),
a glanceable "trees planted" stat, a list of upcoming milestones, Settings,
and Quit.

## Current state

- Exactly one Scene exists in the project (`Assets/Scenes/SampleScene.unity`),
  and it's the only entry in `ProjectSettings/EditorBuildSettings.asset`.
  There is no bootstrap/loader script and no `DontDestroyOnLoad` call
  anywhere in the codebase — every system lives and dies with that single
  scene load.
- `SaveManager.cs` (`Assets/Scripts/Save/SaveManager.cs`) is the entire
  save system: `Start()` (line 25) calls `LoadGame()` unconditionally;
  `SavePath` (line 21) is `Application.persistentDataPath/save.json`,
  read/written via `JsonUtility` against `PlayerSaveData`. `SaveGame()`
  (line 52) and `LoadGame()` (line 110) are the authoritative
  read/write — this design does not change either. `DebugDeleteSave()`
  (line 233) is the existing pattern for wiping the save file and
  resetting the static registries (`EcosystemState.ResetAll()`,
  `PlantedTree.ActiveTrees.Clear()`, `CreatureVisual.ActiveCreatures.Clear()`)
  before a scene reload — New Game reuses this same reset sequence.
- `PlayerSaveData` (referenced from `SaveManager.cs`) already has a
  `plantedTrees` list (one entry per currently-planted tree) and a
  `completedMilestoneIds` list — both are exactly what the menu's stat
  line and milestones view need, read directly with no new save-schema
  work.
- `MilestoneDefinition` (`Assets/Scripts/Milestones/MilestoneDefinition.cs`)
  has `id`, `requirementType` (`TotalTreesPlanted` / `SpeciesUnlockedCount`
  / `FirstPredatorEmerged`), `requirementValue`, `rewardDailyFundsBonus`,
  `rewardGems` — no display name or description field. 5 assets exist
  under `Assets/Data/Milestones/`. The only existing UI that touches
  milestones is `ProfileUI.cs` (`milestonesValueLabel`, line 148: a bare
  `"{completed} / {total}"` count) — there is no existing view that lists
  individual milestones, so the menu's "Future Milestones" list is new
  surface area, not a duplicate of anything.
- `SettingsUI.cs` (`Assets/Scripts/UI/SettingsUI.cs`) is a self-contained
  `UIDocument` panel (music/SFX volume + mute) with one `[SerializeField]`
  dependency, `screenNavigator`, used only by its close button
  (`OnCloseButtonClicked`, line 74): `screenNavigator?.GoHome()` if
  assigned, else `SetOpen(false)`. Leaving `screenNavigator` unassigned in
  a scene with no `ScreenNavigator` (i.e. the new menu scene) already
  falls back correctly with no code change.
- `AudioManager.cs` (`Assets/Scripts/Audio/AudioManager.cs`) is a
  self-contained singleton (`Instance`, set in `Awake()`, line 76):
  procedurally-generated SFX clips, an optional Inspector-assigned music
  playlist, volume/mute persisted via `PlayerPrefs` (not per-scene state).
  Safe to place a second instance of it in the new scene with the same
  Inspector wiring as `SampleScene`'s — no shared state to reconcile.
- `PanelAnimator` and the `.demo-button`-style USS conventions
  (`Assets/UI/Theme/Theme.uss`, `--space-*`/`--radius-*`/`--color-*`
  tokens) are the established look for every panel in the project
  (`Store`, `Settings`, `SpeciesSummary`, etc.) — the menu screen follows
  the same conventions rather than introducing a new visual language.

## Scope

**In:**
- A new `MainMenu` Scene, added to Build Settings as index 0 (`SampleScene`
  becomes index 1). This is the scene that plays when the game launches.
- A `MainMenuUI` panel (`UIDocument` + script, `Assets/UI/MainMenu/`,
  `Assets/Scripts/UI/MainMenuUI.cs`) with:
  - **Continue/Play** button — label depends on whether `save.json`
    exists. Loads `SampleScene`.
  - **New Game** button — shown only when `save.json` exists. Opens an
    in-panel confirmation ("This will erase your current progress — are
    you sure?"); on confirm, deletes `save.json` and loads `SampleScene`
    (which then boots with no save file, i.e. today's existing fresh-start
    behavior, unchanged).
  - **Trees Planted** stat — `data.plantedTrees.Count` from the parsed
    save file, or `0` if none exists.
  - **Future Milestones** list — the `MilestoneDefinition` assets whose
    `id` is not in `data.completedMilestoneIds`, each row showing a
    programmatically-generated label, switched on `requirementType`:
    `TotalTreesPlanted` → `"Plant {requirementValue} trees"`,
    `SpeciesUnlockedCount` → `"Unlock {requirementValue} species"`,
    `FirstPredatorEmerged` → `"Release your first predator"`
    (`requirementValue` ignored for this one) — plus its reward
    (`+N daily funds`, `+N gems`, omitting either half that's `0`).
  - **Settings** button — opens a `SettingsUI` instance placed in this
    scene, reusing the existing panel/asset as-is.
  - **Quit** button — `Application.Quit()`.
- A small `MainMenuSaveReader` helper (or a static method on
  `MainMenuUI`) that does the read-only `File.Exists` +
  `JsonUtility.FromJson<PlayerSaveData>` parse used by the three items
  above. This is a second reader of the same `PlayerSaveData` shape
  `SaveManager` already writes — not a second writer, and not a
  parallel save system.
- A second `AudioManager` GameObject in the `MainMenu` scene, configured
  with the same music/SFX Inspector references as `SampleScene`'s, so
  Settings and button taps work identically to every other panel.

**Out:**
- No persistent (`DontDestroyOnLoad`) bootstrap and no audio continuity
  across the Play transition — confirmed with the user: each scene starts
  its own music independently, and a hard cut/restart when Play is
  clicked is acceptable.
- No changes to `SaveManager`, `PlayerSaveData`, `MilestoneDefinition`, or
  any gameplay system. The menu only *reads* `save.json`; the *authoritative*
  load/save path stays exactly as it is today, entirely inside
  `SampleScene`.
- No new fields on `MilestoneDefinition` (no display name/description) —
  milestone list rows are generated from existing `requirementType`/
  `requirementValue` data.
- No loading-screen/transition-animation work — `SceneManager.LoadScene`
  is a synchronous, unadorned scene swap. This project is small enough
  that load time isn't a concern; can be revisited later if it becomes
  one.
- No Credits/About screen (explicitly deferred by the user in favor of
  the stats/milestones content above).

## Data flow

```
MainMenu scene loads
  -> MainMenuUI.OnEnable() reads save.json (if present) into a
     PlayerSaveData, independent of SampleScene's SaveManager
  -> Continue/Play label, Trees Planted stat, and Future Milestones
     list are all derived from that one parsed struct (or its absence)

Continue/Play clicked
  -> SceneManager.LoadScene("SampleScene")
  -> SampleScene's existing SaveManager.Start() -> LoadGame() runs
     exactly as it does today (unaware the menu ever read the file)

New Game clicked
  -> confirmation shown
  -> on confirm: File.Delete(save.json) if it exists
  -> SceneManager.LoadScene("SampleScene")
  -> SaveManager.Start() -> LoadGame() finds no file, starts fresh
     (identical to today's "no save file" path)
```

No gameplay manager (`EconomyManager`, `TreePlanter`, `EmergenceSystem`,
etc.) is instantiated in `MainMenu` — the scene only needs `MainMenuUI`,
its `UIDocument`, a `SettingsUI` instance, and an `AudioManager`.

## Risks

- **Save-shape drift**: `MainMenuUI`'s reader and `SaveManager`'s
  `LoadGame()` both deserialize `PlayerSaveData`, so a future field
  rename/removal on that type needs updating in two places instead of
  one. Mitigated by keeping the menu's reader to a thin, obvious
  `JsonUtility.FromJson<PlayerSaveData>` call rather than hand-rolled
  parsing — any schema change breaks it the same way it'd break
  `SaveManager`, at compile time.
- **Corrupt/partial save file**: if `save.json` exists but fails to
  parse, `MainMenuUI` should treat it the same way `SaveManager.LoadGame()`
  implicitly would (a `JsonUtility` parse failure surfaces as an
  exception/null read) — wrap the menu's read in the same
  "file exists but unreadable = treat as fresh" fallback so a bad file
  doesn't crash the menu, only silently offers "Play" instead of
  "Continue". Worth one explicit test case.
