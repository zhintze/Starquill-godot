"""Builds docs/Starquill-Art-Assets.xlsx, the artist hand-off workbook.

Plain-language version: no engine or UI jargon. Run
tools/art_screen_pictures.py first; this script reads its callouts.json to
fill the Picture column and embeds the screen pictures in their own tab.

Usage: python tools/build_art_assets_xlsx.py docs/art-screen-pictures docs/Starquill-Art-Assets.xlsx
"""
import json, os, sys
from openpyxl import Workbook
from openpyxl.drawing.image import Image as XLImage
from openpyxl.styles import Alignment, Border, Font, PatternFill, Side
from openpyxl.utils import get_column_letter
from openpyxl.worksheet.table import Table, TableStyleInfo

PICS, OUT = sys.argv[1], sys.argv[2]
callouts = json.load(open(os.path.join(PICS, "callouts.json")))

FONT = "Arial"
HEAD_FILL = PatternFill("solid", fgColor="1F1F2E")
HEAD_FONT = Font(name=FONT, bold=True, color="FFFFFF", size=11)
BODY = Font(name=FONT, size=10)
BOLD = Font(name=FONT, size=10, bold=True)
TITLE = Font(name=FONT, size=16, bold=True)
SUB = Font(name=FONT, size=11, bold=True)
WRAP = Alignment(wrap_text=True, vertical="top")
THIN = Side(style="thin", color="C8C8C8")
BORDER = Border(left=THIN, right=THIN, top=THIN, bottom=THIN)
GROUP_FILL = {
    "Backgrounds": "E8F5E9", "Enemies": "FBE9E7", "Fight screen": "FBE9E7", "Effects": "FCE4EC",
    "Panels, bars and buttons": "FFF3E0", "Icons": "E3F2FD", "Ability cards": "FFF3E0",
    "Key icon": "FFFDE7", "Later: locations": "F3E5F5", "Shop": "E0F7FA", "App and store": "EEEEEE",
    "Tutorial": "EEEEEE",
}
ORDER = {"1": "1 - First", "2": "2 - Next", "3": "3 - Later"}

# Plain words used in the Kind column
STRETCH = "Stretchable frame"
ICON = "Icon"
LAYER = "Scrolling background layer"
ANIM = "Animation frames"
OVER = "Transparent overlay"
PIC = "Illustration"

# Who adds the color
YOU = "You paint it in color"
GAME = "The game colors it: draw in white and gray only"
FILL = "The game colors the fill only"
EDGES = "The game colors the edges only"
BOTH = "Both: one painted version and one white/gray version"

# (ID, Group, What it is, How many, Size in px, Kind, Deliver at, Color, Versions, Where it appears, Order, Drawing notes)
ROWS = []
def R(*a): ROWS.append(a)

def bg(prefix, zone, where, order, sky, mid, ground, front):
    common_sky = ("Fills the whole fight area. The bottom 70 px are hidden behind the ability cards and the top 56 px "
                  "behind the travel bar, so keep the horizon in the middle third. Left and right edges must join "
                  "seamlessly when the picture repeats. Scrolls slowly (5 px per second).")
    R(f"{prefix}a", "Backgrounds", f"{zone}: sky layer (farthest back)", 1, "2160 x 1490, repeats sideways", LAYER, "Listed size", YOU, "-", where, order, f"{sky} {common_sky}")
    R(f"{prefix}b", "Backgrounds", f"{zone}: middle layer", 1, "2160 x 840, repeats sideways", LAYER, "Listed size", YOU, "-", where, order, f"{mid} This layer gives the zone its look. Scrolls at 15 px per second.")
    R(f"{prefix}c", "Backgrounds", f"{zone}: ground layer", 1, "2160 x 420, repeats sideways", LAYER, "Listed size", YOU, "-", where, order, f"{ground} The strip the party walks on; their feet land about 130 to 220 px above its bottom edge. Scrolls at 30 px per second.")
    R(f"{prefix}d", "Backgrounds", f"{zone}: front layer", 1, "2160 x 140, repeats sideways, transparent background", LAYER, "Listed size", YOU, "-", where, order, f"{front} Drawn in front of the characters' feet; fully transparent above the ground line. Scrolls at 50 px per second.")

bg("BG-1", "Forest", "Explore screen, fight area (zone: Verdant Hollow)", "1",
   "Warm daylight through gaps in the tree canopy, drifting pollen and leaves.",
   "Dense tree line, claw-marked trunks, animal dens, a distant watchtower.",
   "Root-woven dirt trail, mossy stones, paw prints.",
   "Tall grass, ferns, the odd bone or snapped trap.")
bg("BG-2", "Mountains", "Explore screen, fight area (zone: Gloomspire Ruins)", "1",
   "High cold sky, dusky violet haze, a looming ruined tower on the crags, faint floating magic symbols.",
   "Stone peaks, broken columns and zigzag paths cut into the rock.",
   "Cracked flagstone ledge, scattered scrolls and loose stones.",
   "Rubble chunks, guttering candles, drifting page fragments.")
bg("BG-3", "Plains", "Explore screen, fight area (a zone we will add later)", "2",
   "Wide open sky, towering clouds, birds on the wind.",
   "Rolling grass sea, a lone windmill or standing stones, a distant caravan line.",
   "Wagon-rutted trail through short grass.",
   "Swaying tall grass, wildflowers, darting critters.")
bg("BG-4", "Desert", "Explore screen, fight area (a zone we will add later)", "2",
   "Blazing gradient sky, heat shimmer, a huge low sun or twin moons.",
   "Dune ridges, half-buried sandstone ruins, a far oasis with palms.",
   "Cracked hard ground and drifted sand.",
   "Dune lips up close, dry scrub, sun-bleached bones.")

