# Starquill: Placeholder Art Inventory & Artist Request List

**Last updated:** 2026-09-16 (fourth pass: delivery criteria added, sizes re-verified against the scene builder, parallax heights corrected, HCI-review additions in §2-§4 and the new §9, priority column on every table)
**Status:** Hand-off ready. Remaining opens in §10.
**Companion:** `docs/hci-review.md` explains which UI changes this art supports.

## 0. Delivery criteria (read first)

| Criterion | Rule |
|---|---|
| Reference canvas | Portrait **1080 x 1920**. Every size below is in reference pixels (1x). The engine scales by height, so on a 1440 x 3200 phone 1 reference px ≈ 1.67 device px. |
| Export scale | **UI chrome, icons, cards, key art: export at 2x** (double the listed size) so they stay crisp on 1440p and tablets. **Backgrounds and parallax strips: 1x only** (they are already wider than the screen; 2x would exceed GPU texture limits). |
| Texture limits | No single PNG wider or taller than **4096 px**; prefer **≤ 2048** for anything that is not a parallax strip. Non-power-of-two sizes are fine. |
| Format | PNG-32 (RGBA, straight alpha, not premultiplied), sRGB. 2 px of transparent bleed on every side of an icon or sprite that is not 9-sliced. |
| 9-slice pieces | Deliver with a note of the corner/border size in px (e.g. "border 24"). Corners carry all the detail; the stretch zones (edges and center) must be flat or a vertical-only gradient, never a pattern. Minimum deliverable size = 2 x border + 8. |
| Tintable pieces | Anything marked **tintable** ships as white/gray on alpha (luminance only). The engine multiplies by a color: stat colors, rarity colors, key family colors. Do not bake hue into tintable pieces. |
| States | Buttons and nav icons need **normal + disabled** at minimum; pressed is optional (the engine darkens). Icons: single weight, no drop shadows (the engine adds outline/shadow through the font/material). |
| Readability test | Every icon must read at **50% of its listed size** in grayscale. Key icons must read at 104 px with all three modifier layers on. |
| Text-safe zones | Cards, banners, buttons and verb cards carry procedurally generated text (names, prices, dialogue). Keep a **quiet center field** with no detail; mark the text-safe rect in the source file. |
| Sources | Deliver the layered source (Aseprite/PSD/Krita) plus flattened PNGs. |
| Naming | `ui_<group>_<name>[_<variant>]@2x.png`, lowercase, no spaces. Groups: `chrome`, `icon`, `nav`, `verb`, `key`, `bg`, `vfx`, `shop`, `app`, `tut`. Variants: `normal`, `disabled`, `active`, rarity/stat/family names. |
| Priority | **P0** needed for the Sprint 12 UI pass and the shippable build · **P1** ship-quality polish, can land after the pass · **P2** Destinations D2/D3 and post-MVP. Order work P0 → P1 → P2. |

## Context for the artist

