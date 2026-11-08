# Starquill MVP Roadmap

**Last updated:** 2026-07-02
**Baseline:** Sprints 1-11, the balance pass, and Destinations Sprint D1 are complete (last confirmed Test Runner: 344 green 2026-07-01, D1 batch green 2026-07-02; ~460 test methods on disk). History: `docs/sprint-review.md`. Current state: `docs/implemented-systems.md`. Completed design/plan docs are archived in `docs/plans/archive/`.

Each sprint follows spec-driven development: design doc → implementation plan → task-by-task execution with tests → review. Plan docs live in `docs/plans/` with the `YYYY-MM-DD-<topic>-design.md` / `-plan.md` naming convention.

---

## Completed (2026-07-01 → 2026-07-02)

| Sprint | Delivered |
|---|---|
| 8.5 + Mobile UI Redesign | Equipment/loot readability, then the app-wide design system: UiTheme/UiFactory, ItemCard v2, BottomSheet flow, mobile sizing |
| 9: Quest Backend | Deterministic zone-ladder quest generation, QuestLog state machine, tier rewards with rarity floors |
| 10: Quests UI | Discovery banner, offer/completion sheets, path indicator bar, sectioned Quests screen with Destinations slot |
| 11: Shop + Offline + XP + Ads | Timed boosts, 4h chest, offline claim sheet, character XP live, rewarded-ad placements + Remove Ads (mock backends) |
| Balance pass | Training ladder as the exponential player-power channel, CHA gold, gear budget scaling, boost repricing; sim-tuned (`tools/balance_sim.py`, `docs/balance-analysis.md`) |
| Reward mailbox | Full-inventory rewards persist in a saved mailbox: never lost, never converted |
| Destinations D1 | Keys (color/type/class, fusion, selling), key-targeted timed dungeon rushes, pouch/fusion UI, run HUD + completion sheet; color families iterated to 11 with the Pastel pool + `tools/color_pools.py` manual override pipeline |

Details per sprint: `docs/sprint-review.md` and `docs/plans/archive/`.

---

## Sprint 12: Polish + MVP Ship — NEXT

**Goal:** shippable Android build.

**Scope:**
- First-time-user tutorial (tap verbs, equip loot, accept a quest); also owns the unlock-gating scheme (Destinations currently gates on first Zone Boss as a placeholder — design D10)
- Animation/juice pass: verb activation, loot drops, quest completion, screen transitions
- **Full UI Pass 2:** unify the newest screens onto UiTheme/UiFactory, revisit IA with real content, art-direct the flat placeholder surfaces, re-run heuristics across the complete app
- Final balance pass across EconomyConfig knobs via play-testing; destinations knobs (key drop rate, fusion cost, dungeon par) are untuned guesses — grow a destinations branch in `tools/balance_sim.py`; known exploit to close or accept: backgrounding right after StartDungeon banks base rolls at 0 waves
- **Real monetization backends (device work):** Unity Ads game id + 3 dashboard placements, Google Play account, real Unity Purchasing 5.x wiring for Remove Ads, store metadata. Interstitials-at-quest-complete already implemented and gated by removeAdsOwned
- Bug sweep and performance check on device; Android build validation, icon/splash

---

## Open Decisions

| Decision | Options | Current lean |
|---|---|---|
| Cloud save / analytics / Remote Config | Sprint 12 or post-MVP | Post-MVP |
| Pastel pool promotion | Keep as excluded pool vs promote sub-tints (Ivory, Sage, ...) to mintable keys | Review in `tools/color_pools.py` during D3 content work |

Resolved decisions (character level-up in MVP, rewarded ads in MVP, reward mailbox, all 10 Destinations design decisions) are recorded in `docs/sprint-review.md` and the design docs.

## Post-MVP Backlog (existing designs)

- Destinations Sprints D2-D3 (`docs/plans/2026-07-02-destinations-design.md`): D2 Locations — fragment consumer, procedurally generated one-time visits, dialogue-tree finales (recruit/loot/boost/quest/boss/gold outcomes); D3 — more location parts, key art, destinations balance-sim branch
- Prestige with Stellar Ink (master design §Prestige; `prestigeMultiplier` stubbed at 1.0 throughout)
- Verb drops/collection (`docs/verb-stat-system-design-doc.md`; verbs currently come with the character)
- Save file protection: the local save (`SaveManager`/`SaveData`) is plain JSON, so gold, gear and quest level can be edited by hand; sign or encrypt it (no design yet)
- Additional species, quest zones, seasonal events, social features