R("EN-1", "Enemies", "Boss glow (drawn behind a boss enemy)", 1, "400 x 520, transparent background", OVER, "Double size", GAME, "1", "Explore screen, behind any enemy that is a boss (enemies are 240 x 360; bosses are shown 30% bigger)", "1",
  "A glow, crown or aura shape. Draw it in white and gray: the game colors it to match the enemy.")
R("EN-2", "Enemies", "Monster body parts (build-a-monster set)", "To be agreed", "200 x 200 per part", "Character parts", "Listed size", GAME, "One file per part", "Explore screen enemies. The game assembles monsters from parts the same way it assembles the player characters.", "2",
  "Black-and-white line art on the same 200 x 200 part canvas as the character parts you already made. Follow the same conventions. The list of parts is still to be agreed.")
R("EN-3", "Fight screen", "Enemy name plate", 1, "240 x 64", STRETCH, "Double size", YOU, "1", "Above each enemy. Name on the left (28 px text), stat icon on the right (32 px), health bar along the bottom edge.", "1",
  "Keep the middle plain for the name. Tell us the corner size.")
R("EN-4", "Fight screen", "Enemy health bar (empty bar + fill)", 1, "200 x 12", STRETCH, "Double size", FILL, "1 set", "Inside the enemy name plate", "1",
  "The fill is colored by the game (6 stat colors). Can be the same drawing as the general progress bar (CH-13).")
R("EN-5", "Fight screen", "'Strong against' marker", 1, "40 x 40", ICON, "Double size", GAME, "1", "Top-right corner of an ability card when that ability is strong against the enemy on screen", "1",
  "A small up-arrow or burst. Must still be readable at 20 px.")

R("VX-1", "Effects", "Hit spark", 6, "128 x 128 per frame, 6 to 8 frames side by side", ANIM, "Double size", YOU, "One per stat (6 files)", "Explore screen, on the enemy that was hit", "2",
  "Strength: dust and impact. Dexterity: silver streaks. Constitution: stone and shields. Intelligence: blue magic symbols. Wisdom: green-gold glow. Charisma: pink swirls.")
R("VX-2", "Effects", "Enemy defeat puff", 1, "256 x 256 per frame, 8 frames side by side", ANIM, "Double size", YOU, "1", "Explore screen, when an enemy is beaten", "2", "Replaces the plain fade-out we have today.")
R("VX-3", "Effects", "Wave-cleared sweep", 1, "1080 x 300, transparent background", OVER, "Listed size", YOU, "1", "Explore screen, sweeps sideways across the fight area when every enemy in a wave is beaten", "2", "")
R("VX-4", "Effects", "Coin burst", 1, "64 x 64 coin plus an 8-frame particle animation", ANIM, "Double size", YOU, "1", "Explore screen, next to the gold counter when gold is earned", "2", "")
R("VX-5", "Effects", "Floating damage number style", 1, "A written description, not an image", "Text style", "-", "-",
  "Normal hits: 28 px white. Ability hits: 32 px, green when strong, red when weak. Gold: gold color.",
  "Explore screen, numbers that float up over enemies", "2",
  "The numbers are typed by the game in the game font. Once the font exists, tell us the outline and shadow you want on them.")

R("CH-1", "Panels, bars and buttons", "Screen background", 1, "1080 x 1920", "Full-screen picture", "Listed size", YOU, "1 (optional: a lightly different one per screen, 5 total)", "Under the Quests, Loot, Party and Shop screens", "1",
  "Low contrast, no focal point. Cards and text sit on top of it.")
R("CH-2", "Panels, bars and buttons", "Top bar background", 1, "1080 x 140, stretches sideways", STRETCH, "Double size", YOU, "1", "Top edge of every screen", "1", "")
R("CH-3", "Panels, bars and buttons", "Bottom tab bar background", 1, "1080 x 160, stretches sideways", STRETCH, "Double size", YOU, "1", "Bottom edge of every screen", "1", "")
R("CH-4", "Icons", "Bottom tab icons", 5, "96 x 96", ICON, "Double size", YOU, "Explore, Quests, Loot, Party, Shop; each in a selected and an unselected version (10 files)", "Bottom tab bar. Each tab is 208 x 140 with the icon centered above a 28 px label.", "1",
  "One line weight, no drop shadow. Must be recognizable at 48 px.")
R("CH-5", "Icons", "Alert dot", 1, "28 x 28", ICON, "Double size", GAME, "1", "Top-right corner of a tab icon when something new is waiting (new loot, quest offer, level-up ready, chest ready)", "1", "")
R("CH-6", "Panels, bars and buttons", "Selected-tab underline", 1, "176 x 8", "Small picture", "Double size", GAME, "1", "Under the selected tab in the bottom tab bar", "1",
  "If you prefer, build the selected look into the tab icon itself and skip this piece.")
R("CH-7", "Panels, bars and buttons", "Card (the box behind list items)", 1, "Any size; corners up to 24 px; at least 120 px tall", STRETCH, "Double size", YOU, "1", "Item cards (1032 x 240), quest and shop cards, tavern rows (1032 x 120), key cards (1032 x 160)", "1",
  "Plain, flat middle: text and icons sit on it everywhere.")
R("CH-8", "Panels, bars and buttons", "Card, selected or highlighted version", 1, "Same corners as CH-7", STRETCH, "Double size", EDGES, "1", "Selected equipment slot, selected roster character, the 'rewards waiting' notice", "1",
  "A glowing edge. The game colors the glow (gold for notices).")
R("CH-9", "Panels, bars and buttons", "Pop-up panel (slides up from the bottom)", 1, "1080 wide, 730 to 1632 tall, rounded top corners", STRETCH, "Double size", YOU, "1", "Every detail and confirmation panel: item, key, quest offer, recruit, welcome-back, results", "1", "")
R("CH-10", "Panels, bars and buttons", "Drag handle bar", 1, "160 x 12", "Small picture", "Double size", YOU, "1", "Top center of every pop-up panel", "1", "")
R("CH-11", "Panels, bars and buttons", "Buttons", 3, "Corners up to 32 px; main button 130 tall, others 120 tall; at least 220 wide", STRETCH, "Double size", YOU,
  "Main action (accent color), secondary (gray), delete or sell (red); each in a normal and a greyed-out version (6 files)", "Every screen and pop-up panel", "1",
  "The label is 34 px bold text, centered. The main button uses the accent color (see the Colors tab).")
