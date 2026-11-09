# Starquill: HCI Review of the UI and Screens

**Date:** 2026-09-16
**Scope:** every screen and sheet in `Assets/Scripts/UI/` (47 files) plus the layout defined by `Assets/Editor/ExploreSceneBuilder.cs`, audited against Nielsen's 10 usability heuristics and Jakob's Law (users spend most of their time in other apps, so the app should behave like the mobile RPGs they already know).
**Method:** code-level audit, then play-mode verification in the Unity Editor at a 1080 x 2280 Game view (a 19:9 phone) against a real save (50/50 inventory, 13 keys, 14-character roster). Every finding cites the file and line that produces it; findings marked **[seen]** were confirmed on screen. Screenshots: `docs/screenshots/01..11-*.png`, contact sheet `docs/screenshots-contact-sheet.png`.
**Purpose:** decide what to alter before Sprint 12's "Full UI Pass 2" and before the artist's chrome lands, and feed the artist request list (`docs/art-asset-list.md`).

Severity scale:

| Code | Meaning |
|---|---|
| S1 | Blocks a core loop or hides a game state the player must act on. Fix before any art work. |
| S2 | Costs the player wrong decisions, lost taps, or lost items. Fix in the UI pass. |
| S3 | Friction or inconsistency. Fix when the surface is touched anyway. |

Heuristic tags: **H1** visibility of system status · **H2** match between system and real world · **H3** user control and freedom · **H4** consistency and standards · **H5** error prevention · **H6** recognition over recall · **H7** flexibility and efficiency · **H8** aesthetic and minimalist design · **H9** help users recognize and recover from errors · **H10** help and documentation · **JL** Jakob's Law.

---

## 1. Executive summary

The July 2026 mobile redesign fixed the density problem: a real token system (`UiTheme`), one item card, one bottom-sheet flow, 28 px type floor, 120 px touch targets. The screens built after it (Quests, Shop, keys, tavern) sit on that system. The problems that remain are of three kinds:

1. **Regressions the redesign never reached.** The Party screen's portrait strip, roster grid, Actions tab, Train and Optimize buttons still carry 10 to 26 px text and sub-120 px targets from Sprint 6 (§4, P1). The Party sub-tabs have no active state (P2).
2. **Game state the player cannot see.** The top bar's fragment counter is a hard-coded placeholder (E4). A full bag silently discards every drop (E5). A dungeon started from the Quests screen runs its timer while the player is still on the Quests screen (Q1). Key drops never surface on Explore (E6). The Remove Ads button shows no price (S1).
3. **Color is the only carrier of the core mechanic.** Verb stat type and enemy stat type, the rock-paper-scissors matchup the whole game is built on, are encoded only as six hues with no text or icon (E1, E2), and those hues collide with the rarity, delta, and button palettes (C1).
4. **The canvas is narrower than the layouts assume on real phones.** The scaler matches height, so a 19:9 phone gets a 909 px wide canvas and a 20:9 phone 864 px, while the verb bar, top bar, party header and enemy column are laid out for 1080. The third verb card, the boss ladder node and the third enemy are clipped today (C5, seen in every screenshot).

Twelve changes cover most of the value; they are listed in §9 as Tier 0 and are all code-only.

---

## 2. Baseline: what already conforms

Keep these; the artist's chrome should drop into them without changing their behavior.

