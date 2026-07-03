# Starquill: Placeholder Art Inventory & Artist Request List

**Last updated:** 2026-07-03 (third pass: biomes fixed to forest/mountains/plains/desert; key-art layer spec; capsule pool and ending portraits specced from the Destinations design, D2/D3 no longer block art)
**Status:** Near hand-off ready. Remaining opens in §8.

## Context for the artist

- Portrait mobile game, reference resolution **1080x1920** (CanvasScaler match height: 1 reference px = 1 authored px at reference aspect). Recommend exporting at **2x** the listed sizes for high-DPI devices.
- The **only final art** in the game today is the paper-doll library: character species parts (27 species), equipment sprites, and weapon sprites. Characters, portraits, and item icons are composited live from these: **no new art needed there**.
- Everything else on screen is a flat colored rectangle or plain text. Every asset below is a real request.
- Current (placeholder) theme is dark: background `#1A1A26`, card `#232330`, sheet `#2C2C3A`. Accent green `#2E8B57`, gold `(1, 0.84, 0)`. The artist may redefine the palette; upgrade-green / downgrade-red semantics must stay distinct from decorative colors.
- UI rule already in code: no Unicode glyphs in text: every pip, arrow, coin, and icon is an Image, so all of these become sprite slots.
- Most UI chrome is built from a small set of shared primitives (card, button, chip, pip, progress bar, banner). **9-slice sprites drop into these with minimal code change**, so one good chrome set covers every screen.

Cross-cutting variant families (these key many assets below):

| Family | Count | Values |
|---|---|---|
| Rarity | 5 | Common (gray), Uncommon (green), Rare (blue), Epic (purple), Legendary (orange) |
| Stat type | 6 | STR (red), DEX (green), CON (brown), INT (blue), WIS (purple), CHA (yellow) |
| Verb category | 2 | Physical, Mental |
| Equipment slot | 11 | Head, Torso, Arms, Legs, Feet, MainHand, OffHand, Misc x4 |
| Key slot | 7 | Head, Torso, Arms, Legs, Feet, Weapon, Misc |
| Key color family | 10 | Red, Orange, Brown, Yellow, Green, Blue, Purple, Neutral, White, Black (confirmed 10) |
| Class archetype | 15 | Warrior, Juggernaut, Battlemage, Warden, Warlord, Skirmisher, Saboteur, Ranger, Trickster, Sentinel, Guardian, Champion, Sage, Occultist, Oracle |
| Quest tier | 4 | Normal, Elite, Hard, Boss |
| Status effect | 6 | Stagger, Bleed, Weaken, Expose, Reveal, Confuse |
| Biome | 4 | Forest (Verdant Hollow), Mountains (Gloomspire Ruins), Plains (new), Desert (new): keys backgrounds, monster palettes, capsule tinting |

---

## 1. Explore screen: world layer

### 1.1 Parallax backgrounds: 4 zones x 4 layers = 16 strips

Four horizontally-scrolling, seamlessly-tileable strips per biome. Currently flat colored rects with debug stripes. Scroll speeds: sky 5, mid 15, ground 30, foreground 50 (px/s), so left/right edges must tile perfectly.

Layer dimensions (identical for every zone):

| Layer | Size (tileable width) | Role |
|---|---|---|
| Sky / far backdrop | 2160 x 1400 | Sky, distant horizon, atmosphere |
| Midground | 2160 x 840 | Hills, treeline, structures: zone identity lives here |
| Ground band | 2160 x 420 | The strip the party walks on |
| Foreground overlay | 2160 x 280, **alpha** | Props drawn OVER characters, transparent above the ground line |

The four biomes are **forest, mountains, plains, desert** (confirmed). The two zones already in game data map onto the first two; plains and desert zones get names/stats when their data is authored.

