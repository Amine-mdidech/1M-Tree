# XP/Level HUD Badge — Design

## Purpose

Let the player see level progress live, during normal play, without opening a
menu: current level and a fill bar toward the next level, updating the moment
XP is earned (planting a tree, completing a milestone, unlocking research),
with a distinct celebration on the level-up moment itself.

## Current state

`PlayerLevel` (`Assets/Scripts/Milestones/PlayerLevel.cs`) is a singleton
already tracking `Level`, `CurrentXp`, and `XpToNextLevel`, and already grants
XP from tree planting, milestone completion, and research unlocks (see
`docs/game-data-specs.md` §8). It plays the milestone fanfare sound and grants
gems on level-up, but has no visual representation anywhere except a static
number in the Profile panel (`ProfileUI.cs`), which is a snapshot refreshed
only when that panel is opened — not live.

The HUD (`HudUI.cs` / `Assets/UI/Hud/Hud.uxml` / `Hud.uss`) already has a
directly comparable always-visible widget: `currency-badge`, showing the gem
count. It polls `MilestoneManager.Gems` once per frame in `HudUI.Update()`,
diffs against a cached `lastDisplayedGems` field, and on change updates the
label and plays a pulse + floating "+N" animation. This is the pattern to
extend, not replace.

## Scope

**In:** an always-visible HUD badge showing current level and a live XP
progress bar, updating as XP is earned during normal play, with a flash +
level-number pop specifically on level-up.

**Out:** a floating "+N XP" popup (the smooth bar-fill already communicates
the gain — matches what was asked for, avoids cluttering a small badge with a
second always-visible widget's worth of extra motion). A badge entrance
fade-in animation (only `currency-badge` has one; most HUD elements don't,
and skipping it keeps this change smaller). Any change to `PlayerLevel`'s XP
math, values, or grant sources — those are unchanged.

## Design

### Placement

A new `xp-badge` element is added to `Hud.uxml` as a sibling of
`currency-badge` inside the existing `top-left-cluster`, positioned between
`currency-badge` and the existing `vegetation-cluster` (`top-left-cluster`'s
only two current children) — so it reads as part of the same status row. No new
`UIDocument`, `PanelSettings`, or scene GameObject is needed: `HudUI` already
exists in the scene and already owns `currency-badge`, so this is purely new
markup/styles/code inside files that already exist and are already wired.

### Structure (`Hud.uxml`)

```xml
<ui:VisualElement name="xp-badge" class="xp-badge" picking-mode="Ignore">
    <ui:VisualElement name="xp-icon" class="xp-icon" picking-mode="Ignore" />
    <ui:Label name="xp-level-label" text="Lv 1" class="xp-level-label" picking-mode="Ignore" />
    <ui:VisualElement name="xp-bar-track" class="xp-bar-track" picking-mode="Ignore">
        <ui:VisualElement name="xp-bar-fill" class="xp-bar-fill" picking-mode="Ignore" />
    </ui:VisualElement>
</ui:VisualElement>
```

### Styling (`Hud.uss`)

- `.xp-badge`: same 32px-tall pill shape as `.currency-badge` (background
  color, border, radius, padding), so the two badges read as one family. Like
  `currency-badge`, the pill's textured background image is applied in code
  (`PixelArtUtility.CreateTexturedPillBackground`), not pure USS.
- `.xp-icon`: 16x16, `PixelArtUtility.CreateGlyphSprite(64, PixelArtUtility.DrawStarGlyph)` —
  reuses the same star glyph already used for "Level" in `ProfileUI`, for
  visual continuity between the two.
- `.xp-level-label`: same font styling as `.gem-count-label` (bold,
  `--font-size-xs`, `--text-on-dark`).
- `.xp-bar-track`: fixed-width (~60px) thin (6px) rounded track, dark
  translucent background.
- `.xp-bar-fill`: fills the track by `width: Length.Percent(...)`, accent
  color matching the star icon's existing purple
  (`rgba(140, 89, 217, 1)` / `Color(0.55f, 0.35f, 0.85f)` used in
  `ProfileUI`'s level icon), `transition-property: width` (~0.3–0.4s
  ease-out) so an XP gain visibly slides the bar forward instead of
  snapping.
- `.xp-bar-fill--flash` / `.xp-level-label--punch`: level-up-only classes,
  toggled on then removed after a short delay via `schedule.Execute(...)`,
  mirroring the existing `currency-badge--pulse` technique exactly (brief
  `scale`/brightness change, no new animation infrastructure).

### Behavior (`HudUI.cs`)

New private fields: `VisualElement xpBarFill`, `Label xpLevelLabel`, and
tracking state `int lastDisplayedLevel = -1`, `int lastDisplayedXp = -1`
(mirroring `lastDisplayedGems`).

`OnEnable()`: query the new elements, set the icon's background image, call
`RefreshXp()` once to initialize the badge with no animation (mirrors the
existing `RefreshGemCount()` call at the end of `OnEnable`).

`Update()`: call `RefreshXp()` alongside the existing `RefreshGemCount()`.

`RefreshXp()`: reads `PlayerLevel.Instance` (already used directly elsewhere
in this file, e.g. `ShowLandLockedPopup`); if `Level`/`CurrentXp` haven't
changed since last frame, return early. Otherwise:

1. Update `xpLevelLabel.text` to `"Lv {Level}"`.
2. Set `xpBarFill.style.width` to `Length.Percent(CurrentXp / XpToNextLevel * 100)`.
3. If `Level` increased since last frame → trigger the flash + pop classes
   (level-up case). Otherwise, if `CurrentXp` increased → no extra animation
   beyond the width transition itself (the bar sliding forward is the
   feedback).

On level-up, `CurrentXp` has already rolled over to a smaller value against a
larger `XpToNextLevel` (per `PlayerLevel.AddXp`), so the bar will visibly
drop and restart filling toward the new threshold — the flash communicates
that this is a celebration, not a loss.

## Testing

Consistent with how every other HUD/panel visual is handled in this project
(`docs/game-data-specs.md` §17: "UI Toolkit visual/layout behavior... [is]
exercised manually via Play mode instead" — none of `CodexUI`, `LaboratoryUI`,
`ProfileUI`, `HudUI` itself, etc. have automated tests), this is verified
manually: run the game, plant a tree, confirm the bar advances smoothly and
the level label is correct; use the Demo panel's level-up button to confirm
the flash/pop animation fires and the bar correctly resets against the new
threshold; confirm the badge's initial state on scene load matches whatever
level/XP was restored from the save file.
