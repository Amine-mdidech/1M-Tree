# XP/Level HUD Badge Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an always-visible HUD badge next to the gem counter showing current level and a live XP progress bar, with a flash + level-number pop on level-up.

**Architecture:** New markup/styles are added directly to the existing `Assets/UI/Hud/Hud.uxml` / `Hud.uss` (no new `UIDocument`, `PanelSettings`, or scene GameObject — `HudUI` already exists in the scene and already owns the sibling `currency-badge` widget this mirrors). `HudUI.cs` polls `PlayerLevel.Instance` once per frame, exactly like it already polls `MilestoneManager.Gems` for the gem counter, and updates the bar width + level label + level-up animation classes on change.

**Tech Stack:** Unity 6 (UI Toolkit / UIElements), C#.

## Global Constraints

- No changes to `PlayerLevel`'s XP math, values, or grant sources (`docs/superpowers/specs/2026-08-08-xp-hud-badge-design.md`, Scope: Out).
- No floating "+N XP" popup and no badge entrance fade-in animation — deliberately out of scope (same spec, Scope: Out).
- New pixel art goes through the existing `PixelArtUtility.DrawStarGlyph` (already used for "Level" in `ProfileUI`) — no new glyph method needed.
- New USS must use `--space-*`/`--radius-*`/`--font-size-*`/`--color-*` custom properties from `Assets/UI/Theme/Theme.uss` wherever the existing `currency-badge` styling already does, matching that file's exact pattern.
- This is UI Toolkit visual/layout behavior. Per this project's established pattern (`docs/game-data-specs.md` §17 — no panel, including `HudUI` itself, has automated tests), this plan has no automated test steps; verification is manual (Task 3).

---

### Task 1: HUD markup + styling

**Files:**
- Modify: `Assets/UI/Hud/Hud.uxml`
- Modify: `Assets/UI/Hud/Hud.uss`

**Interfaces:**
- Produces: five named elements queryable via UQuery — `xp-badge`, `xp-icon`, `xp-level-label`, `xp-bar-track`, `xp-bar-fill` — and four USS classes — `.xp-bar-fill--flash`, `.xp-level-label--punch` (state classes) plus the base `.xp-badge`/`.xp-icon`/`.xp-level-label`/`.xp-bar-track`/`.xp-bar-fill` (structural classes). Consumed by `HudUI.cs` in Task 2.

- [ ] **Step 1: Add the badge markup to Hud.uxml**

In `Assets/UI/Hud/Hud.uxml`, change:

```xml
            <ui:VisualElement name="currency-badge" class="currency-badge" picking-mode="Ignore">
                <ui:VisualElement name="gem-icon" class="gem-icon" picking-mode="Ignore" />
                <ui:Label name="gem-count-label" text="0" class="gem-count-label" picking-mode="Ignore" />
            </ui:VisualElement>
            <ui:VisualElement name="vegetation-cluster" class="vegetation-cluster">
```

to:

```xml
            <ui:VisualElement name="currency-badge" class="currency-badge" picking-mode="Ignore">
                <ui:VisualElement name="gem-icon" class="gem-icon" picking-mode="Ignore" />
                <ui:Label name="gem-count-label" text="0" class="gem-count-label" picking-mode="Ignore" />
            </ui:VisualElement>
            <ui:VisualElement name="xp-badge" class="xp-badge" picking-mode="Ignore">
                <ui:VisualElement name="xp-icon" class="xp-icon" picking-mode="Ignore" />
                <ui:Label name="xp-level-label" text="Lv 1" class="xp-level-label" picking-mode="Ignore" />
                <ui:VisualElement name="xp-bar-track" class="xp-bar-track" picking-mode="Ignore">
                    <ui:VisualElement name="xp-bar-fill" class="xp-bar-fill" picking-mode="Ignore" />
                </ui:VisualElement>
            </ui:VisualElement>
            <ui:VisualElement name="vegetation-cluster" class="vegetation-cluster">
```

- [ ] **Step 2: Add the badge styles to Hud.uss**

In `Assets/UI/Hud/Hud.uss`, change:

```css
.gem-count-label {
    font-size: var(--font-size-xs);
    color: var(--text-on-dark);
    -unity-font-style: bold;
}

.action-cluster {
```

to:

