# Government Funds Economy — Design

## Purpose

Today the government issues a flat, resetting daily allowance of trees
(`DailyGrant`, base 30/day) — plant more and you're simply blocked until
the next calendar day. This replaces that allowance with a spendable
currency, **Funds**: the government instead deposits a daily *budget* that
grows the more you've already planted/unlocked, and planting a tree or
releasing an animal spends Funds instead of consuming a slot. There's no
longer a hard per-day plant limit — only an affordability check. Alongside
this, players get a persistent Funds readout, a Store panel to browse
everything purchasable and its price, and a floating "-cost" popup at the
spot where they spend, so every plant/release action visibly costs
something.

## Current state

- `Assets/Scripts/Vegetation/DailyGrant.cs` is the entire "government"
  allowance: `BaseDailyTreeCap = 30`, `dailyTreeCap = base + milestoneCapBonus`,
  `TryConsumeTreePlant()` increments `treesPlantedToday` gated by
  `CanPlantTree`, and `CheckForDailyReset()`/`ResetDailyAllowance()`
  (polled every 5s in `Update()`) zero the counter once
  `DateTime.Now.Date` advances. `AddDailyCapBonus(int)` is how milestones
  raise the cap.
- `TreePlanter.PlantTree()` (`Assets/Scripts/Vegetation/TreePlanter.cs:134`)
  calls `dailyGrant.TryConsumeTreePlant()` as its last gate before
  spawning; `CanPlantAt()` (line 205) mirrors that as a non-consuming
  query; `DebugPlantBatch()` (line 292) force-resets the grant mid-loop
  when capped, as a debug convenience.
- Animal release has no daily cap at all. `EmergenceSystem.CanRelease()`
  (`Assets/Scripts/Ecosystem/EmergenceSystem.cs:104`) gates purely on
  ecosystem capacity: herbivores via `treesPerIndividualCap`
  (`SpeciesDefinition.cs:19`), predators via `preyThreshold`/
  `preyToPredatorRatio` (lines 22-24). `TryReleaseSpeciesAt()` (line 163)
  is the tap-to-place entry point, called from
  `CameraPanController.HandleEmptyGroundTap()`.
- `MilestoneManager.cs` already owns a second currency, **Gems**
  (`gems` field, `TrySpendGems`/`AddGems`), spent on land tiers in
  `LandManager.cs` (`LandTier.gemCost`, `CanAffordNextTier()`). Milestone
  completion (`CompleteMilestone`, line 112) grants
  `milestone.rewardDailyCapBonus` (→ `dailyGrant.AddDailyCapBonus`) and
  `milestone.rewardGems` in the same call. This stays untouched — Funds is
  a new, separate currency (per direct instruction), Gems keeps its
  existing role.
- `ProfileUI.cs` shows a `Daily Cap` stat row (`dailyCapValueLabel`, line
  163-166: `"{TreesPlantedToday} / {DailyTreeCap}"`) alongside Gems, tree
  count, species count, etc. `SaveManager.cs`/`PlayerSaveData.cs` persist
  `treesPlantedToday`, `dailyTreeCap`, `lastGrantResetTimestamp`.
- `HudUI.cs` already renders one currency badge (`currency-badge` →
  `gem-icon` + `gem-count-label`, wired to `MilestoneManager.Gems`) with a
  proven gain-animation: `RefreshGemCount()`/`AnimateGemGain()`
  (lines 402-446) spawn a `Label("+{amount}")` with the `gem-float-label`
  USS class, which transitions `opacity`/`translate` then self-removes
  after 800ms. `RuntimePanelUtils.ScreenToPanel(root.panel, screenPosition)`
  is already used elsewhere (`CameraPanController.cs:322`) to convert a
  screen point into panel-local coordinates for this UI Toolkit HUD — the
  mechanism needed to place a popup at an arbitrary world position.
- `SpeciesSummaryUI.cs` is the reference pattern for a listing panel:
  three collapsible sections (Trees/Herbivores/Predators), each a
  `VisualElement` row list built by a shared `CreateRow(icon, name, count,
  onClick)` helper, refreshed on an open/poll cycle, opened/closed via
  `PanelAnimator.Open/Close` and routed through `ScreenNavigator`.
- 2 `VegetationDefinition` assets exist (`Assets/Data/Vegetation/{Oak,Pine}.asset`)
  and 4 `SpeciesDefinition` assets (`Assets/Data/Species/{Squirrel,Fox,Gazelle,Wolf}.asset`),
  plus 5 `MilestoneDefinition` assets under `Assets/Data/Milestones/`, all
  plain-text YAML — small enough to hand-author new field values directly.

## Scope

**In:**
- New `EconomyManager` (replacing `DailyGrant`) owning a `Funds` balance,
  a daily grant formula, `CanAfford`/`TrySpend`.
