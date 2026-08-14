# HUD Category/Species Picker & Tap-to-Place Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the HUD's single round vegetation-cycling button into a full Plant/Herbivore/Predator category + species picker that drives both tree planting and criteria-gated animal release at the tapped world position.

**Architecture:** `EmergenceSystem` gains position-aware release (`TryReleaseSpeciesAt`) and per-category "current species" state (mirroring `TreePlanter`'s existing `currentVegetationIndex`/`CycleVegetation` pattern), by refactoring its internal spawn helpers to take an explicit `Vector3 position` instead of resolving one themselves. `HudUI` renames its `vegetation-*` picker to generic `picker-*`, adds a `PickerCategory` enum, and makes the label a clickable `Button`. `CameraPanController`'s empty-ground tap branches on the HUD's active category to call either `TreePlanter.PlantTree` (unchanged) or the new `EmergenceSystem.TryReleaseSpeciesAt`.

**Tech Stack:** Unity 6000.5.6f1, C#, UI Toolkit (UXML/USS), NUnit EditMode tests.

## Global Constraints

- Icon button cycles category: Plant → Herbivore → Predator → Plant (wrapping).
- Label pill is clickable and cycles species within the active category, remembering the last-picked species per category (switching category and back restores the prior pick for that category — not a reset to index 0).
- `EmergenceSystem` per-category species selection mirrors `TreePlanter`'s existing pattern: `[SerializeField] private int` index fields, wrapped with `%`.
- `TryReleaseSpeciesAt` checks, in order: `species != null`; `LandManager.Instance.IsWithinOwnedLand(position)` (fail → log, `AudioManager.Instance?.PlayError()`, `HudUI.Instance?.ShowLandLockedPopup()`); `RiverManager.Instance.IsBlocked(position)` (fail → log, play error); `RiverManager.Instance.IsWithinSandBank(position)` (fail → log, play error); `CanRelease(species)` (fail → log, play error). On success: spawn at `position`, `AudioManager.Instance?.PlayPlant()`, `PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased)`. Does **not** check Lab-exclusion-zone or tree-spacing — those are tree-only rules.
- All `LandManager.Instance`/`RiverManager.Instance` checks are null-guarded (`!= null &&`) exactly like `TreePlanter.PlantTree`, since EditMode tests never have these singletons present.
- Renaming `vegetation-*` → `picker-*` is scoped to `Hud.uxml`, `Hud.uss`, and `HudUI.cs` only — `Demo.uxml`'s `unlock-vegetation-button` and every `TreePlanter`/`VegetationDefinition` member (`CurrentVegetation`, `CycleVegetation`, `unlockedVegetation`, etc.) are untouched.
- Demo panel's four "Release X" buttons (stage 2) are untouched by this stage.
- No change to `CanRelease`'s criteria math, gem cost, or daily cap on release.
- Three new AAA-rendered icons already exist and are committed as final assets: `Assets/Textures/Hud/VegetationIcon.png` (Plant, reused as-is), `Assets/Textures/Hud/HerbivoreIcon.png` (Herbivore), `Assets/Textures/Hud/PredatorIcon.png` (Predator) — import settings already match `VegetationIcon.png` (confirmed: same `textureType`/`spriteMode`/`textureShape`).

---

## File Structure

- **Modify `Assets/Scripts/Ecosystem/EmergenceSystem.cs`:** refactor `SpawnIndividual`/`SpawnCreatureVisual` to take an explicit `Vector3 position`; add `currentHerbivoreIndex`/`currentPredatorIndex` fields, `CurrentReleasableSpecies(category)`, `CycleReleasableSpecies(category)`, `TryReleaseSpeciesAt(species, position)`.
- **Modify `Assets/Scripts/Tests/EmergenceSystemTests.cs`:** add tests for the three new members.
- **Modify `Assets/Scripts/UI/HudUI.cs`:** rename `vegetation-*` fields/methods to `picker-*`/generic names, add `PickerCategory` enum + `currentPickerCategory` field, add `herbivoreIconSprite`/`predatorIconSprite` fields, category-cycling + label-cycling logic.
- **Modify `Assets/UI/Hud/Hud.uxml`:** rename `vegetation-cluster`/`vegetation-button`/`vegetation-label` to `picker-cluster`/`picker-button`/`picker-label`; label becomes a `Button`.
- **Modify `Assets/UI/Hud/Hud.uss`:** rename `.vegetation-cluster`/`.vegetation-label`/`.vegetation-icon-button` selectors to `.picker-cluster`/`.picker-label`/`.picker-icon-button`.
- **Modify `Assets/Scripts/Vegetation/CameraPanController.cs`:** add `[SerializeField] private EmergenceSystem emergenceSystem;`, branch the empty-ground tap on `HudUI.Instance`'s active category.
- **Scene wiring (via `execute_code`):** assign `herbivoreIconSprite`/`predatorIconSprite` on the `HudUI` component, assign `emergenceSystem` on the `CameraPanController` component, in `Assets/Scenes/SampleScene.unity`.
- **Modify `Assets/Scripts/Tests/SceneRosterWiringTests.cs`:** add a test confirming `CameraPanController`'s `emergenceSystem` field is wired in the scene.

---

### Task 1: `EmergenceSystem` position-aware spawn refactor + `TryReleaseSpeciesAt`

**Files:**
- Modify: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`
- Test: `Assets/Scripts/Tests/EmergenceSystemTests.cs`

**Interfaces:**
- Consumes: nothing new — internal refactor of existing private methods, plus the existing `CanRelease(SpeciesDefinition)` (unchanged).
- Produces: `public bool TryReleaseSpeciesAt(SpeciesDefinition species, Vector3 position)` — used by Task 4 (`CameraPanController`).

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Scripts/Tests/EmergenceSystemTests.cs` (after the existing `TryReleaseSpecies_Predator_Succeeds_IncrementsPopulation` test, before the `CountActiveCreaturesForSpecies` helper):

```csharp
    [Test]
    public void TryReleaseSpeciesAt_Succeeds_IncrementsPopulationAtGivenPosition()
    {
        herbivore.treesPerIndividualCap = 1;
        EcosystemState.RegisterTreePlanted("oak");
        Vector3 targetPosition = new Vector3(3f, 4f, 0f);

        bool released = system.TryReleaseSpeciesAt(herbivore, targetPosition);

        Assert.IsTrue(released);
        Assert.AreEqual(1, EcosystemState.GetSpeciesPopulation("test_herbivore"));

        CreatureVisual creature = null;
        foreach (CreatureVisual candidate in CreatureVisual.ActiveCreatures)
        {
            if (candidate.SpeciesId == "test_herbivore")
            {
                creature = candidate;
                break;
            }
        }

        Assert.IsNotNull(creature, "expected a spawned creature for test_herbivore");
        Assert.AreEqual(targetPosition, creature.transform.position);
    }

    [Test]
    public void TryReleaseSpeciesAt_UnmetCriteria_ReturnsFalseAndDoesNotChangePopulation()
    {
        herbivore.treesPerIndividualCap = 10;
        EcosystemState.RegisterTreePlanted("oak"); // capacity = 1/10 = 0

        bool released = system.TryReleaseSpeciesAt(herbivore, Vector3.zero);

        Assert.IsFalse(released);
        Assert.AreEqual(0, EcosystemState.GetSpeciesPopulation("test_herbivore"));
    }

    [Test]
    public void TryReleaseSpeciesAt_NullSpecies_ReturnsFalse()
    {
        Assert.IsFalse(system.TryReleaseSpeciesAt(null, Vector3.zero));
    }

    [Test]
    public void CurrentReleasableSpecies_Herbivore_DefaultsToFirstUnlocked()
    {
        system.AddUnlockedSpecies(herbivore);

        Assert.AreEqual(herbivore, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));
    }

    [Test]
    public void CurrentReleasableSpecies_Predator_DefaultsToFirstUnlocked()
    {
        system.AddUnlockedSpecies(predator);

        Assert.AreEqual(predator, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Predator));
    }

    [Test]
    public void CurrentReleasableSpecies_EmptyCategory_ReturnsNull()
    {
        Assert.IsNull(system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));
    }

    [Test]
    public void CycleReleasableSpecies_Herbivore_WrapsAcrossTwoSpecies()
    {
        SpeciesDefinition herbivore2 = ScriptableObject.CreateInstance<SpeciesDefinition>();
        herbivore2.id = "test_herbivore2";
        herbivore2.category = SpeciesDefinition.SpeciesCategory.Herbivore;

        system.AddUnlockedSpecies(herbivore);
        system.AddUnlockedSpecies(herbivore2);

        system.CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore);
        Assert.AreEqual(herbivore2, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));

        system.CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore);
        Assert.AreEqual(herbivore, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));

        Object.DestroyImmediate(herbivore2);
    }

    [Test]
    public void CycleReleasableSpecies_PredatorIndexIndependentOfHerbivoreIndex()
    {
        SpeciesDefinition predator2 = ScriptableObject.CreateInstance<SpeciesDefinition>();
        predator2.id = "test_predator2";
        predator2.category = SpeciesDefinition.SpeciesCategory.Predator;

        system.AddUnlockedSpecies(herbivore);
        system.AddUnlockedSpecies(predator);
        system.AddUnlockedSpecies(predator2);

        system.CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory.Predator);

        Assert.AreEqual(herbivore, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));
        Assert.AreEqual(predator2, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Predator));

        Object.DestroyImmediate(predator2);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Use `mcp__UnityMCP__run_tests` (EditMode, filter `EmergenceSystemTests`). Expected: FAIL — `TryReleaseSpeciesAt`, `CurrentReleasableSpecies`, `CycleReleasableSpecies` don't exist yet (compile error).

- [ ] **Step 3: Implement the refactor + new methods**

In `Assets/Scripts/Ecosystem/EmergenceSystem.cs`:

Add two new fields next to the existing ones (after `creatureWanderRadius`):

```csharp
    [SerializeField] private float creatureWanderRadius = 2f;
    [SerializeField] private int currentHerbivoreIndex;
    [SerializeField] private int currentPredatorIndex;
```

Replace `TryReleaseSpecies` and add the new methods right after it:

```csharp
    // Player-triggered release: succeeds or fails immediately based on CanRelease, with the same
    // feedback tree-planting already gives (sound + VFX + XP). The VFX burst comes from
    // SpawnIndividual's call into SpawnCreatureVisual (playSpawnEffects defaults to true) - don't
    // add a second one here.
    public bool TryReleaseSpecies(SpeciesDefinition species)
    {
        if (!CanRelease(species))
        {
            return false;
        }

        SpawnIndividual(species, GetRandomTreePosition(PlantedTree.ActiveTrees));
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased);
        return true;
    }

    // Position-aware counterpart of TryReleaseSpecies for the HUD's tap-to-place picker: releases
    // at the tapped world position instead of a random tree, with the same land/river rejection
    // feedback PlantTree gives. Deliberately skips the Lab-exclusion-zone and tree-spacing checks
    // PlantTree has - neither applies to animals.
    public bool TryReleaseSpeciesAt(SpeciesDefinition species, Vector3 position)
    {
        if (species == null)
        {
            return false;
        }

        if (LandManager.Instance != null && !LandManager.Instance.IsWithinOwnedLand(position))
        {
            Debug.Log("Can't release here — outside your owned land.");
            AudioManager.Instance?.PlayError();
            HudUI.Instance?.ShowLandLockedPopup();
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsBlocked(position))
        {
            Debug.Log("Can't release here — that's the river.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        if (RiverManager.Instance != null && RiverManager.Instance.IsWithinSandBank(position))
        {
            Debug.Log("Can't release here — that's the sand bank.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        if (!CanRelease(species))
        {
            Debug.Log($"Can't release {species.displayName} yet — criteria not met.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        SpawnIndividual(species, position);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased);
        return true;
    }

    // Mirrors TreePlanter.CurrentVegetation/CycleVegetation for animal species, split per category
    // (Predator vs. everything else) so switching category and back restores the prior pick there,
    // rather than always resetting to index 0. Any non-Predator category maps to the non-predator
    // list, matching AddUnlockedSpecies's existing category split.
    public SpeciesDefinition CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory category)
    {
        List<SpeciesDefinition> list = ListForCategory(category);
        if (list.Count == 0)
        {
            return null;
        }

        int index = IndexForCategory(category);
        return list[index % list.Count];
    }

    public void CycleReleasableSpecies(SpeciesDefinition.SpeciesCategory category)
    {
        List<SpeciesDefinition> list = ListForCategory(category);
        if (list.Count == 0)
        {
            return;
        }

        if (category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            currentPredatorIndex = (currentPredatorIndex + 1) % list.Count;
        }
        else
        {
            currentHerbivoreIndex = (currentHerbivoreIndex + 1) % list.Count;
        }
    }

    private List<SpeciesDefinition> ListForCategory(SpeciesDefinition.SpeciesCategory category)
    {
        return category == SpeciesDefinition.SpeciesCategory.Predator
            ? unlockedPredatorSpecies
            : unlockedNonPredatorSpecies;
    }

    private int IndexForCategory(SpeciesDefinition.SpeciesCategory category)
    {
        return category == SpeciesDefinition.SpeciesCategory.Predator
            ? currentPredatorIndex
            : currentHerbivoreIndex;
    }
```

Replace `SpawnIndividual` and `SpawnCreatureVisual` (change their signatures from `IReadOnlyList<PlantedTree> availableTrees` to `Vector3 position`):

```csharp
    private void SpawnIndividual(SpeciesDefinition species, Vector3 position)
    {
        EcosystemState.AddSpeciesIndividual(species.id);
        int newPopulation = EcosystemState.GetSpeciesPopulation(species.id);

        SpeciesRuntimeState runtimeState = FindRuntimeState(species);
        if (runtimeState == null)
        {
            runtimeState = new SpeciesRuntimeState { species = species };
            populations.Add(runtimeState);
        }

        runtimeState.population = newPopulation;

        Debug.Log($"{species.displayName} emerged! Population is now {newPopulation}.");

        SpawnCreatureVisual(species, position);
    }
```

```csharp
    private void SpawnCreatureVisual(SpeciesDefinition species, Vector3 spawnPosition, bool playSpawnEffects = true)
    {
        GameObject go = new GameObject($"Creature_{species.id}");
        go.transform.position = spawnPosition;

        Sprite[] frames = GetCreatureFrames(species);

        SpriteRenderer spriteRenderer = go.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingOrder = 1;

        float scale = GetSpeciesScale(species);
        go.transform.localScale = new Vector3(scale, scale, 1f);

        CreatureVisual visual = go.AddComponent<CreatureVisual>();
        visual.Initialize(species, spawnPosition, creatureWanderRadius, frames);

        if (playSpawnEffects)
        {
            VfxUtility.SpawnBurst(spawnPosition, GetSpeciesColor(species), 5, 1.1f, 0.35f);
        }
    }
```

Update `SpawnMissingForList` to resolve the position itself per individual (it previously relied on `SpawnCreatureVisual` doing this internally):

```csharp
    private void SpawnMissingForList(List<SpeciesDefinition> speciesList, IReadOnlyList<PlantedTree> availableTrees)
    {
        foreach (SpeciesDefinition species in speciesList)
        {
            if (species == null)
            {
                continue;
            }

            int targetPopulation = EcosystemState.GetSpeciesPopulation(species.id);
            int existingVisualCount = CountActiveCreatures(species.id);

            for (int i = existingVisualCount; i < targetPopulation; i++)
            {
                SpawnCreatureVisual(species, GetRandomTreePosition(availableTrees), playSpawnEffects: false);
            }
        }
    }
```

`GetRandomTreePosition` itself is unchanged (still private static, still takes `IReadOnlyList<PlantedTree>`) — only its callers changed, from being called inside `SpawnCreatureVisual` to being called by `TryReleaseSpecies` and `SpawnMissingForList` before they call the (now position-taking) spawn methods.

- [ ] **Step 4: Run tests to verify they pass**

Use `mcp__UnityMCP__run_tests` (EditMode, filter `EmergenceSystemTests`). Expected: PASS, all tests including the 8 new ones and every pre-existing test (confirms the refactor didn't change `TryReleaseSpecies`'/`SpawnMissingCreatureVisuals`' observable behavior).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Ecosystem/EmergenceSystem.cs Assets/Scripts/Tests/EmergenceSystemTests.cs
git commit -m "feat: add position-aware animal release and per-category species selection to EmergenceSystem"
```

---

### Task 2: HUD picker rename (`vegetation-*` → `picker-*`) in UXML/USS

**Files:**
- Modify: `Assets/UI/Hud/Hud.uxml`
- Modify: `Assets/UI/Hud/Hud.uss`

**Interfaces:**
- Consumes: nothing (markup/style only).
- Produces: element names `picker-cluster`/`picker-button`/`picker-label` and USS classes `.picker-cluster`/`.picker-label`/`.picker-icon-button`, queried by `HudUI.cs` in Task 3. The label element changes from `ui:Label` to `ui:Button` (needed for it to be clickable in Task 3) — note it therefore loses `picking-mode="Ignore"`.

This is markup/style only — no automated test applies. Verified visually in Task 3's manual verification step (a compile-time `root.Q<Button>("picker-label")` failure in Task 3 would also catch a name mismatch immediately).

- [ ] **Step 1: Rename the UXML block**

In `Assets/UI/Hud/Hud.uxml`, replace:

```xml
            <ui:VisualElement name="vegetation-cluster" class="vegetation-cluster">
                <ui:Button name="vegetation-button" class="icon-button vegetation-icon-button" />
                <ui:Label name="vegetation-label" text="Oak" class="vegetation-label" picking-mode="Ignore" />
            </ui:VisualElement>
```

with:

```xml
            <ui:VisualElement name="picker-cluster" class="picker-cluster">
                <ui:Button name="picker-button" class="icon-button picker-icon-button" />
                <ui:Button name="picker-label" text="Oak" class="picker-label" />
            </ui:VisualElement>
```

- [ ] **Step 2: Rename the USS selectors**

In `Assets/UI/Hud/Hud.uss`, replace:

```css
.vegetation-cluster {
    height: 32px;
    margin-left: var(--space-sm);
    flex-direction: row;
    align-items: center;
}
```

with:

```css
.picker-cluster {
    height: 32px;
    margin-left: var(--space-sm);
    flex-direction: row;
    align-items: center;
}
```

Replace:

```css
.vegetation-label {
    margin-right: var(--space-xs);
    padding: 3px var(--space-sm);
    background-color: rgba(0, 0, 0, 0.2);
    -unity-background-scale-mode: stretch-to-fill;
    border-radius: var(--radius-sm);
    border-width: 1px;
    border-color: var(--color-gold-light);
    color: var(--text-on-dark);
    font-size: var(--font-size-xs);
    -unity-font-style: bold;
}

/* Overrides .icon-button's pill radius for this one icon specifically - the other buttons sharing
   that class (lab/codex/profile/settings/demo) are unaffected, since they weren't part of this
   "make the top-left cluster rectangular" request. */
.vegetation-icon-button {
    border-radius: var(--radius-sm);
    border-width: var(--border-thin);
    border-color: var(--color-gold-light);
}
```

with:

```css
.picker-label {
    margin-right: var(--space-xs);
    padding: 3px var(--space-sm);
    background-color: rgba(0, 0, 0, 0.2);
    -unity-background-scale-mode: stretch-to-fill;
    border-radius: var(--radius-sm);
    border-width: 1px;
    border-color: var(--color-gold-light);
    color: var(--text-on-dark);
    font-size: var(--font-size-xs);
    -unity-font-style: bold;
}

/* Overrides .icon-button's pill radius for this one icon specifically - the other buttons sharing
   that class (lab/codex/profile/settings/demo) are unaffected, since they weren't part of this
   "make the top-left cluster rectangular" request. */
.picker-icon-button {
    border-radius: var(--radius-sm);
    border-width: var(--border-thin);
    border-color: var(--color-gold-light);
}
```

- [ ] **Step 3: Commit**

```bash
git add Assets/UI/Hud/Hud.uxml Assets/UI/Hud/Hud.uss
git commit -m "refactor: rename HUD vegetation picker markup/styles to generic picker-* names"
```

---

### Task 3: `HudUI.cs` category/species picker logic

**Files:**
- Modify: `Assets/Scripts/UI/HudUI.cs`

**Interfaces:**
- Consumes: `Assets/UI/Hud/Hud.uxml`'s `picker-cluster`/`picker-button`/`picker-label` (Task 2); `TreePlanter.CurrentVegetation`/`CycleVegetation()` (existing, unchanged); `EmergenceSystem.CurrentReleasableSpecies(category)`/`CycleReleasableSpecies(category)` (Task 1).
- Produces: `public enum HudUI.PickerCategory { Plant, Herbivore, Predator }` and a way for `CameraPanController` (Task 4) to read the active category and current species — exposed as `public PickerCategory CurrentPickerCategory => currentPickerCategory;` and `public SpeciesDefinition CurrentPickerSpecies()`.

- [ ] **Step 1: Add the `emergenceSystem` field, sprite fields, and `PickerCategory` enum**

In `Assets/Scripts/UI/HudUI.cs`, add the enum right after the class declaration line (`public class HudUI : MonoBehaviour {`):

```csharp
[RequireComponent(typeof(UIDocument))]
public class HudUI : MonoBehaviour
{
    public enum PickerCategory
    {
        Plant,
        Herbivore,
        Predator
    }

    private const int IconSize = 64;
```

Add `emergenceSystem` next to `treePlanter` in the serialized fields:

```csharp
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private EmergenceSystem emergenceSystem;
```

Replace the single `vegetationIconSprite` field with three category icon fields:

```csharp
    // fal.ai-rendered token icons (see docs/art-design-document.md's "HUD token icon" exception) -
    // static single-purpose glyphs that never need runtime recoloring, unlike most HUD chrome.
    [SerializeField] private Sprite currencyIconSprite;
    [SerializeField] private Sprite xpIconSprite;
    [SerializeField] private Sprite plantIconSprite;
    [SerializeField] private Sprite herbivoreIconSprite;
    [SerializeField] private Sprite predatorIconSprite;
```

Replace the `vegetationButton`/`vegetationLabel` private fields with:

```csharp
    private Button pickerButton;
    private Button pickerLabel;
    private PickerCategory currentPickerCategory = PickerCategory.Plant;
```

- [ ] **Step 2: Add the public accessors `CameraPanController` needs**

Add near the top of the class, after `public static HudUI Instance { get; private set; }`:

```csharp
    public PickerCategory CurrentPickerCategory => currentPickerCategory;
```

Add as a new public method (placed after `RefreshPickerLabel`, defined in Step 4 below):

```csharp
    // Resolves whatever the picker is currently offering for the active category, so
    // CameraPanController's tap handler doesn't need to know about TreePlanter/EmergenceSystem
    // internals - Plant has no "species" (VegetationDefinition isn't a SpeciesDefinition), so this
    // only ever returns non-null for Herbivore/Predator.
    public SpeciesDefinition CurrentPickerSpecies()
    {
        if (currentPickerCategory == PickerCategory.Plant || emergenceSystem == null)
        {
            return null;
        }

        SpeciesDefinition.SpeciesCategory category = currentPickerCategory == PickerCategory.Predator
            ? SpeciesDefinition.SpeciesCategory.Predator
            : SpeciesDefinition.SpeciesCategory.Herbivore;

        return emergenceSystem.CurrentReleasableSpecies(category);
    }
```

- [ ] **Step 3: Rewire `OnEnable`'s picker section**

Replace:

```csharp
        vegetationButton = root.Q<Button>("vegetation-button");
        vegetationButton.clicked += OnVegetationButtonClicked;
        vegetationButton.style.backgroundImage = new StyleBackground(vegetationIconSprite);
```

with:

```csharp
        pickerButton = root.Q<Button>("picker-button");
        pickerButton.clicked += OnPickerButtonClicked;
```

Replace:

```csharp
        vegetationLabel = root.Q<Label>("vegetation-label");
        // Flat rgba fills read as "1-dimensional" — layer a subtle gradient+grain texture behind
        // the existing translucent tint instead (stretch-to-fill in USS handles the elements'
        // content-driven widths; the effect is subtle enough that mild horizontal stretch doesn't
        // read as distorted).
        vegetationLabel.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateTexturedPillBackground(96, 24, new Color(0.08f, 0.08f, 0.08f, 0.2f)));
```

with:

```csharp
        pickerLabel = root.Q<Button>("picker-label");
        pickerLabel.clicked += OnPickerLabelClicked;
        // Flat rgba fills read as "1-dimensional" — layer a subtle gradient+grain texture behind
        // the existing translucent tint instead (stretch-to-fill in USS handles the elements'
        // content-driven widths; the effect is subtle enough that mild horizontal stretch doesn't
        // read as distorted).
        pickerLabel.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateTexturedPillBackground(96, 24, new Color(0.08f, 0.08f, 0.08f, 0.2f)));
```

Replace the `RefreshVegetationLabel();` call at the end of `OnEnable` with:

```csharp
        RefreshPickerIcon();
        RefreshPickerLabel();
```

- [ ] **Step 4: Replace `OnVegetationButtonClicked`/`RefreshVegetationLabel` with the category/species picker logic**

Replace:

```csharp
    private void OnVegetationButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (treePlanter == null)
        {
            return;
        }

        treePlanter.CycleVegetation();
        RefreshVegetationLabel();
    }

    private void RefreshVegetationLabel()
    {
        if (vegetationLabel == null || treePlanter == null || treePlanter.CurrentVegetation == null)
        {
            return;
        }

        vegetationLabel.text = treePlanter.CurrentVegetation.displayName;
    }
```

with:

```csharp
    private void OnPickerButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        currentPickerCategory = currentPickerCategory switch
        {
            PickerCategory.Plant => PickerCategory.Herbivore,
            PickerCategory.Herbivore => PickerCategory.Predator,
            _ => PickerCategory.Plant
        };

        RefreshPickerIcon();
        RefreshPickerLabel();
    }

    private void OnPickerLabelClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (currentPickerCategory == PickerCategory.Plant)
        {
            treePlanter?.CycleVegetation();
        }
        else if (emergenceSystem != null)
        {
            SpeciesDefinition.SpeciesCategory category = currentPickerCategory == PickerCategory.Predator
                ? SpeciesDefinition.SpeciesCategory.Predator
                : SpeciesDefinition.SpeciesCategory.Herbivore;
            emergenceSystem.CycleReleasableSpecies(category);
        }

        RefreshPickerLabel();
    }

    private void RefreshPickerIcon()
    {
        if (pickerButton == null)
        {
            return;
        }

        Sprite icon = currentPickerCategory switch
        {
            PickerCategory.Herbivore => herbivoreIconSprite,
            PickerCategory.Predator => predatorIconSprite,
            _ => plantIconSprite
        };

        pickerButton.style.backgroundImage = new StyleBackground(icon);
    }

    private void RefreshPickerLabel()
    {
        if (pickerLabel == null)
        {
            return;
        }

        if (currentPickerCategory == PickerCategory.Plant)
        {
            if (treePlanter != null && treePlanter.CurrentVegetation != null)
            {
                pickerLabel.text = treePlanter.CurrentVegetation.displayName;
            }

            return;
        }

        SpeciesDefinition species = CurrentPickerSpecies();
        if (species != null)
        {
            pickerLabel.text = species.displayName;
        }
    }
```

- [ ] **Step 2 (compile check): Validate the script**

Use `mcp__UnityMCP__validate_script` on `Assets/Scripts/UI/HudUI.cs`. Expected: no errors. (`switch` expressions require C# 8+ — confirm the project's C# language version supports this by checking that no error is raised; Unity 6000.5.6f1's default Roslyn targets C# 9, so this is expected to compile cleanly.)

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/UI/HudUI.cs
git commit -m "feat: turn HUD vegetation picker into a Plant/Herbivore/Predator category picker"
```

---

### Task 4: `CameraPanController` tap-routing

**Files:**
- Modify: `Assets/Scripts/Vegetation/CameraPanController.cs`

**Interfaces:**
- Consumes: `HudUI.Instance.CurrentPickerCategory` (`HudUI.PickerCategory`), `HudUI.Instance.CurrentPickerSpecies()` (Task 3); `EmergenceSystem.TryReleaseSpeciesAt(SpeciesDefinition, Vector3)` (Task 1); `TreePlanter.PlantTree(Vector3)` (existing, unchanged).
- Produces: nothing new consumed elsewhere — this is the terminal tap-routing behavior.

- [ ] **Step 1: Add the `emergenceSystem` field**

In `Assets/Scripts/Vegetation/CameraPanController.cs`, add next to `treePlanter`:

```csharp
    [SerializeField] private Camera targetCamera;
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private EmergenceSystem emergenceSystem;
```

- [ ] **Step 2: Branch the empty-ground tap on the active picker category**

Replace:

```csharp
                        PlantedTree tappedTree = FindTreeNear(worldPoint);
                        if (tappedTree != null)
                        {
                            treeBoostUI?.Open(tappedTree);
                        }
                        else if (treePlanter != null)
                        {
                            treePlanter.PlantTree(worldPoint);
                        }
```

with:

```csharp
                        PlantedTree tappedTree = FindTreeNear(worldPoint);
                        if (tappedTree != null)
                        {
                            treeBoostUI?.Open(tappedTree);
                        }
                        else
                        {
                            HandleEmptyGroundTap(worldPoint);
                        }
```

Add the new private method right after `EndPress` (before `ResolveMoveTreeTap`):

```csharp
    // Routes an empty-ground tap based on the HUD's active picker category: Plant places a tree
    // (unchanged behavior); Herbivore/Predator releases whatever species the picker is currently
    // offering for that category at the tapped position.
    private void HandleEmptyGroundTap(Vector3 worldPoint)
    {
        HudUI.PickerCategory category = HudUI.Instance != null
            ? HudUI.Instance.CurrentPickerCategory
            : HudUI.PickerCategory.Plant;

        if (category == HudUI.PickerCategory.Plant)
        {
            treePlanter?.PlantTree(worldPoint);
            return;
        }

        SpeciesDefinition species = HudUI.Instance?.CurrentPickerSpecies();
        if (species != null)
        {
            emergenceSystem?.TryReleaseSpeciesAt(species, worldPoint);
        }
    }
```

- [ ] **Step 3: Validate the script**

Use `mcp__UnityMCP__validate_script` on `Assets/Scripts/Vegetation/CameraPanController.cs`. Expected: no errors.

- [ ] **Step 4: Commit**

```bash
git add Assets/Scripts/Vegetation/CameraPanController.cs
git commit -m "feat: route empty-ground taps to plant or release based on the HUD's active picker category"
```

---

### Task 5: Scene wiring — sprite fields and `emergenceSystem` references

**Files:**
- Modify (scene data, via `execute_code`): `Assets/Scenes/SampleScene.unity`
- Modify: `Assets/Scripts/Tests/SceneRosterWiringTests.cs`

**Interfaces:**
- Consumes: `HudUI.plantIconSprite`/`herbivoreIconSprite`/`predatorIconSprite` (Task 3, private `[SerializeField]` — set via `SerializedObject`, not code); `CameraPanController.emergenceSystem` (Task 4, same); the three icon assets at `Assets/Textures/Hud/VegetationIcon.png`, `Assets/Textures/Hud/HerbivoreIcon.png`, `Assets/Textures/Hud/PredatorIcon.png`.
- Produces: nothing consumed by later tasks — this is the last task.

- [ ] **Step 1: Wire the fields in the Editor**

Use `mcp__UnityMCP__execute_code` with a script that: opens `Assets/Scenes/SampleScene.unity` if not already open, finds the `HudUI` and `CameraPanController` components via `Object.FindFirstObjectByType`, loads the three sprites via `AssetDatabase.LoadAssetAtPath<Sprite>(...)`, and assigns them through `SerializedObject`/`SerializedProperty` (matching how `currencyIconSprite`/`xpIconSprite` were wired for this same `HudUI` component in an earlier stage — see `plantIconSprite`, `herbivoreIconSprite`, `predatorIconSprite` fields added in Task 3, and the `emergenceSystem` field added on `CameraPanController` in Task 4). Concretely:

```csharp
using UnityEditor;
using UnityEngine;

HudUI hud = Object.FindFirstObjectByType<HudUI>(FindObjectsInactive.Include);
CameraPanController cameraPanController = Object.FindFirstObjectByType<CameraPanController>(FindObjectsInactive.Include);
EmergenceSystem emergenceSystem = Object.FindFirstObjectByType<EmergenceSystem>(FindObjectsInactive.Include);

Sprite plantIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Hud/VegetationIcon.png");
Sprite herbivoreIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Hud/HerbivoreIcon.png");
Sprite predatorIcon = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/Hud/PredatorIcon.png");

SerializedObject hudSerialized = new SerializedObject(hud);
hudSerialized.FindProperty("plantIconSprite").objectReferenceValue = plantIcon;
hudSerialized.FindProperty("herbivoreIconSprite").objectReferenceValue = herbivoreIcon;
hudSerialized.FindProperty("predatorIconSprite").objectReferenceValue = predatorIcon;
hudSerialized.FindProperty("emergenceSystem").objectReferenceValue = emergenceSystem;
hudSerialized.ApplyModifiedProperties();

SerializedObject cameraPanSerialized = new SerializedObject(cameraPanController);
cameraPanSerialized.FindProperty("emergenceSystem").objectReferenceValue = emergenceSystem;
cameraPanSerialized.ApplyModifiedProperties();

EditorSceneManager.MarkSceneDirty(hud.gameObject.scene);
EditorSceneManager.SaveScene(hud.gameObject.scene);

return "wired";
```

(`EditorSceneManager` needs `using UnityEditor.SceneManagement;` — add it to the script's usings.) Confirm the returned result is `"wired"` with no error.

- [ ] **Step 2: Write the failing scene-wiring regression test**

Add to `Assets/Scripts/Tests/SceneRosterWiringTests.cs`, after the existing tests (before the closing brace of the class):

```csharp
    [Test]
    public void CameraPanController_EmergenceSystem_IsWired()
    {
        CameraPanController cameraPanController = FindComponent<CameraPanController>();
        Assert.IsNotNull(cameraPanController);
        Assert.IsNotNull(GetPrivateField(cameraPanController, "emergenceSystem"));
    }

    [Test]
    public void HudUI_PickerIconSprites_AreWired()
    {
        HudUI hud = FindComponent<HudUI>();
        Assert.IsNotNull(hud);
        Assert.IsNotNull(GetPrivateField(hud, "plantIconSprite"));
        Assert.IsNotNull(GetPrivateField(hud, "herbivoreIconSprite"));
        Assert.IsNotNull(GetPrivateField(hud, "predatorIconSprite"));
    }
```

- [ ] **Step 3: Run tests to verify they pass**

Use `mcp__UnityMCP__run_tests` (EditMode, filter `SceneRosterWiringTests`). Expected: PASS for the two new tests (and every pre-existing test in this file, confirming Step 1's scene edit didn't disturb the roster wiring these tests already cover).

- [ ] **Step 4: Commit**

```bash
git add Assets/Scenes/SampleScene.unity Assets/Scripts/Tests/SceneRosterWiringTests.cs
git commit -m "chore: wire HUD picker icon sprites and EmergenceSystem references in the scene"
```

---

### Task 6: Manual Play-mode verification

**Files:** none (manual verification only, no code changes).

**Interfaces:**
- Consumes: the complete feature from Tasks 1-5.
- Produces: nothing — this is the final acceptance check before the plan is considered complete.

- [ ] **Step 1: Enter Play mode and verify category cycling**

Use `mcp__UnityMCP__manage_editor` to enter Play mode. Tap the picker icon button three times; confirm the icon swaps Plant → Herbivore → Predator → Plant and the label text updates to a plausible species/vegetation name at each step (read the console/UI state via `mcp__UnityMCP__read_console` and/or a screenshot if available).

- [ ] **Step 2: Verify per-category species memory**

With Herbivore active, tap the label to cycle to the second herbivore species (if the unlocked roster has more than one — otherwise skip, since Task 1's tests already cover the wrap-around logic directly). Switch to Predator, then back to Herbivore. Confirm the label still shows the previously-selected herbivore species, not the first one.

- [ ] **Step 3: Verify tap-to-plant (Plant category, unchanged)**

With Plant active, tap an empty, owned-land world position. Confirm a tree plants exactly as it did before this stage.

- [ ] **Step 4: Verify tap-to-release (Herbivore/Predator)**

With Herbivore active and its release criteria met (use the Demo panel's "Unlock All Species"/"Mature All Trees" buttons if needed to satisfy criteria), tap an empty, owned-land world position. Confirm the selected animal appears exactly at the tapped spot. Repeat for Predator.

- [ ] **Step 5: Verify rejection feedback**

With an animal category active, tap outside owned land — confirm the land-locked popup appears (same as tree planting). Tap inside the river — confirm the same error-sound rejection tree planting gives, with no animal spawned.

- [ ] **Step 6: Exit Play mode**

Use `mcp__UnityMCP__manage_editor` to exit Play mode. No commit for this task (verification only).

---

## Self-Review Notes

- **Spec coverage:** every "In" scope item from `docs/superpowers/specs/2026-08-14-hud-release-picker-design.md` maps to a task — icon cycling (Task 3), label cycling with per-category memory (Task 1 + Task 3), `TryReleaseSpeciesAt` (Task 1), tap routing (Task 4), three new icons (already generated; wired in Task 5), `vegetation-*` → `picker-*` rename (Task 2 + Task 3).
- **Placeholder scan:** no TBD/TODO; every step has complete code, not a description.
- **Type consistency:** `TryReleaseSpeciesAt(SpeciesDefinition, Vector3)` (Task 1) matches its Task 4 call site exactly; `HudUI.PickerCategory`/`CurrentPickerCategory`/`CurrentPickerSpecies()` (Task 3) match their Task 4 usage exactly; `EmergenceSystem.CurrentReleasableSpecies`/`CycleReleasableSpecies` (Task 1) match their Task 3 usage exactly.