R("CH-12", "Panels, bars and buttons", "Tab buttons (a row of choices, one selected)", 1, "120 tall, stretches sideways", STRETCH, "Double size", YOU, "Selected and unselected (2 files)", "Loot sorting (Score / Newest / Rarity), Party tabs (Roster / Equipment / Actions), choice rows inside pop-up panels", "1", "")
R("CH-13", "Panels, bars and buttons", "Progress bar (empty bar + fill)", 1, "8 to 28 tall, stretches sideways", STRETCH, "Double size", FILL, "1 set", "Travel bar on the Explore screen (14 tall), quest progress (24), fragment bar (28), experience bars (8), timers", "1", "")
R("CH-14", "Panels, bars and buttons", "Small label tag", 1, "120 x 72 (also stretched to 220 to 300 wide)", STRETCH, "Double size", GAME, "1 drawing; the game colors it green (better), red (worse) or gray (neutral)", "Item cards (the +12 tag), key panel (class name), locked cards (LOCKED), quest offer (difficulty)", "1", "")
R("CH-15", "Icons", "Level dot (filled and empty)", 1, "28 x 28", ICON, "Double size", GAME, "Filled, empty (2 files)", "Ability level (shown at 20 to 24 px) and key difficulty (24 to 32 px)", "1", "")
R("CH-16", "Panels, bars and buttons", "Notice strip", 1, "1048 x 100 (quest notice) and 1048 x 120 (dungeon status); thin 4 px colored edges top and bottom", STRETCH, "Double size", EDGES, "1", "Explore screen, under the travel bar", "1",
  "Text on the left, a small button on the right. The game colors the edges: gold for an offer, red for retreat, green while active.")
R("CH-17", "Panels, bars and buttons", "Pop-up message background", 1, "1032 x 72", STRETCH, "Double size", YOU, "1", "Explore screen: short messages (loot found, key found, undo) just above the ability cards", "1", "")
R("CH-18", "Panels, bars and buttons", "Divider line", 1, "3 px tall, stretches sideways", STRETCH, "Double size", YOU, "1", "Under the QUEST, DESTINATIONS, KEYS, BOOSTS and TAVERN headings", "2", "")
R("CH-19", "Icons", "Stage marker (zone progress row)", 1, "72 x 72", ICON, "Double size", "Partly: the game colors the 'current' ring", "Done, current (with a ring), not yet, boss (gold diamond), boss current (5 files)", "Quests screen: a row of 11 markers showing progress through the zone", "2", "")
R("CH-20", "Panels, bars and buttons", "Ability list row", 1, "1032 x 120", STRETCH, "Double size", YOU, "1", "Party > Actions tab: one row per ability (colored stripe on the left, name, damage and recharge time on the right)", "2",
  "Can be the same drawing as the card (CH-7).")

R("IC-1", "Icons", "Stat icons", 6, "48 x 48 (plus a 32 x 32 version)", ICON, "Double size", "Your choice: painted, or white/gray for the game to color", "Strength (STR), Dexterity (DEX), Constitution (CON), Intelligence (INT), Wisdom (WIS), Charisma (CHA)",
  "Ability cards, enemy name plates, item stat lines, Party stat column, recruit panel, help diagram", "1",
  "Each shape must be tellable apart without color (for color-blind players). Must read clearly at 28 px next to text.")
R("IC-2", "Icons", "Gold coin", 1, "48 to 60", ICON, "Double size", YOU, "1", "Top bar gold counter, floating gold numbers, sell prices, Shop", "1", "")
R("IC-3", "Icons", "Key (small icon)", 1, "48 x 48", ICON, "Double size", YOU, "1", "Top bar key count, key-found message, KEYS heading on the Quests screen", "1", "A simplified version of the big key drawing (KY-1).")
R("IC-4", "Icons", "Close (X)", 1, "48 px X inside a 120 x 120 button", ICON, "Double size", YOU, "1", "Top-right of every pop-up panel (replaces today's Back button)", "1", "")
R("IC-5", "Icons", "Empty equipment slot pictures", 7, "120 x 120", ICON, "Double size", GAME, "Head, Torso, Arms, Legs, Feet, Weapon, Misc", "Party > Equipment tab, in slots with nothing equipped. Same drawings as the key equipment symbols (KY-3).", "1",
  "Draw once at 256 px for the key and reuse here.")
R("IC-6", "Icons", "Rarity frames (behind item pictures)", 5, "200 x 200", STRETCH, "Double size", YOU, "Common (gray), Uncommon (green), Rare (blue), Epic (purple), Legendary (orange)", "Behind every item picture on item cards and reward panels. The item picture itself is built by the game.", "1",
  "Rarity should show in the decoration as well as the color.")
