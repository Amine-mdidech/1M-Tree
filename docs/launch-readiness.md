# Launch Readiness Checklist

*Not for now — revisit this doc once core gameplay/content is considered feature-complete. This is the punch list for turning the game into a shippable, store-ready product.*

Status as of 2026-08-08: core gameplay (emergence system, tech tree, save system, land expansion, settings, milestones) is solid and tested. The gaps below are about product/store/business readiness, not gameplay.

## 1. Hard blockers (will fail store review or are unsafe to ship)

- [ ] **Strip or gate the Demo tools panel** (`Assets/Scripts/UI/DemoUI.cs`). It currently ships live: one-tap unlock-everything, grant gems, delete save. Must be removed from release builds or locked behind a build flag/debug menu, per the game design doc's own "Known gap" note.
- [ ] **Set real project identity in ProjectSettings**: `companyName` is still `DefaultCompany`, no bundle identifier configured, no app icon, no splash screen. Stores reject submissions without these.
- [ ] **Resolve monetization + NGO payment flow before building it.** Per the GDD's "Open questions & risks" section:
  - Who verifies funds reach a real reforestation NGO, and how is that shown to players? (Ecosia's monthly public financial reports are a good transparency model to study.)
  - Apple/Google have specific rules tying IAPs to charitable donations — check current store policy before implementing.
  - Publicly claiming "net profit to charity" usually carries legal disclosure obligations depending on country — worth a short conversation with an accountant/lawyer once gameplay is validated.
  - Pick an ad network that won't fight the "calm, not grindy" design pillar with aggressive interstitials.
- [ ] **Implement the actual monetization flow**: rewarded video ads, IAP purchasing (gems), NGO payment routing + visible impact counter. Currently only the gem *spend* mechanics exist (skip research/growth); no ad SDK, no purchasing package, no NGO integration.
- [ ] **Wire up analytics and crash reporting.** Only the empty `com.unity.modules.unityanalytics` stub is present — no real analytics or crash reporting SDK. You'll be blind to crashes and retention post-launch without this.
- [ ] **Write a privacy policy and data-handling disclosure.** Required by both stores, especially once IAP/payment and any analytics data collection are in place.

## 2. Should-fix before launch

- [ ] **Add test coverage for `LandManager` and `PlayerLevel`.** The GDD flags these as built outside the usual spec → plan → TDD flow, unlike everything else (`MilestoneManagerTests`, `DailyGrantTests`, etc.). Same pattern applies: isolated plain C# logic, no Unity lifecycle dependency, straightforward to test.
- [ ] **Expand unique species art.** Only Gazelle and Wolf have hand-tuned art; the other 14 species use generic category silhouettes. Not a functional blocker, but affects store screenshots/first impressions.
- [ ] **Verify the first-run/onboarding experience end-to-end** — confirm it actually teaches the core loop as described in the GDD, not just that the code exists.
- [ ] **Prepare store assets**: screenshots, feature graphic, store description copy, age rating questionnaire (relevant given charity/payment angle).

## 3. Explicitly deferred (do not scope-creep these in before launch)

Per the GDD's "Explicitly out of scope" section: multiple biomes, lab upgrade paths, global shared real-tree counter, cosmetic customization, prestige/reset loop, insects/pollinators, real predation (population-reducing hunting).

## Suggested order of attack when we pick this back up

1. Resolve the NGO/payment/legal questions in section 1 first — they gate the riskiest and most central feature (the game's whole premise is funding real reforestation), and everything else can proceed in parallel or after.
2. Gate/strip the Demo panel and set real project identity (quick, mechanical, low-risk).
3. Implement monetization flow once the legal/NGO direction is settled.
4. Wire analytics/crash reporting.
5. Backfill `LandManager`/`PlayerLevel` tests.
6. Art pass + store assets last, once everything else is locked.
