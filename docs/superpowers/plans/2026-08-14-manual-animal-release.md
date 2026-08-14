# Manual Animal Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `EmergenceSystem`'s automatic, probability-roll, tick-based animal spawning with a deterministic `CanRelease`/`TryReleaseSpecies` API the player triggers directly, reusing the exact ecological criteria (prey population thresholds, tree-count-based caps) the old tick already computed.

**Architecture:** Delete the tick-timer machinery (`Update()`, `ConsumeTicks`, `AdvanceTicks`, `tickIntervalSeconds`/`tickTimer`, `EmergenceTickCount`) and the two probability-roll methods (`RunEmergenceTick`, `RunPredatorEmergence`) from `EmergenceSystem.cs`, replacing them with two new public methods that reuse the same criteria checks minus the dice roll. Everything downstream of "a species population increased" (`SpawnIndividual`, `SpawnCreatureVisual`, `SpawnMissingCreatureVisuals`, `MilestoneManager`'s checks) is untouched. `DemoUI` gets four per-species "Release" buttons in place of the old "Force Emergence Tick" button, as the minimal trigger for this stage (the real player-facing picker + tap-to-place UI is a later stage).

**Tech Stack:** Unity 6000.5.6f1, C# (NUnit EditMode tests via `mcp__UnityMCP__run_tests`), UI Toolkit (UXML), UnityMCP `execute_code`/`refresh_unity` tools.

## Global Constraints

- Final roster (unchanged from the prior stage): Oak/Pine vegetation; Gazelle/Squirrel herbivores; Wolf/Fox predators.
- Herbivore release criteria: `IsVegetationSatisfied(species)` (unchanged helper) AND, if `treesPerIndividualCap > 0`, `EcosystemState.GetSpeciesPopulation(species.id) < EcosystemState.TotalTreeCount / species.treesPerIndividualCap`. A `treesPerIndividualCap` of 0 means uncapped.
- Predator release criteria: `totalPrey = sum of EcosystemState.GetSpeciesPopulation(preyId) for each preySpeciesIds` must be `>= preyThreshold`, AND current population `< totalPrey / preyToPredatorRatio` (integer floor division, matching the removed code's `Mathf.FloorToInt`).
- `baseEmergenceWeight` is not read by the new logic — leave it as an unused field on `SpeciesDefinition` and the existing assets, no cleanup (matches the prior stage's precedent of leaving other now-orphaned fields/methods in place rather than churning unrelated cleanup into a feature commit).
- One release = one individual. No gem cost, no daily cap — the criteria are the only gate.
- Release feedback matches tree-planting's existing pattern: `AudioManager.Instance?.PlayPlant()` and an XP grant. The VFX burst is already covered by the existing `SpawnCreatureVisual` call inside `SpawnIndividual` (`playSpawnEffects: true` by default) — do not add a second one.
- Placement position stays `GetRandomTreePosition` (a random existing tree's spot) — unchanged in this stage.
- Commit hygiene: this project's working tree may contain unrelated uncommitted files at any time. Never use `git add -A`. Stage only the exact files each task lists.
- After any C# script change, force a Unity recompile and confirm zero console errors before running tests (`mcp__UnityMCP__refresh_unity` with `compile: request`, then `mcp__UnityMCP__read_console` filtered to `error`).
- Run the full EditMode suite (`mcp__UnityMCP__run_tests`, `mode: EditMode`) after each task. Four pre-existing `EmergenceSystemTests.SpawnMissingCreatureVisuals_*` failures predate this plan (a stale/timing issue unrelated to the tick mechanism being replaced) — do not treat those as regressions, but treat any *new* failure as one. Do not attempt to fix them as part of this plan; they were investigated and found not to be a quick fix.

---

### Task 1: Replace the tick mechanism with `CanRelease`/`TryReleaseSpecies`

**Files:**
- Modify: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`
- Modify: `Assets/Scripts/Save/SaveManager.cs`
- Modify: `Assets/Scripts/Milestones/PlayerLevel.cs`
- Modify: `Assets/Scripts/Tests/EmergenceSystemTests.cs`

**Interfaces:**
- Produces: `public bool CanRelease(SpeciesDefinition species)` and `public bool TryReleaseSpecies(SpeciesDefinition species)` on `EmergenceSystem` — Task 2's `DemoUI` buttons call `TryReleaseSpecies` directly.
- Removes (no longer available to any caller): `EmergenceSystem.AdvanceTicks(float)`, `.EmergenceTickCount`, `.ForceEmergenceTick()`.

- [ ] **Step 1: Write the failing tests**

Add to `Assets/Scripts/Tests/EmergenceSystemTests.cs`, replacing the 5 `AdvanceTicks_*` tests and their `SetTickIntervalSeconds` helper (delete all 6 — the mechanism they test no longer exists) with:

```csharp
    [Test]
    public void CanRelease_Herbivore_BlockedByPopulationCap()
    {
        herbivore.treesPerIndividualCap = 10;
        EcosystemState.RegisterTreePlanted("oak"); // 1 tree, capacity = 1/10 = 0

        Assert.IsFalse(system.CanRelease(herbivore));
    }

    [Test]
    public void CanRelease_Herbivore_BlockedByMissingRequiredVegetation()
    {
        herbivore.requiredVegetationIds = new[] { "pine" };
        // no vegetation registered

        Assert.IsFalse(system.CanRelease(herbivore));
    }

    [Test]
    public void CanRelease_Herbivore_SatisfiedCriteria_ReturnsTrue()
    {
        herbivore.treesPerIndividualCap = 1;
        EcosystemState.RegisterTreePlanted("oak");

        Assert.IsTrue(system.CanRelease(herbivore));
    }

    [Test]
    public void TryReleaseSpecies_Herbivore_Succeeds_IncrementsPopulation()
    {
        herbivore.treesPerIndividualCap = 1;
        EcosystemState.RegisterTreePlanted("oak");

        bool released = system.TryReleaseSpecies(herbivore);

        Assert.IsTrue(released);
        Assert.AreEqual(1, EcosystemState.GetSpeciesPopulation("test_herbivore"));
    }

    [Test]
    public void TryReleaseSpecies_UnmetCriteria_ReturnsFalseAndDoesNotChangePopulation()
    {
        herbivore.treesPerIndividualCap = 10;
        EcosystemState.RegisterTreePlanted("oak"); // capacity = 1/10 = 0

        bool released = system.TryReleaseSpecies(herbivore);

        Assert.IsFalse(released);
        Assert.AreEqual(0, EcosystemState.GetSpeciesPopulation("test_herbivore"));
    }

    [Test]
    public void CanRelease_Predator_BlockedByPreyThreshold()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 1;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 3); // below threshold

        Assert.IsFalse(system.CanRelease(predator));
    }

    [Test]
    public void CanRelease_Predator_BlockedByPreyToPredatorRatio()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 10;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 5); // meets threshold, capacity = 5/10 = 0

        Assert.IsFalse(system.CanRelease(predator));
    }

    [Test]
    public void CanRelease_Predator_SatisfiedCriteria_ReturnsTrue()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 1;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 5); // capacity = 5/1 = 5, 0 < 5

        Assert.IsTrue(system.CanRelease(predator));
    }

    [Test]
    public void TryReleaseSpecies_Predator_Succeeds_IncrementsPopulation()
    {
        predator.preySpeciesIds = new[] { "test_herbivore" };
        predator.preyThreshold = 5;
        predator.preyToPredatorRatio = 1;
        EcosystemState.SetSpeciesPopulation("test_herbivore", 5);

        bool released = system.TryReleaseSpecies(predator);

        Assert.IsTrue(released);
        Assert.AreEqual(1, EcosystemState.GetSpeciesPopulation("test_predator"));
    }