R("IC-7", "Icons", "Portrait frame (ring around a character's head)", 1, "104, 150 and 300 px (or one stretchable version)", STRETCH, "Double size", GAME, "Normal, selected", "Tavern rows (104), roster cards (150), recruit panel (300), Party portrait strip, item card target portrait (56)", "1", "")
R("IC-8", "Icons", "Recharge clock", 1, "28 x 28", ICON, "Double size", YOU, "1", "Ability list rows, boost countdowns, locked ability card", "1", "")
R("IC-9", "Icons", "Party-member number badge", 1, "40 x 40", ICON, "Double size", YOU, "1 drawing; the game writes the number 1 to 4 on it", "Corner of a roster card", "2", "")
R("IC-10", "Icons", "Quest difficulty badges", 4, "64 x 64 badge and 40 x 40 small marker", ICON, "Double size", YOU, "Normal, Elite, Hard, Boss", "Quest offer panel, quest card, small markers under the stage row", "2", "Different shapes, not just different colors.")
R("IC-11", "Icons", "Zone emblems", 4, "200 x 200", ICON, "Double size", YOU, "Forest, Mountains, Plains, Desert", "Quests screen zone heading, quest offer panel", "2", "")
R("IC-12", "Icons", "Ability type badges", 2, "40 x 40", ICON, "Double size", YOU, "Physical, Mental", "Ability cards and the ability list", "2", "")
R("IC-13", "Icons", "Rewards-waiting (mailbox) icon", 1, "64 x 64", ICON, "Double size", YOU, "1", "Loot screen, on the 'rewards waiting' notice", "2", "")
R("IC-14", "Icons", "Bag full", 1, "48 x 48", ICON, "Double size", YOU, "1", "Next to the inventory counter when it reads 50/50, and on the bag-full message", "2", "")
R("IC-15", "Icons", "Level-up arrow", 1, "48 x 48", ICON, "Double size", YOU, "1", "Party screen Level Up button", "2", "")
R("IC-16", "Icons", "Watch-an-ad (play) icon", 1, "40 x 40", ICON, "Double size", YOU, "1", "The '2x' buttons on the chest card and the welcome-back panel", "2", "")
R("IC-17", "Icons", "Boost icons", 2, "96 x 96", ICON, "Double size", YOU, "Auto-Fire, Speed-Up", "Shop boost cards", "2", "")
R("IC-18", "Icons", "Lock", 1, "40 x 40", ICON, "Double size", YOU, "1", "LOCKED tag, locked ability slots", "2", "")
R("IC-19", "Icons", "Undo", 1, "40 x 40", ICON, "Double size", YOU, "1", "Undo message after selling an item", "2", "")
R("IC-20", "Icons", "Fragment shard", 1, "48 x 48", ICON, "Double size", YOU, "1", "Quests screen, next to the Fragments counter", "3", "")
R("IC-21", "Icons", "Status effect icons", 6, "32 to 48", ICON, "Double size", YOU, "Stagger, Bleed, Weaken, Expose, Reveal, Confuse", "Not shown anywhere yet", "3", "")

R("VB-1", "Ability cards", "Ability card frame", 1, "340 x 180", STRETCH, "Double size", GAME, "1 white/gray drawing the game colors (or 6 painted ones, one per stat color)", "Explore screen, three cards side by side at the bottom", "1",
  "Plain middle for the ability name (34 px text). Leave a 56 x 56 space in the top-left corner for the stat icon and its 3-letter tag. Abilities are shown as text only: no picture per ability.")
R("VB-2", "Ability cards", "Recharge overlay", 1, "340 x 180, transparent background", OVER, "Double size", YOU, "1", "Drawn over an ability card during the 10-second wait for a new card", "1", "A radial wipe or a left-to-right sweep.")
R("VB-3", "Ability cards", "Locked overlay", 1, "340 x 180, transparent background", OVER, "Double size", YOU, "1", "Drawn over an ability card for 3 seconds after it is used", "1", "A dimmed frame plus the small clock (IC-8).")

R("KY-1", "Key icon", "Key body (base drawing)", 1, "256 x 256 canvas", "Layered icon", "Double size", BOTH, "Painted steel, plus a white/gray version (2 files)", "Key cards (shown at 104 px), key panel (200 px), dungeon status strip, top bar (48 px)", "1",
  "A classic fantasy key drawn diagonally: the handle loop at the upper-left, the teeth at the lower-right, filling the canvas. Always shown. Every other key piece lines up on top of this one.")
R("KY-2", "Key icon", "Gem set in the key's handle loop", 1, "About 72 px, on the same 256 canvas", "Layered icon", "Double size", GAME, "1 drawing; the game colors it in 10 key colors (see Colors). No gem = an empty socket.", "Layered on top of KY-1", "1",
  "White and gray on a transparent background. The white and black tints need built-in light and dark edges so they stay visible. Separate PNG on the shared 256 canvas.")
R("KY-3", "Key icon", "Equipment symbols on the key's teeth", 7, "About 80 px, on the same 256 canvas", "Layered icon", "Double size", YOU, "Head (helmet), Torso (chest plate), Arms (gauntlet), Legs (leg armor), Feet (boot), Weapon (sword), Misc (ring or amulet)", "Layered on top of KY-1; also used on their own as IC-5", "1",
  "Simple one-weight silhouettes readable at 32 px. Separate PNGs on the shared 256 canvas.")
R("KY-4", "Key icon", "Class medallions on the key's collar", 15, "About 88 px, on the same 256 canvas", "Layered icon", "Double size", YOU, "See the Key icon layers tab for the 15 classes", "Layered on top of KY-1", "2",
  "One matching family. The class's two stat colors may edge the medallion. Separate PNGs. A key with all three add-ons must still read at 104 px: gem loudest, medallion second, equipment symbol third.")
R("KY-5", "Later: locations", "Location pictures", 10, "1032 x 480", PIC, "Listed size", YOU, "Castle or keep, tower or spire, temple or ruins, cavern or grotto, bog or marsh, grove or court, crypt or vault, camp or village, bridge or crossing, shrine or altar", "Location card heading on the Quests screen (a later feature)", "3",
  "Neutral twilight lighting, or light per-zone tints if time allows. The game adds a frame in the rarity colors.")
R("KY-6", "Later: locations", "Location ending pictures", 6, "400 x 400", PIC, "Listed size", YOU, "Sealed vault door, ancient altar, abandoned camp, mysterious chest, standing stone, collapsed passage", "Story panel at the end of a location (a later feature)", "3",
  "Endings that show a character need no art: the game draws the character.")