| # | Biome | Zone in data | Dominant stats | Sky | Midground | Ground | Foreground |
|---|---|---|---|---|---|---|---|
| 1 | **Forest** | Verdant Hollow | STR, CON | Warm daylight through canopy gaps, drifting pollen/leaves | Dense treeline, claw-marked trunks, animal dens, a distant warden tower | Root-woven dirt trail, mossy stones, paw prints | Tall grass, ferns, the odd bone or snapped trap |
| 2 | **Mountains** | Gloomspire Ruins | INT, WIS | High cold sky, dusky violet haze, a looming ruined spire on the crags, faint floating glyphs | Stone peaks, broken colonnades and switchback paths carved into rock | Cracked flagstone ledge, scattered scrolls and scree | Rubble chunks, guttering ward-candles, drifting page fragments |
| 3 | **Plains** | (new zone, name/stats TBD: DEX lean proposed) | DEX (+?) | Wide open sky, towering clouds, birds on the wind | Rolling grass sea, lone windmill or standing stones, distant caravan line | Wagon-rutted trail through short grass | Swaying tall grass, wildflowers, darting critters |
| 4 | **Desert** | (new zone, name/stats TBD: CHA lean proposed) | CHA (+?) | Blazing gradient sky, heat shimmer, huge low sun or twin moons | Dune ridges, half-buried sandstone ruins, a far oasis with palms | Cracked hardpan and drifted sand | Foreground dune lips, dry scrub, sun-bleached bones |

Deliverable unit: one 4-layer set per biome. Order forest and mountains first (their zones are playable today). Biome is also the keying axis for monster palettes (§1.2) and location capsule tinting (§5.1).

### 1.2 Enemies: paper-doll composition, two piece sets

No fixed-dimension enemy sprites are needed. Enemies are assembled the same way player characters are:

1. **Humanoid enemies:** random constructions from the existing player paper-doll library (27 species + equipment). No new art. (Code note, not for the artist: `EnemyState` has no appearance field yet; wiring required.)
2. **Paper-doll monsters:** a modular monster-piece library in the same construction style: interchangeable pieces that combine into monsters. Pieces are **black and white** line art, colored in-engine (stat-type / zone tinting). The artist already has a small sample set: match its established dimensions and conventions; no new size spec here. **[EXPAND: piece-type roster and per-zone monster palettes once the sample set is reviewed]**

Boss / mini-boss treatment is still a gap for both sets: currently bosses render identically to normal enemies. Request a scale-up plus an **aura/crown overlay** that can sit on any composed enemy (display slot today is 240 x 360, up to 3 on screen; scaling is handled in-engine).

### 1.3 Combat feedback (net-new VFX, nothing exists)

| Asset | Size | Notes |
|---|---|---|
| Enemy HP bar (track + fill) | ~200 x 12, 9-slice | Fill tinted per stat type (6 colors) |
| Hit spark / impact | ~128 x 128 sheet | Per verb category or per stat family (6 flavors per design doc: STR dust/impacts, DEX silver streaks, CON stone/shields, INT blue glyphs, WIS green-gold glow, CHA pink swirls) |
| Death poof | ~256 x 256 sheet | Enemy defeat (currently a fade-out) |
| Wave-clear sweep/burst | screen-width | Currently nothing happens visually |
| Coin burst on gold gain | ~64 coin + particles | Pairs with the gold counter |
| Damage number styling | font/treatment | Currently plain text; white (auto), 32pt colored by advantage (green strong / red weak), gold for gold drops |

---

## 2. Global UI chrome (shared 9-slice set, used by all 5 screens)

