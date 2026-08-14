# Species Summary Panel Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a new HUD-accessible panel summarizing every species/tree currently present in the world, grouped into three independently expandable accordion sections (Trees/Herbivores/Predators), with live counts and tap-to-view release criteria for animals.

**Architecture:** A new `SpeciesSummaryUI` panel follows the exact `ProfileUI`/`CodexUI` shape (UIDocument + UXML/USS + `PanelAnimator`), opened via a new HUD action-cluster button through `ScreenNavigator`'s existing `OpenX()` pattern. Row-building/filtering/criteria-text logic is extracted as `public static` pure functions for direct unit testing, matching how `TreePlanter`/`EmergenceSystem` are tested. Along the way, a small pre-existing bug in `CreatureVisual.cs` (missing the same `Awake()`-reliability workaround `PlantedTree.cs` already has) is fixed first, since it's what caused 5 EditMode tests to fail at the end of stage 3.

**Tech Stack:** Unity 6000.5.6f1, C#, UI Toolkit (UXML/USS), NUnit EditMode tests.

## Global Constraints

- Presence rule: only species with live population > 0 (`EcosystemState.GetSpeciesPopulation`) or vegetation types with ≥1 currently-planted tree appear as rows — no locked/zero-count entries.
- Three accordion sections (Trees/Herbivores/Predators), each independently tap-to-expand/collapse, all expanded by default when the panel opens.
- Section headers show a live count of **distinct types present** in that category (e.g. "Trees (2)" means 2 vegetation types, not total tree count).
- Tree rows: icon (`heroSprite` or procedural fallback) + name + count. **Not tappable.**
- Herbivore/Predator rows: icon (new `EmergenceSystem.GetSpeciesIconSprite`, static) + name + population count. **Tappable** → popup with human-readable release criteria, computed from each species' own fields (`preyThreshold`/`preyToPredatorRatio`/`treesPerIndividualCap`/`requiredVegetationIds`) — no changes to `CanRelease`'s actual gating logic.
- Live refresh every 0.5s while the panel is open (matching the minimap's existing refresh interval convention), not while closed.
- No changes to the Codex, Demo panel, Laboratory, or HUD picker (stages 1-3) beyond what's explicitly listed above.

---

## File Structure

- **Modify `Assets/Scripts/Ecosystem/CreatureVisual.cs`:** fix `Awake()`/`Initialize()` ordering reliability (mirrors `PlantedTree.EnsureAwakeState`).
- **Modify `Assets/Scripts/Ecosystem/EmergenceSystem.cs`:** add `public static Sprite GetSpeciesIconSprite(SpeciesDefinition species)`.
- **Modify `Assets/Scripts/Tests/EmergenceSystemTests.cs`:** add tests for `GetSpeciesIconSprite`.
- **Modify `Assets/UI/Theme/Theme.uss`:** add `--surface-summary`/`--surface-summary-inner`/`--surface-summary-border` tokens, matching the existing per-panel surface-color convention.
- **Create `Assets/UI/SpeciesSummary/SpeciesSummary.uxml`** and **`Assets/UI/SpeciesSummary/SpeciesSummary.uss`**: the panel's markup/styles.
- **Create `Assets/Scripts/UI/SpeciesSummaryUI.cs`:** the panel's `MonoBehaviour`, including the testable static helpers.
- **Create `Assets/Scripts/Tests/SpeciesSummaryTests.cs`:** unit tests for the static helpers.
- **Modify `Assets/Scripts/UI/ScreenNavigator.cs`:** add `speciesSummaryUI` field, `OpenSpeciesSummary()`, and a `CloseOtherPanels` entry.
- **Modify `Assets/Scripts/UI/HudUI.cs`:** add `speciesSummaryUI` field, wire the new button, click handler.
- **Modify `Assets/UI/Hud/Hud.uxml`:** add `species-summary-button` to `action-cluster`.
- **Modify `Assets/Scripts/Tests/SceneRosterWiringTests.cs`:** add wiring/roster tests for the new panel and its references.
- **Scene wiring (via `execute_code`):** create the `SpeciesSummaryUI` GameObject + `UIDocument` + dedicated `PanelSettings` asset in `Assets/Scenes/SampleScene.unity`, wire its fields, and wire `ScreenNavigator.speciesSummaryUI`/`HudUI.speciesSummaryUI`.

---

### Task 1: Fix `CreatureVisual` Awake-reliability bug

**Files:**
- Modify: `Assets/Scripts/Ecosystem/CreatureVisual.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces: nothing new — this is a correctness fix. `CreatureVisual.ActiveCreatures` now reliably reflects every spawned creature immediately after `EmergenceSystem.SpawnCreatureVisual` calls `AddComponent<CreatureVisual>()` + `Initialize(...)`, in both EditMode tests and Play mode.

This task closes the known gap from stage 3: `PlantedTree.cs:123-151` already documents and works around the fact that `AddComponent` doesn't synchronously invoke `Awake()` outside a running Player/PlayMode loop, via a guarded `EnsureAwakeState()` called from both `Awake()` and `Initialize()`/`RestoreState()`. `CreatureVisual.cs`'s `Initialize()` never got this same fix.

- [ ] **Step 1: Confirm the 5 currently-failing tests, to have a before/after comparison**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `group_names: "EmergenceSystemTests"`, `include_failed_tests: true`.
Expected: the same 5 failures from the end of stage 3 (`SpawnMissingCreatureVisuals_CalledTwice_DoesNotDuplicateVisuals`, `SpawnMissingCreatureVisuals_CoversPredatorsToo`, `SpawnMissingCreatureVisuals_PreviouslyCachedNullFrames_SelfHealsWithValidSprite`, `SpawnMissingCreatureVisuals_SpawnsOneVisualPerRestoredPopulation`, `TryReleaseSpeciesAt_Succeeds_IncrementsPopulationAtGivenPosition`), each with "expected a spawned creature" or a count mismatch.

- [ ] **Step 2: Apply the fix**

In `Assets/Scripts/Ecosystem/CreatureVisual.cs`, add a guard field next to the existing private fields (after `private Vector3 previousPosition;`):

```csharp
    private Vector3 previousPosition;
    private bool awakeStateReady;
```

Replace `Initialize` and `Awake`:

```csharp
    public void Initialize(SpeciesDefinition speciesDefinition, Vector3 home, float radius, Sprite[] frames)
    {
        EnsureAwakeState();

        species = speciesDefinition;
        homePosition = home;
        wanderRadius = radius;
        walkFrames = frames;
        previousPosition = home;
        PickNewTarget();
    }

    private void Awake()
    {
        EnsureAwakeState();
    }

    // Mirrors PlantedTree.EnsureAwakeState: AddComponent doesn't synchronously invoke Awake outside
    // a running Player/PlayMode loop - editor-driven code and EditMode tests can call Initialize
    // before Awake has run at all, leaving spriteRenderer null and this creature missing from
    // ActiveCreatures. Calling this from both paths, guarded by awakeStateReady, makes setup correct
    // regardless of which one runs first.
    private void EnsureAwakeState()
    {
        if (awakeStateReady)
        {
            return;
        }

        awakeStateReady = true;
        spriteRenderer = GetComponent<SpriteRenderer>();
        ActiveCreatures.Add(this);
    }
```

- [ ] **Step 3: Run tests to verify the fix**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `group_names: "EmergenceSystemTests"`, `include_failed_tests: true`.
Expected: all tests pass, including the 5 that were previously failing.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Ecosystem/CreatureVisual.cs
git commit -m "fix: make CreatureVisual.Awake() reliable outside Play mode, matching PlantedTree's existing fix"
```

---

### Task 2: `EmergenceSystem.GetSpeciesIconSprite`

**Files:**
- Modify: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`
- Test: `Assets/Scripts/Tests/EmergenceSystemTests.cs`

**Interfaces:**
- Consumes: the existing private `GetCreatureFrames(SpeciesDefinition)` (unchanged).
- Produces: `public static Sprite GetSpeciesIconSprite(SpeciesDefinition species)` — used by `SpeciesSummaryUI` (Task 4) to render each animal row's icon without needing an `EmergenceSystem` instance reference.

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Scripts/Tests/EmergenceSystemTests.cs`, after the existing `TryReleaseSpeciesAt_NullSpecies_ReturnsFalse` test (or any convenient location before the `CountActiveCreaturesForSpecies` helper):

```csharp
    [Test]
    public void GetSpeciesIconSprite_ReturnsNonNullSprite()
    {
        Sprite icon = EmergenceSystem.GetSpeciesIconSprite(herbivore);

        Assert.IsNotNull(icon);
    }

    [Test]
    public void GetSpeciesIconSprite_NullSpecies_ReturnsNull()
    {
        Assert.IsNull(EmergenceSystem.GetSpeciesIconSprite(null));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `group_names: "EmergenceSystemTests"`.
Expected: FAIL — `GetSpeciesIconSprite` doesn't exist yet (compile error).

- [ ] **Step 3: Implement**

In `Assets/Scripts/Ecosystem/EmergenceSystem.cs`, add this method right after `GetCreatureFrames`:

```csharp
    private static Sprite[] GetCreatureFrames(SpeciesDefinition species)
    {
        if (creatureFramesBySpeciesId.TryGetValue(species.id, out Sprite[] cached) && IsValidFrames(cached))
        {
            return cached;
        }

        Sprite[] frames = BuildCreatureFrames(species);

        // Only cache a genuinely usable result. If sprite generation ever returns a null frame
        // (transient failure), caching it anyway would permanently render every future individual
        // of this species invisible - the static dictionary otherwise never gets a chance to
        // self-heal until a full domain reload clears it.
        if (IsValidFrames(frames))
        {
            creatureFramesBySpeciesId[species.id] = frames;
        }

        return frames;
    }

    // Exposes the same procedurally-generated creature sprite CreatureVisual renders in the world,
    // for use as a small icon elsewhere (e.g. the species summary panel) without needing a
    // duplicate, dedicated icon asset per species.
    public static Sprite GetSpeciesIconSprite(SpeciesDefinition species)
    {
        if (species == null)
        {
            return null;
        }

        return GetCreatureFrames(species)[0];
    }
```

(The `GetCreatureFrames` body above is unchanged — shown only to anchor the insertion point exactly after it.)

- [ ] **Step 4: Run tests to verify they pass**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `group_names: "EmergenceSystemTests"`.
Expected: PASS, all tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Ecosystem/EmergenceSystem.cs Assets/Scripts/Tests/EmergenceSystemTests.cs
git commit -m "feat: expose a static per-species icon sprite accessor on EmergenceSystem"
```

---

### Task 3: Theme tokens + panel UXML/USS

**Files:**
- Modify: `Assets/UI/Theme/Theme.uss`
- Create: `Assets/UI/SpeciesSummary/SpeciesSummary.uxml`
- Create: `Assets/UI/SpeciesSummary/SpeciesSummary.uss`

**Interfaces:**
- Consumes: `Theme.uss`'s existing token set (`--space-*`, `--radius-*`, `--color-*`, `--font-size-*`, `--border-*`, `.panel-enter`).
- Produces: element names/classes that `SpeciesSummaryUI.cs` (Task 4) queries: `species-summary-root`, `species-summary-frame`, `close-button`, `species-summary-scroll`, `trees-header-button`/`trees-row-list`, `herbivores-header-button`/`herbivores-row-list`, `predators-header-button`/`predators-row-list`, `criteria-popup-root`, `criteria-popup-title`, `criteria-popup-message`, `criteria-popup-close-button`.

This is markup/style only — no automated test applies; verified visually in Task 8.

- [ ] **Step 1: Add the summary surface tokens**

In `Assets/UI/Theme/Theme.uss`, add after the `--surface-demo-border: rgb(35, 37, 40);` line:

```css
    --surface-demo-border: rgb(35, 37, 40);
    --surface-summary: rgb(120, 130, 90);
    --surface-summary-inner: rgb(232, 232, 210);
    --surface-summary-border: rgb(50, 55, 35);
```

- [ ] **Step 2: Create `Assets/UI/SpeciesSummary/SpeciesSummary.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="project://database/Assets/UI/Theme/Theme.uss" />
    <ui:Style src="project://database/Assets/UI/SpeciesSummary/SpeciesSummary.uss" />
    <ui:VisualElement name="species-summary-root" class="species-summary-root theme-scope">
        <ui:VisualElement name="species-summary-frame" class="species-summary-frame">
            <ui:VisualElement name="species-summary-header" class="species-summary-header">
                <ui:Label name="species-summary-title" text="Species" class="species-summary-title" />
                <ui:Button name="close-button" text="X" class="species-summary-close-button" />
            </ui:VisualElement>
            <ui:ScrollView name="species-summary-scroll" class="species-summary-scroll">
                <ui:VisualElement name="trees-section" class="summary-section">
                    <ui:Button name="trees-header-button" text="Trees (0)" class="summary-section-header" />
                    <ui:VisualElement name="trees-row-list" class="summary-row-list" />
                </ui:VisualElement>
                <ui:VisualElement name="herbivores-section" class="summary-section">
                    <ui:Button name="herbivores-header-button" text="Herbivores (0)" class="summary-section-header" />
                    <ui:VisualElement name="herbivores-row-list" class="summary-row-list" />
                </ui:VisualElement>
                <ui:VisualElement name="predators-section" class="summary-section">
                    <ui:Button name="predators-header-button" text="Predators (0)" class="summary-section-header" />
                    <ui:VisualElement name="predators-row-list" class="summary-row-list" />
                </ui:VisualElement>
            </ui:ScrollView>
        </ui:VisualElement>
        <ui:VisualElement name="criteria-popup-root" class="criteria-popup-root">
            <ui:VisualElement name="criteria-popup-frame" class="criteria-popup-frame">
                <ui:Label name="criteria-popup-title" text="" class="criteria-popup-title" />
                <ui:Label name="criteria-popup-message" text="" class="criteria-popup-message" />
                <ui:Button name="criteria-popup-close-button" text="Close" class="criteria-popup-close-button" />
            </ui:VisualElement>
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 3: Create `Assets/UI/SpeciesSummary/SpeciesSummary.uss`**

```css
.species-summary-root {
    width: 100%;
    height: 100%;
    background-color: rgba(6, 8, 14, 0.85);
    padding: var(--space-sm);
    align-items: center;
    justify-content: center;
}

.species-summary-frame {
    width: 100%;
    height: 100%;
    max-width: 600px;
    -unity-background-scale-mode: scale-and-crop;
    border-radius: var(--radius-lg);
    border-top-width: var(--border-thick);
    border-left-width: var(--border-thick);
    border-right-width: var(--border-shadow);
    border-bottom-width: var(--border-shadow);
    border-top-color: var(--color-gold-light);
    border-left-color: var(--color-gold-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    padding: var(--space-xs);
    transition-property: opacity, scale;
    transition-duration: 0.18s;
    transition-timing-function: ease-out;
}

.species-summary-header {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
    background-color: var(--surface-summary-border);
    border-radius: var(--radius-md);
    border-width: var(--border-thin);
    border-color: rgb(35, 40, 25);
    padding: var(--space-xs) var(--space-sm);
    margin-bottom: var(--space-xs);
    flex-shrink: 0;
}

.species-summary-title {
    font-size: var(--font-size-lg);
    color: rgb(250, 235, 210);
    -unity-font-style: bold;
    -unity-text-outline-width: 1;
    -unity-text-outline-color: var(--color-ink);
}

.species-summary-close-button {
    width: 32px;
    height: 32px;
    border-radius: var(--radius-pill);
    border-top-width: var(--border-thin);
    border-left-width: var(--border-thin);
    border-right-width: var(--border-thick);
    border-bottom-width: var(--border-thick);
    border-top-color: var(--color-danger-light);
    border-left-color: var(--color-danger-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    background-color: var(--color-danger);
    color: var(--text-on-dark);
    font-size: var(--font-size-sm);
    -unity-font-style: bold;
}

.species-summary-close-button:active {
    scale: 0.92 0.92;
}

.species-summary-scroll {
    flex-grow: 1;
    -unity-background-scale-mode: scale-and-crop;
    border-radius: var(--radius-md);
    border-width: var(--border-thin);
    border-color: rgb(70, 80, 50);
    padding: var(--space-lg);
}

.summary-section {
    margin-bottom: var(--space-md);
}

.summary-section-header {
    width: 100%;
    height: 36px;
    justify-content: flex-start;
    padding: 0 var(--space-md);
    background-color: var(--color-ink);
    border-radius: var(--radius-md);
    border-top-width: var(--border-thin);
    border-left-width: var(--border-thin);
    border-right-width: var(--border-shadow);
    border-bottom-width: var(--border-shadow);
    border-top-color: var(--color-gold-light);
    border-left-color: var(--color-gold-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    color: var(--color-gold);
    font-size: var(--font-size-md);
    -unity-font-style: bold;
    -unity-text-align: middle-left;
}

.summary-section-header:active {
    scale: 0.98 0.98;
}

.summary-row-list {
    margin-top: var(--space-xs);
}

.summary-row {
    flex-direction: row;
    align-items: center;
    background-color: rgba(255, 255, 255, 0.06);
    border-radius: var(--radius-sm);
    border-width: 1px;
    border-color: rgba(255, 255, 255, 0.12);
    padding: var(--space-xs) var(--space-sm);
    margin-bottom: var(--space-xs);
}

.summary-row-icon {
    width: 28px;
    height: 28px;
    margin-right: var(--space-sm);
    flex-shrink: 0;
    -unity-background-scale-mode: scale-to-fit;
}

.summary-row-name {
    flex-grow: 1;
    font-size: var(--font-size-sm);
    color: var(--text-on-dark);
    -unity-font-style: bold;
}

.summary-row-count {
    font-size: var(--font-size-md);
    color: var(--color-gold);
    -unity-font-style: bold;
    -unity-text-outline-width: 1;
    -unity-text-outline-color: var(--color-ink);
}

.criteria-popup-root {
    display: none;
    position: absolute;
    width: 100%;
    height: 100%;
    background-color: rgba(6, 8, 14, 0.75);
    align-items: center;
    justify-content: center;
}

.criteria-popup-frame {
    width: 280px;
    background-color: var(--color-ink);
    border-radius: var(--radius-lg);
    border-top-width: var(--border-thick);
    border-left-width: var(--border-thick);
    border-right-width: var(--border-shadow);
    border-bottom-width: var(--border-shadow);
    border-top-color: var(--color-gold-light);
    border-left-color: var(--color-gold-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    padding: var(--space-lg);
    align-items: center;
}

.criteria-popup-title {
    font-size: var(--font-size-lg);
    color: var(--color-gold);
    -unity-font-style: bold;
    margin-bottom: var(--space-sm);
}

.criteria-popup-message {
    font-size: var(--font-size-sm);
    color: var(--text-on-dark);
    white-space: normal;
    -unity-text-align: middle-center;
    margin-bottom: var(--space-md);
}

.criteria-popup-close-button {
    height: 32px;
    padding: 0 var(--space-md);
    border-radius: var(--radius-md);
    border-top-width: var(--border-thin);
    border-left-width: var(--border-thin);
    border-right-width: var(--border-thick);
    border-bottom-width: var(--border-thick);
    border-top-color: var(--color-neutral-light);
    border-left-color: var(--color-neutral-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    background-color: var(--color-neutral);
    color: var(--text-on-dark);
    font-size: var(--font-size-sm);
    -unity-font-style: bold;
}

.criteria-popup-close-button:active {
    scale: 0.95 0.95;
}
```

- [ ] **Step 4: Commit**

```bash
git add Assets/UI/Theme/Theme.uss Assets/UI/SpeciesSummary/SpeciesSummary.uxml Assets/UI/SpeciesSummary/SpeciesSummary.uss
git commit -m "feat: add species summary panel markup, styles, and theme tokens"
```

---

### Task 4: `SpeciesSummaryUI.cs` + unit tests

**Files:**
- Create: `Assets/Scripts/UI/SpeciesSummaryUI.cs`
- Create: `Assets/Scripts/Tests/SpeciesSummaryTests.cs`

**Interfaces:**
- Consumes: `Assets/UI/SpeciesSummary/SpeciesSummary.uxml`'s element names (Task 3); `PanelAnimator.Open`/`.Close` (existing); `EmergenceSystem.GetSpeciesIconSprite` (Task 2, static); `EcosystemState.GetSpeciesPopulation`/`TotalTreeCount` (existing); `PlantedTree.ActiveTrees`/`.Vegetation` (existing); `VegetationDefinition.heroSprite`/`.plantSilhouette`/`.foliageColor`/`.trunkColor`/`.id`/`.displayName` (existing); `SpeciesDefinition.category`/`.id`/`.displayName`/`.preyThreshold`/`.preyToPredatorRatio`/`.preySpeciesIds`/`.treesPerIndividualCap`/`.requiredVegetationIds` (existing).
- Produces: `public void SetOpen(bool visible)` — used by `ScreenNavigator` (Task 5) and `HudUI` (Task 6). `public static Dictionary<string, int> GroupTreeCountsByVegetationId(IReadOnlyList<PlantedTree> trees)`, `public static List<SpeciesDefinition> FilterPresentSpecies(List<SpeciesDefinition> allSpecies, SpeciesDefinition.SpeciesCategory category)`, `public static string BuildCriteriaText(SpeciesDefinition species, Dictionary<string, string> vegetationDisplayNamesById)` — all used only internally by this class plus its test file.

- [ ] **Step 1: Write the failing tests**

Create `Assets/Scripts/Tests/SpeciesSummaryTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SpeciesSummaryTests
{
    private VegetationDefinition oak;
    private VegetationDefinition pine;
    private SpeciesDefinition herbivoreWithCap;
    private SpeciesDefinition herbivoreWithRequiredVegetation;
    private SpeciesDefinition predator;

    [SetUp]
    public void SetUp()
    {
        EcosystemState.ResetAll();

        oak = ScriptableObject.CreateInstance<VegetationDefinition>();
        oak.id = "oak";
        oak.displayName = "Oak";
        oak.growthStages = new[] { new VegetationDefinition.GrowthStage { stageName = "Mature", durationSeconds = 1 } };

        pine = ScriptableObject.CreateInstance<VegetationDefinition>();
        pine.id = "pine";
        pine.displayName = "Pine";
        pine.growthStages = new[] { new VegetationDefinition.GrowthStage { stageName = "Mature", durationSeconds = 1 } };

        herbivoreWithCap = ScriptableObject.CreateInstance<SpeciesDefinition>();
        herbivoreWithCap.id = "test_herbivore_cap";
        herbivoreWithCap.displayName = "Gazelle";
        herbivoreWithCap.category = SpeciesDefinition.SpeciesCategory.Herbivore;
        herbivoreWithCap.treesPerIndividualCap = 20;

        herbivoreWithRequiredVegetation = ScriptableObject.CreateInstance<SpeciesDefinition>();
        herbivoreWithRequiredVegetation.id = "test_herbivore_veg";
        herbivoreWithRequiredVegetation.displayName = "Squirrel";
        herbivoreWithRequiredVegetation.category = SpeciesDefinition.SpeciesCategory.Herbivore;
        herbivoreWithRequiredVegetation.requiredVegetationIds = new[] { "pine" };

        predator = ScriptableObject.CreateInstance<SpeciesDefinition>();
        predator.id = "test_predator";
        predator.displayName = "Wolf";
        predator.category = SpeciesDefinition.SpeciesCategory.Predator;
        predator.preySpeciesIds = new[] { "test_herbivore_cap" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 2;
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(oak);
        Object.DestroyImmediate(pine);
        Object.DestroyImmediate(herbivoreWithCap);
        Object.DestroyImmediate(herbivoreWithRequiredVegetation);
        Object.DestroyImmediate(predator);

        foreach (PlantedTree tree in Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Include))
        {
            Object.DestroyImmediate(tree.gameObject);
        }
    }

    private PlantedTree CreateTestTree(VegetationDefinition vegetation)
    {
        GameObject go = new GameObject("TestTree_" + vegetation.id);
        go.AddComponent<SpriteRenderer>();
        PlantedTree tree = go.AddComponent<PlantedTree>();
        tree.Initialize(vegetation);
        return tree;
    }

    [Test]
    public void GroupTreeCountsByVegetationId_GroupsByType_ExcludesZeroCountTypes()
    {
        List<PlantedTree> trees = new List<PlantedTree>
        {
            CreateTestTree(oak),
            CreateTestTree(oak),
            CreateTestTree(pine)
        };

        Dictionary<string, int> counts = SpeciesSummaryUI.GroupTreeCountsByVegetationId(trees);

        Assert.AreEqual(2, counts.Count);
        Assert.AreEqual(2, counts["oak"]);
        Assert.AreEqual(1, counts["pine"]);
    }

    [Test]
    public void GroupTreeCountsByVegetationId_EmptyList_ReturnsEmptyDictionary()
    {
        Dictionary<string, int> counts = SpeciesSummaryUI.GroupTreeCountsByVegetationId(new List<PlantedTree>());

        Assert.AreEqual(0, counts.Count);
    }

    [Test]
    public void FilterPresentSpecies_OnlyIncludesPositivePopulationOfMatchingCategory()
    {
        EcosystemState.SetSpeciesPopulation("test_herbivore_cap", 3);
        EcosystemState.SetSpeciesPopulation("test_herbivore_veg", 0);
        EcosystemState.SetSpeciesPopulation("test_predator", 1);

        List<SpeciesDefinition> all = new List<SpeciesDefinition> { herbivoreWithCap, herbivoreWithRequiredVegetation, predator };

        List<SpeciesDefinition> herbivores = SpeciesSummaryUI.FilterPresentSpecies(all, SpeciesDefinition.SpeciesCategory.Herbivore);
        List<SpeciesDefinition> predators = SpeciesSummaryUI.FilterPresentSpecies(all, SpeciesDefinition.SpeciesCategory.Predator);

        CollectionAssert.AreEquivalent(new[] { herbivoreWithCap }, herbivores);
        CollectionAssert.AreEquivalent(new[] { predator }, predators);
    }

    [Test]
    public void BuildCriteriaText_Predator_IncludesThresholdAndCurrentPrey()
    {
        EcosystemState.SetSpeciesPopulation("test_herbivore_cap", 3);

        string text = SpeciesSummaryUI.BuildCriteriaText(predator, new Dictionary<string, string>());

        Assert.AreEqual("Needs 5+ prey present (currently 3). Capacity: 1 per 2 prey.", text);
    }

    [Test]
    public void BuildCriteriaText_HerbivoreWithTreeCap_IncludesCurrentTreeCount()
    {
        EcosystemState.RegisterTreePlanted("oak");
        EcosystemState.RegisterTreePlanted("oak");

        string text = SpeciesSummaryUI.BuildCriteriaText(herbivoreWithCap, new Dictionary<string, string>());

        Assert.AreEqual("Needs 20 trees planted per individual (currently 2 trees).", text);
    }

    [Test]
    public void BuildCriteriaText_HerbivoreWithRequiredVegetation_AppendsRequiresList()
    {
        Dictionary<string, string> vegetationNames = new Dictionary<string, string> { { "pine", "Pine" } };

        string text = SpeciesSummaryUI.BuildCriteriaText(herbivoreWithRequiredVegetation, vegetationNames);

        Assert.AreEqual("No tree requirement. Requires: Pine.", text);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `group_names: "SpeciesSummaryTests"`.
Expected: FAIL — `SpeciesSummaryUI` doesn't exist yet (compile error).

- [ ] **Step 3: Implement `SpeciesSummaryUI.cs`**

Create `Assets/Scripts/UI/SpeciesSummaryUI.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class SpeciesSummaryUI : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();
    [SerializeField] private List<VegetationDefinition> allVegetation = new List<VegetationDefinition>();
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement summaryRoot;
    private VisualElement summaryFrame;
    private Button treesHeaderButton;
    private VisualElement treesRowList;
    private Button herbivoresHeaderButton;
    private VisualElement herbivoresRowList;
    private Button predatorsHeaderButton;
    private VisualElement predatorsRowList;
    private VisualElement criteriaPopupRoot;
    private Label criteriaPopupTitle;
    private Label criteriaPopupMessage;
    private Dictionary<string, string> vegetationDisplayNamesById;
    private float refreshTimer;

    private bool treesExpanded = true;
    private bool herbivoresExpanded = true;
    private bool predatorsExpanded = true;

    public static bool IsOpen { get; private set; }

    private void Awake()
    {
        uiDocument = GetComponent<UIDocument>();
    }

    private void OnEnable()
    {
        VisualElement root = uiDocument.rootVisualElement;
        summaryRoot = root.Q<VisualElement>("species-summary-root");
        summaryFrame = root.Q<VisualElement>("species-summary-frame");

        root.Q<Button>("close-button").clicked += OnCloseButtonClicked;

        treesHeaderButton = root.Q<Button>("trees-header-button");
        treesHeaderButton.clicked += OnTreesHeaderClicked;
        treesRowList = root.Q<VisualElement>("trees-row-list");

        herbivoresHeaderButton = root.Q<Button>("herbivores-header-button");
        herbivoresHeaderButton.clicked += OnHerbivoresHeaderClicked;
        herbivoresRowList = root.Q<VisualElement>("herbivores-row-list");

        predatorsHeaderButton = root.Q<Button>("predators-header-button");
        predatorsHeaderButton.clicked += OnPredatorsHeaderClicked;
        predatorsRowList = root.Q<VisualElement>("predators-row-list");

        criteriaPopupRoot = root.Q<VisualElement>("criteria-popup-root");
        criteriaPopupTitle = root.Q<Label>("criteria-popup-title");
        criteriaPopupMessage = root.Q<Label>("criteria-popup-message");
        root.Q<Button>("criteria-popup-close-button").clicked += OnCriteriaPopupCloseButtonClicked;

        vegetationDisplayNamesById = new Dictionary<string, string>();
        foreach (VegetationDefinition vegetation in allVegetation)
        {
            if (vegetation != null)
            {
                vegetationDisplayNamesById[vegetation.id] = vegetation.displayName;
            }
        }

        // Direct hide (not SetOpen/PanelAnimator): this is the initial startup state, not a
        // user-triggered close, so it must be instant with zero flash - no 180ms animated delay.
        summaryRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (summaryRoot == null || summaryRoot.style.display != DisplayStyle.Flex)
        {
            return;
        }

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            refreshTimer = RefreshInterval;
            RefreshAll();
        }
    }

    public void SetOpen(bool visible)
    {
        if (visible)
        {
            RefreshAll();
            refreshTimer = RefreshInterval;
            PanelAnimator.Open(summaryRoot, summaryFrame);
        }
        else
        {
            PanelAnimator.Close(summaryRoot, summaryFrame);
            HideCriteriaPopup();
        }

        IsOpen = visible;
    }

    private void OnCloseButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.GoHome();
        }
        else
        {
            SetOpen(false);
        }
    }

    private void OnTreesHeaderClicked()
    {
        AudioManager.Instance?.PlayTap();
        treesExpanded = !treesExpanded;
        treesRowList.style.display = treesExpanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnHerbivoresHeaderClicked()
    {
        AudioManager.Instance?.PlayTap();
        herbivoresExpanded = !herbivoresExpanded;
        herbivoresRowList.style.display = herbivoresExpanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnPredatorsHeaderClicked()
    {
        AudioManager.Instance?.PlayTap();
        predatorsExpanded = !predatorsExpanded;
        predatorsRowList.style.display = predatorsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void RefreshAll()
    {
        RefreshTreesSection();
        RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory.Herbivore, herbivoresRowList, herbivoresHeaderButton, "Herbivores");
        RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory.Predator, predatorsRowList, predatorsHeaderButton, "Predators");
    }

    private void RefreshTreesSection()
    {
        treesRowList.Clear();

        Dictionary<string, int> counts = GroupTreeCountsByVegetationId(PlantedTree.ActiveTrees);
        Dictionary<string, VegetationDefinition> representative = new Dictionary<string, VegetationDefinition>();
        foreach (PlantedTree tree in PlantedTree.ActiveTrees)
        {
            if (tree != null && tree.Vegetation != null)
            {
                representative[tree.Vegetation.id] = tree.Vegetation;
            }
        }

        int typeCount = 0;
        foreach (KeyValuePair<string, int> entry in counts)
        {
            if (entry.Value <= 0 || !representative.TryGetValue(entry.Key, out VegetationDefinition vegetation))
            {
                continue;
            }

            typeCount++;
            Sprite icon = vegetation.heroSprite != null
                ? vegetation.heroSprite
                : PixelArtUtility.CreateVegetationSprite(48, vegetation.plantSilhouette, vegetation.foliageColor, vegetation.trunkColor, vegetation.id.GetHashCode());
            treesRowList.Add(CreateRow(icon, vegetation.displayName, entry.Value, null));
        }

        treesHeaderButton.text = $"Trees ({typeCount})";
    }

    private void RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory category, VisualElement rowList, Button headerButton, string categoryLabel)
    {
        rowList.Clear();

        List<SpeciesDefinition> present = FilterPresentSpecies(allSpecies, category);
        foreach (SpeciesDefinition species in present)
        {
            Sprite icon = EmergenceSystem.GetSpeciesIconSprite(species);
            int population = EcosystemState.GetSpeciesPopulation(species.id);
            SpeciesDefinition capturedSpecies = species;
            rowList.Add(CreateRow(icon, species.displayName, population, () => ShowCriteriaPopup(capturedSpecies)));
        }

        headerButton.text = $"{categoryLabel} ({present.Count})";
    }

    private VisualElement CreateRow(Sprite icon, string displayName, int count, System.Action onClick)
    {
        VisualElement row = onClick != null ? new Button(onClick) : new VisualElement();
        row.AddToClassList("summary-row");

        VisualElement iconElement = new VisualElement();
        iconElement.AddToClassList("summary-row-icon");
        iconElement.style.backgroundImage = new StyleBackground(icon);
        row.Add(iconElement);

        Label nameLabel = new Label(displayName);
        nameLabel.AddToClassList("summary-row-name");
        row.Add(nameLabel);

        Label countLabel = new Label(count.ToString());
        countLabel.AddToClassList("summary-row-count");
        row.Add(countLabel);

        return row;
    }

    private void ShowCriteriaPopup(SpeciesDefinition species)
    {
        AudioManager.Instance?.PlayTap();
        criteriaPopupTitle.text = species.displayName;
        criteriaPopupMessage.text = BuildCriteriaText(species, vegetationDisplayNamesById);
        criteriaPopupRoot.style.display = DisplayStyle.Flex;
    }

    private void OnCriteriaPopupCloseButtonClicked()
    {
        AudioManager.Instance?.PlayTap();
        HideCriteriaPopup();
    }

    private void HideCriteriaPopup()
    {
        if (criteriaPopupRoot != null)
        {
            criteriaPopupRoot.style.display = DisplayStyle.None;
        }
    }

    // Pure grouping logic, extracted for direct unit testing (see SpeciesSummaryTests.cs).
    public static Dictionary<string, int> GroupTreeCountsByVegetationId(IReadOnlyList<PlantedTree> trees)
    {
        Dictionary<string, int> counts = new Dictionary<string, int>();
        foreach (PlantedTree tree in trees)
        {
            if (tree == null || tree.Vegetation == null)
            {
                continue;
            }

            counts.TryGetValue(tree.Vegetation.id, out int count);
            counts[tree.Vegetation.id] = count + 1;
        }

        return counts;
    }

    // Pure filtering logic, extracted for direct unit testing (see SpeciesSummaryTests.cs).
    public static List<SpeciesDefinition> FilterPresentSpecies(List<SpeciesDefinition> allSpecies, SpeciesDefinition.SpeciesCategory category)
    {
        List<SpeciesDefinition> result = new List<SpeciesDefinition>();
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species != null && species.category == category && EcosystemState.GetSpeciesPopulation(species.id) > 0)
            {
                result.Add(species);
            }
        }

        return result;
    }

    // Pure text-building logic, extracted for direct unit testing (see SpeciesSummaryTests.cs).
    // Surfaces the same criteria EmergenceSystem.CanRelease already gates on, as human-readable
    // text - does not change or duplicate the actual gating decision.
    public static string BuildCriteriaText(SpeciesDefinition species, Dictionary<string, string> vegetationDisplayNamesById)
    {
        if (species.category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            int totalPrey = 0;
            if (species.preySpeciesIds != null)
            {
                foreach (string preyId in species.preySpeciesIds)
                {
                    totalPrey += EcosystemState.GetSpeciesPopulation(preyId);
                }
            }

            return $"Needs {species.preyThreshold}+ prey present (currently {totalPrey}). Capacity: 1 per {species.preyToPredatorRatio} prey.";
        }

        string text = species.treesPerIndividualCap > 0
            ? $"Needs {species.treesPerIndividualCap} trees planted per individual (currently {EcosystemState.TotalTreeCount} trees)."
            : "No tree requirement.";

        if (species.requiredVegetationIds != null && species.requiredVegetationIds.Length > 0)
        {
            List<string> names = new List<string>();
            foreach (string vegetationId in species.requiredVegetationIds)
            {
                string name = vegetationDisplayNamesById != null && vegetationDisplayNamesById.TryGetValue(vegetationId, out string found)
                    ? found
                    : vegetationId;
                names.Add(name);
            }

            text += $" Requires: {string.Join(", ", names)}.";
        }

        return text;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `group_names: "SpeciesSummaryTests"`.
Expected: PASS, all 7 tests.

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/SpeciesSummaryUI.cs Assets/Scripts/Tests/SpeciesSummaryTests.cs
git commit -m "feat: add SpeciesSummaryUI panel logic with unit-tested grouping/filtering/criteria helpers"
```

---

### Task 5: `ScreenNavigator` wiring

**Files:**
- Modify: `Assets/Scripts/UI/ScreenNavigator.cs`

**Interfaces:**
- Consumes: `SpeciesSummaryUI.SetOpen(bool)` (Task 4).
- Produces: `public void OpenSpeciesSummary()` — used by `HudUI` (Task 6).

- [ ] **Step 1: Add the field and method**

In `Assets/Scripts/UI/ScreenNavigator.cs`, add the field after `mapUI`:

```csharp
    [SerializeField] private MapUI mapUI;
    [SerializeField] private SpeciesSummaryUI speciesSummaryUI;
```

Add the method after `OpenMap()`:

```csharp
    public void OpenSpeciesSummary()
    {
        CloseOtherPanels(speciesSummaryUI);
        if (speciesSummaryUI != null) speciesSummaryUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }
```

Add the close entry in `CloseOtherPanels`, after the `mapUI` block:

```csharp
        if (mapUI != null && !ReferenceEquals(mapUI, except))
        {
            mapUI.SetOpen(false);
        }

        if (speciesSummaryUI != null && !ReferenceEquals(speciesSummaryUI, except))
        {
            speciesSummaryUI.SetOpen(false);
        }
```

- [ ] **Step 2: Validate the script**

Use `mcp__UnityMCP__validate_script` on `Assets/Scripts/UI/ScreenNavigator.cs`. Expected: no errors.

- [ ] **Step 3: Commit**

```bash
git add Assets/Scripts/UI/ScreenNavigator.cs
git commit -m "feat: wire ScreenNavigator.OpenSpeciesSummary"
```

---

### Task 6: `HudUI` wiring + `Hud.uxml` button

**Files:**
- Modify: `Assets/Scripts/UI/HudUI.cs`
- Modify: `Assets/UI/Hud/Hud.uxml`

**Interfaces:**
- Consumes: `Assets/UI/Hud/Hud.uxml`'s new `species-summary-button`; `ScreenNavigator.OpenSpeciesSummary()` (Task 5); `SpeciesSummaryUI.SetOpen(bool)` (Task 4, fallback path).
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Add the button to `Hud.uxml`**

In `Assets/UI/Hud/Hud.uxml`, replace:

```xml
        <ui:VisualElement name="action-cluster" class="action-cluster">
            <ui:Button name="lab-button" class="icon-button" />
            <ui:Button name="codex-button" class="icon-button" />
            <ui:Button name="profile-button" class="icon-button" />
            <ui:Button name="settings-button" class="icon-button" />
        </ui:VisualElement>
```

with:

```xml
        <ui:VisualElement name="action-cluster" class="action-cluster">
            <ui:Button name="lab-button" class="icon-button" />
            <ui:Button name="codex-button" class="icon-button" />
            <ui:Button name="profile-button" class="icon-button" />
            <ui:Button name="species-summary-button" class="icon-button" />
            <ui:Button name="settings-button" class="icon-button" />
        </ui:VisualElement>
```

- [ ] **Step 2: Add the field to `HudUI.cs`**

In `Assets/Scripts/UI/HudUI.cs`, add after `[SerializeField] private ProfileUI profileUI;`:

```csharp
    [SerializeField] private ProfileUI profileUI;
    [SerializeField] private SpeciesSummaryUI speciesSummaryUI;
```

- [ ] **Step 3: Wire the button in `OnEnable`**

Replace:

```csharp
        Button profileButton = root.Q<Button>("profile-button");
        profileButton.clicked += OnProfileButtonClicked;
        profileButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.73f, 0.54f, 0.24f), PixelArtUtility.DrawPersonGlyph));

        Button settingsButton = root.Q<Button>("settings-button");
```

with:

```csharp
        Button profileButton = root.Q<Button>("profile-button");
        profileButton.clicked += OnProfileButtonClicked;
        profileButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.73f, 0.54f, 0.24f), PixelArtUtility.DrawPersonGlyph));

        Button speciesSummaryButton = root.Q<Button>("species-summary-button");
        speciesSummaryButton.clicked += OnSpeciesSummaryButtonClicked;
        speciesSummaryButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.45f, 0.58f, 0.32f), PixelArtUtility.DrawPawGlyph));

        Button settingsButton = root.Q<Button>("settings-button");
```

- [ ] **Step 4: Add the click handler**

Add after `OnProfileButtonClicked()`:

```csharp
    private void OnSpeciesSummaryButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenSpeciesSummary();
        }
        else if (speciesSummaryUI != null)
        {
            speciesSummaryUI.SetOpen(true);
        }
    }
```

- [ ] **Step 5: Validate the script**

Use `mcp__UnityMCP__validate_script` on `Assets/Scripts/UI/HudUI.cs`. Expected: no errors.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/HudUI.cs Assets/UI/Hud/Hud.uxml
git commit -m "feat: add species summary HUD button"
```

---

### Task 7: Scene wiring

**Files:**
- Modify (scene data, via `execute_code`): `Assets/Scenes/SampleScene.unity`
- Create (via `execute_code`/`AssetDatabase.CopyAsset`): `Assets/UI/SpeciesSummary/SpeciesSummaryPanelSettings.asset`
- Modify: `Assets/Scripts/Tests/SceneRosterWiringTests.cs`

**Interfaces:**
- Consumes: `SpeciesSummaryUI` (Task 4), `ScreenNavigator.speciesSummaryUI` (Task 5), `HudUI.speciesSummaryUI` (Task 6); species/vegetation assets at `Assets/Data/Species/Gazelle.asset`, `Assets/Data/Species/Squirrel.asset`, `Assets/Data/Species/Wolf.asset`, `Assets/Data/Species/Fox.asset`, `Assets/Data/Vegetation/Oak.asset`, `Assets/Data/Vegetation/Pine.asset` (matching every other panel's existing roster, confirmed via `SceneRosterWiringTests`'s `CodexUI_AllSpecies_MatchesRoster`/`CodexUI_AllVegetation_MatchesRoster`).
- Produces: nothing consumed by later tasks — this is the last wiring step.

- [ ] **Step 1: Create the panel's `PanelSettings` asset and scene GameObject**

Use `mcp__UnityMCP__execute_code`:

```csharp
using UnityEditor;
using UnityEditor.SceneManagement;

string settingsPath = "Assets/UI/SpeciesSummary/SpeciesSummaryPanelSettings.asset";
if (!System.IO.File.Exists(settingsPath))
{
    AssetDatabase.CopyAsset("Assets/UI/Profile/ProfilePanelSettings.asset", settingsPath);
    AssetDatabase.Refresh();
}

UnityEngine.UIElements.PanelSettings panelSettings = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.PanelSettings>(settingsPath);
UnityEngine.UIElements.VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<UnityEngine.UIElements.VisualTreeAsset>("Assets/UI/SpeciesSummary/SpeciesSummary.uxml");

GameObject go = new GameObject("SpeciesSummaryUI");
UnityEngine.UIElements.UIDocument doc = go.AddComponent<UnityEngine.UIElements.UIDocument>();
doc.panelSettings = panelSettings;
doc.visualTreeAsset = visualTree;
doc.sortingOrder = 1;

SpeciesSummaryUI summaryUI = go.AddComponent<SpeciesSummaryUI>();

List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>
{
    AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Gazelle.asset"),
    AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Squirrel.asset"),
    AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Wolf.asset"),
    AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Fox.asset")
};
List<VegetationDefinition> allVegetation = new List<VegetationDefinition>
{
    AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Oak.asset"),
    AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Pine.asset")
};

ScreenNavigator nav = UnityEngine.Object.FindFirstObjectByType<ScreenNavigator>(FindObjectsInactive.Include);
HudUI hud = UnityEngine.Object.FindFirstObjectByType<HudUI>(FindObjectsInactive.Include);

SerializedObject summarySerialized = new SerializedObject(summaryUI);
SerializedProperty speciesProp = summarySerialized.FindProperty("allSpecies");
speciesProp.ClearArray();
for (int i = 0; i < allSpecies.Count; i++)
{
    speciesProp.InsertArrayElementAtIndex(i);
    speciesProp.GetArrayElementAtIndex(i).objectReferenceValue = allSpecies[i];
}
SerializedProperty vegetationProp = summarySerialized.FindProperty("allVegetation");
vegetationProp.ClearArray();
for (int i = 0; i < allVegetation.Count; i++)
{
    vegetationProp.InsertArrayElementAtIndex(i);
    vegetationProp.GetArrayElementAtIndex(i).objectReferenceValue = allVegetation[i];
}
summarySerialized.FindProperty("screenNavigator").objectReferenceValue = nav;
summarySerialized.ApplyModifiedProperties();

SerializedObject navSerialized = new SerializedObject(nav);
navSerialized.FindProperty("speciesSummaryUI").objectReferenceValue = summaryUI;
navSerialized.ApplyModifiedProperties();

SerializedObject hudSerialized = new SerializedObject(hud);
hudSerialized.FindProperty("speciesSummaryUI").objectReferenceValue = summaryUI;
hudSerialized.ApplyModifiedProperties();

EditorSceneManager.MarkSceneDirty(go.scene);
EditorSceneManager.SaveScene(go.scene);

return "wired, allSpecies=" + allSpecies.Count + " allVegetation=" + allVegetation.Count;
```

(Requires `using System.Collections.Generic;` and `using UnityEngine;` in addition to the two shown — the tool wraps the snippet as a method body, so add these usings at the top of the executed code alongside `using UnityEditor;` and `using UnityEditor.SceneManagement;`.) Confirm the returned result shows `allSpecies=4 allVegetation=2` with no error.

- [ ] **Step 2: Write the failing scene-wiring tests**

Add to `Assets/Scripts/Tests/SceneRosterWiringTests.cs`, after the existing `HudUI_PickerIconSprites_AreWired` test (before the closing brace of the class):

```csharp
    [Test]
    public void SpeciesSummaryUI_AllSpecies_MatchesRoster()
    {
        SpeciesSummaryUI summaryUI = FindComponent<SpeciesSummaryUI>();
        Assert.IsNotNull(summaryUI);
        CollectionAssert.AreEquivalent(new[] { "gazelle", "squirrel", "wolf", "fox" }, GetIds(summaryUI, "allSpecies"));
    }

    [Test]
    public void SpeciesSummaryUI_AllVegetation_MatchesRoster()
    {
        SpeciesSummaryUI summaryUI = FindComponent<SpeciesSummaryUI>();
        Assert.IsNotNull(summaryUI);
        CollectionAssert.AreEquivalent(new[] { "oak", "pine" }, GetIds(summaryUI, "allVegetation"));
    }

    [Test]
    public void ScreenNavigator_SpeciesSummaryUI_IsWired()
    {
        ScreenNavigator navigator = FindComponent<ScreenNavigator>();
        Assert.IsNotNull(navigator);
        Assert.IsNotNull(GetPrivateField(navigator, "speciesSummaryUI"));
    }

    [Test]
    public void HudUI_SpeciesSummaryUI_IsWired()
    {
        HudUI hud = FindComponent<HudUI>();
        Assert.IsNotNull(hud);
        Assert.IsNotNull(GetPrivateField(hud, "speciesSummaryUI"));
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `group_names: "SceneRosterWiringTests"`, `include_failed_tests: true`.
Expected: PASS for the 4 new tests and every pre-existing test in this file.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scenes/SampleScene.unity Assets/UI/SpeciesSummary/SpeciesSummaryPanelSettings.asset Assets/UI/SpeciesSummary/SpeciesSummaryPanelSettings.asset.meta Assets/Scripts/Tests/SceneRosterWiringTests.cs
git commit -m "chore: wire the species summary panel into the scene"
```

---

### Task 8: Manual Play-mode verification

**Files:** none (manual verification only, no code changes).

**Interfaces:**
- Consumes: the complete feature from Tasks 1-7.
- Produces: nothing — this is the final acceptance check before the plan is considered complete.

- [ ] **Step 1: Enter Play mode and open the panel**

Use `mcp__UnityMCP__manage_editor` to enter Play mode. Tap the new species-summary button in the HUD action-cluster; confirm the panel opens with three sections, each showing "(0)" if nothing is present yet.

- [ ] **Step 2: Verify presence rule and live counts**

Plant a couple of trees and release a herbivore and a predator (via the Demo panel's debug buttons, or the HUD tap-to-place picker from stage 3) while the panel is open. Confirm each section's header count and row list update within ~0.5s without needing to close/reopen the panel.

- [ ] **Step 3: Verify accordion expand/collapse**

Tap each section header; confirm its row list toggles hidden/visible independently of the other two sections.

- [ ] **Step 4: Verify criteria popup**

Tap a herbivore row and a predator row; confirm each shows a popup with correct, current criteria text (matching `BuildCriteriaText`'s format, with real numbers from `EcosystemState`). Confirm tapping a tree row does nothing (not tappable).

- [ ] **Step 5: Exit Play mode**

Use `mcp__UnityMCP__manage_editor` to exit Play mode. No commit for this task (verification only).

---

## Self-Review Notes

- **Spec coverage:** every "In" scope item from `docs/superpowers/specs/2026-08-14-species-summary-panel-design.md` maps to a task — panel entry point (Tasks 5-6), three accordion sections (Task 4), tree rows/icons (Task 4), herbivore/predator rows/icons (Tasks 2, 4), criteria popup (Task 4), live refresh (Task 4), scene wiring (Task 7). The stage-3 test-gap correction is an explicitly user-approved addition (Task 1), not scope creep.
- **Placeholder scan:** no TBD/TODO; every step has complete code, not a description.
- **Type consistency:** `SpeciesSummaryUI.GroupTreeCountsByVegetationId`/`FilterPresentSpecies`/`BuildCriteriaText` signatures in Task 4 match their Task 4 test-file usage exactly (both defined in the same task); `EmergenceSystem.GetSpeciesIconSprite(SpeciesDefinition)` (Task 2) matches its Task 4 call site exactly; `ScreenNavigator.OpenSpeciesSummary()` (Task 5) matches its Task 6 call site exactly; `SpeciesSummaryUI.SetOpen(bool)` (Task 4) matches its Task 5/6 call sites exactly.