- `plantCost`/`releaseCost` fields on `VegetationDefinition`/
  `SpeciesDefinition`, authored on all 6 existing assets.
- `TreePlanter.PlantTree`/`EmergenceSystem.TryReleaseSpeciesAt` spend
  Funds instead of consuming the daily cap; `EmergenceSystem`'s existing
  capacity/ratio gating (`CanRelease`) is unchanged and stays an
  additional, independent gate.
- A Funds badge in the persistent HUD, mirroring the existing Gems badge.
- A new Store panel (catalog of everything unlocked, with cost; tapping
  an entry selects it on the HUD picker and closes the store — placement
  itself still happens by tapping the ground, unchanged).
- A floating "-cost" popup at the tapped world position on every
  successful plant/release.
- Save data migration (`funds` replaces `treesPlantedToday`/`dailyTreeCap`).
- Milestone reward rename (`rewardDailyCapBonus` → `rewardDailyFundsBonus`,
  preserved via `FormerlySerializedAs` so existing milestone assets don't
  lose their authored values).
- Test coverage mirroring what `DailyGrantTests`/relevant parts of
  `TreePlanterTests` already exercise.

**Out:**
- Any change to `EmergenceSystem.CanRelease`'s capacity/ratio math.
- Any change to Gems, land tiers, or `MilestoneManager`'s requirement
  types.
- Scaling/rising per-type prices (flat cost only, per direct instruction).
- A store "buy" flow that places without a ground tap (catalog + picker
  selection only).
- Rebalancing growth durations, XP, or anything else not load-bearing for
  the economy swap.

## Design

### `EconomyManager` (new file, replaces `DailyGrant`)

`Assets/Scripts/Vegetation/DailyGrant.cs` is deleted; a new
`Assets/Scripts/Economy/EconomyManager.cs` takes its place, keeping the
same day-rollover polling shape (`CheckForDailyReset`, `Update()` every
5s, `DateTime.Now.Date` comparison) but granting Funds instead of
resetting a counter:

```csharp
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

    public int Funds => funds;
    public bool CanAfford(int cost) => cost >= 0 && funds >= cost;

    public bool TrySpend(int cost)
    {
        if (!CanAfford(cost)) return false;
        funds -= cost;
        return true;
    }

    public void AddDailyFundsBonus(int amount) { milestoneFundsBonus += amount; }

    private void GrantDailyFunds()
    {
        int speciesCount = emergenceSystem != null ? emergenceSystem.UnlockedSpeciesCount : 0;
        int amount = BaseDailyFunds + milestoneFundsBonus
            + (EcosystemState.TotalTreeCount * FundsPerTree)
            + (speciesCount * FundsPerSpecies);
        funds += amount;
        // ...advance lastResetDate/lastFundsGrantTimestamp, same as ResetDailyAllowance did
    }
}
```

- `Awake()` sets `funds = StartingFunds` (fresh-game default — overwritten
  by `LoadState` when a save exists, exactly like `DailyGrant.Awake` sets
  `dailyTreeCap = BaseDailyTreeCap` today).
- `CheckForDailyReset()` keeps the exact same single-reset-regardless-of-gap
  semantics `DailyGrant` has today: if `DateTime.Now.Date > lastResetDate`,
  grant **one** day's Funds and advance `lastResetDate` to today —
  offline for a week grants one day's Funds on return, not seven. This is
  a direct port of existing behavior onto the new currency, not a new
  rule.
- `DistinctSpeciesUnlockedCount` in the formula maps directly onto the
  already-public `EmergenceSystem.UnlockedSpeciesCount` — no new API
  needed there.
- `LoadState(int savedFunds, string savedLastFundsGrantTimestamp)` mirrors
  `DailyGrant.LoadState`, ending with `CheckForDailyReset()` so a stale
  save still catches up exactly one day's grant on load.
- Debug context menus: `"Debug: Simulate Next Day"` (unchanged shape —
  rewinds `lastResetDate`, triggers the check) and `"Debug: Force Grant
  Now"` (renamed from `"Force Reset Now"`, calls `GrantDailyFunds()`
  directly).

### Cost data

- `VegetationDefinition` gains `public int plantCost;`.
- `SpeciesDefinition` gains `public int releaseCost;`.
- Authored directly in the 6 existing asset files: Oak/Pine `plantCost:
  10`; Squirrel/Gazelle (herbivores) `releaseCost: 50`; Fox/Wolf
  (predators) `releaseCost: 200` — the same numbers already shown to and
  approved in the brainstorm's pricing preview. All tunable afterward via
  the Inspector.

### Spend call sites