| Asset | Size | Variants |
|---|---|---|
| Screen background | 1080 x 1920 | 1 (or light per-screen theming x5) |
| Top bar panel | 1080 x 140 | 1 |
| Bottom nav panel | 1080 x 160 | 1 |
| Bottom nav tab icons | ~96 x 96 | **5 icons** (Explore, Quests, Loot, Party, Shop) x active/inactive |
| Tab selection indicator | ~176 x 8 | replaces the flat underline |
| Card / panel | 9-slice, min 120 tall | 1 (used everywhere: item cards, shop cards, quest cards, tavern rows) |
| Bottom sheet panel | 1080 x 730-1344, rounded top | 1, + grabber pill 160 x 12, + back button 320 x 90 |
| Buttons | 9-slice, heights 130 / 120 | 3 kinds (primary green, secondary gray, destructive red) x normal/disabled |
| Progress bar (track + fill) | 9-slice, heights 8-28 | 1 set: reused for travel bar, quest progress, fragment bar, XP bars, lock timer |
| Chip | 120 x 72, 9-slice | 1 shape; tinted (delta up/down/neutral, tier colors, LOCKED) |
| Pip (filled + empty) | 20-28 px | 1 pair: ability levels + key difficulty |
| Banner accent frame | 1048 x ~100-120, 4px edge accents | 1: quest banner + dungeon HUD (accent recolored per state) |
| Section rule line | 3px, 9-slice | 1 |
| Ladder nodes (zone quest ladder) | 72 px node | ~5 states: done, current (+ring), ahead, boss (gold diamond), boss-current; plus small E/H tier markers |
| Scrim | flat | keep procedural |

---

## 3. Icon sets

| Asset | Size | Count | Where used |
|---|---|---|---|
| Gold coin | ~48-60 | 1 | top bar counter, floating gold numbers, sell prices, shop |
| Fragment shard | ~48 | 1 | top bar counter + discovery progress bar |
| Key (currency-level icon) | ~48 | 1 | key pouch count (full key art system is §5) |
| Stat icons | ~32-48 | **6** | item stat lines, verb cards, party stat column, tavern sheet |
| Status effect icons | ~32-48 | **6** | not surfaced in UI yet: Stagger, Bleed, Weaken, Expose, Reveal, Confuse |
| Rarity item-icon frames | 200 x 200, 9-slice | **5** | backplate behind every item icon (currently flat tint at 16% alpha) |
| Empty equipment-slot silhouettes | ~120 | **7** (reused) | party equipment tab; same 7 glyphs as the key slot glyphs (§5.0): Weapon covers Main/Off Hand, Misc covers the 4 accessory slots |
| Quest tier badges | ~64 chip | **4** | quest offer sheet, ladder (Normal / Elite / Hard / Boss) |
| Verb category badges | ~40 | **2** | Physical / Mental |
| Cooldown clock icon | ~28 | 1 | verb loadout rows (currently a Unicode glyph, violates glyph rule) |
| Mailbox / reward-waiting icon | ~64 | 1 | loot screen notice card |
| Boost icons | ~96 | **2** | shop (Auto-fire Verbs, Verb Speed Up) |
| Portrait frame / ring | 104-300 | 1-2 | tavern rows (104), recruit sheet (300), roster selection ring, party strip highlight |
| Party-member badge | ~40 | 1 | roster grid corner number |

---

## 4. Verb cards (text-first: no per-verb illustrations)

**Decision:** verbs are text. The verb name, set in the uniform display font (§7), IS the card art. No per-verb icon commissions; the `VerbDefinition.icon` hook stays unused. This keeps the ~30+ verb roster free to grow without art dependencies.

| Asset | Size | Count |
|---|---|---|
| Verb card frame | 340 x 180, 9-slice | 6 (one per stat color) or 1 tintable master; must leave a quiet center field for the verb name to read over |
| Cooldown/lock overlay | 340 x 180 | 1 (radial sweep or dim frame; currently a gray tint) |

---

## 5. Keys & Destinations

Keys are dungeon-entry items with **up to 3 optional modifiers** (color family, slot, class archetype) plus difficulty 1-6. Currently drawn as a card with a 16px colored stripe and pips: no key image exists. Flat art per combination is combinatorially impossible (10 x 7 x 15 x 6), so keys are a **layered icon system**. Total deliverables: **1 base key + 1 gem shape + 7 slot glyphs + 15 archetype emblems = 24 pieces** (plus reuse of the pip asset).

### 5.0 Key icon layer spec

