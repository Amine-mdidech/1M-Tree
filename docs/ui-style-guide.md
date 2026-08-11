# Grove UI Style Guide

*Companion to game-design-document.md. Describes the shared design system all UI panels are built from. For world-object art (buildings, landmarks) and the fal.ai generation pipeline, see `art-design-document.md` instead — check that doc before generating or adding any new UI/art asset.*

## Visual language: chunky mobile game ("Clash of Clans"-inspired)

Every panel and button reads as a solid 3D object, not a flat rectangle. This is achieved entirely procedurally (no external art assets — this section covers UI chrome specifically; world-object art now also includes fal.ai-generated assets, see `art-design-document.md`) via two techniques used together:

- **Beveled/embossed borders**: every frame, button, and node uses a *lighter* tint on its top/left border and a *thicker, near-black* (`--color-shadow`) border on its bottom/right, which reads as a raised, pressed-metal/wood edge lit from the top-left. `:active { scale: 0.9 0.9; }` (or similar) is added on top so buttons visibly depress when tapped. For plain-rect elements this is real per-side USS (`border-top-color`, `border-right-width`, etc.); for icon buttons the bevel is baked directly into the generated sprite (see below), since those already need a baked texture for their glyph.
- **Procedural textures**: panel frames and content areas use Perlin-noise-generated wood grain, parchment, or stone textures (`PixelArtUtility.CreateWoodGrainTexture` / `CreateParchmentTexture` / `CreateStoneTexture`) instead of flat fills, applied via `element.style.backgroundImage = new StyleBackground(texture)` in each panel's `OnEnable()`.
- **Bold outlined text**: headers and prominent labels use `-unity-text-outline-width` / `-unity-text-outline-color` (dark outline, light fill) for a "video game" look that stays legible over textured backgrounds.

`PixelArtUtility.CreateIconButtonSprite` is the reference implementation for the baked-bevel technique: it draws a dark offset drop-shadow shape first (`FillRoundedRectRegion`), then the lighter face shape on top offset toward the top-left, then the glyph — producing a convincing pressed-button look in a single sprite.

The same `PixelBuffer`/`Fill*` toolkit also drives the minimap (`HudUI.GenerateMinimapTexture`), but as a *live* texture rather than a static decorative one: it's regenerated from scratch every 0.5s (not every frame — a full `PixelBuffer` → `Texture2D` round-trip isn't free) to reflect current tree positions, land-tier size, and camera viewport. This is the one place in the project where procedural pixel art encodes live gameplay data instead of purely decorative texture — worth keeping in mind if a future feature wants a similar "live readout drawn as pixel art" widget (e.g. a population graph): reuse the `PixelBuffer` + `Fill*` primitives (now `public`, see below) rather than reaching for a different rendering approach.

## Source of truth

`Assets/UI/Theme/Theme.uss` defines every design token as a USS custom property on a `.theme-scope` class. Every panel's UXML imports the theme stylesheet first, before the panel's own local stylesheet, and adds `theme-scope` to its own root element's class list, so local rules can reference tokens with `var(--token-name)` instead of hardcoding values:

```xml
<ui:Style src="project://database/Assets/UI/Theme/Theme.uss" />
<ui:Style src="project://database/Assets/UI/MyPanel/MyPanel.uss" />
<ui:VisualElement name="my-panel-root" class="my-panel-root theme-scope">
```

(A plain `:root` selector was tried first; a class selector applied directly to each panel's own root element is the pattern confirmed working at runtime, so that's what every panel uses.)

Because each panel (Hud, Codex, Laboratory, TreeBoost, Profile, Demo) is a separate `UIDocument`/`PanelSettings`, custom properties do **not** carry across panels automatically — both the theme stylesheet import and the `theme-scope` class must be added to every panel's own root UXML element.

## Tokens

**Brand**
- `--color-gold` / `--color-gold-light` / `--color-gold-dark` — the game's signature accent and border color, used on every panel frame and primary button border. Light/dark variants give the gold trim its own bevel.
- `--color-ink` — near-black warm brown, used for dark badge/toolbar backgrounds.
- `--color-shadow` — near-black (`rgb(18,12,9)`), the bottom/right bevel-shadow color used on every beveled element regardless of its base hue.

