# Species Roster Cleanup & Tabbed Laboratory Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Cut the species/vegetation roster down to Oak+Pine (plants), Gazelle+Squirrel (herbivores), Wolf+Fox (predators); delete everything else; and give the Laboratory panel three tabs (Plants/Herbivores/Predators) so each category's tiny remaining tech tree is presented on its own.

**Architecture:** Pure data/content deletion (species/vegetation/research `.asset` files), one new species (Squirrel, created as a real `SpeciesDefinition` + `ResearchProjectDefinition` + a new procedural-sprite trait case), a scene-wiring pass updating every component that holds its own "master list" of species/vegetation, and a small UI addition (three tab buttons that filter which tech-tree nodes `LaboratoryUI` builds).

**Tech Stack:** Unity 6000.5.6f1, C# (NUnit EditMode tests via `mcp__UnityMCP__run_tests`), UI Toolkit (UXML/USS), UnityMCP `execute_code`/`manage_asset` tools for asset creation/deletion and scene-serialized-field edits.

## Global Constraints

- Final roster is exactly: Oak (vegetation, free), Pine (vegetation, researchable), Gazelle (herbivore, free), Squirrel (herbivore, researchable), Wolf (predator, free), Fox (predator, researchable). Nothing else survives.
- Wolf's prey → `gazelle`. Fox's prey → `squirrel`. Pine's and Fox's research have no prerequisites.
- Delete asset files via `git rm` (removes the `.meta` companion in the same commit, keeps history clean) — never leave orphaned `.meta` files.
- New `SpeciesDefinition`/`ResearchProjectDefinition` assets are created via `AssetDatabase.CreateAsset` (through `execute_code`), not hand-authored YAML — Unity generates a correct, unique GUID this way.
- `mcp__UnityMCP__execute_code` compiles with the CodeDom backend (C# 6) in this project — **no local function declarations** (`void Foo() { }` inside a method body fails to compile). Use a lambda assigned to a `System.Action<...>`/`System.Func<...>` variable instead when a reusable helper is needed within one script.
- After any C# script change, force a Unity recompile and confirm zero console errors before proceeding (`mcp__UnityMCP__refresh_unity` with `compile: request`, then `mcp__UnityMCP__read_console` filtered to `error`).
- Run the full EditMode suite (`mcp__UnityMCP__run_tests`, `mode: EditMode`) after each task. `EmergenceSystemTests` has 4 pre-existing failures unrelated to this work (creature-visual spawning, from before this plan) — do not treat those as regressions, but treat any *new* failure as one.

---

### Task 1: Delete unused species/vegetation/research assets; fix Wolf/Fox/Pine references

**Files:**
- Delete: 13 files in `Assets/Data/Species/` + `.meta` pairs
- Delete: 9 files in `Assets/Data/Vegetation/` + `.meta` pairs
- Delete: 21 files in `Assets/Data/Research/` + `.meta` pairs
- Modify: `Assets/Data/Species/Wolf.asset`, `Assets/Data/Species/Fox.asset`, `Assets/Data/Research/PineResearch.asset`, `Assets/Data/Research/FoxResearch.asset`
- Create: `Assets/Scripts/Tests/SpeciesRosterTests.cs`

**Interfaces:**
- Produces: the fact that `Assets/Data/Species/{Gazelle,Wolf,Fox}.asset` and `Assets/Data/Vegetation/{Oak,Pine}.asset` are the only survivors — Task 2 (Squirrel creation) and Task 3 (scene wiring) depend on this being done first.

- [ ] **Step 1: Write the failing test**

Create `Assets/Scripts/Tests/SpeciesRosterTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;

public class SpeciesRosterTests
{
    [Test]
    public void SurvivingSpecies_StillExist()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Gazelle.asset"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Wolf.asset"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Fox.asset"));
    }

    [Test]
    public void SurvivingVegetation_StillExist()
    {
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Oak.asset"));
        Assert.IsNotNull(AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Pine.asset"));
    }

    [Test]
    public void DeletedSpecies_NoLongerExist()
    {
        string[] deletedIds = { "Antelope", "Boar", "Buffalo", "Deer", "Eagle", "Elephant", "Giraffe", "Goat", "Hare", "Lion", "Moose", "Rabbit", "Zebra" };
        foreach (string id in deletedIds)
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<SpeciesDefinition>($"Assets/Data/Species/{id}.asset"), $"{id}.asset should have been deleted");
        }
    }

    [Test]
    public void DeletedVegetation_NoLongerExist()
    {
        string[] deletedIds = { "Bamboo", "BerryBush", "Birch", "Fern", "Mangrove", "Maple", "Reed", "Wildflower", "Willow" };
        foreach (string id in deletedIds)
        {
            Assert.IsNull(AssetDatabase.LoadAssetAtPath<VegetationDefinition>($"Assets/Data/Vegetation/{id}.asset"), $"{id}.asset should have been deleted");
        }
    }

    [Test]
    public void Wolf_PreysOnGazelle()
    {
        SpeciesDefinition wolf = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Wolf.asset");
        CollectionAssert.AreEqual(new[] { "gazelle" }, wolf.preySpeciesIds);
    }

    [Test]
    public void Fox_PreysOnSquirrel()
    {
        SpeciesDefinition fox = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Fox.asset");
        CollectionAssert.AreEqual(new[] { "squirrel" }, fox.preySpeciesIds);
    }

    [Test]
    public void PineResearch_HasNoPrerequisite()
    {
        ResearchProjectDefinition pineResearch = AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/PineResearch.asset");
        Assert.IsEmpty(pineResearch.prerequisiteProjectIds);
    }

    [Test]
    public void FoxResearch_HasNoPrerequisite()
    {
        ResearchProjectDefinition foxResearch = AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/FoxResearch.asset");
        Assert.IsEmpty(foxResearch.prerequisiteProjectIds);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_names: ["SpeciesRosterTests"]`, `include_failed_tests: true`.
Expected: `DeletedSpecies_NoLongerExist`, `DeletedVegetation_NoLongerExist`, `Wolf_PreysOnGazelle`, `Fox_PreysOnSquirrel`, `PineResearch_HasNoPrerequisite`, `FoxResearch_HasNoPrerequisite` all FAIL (the assets still exist / still have their old values). `SurvivingSpecies_StillExist` and `SurvivingVegetation_StillExist` PASS already.

- [ ] **Step 3: Delete the unused asset files**

```bash
cd "/Users/thorfinn/Documents/Environment Project/1M trees"

for name in Antelope Boar Buffalo Deer Eagle Elephant Giraffe Goat Hare Lion Moose Rabbit Zebra; do
  git rm "Assets/Data/Species/${name}.asset" "Assets/Data/Species/${name}.asset.meta"
done

for name in Bamboo BerryBush Birch Fern Mangrove Maple Reed Wildflower Willow; do
  git rm "Assets/Data/Vegetation/${name}.asset" "Assets/Data/Vegetation/${name}.asset.meta"
done

for name in Antelope Bamboo BerryBush Birch Boar Buffalo Deer Eagle Elephant Fern Giraffe Goat Hare Lion Mangrove Maple Moose Rabbit Reed Wildflower Willow Zebra; do
  git rm "Assets/Data/Research/${name}Research.asset" "Assets/Data/Research/${name}Research.asset.meta"
done
```

- [ ] **Step 4: Fix Wolf's prey**

Edit `Assets/Data/Species/Wolf.asset` — change:
```yaml
  preySpeciesIds:
  - deer
```
to:
```yaml
  preySpeciesIds:
  - gazelle
```

- [ ] **Step 5: Fix Fox's prey**

Edit `Assets/Data/Species/Fox.asset` — change:
```yaml
  preySpeciesIds:
  - rabbit
  - hare
```
to:
```yaml
  preySpeciesIds:
  - squirrel
```

- [ ] **Step 6: Remove Pine's research prerequisite**

Edit `Assets/Data/Research/PineResearch.asset` — change:
```yaml
  prerequisiteProjectIds:
  - berry_bush_research
```
to:
```yaml
  prerequisiteProjectIds: []
```

- [ ] **Step 7: Remove Fox's research prerequisite**

Edit `Assets/Data/Research/FoxResearch.asset` — change:
```yaml
  prerequisiteProjectIds:
  - rabbit_research
```
to:
```yaml
  prerequisiteProjectIds: []
```

- [ ] **Step 8: Refresh Unity and run tests to verify they pass**

Run `mcp__UnityMCP__refresh_unity` (`mode: force`, `scope: assets`, `compile: none`, `wait_for_ready: true`) so the Editor picks up the deletions, then run `mcp__UnityMCP__run_tests` (`mode: "EditMode"`, `test_names: ["SpeciesRosterTests"]`, `include_failed_tests: true`).
Expected: all 8 tests PASS.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
Trim species/vegetation roster to Oak, Pine, Gazelle, Wolf, Fox

Deletes 13 animal species, 9 vegetation species, and their research
projects, keeping only what the new roster needs. Repoints Wolf's and
Fox's prey at the surviving herbivores and drops Pine's/Fox's research
prerequisites, which pointed at now-deleted species.
EOF
)"
```

---

### Task 2: Create Squirrel species and give it a dedicated procedural appearance

**Files:**
- Create: `Assets/Data/Species/Squirrel.asset`, `Assets/Data/Research/SquirrelResearch.asset`
- Modify: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`
- Modify: `Assets/Scripts/Tests/SpeciesRosterTests.cs`

