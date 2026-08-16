# Government Funds Economy Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat daily tree-planting cap (`DailyGrant`) with a spendable `Funds` currency that grows with trees planted and species unlocked, gate tree-planting and animal-release on affordability instead of a daily counter, and add a Funds HUD badge, a Store catalog panel, and a floating "-cost" popup on every spend.

**Architecture:** A new `EconomyManager` MonoBehaviour (replacing `DailyGrant`, same GUID/scene slot) owns the `Funds` balance and a once-per-calendar-day grant formula. `VegetationDefinition`/`SpeciesDefinition` gain flat `plantCost`/`releaseCost` fields. `TreePlanter`/`EmergenceSystem` spend Funds via `EconomyManager.TrySpend` as an additional gate alongside their existing spatial/capacity checks. `HudUI` gains a Funds badge and a world-position floating cost popup; a new `StoreUI` panel (modeled directly on `SpeciesSummaryUI`) lists everything unlocked with its cost and lets the player select it on the existing HUD picker.

**Tech Stack:** Unity 2D URP, C# (single `Grove.Runtime` asmdef under `Assets/Scripts`), UI Toolkit (UXML/USS) for all panels, NUnit EditMode tests under `Assets/Scripts/Tests` (`Grove.Tests` asmdef).

**Spec:** `docs/superpowers/specs/2026-08-16-government-funds-economy-design.md`

## Global Constraints

- Funds is a **new, separate currency** from the existing Gems (`MilestoneManager.gems`) — Gems and land tiers are untouched.
- Costs are **flat per-type**, not scaling: Oak/Pine `plantCost: 10`; Squirrel/Gazelle (herbivores) `releaseCost: 50`; Fox/Wolf (predators) `releaseCost: 200`.
- Funds income is a **once-per-calendar-day lump sum** (`BaseDailyFunds + milestoneFundsBonus + TotalTreeCount*FundsPerTree + UnlockedSpeciesCount*FundsPerSpecies`), not continuous real-time accrual — mirrors `DailyGrant`'s existing single-reset-regardless-of-offline-gap semantics exactly.
- `EmergenceSystem.CanRelease`'s existing capacity/ratio gating (tree-per-individual caps, predator/prey ratios) is **unchanged** and stays an independent, additional gate alongside the new Funds cost.
- The Store is a **catalog only** — selecting an item there sets it as the active HUD picker selection and closes the store; actual placement still happens by tapping the ground, exactly as today. No "buy button places directly" flow.
- This project has exactly one runtime assembly (`Grove.Runtime`, all of `Assets/Scripts` recursively) and one test assembly (`Grove.Tests`, references `Grove.Runtime`) — no assembly-boundary concerns for any new file.
- `Assets/Scenes/SampleScene.unity` is the only scene wired with game objects for these systems (`Assets/_Recovery/*.unity` are stale backups — do not touch them).

---

### Task 1: Cost fields on VegetationDefinition and SpeciesDefinition

**Files:**
- Modify: `Assets/Scripts/Vegetation/VegetationDefinition.cs`
- Modify: `Assets/Scripts/Species/SpeciesDefinition.cs`
- Modify: `Assets/Data/Vegetation/Oak.asset`, `Assets/Data/Vegetation/Pine.asset`
- Modify: `Assets/Data/Species/Squirrel.asset`, `Assets/Data/Species/Gazelle.asset`, `Assets/Data/Species/Fox.asset`, `Assets/Data/Species/Wolf.asset`

**Interfaces:**
- Produces: `VegetationDefinition.plantCost` (public int), `SpeciesDefinition.releaseCost` (public int) — consumed by Task 2's `TreePlanter`/`EmergenceSystem` spend gates and Task 5's Store panel.

- [ ] **Step 1: Add `plantCost` to `VegetationDefinition`**

In `Assets/Scripts/Vegetation/VegetationDefinition.cs`, add a new public field after `heroSpriteMatureWorldDiameter`:

```csharp
public float heroSpriteMatureWorldDiameter = 2.4f;

// Flat Funds cost to plant one instance of this vegetation (see EconomyManager).
public int plantCost;
```

- [ ] **Step 2: Add `releaseCost` to `SpeciesDefinition`**

In `Assets/Scripts/Species/SpeciesDefinition.cs`, add a new public field after `treesPerIndividualCap`:

```csharp
public float baseEmergenceWeight;
public int treesPerIndividualCap;

// Flat Funds cost to release one individual of this species (see EconomyManager).
public int releaseCost;
```

- [ ] **Step 3: Author cost values on the existing assets**

Edit each asset file, adding the new field's YAML key with the value shown (insert `plantCost:`/`releaseCost:` as a new line anywhere in the field list — Unity's YAML importer reads fields by key, not position):

- `Assets/Data/Vegetation/Oak.asset`: add `plantCost: 10`
- `Assets/Data/Vegetation/Pine.asset`: add `plantCost: 10`
- `Assets/Data/Species/Squirrel.asset`: add `releaseCost: 50`
- `Assets/Data/Species/Gazelle.asset`: add `releaseCost: 50`
- `Assets/Data/Species/Fox.asset`: add `releaseCost: 200`
- `Assets/Data/Species/Wolf.asset`: add `releaseCost: 200`

For example, in `Assets/Data/Vegetation/Oak.asset`, change:
```yaml
  heroSprite: {fileID: 21300000, guid: cefba41b4e318459b9768330797c28de, type: 3}
  heroSpriteMatureWorldDiameter: 2.4
```
to:
```yaml
  heroSprite: {fileID: 21300000, guid: cefba41b4e318459b9768330797c28de, type: 3}
  heroSpriteMatureWorldDiameter: 2.4
  plantCost: 10
```

- [ ] **Step 4: Verify the project still compiles**

These are plain additive public fields with no behavior — there's no dedicated test for this step. Confirm by reading each edited asset file back and checking the new key/value is present, and that `VegetationDefinition.cs`/`SpeciesDefinition.cs` have no syntax errors (balanced braces, correct field declarations).

- [ ] **Step 5: Commit**

```bash
git add Assets/Scripts/Vegetation/VegetationDefinition.cs Assets/Scripts/Species/SpeciesDefinition.cs \
  Assets/Data/Vegetation/Oak.asset Assets/Data/Vegetation/Pine.asset \
  Assets/Data/Species/Squirrel.asset Assets/Data/Species/Gazelle.asset \
  Assets/Data/Species/Fox.asset Assets/Data/Species/Wolf.asset
git commit -m "feat: add flat plant/release cost fields to vegetation and species data"
```

---

### Task 2: EconomyManager replaces DailyGrant (core + full rewire)

This is one task, not several, because C# forces atomicity here: `DailyGrant` is a compile-time type referenced by five other files (`TreePlanter`, `EmergenceSystem` gains a new reference, `MilestoneManager`, `SaveManager`, `ProfileUI`), so the project cannot compile in any intermediate state where only some of them have been updated. Work through the steps in order; the project should compile again only once the whole task is done.

**Files:**
- Create (rename from `Assets/Scripts/Vegetation/DailyGrant.cs`, preserving the script GUID): `Assets/Scripts/Economy/EconomyManager.cs`
- Create (rename from `Assets/Scripts/Tests/DailyGrantTests.cs`): `Assets/Scripts/Tests/EconomyManagerTests.cs`
- Modify: `Assets/Scripts/Vegetation/TreePlanter.cs`
- Modify: `Assets/Scripts/Tests/TreePlanterTests.cs`
- Modify: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`
- Modify: `Assets/Scripts/Tests/EmergenceSystemTests.cs`
- Modify: `Assets/Scripts/Milestones/MilestoneManager.cs`
- Modify: `Assets/Scripts/Milestones/MilestoneDefinition.cs`
- Modify: `Assets/Data/Milestones/AllStartersPresent.asset`, `First10Trees.asset`, `First200Trees.asset`, `First50Trees.asset`, `FirstPredator.asset`
- Modify: `Assets/Scripts/Save/SaveManager.cs`
- Modify: `Assets/Scripts/Save/PlayerSaveData.cs`
- Modify: `Assets/Scripts/UI/ProfileUI.cs`
- Modify: `Assets/UI/Profile/Profile.uxml`
- Modify: `Assets/Scripts/UI/PixelArtUtility.cs` (adds `DrawCoinGlyph`, needed by `ProfileUI`'s funds row)
- Modify: `Assets/Scenes/SampleScene.unity`

**Interfaces:**
- Consumes: `VegetationDefinition.plantCost`, `SpeciesDefinition.releaseCost` (Task 1), `EmergenceSystem.UnlockedSpeciesCount` (already public), `EcosystemState.TotalTreeCount` (already public).
- Produces: `EconomyManager.Funds` (int, get), `EconomyManager.LastFundsGrantTimestamp` (string, get), `EconomyManager.CanAfford(int cost)` (bool), `EconomyManager.TrySpend(int cost)` (bool), `EconomyManager.AddDailyFundsBonus(int amount)` (void), `EconomyManager.LoadState(int savedFunds, string savedLastFundsGrantTimestamp)` (void), `EconomyManager.DebugForceGrant()` (public, for tests/debug) — all consumed by later steps in this task and by Tasks 3-5. `PixelArtUtility.DrawCoinGlyph(PixelBuffer canvas)` — consumed by Task 3.

- [ ] **Step 1: Rename the script file, preserving its GUID**

```bash
mkdir -p "Assets/Scripts/Economy"
git mv "Assets/Scripts/Vegetation/DailyGrant.cs" "Assets/Scripts/Economy/EconomyManager.cs"
git mv "Assets/Scripts/Vegetation/DailyGrant.cs.meta" "Assets/Scripts/Economy/EconomyManager.cs.meta"
```

- [ ] **Step 2: Rewrite `EconomyManager.cs`**

Replace the entire file content with:

```csharp
using System;
using UnityEngine;