- `TreePlanter.cs`: `dailyGrant` field renamed `economyManager`
  (`EconomyManager`). `PlantTree()`'s `dailyGrant.TryConsumeTreePlant()`
  gate becomes `economyManager.TrySpend(vegetation.plantCost)`; on success,
  after the existing `VfxUtility.SpawnBurst` call, add
  `HudUI.Instance?.ShowFloatingCost(position, vegetation.plantCost);`.
  `CanPlantAt()`'s `dailyGrant.CanPlantTree` check becomes
  `economyManager.CanAfford(CurrentVegetation.plantCost)`. `DebugPlantBatch()`'s
  auto-unblock (`dailyGrant.DebugForceReset()` when capped) becomes
  `economyManager.DebugForceGrant()` when `!economyManager.CanAfford(...)`,
  preserving the same "never gets stuck" debug convenience.
- `EmergenceSystem.cs`: gains `[SerializeField] private EconomyManager
  economyManager;`. `TryReleaseSpeciesAt()` adds a spend check after the
  existing `CanRelease(species)` check and before `SpawnIndividual`:
  `if (!economyManager.TrySpend(species.releaseCost)) { ...same
  log+PlayError rejection shape as the other checks...; return false; }`.
  On success, after `PlayerLevel.Instance?.AddXp(...)`, add
  `HudUI.Instance?.ShowFloatingCost(position, species.releaseCost);`.
  `CanRelease`'s own capacity/ratio logic is untouched — cost is an
  independent, additional gate, per the approved design.
- `MilestoneManager.cs`: `dailyGrant` field renamed `economyManager`;
  `CompleteMilestone`'s `dailyGrant.AddDailyCapBonus(milestone.rewardDailyCapBonus)`
  becomes `economyManager.AddDailyFundsBonus(milestone.rewardDailyFundsBonus)`.
- `MilestoneDefinition.cs`: `rewardDailyCapBonus` renamed
  `rewardDailyFundsBonus`, with `[FormerlySerializedAs("rewardDailyCapBonus")]`
  so the 5 existing milestone assets' authored values survive the rename
  without manual asset edits.

### HUD: Funds badge