**Authoring canvas: 256 x 256** (displayed at 104-200 px in key cards, detail/fusion sheets, dungeon HUD). Every layer must read at 104 px.

**Anatomy & layer placement.** A classic fantasy key, oriented diagonally (bow upper-left, bit lower-right), fills the canvas:

| Layer | Piece | Placement & size | Behavior |
|---|---|---|---|
| 1 (base) | **Key body**: bow (ring head), shaft, bit (teeth) | full canvas | Painted in neutral steel with a **tintable flat/grayscale variant** for engine tinting. Always present. |
| 2 | **Color-family gem** set in the bow | ~72 px, centered in the bow | Tinted in-engine to one of 10 family colors (hex below). Absent when the key has no color modifier: the bow shows an empty socket. |
| 3 | **Slot glyph** forged into the bit | ~80 px, replaces/overlays the teeth silhouette | 7 variants. Absent = plain generic teeth. |
| 4 | **Archetype emblem** medallion on the shaft collar | ~88 px badge at the bow-shaft junction | 15 variants. Absent = plain collar. |
| 5 (UI, not in icon) | **Difficulty pips** | existing pip asset, drawn by the UI below/beside the icon | 1-6 filled |

Because all three modifiers are optional and combine freely, each piece is delivered as a **separate transparent PNG on the shared 256 canvas**, aligned to the base key so the engine can stack them (base → gem → glyph → emblem).

**Color family tints (exact values from code).** Gem (and optionally a subtle key-body tint) uses:

| Family | Hex | Family | Hex |
|---|---|---|---|
| Red | `#D94545` | Blue | `#4D85E6` |
| Orange | `#E68C33` | Purple | `#A661D9` |
| Brown | `#8C6133` | White | `#EBEBEB` |
| Yellow | `#E0C740` | Black | `#29292E` |
| Green | `#4DBF59` | Neutral | `#8C8C8C` |

Notes: White and Black gems need built-in edge contrast (they sit on both light steel and the dark UI). By design, a Neutral-family key and a key with no color modifier share the same gray accent in the UI: the gem-vs-empty-socket distinction is what tells them apart.

**Slot glyphs (7):** small forged icons, single-weight silhouette style, readable at ~32 px final:

| Slot | Glyph suggestion |
|---|---|
| Head | helm |
| Torso | chestplate |
| Arms | gauntlet |
| Legs | greaves |
| Feet | boot |
| Weapon | sword |
| Misc | ring/amulet |

These 7 glyphs should be reusable standalone icons too: they double as the empty-equipment-slot silhouettes in §3 (drawn once, used both places; the party screen needs Head/Chest/Arms/Legs/Feet/Main Hand/Off Hand/Accessory labels, so Weapon and Misc cover MainHand/OffHand and Misc1-4 respectively).

**Archetype emblems (15):** each archetype is a two-stat build; the emblem should hint at both stats. Stat color pairs may edge the medallion (STR red, DEX green, CON brown, INT blue, WIS purple, CHA yellow). Suggested iconography (artist has latitude; keep to one visual family):

| Archetype | Stats | Emblem suggestion |
|---|---|---|
| Warrior | STR+DEX | crossed swords |
| Juggernaut | STR+CON | anvil / tower shield |
| Battlemage | STR+INT | sword wreathed in glyphs |
| Warden | STR+WIS | axe crossed with a sprig |
| Warlord | STR+CHA | crowned banner |
| Skirmisher | DEX+CON | buckler and dagger |
| Saboteur | DEX+INT | gear and dagger / bomb |
| Ranger | DEX+WIS | bow and antler |
| Trickster | DEX+CHA | grinning mask |
| Sentinel | CON+INT | eye set in a shield |
| Guardian | CON+WIS | tree-emblazoned shield |
| Champion | CON+CHA | laurel-wreathed chalice |
| Sage | INT+WIS | open book / owl |
| Occultist | INT+CHA | candle sigil / arcane eye |
| Oracle | WIS+CHA | third eye over a crescent |

