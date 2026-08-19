# Art Design Document

*Version 0.1 — living document. **Consult this before generating or adding any new UI element, sprite, or world asset.** Companion to `ui-style-guide.md` (structural/UI Toolkit conventions) and `game-design-document.md` (mechanics).*

## Purpose

This project has three co-existing visual systems: a procedural pixel-art UI system (buttons, panels, HUD chrome), fal.ai-generated set-piece art for hero world objects, and — newest — fal.ai-generated flat illustrated backdrops for full-bleed UI screens (title/menu screens, not in-world chrome). This doc exists so that every future asset — whether a new panel, a new HUD icon, a new world building/prop, or a new screen backdrop — gets deliberately assigned to the right system instead of drifting toward a fourth, inconsistent style. Read the relevant section below *before* writing a prompt or a `PixelArtUtility` call.

## The three systems, and how to choose between them

| | Procedural pixel art | fal.ai hero objects | fal.ai screen backdrops |
|---|---|---|---|
| Used for | UI chrome: panel frames, buttons, badges, icons, HUD widgets, minimap | Hero/set-piece **world objects**: single landmark buildings, the river | Full-bleed **screen backgrounds**: title/menu screen art, not in-world scenery and not clickable chrome |
| Reference | `ui-style-guide.md`, `PixelArtUtility.cs` | This document, `Assets/Textures/Laboratory/LabBuilding.png` | This document, `Assets/Textures/MainMenu/MainMenuBackground.png` |
| Style | Beveled chunky mobile-game chrome | Clean stylized 3D render, semi-realistic materials | Flat 2D illustration — flat color shapes, no 3D shading, no outlines |
| Why | Needs tight, consistent bevel/token system; must be recolorable and regenerable at runtime (badges change color per state, icons are drawn from a shared glyph set) | Benefits from rich real-material detail (glass, stone, foliage) that procedural pixel art can't cheaply match; generated once, cached as a static asset, not recolored at runtime | A screen backdrop is atmosphere, not a game object — it never needs recoloring/state, and a flat illustration reads better as a full-screen backdrop behind UI text than a busy 3D render would (less visual competition with button/text contrast) |
| Trees / animals | Still procedural today (`PixelArtUtility`) — this is deliberate for now (there are many of them, spawned/recolored at runtime), **but** the Laboratory sets a visibly higher bar. If a future request asks to upgrade trees/animals to fal.ai art, that's a legitimate direction to pursue, not a rule violation — flag it and decide with the user rather than assuming either way. |  |  |
| HUD token icons | `Assets/Textures/Hud/GemIcon.png`, `LevelStarIcon.png`, `VegetationIcon.png` (currency, level/XP, and vegetation-picker icons) — a deliberate, scoped exception to the default rule below. |  |  |

**Default rule:** if what you're building is a *widget the player clicks/reads UI state from*, it's procedural (follow `ui-style-guide.md`). If it's a *thing that exists in the game world as scenery/landmark*, it's a fal.ai hero-object candidate. If it's a *full-screen backdrop behind a UI screen* (not clicked, not in-world), it's a fal.ai screen-backdrop candidate, per the template below. Check with the user before spending a generation on something that wasn't explicitly asked for.

## Screen backdrops: flat illustrated style

Unlike hero objects (isolated, chroma-keyed, composited into a 3D world), a screen backdrop is a **full illustrated scene**, generated at a wide aspect ratio so it can pan/parallax behind UI content, and used directly — no background removal, no world-scale/pivot conventions. Composited with a runtime-generated dark gradient overlay (bottom-heavy, see the owning UI script) so foreground button text stays readable without baking a scrim into the source image itself, which would make the asset non-reusable if the UI's color scheme ever changes.

**Prompt template:**
```
flat illustrated video game background art, [subject], minimalist flat vector
illustration style, flat color shapes, no outlines, no 3D shading, soft color
gradient lighting, wide panoramic landscape composition, distinct foreground/
midground/background layers, peaceful mood, no text, no characters, no UI
elements
```
Generate wide (roughly 2.2–2.5:1) so there's enough width for a slow horizontal pan without ever showing an edge. No chroma key, no post-processing background removal — this style is a finished full-frame image, not an isolated object to be cut out.