- The **only final art** in the game today is the paper-doll library: character species parts (27 species), equipment sprites, and weapon sprites, each a **200 x 200 PNG part** composited live into 400 x 400 characters. Characters, portraits, and item icons are all built from these: **no new art needed there**.
- Everything else on screen is a flat colored rectangle or plain text. Every asset below is a real request.
- Current placeholder theme is dark: background `#1A1A26`, card `#232330`, sheet `#2C2C3A`. Accent green `#2E8B57`, gold `#FFD600`. **The artist may redefine the palette**, with one constraint from the HCI review: upgrade-green / downgrade-red are reserved for "better / worse" and must stay distinct from stat colors, rarity colors and the button accent (today the button accent is green and collides; please pick a non-green action accent).
- UI rule already in code: no Unicode glyphs in text: every pip, arrow, coin, and icon is an Image, so all of these become sprite slots.
- Most UI chrome is built from a small set of shared primitives (card, button, chip, pip, progress bar, banner, segmented tab). **9-slice sprites drop into these with minimal code change**, so one good chrome set covers every screen.
- Screen layout the chrome must fit (all screens): top bar 1080 x 140 · content area 1080 x 1620 · bottom nav 1080 x 160. Explore content: path bar 56 tall under the top bar, combat area 1080 x 1490, verb bar 1080 x 200 at the bottom (overlaps the combat area's lowest 70 px). Lists use a 24 px side gutter, so full-width cards are **1032 px wide**.

Cross-cutting variant families (these key many assets below):

| Family           | Count | Values                                                                                                                                           |
| ---------------- | ----- | ------------------------------------------------------------------------------------------------------------------------------------------------ |
| Rarity           | 5     | Common (gray), Uncommon (green), Rare (blue), Epic (purple), Legendary (orange)                                                                  |
| Stat type        | 6     | STR (red), DEX (green), CON (brown), INT (blue), WIS (purple), CHA (yellow)                                                                      |
| Verb category    | 2     | Physical (STR/DEX/CON), Mental (INT/WIS/CHA)                                                                                                     |
| Equipment slot   | 11    | Head, Torso, Arms, Legs, Feet, MainHand, OffHand, Misc x4                                                                                        |
| Key slot         | 7     | Head, Torso, Arms, Legs, Feet, Weapon, Misc                                                                                                      |
| Key color family | 10    | Red, Orange, Brown, Yellow, Green, Blue, Purple, Neutral, White, Black                                                                            |
| Class archetype  | 15    | Warrior, Juggernaut, Battlemage, Warden, Warlord, Skirmisher, Saboteur, Ranger, Trickster, Sentinel, Guardian, Champion, Sage, Occultist, Oracle |
| Quest tier       | 4     | Normal, Elite, Hard, Boss                                                                                                                        |
| Status effect    | 6     | Stagger, Bleed, Weaken, Expose, Reveal, Confuse                                                                                                  |
| Biome            | 4     | Forest (Verdant Hollow), Mountains (Gloomspire Ruins), Plains (new), Desert (new): backgrounds, monster palettes, capsule tinting, zone emblems   |

---

## 1. Explore screen: world layer

### 1.1 Parallax backgrounds: 4 biomes x 4 layers = 16 strips (P0 for forest + mountains, P1 for plains + desert)

Four horizontally scrolling, seamlessly tileable strips per biome, **authored at 1x** (never 2x). The engine tiles them by width and stretches each to its on-screen band, so author exactly the on-screen height. Scroll speeds: sky 5, mid 15, ground 30, foreground 50 px/s. Left and right edges must tile perfectly.

| Layer              | Size (tileable width x height) | Role                                                                         |
| ------------------ | ------------------------------ | ---------------------------------------------------------------------------- |
| Sky / far backdrop | 2160 x 1490                    | Fills the whole combat area. The lowest 70 px sit under the verb bar; the top 56 px sit under the path bar. Keep the horizon in the middle third. |
| Midground          | 2160 x 840                     | Hills, treeline, structures: zone identity lives here                        |
| Ground band        | 2160 x 420                     | The strip the party walks on. Characters' feet land at about y = 130-220 above the strip's bottom edge. |
| Foreground overlay | 2160 x 140, **alpha**          | Props drawn OVER characters' feet; fully transparent above the ground line   |

Why 2160 wide: two screen widths, so a 1080 px viewport never shows the seam twice. Anything from 1620 to 2160 works if the seam tiles.

The four biomes are **forest, mountains, plains, desert** (confirmed). The two zones already in game data map onto the first two; plains and desert zones get names/stats when their data is authored.

| #   | Biome         | Zone in data                                  | Dominant stats | Sky                                                                                          | Midground                                                                 | Ground                                               | Foreground                                                     |
| --- | ------------- | --------------------------------------------- | -------------- | -------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- | ---------------------------------------------------- | -------------------------------------------------------------- |
| 1   | **Forest**    | Verdant Hollow                                | STR, CON       | Warm daylight through canopy gaps, drifting pollen/leaves                                    | Dense treeline, claw-marked trunks, animal dens, a distant warden tower   | Root-woven dirt trail, mossy stones, paw prints      | Tall grass, ferns, the odd bone or snapped trap                |
| 2   | **Mountains** | Gloomspire Ruins                              | INT, WIS       | High cold sky, dusky violet haze, a looming ruined spire on the crags, faint floating glyphs | Stone peaks, broken colonnades and switchback paths carved into rock      | Cracked flagstone ledge, scattered scrolls and scree | Rubble chunks, guttering ward-candles, drifting page fragments |
| 3   | **Plains**    | (new zone, name/stats TBD: DEX lean proposed) | DEX (+?)       | Wide open sky, towering clouds, birds on the wind                                            | Rolling grass sea, lone windmill or standing stones, distant caravan line | Wagon-rutted trail through short grass               | Swaying tall grass, wildflowers, darting critters              |
| 4   | **Desert**    | (new zone, name/stats TBD: CHA lean proposed) | CHA (+?)       | Blazing gradient sky, heat shimmer, huge low sun or twin moons                               | Dune ridges, half-buried sandstone ruins, a far oasis with palms          | Cracked hardpan and drifted sand                     | Foreground dune lips, dry scrub, sun-bleached bones            |

Deliverable unit: one 4-layer set per biome. Order forest and mountains first (their zones are playable today). Biome is also the keying axis for monster palettes (§1.2), zone emblems (§3) and location capsule tinting (§5.1).

### 1.2 Enemies: paper-doll composition, two piece sets (P1)

No fixed-dimension enemy sprites are needed. Enemies are assembled the same way player characters are:

1. **Humanoid enemies:** random constructions from the existing player paper-doll library (27 species + equipment). No new art. (Code note, not for the artist: `EnemyState` has no appearance field yet; wiring required.)
2. **Paper-doll monsters:** a modular monster-piece library in the same construction style: interchangeable pieces that combine into monsters. Pieces are **black and white** line art on the existing **200 x 200 part canvas**, colored in-engine (stat-type / zone tinting). The artist already has a small sample set: match its established conventions. **[EXPAND: piece-type roster and per-zone monster palettes once the sample set is reviewed]**

Boss / mini-boss treatment (P0, because bosses render identically to normal enemies today): a **scale-up plus an aura/crown overlay** that can sit behind/over any composed enemy. Display slot is 240 x 360, up to 3 on screen; author the aura at **400 x 520** (tintable, alpha) so it clears a 1.3x-scaled boss.

### 1.3 Enemy nameplate and combat feedback (P0 nameplate, P1 VFX)

The HCI review found that enemies carry no name or stat type and the HP bar sits under their feet. The nameplate is the fix.

| Asset                       | Size                 | Priority | Notes                                                                                                                                                                           |
| --------------------------- | -------------------- | -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Enemy nameplate             | 240 x 64, 9-slice    | P0       | Sits above the enemy: name text (28 px) left, stat icon (32) right, HP bar along the bottom edge. Quiet center. |
| Enemy HP bar (track + fill) | 200 x 12, 9-slice    | P0       | Fill tinted per stat type (6 colors); same sprite family as the UI progress bar (§2) is acceptable |
| Advantage marker            | 40 x 40              | P0       | Small up-arrow/burst shown on a verb card when it is Strong against a living enemy; tintable |
| Hit spark / impact          | 128 x 128 sheet, 6-8 frames | P1 | Per stat family (STR dust/impacts, DEX silver streaks, CON stone/shields, INT blue glyphs, WIS green-gold glow, CHA pink swirls) |
| Death poof                  | 256 x 256 sheet, 8 frames | P1  | Enemy defeat (currently a fade-out)                                                                                                                                             |
| Wave-clear sweep/burst      | 1080 x 300, alpha    | P1       | Horizontal sweep over the combat area; currently nothing happens visually                                                                                                        |
| Coin burst on gold gain     | 64 coin + 8-frame particle sheet | P1 | Pairs with the gold counter                                                                                                                                                     |
| Damage number treatment     | font/material preset | P1       | Currently plain text; 28 px auto hits white, 32 px verb hits colored by advantage (green strong / red weak), gold for gold drops. Deliver as a TMP material spec (outline color/width, shadow) once the font (§7) exists. |

---

## 2. Global UI chrome (shared 9-slice set, used by all 5 screens)

| Asset                            | Size                              | Priority | Variants                                                                                                |
| -------------------------------- | --------------------------------- | -------- | ------------------------------------------------------------------------------------------------------- |
| Screen background                | 1080 x 1920                       | P0       | 1 (or light per-screen theming x5). Sits under every non-Explore screen; low contrast, no focal point |
| Top bar panel                    | 1080 x 140, 9-slice horizontal    | P0       | 1                                                                                                       |
| Bottom nav panel                 | 1080 x 160, 9-slice horizontal    | P0       | 1                                                                                                       |
| Bottom nav tab icons             | 96 x 96                           | P0       | **5 icons** (Explore, Quests, Loot, Party, Shop) x active/inactive. Each tab cell is 208 x 140; icon centered above a 28 px label |
| Nav badge dot                    | 28 x 28                           | P0       | 1, tintable; sits top-right of the icon (new loot, quest offer, level-up ready, chest ready) |
| Tab selection indicator          | 176 x 8                           | P0       | replaces the flat underline (or bake the active state into the icon frame)                             |
| Card / panel                     | 9-slice, border ≤ 24, min 120 tall | P0      | 1 (used everywhere: item cards 1032 x 240, shop/quest cards, tavern rows 1032 x 120, key cards 1032 x 160) |
| Card selected / highlighted      | 9-slice, same border              | P0       | 1: the selected equipment slot, selected roster card, mailbox notice (tintable edge glow)              |
| Bottom sheet panel               | 1080 x 730-1632, rounded top, 9-slice | P0   | 1, + grabber pill 160 x 12 + close button 120 x 120 (see §3 close icon)                                |
| Buttons                          | 9-slice, border ≤ 32, heights 130 / 120 | P0 | 3 kinds (primary accent, secondary gray, destructive red) x normal/disabled. Min width 220             |
| Segmented tab (pill)             | 9-slice, height 120               | P0       | 1 shape x active/inactive: Loot sort (Score/Newest/Rarity), Party sub-tabs (Roster/Equipment/Verbs), sheet target picker |
| Progress bar (track + fill)      | 9-slice, height 8-28              | P0       | 1 set: travel bar (14), quest progress (24), fragment bar (28), XP bars (8), lock timer. Fill is tintable |
| Chip                             | 120 x 72, 9-slice                 | P0       | 1 shape; tinted (delta up/down/neutral, tier colors, LOCKED, key modifiers at 220-300 wide)           |
| Pip (filled + empty)             | 28 x 28                           | P0       | 1 pair: ability levels (20-24 px) + key difficulty (24-32 px); tintable                                |
| Banner accent frame              | 1048 x 100-120, 9-slice, 4 px edge accents | P0 | 1: quest banner + dungeon HUD (accent recolored per state: gold offer, red retreat, green active)      |
| Toast background                 | 1032 x 72, 9-slice                | P0       | 1: loot/key/undo toasts on Explore (currently bare text over the party dolls)                          |
| Section rule line                | 3 px tall, 9-slice horizontal     | P1       | 1: QUEST / DESTINATIONS / BOOSTS headers                                                                |
| Ladder nodes (zone quest ladder) | 72 px node                        | P1       | 5 states: done, current (+ring), ahead, boss (gold diamond), boss-current; plus tier markers (§3 tier badges at 40 px) |
| Scrim                            | flat                              |          | keep procedural                                                                                        |

---

## 3. Icon sets

| Asset                            | Size               | Count          | Priority | Where used                                                                                                                       |
| -------------------------------- | ------------------ | -------------- | -------- | -------------------------------------------------------------------------------------------------------------------------------- |
| Stat icons                       | 48 x 48 (+ 32 variant) | **6**      | P0       | verb cards, enemy nameplates, item stat lines, party stat column, tavern sheet, help diagram. Must read at 28 px next to text. Tintable is fine but each shape must be distinct without color (color-blind rule) |
| Gold coin                        | 48-60              | 1              | P0       | top bar counter, floating gold numbers, sell prices, shop                                                                        |
| Key (currency-level icon)        | 48                 | 1              | P0       | top bar key count (replaces the fragment counter), key toast, pouch header (full key art system is §5)                          |
| Close (X)                        | 48 glyph in a 120 x 120 button | 1  | P0       | every bottom sheet (replaces the "Back" text button)                                                                             |
| Empty equipment-slot silhouettes | 120                | **7** (reused) | P0       | party equipment tab; same 7 glyphs as the key slot glyphs (§5.0): Weapon covers Main/Off Hand, Misc covers the 4 accessory slots |
| Rarity item-icon frames          | 200 x 200, 9-slice | **5**          | P0       | backplate behind every item icon (currently flat tint at 16% alpha). Rarity should read from ornament, not only hue             |
| Portrait frame / ring            | 104, 150, 300      | 1-2            | P0       | tavern rows (104), roster cards (150), recruit sheet (300), portrait strip selection ring, loot delta target (56)               |
| Party-member badge               | 40                 | 1              | P1       | roster grid corner number (1-4)                                                                                                  |
| Quest tier badges                | 64 chip, 40 marker | **4**          | P1       | quest offer sheet, ladder (Normal / Elite / Hard / Boss): shapes, not hue, so they stop colliding with rarity colors            |
| Zone emblems                     | 200 x 200          | **4**          | P1       | Quests screen zone header, quest offer sheet; one per biome                                                                      |
| Fragment shard                   | 48                 | 1              | P2       | Quests screen fragment bar (Locations, D2)                                                                                       |
| Status effect icons              | 32-48              | **6**          | P2       | not surfaced in UI yet: Stagger, Bleed, Weaken, Expose, Reveal, Confuse                                                          |
| Verb category badges             | 40                 | **2**          | P1       | Physical / Mental                                                                                                                |
| Cooldown clock icon              | 28                 | 1              | P0       | verb list rows (currently a Unicode glyph that renders as a box), boost countdowns                                               |
| Mailbox / reward-waiting icon    | 64                 | 1              | P1       | loot screen notice card                                                                                                          |
| Bag-full icon                    | 48                 | 1              | P1       | inventory count at 50/50, "bag full" toast                                                                                       |
| Level-up arrow                   | 48                 | 1              | P1       | Party header Level Up button, nav badge context                                                                                  |
| Watch-ad (play) icon             | 40                 | 1              | P1       | "2x (watch ad)" buttons on chest and offline sheet                                                                               |
| Boost icons                      | 96                 | **2**          | P1       | shop (Auto-fire Verbs, Verb Speed Up)                                                                                            |
| Lock icon                        | 40                 | 1              | P1       | LOCKED chip on the Locations card, locked verb slots                                                                             |
| Undo icon                        | 40                 | 1              | P1       | undo toast after selling an item or key                                                                                          |

---

## 4. Verb cards (text-first: no per-verb illustrations)

**Decision:** verbs are text. The verb name, set in the uniform display font (§7), IS the card art. No per-verb icon commissions; the `VerbDefinition.icon` hook stays unused. This keeps the ~30+ verb roster free to grow without art dependencies.

| Asset                 | Size               | Count | Priority | Notes |
| --------------------- | ------------------ | ----- | -------- | ----- |
| Verb card frame       | 340 x 180, 9-slice | 1 tintable master (or 6, one per stat color) | P0 | Quiet center field for the verb name (34 px); a 56 x 56 corner slot top-left for the stat icon + abbreviation chip (HCI E1) |
| Cooldown/draw overlay | 340 x 180          | 1     | P0       | Radial or left-to-right sweep showing the 10 s draw timer; alpha                                                                   |
| Lock overlay          | 340 x 180          | 1     | P0       | 3 s post-fire lock; dim frame + small clock (§3); alpha                                                                            |
| Verb list row         | 1032 x 120, 9-slice | 1    | P1       | Party > Verbs read-only list (stat stripe left, name, damage/cooldown right); can reuse the card 9-slice                           |

---

## 5. Keys & Destinations

Keys are dungeon-entry items with **up to 3 optional modifiers** (color family, slot, class archetype) plus difficulty 1-6. Currently drawn as a card with a 16px colored stripe and pips: no key image exists. Flat art per combination is combinatorially impossible (10 x 7 x 15 x 6), so keys are a **layered icon system**. Total deliverables: **1 base key + 1 gem shape + 7 slot glyphs + 15 archetype emblems = 24 pieces** (plus reuse of the pip asset). Priority **P0** for base + gem + 7 slot glyphs (they double as the empty-slot silhouettes), **P1** for the 15 emblems.

### 5.0 Key icon layer spec

**Authoring canvas: 256 x 256** (displayed at 104-200 px in key cards, detail/fusion sheets, dungeon HUD; 48 px in the top bar). Every layer must read at 104 px.

**Anatomy & layer placement.** A classic fantasy key, oriented diagonally (bow upper-left, bit lower-right), fills the canvas:

| Layer               | Piece                                              | Placement & size                                          | Behavior                                                                                                                           |
| ------------------- | -------------------------------------------------- | --------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------- |
| 1 (base)            | **Key body**: bow (ring head), shaft, bit (teeth)  | full canvas                                               | Painted in neutral steel with a **tintable flat/grayscale variant** for engine tinting. Always present.                            |
| 2                   | **Color-family gem** set in the bow                | ~72 px, centered in the bow                               | Tinted in-engine to one of 10 family colors (hex below). Absent when the key has no color modifier: the bow shows an empty socket. |
| 3                   | **Slot glyph** forged into the bit                 | ~80 px, replaces/overlays the teeth silhouette            | 7 variants. Absent = plain generic teeth.                                                                                          |
| 4                   | **Archetype emblem** medallion on the shaft collar | ~88 px badge at the bow-shaft junction                    | 15 variants. Absent = plain collar.                                                                                                |
| 5 (UI, not in icon) | **Difficulty pips**                                | existing pip asset, drawn by the UI below/beside the icon | 1-6 filled                                                                                                                         |

Because all three modifiers are optional and combine freely, each piece is delivered as a **separate transparent PNG on the shared 256 canvas**, aligned to the base key so the engine can stack them (base → gem → glyph → emblem).

**Color family tints (exact values from code).** Gem (and optionally a subtle key-body tint) uses:

| Family | Hex       | Family  | Hex       |
| ------ | --------- | ------- | --------- |
| Red    | `#D94545` | Blue    | `#4D85E6` |
| Orange | `#E68C33` | Purple  | `#A661D9` |
| Brown  | `#8C6133` | White   | `#EBEBEB` |
| Yellow | `#E0C740` | Black   | `#29292E` |
| Green  | `#4DBF59` | Neutral | `#8C8C8C` |

Notes: White and Black gems need built-in edge contrast (they sit on both light steel and the dark UI). By design, a Neutral-family key and a key with no color modifier share the same gray accent in the UI: the gem-vs-empty-socket distinction is what tells them apart.

**Slot glyphs (7):** small forged icons, single-weight silhouette style, readable at ~32 px final:

| Slot   | Glyph suggestion |
| ------ | ---------------- |
| Head   | helm             |
| Torso  | chestplate       |
| Arms   | gauntlet         |
| Legs   | greaves          |
| Feet   | boot             |
| Weapon | sword            |
| Misc   | ring/amulet      |

These 7 glyphs should be reusable standalone icons too: they double as the empty-equipment-slot silhouettes in §3 (drawn once, used both places; the party screen needs Head/Chest/Arms/Legs/Feet/Main Hand/Off Hand/Accessory labels, so Weapon and Misc cover MainHand/OffHand and Misc1-4 respectively).

**Archetype emblems (15):** each archetype is a two-stat build; the emblem should hint at both stats. Stat color pairs may edge the medallion (STR red, DEX green, CON brown, INT blue, WIS purple, CHA yellow). Suggested iconography (artist has latitude; keep to one visual family):

| Archetype  | Stats   | Emblem suggestion         |
| ---------- | ------- | ------------------------- |
| Warrior    | STR+DEX | crossed swords            |
| Juggernaut | STR+CON | anvil / tower shield      |
| Battlemage | STR+INT | sword wreathed in glyphs  |
| Warden     | STR+WIS | axe crossed with a sprig  |
| Warlord    | STR+CHA | crowned banner            |
| Skirmisher | DEX+CON | buckler and dagger        |
| Saboteur   | DEX+INT | gear and dagger / bomb    |
| Ranger     | DEX+WIS | bow and antler            |
| Trickster  | DEX+CHA | grinning mask             |
| Sentinel   | CON+INT | eye set in a shield       |
| Guardian   | CON+WIS | tree-emblazoned shield    |
| Champion   | CON+CHA | laurel-wreathed chalice   |
| Sage       | INT+WIS | open book / owl           |
| Occultist  | INT+CHA | candle sigil / arcane eye |
| Oracle     | WIS+CHA | third eye over a crescent |

**Readability rule:** a fully-modified key (gem + glyph + emblem) at 104 px must still be parseable at a glance; when in doubt, the gem color is the loudest signal, emblem second, slot glyph third (matching gameplay importance: color 40% / slot 30% / class 30% drop weighting).

### 5.1 Location capsule art (Sprint D2: art defines the pool) (P2)

The location card (name, tier, capsule, Enter button, 24h countdown) follows the quests-screen pattern: full width minus gutters = **1032 px wide** (key cards there are 160 tall, item cards 240).

**Format (decided, UI depth = hybrid): banner.** Capsule = **1032 x 480** (~2.15:1) card header; name/tier/Enter/countdown row below in the game font (~card total 700 tall). A tier frame (4 tiers: Common/Rare/Epic/Legendary, matching the rarity color language) wraps the capsule.

**Pool spec.** Locations are procedurally named from `location_parts.json` (adjective + noun + **place-type**: "Blackroot Castle", "Bogun's Bog", "the Sunken Antler Court"). That file does not exist yet, so **this art pool defines the place-type vocabulary** and the D2 name grammar will be authored against it: every place-type with a capsule is nameable, nothing else is. No blocker.

Starter pool: **10 place-type capsules**, painted biome-neutral (twilight/dramatic lighting so they sit on any zone) or with light per-biome palette tints if budget allows:

| #   | Place-type        | Capsule brief                               |
| --- | ----------------- | ------------------------------------------- |
| 1   | Castle / Keep     | walled silhouette on a rise, one lit window |
| 2   | Tower / Spire     | lone vertical landmark, storm-lit           |
| 3   | Temple / Ruins    | broken columns, overgrowth, shafts of light |
| 4   | Cavern / Grotto   | glowing mouth in rock, stalactite teeth     |
| 5   | Bog / Marsh       | dead trees in mist, will-o-wisps            |
| 6   | Grove / Court     | ring of ancient trees or standing stones    |
| 7   | Crypt / Vault     | sunken sealed door, heavy stonework         |
| 8   | Camp / Village    | tents or huts, fire smoke, palisade         |
| 9   | Bridge / Crossing | span over a gorge or river, toll gate       |
| 10  | Shrine / Altar    | small sacred site, offering glow            |

Growth path: Sprint D3 adds more place-types (each new capsule literally expands the name generator).

### 5.2 Location ending portraits (Sprint D2) (P2)

Every location ends in a dialogue scene: portrait + flavor text + 2-3 choice buttons, in a bottom sheet (portrait slot ~**400 x 400**, same pattern as the recruit sheet's 300 x 300 doll).

Split by ending archetype:

- **Character endings (gatekeeper, stranded traveler, hermit, rival, merchant, ...): no art needed.** The engine renders a generated paper-doll NPC: for `Recruit` outcomes this is the actual character who joins, which beats an illustration.
- **Object/scene endings need painted portraits.** Starter set of **6**, 400 x 400: sealed vault door, ancient altar, abandoned camp, mysterious chest, monolith/obelisk, collapsed passage. Same growth path as capsules: each new portrait is a new ending archetype for the generator.

Outcome types (Recruit, Loot, ShopBoost, QuestStart, BossFight, Gold) need no dedicated art: choices are text buttons and the result fanfare reuses the quest reward sheet.

### 5.3 D3 art checklist (from the Destinations phasing)

Sprint D3's "content + polish" art items and where this list covers them: archetype/color key art → §5.0; more location parts → grow §5.1/§5.2 pools; fanfare/juice → combat and completion VFX in §1.3. Nothing in D3 introduces a new asset category.

---

## 6. Shop & sheets

| Asset                                        | Size                | Count | Priority | Notes |
| -------------------------------------------- | ------------------- | ----- | -------- | ----- |
| Chest illustration                           | 300 x 300           | 2 states | P1    | Exploration Chest card: closed (counting down) and ready (glowing) |
| Offline earnings / welcome-back illustration | 400 x 240           | 1     | P1       | coin pile or campfire scene, sits above the "Away 3h 12m" line     |
| Claim-result fanfare                         | 1032 x 120, alpha   | 1     | P1       | header flourish on the reward sheets (quest, dungeon, chest claim) |
| Premium / remove-ads mark                    | 96                  | 1     | P1       |                                                                    |
| Tavern sign / header                         | 1032 x 160          | 1     | P2       | optional flavor header above the six recruit rows                  |

---

## 7. App-level

| Asset                 | Spec                                                                                                    | Priority | Notes |
| --------------------- | ------------------------------------------------------------------------------------------------------- | -------- | ----- |
| **Uniform game font** | TMP-compatible TTF/OTF, 2 weights (display/bold + body/regular)                                         | **P0**   | Everything is LiberationSans (engine default). Nearly all content is procedural text (character names, quest dialogue, stats, verb names, prices), so text sits ON TOP of art for most buttons, labels, and cards: the font must carry the art style itself. Requirements: legible at the 28px floor; clean numerals (counters, damage numbers, big-number suffixes K/M/B); full ASCII + punctuation including the middle dot (·), en/em dashes and curly quotes used in dialogue; SDF-friendly (works with outline/shadow presets so it reads over illustrated surfaces). Verb cards (§4) use the display weight as their art. |
| App icon              | Android adaptive (foreground + background layers, 432 x 432 safe zone) + iOS set; master at 1024 x 1024 | **P0**   | **Nothing assigned on any platform** |
| Splash logo           | 1080 x 1920 portrait, or logo on dark `#1F1F20`                                                        | **P0**   | Unity default splash currently shows |
| Store art             | Play Store feature graphic 1024 x 500, 6-8 screenshots 1080 x 1920                                     | P1       | for ship |

---

## 8. Tutorial and help (Sprint 12)

The first-time-user flow (tap a strong verb, equip loot, accept a quest) and a help sheet explaining the stat triangles are Sprint 12 scope. Their art is small and generic; commission it with the P0 chrome so the tutorial is not blocked.

| Asset                       | Size              | Count | Priority | Notes |
| --------------------------- | ----------------- | ----- | -------- | ----- |
| Pointer hand / tap cursor   | 96 x 96, 2 frames | 1     | P0       | "tap here" indicator on verb cards, item cards, the ACCEPT button |
| Callout bubble              | 9-slice, border 32, + 4 tail variants (up/down/left/right) | 1 | P0 | tutorial and tooltip text (34 px body); quiet center |
| Spotlight mask              | procedural        |       |          | keep in code (cut-out scrim) |
| Advantage triangles diagram | 900 x 520         | 1     | P0       | The two rock-paper-scissors triangles: Physical STR → DEX → CON → STR, Mental INT → WIS → CHA → INT. Uses the 6 stat icons (§3) with arrows meaning "beats". Shown in the help sheet and once in the tutorial. Leave the labels to the engine (text) or bake them in the game font |
| Help (?) icon               | 48                | 1     | P0       | verb bar corner button that opens the help sheet |

---

## 9. Screen pictures and screenshots

**Screen pictures** (`docs/art-screen-pictures/01-explore.png` … `11-quest-offer-panel.png`, overview `00-all-screens.png`, generated by `tools/art_screen_pictures.py` from the screenshots below): each is the real screenshot with numbered boxes at the true pixel positions and a legend naming the asset ID for each box. Box colors: green = background layer, orange = stretchable panel/card/bar/button, blue = icon or small picture, magenta = effect or animation, yellow = key artwork, dashed gray = drawn by the game, no art. `callouts.json` in the same folder maps asset ID to screen and box number; `tools/build_art_assets_xlsx.py` reads it to fill the Picture column of `docs/Starquill-Art-Assets.xlsx` and embeds the pictures in its Screen pictures tab. The earlier wireframe layout maps (`docs/art-layouts/`) were retired 2026-09-16 as too abstract for the artist.

**Artist-facing wording:** the workbook is written without engine or UI terms (no "9-slice", "chrome", "parallax", "tintable", "HUD", "P0"). This file keeps the internal terms; when they diverge, the workbook is what the artist sees.

**Action accent, for the artist:** one green (`UiTheme.AccentGreen`, `#2E8B57`) currently paints every primary button, the active-tab underline, the travel-bar fill and the active-quest banner edge, while the same hue family also means "upgrade" (delta chips), DEX (stat color) and Uncommon (rarity). The primary button / active tab / bar fill should use an action color that is none of green, red, blue, purple, brown or yellow. One token in code; the artist's choice applies everywhere.

### Screenshots of the current placeholder build

Captured 2026-09-16 in the Editor at 1080 x 2280 (a 19:9 phone) from a mid-game save: `docs/screenshots/01-explore-quest-offered.png` through `11-quest-offer-sheet.png`, contact sheet `docs/screenshots-contact-sheet.png`. They show where every asset above lands. Note for the artist: the verb bar's third card and the ladder's boss node are clipped in these shots because of a canvas-scaling bug (HCI review C5) that the code fix removes; design for the full 1080 width.

Still to capture after the Tier 0 code fixes: dungeon HUD during a run, fusion sheet, completion sheets, offline sheet, drawer open.

---

## 10. Decisions & remaining questions

**Resolved:**

- **Enemies:** paper-doll composition, two sets (player library reuse + B/W monster-piece library the artist has samples of). See §1.2.
- **Key color families:** 10. Slots 7, archetypes 15, difficulty 6 (all from code enums). Full layer spec in §5.0.
- **Biomes for this order:** forest, mountains, plains, desert (§1.1). Forest = Verdant Hollow, Mountains = Gloomspire Ruins; plains/desert zone data to be authored.
- **Capsule art unblocked (§5.1):** `location_parts.json` does not exist yet, so the capsule pool defines the place-type vocabulary and the D2 name grammar is authored against it. Starter pool of 10 place-types specced.
- **Ending portraits (§5.2):** character endings reuse paper-doll NPC renders (no art); 6 object/scene portraits specced at 400 x 400.
- **UI depth:** option C (hybrid) confirmed. Neutral chrome set + illustrated hero moments; capsule art uses the banner size (§5.1).
- **Verbs:** text-first, no per-verb illustrations (§4). Consequence: the **uniform game font is a first-order art deliverable** (§7).
- **Parallax sizes (this pass):** authored at 1x; foreground is 140 tall (was listed as 280, which the engine squashed 2:1); sky is 1490 tall (the full combat area).
- **Stat types get icons (this pass):** the HCI review's color-only finding (E1/E2/C1) makes the 6 stat icons P0 and requires each shape to be distinct without color.

**UI depth: options weighed**

|                        | A. Neutral 9-slice chrome                                                     | B. Fully illustrated per screen                                                           | C. Hybrid (chrome + illustrated hero moments)            |
| ---------------------- | ----------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------- | -------------------------------------------------------- |
| Asset count            | ~15 sprites cover all 5 screens                                               | Bespoke panels/headers/frames per screen: 5-10x the count                                 | Chrome set + ~10 hero pieces                             |
| Code impact            | Minimal: primitives are centralized in `UiFactory`                            | Per-screen layout rework; current builders assume uniform primitives                      | Same as A for chrome; hero art drops into existing slots |
| Layout flexibility     | High: text-driven layouts survive balance changes, localization, new features | Low: illustrated panels resist reflow (quest ladder length, roster growth, D2/D3 screens) | High where it matters                                    |
| Visual identity        | Generic risk: identity carried only by backgrounds/enemies/keys               | Strongest: premium feel, best store screenshots                                           | Strong: identity from content art, chrome stays quiet    |
| Timeline / rework risk | Fast; safe before Sprint 12 UI pass                                           | Slow; high rework risk since Sprint 12 may still reflow layouts                           | Fast chrome now, hero art parallel                       |

**Chosen: C (hybrid).** Commission the neutral chrome set (§2) plus illustrated hero pieces: parallax zones (§1.1), key art (§5.0), capsule banners (§5.1), chest/offline illustrations (§6), app icon/splash (§7), and the uniform game font (§7) as the text layer over all of it. Revisit per-screen illustration after Sprint 12 stabilizes layouts.

**Still open:**

1. Plains/desert zone names and stat pairings (art is unblocked: biome briefs in §1.1 stand regardless).
2. Monster-piece library scope: piece-type roster and count, pending review of the artist's sample set.
3. Capsule pool sign-off: the 10 place-types in §5.1 become the location name-generator vocabulary, so cutting/adding one changes game content, not just art.
4. Action accent color: the artist proposes a non-green accent (see Context); confirm before the button set is painted.
5. Screenshot pack (§9) to attach to the hand-off.

**P0 count:** 16 parallax strips (8 for the two live biomes) · nameplate + HP bar + advantage marker + boss aura · ~17 chrome pieces · 6 stat icons · coin, key, close, clock · 7 slot glyphs · 5 rarity frames · portrait ring · 5 nav icons x 2 + badge · verb frame + 2 overlays · key base + gem · font · app icon · splash · 4 tutorial pieces.