**Readability rule:** a fully-modified key (gem + glyph + emblem) at 104 px must still be parseable at a glance; when in doubt, the gem color is the loudest signal, emblem second, slot glyph third (matching gameplay importance: color 40% / slot 30% / class 30% drop weighting).

### 5.1 Location capsule art (Sprint D2: art defines the pool)

The location card (name, tier, capsule, Enter button, 24h countdown) follows the quests-screen pattern: full width minus gutters = **~1032 px wide** (key cards there are 160 tall, item cards 240).

**Format (decided, UI depth = hybrid): banner.** Capsule = **1032 x 480** (~2.15:1) card header; name/tier/Enter/countdown row below in the game font (~card total 700 tall). A tier frame (4 tiers: Common/Rare/Epic/Legendary, matching the rarity color language) wraps the capsule.

**Pool spec.** Locations are procedurally named from `location_parts.json` (adjective + noun + **place-type**: "Blackroot Castle", "Bogun's Bog", "the Sunken Antler Court"). That file does not exist yet, so **this art pool defines the place-type vocabulary** and the D2 name grammar will be authored against it: every place-type with a capsule is nameable, nothing else is. No blocker.

Starter pool: **10 place-type capsules**, painted biome-neutral (twilight/dramatic lighting so they sit on any zone) or with light per-biome palette tints if budget allows:

| # | Place-type | Capsule brief |
|---|---|---|
| 1 | Castle / Keep | walled silhouette on a rise, one lit window |
| 2 | Tower / Spire | lone vertical landmark, storm-lit |
| 3 | Temple / Ruins | broken columns, overgrowth, shafts of light |
| 4 | Cavern / Grotto | glowing mouth in rock, stalactite teeth |
| 5 | Bog / Marsh | dead trees in mist, will-o-wisps |
| 6 | Grove / Court | ring of ancient trees or standing stones |
| 7 | Crypt / Vault | sunken sealed door, heavy stonework |
| 8 | Camp / Village | tents or huts, fire smoke, palisade |
| 9 | Bridge / Crossing | span over a gorge or river, toll gate |
| 10 | Shrine / Altar | small sacred site, offering glow |

Growth path: Sprint D3 adds more place-types (each new capsule literally expands the name generator).

### 5.2 Location ending portraits (Sprint D2)