**Reference:** `Assets/Textures/MainMenu/MainMenuBackground.png` (the title screen's sunrise-over-a-reforested-valley backdrop) is the first and current example of this style.

**The HUD token icon exception:** a small class of HUD icon does fit fal.ai despite being "chrome" — a *static single-purpose token* (a currency symbol, a level medal, a category icon) whose glyph never changes shape or needs runtime recoloring, only the frame/count/label around it does. The badge frame, XP bar, and button bevel stay procedural (still built from `PixelArtUtility`/USS tokens, still animate/pulse/recolor normally); only the glyph itself is fal.ai art, swapped in via a `[SerializeField] Sprite` on the owning UI script (see `HudUI.cs`'s `currencyIconSprite`/`xpIconSprite`/`vegetationIconSprite`). Framing differs from the isolated-hero-object template above: these are small HUD tokens, not world objects, so the prompt uses a 3/4 icon angle ("stylized 3D render of a [subject]... 3/4 perspective, centered composition") rather than the top-down isometric framing used for buildings/landmarks. Don't reach for this by default — most "chrome" icons (nav buttons, category glyphs that vary per state) should stay procedural per the default rule; this exception is for the handful of icons that are effectively a game's "brand" tokens (currency, rank) where the extra visual weight earns its keep.

## The reference template: the Laboratory

`Assets/Textures/Laboratory/LabBuilding.png` (added via fal.ai, see `LaboratoryBuilding.cs`) is the target quality bar and aesthetic anchor for every future fal.ai-generated asset in this game. Open it and look at it before generating anything new — it's the standard everything else gets compared against. What makes it the reference:

- **Perspective**: three-quarter isometric "game asset" framing — not a flat top-down icon, not a full first-person/photographic angle. Reads clearly as an object sitting on a small ground plinth, viewed from above and slightly to the side.
- **Style**: clean stylized 3D render. Semi-realistic materials (real-looking glass, brushed metal, cut stone, foliage) but not photographic and not painterly/hand-drawn. This is the midpoint to aim for — too photoreal clashes with the game's otherwise stylized presentation, too flat/cartoonish wastes the point of using fal.ai over procedural art.
- **Theme fusion — "scientific but environmental"**: a man-made structure being visibly reclaimed by nature (moss climbing the walls, vines, garden beds, a tree growing right up against the dome). This is the game's core thematic identity (rebuilding an ecosystem) made literal in the art, not just in the mechanics. Every future hero asset should look for a way to fuse "built/technical" with "alive/growing" the same way, where the subject matter allows it.
- **Complexity and scale**: it's deliberately *busy* — multiple materials, layered detail (dome ribs, satellite dish, garden beds, paving, vents), and it fills essentially all of its allocated space in the world rather than sitting as a small token icon in the middle of a big empty exclusion zone. "Uses all the space dedicated for it" is a direct, reusable brief for future hero assets: size the generation and the in-scene scale so the object actually occupies its gameplay footprint, not a fraction of it.
- **Presentation**: single isolated object, centered composition, clean removed background, no baked-in ground shadow bleeding into the final sprite. The building reads as a discrete "piece" you could drop anywhere, not a cropped photograph.

Anything generated in the future should be judged against this bar: does it feel like it belongs next to the Laboratory, or does it look flatter/simpler/less considered? If the latter, it's not done yet.

## Prompt recipe

Model: `fal-ai/flux/dev` (`num_inference_steps: 28-30`, `guidance_scale: 3.5`) for anything that ships in-game. `fal-ai/flux/schnell` is fine only for cheap throwaway iteration, never for a final asset.

Two different prompt shapes depending on asset category:

**Isolated hero object** (buildings, future landmark props) — the Laboratory's actual prompt is the template:
```
top-down isometric video game asset of a [subject], complex detailed architecture,
[2-4 concrete material/detail callouts specific to the subject], clean crisp stylized
3D render, centered composition, solid flat pure [KEY COLOR] chroma key background
(#hex), no shadow cast on background, no ground shadow, no gradient, flat uniform
[KEY COLOR] color fill, isolated object on [KEY COLOR] screen
```
**Key color rule**: pick a chroma key color that is hue-distinct from anything the subject itself will contain. The first Laboratory attempt used a green background for a building covered in green foliage — the hues collided closely enough that clean keying became unreliable, and the model didn't even render a flat color (it added a soft gradient + a cast shadow despite being told not to). Magenta/pink worked because nothing in an eco-lab (grays, glass-blue, foliage-green) is anywhere near that hue. Default to magenta (`#ff00ff`) unless the subject itself is pink/red/magenta, in which case pick another hue the subject won't contain.

**Tileable material texture** (ground textures, water, anything meant to repeat) — different failure mode, different template:
```
seamless tileable material texture of [subject], photorealistic, directly overhead
flat lighting with no glare hotspot and no vignette, uniform brightness across the
whole frame, uniform [detail] evenly distributed, [color], top-down orthographic
photo, no single focal feature, repeating pattern material swatch, texture map
```
Photographic-composition prompts (single focal point, vignette, directional lighting) tile visibly badly — the prompt has to explicitly fight the model's default instinct to compose a "nice photo" instead of a flat material swatch.

## Post-processing pipeline (do not skip — raw generations are never final assets)

Never take a fal.ai output directly into the project. Every asset so far has needed at least one of these passes; treat them as the standard pipeline, not one-off fixes:

1. **Background removal — flood-fill from the corners for *connectivity*, but classify each candidate against a fixed reference by hue, not by chaining deltas between neighbors.** The model will not give you a perfectly flat key color even when explicitly asked (see above); real outputs have soft gradients and a cast shadow. Two failure modes to avoid, both hit while building the HUD token icons (`Assets/Editor/HudIconPostProcessor.cs` is the working, reusable implementation - start from it rather than re-deriving this):
   - *Chained-delta drift*: accepting a neighbor whenever it's only a small RGB step from whatever pixel is currently being expanded sounds like it should "follow a gradient smoothly," but over a BFS spanning thousands of pixels the accumulated drift is unbounded - it will walk straight through a soft-edged subject's silhouette and eat most of the object. Compare every candidate against one *fixed* reference color instead (e.g. the average of the four corners); BFS is still used, but purely for connectivity, not to let the accepted color wander.
   - *Raw RGB distance vs. the fixed reference still under-removes the cast shadow*: a colored drop-shadow (same hue as the key, much darker/less saturated) can sit far from a bright corner sample in raw RGB space even though it's obviously still "background." Compare **hue** (`Color.RGBToHSV`, circular distance) instead, with a low saturation floor (skip near-white/near-black pixels, whose hue is numerically unstable) - hue is stable across the shadow's brightness/saturation swing in a way raw RGB distance isn't. Tune the hue threshold conservatively: too wide and it starts eating a warm/orange-shaded subject's own darker facets (hit this with a gold star icon whose shaded underside drifted close enough to the magenta key's hue at a loose threshold).
   
   Then keep only the *largest connected component* of the resulting alpha mask to strip any small disconnected specks. When cropping/resizing afterward, premultiply alpha first (zero the RGB of background pixels, not just their alpha) - otherwise bilinear resizing blends the stale background hue into edge pixels and leaves a visible color fringe even though those pixels are fully transparent at native resolution.
2. **Tileable textures also need a lighting-flatten pass before tiling.** fal.ai bakes in a directional light gradient even when told "uniform lighting" — divide out a heavily-blurred (large-radius Gaussian) version of the image ("flat-field correction") before doing the offset-and-heal seam blend, or the tile will show periodic banding even with a perfect seam.
3. **Tileable seam fix**: roll the image 50%/50% (torus wrap) so any edge mismatch relocates to a cross in the center, then blend in a blurred copy *only* in that narrow center cross. The actual outer edges are left untouched — after the roll they're just ordinary interior content, so they already tile against themselves.
4. **Crop to content bounding box** with a small padding margin, pad to a square canvas (centered) so the sprite isn't stretched when displayed, resize to a clean final resolution.
5. **Unity import settings**: hero objects import as `Sprite` (`textureType: 8`, `spriteMode: 1`, `alphaIsTransparency: 1`, pivot `(0.5, 0.5)` matching the procedural sprites' convention). Tileable materials import as a plain `Texture2D` (`textureType: 0`) with `wrapU`/`wrapV: 0` (Repeat).

## Sizing convention

Keep `spritePixelsToUnits` at the project default (100) for every imported sprite — don't tune per-asset pixel density. Control an asset's actual in-game size with the GameObject's `transform.localScale` instead, sized so the rendered object fills its gameplay-allocated footprint (an exclusion radius, a tile width, etc.) with a small margin, per the Laboratory's "uses all the space dedicated for it" brief. This keeps every asset's native resolution consistent and puts all the sizing logic in one place (the scene) instead of splitting it between import settings and scale.

## Checklist before generating or adding a new UI/art asset

1. **Is this UI chrome or a world object?** Chrome → stop, go follow `ui-style-guide.md`'s procedural system instead, this document doesn't apply.
2. **If it's a world object**: is it a one-off hero/landmark (fal.ai candidate) or a frequently-spawned/recolored gameplay prop (stay procedural, per the trees/animals note above)? Confirm with the user before spending a generation on something not explicitly requested.
3. **Write the prompt** from the matching template above (isolated object vs. tileable material), picking a hue-distinct key color.
4. **Run the full post-processing pipeline** — background removal via flood-fill, largest-connected-component cleanup, (tileables only) lighting-flatten + seam-heal, crop/pad/resize, correct Unity import settings.
5. **Compare against the Laboratory.** Does it match its perspective, material complexity, and "built + reclaimed by nature" theming (where the subject allows it)? Does it fill its allocated space? If it reads as flatter or simpler, iterate before shipping it.