```css
.gem-count-label {
    font-size: var(--font-size-xs);
    color: var(--text-on-dark);
    -unity-font-style: bold;
}

.xp-badge {
    height: 32px;
    flex-direction: row;
    align-items: center;
    background-color: rgba(24, 20, 18, 0.82);
    -unity-background-scale-mode: stretch-to-fill;
    border-radius: var(--radius-pill);
    border-width: 1.5px;
    border-color: rgba(255, 255, 255, 0.16);
    padding: 0 var(--space-md) 0 var(--space-xs);
    margin-left: var(--space-sm);
}

.xp-icon {
    width: 16px;
    height: 16px;
    margin-right: var(--space-xs);
}

.xp-level-label {
    font-size: var(--font-size-xs);
    color: var(--text-on-dark);
    -unity-font-style: bold;
    margin-right: var(--space-xs);
    scale: 1 1;
    transition-property: scale;
    transition-duration: 0.15s;
    transition-timing-function: ease-out;
}

.xp-level-label--punch {
    scale: 1.4 1.4;
}

.xp-bar-track {
    width: 60px;
    height: 6px;
    border-radius: var(--radius-pill);
    background-color: rgba(0, 0, 0, 0.4);
    overflow: hidden;
}

.xp-bar-fill {
    height: 100%;
    width: 0%;
    background-color: rgb(140, 89, 217);
    border-radius: var(--radius-pill);
    transition-property: width;
    transition-duration: 0.35s;
    transition-timing-function: ease-out;
}

.xp-bar-fill--flash {
    background-color: rgb(210, 180, 255);
}

.action-cluster {
```

(`rgb(140, 89, 217)` is the same purple already used for the "Level" icon badge in `ProfileUI.cs` — `new Color(0.55f, 0.35f, 0.85f)` — kept visually consistent between the two.)

- [ ] **Step 3: Verify it parses with no console errors**

In the Unity Editor, wait for it to detect the changed `.uxml`/`.uss` files and confirm the Console shows no new errors referencing `Hud.uxml` or `Hud.uss` (a bad selector or malformed element reports as a UI Toolkit error there). The badge will render in the HUD showing "Lv 1" and an empty bar — nothing updates it live yet, that's Task 2.

- [ ] **Step 4: Commit**

```bash
git add "Assets/UI/Hud/Hud.uxml" "Assets/UI/Hud/Hud.uss"
git commit -m "feat: add XP badge markup and styling to the HUD"
```

---

### Task 2: Live XP/level behavior in HudUI

**Files:**
- Modify: `Assets/Scripts/UI/HudUI.cs`

**Interfaces:**
- Consumes: `xp-badge`/`xp-icon`/`xp-level-label`/`xp-bar-track`/`xp-bar-fill` elements and `.xp-bar-fill--flash`/`.xp-level-label--punch` classes from Task 1. `PlayerLevel.Instance.Level` / `.CurrentXp` / `.XpToNextLevel` (`Assets/Scripts/Milestones/PlayerLevel.cs`, unchanged, already used elsewhere in this file via `ShowLandLockedPopup`). `PixelArtUtility.CreateGlyphSprite(int, Action<PixelBuffer>)` and `PixelArtUtility.DrawStarGlyph(PixelBuffer)` (unchanged, already used in `ProfileUI.cs`).
- Produces: nothing new consumed elsewhere — this is the last task that touches code.

- [ ] **Step 1: Add tracking fields**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
    private Label gemCountLabel;
    private int lastDisplayedGems = -1;
```

to:

```csharp
    private Label gemCountLabel;
    private int lastDisplayedGems = -1;
    private VisualElement xpBarFill;
    private Label xpLevelLabel;
    private int lastDisplayedLevel = -1;
    private int lastDisplayedXp = -1;
```

- [ ] **Step 2: Query the new elements and set the icon in OnEnable**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
        VisualElement gemIcon = root.Q<VisualElement>("gem-icon");
        gemIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawGemGlyph));
        gemCountLabel = root.Q<Label>("gem-count-label");

        topLeftCluster = root.Q<VisualElement>("top-left-cluster");
```

to:

```csharp
        VisualElement gemIcon = root.Q<VisualElement>("gem-icon");
        gemIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawGemGlyph));
        gemCountLabel = root.Q<Label>("gem-count-label");

        VisualElement xpBadge = root.Q<VisualElement>("xp-badge");
        xpBadge.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateTexturedPillBackground(140, 32, new Color(0.094f, 0.078f, 0.071f, 0.82f)));

        VisualElement xpIcon = root.Q<VisualElement>("xp-icon");
        xpIcon.style.backgroundImage = new StyleBackground(PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawStarGlyph));

        xpLevelLabel = root.Q<Label>("xp-level-label");
        xpBarFill = root.Q<VisualElement>("xp-bar-fill");

        topLeftCluster = root.Q<VisualElement>("top-left-cluster");
```

- [ ] **Step 3: Initialize the badge at the end of OnEnable**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
        RefreshVegetationLabel();
        RefreshGemCount();
        minimapRefreshTimer = 0f;
    }
```

to:

```csharp
        RefreshVegetationLabel();
        RefreshGemCount();
        RefreshXp();
        minimapRefreshTimer = 0f;
    }
```

- [ ] **Step 4: Poll it every frame in Update**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
    private void Update()
    {
        RefreshGemCount();

        minimapRefreshTimer -= Time.deltaTime;
```

to:

```csharp
    private void Update()
    {
        RefreshGemCount();
        RefreshXp();

        minimapRefreshTimer -= Time.deltaTime;
```

- [ ] **Step 5: Add RefreshXp() and AnimateLevelUp()**

In `Assets/Scripts/UI/HudUI.cs`, change:

```csharp
    private void AnimateGemGain(int amount)
    {
        if (currencyBadge != null)
        {
            currencyBadge.AddToClassList("currency-badge--pulse");
            currencyBadge.schedule.Execute(() => currencyBadge.RemoveFromClassList("currency-badge--pulse")).ExecuteLater(150);
        }

        VisualElement hudLayer = currencyBadge?.parent;
        if (hudLayer == null)
        {
            return;
        }

        Label floatingLabel = new Label($"+{amount}");
        floatingLabel.AddToClassList("gem-float-label");
        floatingLabel.pickingMode = PickingMode.Ignore;
        hudLayer.Add(floatingLabel);

        floatingLabel.schedule.Execute(() => floatingLabel.AddToClassList("gem-float-label--animate")).ExecuteLater(10);
        floatingLabel.schedule.Execute(() => floatingLabel.RemoveFromHierarchy()).ExecuteLater(800);
    }
```

to:

```csharp
    private void AnimateGemGain(int amount)
    {
        if (currencyBadge != null)
        {
            currencyBadge.AddToClassList("currency-badge--pulse");
            currencyBadge.schedule.Execute(() => currencyBadge.RemoveFromClassList("currency-badge--pulse")).ExecuteLater(150);
        }

        VisualElement hudLayer = currencyBadge?.parent;
        if (hudLayer == null)
        {
            return;
        }

        Label floatingLabel = new Label($"+{amount}");
        floatingLabel.AddToClassList("gem-float-label");
        floatingLabel.pickingMode = PickingMode.Ignore;
        hudLayer.Add(floatingLabel);

        floatingLabel.schedule.Execute(() => floatingLabel.AddToClassList("gem-float-label--animate")).ExecuteLater(10);
        floatingLabel.schedule.Execute(() => floatingLabel.RemoveFromHierarchy()).ExecuteLater(800);
    }

    private void RefreshXp()
    {
        if (xpBarFill == null || xpLevelLabel == null || PlayerLevel.Instance == null)
        {
            return;
        }

        int currentLevel = PlayerLevel.Instance.Level;
        int currentXp = PlayerLevel.Instance.CurrentXp;
        int xpToNextLevel = PlayerLevel.Instance.XpToNextLevel;

        if (currentLevel == lastDisplayedLevel && currentXp == lastDisplayedXp)
        {
            return;
        }

        bool leveledUp = lastDisplayedLevel >= 0 && currentLevel > lastDisplayedLevel;

        lastDisplayedLevel = currentLevel;
        lastDisplayedXp = currentXp;

        xpLevelLabel.text = $"Lv {currentLevel}";

        float fillPercent = xpToNextLevel > 0 ? Mathf.Clamp01((float)currentXp / xpToNextLevel) * 100f : 0f;
        xpBarFill.style.width = Length.Percent(fillPercent);

        if (leveledUp)
        {
            AnimateLevelUp();
        }
    }

    private void AnimateLevelUp()
    {
        xpBarFill.AddToClassList("xp-bar-fill--flash");
        xpBarFill.schedule.Execute(() => xpBarFill.RemoveFromClassList("xp-bar-fill--flash")).ExecuteLater(300);

        xpLevelLabel.AddToClassList("xp-level-label--punch");
        xpLevelLabel.schedule.Execute(() => xpLevelLabel.RemoveFromClassList("xp-level-label--punch")).ExecuteLater(200);
    }
```