- `UiTheme` tokens: type scale 44/38/34/28, 8 px grid, 120 px touch minimum, 130 px primary button. `UiFactory` primitives (Text, Button with disabled-reason labels, Chip, PipRow, ProgressBar, SegmentedTabs, HeaderBar, EmptyState, Banner, LadderRow).
- `ItemCardBuilder` v2 (240 px card, rarity-framed 200 px icon, stat pair, ability pips, meta, delta chip) used by loot list, drawer, party slots, and both completion sheets. One card everywhere (H4).
- `BottomSheet`: scrim tap and Back close it; stacked sheets sort correctly; content is a vertical layout. Every detail and confirmation flow uses it (H4).
- Disabled buttons explain themselves ("Need 1.2Kg", "Ad not ready", "Recruited") via `ButtonHandle.SetEnabled` (H9).
- Retreat has a confirmation sheet that states the cost (H5). Dismissing the offline sheet still pays the plain amount (H5).
- Full-inventory rewards go to a persisted mailbox with a gold notice card on the Loot screen (H1, H5).
- Key fusion dims invalid partners and prints the rule they break (H9, H10). The CONFIRM gate re-evaluates on every gold change (H1).
- Quest discovery is redundant: pulsing banner on Explore and a card on Quests (H1).
- Presenters (`QuestPresenter`, `KeyPresenter`, `ShopPresenter`, `TavernPresenter`) keep copy in one testable place.

---

## 3. Explore screen

| ID | Sev | Heuristic | Finding | Evidence | Recommendation |
|---|---|---|---|---|---|
| E1 | S2 | H6, H2, accessibility | Verb cards show only the verb name on a stat-colored rectangle. The stat type, the thing the player must match against the enemy, is color-only. | `VerbBarDisplay.CreateCard` lines 477-511: background `StatTypeColors.GetColor(statType)`, one text label, no stat text or icon. | Add a stat chip (abbreviation + stat icon) on every card and an advantage marker when the drawn verb is Strong against a living enemy. Art: stat icons (§art 3), verb card frame (§art 4). |
| E2 | S2 | H1, H6 | Enemies are untinted silhouettes with a 12 px HP bar under their feet. No name, no stat type label, no boss marker. The player cannot tell what they are fighting or why a verb was strong. | `EnemyDisplayController.SetupEnemies` lines 977-1006 (silhouette tint only), `CreateHPBar` (bar anchored below, 12 px). Bosses render identically (design docs note this). | Nameplate above each enemy: name, stat icon, HP bar; boss/mini-boss aura overlay and scale-up. Art: enemy nameplate 9-slice, stat icons, aura. |
| E3 | S2 | H1, H5 | The verb bar is destroyed and rebuilt every combat tick (once per second). A tap that lands on the rebuild frame is lost; there is no draw animation (`VerbCardAnimator` has no callers); lock state is a gray tint with no countdown. | `ExploreSceneController.HandleCombatTick` line 184 → `RebuildFromSlots` every tick; `VerbBarDisplay.UpdateLockVisual`. `grep AnimateCardIn` finds no call sites. | Diff drawn slots against existing cards and only replace changed ones. Radial cooldown fill for the 10 s draw timer, 3 s lock shown as a sweep on the tapped card. Wire `VerbCardAnimator` for slide-in. |
| E4 | S1 | H1, H4 | **[seen]** The top-bar fragment counter reads "7/12" forever (every screenshot shows it while the Quests screen shows the real 14/100). `SetFragments` is only called on the no-GameManager placeholder path; the label keeps the builder's default text in real play. | `ExploreSceneController.SetupPlaceholderUI` line 371 is the only real call; `DeferredInitialSync` never sets fragments. Builder line 924 seeds "7/12". | Fragments have no consumer until Destinations D2. Remove the counter from the top bar and show the key count there instead (keys are live and actionable). Keep the fragment bar on the Quests screen. |
| E5 | S2 | H1, H5 | With 50/50 items, every drop is discarded silently. No toast, no counter color change, no nav badge. | `GameManager.ProcessLootDrops` line 1096: `AddItem` false → nothing fires. | "Bag full" cue: toast on first discard, inventory count turns `DeltaDown` at capacity, Loot nav badge. Consider a "sell Commons automatically when full" toggle later (H7). |
| E6 | S3 | H1 | Key drops (0.4%/kill, the rarest exciting event) never appear on Explore; only the Quests screen subscribes to `OnKeysChanged`. | `QuestsScreenController.HandleKeysChanged` is the sole listener; `LootToastFeed` handles `OnLootDropped` only. | Key toast in `LootToastFeed` with the key icon; Quests nav badge. |
| E7 | S3 | H8, type floor | Auto-attack damage numbers are 24 px (below the 28 px floor) and one spawns per enemy per tick. | `DamageNumberSpawner.SpawnDamage` line 1348: `isVerbHit ? 32 : 24`. | 28 px minimum; batch auto-attack numbers per enemy per tick; keep verb hits larger and advantage-colored. |
| E8 | S3 | H8 | The toast feed (3 rows, y 208-428 from the panel bottom) overlaps the front party dolls, whose rects reach y ≈ 500. | Builder lines 109-113 (doll rects) vs 178-179 (feed rect). | Give toast rows a sprite background with a light scrim, or move the feed to the right column above the enemies. Art: toast background. |
| E9 | S3 | H1 | Wave clear and enemy death have no visible event beyond a fade; the wave counter increments. | `EnemyDisplayController.Update` fade only. | Sprint 12 juice pass: death poof, wave-clear sweep, coin burst (already in the art list §1.3). |
| E10 | S3 | H1 | Dungeon HUD shows m:ss but no urgency state near the end of a run. | `DungeonHudDisplay.Refresh`. | Accent turns `DeltaDown` and pulses under 30 s. |
| E11 | S2 | H1 | A character that can level up, an ability that can level, and a ready chest are invisible from Explore. The Level Up glow lives on the Party header only. | `CharacterFocusDisplay.UpdateLevelUpButton`; no nav badge system exists. | Nav badge dots: Party (level-up available), Loot (mailbox or bag full), Quests (offer pending), Shop (chest ready, tavern refreshed). Art: badge dot. |
| E12 | S3 | H1, H2 | **[seen]** The welcome-back sheet reports the raw away time ("Away 1818h 00m · +17.0K gold") while the payout is capped at 8 h, so the numbers contradict each other. | `OfflineClaimSheet` uses `ShopPresenter.OfflineSummary(gm.PendingOfflineSeconds, ...)` with the uncapped seconds. | Show the capped time: "Away 8h (max) · +17.0K gold", and a caption "Earnings cap at 8 hours". |