R("SH-1", "Shop", "Treasure chest", 1, "300 x 300", PIC, "Double size", YOU, "Closed (counting down), ready to open (glowing) (2 files)", "Shop screen chest card", "2", "")
R("SH-2", "Shop", "Welcome-back picture", 1, "400 x 240", PIC, "Double size", YOU, "1", "Welcome-back panel shown after time away, above the 'Away 3h 12m' line", "2", "A coin pile or a campfire scene.")
R("SH-3", "Shop", "Reward header flourish", 1, "1032 x 120, transparent background", OVER, "Double size", YOU, "1", "Across the top of reward panels (quest complete, dungeon complete, chest opened)", "2", "")
R("SH-4", "Shop", "Premium / remove-ads mark", 1, "96 x 96", ICON, "Double size", YOU, "1", "Shop premium card", "2", "")
R("SH-5", "Shop", "Tavern sign", 1, "1032 x 160", PIC, "Double size", YOU, "1", "Above the recruit rows on the Shop screen", "3", "Optional flavor piece.")

R("AP-1", "App and store", "Game font", 1, "TTF or OTF, 2 weights (bold for headings, regular for body text)", "Font", "-", "-", "-", "All text in the game: names, dialogue, stats, prices, ability cards", "1",
  "Readable at 28 px. Clean numbers (counters, damage numbers, short forms like 534.4K). Full basic Latin character set plus the middle dot, dashes and curly quotes. Must work with an outline or shadow over illustrated backgrounds. The bold weight doubles as the art on ability cards.")
R("AP-2", "App and store", "App icon", 1, "1024 x 1024 master; Android adaptive icon (separate foreground and background layers, keep important content inside the central 432 px); iOS icon set", ICON, "-", YOU, "1", "Phone home screen and app stores", "1", "")
R("AP-3", "App and store", "Launch screen", 1, "1080 x 1920, or a logo on dark #1F1F20", PIC, "Listed size", YOU, "1", "Shown while the app starts", "1", "")
R("AP-4", "App and store", "Store artwork", 1, "Feature banner 1024 x 500; 6 to 8 screenshots 1080 x 1920", PIC, "Listed size", YOU, "1", "Google Play store page", "2", "")

R("TU-1", "Tutorial", "Pointing hand (tap here)", 1, "96 x 96, 2 frames", ANIM, "Double size", YOU, "1", "Tutorial: points at ability cards, item cards and the ACCEPT button", "1", "")
R("TU-2", "Tutorial", "Speech bubble", 1, "Corners 32 px; plus 4 pointer versions (up, down, left, right)", STRETCH, "Double size", YOU, "5 files", "Tutorial and tip text (34 px)", "1", "Plain middle.")
R("TU-3", "Tutorial", "'Who beats whom' diagram", 1, "900 x 520", PIC, "Double size", YOU, "1", "Help panel and tutorial", "1",
  "Two rock-paper-scissors triangles. Physical: Strength beats Dexterity beats Constitution beats Strength. Mental: Intelligence beats Wisdom beats Charisma beats Intelligence. Use the six stat icons (IC-1) with arrows meaning 'beats'. The game adds the labels in the game font.")
R("TU-4", "Tutorial", "Help (?) icon", 1, "48 x 48", ICON, "Double size", YOU, "1", "Small button in the corner of the ability card bar on the Explore screen", "1", "")


def style_header(ws, row, ncols):
    for c in range(1, ncols + 1):
        cell = ws.cell(row=row, column=c)
        cell.font = HEAD_FONT; cell.fill = HEAD_FILL; cell.alignment = WRAP; cell.border = BORDER


def write_table(ws, start_row, headers, rows, widths, fills=None):
    for i, h in enumerate(headers, 1):
        ws.cell(row=start_row, column=i, value=h)
    style_header(ws, start_row, len(headers))
    for r, row in enumerate(rows, start_row + 1):
        for c, v in enumerate(row, 1):
            cell = ws.cell(row=r, column=c, value=v)
            cell.font = BODY; cell.alignment = WRAP; cell.border = BORDER
            if fills and fills[r - start_row - 1]:
                cell.fill = PatternFill("solid", fgColor=fills[r - start_row - 1])
        longest = max(lines_needed(v or "", max(4, int(widths[c] * 1.15))) for c, v in enumerate(row))
        ws.row_dimensions[r].height = max(15, min(260, 13.5 * longest + 4))
    for i, w in enumerate(widths, 1):
        ws.column_dimensions[get_column_letter(i)].width = w


def lines_needed(text, chars_per_line):
    import math
    return sum(max(1, math.ceil(len(par) / chars_per_line)) for par in str(text).split("\n"))


def labeled_rows(ws, r, items, chars=105):
    """Bold label in column A, wrapped description merged across B:F."""
    for name, desc in items:
        a = ws.cell(row=r, column=1, value=name); a.font = BOLD; a.alignment = WRAP
        c = ws.cell(row=r, column=2, value=desc); c.font = BODY; c.alignment = WRAP
        ws.merge_cells(start_row=r, start_column=2, end_row=r, end_column=6)
        ws.row_dimensions[r].height = 14 * lines_needed(desc, chars) + 4
        r += 1
    return r


def para(ws, row, text, bold=False, size=10, col=1, span=6, height=None):
    cell = ws.cell(row=row, column=col, value=text)
    cell.font = Font(name=FONT, size=size, bold=bold)
    cell.alignment = WRAP
    if span > 1:
        ws.merge_cells(start_row=row, start_column=col, end_row=row, end_column=col + span - 1)
    if height: ws.row_dimensions[row].height = height
    return row + 1


wb = Workbook()

# ---------------------------------------------------------------- Start here
ws = wb.active; ws.title = "Start here"
ws.column_dimensions["A"].width = 26
for col in "BCDEF": ws.column_dimensions[col].width = 22
r = 1
r = para(ws, r, "Starquill: artwork request list", bold=True, size=16, height=28)
r = para(ws, r, "Starquill is a phone game, held upright. A party of four characters walks through a zone and fights waves of enemies on its own; "
        "the player taps ability cards to help and collects the loot that drops. The characters, enemies and items are built by the game from "
        "the 200 x 200 character parts you already drew. Everything else on screen is a temporary flat box or plain text. "
        "This workbook lists every piece of art that replaces one of those boxes.", height=78)