Every location ends in a dialogue scene: portrait + flavor text + 2-3 choice buttons, in a bottom sheet (portrait slot ~**400 x 400**, same pattern as the recruit sheet's 300 x 300 doll).

Split by ending archetype:
- **Character endings (gatekeeper, stranded traveler, hermit, rival, merchant, ...): no art needed.** The engine renders a generated paper-doll NPC: for `Recruit` outcomes this is the actual character who joins, which beats an illustration.
- **Object/scene endings need painted portraits.** Starter set of **6**, 400 x 400: sealed vault door, ancient altar, abandoned camp, mysterious chest, monolith/obelisk, collapsed passage. Same growth path as capsules: each new portrait is a new ending archetype for the generator.

Outcome types (Recruit, Loot, ShopBoost, QuestStart, BossFight, Gold) need no dedicated art: choices are text buttons and the result fanfare reuses the quest reward sheet.

### 5.3 D3 art checklist (from the Destinations phasing)

Sprint D3's "content + polish" art items and where this list covers them: archetype/color key art → §5.0; more location parts → grow §5.1/§5.2 pools; fanfare/juice → combat and completion VFX in §1.3. Nothing in D3 introduces a new asset category.

---

## 6. Shop & sheets

| Asset | Size | Count |
|---|---|---|
| Chest illustration | ~200-300 | 1 (Exploration Chest card) |
| Offline earnings / welcome-back illustration | ~300-400 wide | 1 (coin pile or campfire scene) |
| Premium / remove-ads mark | ~96 | 1 |
| Loot toast background | ~1000 x 72, 9-slice | 1 (toasts are currently bare text; item thumbnails reuse existing sprites) |

---

## 7. App-level

| Asset | Spec | Notes |
|---|---|---|
| App icon | Android adaptive (foreground + background layers, 432 x 432 safe zone) + iOS set; master at 1024 x 1024 | **Nothing assigned on any platform** |
| Splash logo | portrait, on dark `#1F1F20` | Unity default splash currently shows |
| **Uniform game font** | TMP-compatible TTF/OTF, 2 weights (display/bold + body/regular) | **High priority.** Everything is LiberationSans (engine default). Nearly all content is procedural text (character names, quest dialogue, stats, verb names, prices), so text sits ON TOP of art for most buttons, labels, and cards: the font must carry the art style itself. Requirements: legible at the 28px floor; clean numerals (counters, damage numbers, big-number suffixes K/M/B); full ASCII + punctuation; SDF-friendly (works with outline/shadow presets so it reads over illustrated surfaces). Verb cards (§4) use the display weight as their art. |
| Store art | Play Store feature graphic 1024 x 500, screenshots | for ship |

---

## 8. Decisions & remaining questions

**Resolved:**
- **Enemies:** paper-doll composition, two sets (player library reuse + B/W monster-piece library the artist has samples of). See §1.2.
- **Key color families:** 10. Slots 7, archetypes 15, difficulty 6 (all from code enums). Full layer spec in §5.0.
- **Biomes for this order:** forest, mountains, plains, desert (§1.1). Forest = Verdant Hollow, Mountains = Gloomspire Ruins; plains/desert zone data to be authored.
- **Capsule art unblocked (§5.1):** `location_parts.json` does not exist yet, so the capsule pool defines the place-type vocabulary and the D2 name grammar is authored against it. Starter pool of 10 place-types specced.
- **Ending portraits (§5.2):** character endings reuse paper-doll NPC renders (no art); 6 object/scene portraits specced at 400 x 400.
- **UI depth:** option C (hybrid) confirmed. Neutral chrome set + illustrated hero moments; capsule art uses the banner size (§5.1).
- **Verbs:** text-first, no per-verb illustrations (§4). Consequence: the **uniform game font is a first-order art deliverable** (§7): procedural names/quests/dialogue/stats mean text sits on top of art for most buttons and labels, so the font carries the style.

**UI depth: options weighed**

| | A. Neutral 9-slice chrome | B. Fully illustrated per screen | C. Hybrid (chrome + illustrated hero moments) |
|---|---|---|---|
| Asset count | ~15 sprites cover all 5 screens | Bespoke panels/headers/frames per screen: 5-10x the count | Chrome set + ~10 hero pieces |
| Code impact | Minimal: primitives are centralized in `UiFactory` | Per-screen layout rework; current builders assume uniform primitives | Same as A for chrome; hero art drops into existing slots |
| Layout flexibility | High: text-driven layouts survive balance changes, localization, new features | Low: illustrated panels resist reflow (quest ladder length, roster growth, D2/D3 screens) | High where it matters |
| Visual identity | Generic risk: identity carried only by backgrounds/enemies/keys | Strongest: premium feel, best store screenshots | Strong: identity from content art, chrome stays quiet |
| Timeline / rework risk | Fast; safe before Sprint 12 UI pass | Slow; high rework risk since Sprint 12 may still reflow layouts | Fast chrome now, hero art parallel |

**Chosen: C (hybrid).** Commission the neutral chrome set (§2) plus illustrated hero pieces: parallax zones (§1.1), key art (§5.0), capsule banners (§5.1), chest/offline illustrations (§6), app icon/splash (§7), and the uniform game font (§7) as the text layer over all of it. Revisit per-screen illustration after Sprint 12 stabilizes layouts.

**Still open:**
1. Plains/desert zone names and stat pairings (art is unblocked: biome briefs in §1.1 stand regardless).
2. Monster-piece library scope: piece-type roster and count, pending review of the artist's sample set.
3. Capsule pool sign-off: the 10 place-types in §5.1 become the location name-generator vocabulary, so cutting/adding one changes game content, not just art.