**Semantic action colors** — each now ships a `-light` and `-dark` variant for beveling, not just a single flat tone. Buttons are colored by what they *do*, not by which panel they're in:
- `--color-success` / `-light` / `-dark` — confirm/positive actions (start research, speed up growth).
- `--color-danger` / `-light` / `-dark` — close/cancel/destructive actions.
- `--color-info` / `-light` / `-dark` — informational accents (Laboratory's teal identity).
- `--color-neutral` / `-light` / `-dark` — secondary navigation (Codex prev/next).

Bevel convention: `-light` on `border-top-color`/`border-left-color` (thin), `--color-shadow` on `border-bottom-color`/`border-right-color` (thick, `--border-shadow`), base color as the fill.

**Panel surfaces** — each feature has its own tinted identity so players always know which system they're in at a glance, while sharing the same structural language:
| Panel | Surface | Inner | Border |
|---|---|---|---|
| Codex | wood/parchment (`--surface-codex*`) | | |
| Laboratory | teal (`--surface-lab*`) | | |
| Tree boost | leaf green (`--surface-treeboost*`) | | |
| Profile | slate blue (`--surface-profile*`) | | |
| Demo | plum (`--surface-demo*`) | | |
| Settings | slate/steel blue — hardcoded `rgb(...)` literals rather than a new `--surface-settings*` token triad (the panel is small enough, and used rarely enough, that a full token set felt like premature setup; see "Applying the system to new UI" below for when a new triad *is* warranted) | | |

**Spacing** — 4px grid: `--space-xs` (4) / `sm` (8) / `md` (12) / `lg` (16) / `xl` (24). Never hardcode a pixel gap outside this scale.

**Radius** — `--radius-sm` (8, small badges) / `md` (12, buttons/nodes) / `lg` (18, panel frames) / `pill` (999, fully-rounded pills like the currency badge).

**Borders** — `--border-thin` (2px, most components' light top/left bevel edge) / `--border-thick` (4px, outer panel frames) / `--border-shadow` (6px, the dark bottom/right bevel edge — always thicker than the top/left edge it pairs with, so the "light source" reads consistently top-left across every element).

**Typography** — `--font-size-xs` (11) through `--font-size-xl` (24). No custom fonts are imported (procedural-art project); this scale is Unity's default runtime theme font at fixed sizes.

## Component patterns

- **Panel frame**: `border-radius: var(--radius-lg); border-width: var(--border-thick); border-color: var(--color-gold);` over a procedural wood-grain or stone texture (not a flat surface color), with a semi-transparent `--color-ink` backdrop behind it (`rgba` override, since custom properties don't carry alpha channels usefully — backdrops set their own `rgba(...)` directly).
- **Header bar**: solid panel-identity color, `border-radius: var(--radius-md)`, bold outlined title text in a contrasting light tint, a small round beveled `X` button (`--color-danger` family, `--radius-pill`) top-right.
- **Buttons**: colored by semantic action (success/danger/neutral/info) using the light/dark bevel pair on top-left/bottom-right borders, `border-radius: var(--radius-md)` (or `--radius-pill` for compact pill buttons), bold outlined text, `:active { scale: 0.9 0.9; }` for tactile press-down feedback.
- **Icon buttons** (HUD toolbar): small round button, procedurally-drawn glyph background (see Visual language above) with its own per-button tint color set in C# (`button.style.backgroundColor`). These float directly on the world now rather than sitting inside an enclosing pill/oval toolbar container — an earlier pass grouped them inside a dark `--color-ink` pill per cluster, but it read as a heavy UI chrome element competing with the game world underneath, so the containers were removed and only the individual round buttons remain (the gem-count badge is the one HUD cluster that still keeps its pill container, since it's a compact single-line stat rather than a row of tappable icons).
- **Text-only toggle buttons** (e.g. Settings' Mute/Unmute): for a binary on/off control with no natural icon, a plain text button colored by state (`--color-success` when active/unmuted, `--color-danger` when the alternate state is engaged) is simpler and just as legible as commissioning a new glyph — reserve procedural icon art for buttons that navigate somewhere or represent a concrete object/species.
- **Content areas**: inner scroll/content regions use a lighter procedural texture (parchment or light stone) distinct from the frame's texture, so the frame reads as the "object" and the content area reads as the "surface you're looking at" (e.g. Codex's wood frame vs. its parchment book-page).
- **Locked content**: `--color-locked` fill, `--color-locked-dark` border, "🔒 ???" label — used consistently in the Codex and the Laboratory tech tree so "locked" always looks and reads the same way everywhere.

## Layout principles

- All modal panels (Codex/Laboratory/Profile/Demo) share the same structure: full-screen dim backdrop → centered frame (`max-width` capped, `width/height: 100%` within a small root padding) → header bar → scrollable content area → optional footer/nav row.
- Landscape-first sizing: headers and chrome are kept compact (small font sizes, tight padding) because the game runs in landscape only, where vertical space is scarce.
- World-space taps resolve in a fixed priority order (Laboratory building → existing tree → plant new tree) so overlapping interactive elements never fight each other; UI panels register themselves as pointer-blocking so a tap on a button never also triggers a world action underneath it.

## Navigation model: single-screen home/panel switching

The game behaves like a single-screen mobile app, not a stack of overlapping windows: at most one of {home toolbar, Codex, Laboratory, Profile, Demo, Settings} is visible at a time.

- **`ScreenNavigator`** (`Assets/Scripts/UI/ScreenNavigator.cs`) is the single coordinator for this. It holds a reference to `HudUI` and each of the five panel scripts. `OpenCodex()` / `OpenLab()` / `OpenProfile()` / `OpenDemo()` / `OpenSettings()` each close every other panel, open the requested one, and hide the HUD's action toolbar (`HudUI.SetToolbarVisible(false)`); `GoHome()` closes everything and restores the toolbar.
- Every panel script (`CodexUI`, `LaboratoryUI`, `ProfileUI`, `DemoUI`, `SettingsUI`) exposes a public `SetOpen(bool visible)` — the single entry point for showing/hiding that panel — and a `[SerializeField] private ScreenNavigator screenNavigator` reference. Each panel's close (`X`) button routes through `screenNavigator.GoHome()`, with a fallback to `SetOpen(false)` if `screenNavigator` isn't wired (keeps panels functional in isolation, e.g. for tests).
- `HudUI`'s toolbar buttons call `screenNavigator.OpenX()` (with the same self-open fallback pattern) rather than talking to panels directly, so `HudUI` never needs to know about other panels beyond its `ScreenNavigator` reference.
- To add another screen: give the new panel script a public `SetOpen(bool)`, add a `screenNavigator` field routed through `GoHome()` on close, add a reference + `OpenX()` method to `ScreenNavigator`, and call it from a new HUD button.

## HUD widgets that aren't `ScreenNavigator` screens

Not everything the player interacts with belongs behind a full-screen panel. Two things were added directly to `Hud.uxml`/`HudUI.cs` instead — a persistent always-visible widget (the minimap) and a modal popup that's still logically "part of the HUD" (the land-lock dialog) — plus a third, lighter-weight pattern for one-off transient messages:

- **Persistent widget** (minimap, bottom-left): just another element in `Hud.uxml`, always present, no open/close state. Good fit for anything the player should be able to glance at or use without leaving whatever they're doing — a `ScreenNavigator` panel would force them to stop and look at a full screen for something that's meant to be peripheral.
- **Embedded modal popup** (land-lock dialog): a hidden-by-default (`display: none`) full-screen overlay `VisualElement` inside `Hud.uxml`, shown/hidden imperatively by `HudUI` rather than through `ScreenNavigator`. It's visually modal (dims the background, blocks world taps by virtue of living inside the HUD's `UIDocument`, which is already in `CameraPanController`'s `blockingUIDocuments` list — see below) but doesn't participate in the "only one screen open" rule, because it's triggered reactively by a gameplay event (tapping outside owned land) rather than by a HUD button press. Use this shape when a popup needs to interrupt but is too lightweight/contextual to deserve its own `PanelSettings`/`UIDocument` pair.
- **Ephemeral floating label** (the `+N` gem-gain popup, and the animal-name-on-tap popup): a `Label` created in C#, added directly to the HUD root, positioned in panel-space (screen-space for the gem popup; world-position-projected via `Camera.main.WorldToScreenPoint` → `RuntimePanelUtils.ScreenToPanel` for the animal popup — the latter is the technique to reach for any time a UI element needs to track a moving world object), given an entrance animation via a `--animate` class toggled on `.schedule.Execute(...).ExecuteLater(...)`, then removed from the hierarchy entirely after a short delay. No open/close state at all — it just exists briefly. Reach for this over the popup pattern above when the message is purely informational and doesn't need a response (nothing to confirm/cancel).

**Blocking world input from HUD elements**: `CameraPanController` maintains a `blockingUIDocuments` list (`Assets/Scripts/Vegetation/CameraPanController.cs`) — any tap that lands on a visible, pickable element inside one of these `UIDocument`s is absorbed instead of falling through to plant a tree or pan the camera. Every panel's `UIDocument` must be in this list, including ones embedded inside another `UIDocument` (like the land-lock popup, which piggybacks on the HUD's own `UIDocument` and therefore needs no separate list entry) — a panel added here and forgotten there is a real, easy-to-miss bug (this happened once with the Settings panel and had to be fixed after the fact).

**Cross-system UI triggering**: `HudUI` exposes a `public static HudUI Instance` (set in `Awake`, cleared in `OnDestroy` — the same singleton shape as `AudioManager`/`PlayerLevel`/`LandManager`), so gameplay scripts that have no reason to hold a `HudUI` reference of their own (`TreePlanter`, `CameraPanController`) can still trigger a HUD popup with `HudUI.Instance?.ShowX(...)`. Prefer this over threading a `[SerializeField] HudUI hudUI` field through every script that might someday need to show a popup — it's the same trade-off already made for `AudioManager.Instance?.PlayTap()` everywhere else in the codebase.

## Applying the system to new UI

1. Import `Theme.uss` first in the new UXML, and add the `theme-scope` class to the panel's own root element.
2. Pick the closest existing panel surface identity, or add a new `--surface-*` triad to `Theme.uss` if it's a genuinely new feature area (as done for Profile and Demo).
3. Build the frame/header/content structure from the component patterns above, applying a procedural texture (wood/parchment/stone) to the frame and content area in the panel's `OnEnable()`.
4. Color every button semantically, not decoratively, using the light/dark bevel pair per the beveling convention.
5. Use only tokens from the spacing/radius/typography scales — no new magic numbers.
6. If the new UI is a full-screen "screen" (not an in-place widget), give it a public `SetOpen(bool)` and a `screenNavigator` field routed through `GoHome()` on close, then wire it into `ScreenNavigator` per the Navigation model section above.
