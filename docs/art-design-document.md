# Art Design Document

*Version 0.1 — living document. **Consult this before generating or adding any new UI element, sprite, or world asset.** Companion to `ui-style-guide.md` (structural/UI Toolkit conventions) and `game-design-document.md` (mechanics).*

## Purpose

This project has two co-existing visual systems: a procedural pixel-art UI system (buttons, panels, HUD chrome) and, newer, fal.ai-generated set-piece art for hero world objects. This doc exists so that every future asset — whether a new panel, a new HUD icon, or a new world building/prop — gets deliberately assigned to the right system instead of drifting toward a third, inconsistent style. Read the relevant section below *before* writing a prompt or a `PixelArtUtility` call.

## The two systems, and how to choose between them

| | Procedural pixel art | fal.ai-generated art |
|---|---|---|
| Used for | UI chrome: panel frames, buttons, badges, icons, HUD widgets, minimap | Hero/set-piece **world objects**: single landmark buildings, the river |
| Reference | `ui-style-guide.md`, `PixelArtUtility.cs` | This document, `Assets/Textures/Laboratory/LabBuilding.png` |
| Why | Needs tight, consistent bevel/token system; must be recolorable and regenerable at runtime (badges change color per state, icons are drawn from a shared glyph set) | Benefits from rich real-material detail (glass, stone, foliage) that procedural pixel art can't cheaply match; generated once, cached as a static asset, not recolored at runtime |
| Trees / animals | Still procedural today (`PixelArtUtility`) — this is deliberate for now (there are many of them, spawned/recolored at runtime), **but** the Laboratory sets a visibly higher bar. If a future request asks to upgrade trees/animals to fal.ai art, that's a legitimate direction to pursue, not a rule violation — flag it and decide with the user rather than assuming either way. |

**Default rule:** if what you're building is a *widget the player clicks/reads UI state from*, it's procedural (follow `ui-style-guide.md`). If it's a *thing that exists in the game world as scenery/landmark*, it's a fal.ai candidate — but check with the user before spending a generation on something that wasn't explicitly asked for.

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

1. **Background removal — flood-fill from the corners, not a fixed color-distance threshold.** The model will not give you a perfectly flat key color even when explicitly asked (see above); real outputs have soft gradients and a cast shadow. A global "distance from color X" threshold either keeps shadow as opaque background-colored junk or eats into subject detail that happens to share the key's hue. Flood-fill (BFS/region-growing from the four corners, accepting a neighbor if it's a small color-delta step from an already-accepted pixel) follows a gradient/shadow smoothly while still stopping hard at the subject's actual silhouette edge. Then keep only the *largest connected component* of the resulting alpha mask to strip any small disconnected specks the flood-fill left behind.
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