```

Delete these 6 members entirely from the test file: `AdvanceTicks_ElapsedTimeLessThanOneInterval_DoesNotRunTick`, `AdvanceTicks_ElapsedTimeCoversMultipleIntervals_RunsThatManyTicksAtOnce`, `AdvanceTicks_RemainderFromAnEarlierCallCarriesOverInsteadOfBeingLost`, `AdvanceTicks_ZeroTickInterval_DoesNotHangAndRunsNoTicks`, `AdvanceTicks_NegativeTickInterval_DoesNotHangAndRunsNoTicks`, and the `SetTickIntervalSeconds` private helper method.

- [ ] **Step 2: Run tests to verify the new ones fail**

Run `mcp__UnityMCP__refresh_unity` (`mode: force`, `scope: scripts`, `compile: request`, `wait_for_ready: true`) so Unity picks up the test file edit, then run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_names: ["EmergenceSystemTests"]`, `include_failed_tests: true`.
Expected: the 9 new tests FAIL to compile or FAIL (`CanRelease`/`TryReleaseSpecies` don't exist yet on `EmergenceSystem`). The pre-existing `SpawnMissingCreatureVisuals_*` failures (4 of them) are still present and unrelated — ignore them for this check.

- [ ] **Step 3: Replace the tick mechanism in `EmergenceSystem.cs`**

Delete these members entirely: the constants `DefaultTickIntervalSeconds`, `TreeCountDivisor`, `VegetationDiversityStep`, `NonPredatorChanceCeiling`, `PredatorChanceCeiling`, `PredatorPreyRatioCeiling`; the fields `tickIntervalSeconds` and `tickTimer`; the `EmergenceTickCount` property; the `Update()` method; the `AdvanceTicks(float)` method; the `ConsumeTicks(float)` method; the `RunEmergenceTick()` method; the `RunPredatorEmergence(IReadOnlyList<PlantedTree>)` method; the `[ContextMenu("Debug: Force Emergence Tick")] ForceEmergenceTick()` method at the bottom of the file.

Add these two new public methods in their place (a reasonable location: right after `GetUnlockedSpeciesIds()`, before the deleted `Update()`'s old position):

```csharp
    // Deterministic replacement for the old per-tick probability roll: given the same criteria
    // (prey population for predators, tree-count-derived capacity for herbivores), this either
    // is or isn't currently satisfied - no randomness. baseEmergenceWeight is intentionally not
    // read here; it only ever fed the removed probability roll.
    public bool CanRelease(SpeciesDefinition species)
    {
        if (species == null)
        {
            return false;
        }

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

            int predatorCapacity = Mathf.FloorToInt(totalPrey / (float)species.preyToPredatorRatio);
            int currentPredatorPopulation = EcosystemState.GetSpeciesPopulation(species.id);

            return totalPrey >= species.preyThreshold && currentPredatorPopulation < predatorCapacity;
        }

        if (!IsVegetationSatisfied(species))
        {
            return false;
        }

        if (species.treesPerIndividualCap > 0)
        {
            int populationCapacity = EcosystemState.TotalTreeCount / species.treesPerIndividualCap;
            return EcosystemState.GetSpeciesPopulation(species.id) < populationCapacity;
        }

        return true;
    }

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

        SpawnIndividual(species, PlantedTree.ActiveTrees);
        AudioManager.Instance?.PlayPlant();
        PlayerLevel.Instance?.AddXp(PlayerLevel.XpPerAnimalReleased);
        return true;
    }
```

- [ ] **Step 4: Remove `SaveManager`'s offline catch-up call**

In `Assets/Scripts/Save/SaveManager.cs`, delete line 156, `emergenceSystem.AdvanceTicks((float)offlineSeconds);`. Leave line 157, `emergenceSystem.SpawnMissingCreatureVisuals();`, exactly as-is (it doesn't depend on the deleted line and still matters — restoring visuals for saved population counts).

- [ ] **Step 5: Add the XP constant**

In `Assets/Scripts/Milestones/PlayerLevel.cs`, change:
```csharp
    public const int XpPerTreePlanted = 5;
    public const int XpPerUnlock = 20;
    public const int XpPerMilestone = 50;
```
to:
```csharp
    public const int XpPerTreePlanted = 5;
    public const int XpPerAnimalReleased = 5;
    public const int XpPerUnlock = 20;
    public const int XpPerMilestone = 50;
```

- [ ] **Step 6: Refresh Unity and confirm zero compile errors**

Run `mcp__UnityMCP__refresh_unity` (`mode: force`, `scope: all`, `compile: request`, `wait_for_ready: true`), then `mcp__UnityMCP__read_console` filtered to `error`.
Expected: zero errors. (`DemoUI.cs` still references `ForceEmergenceTick()`, which no longer exists — this WILL cause a compile error until Task 2 fixes it. If Task 2 hasn't run yet, this step will show that one error; if so, that's expected and not a defect in this task — note it in your report and do not attempt to fix `DemoUI.cs` here, that's Task 2's job.)

- [ ] **Step 7: Run tests to verify they pass**

Run: `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `test_names: ["EmergenceSystemTests"]`, `include_failed_tests: true`.
Expected: all 9 new tests PASS. The same 4 pre-existing `SpawnMissingCreatureVisuals_*` failures remain (unrelated, unchanged). If `DemoUI.cs` is still broken (Task 2 not yet done), the test *runner* may fail to compile the whole project rather than reporting individual results — if so, this step cannot fully pass until Task 2 is also done; note this clearly rather than reporting a false pass.

- [ ] **Step 8: Commit**

Stage only: `Assets/Scripts/Ecosystem/EmergenceSystem.cs`, `Assets/Scripts/Save/SaveManager.cs`, `Assets/Scripts/Milestones/PlayerLevel.cs`, `Assets/Scripts/Tests/EmergenceSystemTests.cs`.

```bash
git add Assets/Scripts/Ecosystem/EmergenceSystem.cs Assets/Scripts/Save/SaveManager.cs Assets/Scripts/Milestones/PlayerLevel.cs Assets/Scripts/Tests/EmergenceSystemTests.cs
git status
git commit -m "$(cat <<'EOF'
Replace automatic emergence ticking with manual, criteria-gated release

CanRelease()/TryReleaseSpecies() reuse the exact ecological criteria
the old per-tick probability roll used (prey population thresholds for
predators, tree-count-derived caps for herbivores) as a deterministic
yes/no check instead. Removes the tick timer entirely (Update(),
AdvanceTicks, ConsumeTicks, tickIntervalSeconds) and SaveManager's
offline catch-up call into it - population only changes now when the
player explicitly releases a species.
EOF
)"
```

---

### Task 2: Replace the Demo panel's "Force Emergence Tick" with per-species Release buttons

**Files:**
- Modify: `Assets/UI/Demo/Demo.uxml`
- Modify: `Assets/Scripts/UI/DemoUI.cs`

**Interfaces:**
- Consumes: `EmergenceSystem.TryReleaseSpecies(SpeciesDefinition)` (from Task 1) and `DemoUI`'s existing `allSpecies` field (already wired in the scene to `[Gazelle, Squirrel, Wolf, Fox]` from the prior stage).

- [ ] **Step 1: Replace the button in the UXML**

In `Assets/UI/Demo/Demo.uxml`, replace this line:
```xml
                <ui:Button name="force-tick-button" text="Force Emergence Tick" class="demo-button" />
```
with:
```xml
                <ui:Button name="release-gazelle-button" text="Release Gazelle" class="demo-button" />
                <ui:Button name="release-squirrel-button" text="Release Squirrel" class="demo-button" />
                <ui:Button name="release-wolf-button" text="Release Wolf" class="demo-button" />
                <ui:Button name="release-fox-button" text="Release Fox" class="demo-button" />
```

- [ ] **Step 2: Replace the handler in `DemoUI.cs`**

In `Assets/Scripts/UI/DemoUI.cs`, change this line in `OnEnable()`:
```csharp
        root.Q<Button>("force-tick-button").clicked += OnForceTickClicked;
```
to:
```csharp
        root.Q<Button>("release-gazelle-button").clicked += () => OnReleaseSpeciesClicked("gazelle");
        root.Q<Button>("release-squirrel-button").clicked += () => OnReleaseSpeciesClicked("squirrel");
        root.Q<Button>("release-wolf-button").clicked += () => OnReleaseSpeciesClicked("wolf");
        root.Q<Button>("release-fox-button").clicked += () => OnReleaseSpeciesClicked("fox");
```

Replace this method:
```csharp
    private void OnForceTickClicked()
    {
        if (emergenceSystem == null)
        {
            return;
        }

        emergenceSystem.ForceEmergenceTick();
        SetStatus("Forced an emergence tick.");
    }
```
with:
```csharp
    private void OnReleaseSpeciesClicked(string speciesId)
    {
        if (emergenceSystem == null)
        {
            return;
        }

        SpeciesDefinition species = FindSpeciesById(speciesId);
        if (species == null)
        {
            SetStatus($"No species definition wired for '{speciesId}'.");
            return;
        }

        bool released = emergenceSystem.TryReleaseSpecies(species);
        SetStatus(released
            ? $"Released a {species.displayName}."
            : $"Can't release {species.displayName} yet - criteria not met.");
    }

    private SpeciesDefinition FindSpeciesById(string speciesId)
    {
        foreach (SpeciesDefinition species in allSpecies)
        {
            if (species != null && species.id == speciesId)
            {
                return species;
            }
        }

        return null;
    }
```

- [ ] **Step 3: Refresh Unity and confirm zero compile errors**

Run `mcp__UnityMCP__refresh_unity` (`mode: force`, `scope: all`, `compile: request`, `wait_for_ready: true`), then `mcp__UnityMCP__read_console` filtered to `error`.
Expected: zero errors (this also resolves the `ForceEmergenceTick()` reference Task 1 may have left dangling).

- [ ] **Step 4: Run the full EditMode suite**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `include_failed_tests: true`.
Expected: same pass/fail set as the end of Task 1 (9 new `EmergenceSystemTests` passing, 4 pre-existing `SpawnMissingCreatureVisuals_*` failures, everything else passing) — this step should now compile and run cleanly since `DemoUI.cs` no longer references `ForceEmergenceTick()`.

- [ ] **Step 5: Verify visually**

Enter Play mode, open the Demo panel (F1 or its HUD button), and confirm the four new "Release X" buttons are present and the old "Force Emergence Tick" button is gone. Click "Release Gazelle" — since a fresh game has 0 trees, `CanRelease` should return false (treesPerIndividualCap gate), so the status label should read the "can't release yet" message and `EcosystemState.GetSpeciesPopulation("gazelle")` should stay 0. Plant several trees (via "Plant a Batch of Trees" or manually), then click "Release Gazelle" again — it should succeed this time, and the status label should confirm it. No screenshot needed for this check — read the status label text and population count directly via `execute_code`.

- [ ] **Step 6: Commit**

Stage only: `Assets/UI/Demo/Demo.uxml`, `Assets/Scripts/UI/DemoUI.cs`.

```bash
git add Assets/UI/Demo/Demo.uxml Assets/Scripts/UI/DemoUI.cs
git status
git commit -m "$(cat <<'EOF'
Replace Demo panel's Force Emergence Tick with per-species Release buttons

Minimal test trigger for the new manual release mechanic - one button
per species (Gazelle/Squirrel/Wolf/Fox), each calling
EmergenceSystem.TryReleaseSpecies() directly. The real player-facing
picker + tap-to-place UI is a later stage; this just needs to be fully
testable now.
EOF
)"
```

---

### Task 3: Full integration verification

**Files:** None (verification only).

- [ ] **Step 1: Run the complete EditMode suite**

Run `mcp__UnityMCP__run_tests` with `mode: "EditMode"`, `include_failed_tests: true`.
Expected: every test passes except the same 4 pre-existing `EmergenceSystemTests.SpawnMissingCreatureVisuals_*` failures that predate this plan. Zero other failures, and specifically zero failures related to `AdvanceTicks` (that test class no longer exists) or the new `CanRelease`/`TryReleaseSpecies` tests.

- [ ] **Step 2: Play-mode ecosystem chain test**

Via `execute_code`, in a fresh Play session: build up enough tree count and Gazelle population to satisfy Wolf's prey threshold, then confirm Wolf's release succeeds. This exercises the full predator-depends-on-herbivore chain end-to-end, not just each species in isolation.

Use `EcosystemState.RegisterTreePlanted` directly rather than `TreePlanter.PlantTree` — real planting is subject to land-size and spacing rules that vary by save state and aren't the point of this check; this test only needs `TotalTreeCount` to reach a number, not real spatial trees. The exact numbers below are already confirmed from the actual assets (`Gazelle.asset`: `treesPerIndividualCap: 20`; `Wolf.asset`: `preyThreshold: 5`, `preyToPredatorRatio: 5`) — don't re-derive them, use them as given:

```csharp
var emergence = UnityEngine.Object.FindFirstObjectByType<EmergenceSystem>();
var gazelle = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Gazelle.asset");
var wolf = UnityEditor.AssetDatabase.LoadAssetAtPath<SpeciesDefinition>("Assets/Data/Species/Wolf.asset");

// Gazelle's populationCapacity = TotalTreeCount / treesPerIndividualCap (20). Need capacity >= 5
// to clear Wolf's preyThreshold, so 100 trees gives exactly capacity = 5.
for (int i = 0; i < 100; i++)
{
    EcosystemState.RegisterTreePlanted("oak");
}

int gazellesReleased = 0;
while (emergence.TryReleaseSpecies(gazelle))
{
    gazellesReleased++;
}

bool wolfReleased = emergence.TryReleaseSpecies(wolf);

return "gazelles released=" + gazellesReleased + " (expect 5) wolf released=" + wolfReleased + " (expect True) wolf population=" + EcosystemState.GetSpeciesPopulation("wolf");
```
Expected: `gazellesReleased` = 5 (the `while` loop naturally stops once `CanRelease` goes false at the population cap), `wolfReleased` = `true`.

- [ ] **Step 3: Confirm the milestone still fires**

`MilestoneManager` checks milestones on its own 1-second `Update()` timer, not instantly on population change (`Assets/Scripts/Milestones/MilestoneManager.cs:60-68`). Rather than waiting on real time, invoke its private `CheckMilestones()` method directly via reflection to force an immediate check, then read the result through the existing public `CompletedMilestoneIds` property (no need for reflection on the result — it's already public):

```csharp
var milestoneManager = UnityEngine.Object.FindFirstObjectByType<MilestoneManager>();
System.Reflection.MethodInfo checkMethod = typeof(MilestoneManager).GetMethod("CheckMilestones", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
checkMethod.Invoke(milestoneManager, null);

return "first_predator completed=" + milestoneManager.CompletedMilestoneIds.Contains("first_predator");
```
Expected: `true` (assuming Step 2's Wolf release succeeded — this milestone fires off `EmergenceSystem.HasAnyPredatorEmerged()`, which reads `EcosystemState` population directly and doesn't care whether the population got there via the old tick or the new manual release).

- [ ] **Step 4: Confirm no console errors**

Run `mcp__UnityMCP__read_console` filtered to `["error", "warning"]` after the above. Expected: nothing related to `EmergenceSystem`, `SaveManager`, or `DemoUI`.

- [ ] **Step 5: Stop Play mode; no further action needed**

This task produces no commit — it's a verification pass over Tasks 1–2's work. If any check above fails, return to the relevant task and fix it there (with its own re-test-and-commit cycle) rather than patching here.
