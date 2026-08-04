# [Working title: "Grove" — rename freely] — Game Design Document

*Version 0.5 — living document, reflects the current implemented build*

## Concept (one sentence)
You're a scientist awarded a government grant to physically rebuild Earth's ecosystem — plant trees, grow biodiversity through a real food chain, and unlock new species through research, funding real-world reforestation as you play.

## Design pillars
Every feature gets checked against these. If it doesn't serve one, cut it.

1. **Every tap grows something real** — the virtual and real impact loop must always feel connected, not like a bolted-on donation banner.
2. **Calm, not grindy** — idle-friendly pacing, low pressure, satisfying to check in on for 30 seconds or 10 minutes.
3. **Visible impact** — the player can always see, in plain numbers, what their play has funded.
4. **Biodiversity over monoculture** — the win-condition rewards a rich, varied ecosystem, reinforced mechanically by the food-chain emergence system.
5. **Discovery feels earned** — research and unlocks should feel like genuine scientific progress, not a paywall.

## Narrative frame & onboarding
- Player opens on a bare, lifeless plot with a small laboratory building placed on the map.
- Intro beat: you're a breakthrough scientist, just awarded a government grant to rebuild the planet's ecosystem. Funding starts modest — **30 trees/day** — and grows as you prove results.
- Tutorial walks the player through: planting the first trees, opening the **Codex** (species menu) to see gazelle and wolf already unlocked as starting species, and planting enough trees to trigger the first emergence.
- This framing does double duty: it justifies the daily-cap mechanic narratively (grant funding, not an arbitrary energy bar) and sets up the lab as the long-term progression hub.

## Core loop
```
Daily grant resets tree-planting allowance (starts at 30/day)
  -> Player pans/scrolls the map and plants trees, choosing placement/vegetation
  -> Tree count + vegetation diversity raise emergence % for unlocked species
  -> Herbivores emerge first; predators emerge only once prey population supports them
  -> Player can tap any growing tree to instantly finish its growth for gems
  -> Lab research unlocks new species & vegetation over time via a branching tech tree (speed up with gems)
  -> Milestones (species count, tree count, first-predator, etc.) raise the daily tree cap
  -> Ecosystem grows toward a dense, biodiverse "jungle" state, with predators occasionally visibly hunting nearby prey
  -> Ad views / gem purchases fund research speed AND real NGO reforestation
```

## Mechanics

**A. Government grant & daily tree cap**
- Starts at 30 trees/day, resets daily (real-time) — this is your core retention hook, similar to daily energy systems but narratively justified as grant funding.
- Increases permanently as milestones are hit.
- Treated as a spending allowance for placing trees, separate from the player's total living tree count, which keeps growing over time.

**B. The Codex (biodiversity menu)**
- A book-style panel showing every species — locked (silhouette + "???") and unlocked — organized by category: Herbivores, Predators, then Plants.
- Each unlocked entry has a documentary section: real facts about the species and its conservation status.
- Every species page (locked or unlocked) also shows its **emergence conditions** in plain language — required vegetation (if any), a predator's prey/threshold/ratio, or its population cap relative to tree count — so players can always see what it takes for a species to actually appear, not just whether it's unlocked.

**C. Starting roster & research unlocks**
- Default unlocked: Gazelle (herbivore), Wolf (predator). (An earlier draft also included a starting pollinator/insect species; insects have been cut entirely from the current build — see "Explicitly out of scope" below.)
- Every other plant and animal — 9 more vegetation types and 14 more species — is unlocked through lab research (see "Laboratory" below).
- `requiredVegetationIds` (a species needing specific vegetation present before it can ever emerge, even once researched) exists as a supported mechanism in the data model, but no current species uses it — it's available for future tuning.