r += 1
r = para(ws, r, "What is in this workbook", bold=True, size=12)
ITEMS = [
    ("Asset list", "Every piece of art, one row each. Filter the Order column to see what to do first."),
    ("Screen pictures", "Screenshots of the game today with numbered boxes showing where each piece goes. The Picture column in the Asset list points at them (for example 'Loot #7')."),
    ("How to deliver", "Sizes, file format, naming and a few rules that keep the pieces working in the game."),
    ("Colors", "The exact colors the game already uses, so your art fits with them."),
    ("Key icon layers", "How the four layers of the key icon stack, and the 15 classes and 7 equipment types they show."),
]
r = labeled_rows(ws, r, ITEMS)
r += 1
r = para(ws, r, "How to read the Asset list columns", bold=True, size=12)
ITEMS = [
    ("ID", "A short code like CH-7. Use it in file names and when you ask us questions. The letters group related pieces (BG backgrounds, CH panels and bars, IC icons, VB ability cards, KY key, VX effects, SH shop, AP app, TU tutorial, EN enemies)."),
    ("Size (pixels)", "All sizes are measured on a 1080 x 1920 phone screen. 'Stretches sideways' means the game stretches that piece to fit the width, so only the height matters."),
    ("Kind", "Stretchable frame: a box the game stretches to any size. The corners stay exactly as you drew them; the edges and the middle get stretched. So put all the detail in the corners and keep the edges and middle plain (flat color or a simple top-to-bottom gradient). Tell us the corner size in pixels.\n"
             "Scrolling background layer: a wide strip that repeats sideways as the party walks. Its left and right edges must join seamlessly.\n"
             "Transparent overlay: a picture with a transparent background that the game draws on top of something else.\n"
             "Animation frames: the frames of a short animation laid side by side in one image, all the same size."),
    ("Deliver at", "'Double size' means draw or export the file at twice the listed size, so it stays sharp on high-resolution phones. 'Listed size' means exactly the size shown."),
    ("Who adds the color", "'You paint it in color' is normal artwork. 'The game colors it' means draw the piece in white and shades of gray on a transparent background; the game multiplies in a color (a stat color, a rarity color or a key color) when it shows it. Do not add any hue to those pieces."),
    ("Versions", "How many variations we need of that row, and what they are."),
    ("Where it appears", "Which screen and which spot, in words."),
    ("Picture", "Which screen picture and which numbered box to look at, on the Screen pictures tab."),
    ("Order", "1 - First: needed for the first release. Start here.\n2 - Next: improves the first release; do these after the 1s.\n3 - Later: for features that come after release. Skip for now."),
]
r = labeled_rows(ws, r, ITEMS)
r += 1
r = para(ws, r, "Game words you will see", bold=True, size=12)
ITEMS = [
    ("Explore screen", "The main screen where the fighting happens. The first tab at the bottom."),
    ("Wave", "One group of enemies. Beat them and the next wave walks in."),
    ("Quest", "A run of several waves with a reward at the end. Quests are offered on the Explore screen and accepted on the Quests screen."),
    ("Zone", "A region with its own look: Forest and Mountains exist today; Plains and Desert come later. Each zone has 11 stages."),
    ("Ability cards ('Verbs')", "The three cards at the bottom of the Explore screen (Fireball, Slash, Shield Bash). Tapping one fires that ability. The game's text calls them Verbs."),
    ("Stats", "Every character, enemy and item has six stats: Strength (STR), Dexterity (DEX), Constitution (CON), Intelligence (INT), Wisdom (WIS), Charisma (CHA). Each has its own color and icon. Strength beats Dexterity beats Constitution beats Strength; Intelligence beats Wisdom beats Charisma beats Intelligence."),
    ("Rarity", "How good an item is: Common, Uncommon, Rare, Epic, Legendary. Each has its own color."),
    ("Key", "A collectible item that opens a timed dungeon run. A key can have a color, an equipment type and a class, which change what the dungeon drops. The key icon is built from layers so any combination can be shown."),
    ("Dungeon", "The timed run a key opens. A status strip shows on the Explore screen while it runs."),
    ("Party and roster", "The party is the four characters fighting. The roster is every character the player owns."),
    ("Tavern", "The part of the Shop screen where new characters can be recruited for gold."),
    ("Boosts", "Things bought with gold on the Shop screen that speed the game up."),
    ("Fragments and Destinations", "A later feature on the Quests screen. Fragments are collected while exploring and unlock special locations."),
]
r = labeled_rows(ws, r, ITEMS)
r += 1
r = para(ws, r, "How many pieces, by order", bold=True, size=12)
ws.cell(row=r, column=1, value="Order").font = BOLD
ws.cell(row=r, column=2, value="Rows").font = BOLD
ws.cell(row=r, column=3, value="Meaning").font = BOLD
style_header(ws, r, 3)
r += 1
first_count_row = r
for key, label, meaning in [("1", "1 - First", "Needed for the first release. Start here."),
                            ("2", "2 - Next", "Improves the first release. Do after the 1s."),
                            ("3", "3 - Later", "For features after release. Skip for now.")]:
    ws.cell(row=r, column=1, value=label).font = BODY
    c = ws.cell(row=r, column=2, value=f'=COUNTIF(\'Asset list\'!$K$2:$K${len(ROWS) + 1},A{r})'); c.font = BODY
    ws.cell(row=r, column=3, value=meaning).font = BODY
    r += 1