`Hud.uxml`'s `top-left-cluster` gains a `funds-badge` VisualElement
(`funds-icon` + `funds-count-label`), same structure/USS classes as the
existing `currency-badge`, inserted alongside it. `HudUI.cs` gains
`[SerializeField] private EconomyManager economyManager;`, wires
`fundsCountLabel`, and adds `RefreshFundsCount()`/`AnimateFundsGain()` —
a direct copy of `RefreshGemCount()`/`AnimateGemGain()`'s shape (poll in
`Update()`, diff against `lastDisplayedFunds`, float a `"+{amount}"`
label using a new `funds-float-label` USS class on increase only). The
icon is generated procedurally via a new `PixelArtUtility.DrawCoinGlyph`
(mirroring the existing `DrawGemGlyph`'s style) through
`CreateBadgedGlyphSprite(16, ...)`, since there's no fal.ai coin sprite
to assign yet — consistent with how the HUD's action-cluster buttons
already generate their icons.

### Floating cost popup

New `HudUI.ShowFloatingCost(Vector3 worldPosition, int cost)`, using the
same `RuntimePanelUtils.ScreenToPanel` conversion `CameraPanController`
already uses:

```csharp
public void ShowFloatingCost(Vector3 worldPosition, int cost)
{
    VisualElement root = uiDocument.rootVisualElement;
    if (root.panel == null || cameraPanController == null) return;

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

New `.cost-float-label`/`.cost-float-label--animate` USS classes in
`Hud.uss`, same opacity/translate transition shape as `.gem-float-label`
but with `left`/`top` left unset in USS (set inline per-instance above,
since the spawn point is arbitrary, not fixed). Called from
`TreePlanter.PlantTree` and `EmergenceSystem.TryReleaseSpeciesAt` on
every successful spend (see "Spend call sites" above) — this is the "a
disappearing number... for each click plantation" requirement.

### Store panel (new)

New `Assets/UI/Store/Store.uxml` + `Store.uss` + `Assets/Scripts/UI/StoreUI.cs`,
structurally a direct copy of `SpeciesSummaryUI`'s three-collapsible-section
pattern (Trees/Herbivores/Predators), reusing its `CreateRow`-style
helper but showing cost instead of population count, and making every
row clickable (trees included — `SpeciesSummaryUI`'s tree rows aren't
today, since they're display-only there):

- Takes the same `allVegetation`/`allSpecies` injected lists
  `ProfileUI`/`SpeciesSummaryUI` already take, filtered to only
  `treePlanter.IsVegetationUnlocked(id)` / `emergenceSystem.IsSpeciesUnlocked(id)`
  entries (both already public) — the store shows what you *can* buy
  (unlocked), not everything in the game.
- Each row shows icon, name, and `"{cost}"` with the Funds glyph; if
  `!economyManager.CanAfford(cost)`, the row gets a `store-row--unaffordable`
  class (dimmed) but stays tappable — selecting is free, only placing
  spends.
- Tapping a tree row calls new `TreePlanter.SelectVegetation(VegetationDefinition)`
  (sets `currentVegetationIndex` to that entry's index in
  `unlockedVegetation`, mirroring `CycleVegetation`'s indexing), then
  `HudUI.Instance?.SetPickerCategory(HudUI.PickerCategory.Plant)`, then
  closes the store via `ScreenNavigator.GoHome()`.
- Tapping an animal row calls new `EmergenceSystem.SelectReleasableSpecies(SpeciesDefinition)`
  (sets `currentHerbivoreIndex`/`currentPredatorIndex` to that entry's
  index, mirroring `CycleReleasableSpecies`'s per-category split), then
  `HudUI.Instance?.SetPickerCategory(...)` with the matching category,
  then closes the store.
- `HudUI` gains `public void SetPickerCategory(PickerCategory category)`
  (sets `currentPickerCategory`, calls the existing
  `RefreshPickerIcon()`/`RefreshPickerLabel()`).
- Opened via a new `store-button` in `Hud.uxml`'s `action-cluster`
  (alongside lab/codex/profile/species-summary/settings), wired in
  `HudUI.cs` the same way every other action-cluster button is, and
  routed through `ScreenNavigator.OpenStore()` (new method, added to
  `CloseOtherPanels` the same way `speciesSummaryUI` is).

### Save data

`PlayerSaveData.cs`: remove `treesPlantedToday`, `dailyTreeCap`,
`lastGrantResetTimestamp`; add `funds` and `lastFundsGrantTimestamp`.
`SaveManager.cs`: `SaveGame()`'s `dailyGrant.*` reads become
`economyManager.Funds`/`economyManager.LastFundsGrantTimestamp` (new
public property mirroring `LastGrantResetTimestamp`); `LoadGame()`'s
`dailyGrant.LoadState(...)` call becomes
`economyManager.LoadState(data.funds, data.lastFundsGrantTimestamp)`.
No explicit "old save with no funds field" migration branch is needed:
`JsonUtility.FromJson` leaves an unset `int` at its default `0`, and
`EconomyManager.LoadState` given `0` would zero out a returning player's
Funds — instead, `PlayerSaveData.funds` gets a field initializer
(`public int funds = 300;`, numerically matching `EconomyManager`'s own
`StartingFunds` constant, though the two aren't linked by reference) so
an old save missing that key deserializes to a reasonable starting
balance rather than 0.

### Profile panel

`ProfileUI.cs`'s `dailyCapValueLabel`/`daily-cap-value` row is repurposed
into a `Funds` row in the same slot (same icon chip position, label
retargeted to `economyManager.Funds.ToString()`), rather than deleted —
keeps parity with how Gems already gets its own row there.

## Testing

- New `EconomyManagerTests.cs` replacing `DailyGrantTests.cs`: starting
  `Funds` equals `StartingFunds`; `TrySpend`/`CanAfford` behave correctly
  at, above, and below the balance; `GrantDailyFunds`'s formula (base +
  bonus + tree-count term + species-count term) via the day-rollover
  debug hook; `AddDailyFundsBonus` raises future grants; `LoadState`
  round-trips funds and timestamp and triggers exactly one day's
  catch-up grant when the saved timestamp is in the past.
- `TreePlanterTests.cs`: update `SetUp`/fields from `DailyGrant` to
  `EconomyManager`; replace the "stops at daily cap" test with a "stops
  when funds run out" test using a low `StartingFunds`/`plantCost`
  fixture; add a "spends `plantCost` Funds per successful plant" test.
- `EmergenceSystemTests.cs`: add a case where `CanRelease` is satisfied
  but Funds are insufficient — `TryReleaseSpeciesAt` must fail and leave
  population unchanged; and one where both are satisfied — succeeds and
  spends.
- `MilestoneManagerTests.cs`: update the daily-cap-bonus assertion to a
  daily-funds-bonus one against the renamed `EconomyManager` API.
- Manual verification: open the Store, confirm all 6 unlocked items list
  with correct cost and dim when unaffordable; tap a tree/animal row and
  confirm the HUD picker updates and the store closes; plant/release and
  confirm the "-cost" number appears at the tap point and fades, the
  Funds badge updates live, and going below the cost of the selected item
  blocks placement with the existing rejection sound; let a day roll over
  (via the debug menu) and confirm the Funds badge shows a "+N" gain
  animation for the correct amount given current tree/species counts.