public class EconomyManager : MonoBehaviour
{
    private const int BaseDailyFunds = 300;
    private const int FundsPerTree = 1;
    private const int FundsPerSpecies = 50;
    private const int StartingFunds = 300;
    private const float ResetCheckIntervalSeconds = 5f;

    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private int funds;
    [SerializeField] private int milestoneFundsBonus;
    [SerializeField] private string lastFundsGrantTimestamp;

    private DateTime lastResetDate;
    private float resetCheckTimer;

    public int Funds => funds;
    public string LastFundsGrantTimestamp => lastFundsGrantTimestamp;

    private void Awake()
    {
        milestoneFundsBonus = 0;
        funds = StartingFunds;
        lastResetDate = DateTime.Now.Date;
        lastFundsGrantTimestamp = lastResetDate.ToString("o");
    }

    public bool CanAfford(int cost)
    {
        return cost >= 0 && funds >= cost;
    }

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost))
        {
            return false;
        }

        funds -= cost;
        return true;
    }

    public void AddDailyFundsBonus(int amount)
    {
        milestoneFundsBonus += amount;
        Debug.Log($"Daily funds bonus increased by {amount}. New base daily funds: {BaseDailyFunds + milestoneFundsBonus}.");
    }

    public void LoadState(int savedFunds, string savedLastFundsGrantTimestamp)
    {
        funds = savedFunds;

        if (DateTime.TryParse(savedLastFundsGrantTimestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed))
        {
            lastResetDate = parsed.ToLocalTime().Date;
        }
        else
        {
            lastResetDate = DateTime.Now.Date;
        }

        lastFundsGrantTimestamp = lastResetDate.ToString("o");

        CheckForDailyReset();
    }

    private void Update()
    {
        resetCheckTimer += Time.deltaTime;
        if (resetCheckTimer >= ResetCheckIntervalSeconds)
        {
            resetCheckTimer = 0f;
            CheckForDailyReset();
        }
    }

    private void CheckForDailyReset()
    {
        DateTime today = DateTime.Now.Date;
        if (today > lastResetDate)
        {
            GrantDailyFunds();
        }
    }

    private void GrantDailyFunds()
    {
        int speciesCount = emergenceSystem != null ? emergenceSystem.UnlockedSpeciesCount : 0;
        int amount = BaseDailyFunds + milestoneFundsBonus
            + (EcosystemState.TotalTreeCount * FundsPerTree)
            + (speciesCount * FundsPerSpecies);

        funds += amount;
        lastResetDate = DateTime.Now.Date;
        lastFundsGrantTimestamp = lastResetDate.ToString("o");
        Debug.Log($"Daily funds granted: +{amount}. Total funds: {funds}.");
    }

    [ContextMenu("Debug: Simulate Next Day")]
    private void DebugSimulateNextDay()
    {
        lastResetDate = DateTime.Now.Date.AddDays(-1);
        CheckForDailyReset();
    }

    [ContextMenu("Debug: Force Grant Now")]
    public void DebugForceGrant()
    {
        GrantDailyFunds();
    }
}
```

- [ ] **Step 3: Rename and rewrite the test file**

```bash
git mv "Assets/Scripts/Tests/DailyGrantTests.cs" "Assets/Scripts/Tests/EconomyManagerTests.cs"
git mv "Assets/Scripts/Tests/DailyGrantTests.cs.meta" "Assets/Scripts/Tests/EconomyManagerTests.cs.meta"
```

Replace the file content with:

```csharp
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class EconomyManagerTests
{
    private GameObject go;
    private EconomyManager economy;
    private GameObject emergenceGO;
    private EmergenceSystem emergenceSystem;

    [SetUp]
    public void SetUp()
    {
        EcosystemState.ResetAll();

        emergenceGO = new GameObject("TestEmergenceSystem");
        emergenceSystem = emergenceGO.AddComponent<EmergenceSystem>();

        go = new GameObject("TestEconomyManager");
        economy = go.AddComponent<EconomyManager>();
        SetPrivateField(economy, "emergenceSystem", emergenceSystem);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(go);
        Object.DestroyImmediate(emergenceGO);
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        field.SetValue(target, value);
    }

    [Test]
    public void Awake_StartsWithStartingFunds()
    {
        Assert.AreEqual(300, economy.Funds);
    }

    [Test]
    public void CanAfford_TrueUpToExactBalance()
    {
        Assert.IsTrue(economy.CanAfford(300));
        Assert.IsFalse(economy.CanAfford(301));
    }

    [Test]
    public void TrySpend_SucceedsAndDeductsWhenAffordable()
    {
        bool result = economy.TrySpend(100);

        Assert.IsTrue(result);
        Assert.AreEqual(200, economy.Funds);
    }

    [Test]
    public void TrySpend_FailsAndLeavesBalanceWhenInsufficient()
    {
        bool result = economy.TrySpend(500);

        Assert.IsFalse(result);
        Assert.AreEqual(300, economy.Funds);
    }

    [Test]
    public void DebugForceGrant_AddsBaseAmountWithNoTreesOrSpecies()
    {
        economy.DebugForceGrant();

        Assert.AreEqual(300 + 300, economy.Funds);
    }

    [Test]
    public void DebugForceGrant_AddsTreeAndSpeciesContribution()
    {
        EcosystemState.RegisterTreePlanted("oak");
        EcosystemState.RegisterTreePlanted("oak");

        SpeciesDefinition species = ScriptableObject.CreateInstance<SpeciesDefinition>();
        species.id = "squirrel";
        species.category = SpeciesDefinition.SpeciesCategory.Herbivore;
        emergenceSystem.AddUnlockedSpecies(species);

        economy.DebugForceGrant();

        // starting 300 + base 300 + 2 trees * 1 + 1 species * 50
        Assert.AreEqual(300 + 300 + 2 + 50, economy.Funds);

        Object.DestroyImmediate(species);
    }

    [Test]
    public void AddDailyFundsBonus_RaisesFutureGrantAmount()
    {
        economy.AddDailyFundsBonus(50);

        economy.DebugForceGrant();

        Assert.AreEqual(300 + 300 + 50, economy.Funds);
    }

    [Test]
    public void LoadState_RestoresFundsAndCatchesUpOneDayGrant()
    {
        string yesterday = System.DateTime.Now.Date.AddDays(-1).ToString("o");

        economy.LoadState(50, yesterday);

        // 50 restored + one catch-up grant of 300 (no trees/species)
        Assert.AreEqual(50 + 300, economy.Funds);
    }

    [Test]
    public void LoadState_SameDayTimestamp_DoesNotGrantAgain()
    {
        string today = System.DateTime.Now.Date.ToString("o");

        economy.LoadState(50, today);

        Assert.AreEqual(50, economy.Funds);
    }
}
```

- [ ] **Step 4: Rewire `TreePlanter.cs`**

Change the field (near the top):
```csharp
[SerializeField] private DailyGrant dailyGrant;
```
to:
```csharp
[SerializeField] private EconomyManager economyManager;
```

In `PlantTree(Vector3 position)`, change:
```csharp
if (dailyGrant == null)
{
    Debug.LogWarning("TreePlanter has no DailyGrant assigned.");
    return;
}
```
to:
```csharp
if (economyManager == null)
{
    Debug.LogWarning("TreePlanter has no EconomyManager assigned.");
    return;
}
```

Change:
```csharp
if (!dailyGrant.TryConsumeTreePlant())
{
    Debug.Log($"Daily tree cap reached ({dailyGrant.TreesPlantedToday}/{dailyGrant.DailyTreeCap}). Come back after the next grant reset.");
    AudioManager.Instance?.PlayError();
    return;
}
```
to:
```csharp
if (!economyManager.TrySpend(vegetation.plantCost))
{
    Debug.Log($"Not enough funds to plant {vegetation.displayName} (need {vegetation.plantCost}, have {economyManager.Funds}).");
    AudioManager.Instance?.PlayError();
    return;
}
```

In `CanPlantAt(Vector3 position)`, change:
```csharp
if (dailyGrant != null && !dailyGrant.CanPlantTree)
{
    return false;
}
```
to:
```csharp
if (economyManager != null)
{
    VegetationDefinition vegetation = CurrentVegetation;
    int cost = vegetation != null ? vegetation.plantCost : 0;
    if (!economyManager.CanAfford(cost))
    {
        return false;
    }
}
```

Change `DebugPlantBatch()`:
```csharp
[ContextMenu("Debug: Plant N Trees (auto-resets daily cap)")]
public void DebugPlantBatch()
{
    for (int i = 0; i < debugBatchPlantCount; i++)
    {
        if (dailyGrant != null && !dailyGrant.CanPlantTree)
        {
            dailyGrant.DebugForceReset();
        }

        Vector3 position = new Vector3(i * 0.5f, 0f, 0f);
        PlantTree(position);
    }
}
```
to:
```csharp
[ContextMenu("Debug: Plant N Trees (auto-grants funds)")]
public void DebugPlantBatch()
{
    for (int i = 0; i < debugBatchPlantCount; i++)
    {
        VegetationDefinition vegetation = CurrentVegetation;
        int cost = vegetation != null ? vegetation.plantCost : 0;
        if (economyManager != null && !economyManager.CanAfford(cost))
        {
            economyManager.DebugForceGrant();
        }

        Vector3 position = new Vector3(i * 0.5f, 0f, 0f);
        PlantTree(position);
    }
}
```

- [ ] **Step 5: Update `TreePlanterTests.cs`**

Change the fields:
```csharp
private GameObject dailyGrantGO;
private DailyGrant dailyGrant;
```
to:
```csharp
private GameObject economyManagerGO;
private EconomyManager economyManager;
```

In `SetUp()`, change:
```csharp
dailyGrantGO = new GameObject("TestDailyGrant");
dailyGrant = dailyGrantGO.AddComponent<DailyGrant>();