ws.cell(row=r, column=1, value="Total").font = BOLD
ws.cell(row=r, column=2, value=f"=SUM(B{first_count_row}:B{r - 1})").font = BOLD
r += 2
r = para(ws, r, "Questions: ask us early and often. If a row does not make sense, that is our fault, not yours.", bold=False)

# ---------------------------------------------------------------- Asset list
ws = wb.create_sheet("Asset list")
headers = ["ID", "Group", "What it is", "How many", "Size (pixels)", "Kind", "Deliver at", "Who adds the color", "Versions",
           "Where it appears", "Order", "Picture", "Drawing notes"]
rows, fills = [], []
for row in ROWS:
    aid, group, what, count, size, kind, deliver, color, versions, where, order, notes = row
    pic = "; ".join(callouts.get(aid, [])) or "Not on a screen picture"
    rows.append([aid, group, what, count, size, kind, deliver, color, versions, where, ORDER[order], pic, notes])
    fills.append(GROUP_FILL.get(group))
widths = [8, 16, 30, 9, 30, 18, 12, 24, 34, 40, 10, 30, 60]
write_table(ws, 1, headers, rows, widths, fills)
ws.freeze_panes = "D2"
ws.auto_filter.ref = f"A1:{get_column_letter(len(headers))}{len(rows) + 1}"

# ---------------------------------------------------------------- Screen pictures
ws = wb.create_sheet("Screen pictures")
ws.column_dimensions["A"].width = 120
r = 1
r = para(ws, r, "Where the artwork goes", bold=True, size=16, span=1, height=28)
r = para(ws, r, "Each picture is one screen of the game as it looks today, with numbered boxes. The list on the right of each picture says which "
        "asset (by ID) goes in each box. The same pictures are in the folder docs/art-screen-pictures at full size.", span=1, height=48)
r += 1
pics = sorted(f for f in os.listdir(PICS) if f.endswith(".png") and f != "00-all-screens.png")
for fname in pics:
    title = fname[3:-4].replace("-", " ").capitalize()
    r = para(ws, r, f"{fname[:2]}. {title}", bold=True, size=13, span=1, height=22)
    img = XLImage(os.path.join(PICS, fname))
    scale = 800 / img.width
    img.width, img.height = int(img.width * scale), int(img.height * scale)
    ws.add_image(img, f"A{r}")
    r += int(img.height / 20) + 2

# ---------------------------------------------------------------- How to deliver
ws = wb.create_sheet("How to deliver")
rules = [
    ("Screen size", "All sizes are measured on a 1080 x 1920 phone screen. Phones with more pixels scale everything up, which is why we ask for double size on most pieces."),
    ("Deliver at", "Panels, bars, buttons, icons, cards and the key: double the listed size. Backgrounds, scrolling layers, launch screen and store art: the listed size only."),
    ("Biggest file", "No single PNG wider or taller than 4096 px. Keep everything except the scrolling background layers at 2048 or less. Any width and height are fine, they need not be round numbers."),
    ("File format", "PNG with transparency (32-bit, RGBA), standard sRGB color. Leave a 2 px transparent margin on every side of an icon or animation frame (not needed on stretchable frames)."),
    ("Stretchable frames", "The game stretches these boxes to any size. The corners stay exactly as drawn; the edges and middle get stretched. Put the detail in the corners, keep the edges and middle plain (flat color or a simple top-to-bottom gradient, never a pattern). Tell us the corner size in pixels in the file name or a note (for example 'corners 24'). The smallest file you need to send is twice the corner size plus 8 px in each direction."),
    ("Pieces the game colors", "Where the Asset list says the game adds the color, draw the piece in white and shades of gray on a transparent background. The game multiplies in a color when it shows the piece, so any hue you add would be wrong."),
    ("Button and icon versions", "Buttons and bottom tab icons need a normal and a greyed-out version at least. A pressed version is optional (the game darkens the normal one). Icons: one line weight, no drop shadows (the game adds an outline or shadow itself)."),
    ("Readability check", "Every icon must still be recognizable at half its listed size and in grayscale. The key icon must read at 104 px with all three add-on layers on."),
    ("Room for text", "Cards, notice strips, buttons and ability cards carry text the game writes (names, prices, dialogue). Keep a plain area in the middle with no detail, and mark that area in your working file."),
    ("Working files", "Send the layered working file (Aseprite, PSD or Krita) as well as the flattened PNGs."),
    ("File names", "ui_<group>_<name>[_<version>]@2x.png, all lowercase, no spaces. Groups: chrome (panels, bars, buttons), icon, nav (bottom tabs), verb (ability cards), key, bg, vfx (effects), shop, app, tut (tutorial). Versions: normal, disabled, active, or the rarity, stat or key color name. Example: ui_chrome_button_primary_disabled@2x.png"),
    ("Art that already exists", "The character parts are finished: 27 species, plus clothing, armor and weapons, each a 200 x 200 PNG part that the game stacks into a 400 x 400 character. Characters, portraits and item pictures are all built from these, so no new art is needed there."),
    ("Text in the game", "The game never uses typed symbols for dots, arrows or coins. Every one of those is an image, which is why they appear as rows in the Asset list."),
    ("Order of work", "1 - First rows before anything else, then 2 - Next, then 3 - Later. Within the 1s, the panels, bars and buttons (CH rows) unlock the most, because one set covers every screen."),
]
write_table(ws, 1, ["Rule", "What to do"], rules, [22, 120])