**Interfaces:**
- Consumes: nothing new from Task 1 beyond the roster being trimmed.
- Produces: `Assets/Data/Species/Squirrel.asset` (id `"squirrel"`, category Herbivore, `unlockedByDefault: false`) and `Assets/Data/Research/SquirrelResearch.asset` (id `"squirrel_research"`, unlocks `"squirrel"`, no prerequisite) — Task 3's scene wiring adds both to the relevant lists.

- [ ] **Step 1: Write the failing test**

Add to `Assets/Scripts/Tests/SpeciesRosterTests.cs` (inside the existing class):

```csharp
    [Test]
    public void Squirrel_ExistsWithCorrectData()
    {
        SpeciesDefinition squirrel = AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Squirrel.asset");
        Assert.IsNotNull(squirrel);
        Assert.AreEqual("squirrel", squirrel.id);
        Assert.AreEqual(SpeciesDefinition.SpeciesCategory.Herbivore, squirrel.category);
        Assert.IsFalse(squirrel.unlockedByDefault);
    }

    [Test]
    public void SquirrelResearch_ExistsWithNoPrerequisite()
    {
        ResearchProjectDefinition squirrelResearch = AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/SquirrelResearch.asset");
        Assert.IsNotNull(squirrelResearch);
        Assert.AreEqual("squirrel", squirrelResearch.unlocksSpeciesId);
        Assert.IsEmpty(squirrelResearch.prerequisiteProjectIds);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_names: ["SpeciesRosterTests"]`, `include_failed_tests: true`.
