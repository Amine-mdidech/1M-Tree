# Grove UI Style Guide

*Companion to game-design-document.md. Describes the shared design system all UI panels are built from.*

## Visual language: chunky mobile game ("Clash of Clans"-inspired)

Every panel and button reads as a solid 3D object, not a flat rectangle. This is achieved entirely procedurally (no external art assets) via two techniques used together:

- **Beveled/embossed borders**: every frame, button, and node uses a *lighter* tint on its top/left border and a *thicker, near-black* (`--color-shadow`) border on its bottom/right, which reads as a raised, pressed-metal/wood edge lit from the top-left. `:active { scale: 0.9 0.9; }` (or similar) is added on top so buttons visibly depress when tapped. For plain-rect elements this is real per-side USS (`border-top-color`, `border-right-width`, etc.); for icon buttons the bevel is baked directly into the generated sprite (see below), since those already need a baked texture for their glyph.
- **Procedural textures**: panel frames and content areas use Perlin-noise-generated wood grain, parchment, or stone textures (`PixelArtUtility.CreateWoodGrainTexture` / `CreateParchmentTexture` / `CreateStoneTexture`) instead of flat fills, applied via `element.style.backgroundImage = new StyleBackground(texture)` in each panel's `OnEnable()`.
- **Bold outlined text**: headers and prominent labels use `-unity-text-outline-width` / `-unity-text-outline-color` (dark outline, light fill) for a "video game" look that stays legible over textured backgrounds.

`PixelArtUtility.CreateIconButtonSprite` is the reference implementation for the baked-bevel technique: it draws a dark offset drop-shadow shape first (`FillRoundedRectRegion`), then the lighter face shape on top offset toward the top-left, then the glyph — producing a convincing pressed-button look in a single sprite.

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

**Spacing** — 4px grid: `--space-xs` (4) / `sm` (8) / `md` (12) / `lg` (16) / `xl` (24). Never hardcode a pixel gap outside this scale.

**Radius** — `--radius-sm` (8, small badges) / `md` (12, buttons/nodes) / `lg` (18, panel frames) / `pill` (999, fully-rounded pills like the currency badge).

**Borders** — `--border-thin` (2px, most components' light top/left bevel edge) / `--border-thick` (4px, outer panel frames) / `--border-shadow` (6px, the dark bottom/right bevel edge — always thicker than the top/left edge it pairs with, so the "light source" reads consistently top-left across every element).

**Typography** — `--font-size-xs` (11) through `--font-size-xl` (24). No custom fonts are imported (procedural-art project); this scale is Unity's default runtime theme font at fixed sizes.

## Component patterns

- **Panel frame**: `border-radius: var(--radius-lg); border-width: var(--border-thick); border-color: var(--color-gold);` over a procedural wood-grain or stone texture (not a flat surface color), with a semi-transparent `--color-ink` backdrop behind it (`rgba` override, since custom properties don't carry alpha channels usefully — backdrops set their own `rgba(...)` directly).
- **Header bar**: solid panel-identity color, `border-radius: var(--radius-md)`, bold outlined title text in a contrasting light tint, a small round beveled `X` button (`--color-danger` family, `--radius-pill`) top-right.
- **Buttons**: colored by semantic action (success/danger/neutral/info) using the light/dark bevel pair on top-left/bottom-right borders, `border-radius: var(--radius-md)` (or `--radius-pill` for compact pill buttons), bold outlined text, `:active { scale: 0.9 0.9; }` for tactile press-down feedback.
- **Icon buttons** (HUD toolbar): small square, baked-bevel sprite background (see Visual language above) carrying its own drop shadow and glyph, sitting inside a toolbar panel (`--color-ink` background, `--radius-lg`, gold border) rather than floating bare on the world.
- **Content areas**: inner scroll/content regions use a lighter procedural texture (parchment or light stone) distinct from the frame's texture, so the frame reads as the "object" and the content area reads as the "surface you're looking at" (e.g. Codex's wood frame vs. its parchment book-page).
- **Locked content**: `--color-locked` fill, `--color-locked-dark` border, "🔒 ???" label — used consistently in the Codex and the Laboratory tech tree so "locked" always looks and reads the same way everywhere.

## Layout principles

- All modal panels (Codex/Laboratory/Profile/Demo) share the same structure: full-screen dim backdrop → centered frame (`max-width` capped, `width/height: 100%` within a small root padding) → header bar → scrollable content area → optional footer/nav row.
- Landscape-first sizing: headers and chrome are kept compact (small font sizes, tight padding) because the game runs in landscape only, where vertical space is scarce.
- World-space taps resolve in a fixed priority order (Laboratory building → existing tree → plant new tree) so overlapping interactive elements never fight each other; UI panels register themselves as pointer-blocking so a tap on a button never also triggers a world action underneath it.

## Navigation model: single-screen home/panel switching

The game behaves like a single-screen mobile app, not a stack of overlapping windows: at most one of {home toolbar, Codex, Laboratory, Profile, Demo} is visible at a time.

- **`ScreenNavigator`** (`Assets/Scripts/UI/ScreenNavigator.cs`) is the single coordinator for this. It holds a reference to `HudUI` and each of the four panel scripts. `OpenCodex()` / `OpenLab()` / `OpenProfile()` / `OpenDemo()` each close every other panel, open the requested one, and hide the HUD's action toolbar (`HudUI.SetToolbarVisible(false)`); `GoHome()` closes everything and restores the toolbar.
- Every panel script (`CodexUI`, `LaboratoryUI`, `ProfileUI`, `DemoUI`) exposes a public `SetOpen(bool visible)` — the single entry point for showing/hiding that panel — and a `[SerializeField] private ScreenNavigator screenNavigator` reference. Each panel's close (`X`) button routes through `screenNavigator.GoHome()`, with a fallback to `SetOpen(false)` if `screenNavigator` isn't wired (keeps panels functional in isolation, e.g. for tests).
- `HudUI`'s four toolbar buttons call `screenNavigator.OpenX()` (with the same self-open fallback pattern) rather than talking to panels directly, so `HudUI` never needs to know about other panels beyond its `ScreenNavigator` reference.
- To add a fifth screen: give the new panel script a public `SetOpen(bool)`, add a `screenNavigator` field routed through `GoHome()` on close, add a reference + `OpenX()` method to `ScreenNavigator`, and call it from a new HUD button.

## Applying the system to new UI

1. Import `Theme.uss` first in the new UXML, and add the `theme-scope` class to the panel's own root element.
2. Pick the closest existing panel surface identity, or add a new `--surface-*` triad to `Theme.uss` if it's a genuinely new feature area (as done for Profile and Demo).
3. Build the frame/header/content structure from the component patterns above, applying a procedural texture (wood/parchment/stone) to the frame and content area in the panel's `OnEnable()`.
4. Color every button semantically, not decoratively, using the light/dark bevel pair per the beveling convention.
5. Use only tokens from the spacing/radius/typography scales — no new magic numbers.
6. If the new UI is a full-screen "screen" (not an in-place widget), give it a public `SetOpen(bool)` and a `screenNavigator` field routed through `GoHome()` on close, then wire it into `ScreenNavigator` per the Navigation model section above.