**D. Emergence system (the core "magic")**
- Every planted tree, plus vegetation diversity, raises a background probability (rolled on a fixed tick) that an already-unlocked species spawns in the ecosystem.
- Herbivores: emergence chance scales with tree count + presence of their required vegetation.
- Every herbivore also has a **population cap tied to tree count** (`treesPerIndividualCap` — e.g. 1 rabbit per 12 trees, 1 elephant per 60 trees), so population growth stays bounded no matter how fast the emergence tick runs. Rarer/larger animals need proportionally more trees per individual.
- Predators: gated by prey population — a predator only starts appearing once its prey's total population crosses a threshold, and its population capacity is a ratio of current prey population (e.g. 1 wolf per 8 gazelles, once gazelle population reaches 15+). This is a simple, legible food-chain simulation, and it's what makes biodiversity feel earned rather than just unlocked via menu.
- **Predator hunting behavior**: once spawned, a predator creature wandering near a valid prey creature has a 10% chance, each time it picks a new wander target, to visibly chase that prey instead of wandering randomly. This is a cosmetic/behavioral flourish (predators don't currently reduce prey population by hunting) that makes the food chain feel alive.

**E. Tree growth boost**
- Tapping any planted, not-yet-mature tree opens a small popup showing its remaining growth time and a gem cost to finish it instantly (1 gem per remaining minute, minimum 1 gem). Confirming spends the gems and matures the tree immediately.
- Tapping an already-mature tree does nothing (no popup); tapping a spot with no tree plants a new one as normal.

**F. Laboratory**
- A physical building placed on the map (not just a menu) — tap it directly to open the research panel. Trees can't be planted within a small radius around it, so it's always visible and clickable.
- The research panel is a **branching tech tree** (Civ5-style), not a flat list: 24 research projects (10 vegetation types + 14 species) laid out across 5 tiers, connected by lines showing prerequisites. Basic plants and common herbivores unlock with no prerequisites; more advanced plants and animals require specific earlier research to be completed first (e.g. Birch requires Wildflower; Boar requires Birch; predators require their prey's research). Locked nodes show as "🔒 ???"; available nodes show their real name and can be tapped to start research; only one research project runs at a time.
- Research time can be skipped with gems — convenience monetization, not pay-to-win, consistent with the "calm, not grindy" pillar.

**G. Milestones**
- Threshold-based achievements: e.g. "Plant 10/50/200 trees," "Both starters present," "First predator emerges."
- Rewards: permanent daily tree cap increases and gems.
- This is the primary long-term progression spine.

**H. Profile menu**
- A stats panel (HUD toolbar, person icon) showing: gems, total trees planted (lifetime, not just currently alive), species unlocked (X / 16), plants unlocked (X / 11), milestones completed (X / 5), and today's planting (X / daily cap). A snapshot view, refreshed each time it's opened — not a live-updating dashboard.
- Gives players one place to see overall progress at a glance, rather than piecing it together from the Codex, Lab, and HUD separately.

**I. Demo tools panel**
- A separate small utility panel (HUD top-right, gear icon — visually distinct from the core action toolbar so it doesn't read as a normal gameplay feature) exposing one-tap actions for testing/demoing every system without grinding: grant gems, instantly unlock all species, instantly unlock all vegetation, instantly finish the active research project, instantly mature every planted tree, force an emergence tick, plant a batch of trees, and delete the save file to reset.
- Intended for reviewers/testers/demos, not normal play — it's not gated behind a build flag yet, so treat it as a development convenience to be removed or locked behind a debug flag before a real release build.

## Progression
Plant, trigger emergence, hit milestones, raise the daily cap, research deeper species/vegetation through the tech tree, grow a bigger and more biodiverse ecosystem, and (stretch goal) contribute to a global shared counter of real trees funded across all players.

Open question: whether to add a prestige/reset loop (restore a new grove for multipliers) once late-game pacing needs it, or keep continuous growth toward "jungle" status as the whole arc. Not yet built either way.

## Monetization
- Rewarded video ads: speed up growth, bonus seeds/trees, instant research — opt-in only, never forced. (Not yet implemented — gems currently come from milestones only.)
- Gems (IAP): skip research time, skip tree growth, unlock rare species faster. Skip mechanics are implemented; IAP purchase flow is not.
- Net profits routed to NGO partner(s), with a visible, honest impact counter. (Not yet implemented.)

## Platform & art direction
- Mobile, iOS + Android, **landscape orientation** (locked — portrait and upside-down are disabled in Player Settings).
- Unity, 2D, top-down camera looking straight down at the map/ground.
- The map is bigger than the screen and pans: drag to scroll around a fixed-size world, clamped so you can't scroll past its edges. A short drag is treated as a pan; a quick tap plants a tree (or interacts with the lab building / an existing tree).
- Art style is fully procedural (no external art assets) — chunky, beveled/embossed UI panels, buttons, and icon buttons in a warm gold/wood/teal palette (Clash-of-Clans-inspired but not a copy): every element has a lighter top/left edge and a thicker near-black bottom/right edge to read as a raised, pressed object lit from the top-left, bold white text carries a dark outline for legibility, and panel frames/content areas use procedurally-generated wood-grain, parchment, or stone textures (Perlin-noise-based) instead of flat fills. World sprites use per-species layered shapes: each vegetation type has its own `plantSilhouette` (broadleaf tree, conifer, weeping tree, shrub, stalk cluster, fern, flower, or rooted tree) drawn from multiple overlapping shaded blobs/shapes plus a ground shadow, so trees read as visually distinct species rather than one recolored shape (e.g. Pine is a tiered conical evergreen, Willow has drooping fronds, Bamboo/Reed are stalk clusters, Mangrove has splayed prop roots), with foliage color deepening as each plant matures. Creatures are rounded blobs with eyes (tinted per category), and the lab building is a small hut. All generated at runtime via a shared pixel-drawing utility (`PixelArtUtility`).
- Every UI panel is built from a shared design-token system (`docs/ui-style-guide.md`) — one source of truth for colors (including light/dark bevel pairs), spacing, corner radius, border width, and type scale, so new panels are visually consistent by construction rather than by eyeballing hex codes. Each feature area (Codex, Laboratory, tree-boost popup, Profile, Demo tools) has its own tinted "surface" identity built from the shared tokens, so players can tell at a glance which system they're in while the overall look stays cohesive.
- Navigation is single-screen: the home toolbar and each full-screen panel (Codex/Laboratory/Profile/Demo) are mutually exclusive. Tapping a HUD button opens that panel and hides the home toolbar; the panel's close button returns home, hiding the panel and restoring the toolbar. A `ScreenNavigator` coordinator (see `docs/ui-style-guide.md`) owns this so the game never shows two full-screen panels stacked on top of each other.

## Scope — where the build actually stands
The original MVP scope (single-biome map, 3 starting species, one flat research unlock, portrait) has been built and substantially exceeded:
- Full branching, prerequisite-gated tech tree (24 nodes) instead of a single research project.
- Vegetation unlocking follows the same research-gated pattern as species (only Oak starts unlocked).
- 16 total species (2 starters + 14 researchable) across Herbivore/Predator categories.
- Bigger, pannable map instead of a single fixed view.
- Landscape orientation instead of portrait.
- Tree growth boost and predator hunting-behavior mechanics, neither of which were in the original design.
- A full procedural-art visual pass instead of placeholder shapes, built on a shared design-token system rather than one-off styling per panel.
- Profile and Demo tools panels, neither of which were in the original design.
- A unit test suite (Unity Test Framework, EditMode) covering the core game-logic scripts — see `docs/game-data-specs.md` section 11.

## Explicitly out of scope (still not built)
Insects/pollinators as a species category (cut entirely — see "Starting roster" above), lab upgrade paths (faster research, more concurrent slots — there's currently one lab, one research slot), multiple biomes, global shared real-tree counters, cosmetic customization, ad-based rewards, IAP purchase flow, real predation (predators hunting visually but not consuming prey population), and a prestige/reset loop.

## Open questions & risks — resolve before building monetization
- NGO partnership & payment flow: who verifies funds reach a real reforestation NGO, and how is that shown to players? Ecosia's monthly public financial reports are a good transparency model to study.
- App store policy: Apple and Google have specific rules about tying IAPs to charitable donations — research current guidelines before building the IAP system.
- "Net profit" claims: publicly claiming a percentage goes to charity usually carries disclosure obligations depending on your country — worth a short conversation with an accountant or lawyer once the game's fun is validated, not before.
- Ad network choice: pick one that won't fight the "calm, not grindy" pillar with aggressive interstitials.