Layout note for the artist: the combat area is 1080 x 1490 (top bar 140, path bar 56 inside it, bottom nav 160, verb bar 200 overlapping the combat area's bottom 70 px). Party dolls occupy the left 55% (four 360/440 px squares), enemies the right 35% (three 240 x 360 slots).

---

## 4. Party screen

| ID | Sev | Heuristic | Finding | Evidence | Recommendation |
|---|---|---|---|---|---|
| P1 | S1 | H4, type floor, touch targets | **[seen]** Text and targets that predate the redesign and were never converted: portrait strip names 11 px and levels 10 px; roster card name 16 px and party badge 20 px; Actions tab 14-20 px; Train label 26 px; Optimize button 40 px tall with an 18 px label. The redesign set 28 px / 120 px as invariants. | Builder lines 458, 467 (portrait labels), `RosterGridDisplay` 364/391, `ActionLoadoutDisplay` 949-1126, builder 541 (Train), 675-677 (Optimize). | Convert all to `UiFactory` text styles; Optimize becomes a 120 px secondary button or moves into the header bar like Loot's Optimize All; Train becomes a full-width primary button under the stat column. |
| P2 | S1 | H1, H4 | **[seen]** The Roster / Equipment / Actions sub-tabs have no active state. All three are identical flat buttons; selecting one changes nothing about the bar. | Builder 565-575 creates plain buttons; `PartyScreenController.SetSubTab` never restyles them. | Replace with `UiFactory.SegmentedTabs` (already used on Loot) so the pattern matches (H4). |
| P3 | S1 | H3, JL | There is no way to change the active party. The roster grid only selects a character for the focus view; `CharacterRoster.AddToParty` / `RemoveFromParty` / `SetPartyMember` have no UI callers. Benched characters earn half XP and the roster cap is 20, so this matters. | `RosterGridDisplay.CreateCard` (select only); `grep AddToParty\|RemoveFromParty` finds only `GameManager` line 793 (recruit auto-join). | Header action button: "Add to party" when benched, "Bench" when in the party; when the party is full, tap-to-swap with the portrait strip (strip highlights, tap a slot). Rebuild party and verb pool after (the level-up path already does this). |
| P4 | S2 | H1, H3, glyph rule | **[seen]** The Actions tab is a dead end (screenshot 07: 14-20 px text, a box glyph after "30 dmg", two-thirds of the panel empty): verb cards and the empty slot are buttons whose events (`OnEquippedSlotTapped`, `OnEmptySlotTapped`) have no subscribers; the "N verbs available" drawer bar never opens; the cooldown uses a Unicode ⏱ that renders as a box. | `ActionLoadoutDisplay` lines 913-914, 1090, 1098, 1130; grep finds no subscribers. | Until verb drops ship (post-MVP), make it a read-only "Verbs" list on `UiTheme` styles with no button affordance, keep the pool summary (it is the one place the stat-coverage gap is shown), drop the drawer bar. Or hide the tab. |
| P5 | S2 | H8 | The focus paper doll is a 400 x 400 render stretched into a 760 x 560 `RawImage` with no aspect fitter, which widens the character by about 36% on a 9:16 canvas. On the 909 px test canvas the area is 589 x 560, so the distortion was barely visible in the screenshots; it returns on 9:16 tablets and in the 1080-wide layout fix (C5). | Builder 483-494 (`StretchFill` into the 760 x 560 area); `CharacterFocusDisplay.RenderPaperDoll`; `CharacterDisplay` texture 400. No `AspectRatioFitter` anywhere in the UI. | Add `AspectRatioFitter` (FitInParent, ratio 1) to the doll image; the same guard on every `RawImage` that shows a composited texture. |
| P6 | S3 | H4 | Species label shows the raw id ("dwarf") while the tavern capitalizes it. | `CharacterFocusDisplay.ShowCharacter` line 480 vs `TavernPresenter.SpeciesLabel`. | Use `TavernPresenter.SpeciesLabel` (move it to a shared presenter). |
| P7 | S3 | H6 | The equipment drawer hides the slot list, so the currently equipped item is not visible while browsing candidates; only delta chips reference it. Drawer Back is 320 x 90 (under 120). | `PartyScreenController.HandleEquipmentSlotTapped` hides `equipmentListArea`; builder 718-723. | Pin the equipped item as a dimmed "Equipped" card at the top of the drawer list; Back to 120 px height. |
| P8 | S3 | H8 | **[seen]** Roster selection is a solid yellow card fill; party membership is a bare number. | `RosterGridDisplay` 349, 390. | Selection ring + party slot badge sprite (art §3). |
| P9 | S2 | H1, H7 | **[seen]** Level-up is a manual tap and XP keeps accruing past the threshold: the test character shows "18855/1412 XP", thirteen levels banked behind one button that must be tapped thirteen times. | `CharacterFocusDisplay.UpdateXpDisplay`; `PartyScreenController.HandleLevelUp` levels once per tap. | Auto-level on the tick (idle-genre convention) with a toast, or a "Level Up x13" button that applies all banked levels. Either way the XP label shows progress within the current level. |
| P10 | S2 | H8, C5 | **[seen]** The header row wraps at 909 px: "Jace the / Fierce", "catfol / k", "Lv / 16", "18855/1412 / XP"; the stat column wraps "STR" and "12+20" onto two lines. | Builder 347-357 (five flexible-width labels in a 120 px strip), 525-532 (180 px column at 32 px). | Header becomes two lines by design (name; species · Lv · XP), the Level Up button moves to the stat column above Train, and the stat column widens to 220 px. Depends on the C5 decision. |

---

## 5. Loot screen and item sheet

| ID | Sev | Heuristic | Finding | Evidence | Recommendation |
|---|---|---|---|---|---|
| L1 | S2 | H5, H3 | Sell is one tap, immediate, irreversible. The redesign spec promised an undo toast; it was not built. A mis-tap on a Legendary is unrecoverable. | `ItemDetailSheet` 640-651 → `GameManager.SellItem`; `grep -i undo` finds nothing. | Undo toast (4 s) backed by a single-item "last sold" buffer that restores the item and refunds the gold. Keys (Q2) get the same. |
| L2 | S2 | H6, H1 | The delta chip on every loot card ("+4") compares against whichever roster member gains most, chosen silently. The player cannot tell who "+4" is for. | `LootScreenController.FindBestUpgradeTarget` 158-176. | Put the target on the card: "+4" chip plus the target's head portrait (56 px) or first name. Art: portrait ring. |
| L3 | S1 | H8, H4 | **[seen]** The "Equip on" picker is a `SegmentedTabs` row containing every roster name (up to 20) in one row; with 14 characters it renders as a wall of two-letter fragments that also overlaps the EQUIP / Sell row (screenshot 04). The design said four party portraits with the best match ringed. | `ItemDetailSheet` 596-610. | Horizontal scroll of head portraits, party members first, a divider, then benched; gold ring on the pre-selected best target. |
| L4 | S3 | H1 | Optimize All (and the Party tab's Optimize) gives no feedback about what changed. | `LootScreenController.HandleOptimizeAll`. | Result toast or sheet: "Equipped 3 upgrades on Bram, Ysolde". |
| L5 | S3 | H7, JL | No slot filter, no "new since last visit" marker, no bulk sell. Mobile RPG bags conventionally offer "sell all below Rare". | `LootScreenController` sort only. | Sprint 12 or post-MVP: slot filter chips, NEW marker, "Sell all Common" with confirm. |
| L7 | S2 | H2 | **[seen]** Item names leak raw asset names: "Rare Quarter helmet det", "Shirt with breasts", "Heavy armor" (screenshots 03, 04, 06). Players read these as the game's voice. | `EquipmentInstance.DisplayName` from `equipment.json` names. | Display-name table per item type (artist or writer supplies ~60 names), keep asset ids internal. |
| L8 | S3 | H8 | **[seen]** Small items (glasses, belt) render as a thin sliver inside the 200 px frame because the per-category crop is tuned for hats and torsos. | `ItemIconFraming.GetFrame`. | Per-type crop overrides for `mc` (misc) and eyewear; or a minimum-fill rule that zooms until the sprite's alpha bounds fill 70% of the frame. |
| L6 | S3 | H8 | Sheets use fixed height fractions (0.38-0.7). Item detail content is ~1000 px at 0.55 (1056 px minus insets), so long ability descriptions crush rows; `DungeonCompletionSheet` already worked around the same problem with an inner scroll. | `BottomSheet.Show` heightFraction; `ItemDetailSheet.Fill`. | Size the panel to content up to 0.85, scroll inside beyond that. |

---

## 6. Quests screen and key sheets

| ID | Sev | Heuristic | Finding | Evidence | Recommendation |
|---|---|---|---|---|---|
| Q1 | S1 | H1, H3 | OPEN DUNGEON starts a timed run (150 s + 20 s per difficulty) and closes the sheet, leaving the player on the Quests screen while the countdown runs. Verb taps are how bonus rolls are earned. | `KeyDetailSheet` 869-873: `StartDungeon` then `Close`; no `ShowScreen(0)`. The quest card's RETRY does navigate (`QuestsScreenController` 256). | Navigate to Explore on a successful start, same as RETRY. |
| Q2 | S2 | H5 | Key SELL is immediate with no confirm or undo; keys are rare. | `KeyDetailSheet` 890-896. | Same undo toast as L1, or a confirm for difficulty ≥ 3. |
| Q3 | S3 | H1 | The fragments bar shows progress toward a system that is LOCKED. Acceptable as a tease on this screen, but the top-bar duplicate (E4) is broken. | `QuestsScreenController` 89-97, 100. | Keep here with copy "Locations arrive in a later update"; remove from the top bar. |
| Q4 | S3 | H6 | Ladder nodes carry E/H letter markers and no labels; the player recalls the tier meaning from the offer sheet. | `UiFactory.LadderRow` 518-528. | Tap a node for a small tooltip (tier, done/current); art: tier badges (§art 3). |
| Q5 | S3 | H4 | Tier chip colors reuse rarity hues: Elite purple = Epic, Hard orange = Legendary, Boss gold = BestGold. | `QuestOfferSheet` 608-614 vs `ItemDisplayData.RarityColors`. | Tier badges as shapes (icons) rather than hues; see C1. |
| Q6 | S3 | H8 | The zone header is text only; zones have no visual identity outside the (future) backgrounds. | `QuestsScreenController` 77-81. | Zone emblem 200 px per biome (art §3). |

---

## 7. Shop screen

| ID | Sev | Heuristic | Finding | Evidence | Recommendation |
|---|---|---|---|---|---|
| S1 | S1 | H1, JL, store policy | Remove Ads shows a "Purchase" button with no price. | `ShopScreenController.RemoveAdsCard` 326. | Label from the IAP service's localized price ("Purchase · $2.99"); disabled with reason until the store returns a price. |
| S2 | S2 | H5, H1 | While visible, the entire shop content is destroyed and rebuilt once per second (and on every gold change). A tap landing on a rebuild frame is dropped; scroll can jump; six tavern portraits are re-parented every second. | `ShopScreenController.Update` 63-72 → `Refresh` destroys `content` children. | Build once per visibility change; tick only countdown labels and `SetEnabled` states. |
| S3 | S2 | H1 | Claiming the chest or buying a boost produces no visible result on this screen. The chest's item fires `OnLootDropped`, whose only listener is the Explore toast feed, which is inactive while the Shop is shown. | `GameManager.ClaimChest` fires `OnLootDropped`; `LootToastFeed.HandleLootDropped` returns when inactive. | Claim result sheet (reuse the completion sheet layout: gold line + item card). Boost purchase: button flips to "Active · 5:00" (already does) plus a short toast. |
| S4 | S3 | H1, H5 | The recruit sheet does not say where the recruit will go (open party slot vs bench) before purchase. | `TavernRecruitSheet.Fill`. | One caption: "Joins party slot 3" or "Joins the bench (party full)". |
| S5 | S3 | H4 | Section headers on Quests and Shop are duplicated code with identical output. | `QuestsScreenController.SectionHeader`, `ShopScreenController.SectionHeader`. | Move to `UiFactory.SectionHeader`. Art: section rule (§art 2). |

---

## 8. Navigation, sheets, and cross-cutting

| ID | Sev | Heuristic | Finding | Evidence | Recommendation |
|---|---|---|---|---|---|
| N1 | S2 | JL, H3 | The Android back button does nothing anywhere. Android users expect back to close a sheet or drawer, then return to the first tab, then leave the app. | No `KeyCode.Escape` / back handling in `UI/` or `Managers/`. | Back stack: open sheets and the equipment drawer register; `BottomNavDisplay` handles tab fallback to Explore. |
| N2 | S3 | H4, JL | The sheet's "drag handle" is a false affordance: it closes on tap, does not drag. | `BottomSheet.Show` 727-736. | Implement drag-to-dismiss (threshold 25% of panel height) or draw it as a plain divider. |
| N3 | S3 | H4, touch target | Sheet close is labeled "Back" and is 320 x 90. Convention is a close control, 120 x 120, top-right. | `BottomSheet.Show` 738-754. | 120 px close button with the close icon (art §3); keep scrim tap. |
| N4 | S2 | JL, H1 | Bottom nav is text-only (32 px bold labels, underline active state), no icons, no badges. Every comparable mobile RPG uses icon + label tabs with badge dots. | `BottomNavDisplay.CreatePlaceholderButtons`. | Icon (96 px) above a 28 px label, active tint, badge dot slot top-right of the icon. Art: 5 icons x 2 states, badge dot. |
| N5 | S3 | H1 | Screen switches are instant `SetActive` toggles. | `ScreenManager.ShowScreen`. | Sprint 12 juice: 150 ms crossfade or slide. |
| N6 | S2 | H10, H6 | Nothing in the app explains the two stat triangles (Physical STR > DEX > CON, Mental INT > WIS > CHA), which is the core skill. There is no help surface and no tutorial yet. | No help sheet exists; tutorial is Sprint 12 scope. | "?" button on the verb bar opening a help sheet with the triangles diagram; FTUE covers tap-a-strong-verb. Art: triangles diagram, tutorial pointer and callout. |
| C1 | S2 | H4, accessibility | Hue collisions across independent meanings: DEX green ≈ Uncommon green ≈ DeltaUp green ≈ AccentGreen (every primary button); STR red ≈ DeltaDown ≈ Destructive buttons; CHA yellow ≈ BestGold; WIS purple ≈ Epic ≈ Elite; INT blue ≈ Rare; Legendary orange ≈ Hard. A color-blind player loses stat and rarity entirely. | `StatTypeColors`, `ItemDisplayData.RarityColors`, `UiTheme` accents, `QuestOfferSheet` tier colors. | Rule: stat type is always icon + text, color is reinforcement. Rarity is frame ornament + color. Delta green/red stay exclusive (already the rule). Move the action accent off green (teal or amber, artist's call) so buttons stop reading as "upgrade". |
| C2 | S1 | type floor, touch targets | Regressions listed in P1, P7, N3, plus damage numbers (E7). | See rows. | One sweep converting every remaining `CreateTMPLabel` size and every sub-120 button in `ExploreSceneBuilder`, `RosterGridDisplay`, `ActionLoadoutDisplay`, `DamageNumberSpawner`. |
| C5 | S1 | H8, device fit | **[seen]** `CanvasScaler` matches height, so the canvas width shrinks on tall phones: 909 px at 19:9 (the test view), 864 px at 20:9. Layouts sized for 1080 overflow: the third verb card is cut off, the boss diamond on the quest ladder is clipped, the third enemy slot sits off-screen, the party header wraps (P10). Every screenshot shows the verb bar defect. | Scene `CanvasScaler` (match 1.0); builder verb grid 3 x 340 + 15 spacing; enemy container anchored 0.65-1; header labels. | Switch the scaler to **match width** (0.0): the canvas is then always 1080 wide and grows taller on tall phones (2280 at 19:9). Top bar and nav are anchored to their edges already; the Explore combat area and every scroll list stretch. Then re-run the layout at 9:16, 19:9 and 20:9 Game-view presets and add those three presets to the play-test checklist. |
| C6 | S3 | H1 | **[seen]** The nav underline and the segmented-tab underline are the only active-state cues, 8 px tall, and the Party sub-tabs have none (P2). | `BottomNavDisplay`, `UiFactory.SegmentedTabs`, builder 565-575. | Active tab gets a filled background plus the underline; the artist's tab sprite (art §2) carries both. |
| C3 | S3 | performance, H5 | Refresh-by-rebuild everywhere (destroy children, rebuild). Fine on event-driven screens; harmful where it runs on a timer (S2) or per tick (E3). | See S2, E3. | Fix those two; leave the rest. |
| C4 | S3 | glyph rule | One remaining Unicode glyph (⏱) in the Actions tab; em dashes in copy render fine in LiberationSans but will need checking against the new font. | `ActionLoadoutDisplay` 1090; `QuestsScreenController` 253, `QuestCompletionSheet` 766, `ShopScreenController` 319. | Replace ⏱ with the clock icon; re-check dashes when the game font lands. |

---

## 9. Recommended alterations, in order

### Tier 0: code-only, before the artist's chrome lands (est. 2-3 days)

| # | Change | Fixes |
|---|---|---|
| 0 | `CanvasScaler` → match width; re-test at 9:16, 19:9, 20:9 | C5, P10 |
| 1 | Top bar: drop the fragment counter, show key count (bind to `OnKeysChanged`) | E4, Q3 |
| 2 | Party sub-tabs → `UiFactory.SegmentedTabs`; type-floor and touch sweep on the Party screen and damage numbers | P1, P2, C2, E7 |
| 3 | Party membership control: Add to party / Bench / swap | P3 |
| 4 | Actions tab → read-only Verbs list (keep pool summary), remove ⏱ | P4, C4 |
| 5 | `AspectRatioFitter` on the focus doll and every composited `RawImage` | P5 |
| 6 | OPEN DUNGEON navigates to Explore | Q1 |
| 7 | Undo toast for item and key sells | L1, Q2 |
| 8 | Bag-full cue (toast + red count) and nav badge plumbing (Party level-up, Loot mailbox/full, Quests offer, Shop chest) | E5, E11 |
| 9 | Android back stack (sheets, drawer, tabs) | N1 |
| 10 | Remove Ads price label; shop builds once and ticks labels | S1, S2 |
| 11 | Verb bar: diff slots instead of rebuilding per tick; lock countdown | E3 |
| 12 | Species label capitalization; shared `SectionHeader` | P6, S5 |
| 13 | Auto-level (or "Level Up xN"); capped away-time copy | P9, E12 |
| 14 | Item display-name table; portrait-based target picker (interim: party members only, 4 tabs) | L7, L3 |

### Tier 1: lands with the art drop-in

| Change | Fixes | Art |
|---|---|---|
| Stat icon + abbreviation chip on verb cards; advantage marker | E1, C1 | stat icons, verb card frame, advantage marker |
| Enemy nameplates (name, stat icon, HP) and boss aura | E2 | nameplate 9-slice, aura |
| Nav icons + badge dots | N4, E11 | 5 icons x 2 states, badge dot |
| Loot delta chip shows target portrait; portrait-based target picker in the item sheet | L2, L3 | portrait ring |
| Key toast on Explore | E6 | key icon |
| Close icon on sheets, 120 px | N3 | close icon |
| Toast background sprite | E8 | toast 9-slice |
| Tier badges as shapes; zone emblems | Q4, Q5, Q6 | tier badges, zone emblems |
| Action accent moved off green | C1 | palette decision by the artist |

### Tier 2: Sprint 12 (tutorial, juice, polish)

| Change | Fixes |
|---|---|
| FTUE: tap a strong verb, equip loot, accept a quest; help sheet with the triangles diagram | N6 |
| Verb draw animation, death poof, wave-clear sweep, coin burst, screen transitions, dungeon urgency state | E3, E9, E10, N5 |
| Chest claim result sheet, Optimize feedback, recruit destination caption | S3, L4, S4 |
| Drag-to-dismiss sheets, content-sized sheet height | N2, L6 |
| Slot filter, NEW marker, bulk sell | L5 |
| Drawer shows the equipped item pinned | P7 |

---

## 10. Decisions needed

| Decision | Recommendation | Why |
|---|---|---|
| Fragment counter in the top bar | Remove until Destinations D2; show keys instead | It is a stale placeholder today and fragments have no consumer |
| Actions tab | Read-only "Verbs" list, keep the pool summary | The tab's only live information is the party's stat coverage; verb collection is post-MVP |
| Sell safety | Undo toast for items and keys (not a confirm) | One tap to sell is the mobile convention; undo preserves speed and recovers mistakes |
| Action accent color | Let the artist pick a non-green accent for buttons | Green currently means four different things |
| Nav tab order | Keep Explore · Quests · Loot · Party · Shop | Matches the genre; Explore first is where the player returns on back |
| Canvas scaling | Match width (canvas always 1080 wide) | Every fixed-width row already assumes 1080; tall phones then gain vertical room instead of losing horizontal room |
| Character level-up | Auto-level with a toast | Manual taps with a 13-level backlog is busywork; the Level Up button becomes a "Train" companion only if the design wants a ceremony |

---

## 11. What this review did not do

- Play-mode verification ran at one aspect (19:9) with one save. The doll distortion (P5) and toast overlap (E8) are derived from the builder's rect math at 9:16 and were not reproduced at 19:9; confirm on a 9:16 device or after the C5 change.
- No device build: Android back handling (N1), safe-area insets and touch accuracy were reviewed in code only.
- No audio review; no localization review (all strings are hard-coded English, acceptable for MVP).
- No performance profiling beyond the rebuild patterns noted in C3.