SetPrivateField(planter, "unlockedVegetation", new List<VegetationDefinition> { oak });
SetPrivateField(planter, "plantableVegetation", new List<VegetationDefinition> { oak, pine });
SetPrivateField(planter, "dailyGrant", dailyGrant);
```
to:
```csharp
economyManagerGO = new GameObject("TestEconomyManager");
economyManager = economyManagerGO.AddComponent<EconomyManager>();

oak.plantCost = 10;
pine.plantCost = 10;

SetPrivateField(planter, "unlockedVegetation", new List<VegetationDefinition> { oak });
SetPrivateField(planter, "plantableVegetation", new List<VegetationDefinition> { oak, pine });
SetPrivateField(planter, "economyManager", economyManager);
```

In `TearDown()`, change:
```csharp
Object.DestroyImmediate(dailyGrantGO);
```
to:
```csharp
Object.DestroyImmediate(economyManagerGO);
```

Replace the test:
```csharp
[Test]
public void PlantTree_ConsumesDailyAllowanceAndSpawnsATree()
{
    int treesBefore = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;

    planter.PlantTree(Vector3.zero);

    int treesAfter = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
    Assert.AreEqual(treesBefore + 1, treesAfter);
    Assert.AreEqual(1, dailyGrant.TreesPlantedToday);
}
```
with:
```csharp
[Test]
public void PlantTree_SpendsFundsAndSpawnsATree()
{
    int treesBefore = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
    int fundsBefore = economyManager.Funds;

    planter.PlantTree(Vector3.zero);

    int treesAfter = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
    Assert.AreEqual(treesBefore + 1, treesAfter);
    Assert.AreEqual(fundsBefore - oak.plantCost, economyManager.Funds);
}
```

Replace the test:
```csharp
[Test]
public void PlantTree_StopsAtDailyCap()
{
    // Spaced well past the minimum tree-to-tree distance so this loop exercises the daily cap
    // specifically, not incidentally getting rejected by the spacing rule instead.
    for (int i = 0; i < 30; i++)
    {
        planter.PlantTree(new Vector3(i * 10f, 0f, 0f));
    }

    int treesAtCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;

    planter.PlantTree(new Vector3(1000f, 0f, 0f));

    int treesAfterOverCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
    Assert.AreEqual(treesAtCap, treesAfterOverCap);
}
```
with:
```csharp
[Test]
public void PlantTree_StopsWhenFundsRunOut()
{
    // EconomyManager starts with 300 Funds and oak costs 10, so exactly 30 plants are
    // affordable before funds run out. Spaced well past the minimum tree-to-tree distance so
    // this loop exercises running out of funds specifically, not the spacing rule.
    for (int i = 0; i < 30; i++)
    {
        planter.PlantTree(new Vector3(i * 10f, 0f, 0f));
    }

    int treesAtCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
    Assert.AreEqual(0, economyManager.Funds);

    planter.PlantTree(new Vector3(1000f, 0f, 0f));

    int treesAfterOverCap = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude).Length;
    Assert.AreEqual(treesAtCap, treesAfterOverCap);
}
```

Replace the test:
```csharp
[Test]
public void MoveTree_ValidDestination_RelocatesTreeAndDoesNotConsumeDailyCap()
{
    planter.PlantTree(Vector3.zero);
    PlantedTree tree = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude)[0];
    int treesPlantedBefore = dailyGrant.TreesPlantedToday;

    bool moved = planter.MoveTree(tree, new Vector3(20f, 0f, 0f));

    Assert.IsTrue(moved);
    Assert.AreEqual(new Vector3(20f, 0f, 0f), tree.transform.position);
    Assert.AreEqual(treesPlantedBefore, dailyGrant.TreesPlantedToday);
}
```
with:
```csharp
[Test]
public void MoveTree_ValidDestination_RelocatesTreeAndDoesNotSpendFunds()
{
    planter.PlantTree(Vector3.zero);
    PlantedTree tree = Object.FindObjectsByType<PlantedTree>(FindObjectsInactive.Exclude)[0];
    int fundsBefore = economyManager.Funds;

    bool moved = planter.MoveTree(tree, new Vector3(20f, 0f, 0f));

    Assert.IsTrue(moved);
    Assert.AreEqual(new Vector3(20f, 0f, 0f), tree.transform.position);
    Assert.AreEqual(fundsBefore, economyManager.Funds);
}
```

- [ ] **Step 6: Rewire `EmergenceSystem.cs`**

Add a new field near the other `[SerializeField]` fields:
```csharp
[SerializeField] private List<SpeciesRuntimeState> populations = new List<SpeciesRuntimeState>();
[SerializeField] private float creatureWanderRadius = 2f;
[SerializeField] private int currentHerbivoreIndex;
[SerializeField] private int currentPredatorIndex;
[SerializeField] private EconomyManager economyManager;
```

In `TryReleaseSpeciesAt(SpeciesDefinition species, Vector3 position)`, change:
```csharp
        if (!CanRelease(species))
        {
            Debug.Log($"Can't release {species.displayName} yet — criteria not met.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        SpawnIndividual(species, position);
```
to:
```csharp
        if (!CanRelease(species))
        {
            Debug.Log($"Can't release {species.displayName} yet — criteria not met.");
            AudioManager.Instance?.PlayError();
            return false;
        }

        if (economyManager != null && !economyManager.TrySpend(species.releaseCost))
        {
            Debug.Log($"Can't release {species.displayName} yet — not enough funds (need {species.releaseCost}, have {economyManager.Funds}).");
            AudioManager.Instance?.PlayError();
            return false;
        }

        SpawnIndividual(species, position);
```

- [ ] **Step 7: Update `EmergenceSystemTests.cs`**

Add fields near the top:
```csharp
private GameObject go;
private EmergenceSystem system;
private SpeciesDefinition herbivore;
private SpeciesDefinition predator;
private GameObject economyManagerGO;
private EconomyManager economyManager;
```

In `SetUp()`, after `system = go.AddComponent<EmergenceSystem>();`, add:
```csharp
economyManagerGO = new GameObject("TestEconomyManager");
economyManager = economyManagerGO.AddComponent<EconomyManager>();
SetPrivateField(system, "economyManager", economyManager);
```

Add the reflection helper (mirrors `TreePlanterTests.SetPrivateField`; the file already has `using System.Reflection;`):
```csharp
private static void SetPrivateField(object target, string fieldName, object value)
{
    FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
    field.SetValue(target, value);
}
```

In `TearDown()`, add:
```csharp
Object.DestroyImmediate(economyManagerGO);
```

Add two new tests near the existing `TryReleaseSpeciesAt_*` tests:
```csharp
[Test]
public void TryReleaseSpeciesAt_InsufficientFunds_ReturnsFalseAndDoesNotChangePopulation()
{
    herbivore.treesPerIndividualCap = 1;
    herbivore.releaseCost = 1000;
    EcosystemState.RegisterTreePlanted("oak");

    bool released = system.TryReleaseSpeciesAt(herbivore, Vector3.zero);

    Assert.IsFalse(released);
    Assert.AreEqual(0, EcosystemState.GetSpeciesPopulation("test_herbivore"));
}

[Test]
public void TryReleaseSpeciesAt_SufficientFunds_SpendsCost()
{
    herbivore.treesPerIndividualCap = 1;
    herbivore.releaseCost = 50;
    EcosystemState.RegisterTreePlanted("oak");
    int fundsBefore = economyManager.Funds;

    bool released = system.TryReleaseSpeciesAt(herbivore, Vector3.zero);

    Assert.IsTrue(released);
    Assert.AreEqual(fundsBefore - 50, economyManager.Funds);
}
```

- [ ] **Step 8: Rewire `MilestoneManager.cs`**

Change:
```csharp
[SerializeField] private DailyGrant dailyGrant;
```
to:
```csharp
[SerializeField] private EconomyManager economyManager;
```

In `CompleteMilestone(MilestoneDefinition milestone)`, change:
```csharp
if (milestone.rewardDailyCapBonus > 0 && dailyGrant != null)
{
    dailyGrant.AddDailyCapBonus(milestone.rewardDailyCapBonus);
}

gems += milestone.rewardGems;

Debug.Log($"Milestone completed: {milestone.id} (+{milestone.rewardDailyCapBonus} daily cap, +{milestone.rewardGems} gems).");
```
to:
```csharp
if (milestone.rewardDailyFundsBonus > 0 && economyManager != null)
{
    economyManager.AddDailyFundsBonus(milestone.rewardDailyFundsBonus);
}

gems += milestone.rewardGems;

Debug.Log($"Milestone completed: {milestone.id} (+{milestone.rewardDailyFundsBonus} daily funds, +{milestone.rewardGems} gems).");
```

- [ ] **Step 9: Rename the reward field on `MilestoneDefinition.cs`**

Change:
```csharp
public int rewardDailyCapBonus;
```
to:
```csharp
public int rewardDailyFundsBonus;
```

- [ ] **Step 10: Rename the reward key on the 5 milestone assets**

In each of these files, change the line `rewardDailyCapBonus: <value>` to `rewardDailyFundsBonus: <value>`, keeping the existing value unchanged:

- `Assets/Data/Milestones/AllStartersPresent.asset`: `rewardDailyCapBonus: 5` → `rewardDailyFundsBonus: 5`
- `Assets/Data/Milestones/First10Trees.asset`: `rewardDailyCapBonus: 5` → `rewardDailyFundsBonus: 5`
- `Assets/Data/Milestones/First200Trees.asset`: `rewardDailyCapBonus: 15` → `rewardDailyFundsBonus: 15`
- `Assets/Data/Milestones/First50Trees.asset`: `rewardDailyCapBonus: 10` → `rewardDailyFundsBonus: 10`
- `Assets/Data/Milestones/FirstPredator.asset`: `rewardDailyCapBonus: 10` → `rewardDailyFundsBonus: 10`

- [ ] **Step 11: Rewire `SaveManager.cs`**

Change:
```csharp
[SerializeField] private DailyGrant dailyGrant;
```
to:
```csharp
[SerializeField] private EconomyManager economyManager;
```

In `SaveGame()`, change:
```csharp
PlayerSaveData data = new PlayerSaveData
{
    treesPlantedToday = dailyGrant.TreesPlantedToday,
    dailyTreeCap = dailyGrant.DailyTreeCap,
    lastGrantResetTimestamp = dailyGrant.LastGrantResetTimestamp,
    lastSessionTimestamp = DateTime.UtcNow.ToString("o"),
```
to:
```csharp
PlayerSaveData data = new PlayerSaveData
{
    funds = economyManager.Funds,
    lastFundsGrantTimestamp = economyManager.LastFundsGrantTimestamp,
    lastSessionTimestamp = DateTime.UtcNow.ToString("o"),
```

In `LoadGame()`, change:
```csharp
dailyGrant.LoadState(data.treesPlantedToday, data.dailyTreeCap, data.lastGrantResetTimestamp);
```
to:
```csharp
economyManager.LoadState(data.funds, data.lastFundsGrantTimestamp);
```

- [ ] **Step 12: Update `PlayerSaveData.cs`**

Change:
```csharp
public int treesPlantedToday;
public int dailyTreeCap;
public string lastGrantResetTimestamp;
```
to:
```csharp
// Field initializer (not just a default) so an old save file missing this key — from before
// this field existed — deserializes to a reasonable starting balance instead of 0. Numerically
// matches EconomyManager.StartingFunds, though the two aren't linked by reference.
public int funds = 300;
public string lastFundsGrantTimestamp;
```

- [ ] **Step 13: Add `DrawCoinGlyph` to `PixelArtUtility.cs`**

Add a new method right after `DrawGemGlyph`:
```csharp
    public static void DrawGemGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color gem = new Color(0.35f, 0.75f, 0.9f);
        Color gemLight = new Color(0.75f, 0.95f, 1f);

        FillDiamond(canvas, new Vector2(size * 0.5f, size * 0.5f), size * 0.34f, gem, gem, 0);
        FillDiamond(canvas, new Vector2(size * 0.42f, size * 0.42f), size * 0.12f, gemLight, gemLight, 0);
    }

    public static void DrawCoinGlyph(PixelBuffer canvas)
    {
        int size = canvas.Width;
        Color coin = new Color(0.95f, 0.78f, 0.25f);
        Color coinLight = new Color(1f, 0.92f, 0.6f);

        FillCircle(canvas, new Vector2(size * 0.5f, size * 0.5f), size * 0.34f, coin, coin, 0);
        FillCircle(canvas, new Vector2(size * 0.4f, size * 0.4f), size * 0.1f, coinLight, coinLight, 0);
    }
```

- [ ] **Step 14: Rewire `ProfileUI.cs`**

Change:
```csharp
[SerializeField] private DailyGrant dailyGrant;
```
to:
```csharp
[SerializeField] private EconomyManager economyManager;
```

Change:
```csharp
private Label dailyCapValueLabel;
```
to:
```csharp
private Label fundsValueLabel;
```

In `OnEnable()`, change:
```csharp
dailyCapValueLabel = root.Q<Label>("daily-cap-value");
```
to:
```csharp
fundsValueLabel = root.Q<Label>("funds-value");
```

Change:
```csharp
root.Q<VisualElement>("daily-cap-icon").style.backgroundImage = new StyleBackground(
    PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.75f, 0.55f, 0.28f), PixelArtUtility.DrawHourglassGlyph));
```
to:
```csharp
root.Q<VisualElement>("funds-icon").style.backgroundImage = new StyleBackground(
    PixelArtUtility.CreateBadgedGlyphSprite(28, new Color(0.85f, 0.65f, 0.2f), PixelArtUtility.DrawCoinGlyph));
```

In `Refresh()`, change:
```csharp
if (dailyGrant != null)
{
    dailyCapValueLabel.text = $"{dailyGrant.TreesPlantedToday} / {dailyGrant.DailyTreeCap}";
}
```
to:
```csharp
if (economyManager != null)
{
    fundsValueLabel.text = economyManager.Funds.ToString();
}
```

- [ ] **Step 15: Update `Profile.uxml`**

Change:
```xml
                <ui:VisualElement class="stat-card">
                    <ui:VisualElement name="daily-cap-icon" class="stat-icon" />
                    <ui:Label text="Today's Planting" class="stat-label" />
                    <ui:Label name="daily-cap-value" text="0 / 0" class="stat-value" />
                </ui:VisualElement>
```
to:
```xml
                <ui:VisualElement class="stat-card">
                    <ui:VisualElement name="funds-icon" class="stat-icon" />
                    <ui:Label text="Funds" class="stat-label" />
                    <ui:Label name="funds-value" text="0" class="stat-value" />
                </ui:VisualElement>
```

- [ ] **Step 16: Fix up `Assets/Scenes/SampleScene.unity`**

The scene wires these managers together by GUID/fileID, not by class name, so the field renames above need matching edits here or the existing wiring breaks. Four different components already have a `dailyGrant: {fileID: 796134812}` line (that fileID is the `EconomyManager` component, formerly `DailyGrant`, which kept its GUID from the Step 1 rename) — rename all four to `economyManager:`:

```bash
sed -i '' 's/  dailyGrant: {fileID: 796134812}/  economyManager: {fileID: 796134812}/' "Assets/Scenes/SampleScene.unity"
```

Verify exactly 4 lines changed and none remain:
```bash
grep -c "economyManager: {fileID: 796134812}" "Assets/Scenes/SampleScene.unity"   # expect 4
grep -c "dailyGrant:" "Assets/Scenes/SampleScene.unity"                            # expect 0
```

Rename the GameObject's own display name (cosmetic, Hierarchy label only — this line is unique in the file):

Find:
```yaml
  m_Name: DailyGrant
```
Replace with:
```yaml
  m_Name: EconomyManager
```

Three cross-references are genuinely new (they didn't exist before this refactor) and need inserting by hand — this is safe here because the target fileIDs are already confirmed by the existing references above and by cross-checking against `MilestoneManager`'s and `HudUI`'s existing (unrelated) `emergenceSystem: {fileID: 1589978606}` lines, which point at the same `EmergenceSystem` instance these new lines need to reference:

First, in the `EconomyManager` component's own field block (this is the same component renamed in Step 1/2 — its serialized data still shows the old `DailyGrant` field names until Unity re-saves it, which is expected and harmless), find:
```yaml
  treesPlantedToday: 0
  dailyTreeCap: 30
  milestoneCapBonus: 0
  lastGrantResetTimestamp: 
```
Replace with:
```yaml
  emergenceSystem: {fileID: 1589978606}
  funds: 300
  milestoneFundsBonus: 0
  lastFundsGrantTimestamp: 
```

Second, in the `EmergenceSystem` component's field block, find:
```yaml
  currentHerbivoreIndex: 0
  currentPredatorIndex: 0
```
Replace with:
```yaml
  currentHerbivoreIndex: 0
  currentPredatorIndex: 0
  economyManager: {fileID: 796134812}
```

Third, in the `HudUI` component's field block, find:
```yaml
  emergenceSystem: {fileID: 1589978606}
  milestoneManager: {fileID: 150096813}
```
Replace with:
```yaml
  emergenceSystem: {fileID: 1589978606}
  milestoneManager: {fileID: 150096813}
  economyManager: {fileID: 796134812}
```

- [ ] **Step 17: Run the full EditMode test suite**

Run Unity's Test Runner (EditMode) or, from the command line if the project has a CI test entry point configured, the equivalent batch-mode test run. Expected: all tests in `EconomyManagerTests`, `TreePlanterTests`, `EmergenceSystemTests`, `MilestoneManagerTests`, and every other existing EditMode test suite pass, and the project has zero compile errors.

- [ ] **Step 18: Commit**

`git mv` already stages its rename (both the old path's removal and the new path's addition) — do not `git add` the old `DailyGrant*` paths, they no longer exist on disk and `git add` would error on them. Just add the new paths plus everything else touched:

```bash
git add Assets/Scripts/Economy/EconomyManager.cs Assets/Scripts/Economy/EconomyManager.cs.meta \
  Assets/Scripts/Tests/EconomyManagerTests.cs Assets/Scripts/Tests/EconomyManagerTests.cs.meta \
  Assets/Scripts/Vegetation/TreePlanter.cs Assets/Scripts/Tests/TreePlanterTests.cs \
  Assets/Scripts/Ecosystem/EmergenceSystem.cs Assets/Scripts/Tests/EmergenceSystemTests.cs \
  Assets/Scripts/Milestones/MilestoneManager.cs Assets/Scripts/Milestones/MilestoneDefinition.cs \
  Assets/Data/Milestones/AllStartersPresent.asset Assets/Data/Milestones/First10Trees.asset \
  Assets/Data/Milestones/First200Trees.asset Assets/Data/Milestones/First50Trees.asset \
  Assets/Data/Milestones/FirstPredator.asset \
  Assets/Scripts/Save/SaveManager.cs Assets/Scripts/Save/PlayerSaveData.cs \
  Assets/Scripts/UI/ProfileUI.cs Assets/UI/Profile/Profile.uxml \
  Assets/Scripts/UI/PixelArtUtility.cs \
  Assets/Scenes/SampleScene.unity
git commit -m "feat: replace DailyGrant's flat daily cap with a spendable Funds economy"
```

---

### Task 3: HUD Funds badge

**Files:**
- Modify: `Assets/UI/Hud/Hud.uxml`
- Modify: `Assets/UI/Hud/Hud.uss`
- Modify: `Assets/Scripts/UI/HudUI.cs`

**Interfaces:**
- Consumes: `EconomyManager.Funds` (Task 2), `PixelArtUtility.DrawCoinGlyph` (Task 2, Step 13).
- Produces: nothing new consumed by later tasks — this is a leaf UI addition.

- [ ] **Step 1: Add the Funds badge to `Hud.uxml`**

Change:
```xml
        <ui:VisualElement name="top-left-cluster" class="top-left-cluster">
            <ui:VisualElement name="currency-badge" class="currency-badge" picking-mode="Ignore">
                <ui:VisualElement name="gem-icon" class="gem-icon" picking-mode="Ignore" />
                <ui:Label name="gem-count-label" text="0" class="gem-count-label" picking-mode="Ignore" />
            </ui:VisualElement>
```
to:
```xml
        <ui:VisualElement name="top-left-cluster" class="top-left-cluster">
            <ui:VisualElement name="currency-badge" class="currency-badge" picking-mode="Ignore">
                <ui:VisualElement name="gem-icon" class="gem-icon" picking-mode="Ignore" />
                <ui:Label name="gem-count-label" text="0" class="gem-count-label" picking-mode="Ignore" />
            </ui:VisualElement>
            <ui:VisualElement name="funds-badge" class="currency-badge" picking-mode="Ignore">
                <ui:VisualElement name="funds-icon" class="gem-icon" picking-mode="Ignore" />
                <ui:Label name="funds-count-label" text="0" class="gem-count-label" picking-mode="Ignore" />
            </ui:VisualElement>
```

- [ ] **Step 2: Add the gain-animation USS class to `Hud.uss`**

Add after the existing `.gem-float-label--animate` rule:
```css
.gem-float-label--animate {
    opacity: 0;
    translate: 0 -28px;
}

.funds-float-label {
    position: absolute;
    top: 20px;
    left: 265px;
    color: var(--color-gold);
    font-size: var(--font-size-md);
    -unity-font-style: bold;
    opacity: 1;
    translate: 0 0;
    transition-property: opacity, translate;
    transition-duration: 0.75s;
    transition-timing-function: ease-out;
}

.funds-float-label--animate {
    opacity: 0;
    translate: 0 -28px;
}
```

- [ ] **Step 3: Wire the badge in `HudUI.cs`**

Add fields near the existing gem fields:
```csharp
private Label gemCountLabel;
private int lastDisplayedGems = -1;
```
becomes:
```csharp
private Label gemCountLabel;
private int lastDisplayedGems = -1;
private Label fundsCountLabel;
private int lastDisplayedFunds = -1;
```

Add a serialized reference near `[SerializeField] private MilestoneManager milestoneManager;`:
```csharp
[SerializeField] private MilestoneManager milestoneManager;
[SerializeField] private EconomyManager economyManager;
```

In `OnEnable()`, after the existing gem-icon/gem-count-label wiring:
```csharp
VisualElement gemIcon = root.Q<VisualElement>("gem-icon");
gemIcon.style.backgroundImage = new StyleBackground(currencyIconSprite);
gemCountLabel = root.Q<Label>("gem-count-label");
```
add:
```csharp
VisualElement fundsIcon = root.Q<VisualElement>("funds-icon");
fundsIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(16, new Color(0.85f, 0.65f, 0.2f), PixelArtUtility.DrawCoinGlyph));
fundsCountLabel = root.Q<Label>("funds-count-label");
```

At the end of `OnEnable()`, change:
```csharp
RefreshPickerIcon();
RefreshPickerLabel();
RefreshGemCount();
RefreshXp();
minimapRefreshTimer = 0f;
```
to:
```csharp
RefreshPickerIcon();
RefreshPickerLabel();
RefreshGemCount();
RefreshFundsCount();
RefreshXp();
minimapRefreshTimer = 0f;
```

In `Update()`, change:
```csharp
private void Update()
{
    RefreshGemCount();
    RefreshXp();
```
to:
```csharp
private void Update()
{
    RefreshGemCount();
    RefreshFundsCount();
    RefreshXp();
```

Add new methods after `AnimateGemGain`:
```csharp
    private void RefreshFundsCount()
    {
        if (fundsCountLabel == null || economyManager == null)
        {
            return;
        }

        int currentFunds = economyManager.Funds;
        if (currentFunds == lastDisplayedFunds)
        {
            return;
        }

        int previousFunds = lastDisplayedFunds;
        lastDisplayedFunds = currentFunds;
        fundsCountLabel.text = currentFunds.ToString();

        if (previousFunds >= 0 && currentFunds > previousFunds)
        {
            AnimateFundsGain(currentFunds - previousFunds);
        }
    }

    private void AnimateFundsGain(int amount)
    {
        VisualElement fundsBadge = fundsCountLabel?.parent;
        VisualElement hudLayer = fundsBadge?.parent;
        if (hudLayer == null)
        {
            return;
        }

        Label floatingLabel = new Label($"+{amount}");
        floatingLabel.AddToClassList("funds-float-label");
        floatingLabel.pickingMode = PickingMode.Ignore;
        hudLayer.Add(floatingLabel);

        floatingLabel.schedule.Execute(() => floatingLabel.AddToClassList("funds-float-label--animate")).ExecuteLater(10);
        floatingLabel.schedule.Execute(() => floatingLabel.RemoveFromHierarchy()).ExecuteLater(800);
    }
```

- [ ] **Step 4: Manual smoke check**

There's no automated test for HUD Toolkit wiring in this codebase (consistent with `ProfileUI`/`SpeciesSummaryUI`, neither of which have dedicated UI tests) — verify by inspection that `HudUI.cs` compiles (the labels/classes it queries by name now exist in `Hud.uxml`) and that `Hud.uss` has no syntax errors (balanced braces, valid property names matching the file's existing style). Full visual verification happens in Task 6's manual Editor checklist.

- [ ] **Step 5: Commit**

```bash
git add Assets/UI/Hud/Hud.uxml Assets/UI/Hud/Hud.uss Assets/Scripts/UI/HudUI.cs
git commit -m "feat: add a Funds badge to the HUD"
```

---

### Task 4: Floating cost popup

**Files:**
- Modify: `Assets/Scripts/UI/HudUI.cs`
- Modify: `Assets/UI/Hud/Hud.uss`
- Modify: `Assets/Scripts/Vegetation/TreePlanter.cs`
- Modify: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`

**Interfaces:**
- Consumes: `CameraPanController.TargetCamera` (already public), `RuntimePanelUtils.ScreenToPanel` (Unity API, already used elsewhere in this codebase in `CameraPanController.cs`).
- Produces: `HudUI.ShowFloatingCost(Vector3 worldPosition, int cost)` (public void) — this is the only new call site added to `TreePlanter`/`EmergenceSystem` in this task.

- [ ] **Step 1: Add `ShowFloatingCost` to `HudUI.cs`**

Add near `ShowMoveTreeBanner`/`HideMoveTreeBanner` (both public, both cross-system entry points via `HudUI.Instance`):
```csharp
    public void ShowFloatingCost(Vector3 worldPosition, int cost)
    {
        if (uiDocument == null || cameraPanController == null)
        {
            return;
        }

        VisualElement root = uiDocument.rootVisualElement;
        if (root.panel == null)
        {
            return;
        }

        Vector2 screenPosition = cameraPanController.TargetCamera.WorldToScreenPoint(worldPosition);
        Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(root.panel, screenPosition);

        Label floatingLabel = new Label($"-{cost}");
        floatingLabel.AddToClassList("cost-float-label");
        floatingLabel.pickingMode = PickingMode.Ignore;
        floatingLabel.style.left = panelPosition.x;
        floatingLabel.style.top = panelPosition.y;
        root.Add(floatingLabel);

        floatingLabel.schedule.Execute(() => floatingLabel.AddToClassList("cost-float-label--animate")).ExecuteLater(10);
        floatingLabel.schedule.Execute(() => floatingLabel.RemoveFromHierarchy()).ExecuteLater(800);
    }
```

- [ ] **Step 2: Add the popup's USS classes to `Hud.uss`**

Add at the end of the file:
```css
.cost-float-label {
    position: absolute;
    color: var(--color-danger-light);
    font-size: var(--font-size-md);
    -unity-font-style: bold;
    -unity-text-outline-width: 1;
    -unity-text-outline-color: var(--color-ink);
    opacity: 1;
    translate: 0 0;
    transition-property: opacity, translate;
    transition-duration: 0.75s;
    transition-timing-function: ease-out;
}

.cost-float-label--animate {
    opacity: 0;
    translate: 0 -28px;
}
```

- [ ] **Step 3: Call it from `TreePlanter.PlantTree`**

Change:
```csharp
        PlantedTree plantedTree = SpawnRestoredTree(vegetation, position);
        plantedTree.Initialize(vegetation);

        EcosystemState.RegisterTreePlanted(vegetation.id);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerTreePlanted);
        VfxUtility.SpawnBurst(position, vegetation.foliageColor);
        Handheld.Vibrate();
```
to:
```csharp
        PlantedTree plantedTree = SpawnRestoredTree(vegetation, position);
        plantedTree.Initialize(vegetation);

        EcosystemState.RegisterTreePlanted(vegetation.id);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerTreePlanted);
        VfxUtility.SpawnBurst(position, vegetation.foliageColor);
        HudUI.Instance?.ShowFloatingCost(position, vegetation.plantCost);
        Handheld.Vibrate();
```

- [ ] **Step 4: Call it from `EmergenceSystem.TryReleaseSpeciesAt`**

Change:
```csharp
        SpawnIndividual(species, position);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased);
        return true;
    }
```
(the one inside `TryReleaseSpeciesAt`, not `TryReleaseSpecies`) to:
```csharp
        SpawnIndividual(species, position);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased);
        HudUI.Instance?.ShowFloatingCost(position, species.releaseCost);
        return true;
    }
```

- [ ] **Step 5: Run the full EditMode test suite**

No test assertions cover `HudUI.ShowFloatingCost` directly (it's UI Toolkit chrome, consistent with the rest of `HudUI`'s untested methods), but confirm `TreePlanterTests` and `EmergenceSystemTests` from Task 2 still pass unchanged — `HudUI.Instance` is null in EditMode tests (no scene loaded), so `HudUI.Instance?.ShowFloatingCost(...)` is a no-op there, exactly like the existing `HudUI.Instance?.ShowLandLockedPopup()` calls these same methods already make.

- [ ] **Step 6: Commit**

```bash
git add Assets/Scripts/UI/HudUI.cs Assets/UI/Hud/Hud.uss Assets/Scripts/Vegetation/TreePlanter.cs Assets/Scripts/Ecosystem/EmergenceSystem.cs
git commit -m "feat: show a fading -cost popup at the tap point when planting or releasing"
```

---

### Task 5: Store panel

**Files:**
- Create: `Assets/UI/Store/Store.uxml`
- Create: `Assets/UI/Store/Store.uss`
- Create: `Assets/Scripts/UI/StoreUI.cs`
- Modify: `Assets/Scripts/Vegetation/TreePlanter.cs`
- Modify: `Assets/Scripts/Tests/TreePlanterTests.cs`
- Modify: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`
- Modify: `Assets/Scripts/Tests/EmergenceSystemTests.cs`
- Modify: `Assets/Scripts/UI/HudUI.cs`
- Modify: `Assets/UI/Hud/Hud.uxml`
- Modify: `Assets/Scripts/UI/ScreenNavigator.cs`

**Interfaces:**
- Consumes: `TreePlanter.IsVegetationUnlocked` (existing), `EmergenceSystem.IsSpeciesUnlocked` (existing), `EmergenceSystem.GetSpeciesIconSprite` (existing static), `PixelArtUtility.CreateVegetationSprite` (existing), `EconomyManager.CanAfford` (Task 2), `PanelAnimator.Open`/`Close` (existing).
- Produces: `TreePlanter.SelectVegetation(VegetationDefinition vegetation)`, `EmergenceSystem.SelectReleasableSpecies(SpeciesDefinition species)`, `HudUI.SetPickerCategory(HudUI.PickerCategory category)`, `ScreenNavigator.OpenStore()` — all leaf additions, nothing later depends on them.

- [ ] **Step 1: Write the failing tests for `TreePlanter.SelectVegetation`**

In `Assets/Scripts/Tests/TreePlanterTests.cs`, add after `CycleVegetation_WrapsAroundUnlockedList`:
```csharp
[Test]
public void SelectVegetation_SetsCurrentVegetationToMatch()
{
    planter.AddUnlockedVegetation(pine);

    planter.SelectVegetation(pine);

    Assert.AreEqual("pine", planter.CurrentVegetation.id);
}

[Test]
public void SelectVegetation_NotUnlocked_LeavesCurrentVegetationUnchanged()
{
    planter.SelectVegetation(pine); // pine not yet unlocked

    Assert.AreEqual("oak", planter.CurrentVegetation.id);
}
```

- [ ] **Step 2: Run the tests to see them fail**

Run: the `TreePlanterTests` fixture in Unity's Test Runner (EditMode).
Expected: FAIL — `SelectVegetation` does not exist on `TreePlanter` yet (compile error).

- [ ] **Step 3: Implement `TreePlanter.SelectVegetation`**

Add to `Assets/Scripts/Vegetation/TreePlanter.cs`, right after `CycleVegetation()`:
```csharp
    public void SelectVegetation(VegetationDefinition vegetation)
    {
        int index = unlockedVegetation.IndexOf(vegetation);
        if (index >= 0)
        {
            currentVegetationIndex = index;
        }
    }
```

- [ ] **Step 4: Run the tests to see them pass**

Run: the `TreePlanterTests` fixture.
Expected: PASS.

- [ ] **Step 5: Write the failing tests for `EmergenceSystem.SelectReleasableSpecies`**

In `Assets/Scripts/Tests/EmergenceSystemTests.cs`, add after `CycleReleasableSpecies_PredatorIndexIndependentOfHerbivoreIndex`:
```csharp
[Test]
public void SelectReleasableSpecies_Herbivore_SetsCurrentSpeciesToMatch()
{
    SpeciesDefinition herbivore2 = ScriptableObject.CreateInstance<SpeciesDefinition>();
    herbivore2.id = "test_herbivore2";
    herbivore2.category = SpeciesDefinition.SpeciesCategory.Herbivore;

    system.AddUnlockedSpecies(herbivore);
    system.AddUnlockedSpecies(herbivore2);

    system.SelectReleasableSpecies(herbivore2);

    Assert.AreEqual(herbivore2, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));

    Object.DestroyImmediate(herbivore2);
}

[Test]
public void SelectReleasableSpecies_NotUnlocked_LeavesSelectionUnchanged()
{
    system.AddUnlockedSpecies(herbivore);

    SpeciesDefinition notUnlocked = ScriptableObject.CreateInstance<SpeciesDefinition>();
    notUnlocked.id = "test_not_unlocked";
    notUnlocked.category = SpeciesDefinition.SpeciesCategory.Herbivore;

    system.SelectReleasableSpecies(notUnlocked);

    Assert.AreEqual(herbivore, system.CurrentReleasableSpecies(SpeciesDefinition.SpeciesCategory.Herbivore));

    Object.DestroyImmediate(notUnlocked);
}
```

- [ ] **Step 6: Run the tests to see them fail**

Run: the `EmergenceSystemTests` fixture.
Expected: FAIL — `SelectReleasableSpecies` does not exist on `EmergenceSystem` yet (compile error).

- [ ] **Step 7: Implement `EmergenceSystem.SelectReleasableSpecies`**

Add to `Assets/Scripts/Ecosystem/EmergenceSystem.cs`, right after `CycleReleasableSpecies`:
```csharp
    public void SelectReleasableSpecies(SpeciesDefinition species)
    {
        if (species == null)
        {
            return;
        }

        List<SpeciesDefinition> list = ListForCategory(species.category);
        int index = list.IndexOf(species);
        if (index < 0)
        {
            return;
        }

        if (species.category == SpeciesDefinition.SpeciesCategory.Predator)
        {
            currentPredatorIndex = index;
        }
        else
        {
            currentHerbivoreIndex = index;
        }
    }
```

- [ ] **Step 8: Run the tests to see them pass**

Run: the `EmergenceSystemTests` fixture.
Expected: PASS.

- [ ] **Step 9: Add `HudUI.SetPickerCategory`**

Add to `Assets/Scripts/UI/HudUI.cs`, right after `CurrentPickerSpecies()`:
```csharp
    public void SetPickerCategory(PickerCategory category)
    {
        currentPickerCategory = category;
        RefreshPickerIcon();
        RefreshPickerLabel();
    }
```

- [ ] **Step 10: Create `Store.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" editor-extension-mode="False">
    <ui:Style src="project://database/Assets/UI/Theme/Theme.uss" />
    <ui:Style src="project://database/Assets/UI/Store/Store.uss" />
    <ui:VisualElement name="store-root" class="store-root theme-scope">
        <ui:VisualElement name="store-frame" class="store-frame">
            <ui:VisualElement name="store-header" class="store-header">
                <ui:Label name="store-title" text="Store" class="store-title" />
                <ui:Button name="close-button" text="X" class="store-close-button" />
            </ui:VisualElement>
            <ui:ScrollView name="store-scroll" class="store-scroll">
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
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 11: Create `Store.uss`**

```css
.store-root {
    width: 100%;
    height: 100%;
    background-color: rgba(6, 8, 14, 0.85);
    padding: var(--space-sm);
    align-items: center;
    justify-content: center;
}

.store-frame {
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

.store-header {
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

.store-title {
    font-size: var(--font-size-lg);
    color: rgb(250, 235, 210);
    -unity-font-style: bold;
    -unity-text-outline-width: 1;
    -unity-text-outline-color: var(--color-ink);
}

.store-close-button {
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

.store-close-button:active {
    scale: 0.92 0.92;
}

.store-scroll {
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

.summary-row--unaffordable {
    opacity: 0.5;
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

.summary-row-cost {
    font-size: var(--font-size-md);
    color: var(--color-gold);
    -unity-font-style: bold;
    -unity-text-outline-width: 1;
    -unity-text-outline-color: var(--color-ink);
}
```

- [ ] **Step 12: Create `StoreUI.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class StoreUI : MonoBehaviour
{
    private const float RefreshInterval = 0.5f;

    [SerializeField] private List<VegetationDefinition> allVegetation = new List<VegetationDefinition>();
    [SerializeField] private List<SpeciesDefinition> allSpecies = new List<SpeciesDefinition>();
    [SerializeField] private TreePlanter treePlanter;
    [SerializeField] private EmergenceSystem emergenceSystem;
    [SerializeField] private EconomyManager economyManager;
    [SerializeField] private ScreenNavigator screenNavigator;

    private UIDocument uiDocument;
    private VisualElement storeRoot;
    private VisualElement storeFrame;
    private Button treesHeaderButton;
    private VisualElement treesRowList;
    private Button herbivoresHeaderButton;
    private VisualElement herbivoresRowList;
    private Button predatorsHeaderButton;
    private VisualElement predatorsRowList;
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
        storeRoot = root.Q<VisualElement>("store-root");
        storeFrame = root.Q<VisualElement>("store-frame");

        ScrollView storeScroll = root.Q<ScrollView>("store-scroll");
        storeScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        storeScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

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

        storeRoot.style.display = DisplayStyle.None;
    }

    private void OnDisable()
    {
        IsOpen = false;
    }

    private void Update()
    {
        if (storeRoot == null || storeRoot.style.display != DisplayStyle.Flex)
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
            PanelAnimator.Open(storeRoot, storeFrame);
        }
        else
        {
            PanelAnimator.Close(storeRoot, storeFrame);
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

        int count = 0;
        foreach (VegetationDefinition vegetation in allVegetation)
        {
            if (vegetation == null || treePlanter == null || !treePlanter.IsVegetationUnlocked(vegetation.id))
            {
                continue;
            }

            count++;
            Sprite icon = vegetation.heroSprite != null
                ? vegetation.heroSprite
                : PixelArtUtility.CreateVegetationSprite(48, vegetation.plantSilhouette, vegetation.foliageColor, vegetation.trunkColor, vegetation.id.GetHashCode());

            VegetationDefinition capturedVegetation = vegetation;
            treesRowList.Add(CreateRow(icon, vegetation.displayName, vegetation.plantCost, () => SelectVegetation(capturedVegetation)));
        }

        treesHeaderButton.text = $"Trees ({count})";
    }

    private void RefreshSpeciesSection(SpeciesDefinition.SpeciesCategory category, VisualElement rowList, Button headerButton, string categoryLabel)
    {
        rowList.Clear();

        int count = 0;
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species == null || species.category != category || emergenceSystem == null || !emergenceSystem.IsSpeciesUnlocked(species.id))
            {
                continue;
            }

            count++;
            Sprite icon = EmergenceSystem.GetSpeciesIconSprite(species);
            SpeciesDefinition capturedSpecies = species;
            rowList.Add(CreateRow(icon, species.displayName, species.releaseCost, () => SelectSpecies(capturedSpecies)));
        }

        headerButton.text = $"{categoryLabel} ({count})";
    }

    private VisualElement CreateRow(Sprite icon, string displayName, int cost, System.Action onClick)
    {
        Button row = new Button(onClick);
        row.AddToClassList("summary-row");

        if (economyManager != null && !economyManager.CanAfford(cost))
        {
            row.AddToClassList("summary-row--unaffordable");
        }

        VisualElement iconElement = new VisualElement();
        iconElement.AddToClassList("summary-row-icon");
        iconElement.style.backgroundImage = new StyleBackground(icon);
        row.Add(iconElement);

        Label nameLabel = new Label(displayName);
        nameLabel.AddToClassList("summary-row-name");
        row.Add(nameLabel);

        Label costLabel = new Label(cost.ToString());
        costLabel.AddToClassList("summary-row-cost");
        row.Add(costLabel);

        return row;
    }

    private void SelectVegetation(VegetationDefinition vegetation)
    {
        AudioManager.Instance?.PlayTap();

        treePlanter?.SelectVegetation(vegetation);
        HudUI.Instance?.SetPickerCategory(HudUI.PickerCategory.Plant);

        OnCloseButtonClicked();
    }

    private void SelectSpecies(SpeciesDefinition species)
    {
        AudioManager.Instance?.PlayTap();

        emergenceSystem?.SelectReleasableSpecies(species);
        HudUI.PickerCategory category = species.category == SpeciesDefinition.SpeciesCategory.Predator
            ? HudUI.PickerCategory.Predator
            : HudUI.PickerCategory.Herbivore;
        HudUI.Instance?.SetPickerCategory(category);

        OnCloseButtonClicked();
    }
}
```

- [ ] **Step 13: Add the Store button to `Hud.uxml`**

Change:
```xml
        <ui:VisualElement name="action-cluster" class="action-cluster">
            <ui:Button name="lab-button" class="icon-button" />
            <ui:Button name="codex-button" class="icon-button" />
            <ui:Button name="profile-button" class="icon-button" />
            <ui:Button name="species-summary-button" class="icon-button" />
            <ui:Button name="settings-button" class="icon-button" />
        </ui:VisualElement>
```
to:
```xml
        <ui:VisualElement name="action-cluster" class="action-cluster">
            <ui:Button name="lab-button" class="icon-button" />
            <ui:Button name="codex-button" class="icon-button" />
            <ui:Button name="profile-button" class="icon-button" />
            <ui:Button name="species-summary-button" class="icon-button" />
            <ui:Button name="store-button" class="icon-button" />
            <ui:Button name="settings-button" class="icon-button" />
        </ui:VisualElement>
```

- [ ] **Step 14: Wire the Store button in `HudUI.cs`**

Add a field near `[SerializeField] private SpeciesSummaryUI speciesSummaryUI;`:
```csharp
[SerializeField] private SpeciesSummaryUI speciesSummaryUI;
[SerializeField] private StoreUI storeUI;
```

In `OnEnable()`, after the `speciesSummaryButton` wiring:
```csharp
Button speciesSummaryButton = root.Q<Button>("species-summary-button");
speciesSummaryButton.clicked += OnSpeciesSummaryButtonClicked;
speciesSummaryButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.45f, 0.58f, 0.32f), PixelArtUtility.DrawPawGlyph));
```
add:
```csharp
Button storeButton = root.Q<Button>("store-button");
storeButton.clicked += OnStoreButtonClicked;
storeButton.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateBadgedGlyphSprite(IconSize, new Color(0.8f, 0.6f, 0.15f), PixelArtUtility.DrawCoinGlyph));
```

Add a click handler after `OnSpeciesSummaryButtonClicked`:
```csharp
    private void OnStoreButtonClicked()
    {
        AudioManager.Instance?.PlayTap();

        if (screenNavigator != null)
        {
            screenNavigator.OpenStore();
        }
        else if (storeUI != null)
        {
            storeUI.SetOpen(true);
        }
    }
```

- [ ] **Step 15: Wire `ScreenNavigator.cs`**

Add a field:
```csharp
[SerializeField] private SpeciesSummaryUI speciesSummaryUI;
[SerializeField] private TreeBoostUI treeBoostUI;
```
becomes:
```csharp
[SerializeField] private SpeciesSummaryUI speciesSummaryUI;
[SerializeField] private StoreUI storeUI;
[SerializeField] private TreeBoostUI treeBoostUI;
```

Add a method after `OpenSpeciesSummary()`:
```csharp
    public void OpenStore()
    {
        CloseOtherPanels(storeUI);
        if (storeUI != null) storeUI.SetOpen(true);
        SetHomeToolbarVisible(false);
    }
```

In `CloseOtherPanels(object except)`, add after the `speciesSummaryUI` block:
```csharp
        if (speciesSummaryUI != null && !ReferenceEquals(speciesSummaryUI, except))
        {
            speciesSummaryUI.SetOpen(false);
        }

        if (storeUI != null && !ReferenceEquals(storeUI, except))
        {
            storeUI.SetOpen(false);
        }
```

- [ ] **Step 16: Run the full EditMode test suite**

Confirm every test from Tasks 2-5 still passes and the project compiles (this task adds two new UI-only files with no test coverage of their own, consistent with `SpeciesSummaryUI`/`ProfileUI` — but they must still compile against `TreePlanter`, `EmergenceSystem`, `HudUI`, `EconomyManager`, `ScreenNavigator`'s current public surface).

- [ ] **Step 17: Commit**

```bash
git add Assets/UI/Store Assets/Scripts/UI/StoreUI.cs \
  Assets/Scripts/Vegetation/TreePlanter.cs Assets/Scripts/Tests/TreePlanterTests.cs \
  Assets/Scripts/Ecosystem/EmergenceSystem.cs Assets/Scripts/Tests/EmergenceSystemTests.cs \
  Assets/Scripts/UI/HudUI.cs Assets/UI/Hud/Hud.uxml Assets/Scripts/UI/ScreenNavigator.cs
git commit -m "feat: add a Store panel to browse and select unlocked trees and animals by cost"
```

---

### Task 6: Manual Unity Editor setup (not agent-executable)

Everything through Task 5 is done once the project compiles and its tests pass — but three things in this feature genuinely require the Unity Editor GUI and cannot be done by editing text files blindly: importing new assets, verifying the hand-edited scene YAML from Task 2 actually deserializes as intended, and wiring the brand-new `StoreUI` GameObject (which needs a `UIDocument` + `PanelSettings` + a scene-specific hierarchy position that doesn't exist yet). Whoever picks this up next should do the following in the Editor, in order:

- [ ] **Step 1:** Open the project in Unity and let it finish reimporting. This generates `.meta` files for every newly created asset from Tasks 2-5 (`EconomyManagerTests.cs`, `StoreUI.cs`, `Store.uxml`, `Store.uss`) — expected and automatic, nothing to configure.

- [ ] **Step 2:** Open `Assets/Scenes/SampleScene.unity` and find the GameObject now named "EconomyManager" (Task 2 renamed it from "DailyGrant"). Confirm in the Inspector: `Emergence System` field is populated (not "None"), `Funds` reads `300`, `Milestone Funds Bonus` reads `0`.

- [ ] **Step 3:** Select the `TreePlanter`, `MilestoneManager`, `SaveManager`, and `ProfileUI` GameObjects in turn. Confirm each now shows an "Economy Manager" field (renamed from "Daily Grant") pointing at the same EconomyManager GameObject from Step 2 — these should already be correctly wired from Task 2's scene edit, but verify none show "None" (a `None` here means the hand-edit in Task 2 Step 16 needs re-checking against the exact `fileID`s in the scene file).

- [ ] **Step 4:** Select the `EmergenceSystem` and `HudUI` GameObjects. Confirm each now shows a new "Economy Manager" field pointing at the same EconomyManager GameObject (also from Task 2's scene edit — verify, don't assume).

- [ ] **Step 5:** Create the Store panel GameObject. The simplest path: duplicate the existing "SpeciesSummary" UIDocument GameObject in the Hierarchy, rename the duplicate to "Store", remove its `SpeciesSummaryUI` component, add a `StoreUI` component in its place, and on the duplicated `UIDocument` component change **Source Asset** to `Assets/UI/Store/Store.uxml`.

- [ ] **Step 6:** On the new `StoreUI` component, assign: **All Vegetation** and **All Species** to the same lists `ProfileUI`/`SpeciesSummaryUI` already use (drag the same `VegetationDefinition`/`SpeciesDefinition` assets, or copy the list from `SpeciesSummaryUI`'s Inspector), **Tree Planter**, **Emergence System**, **Economy Manager**, and **Screen Navigator** to the same singleton GameObjects every other panel controller already references. Also select the `CameraPanController` GameObject and add the new Store `UIDocument` component to its **Blocking Ui Documents** list in the Inspector, exactly like every other panel (SpeciesSummary, Codex, Profile, etc.) is already registered there — otherwise tapping a Store row also falls through and places/spends at that screen point on the world underneath it.

- [ ] **Step 7:** Select the `ScreenNavigator` GameObject and assign its new **Store UI** field to the Store GameObject from Step 5.

- [ ] **Step 8:** Select the `HudUI` GameObject and assign its new **Store UI** field (the fallback direct reference) to the same Store GameObject — mirrors how `HudUI` already keeps a fallback reference to every other panel alongside routing through `ScreenNavigator`.

- [ ] **Step 9:** Enter Play Mode and verify, in order:
  - The Funds badge in the top-left HUD cluster reads `300`.
  - Tapping the ground with a tree selected plants it, the Funds badge drops by the tree's cost, and a fading `-10` appears at the tap point.
  - Opening the Store (new toolbar button) lists Oak/Pine under Trees, Squirrel/Gazelle under Herbivores, Fox/Wolf under Predators, each with the right cost; tapping a row switches the HUD's picker to that item and closes the Store.
  - Spending down to fewer Funds than an item's cost dims that item's Store row and blocks placement (rejection sound, no popup, no spend) when tapping the ground with it selected.
  - The `EconomyManager` GameObject's **Debug: Simulate Next Day** context menu action grants a new day's Funds and the badge shows a `+N` gain animation.
  - The Profile panel's "Funds" row matches the HUD badge.

- [ ] **Step 10:** Save the scene (`File > Save`) once everything checks out, and commit it:
```bash
git add Assets/Scenes/SampleScene.unity
git commit -m "chore: wire the Store panel into the scene"
```
(This commit is separate from Task 5's because the Store GameObject can only be created inside the Editor — everything else in this checklist was already correct from Task 2's hand-edited scene changes and needs no further commit if Step 9 passes without surprises.)