(`lastDisplayedLevel >= 0` guards against firing the level-up animation on the very first `RefreshXp()` call — mirrors the existing `previousGems >= 0` guard in `RefreshGemCount()` immediately above.)

- [ ] **Step 6: Verify it compiles**

In the Unity Editor, wait for script compilation to finish (bottom-right spinner) and confirm the Console shows no new errors referencing `HudUI.cs`.

- [ ] **Step 7: Commit**

```bash
git add "Assets/Scripts/UI/HudUI.cs"
git commit -m "feat: wire live XP/level updates into the HUD badge"
```

---

### Task 3: Manual verification

**Files:** none (no code changes — this task is a playthrough checklist).

- [ ] **Step 1: Confirm the badge shows on entering Play mode**

Enter Play mode. Expected: the XP badge appears next to the gem counter in the top-left cluster, showing `Lv` followed by the current saved level, with the bar filled proportionally to current XP toward the next level (matching whatever `PlayerLevel` restored from the save file — cross-check against the Profile panel's Level row, which should show the same number).

- [ ] **Step 2: Confirm the bar advances on planting**

Plant a tree. Expected: within a frame or two, the bar visibly slides forward (eased, not an instant snap) by an amount proportional to 5 XP (`PlayerLevel.XpPerTreePlanted`) out of the current level's XP requirement.

- [ ] **Step 3: Confirm milestone/research XP also updates it**

Trigger a milestone completion (or use the Demo panel's "Unlock All Species"/"Finish Active Research" buttons, which grant research-unlock XP) and confirm the bar advances again.

- [ ] **Step 4: Confirm the level-up animation**

Open the Demo panel (gear icon, or press F1) and click its "Level Up" button (tops up XP to trigger exactly one level-up). Expected: the bar briefly flashes to a lighter color, the `Lv N` label does a quick pop/scale animation, the label updates to the new level number, and the bar shows the rolled-over XP progress against the new (higher) XP requirement — a visible drop-then-refill-start, not a jump to 100%.

- [ ] **Step 5: Confirm it survives a reload**

Stop Play mode and enter it again. Expected: the badge immediately shows the correct persisted level/XP on the very first frame (matching Step 1), not a stale or zeroed value.

---

## Self-Review

**Spec coverage:**
- Always-visible badge, live-updating on tree planting and milestones → Task 1 (markup/styles) + Task 2 (`RefreshXp()` polled every `Update()`), manually confirmed in Task 3 Steps 1–3. ✓
- Current level visible → `xp-level-label` text set in `RefreshXp()` (Task 2), confirmed Task 3 Step 1. ✓
- Level-up flash + label pop, no other new celebration → `AnimateLevelUp()` (Task 2), confirmed Task 3 Step 4; no floating-label code was added anywhere. ✓
- No floating "+N XP" popup, no entrance fade-in → neither appears in Task 1 or Task 2's code. ✓
- Placement next to the gem counter → Task 1 Step 1 inserts `xp-badge` between `currency-badge` and `vegetation-cluster`. ✓
- No `PlayerLevel` changes → no task touches `PlayerLevel.cs`. ✓

**Placeholder scan:** no TODO/TBD/"add appropriate" phrasing; every step has literal before/after code blocks.

**Type consistency:** `xpBarFill`/`xpLevelLabel` field names and `RefreshXp()`/`AnimateLevelUp()` method names are used identically between where they're declared (Task 2 Step 1) and where they're called (Task 2 Steps 3–5). Element names `xp-badge`/`xp-icon`/`xp-level-label`/`xp-bar-track`/`xp-bar-fill` and classes `.xp-bar-fill--flash`/`.xp-level-label--punch` match exactly between Task 1 (UXML/USS) and Task 2 (`root.Q<...>(...)` calls and `AddToClassList`/`RemoveFromClassList` calls).