# ---------------------------------------------------------------- Colors
ws = wb.create_sheet("Colors")
colors = [
    ("Stat", "Strength (STR)", "#E63333", "Stat icons, ability cards, item stat lines. Color is a backup only: every stat also has an icon."),
    ("Stat", "Dexterity (DEX)", "#33CC4D", ""),
    ("Stat", "Constitution (CON)", "#B38033", ""),
    ("Stat", "Intelligence (INT)", "#4D66E6", ""),
    ("Stat", "Wisdom (WIS)", "#994DCC", ""),
    ("Stat", "Charisma (CHA)", "#E6CC33", ""),
    ("Rarity", "Common", "#B3B3B3", "Item names and rarity frames (IC-6)."),
    ("Rarity", "Uncommon", "#4DCC4D", ""),
    ("Rarity", "Rare", "#4D80FF", ""),
    ("Rarity", "Epic", "#B34DE6", ""),
    ("Rarity", "Legendary", "#FF991A", ""),
    ("Meaning", "Better / upgrade", "#4DE64D", "The +12 tags and UPGRADE labels. Reserved: do not use it for decoration."),
    ("Meaning", "Worse / downgrade", "#E64D4D", "The -3 tags and delete or sell buttons. Reserved."),
    ("Meaning", "Gold / best", "#FFD600", "Gold counter, BEST label, offers, boss stage marker."),
    ("Accent", "Main button, selected tab, bar fill", "TBD", "Your choice. It must not be green, red, blue, purple, brown or yellow, because those already mean something above. Teal, amber or a warm off-white all work. Today's placeholder is #2E8B57."),
    ("Key color", "Red", "#D94545", "Tint applied to the key gem (KY-2). Exact values."),
    ("Key color", "Orange", "#E68C33", ""),
    ("Key color", "Brown", "#8C6133", ""),
    ("Key color", "Yellow", "#E0C740", ""),
    ("Key color", "Green", "#4DBF59", ""),
    ("Key color", "Blue", "#4D85E6", ""),
    ("Key color", "Purple", "#A661D9", ""),
    ("Key color", "Neutral", "#8C8C8C", "Also used for a key with no color."),
    ("Key color", "White", "#EBEBEB", "The gem needs built-in dark edges so it stays visible."),
    ("Key color", "Black", "#29292E", "The gem needs built-in light edges so it stays visible."),
    ("Today's placeholder", "Screen background", "#1A1A26", "The temporary dark look. You may replace this palette."),
    ("Today's placeholder", "Card", "#232330", ""),
    ("Today's placeholder", "Pop-up panel", "#2C2C3A", ""),
]
write_table(ws, 1, ["Family", "Name", "Hex", "Swatch", "Used for"], [(a, b, c, "", d) for a, b, c, d in colors], [20, 30, 10, 8, 80])
for i, (_, _, hx, _) in enumerate(colors, 2):
    if hx.startswith("#"):
        ws.cell(row=i, column=4).fill = PatternFill("solid", fgColor=hx[1:])

# ---------------------------------------------------------------- Key icon layers
ws = wb.create_sheet("Key icon layers")
r = 1
r = para(ws, r, "The key icon is four pictures stacked on one 256 x 256 canvas", bold=True, size=13, span=5, height=22)
r = para(ws, r, "The key body is always shown. The other three layers appear only when the key has that add-on, so every combination has to line up. "
        "The handle loop is the round part at the upper-left; the teeth are the notched end at the lower-right; the collar is where the loop meets the shaft.", span=5, height=48)
r += 1
layers = [
    ("1 (bottom)", "Key body (KY-1)", "Fills the canvas: handle loop upper-left, teeth lower-right", 2, "A painted steel version plus a white/gray version the game colors. Always shown."),
    ("2", "Gem (KY-2)", "About 72 px, centered in the handle loop", 1, "The game colors it in one of the 10 key colors (Colors tab). When the key has no color the loop shows an empty socket."),
    ("3", "Equipment symbol (KY-3)", "About 80 px, over the teeth", 7, "When absent, the teeth are plain."),
    ("4 (top)", "Class medallion (KY-4)", "About 88 px, on the collar", 15, "When absent, the collar is plain."),
    ("Drawn by the game", "Difficulty dots (CH-15)", "Next to the icon, not on it", "-", "1 to 6 filled dots."),
]
write_table(ws, r, ["Layer", "Piece", "Where on the canvas", "Files", "Notes"], layers, [16, 26, 44, 8, 70])
r += len(layers) + 2
r = para(ws, r, "The 7 equipment symbols (KY-3)", bold=True, size=12, span=5)
slots = [("Head", "Helmet"), ("Torso", "Chest plate"), ("Arms", "Gauntlet"), ("Legs", "Leg armor"), ("Feet", "Boot"), ("Weapon", "Sword"), ("Misc", "Ring or amulet")]
write_table(ws, r, ["Equipment type", "Suggested symbol"], slots, [16, 26])
r += len(slots) + 2
r = para(ws, r, "The 15 class medallions (KY-4). Each class favors two stats; hint at both.", bold=True, size=12, span=5)
classes = [
    ("Warrior", "Strength + Dexterity", "Crossed swords"), ("Juggernaut", "Strength + Constitution", "Anvil or tower shield"),
    ("Battlemage", "Strength + Intelligence", "Sword wreathed in magic symbols"), ("Warden", "Strength + Wisdom", "Axe crossed with a sprig"),
    ("Warlord", "Strength + Charisma", "Crowned banner"), ("Skirmisher", "Dexterity + Constitution", "Small shield and dagger"),
    ("Saboteur", "Dexterity + Intelligence", "Gear and dagger, or a bomb"), ("Ranger", "Dexterity + Wisdom", "Bow and antler"),
    ("Trickster", "Dexterity + Charisma", "Grinning mask"), ("Sentinel", "Constitution + Intelligence", "Eye set in a shield"),
    ("Guardian", "Constitution + Wisdom", "Shield with a tree on it"), ("Champion", "Constitution + Charisma", "Laurel-wreathed cup"),
    ("Sage", "Intelligence + Wisdom", "Open book or owl"), ("Occultist", "Intelligence + Charisma", "Candle sigil or arcane eye"),
    ("Oracle", "Wisdom + Charisma", "Third eye over a crescent moon"),
]
write_table(ws, r, ["Class", "Stats", "Suggested medallion"], classes, [16, 26, 44])

wb.save(OUT)
print("wrote", OUT, "rows:", len(ROWS))