Expected: `Squirrel_ExistsWithCorrectData` and `SquirrelResearch_ExistsWithNoPrerequisite` FAIL (assets don't exist yet). All Task 1 tests still PASS.

- [ ] **Step 3: Create the Squirrel and SquirrelResearch assets**

Run via `mcp__UnityMCP__execute_code`:

```csharp
SpeciesDefinition squirrel = ScriptableObject.CreateInstance<SpeciesDefinition>();
squirrel.id = "squirrel";
squirrel.displayName = "Squirrel";
squirrel.category = SpeciesDefinition.SpeciesCategory.Herbivore;
squirrel.unlockedByDefault = false;
squirrel.requiredVegetationIds = new string[0];
squirrel.baseEmergenceWeight = 1f;
squirrel.treesPerIndividualCap = 20;
squirrel.preySpeciesIds = new string[0];
squirrel.preyThreshold = 0;
squirrel.preyToPredatorRatio = 0;
squirrel.researchDurationSeconds = 180;
squirrel.documentaryTitle = "Squirrel";
squirrel.documentaryText = "Documentary text coming soon.";
UnityEditor.AssetDatabase.CreateAsset(squirrel, "Assets/Data/Species/Squirrel.asset");

ResearchProjectDefinition squirrelResearch = ScriptableObject.CreateInstance<ResearchProjectDefinition>();
squirrelResearch.id = "squirrel_research";
squirrelResearch.unlocksSpeciesId = "squirrel";
squirrelResearch.unlocksVegetationId = "";
squirrelResearch.prerequisiteProjectIds = new string[0];
squirrelResearch.baseDurationSeconds = 180;
squirrelResearch.gemSkipCostPerRemainingMinute = 2;
UnityEditor.AssetDatabase.CreateAsset(squirrelResearch, "Assets/Data/Research/SquirrelResearch.asset");

UnityEditor.AssetDatabase.SaveAssets();
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_names: ["SpeciesRosterTests"]`, `include_failed_tests: true`.
Expected: all tests PASS.

- [ ] **Step 5: Give the squirrel its own procedural appearance and remove dead species cases**

In `Assets/Scripts/Ecosystem/EmergenceSystem.cs`:

Replace the `GetSpeciesColor` switch body (currently `case "gazelle"` through `case "eagle"` plus `default`) with:

```csharp
        switch (species.id)
        {
            case "gazelle":
                return new Color(0.82f, 0.62f, 0.35f);
            case "wolf":
                return new Color(0.55f, 0.56f, 0.58f);
            case "fox":
                return new Color(0.78f, 0.4f, 0.2f);
            case "squirrel":
                return new Color(0.62f, 0.38f, 0.2f);
            default:
                return GetCategoryColor(species.category);
        }
```

Replace the `GetSpeciesScale` switch body (currently `case "elephant"` through `case "fox"` plus `default`) with:

```csharp
        switch (species.id)
        {
            case "fox":
                return baseScale * 0.85f;
            case "squirrel":
                return baseScale * 0.6f;
            default:
                return baseScale;
        }
```

Replace the `GetQuadrupedTraits` switch body (currently `case "gazelle"` through `case "fox"` plus `default`) with:

```csharp
        switch (species.id)
        {
            case "gazelle":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.ThinHorns, tailStyle = PixelArtUtility.AnimalTailStyle.Nub };
            case "wolf":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Bushy, hasFangs = true };
            case "fox":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Pointed, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Bushy, hasFangs = true };
            case "squirrel":
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Bushy };
            default:
                return new PixelArtUtility.QuadrupedTraits { bodyColor = color, earStyle = PixelArtUtility.AnimalEarStyle.Round, ornamentStyle = PixelArtUtility.AnimalOrnamentStyle.None, tailStyle = PixelArtUtility.AnimalTailStyle.Nub, hasFangs = species.category == SpeciesDefinition.SpeciesCategory.Predator };
        }
```

Remove the now-dead eagle branch from `BuildCreatureFrames` — delete this block entirely (the method should go straight to building `PixelArtUtility.QuadrupedTraits traits = GetQuadrupedTraits(species);`):

```csharp
        if (species.id == "eagle")
        {
            Color birdColor = GetSpeciesColor(species);
            return new[]
            {
                PixelArtUtility.CreateBirdSprite(64, birdColor, 0),
                PixelArtUtility.CreateBirdSprite(64, birdColor, 1)
            };
        }

```

- [ ] **Step 6: Refresh Unity and confirm it compiles with no errors**

Run `mcp__UnityMCP__refresh_unity` (`mode: force`, `scope: scripts`, `compile: request`, `wait_for_ready: true`), then `mcp__UnityMCP__read_console` filtered to `error`.
Expected: zero errors.

- [ ] **Step 7: Run the full EditMode suite**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `include_failed_tests: true`.
Expected: same pass/fail set as before this task, plus the two new `SpeciesRosterTests` passing — no new failures (the 4 pre-existing `EmergenceSystemTests` failures are unrelated and expected to remain).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
Add Squirrel as the researchable herbivore

New SpeciesDefinition + ResearchProjectDefinition (no prerequisite,
matching Pine/Fox's tier), plus a dedicated procedural sprite trait
case (round ears, bushy tail) so it doesn't fall back to the generic
default quadruped shape. Also drops the now-dead per-species cases for
every deleted animal, including the eagle-specific bird-sprite branch.
EOF
)"
```

---

### Task 3: Rewire every scene component's species/vegetation lists to the final roster

**Files:**
- Modify (via Unity, not text edit): `Assets/Scenes/SampleScene.unity` — specifically the serialized fields on the `TreePlanter`, `EmergenceSystem`, `ResearchManager`, `CodexUI`, `DemoUI`, `ProfileUI`, and `SaveManager` components.

**Interfaces:**
- Consumes: the 6 final species/vegetation assets and 3 final research assets from Tasks 1–2.
- Produces: a scene where every one of these components' lists reflects the final roster — Task 4 (Lab tabs) depends on `ResearchManager.availableResearchProjects`/`unlockableSpecies` being correct, and manual play-testing of planting/research/emergence in Task 5 depends on all of them.

This is a scene-data change, not a code change — there is no compiled unit to unit-test, and the project's existing test suite doesn't load the scene (every existing test builds synthetic `GameObject`s instead, see `Assets/Scripts/Tests/ResearchManagerTests.cs` for the established pattern). Verification here is a direct runtime check via `execute_code`, matching how HUD sprite fields were wired earlier in this project's history — not a gap, just the right tool for scene data.

- [ ] **Step 1: Confirm current (stale) state before changing anything**

Run via `mcp__UnityMCP__execute_code` (this is a read-only sanity check, not part of the fix):

```csharp
TreePlanter treePlanter = UnityEngine.Object.FindFirstObjectByType<TreePlanter>();
System.Reflection.FieldInfo field = typeof(TreePlanter).GetField("plantableVegetation", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
System.Collections.Generic.List<VegetationDefinition> current = (System.Collections.Generic.List<VegetationDefinition>)field.GetValue(treePlanter);
return "plantableVegetation count=" + current.Count;
```

Expected: a count reflecting the old (pre-cleanup) roster size — confirms the scene still has stale references before this task fixes them.

- [ ] **Step 2: Rewire every list**

Run via `mcp__UnityMCP__execute_code`:

```csharp
VegetationDefinition oak = UnityEditor.AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Oak.asset");
VegetationDefinition pine = UnityEditor.AssetDatabase.LoadAssetAtPath<VegetationDefinition>("Assets/Data/Vegetation/Pine.asset");
SpeciesDefinition gazelle = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Gazelle.asset");
SpeciesDefinition squirrel = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Squirrel.asset");
SpeciesDefinition wolf = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Wolf.asset");
SpeciesDefinition fox = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Fox.asset");
ResearchProjectDefinition pineResearch = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/PineResearch.asset");
ResearchProjectDefinition squirrelResearch = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/SquirrelResearch.asset");
ResearchProjectDefinition foxResearch = UnityEditor.AssetDatabase.LoadAssetAtPath<ResearchProjectDefinition>("Assets/Data/Research/FoxResearch.asset");

System.Action<UnityEngine.Object, string, UnityEngine.Object[]> setList = (target, fieldName, values) =>
{
    UnityEditor.SerializedObject so = new UnityEditor.SerializedObject(target);
    UnityEditor.SerializedProperty prop = so.FindProperty(fieldName);
    prop.arraySize = values.Length;
    for (int i = 0; i < values.Length; i++)
    {
        prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
    so.ApplyModifiedProperties();
    UnityEditor.EditorUtility.SetDirty(target);
};

TreePlanter treePlanter = UnityEngine.Object.FindFirstObjectByType<TreePlanter>();
setList(treePlanter, "plantableVegetation", new UnityEngine.Object[] { oak, pine });
setList(treePlanter, "unlockedVegetation", new UnityEngine.Object[] { oak });

EmergenceSystem emergenceSystem = UnityEngine.Object.FindFirstObjectByType<EmergenceSystem>();
setList(emergenceSystem, "unlockedNonPredatorSpecies", new UnityEngine.Object[] { gazelle });
setList(emergenceSystem, "unlockedPredatorSpecies", new UnityEngine.Object[] { wolf });

ResearchManager researchManager = UnityEngine.Object.FindFirstObjectByType<ResearchManager>();
setList(researchManager, "availableResearchProjects", new UnityEngine.Object[] { pineResearch, squirrelResearch, foxResearch });
setList(researchManager, "unlockableSpecies", new UnityEngine.Object[] { squirrel, fox });

CodexUI codexUI = UnityEngine.Object.FindFirstObjectByType<CodexUI>(UnityEngine.FindObjectsInactive.Include);
setList(codexUI, "allSpecies", new UnityEngine.Object[] { gazelle, squirrel, wolf, fox });
setList(codexUI, "allVegetation", new UnityEngine.Object[] { oak, pine });

DemoUI demoUI = UnityEngine.Object.FindFirstObjectByType<DemoUI>(UnityEngine.FindObjectsInactive.Include);
setList(demoUI, "allSpecies", new UnityEngine.Object[] { gazelle, squirrel, wolf, fox });
setList(demoUI, "allVegetation", new UnityEngine.Object[] { oak, pine });

ProfileUI profileUI = UnityEngine.Object.FindFirstObjectByType<ProfileUI>(UnityEngine.FindObjectsInactive.Include);
setList(profileUI, "allSpecies", new UnityEngine.Object[] { gazelle, squirrel, wolf, fox });
setList(profileUI, "allVegetation", new UnityEngine.Object[] { oak, pine });

SaveManager saveManager = UnityEngine.Object.FindFirstObjectByType<SaveManager>();
setList(saveManager, "allSpecies", new UnityEngine.Object[] { gazelle, squirrel, wolf, fox });

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(treePlanter.gameObject.scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(treePlanter.gameObject.scene);

return "done";
```

- [ ] **Step 3: Verify every list in a fresh Play session**

Stop Play mode if running, start it again (a fresh `Awake()`/`OnEnable()` pass is required — mid-play-session script/asset changes can leave stale singleton state, as seen earlier in this project's history), then run via `mcp__UnityMCP__execute_code`:

```csharp
TreePlanter treePlanter = UnityEngine.Object.FindFirstObjectByType<TreePlanter>();
return "plantableVegetation=" + treePlanter.GetUnlockedVegetationIds().Count + " (expect unlockedVegetation to report 1: Oak only until Pine is researched)\n"
     + "unlocked=" + string.Join(",", treePlanter.GetUnlockedVegetationIds());
```

Expected: `unlocked=oak` (only the free starter is unlocked at a fresh game start; Pine appears only after being researched — this is existing `AddUnlockedVegetation` behavior, unchanged by this task).

Then confirm the Laboratory's project list directly:

```csharp
ResearchManager researchManager = UnityEngine.Object.FindFirstObjectByType<ResearchManager>();
System.Text.StringBuilder sb = new System.Text.StringBuilder();
foreach (ResearchProjectDefinition project in researchManager.AvailableResearchProjects)
{
    sb.AppendLine(project.id);
}
return sb.ToString();
```

Expected: exactly `pine_research`, `squirrel_research`, `fox_research` (any order).

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
Rewire scene components' species/vegetation lists to the final roster

TreePlanter, EmergenceSystem, ResearchManager, CodexUI, DemoUI,
ProfileUI, and SaveManager each hold their own serialized list of
species/vegetation - all seven now reference only Oak, Pine, Gazelle,
Squirrel, Wolf, and Fox.
EOF
)"
```

---

### Task 4: Split the Laboratory into Plants / Herbivores / Predators tabs

**Files:**
- Modify: `Assets/UI/Laboratory/Laboratory.uxml`
- Modify: `Assets/UI/Laboratory/Laboratory.uss`
- Modify: `Assets/Scripts/UI/LaboratoryUI.cs`

**Interfaces:**
- Consumes: `ResearchManager.AvailableResearchProjects` (from Task 3, now just 3 projects) and the existing `GetBranchRank(ResearchProjectDefinition)` method (already returns 0=vegetation/1=herbivore/2=predator — this task reuses it as the tab filter key instead of adding a new categorization).
- Produces: nothing new consumed by later stages of the larger refactor — this is this stage's final UI piece.

- [ ] **Step 1: Add the tab buttons to the UXML**

In `Assets/UI/Laboratory/Laboratory.uxml`, insert a new `lab-tabs` row between `lab-header` and `active-research-panel`:

```xml
            <ui:VisualElement name="lab-header" class="lab-header">
                <ui:Label name="lab-title" text="Laboratory" class="lab-title" />
                <ui:Button name="close-button" text="X" class="lab-close-button" />
            </ui:VisualElement>
            <ui:VisualElement name="lab-tabs" class="lab-tabs">
                <ui:Button name="plants-tab-button" text="Plants" class="lab-tab-button" />
                <ui:Button name="herbivores-tab-button" text="Herbivores" class="lab-tab-button" />
                <ui:Button name="predators-tab-button" text="Predators" class="lab-tab-button" />
            </ui:VisualElement>
            <ui:VisualElement name="active-research-panel" class="active-research-panel">
```

(Replace the existing `lab-header`...`active-research-panel` two lines with these three blocks — everything else in the file is unchanged.)

- [ ] **Step 2: Style the tabs**

Add to `Assets/UI/Laboratory/Laboratory.uss`:

```css
.lab-tabs {
    flex-direction: row;
    margin-bottom: var(--space-sm);
}

.lab-tab-button {
    flex-grow: 1;
    height: 32px;
    margin-right: var(--space-xs);
    border-radius: var(--radius-md);
    border-top-width: var(--border-thin);
    border-left-width: var(--border-thin);
    border-right-width: var(--border-shadow);
    border-bottom-width: var(--border-shadow);
    border-top-color: var(--color-neutral-light);
    border-left-color: var(--color-neutral-light);
    border-right-color: var(--color-shadow);
    border-bottom-color: var(--color-shadow);
    background-color: var(--color-neutral);
    color: var(--text-on-dark);
    font-size: var(--font-size-sm);
    -unity-font-style: bold;
}

.lab-tab-button:last-child {
    margin-right: 0;
}

.lab-tab-button--active {
    background-color: var(--color-info);
    border-top-color: var(--color-info-light);
    border-left-color: var(--color-info-light);
}
```

- [ ] **Step 3: Wire tab selection and filtering in `LaboratoryUI.cs`**

Add a `LabTab` enum and selection state — insert right after the `NodeState` enum declaration (around line 186):

```csharp
    private enum LabTab
    {
        Plants = 0,
        Herbivores = 1,
        Predators = 2
    }

    private LabTab selectedTab = LabTab.Plants;
```

Add three new fields alongside the other `Button`/`VisualElement` fields (near `skipButton`):

```csharp
    private Button plantsTabButton;
    private Button herbivoresTabButton;
    private Button predatorsTabButton;
```

In `OnEnable()`, right after `skipButton = root.Q<Button>("skip-button");`, add:

```csharp
        plantsTabButton = root.Q<Button>("plants-tab-button");
        herbivoresTabButton = root.Q<Button>("herbivores-tab-button");
        predatorsTabButton = root.Q<Button>("predators-tab-button");
        plantsTabButton.clicked += () => SelectTab(LabTab.Plants);
        herbivoresTabButton.clicked += () => SelectTab(LabTab.Herbivores);
        predatorsTabButton.clicked += () => SelectTab(LabTab.Predators);
```

Add two new methods anywhere in the class (e.g. right after `SetOpen`):

```csharp
    private void SelectTab(LabTab tab)
    {
        if (selectedTab == tab)
        {
            return;
        }

        AudioManager.Instance?.PlayTap();
        selectedTab = tab;
        RefreshTabButtons();
        BuildTree();
    }

    private void RefreshTabButtons()
    {
        plantsTabButton.EnableInClassList("lab-tab-button--active", selectedTab == LabTab.Plants);
        herbivoresTabButton.EnableInClassList("lab-tab-button--active", selectedTab == LabTab.Herbivores);
        predatorsTabButton.EnableInClassList("lab-tab-button--active", selectedTab == LabTab.Predators);
    }
```

In `SetOpen(bool visible)`, reset to the Plants tab every time the panel opens (so it doesn't reopen on whatever tab was last selected) — change:

```csharp
        if (visible)
        {
            RefreshLab();
            PanelAnimator.Open(labRoot, labFrame);
        }
```

to:

```csharp
        if (visible)
        {
            selectedTab = LabTab.Plants;
            RefreshTabButtons();
            RefreshLab();
            PanelAnimator.Open(labRoot, labFrame);
        }
```

Finally, filter `BuildTree()` to only the selected tab's projects. Change:

```csharp
        IReadOnlyList<ResearchProjectDefinition> projects = researchManager.AvailableResearchProjects;
```

to:

```csharp
        List<ResearchProjectDefinition> projects = new List<ResearchProjectDefinition>();
        foreach (ResearchProjectDefinition project in researchManager.AvailableResearchProjects)
        {
            if (project != null && GetBranchRank(project) == (int)selectedTab)
            {
                projects.Add(project);
            }
        }
```

(Everything below this line in `BuildTree()` already operates on the `projects` variable and needs no further change — `GetBranchRank`'s existing 0/1/2 return values already line up exactly with `LabTab.Plants/Herbivores/Predators`.)

- [ ] **Step 4: Refresh Unity and confirm it compiles with no errors**

Run `mcp__UnityMCP__refresh_unity` (`mode: force`, `scope: all`, `compile: request`, `wait_for_ready: true`), then `mcp__UnityMCP__read_console` filtered to `error`.
Expected: zero errors.

- [ ] **Step 5: Verify visually**

Enter Play mode, open the Laboratory (via `LaboratoryUI.ToggleLab()` through `execute_code`, or tap its HUD button), and use `mcp__UnityMCP__manage_camera` (`action: screenshot`, no `camera` param) to capture it. Confirm:
- Three tab buttons are visible, "Plants" active by default.
- The Plants tab shows exactly one node: "Pine".
- Clicking "Herbivores" shows exactly one node: "Squirrel".
- Clicking "Predators" shows exactly one node: "Fox".

Delete the debug screenshot file afterward (`mcp__UnityMCP__manage_asset`, `action: delete`) — matches this project's established practice of not leaving debug screenshots in `Assets/Screenshots/`.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "$(cat <<'EOF'
Split the Laboratory into Plants/Herbivores/Predators tabs

Three tab buttons filter which tech-tree nodes BuildTree() renders,
reusing the existing GetBranchRank() categorization (0/1/2) as the
filter key instead of introducing a new one. Each tab now shows just
its one researchable node, matching the trimmed roster.
EOF
)"
```

---

### Task 5: Full integration verification

**Files:** None (verification only).

- [ ] **Step 1: Run the complete EditMode suite**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `include_failed_tests: true`.
Expected: every test passes except the same 4 pre-existing `EmergenceSystemTests` failures that predate this plan (creature-visual spawning issue, unrelated). Zero other failures.

- [ ] **Step 2: Play-mode smoke test with no console errors**

Enter Play mode. Run `mcp__UnityMCP__read_console` filtered to `["error", "warning"]`.
Expected: no errors or warnings referencing a missing/null species or vegetation reference (e.g. no `MissingReferenceException`, no `NullReferenceException` from `EmergenceSystem`/`CodexUI`/`ResearchManager`).

- [ ] **Step 3: Confirm the Codex shows exactly the final roster**

Via `execute_code`:

```csharp
CodexUI codex = UnityEngine.Object.FindFirstObjectByType<CodexUI>();
codex.SetOpen(true);
System.Reflection.FieldInfo pagesField = typeof(CodexUI).GetField("pages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
System.Collections.IList pages = (System.Collections.IList)pagesField.GetValue(codex);
return "page count=" + pages.Count + " (expect 6: Gazelle, Fox, Oak, Pine - herbivore/predator/vegetation order per BuildPages, plus Wolf and Squirrel)";
```

Expected: exactly 6 pages (Gazelle, Wolf come from the Herbivore/Predator loop alongside Squirrel/Fox — 4 species + 2 vegetation).

- [ ] **Step 4: Research each of the three researchable items successfully**

Via `execute_code`, confirm each can be started and finished without error (mirrors what a player does through the Lab UI):

```csharp
ResearchManager researchManager = UnityEngine.Object.FindFirstObjectByType<ResearchManager>();
System.Text.StringBuilder sb = new System.Text.StringBuilder();
foreach (ResearchProjectDefinition project in researchManager.AvailableResearchProjects)
{
    bool canStart = researchManager.CanStartResearch(project);
    sb.AppendLine(project.id + " canStart=" + canStart);
}
return sb.ToString();
```

Expected: all three report `canStart=True` (nothing blocked by a leftover prerequisite reference to a deleted project).

- [ ] **Step 5: Stop Play mode; no further action needed**

This task produces no commit — it's a verification pass over Tasks 1–4's work. If any check above fails, return to the relevant task and fix it there (with its own re-test-and-commit cycle) rather than patching here.
