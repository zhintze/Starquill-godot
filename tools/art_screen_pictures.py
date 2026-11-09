"""Screen pictures for the artist hand-off.

Takes the play-mode screenshots in docs/screenshots (1080 x 2280) and draws
numbered boxes on them showing where each requested piece of art goes. Every
screen gets one picture with a legend on the right, and 00-all-screens.png
tiles them all. Also writes callouts.json (asset ID -> screen + number) which
tools/build_art_assets_xlsx.py uses to fill the Picture column.

Usage: python tools/art_screen_pictures.py docs/screenshots docs/art-screen-pictures
"""
from PIL import Image, ImageDraw, ImageFont
import json, os, sys, textwrap

SHOTS, OUT = sys.argv[1], sys.argv[2]
os.makedirs(OUT, exist_ok=True)

FONT = "/usr/share/fonts/TTF/DejaVuSans.ttf"
FONT_B = "/usr/share/fonts/TTF/DejaVuSans-Bold.ttf"
def font(size, bold=False):
    return ImageFont.truetype(FONT_B if bold else FONT, size)

SW, SH = 1080, 2280          # screenshot size
LEGEND_W = 800
HEADER_H = 150
DARK = (24, 24, 32)

# Category -> (color, legend name)
CAT = {
    "bg":    ((90, 210, 130),  "Background layer"),
    "frame": ((255, 165, 50),  "Panel, card, bar or button (stretchable)"),
    "icon":  ((90, 190, 255),  "Icon or small picture"),
    "fx":    ((255, 100, 210), "Effect or animation"),
    "key":   ((255, 225, 90),  "Key artwork"),
    "none":  ((190, 190, 200), "No art needed: the game draws this"),
}

# Each screen: (file, title, subtitle, callouts)
# callout = (ids, category, (x1, y1, x2, y2), legend text[, badge position])
SCREENS = [
    ("01-explore-quest-offered.png", "Explore", "The fight screen. The party walks right and fights waves of enemies while loot drops.", [
        ("CH-2", "frame", (0, 0, 1080, 160), "Top bar background. Holds the gold counter, key count, wave number and quest level.", (810, 78)),
        ("IC-2", "icon", (8, 45, 68, 105), "Gold coin icon, left of the gold amount."),
        ("IC-3", "icon", (405, 50, 460, 105), "Small key icon, left of the key count (7/12)."),
        ("CH-13", "frame", (0, 165, 1080, 240), "Progress bar: the travel bar. Empty bar plus a fill the game colors.", (0, 190)),
        ("CH-16", "frame", (16, 250, 1064, 372), "Notice strip. 'Quest discovered' today; also the dungeon status strip. Thin colored edges top and bottom."),
        ("BG-1a", "bg", (0, 165, 1080, 1745), "Sky layer. Fills the whole fight area behind everything.", (500, 440)),
        ("BG-1b", "bg", (0, 820, 1080, 1745), "Middle layer: trees, hills, buildings. This layer gives the zone its look.", (500, 830)),
        ("BG-1c", "bg", (0, 1300, 1080, 1745), "Ground layer: the trail the party walks on.", (500, 1310)),
        ("BG-1d", "bg", (0, 1600, 1080, 1745), "Front layer: grass and props drawn over the characters' feet. Transparent above the ground line.", (500, 1610)),
        ("", "none", (0, 1000, 720, 1745), "Party characters. The game builds them from the character parts you already made. No new art.", (10, 1010)),
        ("EN-3", "frame", (820, 1180, 1080, 1246), "Enemy name plate, above each enemy. Name left, stat icon right."),
        ("EN-2, EN-1", "none", (820, 1250, 1080, 1680), "Enemy. Built by the game from parts (EN-2 monster parts are a later request). Boss enemies get the EN-1 glow behind them.", (830, 1260)),
        ("EN-4", "frame", (835, 1685, 1075, 1700), "Enemy health bar (sits inside the name plate in the new design).", (835, 1640)),
        ("VX-1, VX-2, VX-5", "fx", (830, 1400, 1070, 1560), "Effects on the enemy: hit spark, defeat puff, floating damage numbers.", (840, 1405)),
        ("VX-3, VX-4", "fx", (0, 900, 1080, 1000), "Wave-cleared sweep crosses the fight area here. Coin burst plays by the gold counter.", (500, 905)),
        ("CH-17", "frame", (16, 1590, 1064, 1735), "Short pop-up messages ('Loot found', 'Undo') stack here, just above the ability cards. Up to 3 rows.", (16, 1560)),
        ("VB-1", "frame", (0, 1748, 322, 1968), "Ability card frame. Three cards side by side. Plain middle for the ability name.", (160, 1935)),
        ("IC-1", "icon", (8, 1756, 100, 1848), "Stat icon in the top-left corner of each ability card (with a 3-letter tag).", (60, 1756)),
        ("EN-5", "icon", (700, 1756, 740, 1796), "'Strong against' marker in the top-right corner of an ability card.", (650, 1756)),
        ("VB-2, VB-3", "fx", (762, 1748, 1080, 1968), "Overlays drawn over a card: recharge wipe (10 s) and locked/dimmed (3 s after use)."),
        ("CH-3", "frame", (0, 1985, 1080, 2280), "Bottom tab bar background.", (540, 2210)),
        ("CH-4", "icon", (60, 1992, 156, 2052), "Tab icons (5 tabs, each with a selected and an unselected version) sit above each label.", (160, 1992)),
        ("CH-5", "icon", (500, 1990, 530, 2020), "Alert dot on a tab icon when something new is waiting.", (540, 1990)),
        ("CH-6", "frame", (30, 2128, 190, 2146), "Selected-tab underline.", (200, 2118)),
    ]),
    ("02-quests.png", "Quests", "Quest offers, progress through the current zone, and the keys you have found.", [
        ("CH-1", "bg", (0, 165, 1080, 1985), "Screen background under everything on this screen (also Loot, Party, Shop).", (720, 1090)),
        ("CH-18", "frame", (16, 272, 1064, 284), "Divider line under each section heading (QUEST, DESTINATIONS, KEYS).", (300, 236)),
        ("CH-7", "frame", (16, 300, 1064, 720), "Card: the box behind the quest offer."),
        ("IC-10", "icon", (900, 320, 1050, 400), "Quest difficulty badge (Normal / Elite / Hard / Boss) goes here."),
        ("CH-11", "frame", (48, 535, 528, 685), "Main-action button (ACCEPT)."),
        ("CH-11", "frame", (548, 535, 1032, 685), "Secondary button (Decline), gray."),
        ("IC-11", "icon", (520, 735, 605, 815), "Zone emblem next to the zone name."),
        ("CH-19", "frame", (24, 830, 960, 905), "Stage markers: 11 in a row (done, current, not yet)."),
        ("CH-19", "frame", (990, 820, 1068, 905), "Boss stage marker (gold diamond).", (1000, 780)),
        ("IC-10", "icon", (325, 925, 372, 968), "Small difficulty markers under Elite / Hard stages.", (380, 920)),
        ("IC-20", "icon", (8, 1110, 58, 1155), "Fragment shard icon, left of the Fragments counter.", (60, 1100)),
        ("CH-13", "frame", (16, 1185, 1064, 1216), "Progress bar: fragments collected.", (300, 1160)),
        ("IC-3", "icon", (8, 1250, 58, 1300), "Small key icon, left of the KEYS heading.", (60, 1240)),
        ("CH-7", "frame", (16, 1325, 1064, 1520), "Card: one per key."),
        ("KY-1, KY-2, KY-3, KY-4", "key", (28, 1345, 132, 1449), "Layered key icon at 104 px: body + gem + equipment symbol + class medallion.", (140, 1340)),
        ("CH-15", "icon", (800, 1360, 1032, 1400), "Difficulty dots (filled and empty)."),
        ("CH-3, CH-4, CH-6", "frame", (0, 1985, 1080, 2280), "Bottom tab bar: see the Explore picture."),
    ]),
    ("03-loot.png", "Loot", "Everything the party has picked up. Tap an item to open its panel.", [
        ("CH-1", "bg", (0, 165, 1080, 1985), "Screen background.", (1050, 1960)),
        ("IC-14", "icon", (345, 205, 395, 255), "Bag-full icon appears next to the inventory counter at 50/50."),
        ("CH-11", "frame", (675, 180, 1055, 300), "Main-action button (Optimize All)."),
        ("CH-12", "frame", (16, 310, 360, 435), "Tab buttons: a row of choices with one selected (Score / Newest / Rarity)."),
        ("CH-8", "frame", (10, 465, 1070, 590), "Highlighted card: the 'rewards waiting' notice."),
        ("IC-13", "icon", (20, 495, 85, 560), "Rewards-waiting (mailbox) icon at the left of that notice.", (90, 470)),
        ("CH-7", "frame", (16, 615, 1064, 890), "Card: one per item.", (300, 620)),
        ("IC-6", "icon", (40, 638, 276, 874), "Rarity frame behind the item picture (5 colors). The item picture itself is built by the game."),
        ("IC-1", "icon", (298, 712, 345, 757), "Stat icons next to the two stat lines (CON, CHA).", (350, 690)),
        ("CH-15", "icon", (735, 780, 872, 816), "Ability level dots.", (735, 750)),
        ("CH-14", "frame", (895, 715, 1040, 806), "Small label tag: the +12 score change. The game colors it green, red or gray.", (900, 665)),
        ("IC-7", "icon", (960, 625, 1030, 695), "Portrait frame (56 px) around the head of the character who would get this item.", (890, 605)),
        ("CH-3, CH-4, CH-6", "frame", (0, 1985, 1080, 2280), "Bottom tab bar: see the Explore picture."),
    ]),
    ("04-item-sheet.png", "Item panel", "Pop-up panel that slides up when you tap an item. Every pop-up panel in the game uses these pieces.", [
        ("", "none", (0, 0, 1080, 1020), "The game darkens the screen behind the panel. No art.", (500, 600)),
        ("CH-9", "frame", (0, 1022, 1080, 2280), "Pop-up panel background with rounded top corners. Height varies (730 to 1632 px)."),
        ("CH-10", "frame", (445, 1040, 635, 1062), "Drag handle bar at the top center.", (640, 1020)),
        ("IC-4", "icon", (670, 1045, 1050, 1150), "Close (X) button, top-right. Replaces today's Back button."),
        ("IC-1", "icon", (18, 1350, 115, 1512), "Stat icons next to each stat row.", (120, 1340)),
        ("CH-15", "icon", (25, 1605, 130, 1642), "Ability level dots.", (140, 1595)),
        ("IC-7", "icon", (16, 1780, 1064, 2090), "'Equip on' row: portrait frames around each character's head. (Names are squashed today because of a layout bug that is being fixed.)"),
        ("CH-11", "frame", (16, 2100, 700, 2280), "Main-action button (EQUIP)."),
        ("CH-11", "frame", (715, 2100, 1055, 2280), "Delete / sell button (red)."),
    ]),
    ("05-party-roster.png", "Party: Roster", "One character shown large; every character you own in the grid below.", [
        ("CH-1", "bg", (0, 165, 1080, 1985), "Screen background.", (500, 1930)),
        ("CH-11, IC-15", "frame", (800, 175, 1055, 300), "Main-action button (Level Up) with the level-up arrow icon."),
        ("CH-13", "frame", (0, 296, 1080, 314), "Progress bar: experience toward the next level.", (500, 262)),
        ("IC-1", "icon", (10, 330, 200, 800), "Stat icons left of each of the six stats."),
        ("CH-11", "frame", (16, 810, 205, 955), "Secondary button (Train)."),
        ("", "none", (330, 330, 800, 950), "Character drawn by the game from your parts. No art.", (340, 340)),
        ("IC-7", "icon", (943, 323, 1077, 452), "Portrait frame around each of the four party members. Yellow = selected version.", (900, 300)),
        ("CH-12", "frame", (16, 985, 360, 1105), "Tab buttons (Roster / Equipment / Actions)."),
        ("CH-8", "frame", (10, 1125, 200, 1365), "Highlighted card: the selected roster character."),
        ("IC-7", "icon", (20, 1135, 195, 1315), "Portrait frame (150 px) on each roster card.", (200, 1180)),
        ("IC-9", "icon", (355, 1130, 400, 1175), "Party-number badge in the card corner (the game writes 1 to 4 on it).", (405, 1120)),
        ("CH-7", "frame", (210, 1125, 400, 1365), "Card: one per roster character.", (215, 1330)),
        ("CH-3, CH-4, CH-6", "frame", (0, 1985, 1080, 2280), "Bottom tab bar: see the Explore picture."),
    ]),
    ("06-party-equipment.png", "Party: Equipment", "What the selected character is wearing, one card per slot. Same header as the Roster tab.", [
        ("CH-18", "frame", (16, 1210, 1064, 1220), "Divider line under the ARMOR heading.", (300, 1170)),
        ("CH-7", "frame", (16, 1245, 1064, 1540), "Card: one per equipment slot.", (300, 1250)),
        ("IC-6", "icon", (40, 1265, 276, 1500), "Rarity frame behind the item picture."),
        ("IC-5", "icon", (40, 1570, 276, 1800), "Empty slots show the slot picture (helmet, boot, sword...) instead of an item.", (280, 1560)),
        ("IC-1", "icon", (298, 1360, 345, 1405), "Stat icons next to the stat lines.", (350, 1335)),
        ("CH-14", "frame", (840, 1370, 1055, 1432), "Small label tag (UPGRADE).", (760, 1330)),
        ("CH-15", "icon", (768, 1428, 882, 1470), "Ability level dots.", (700, 1470)),
        ("CH-11", "frame", (320, 1905, 760, 1968), "Secondary button (Optimize)."),
    ]),
    ("07-party-actions.png", "Party: Actions", "The selected character's abilities. Same header as the Roster tab.", [
        ("CH-14, IC-1", "frame", (16, 1125, 1064, 1170), "Row of small label tags showing how many abilities of each stat the party has. Stat icons go in them."),
        ("CH-20", "frame", (16, 1195, 1064, 1290), "Ability list row: colored stripe on the left, name, damage and recharge time on the right."),
        ("IC-12", "icon", (500, 1205, 560, 1265), "Ability type badge (Physical / Mental) in each row.", (565, 1195)),
        ("IC-8", "icon", (992, 1220, 1030, 1258), "Recharge clock icon before the seconds (today a blank square).", (940, 1170)),
        ("IC-18", "icon", (16, 1400, 1064, 1490), "Empty and locked slot rows. Locked rows get the lock icon.", (16, 1370)),
        ("CH-13", "frame", (600, 1815, 1064, 1875), "Progress bar: ability slots unlocked."),
    ]),
    ("08-shop.png", "Shop", "Boosts to buy with gold, the tavern where new characters can be recruited, and the treasure chest (further down).", [
        ("CH-1", "bg", (0, 165, 1080, 1985), "Screen background.", (900, 660)),
        ("CH-18", "frame", (16, 272, 1064, 284), "Divider line under each heading.", (300, 236)),
        ("CH-7", "frame", (16, 300, 1064, 640), "Card: one per boost.", (300, 305)),
        ("IC-17", "icon", (30, 325, 126, 421), "Boost icon (one per boost) goes here, left of the title."),
        ("CH-11", "frame", (48, 455, 1032, 605), "Main-action button (Buy)."),
        ("SH-5", "icon", (16, 1070, 1064, 1140), "Optional tavern sign artwork in place of the plain TAVERN heading."),
        ("IC-8", "icon", (8, 1180, 50, 1222), "Recharge clock icon before the countdown ('New faces in 4:39:25').", (60, 1150)),
        ("CH-7", "frame", (40, 1240, 1040, 1385), "Card: one row per recruit.", (300, 1245)),
        ("IC-7", "icon", (55, 1250, 185, 1380), "Portrait frame (104 px) around each recruit's head.", (190, 1300)),
        ("SH-1, SH-4, IC-16", "icon", (16, 1895, 1064, 1975), "Further down (scroll): the chest card with the chest picture and a watch-an-ad button, then the premium card with its mark."),
        ("CH-3, CH-4, CH-6", "frame", (0, 1985, 1080, 2280), "Bottom tab bar: see the Explore picture."),
    ]),
    ("09-tavern-recruit-sheet.png", "Recruit panel", "Pop-up panel shown when you tap a recruit in the tavern.", [
        ("CH-9", "frame", (0, 692, 1080, 2280), "Pop-up panel background."),
        ("CH-10", "frame", (445, 700, 635, 725), "Drag handle bar.", (640, 680)),
        ("IC-4", "icon", (670, 705, 1050, 810), "Close (X) button, replaces Back."),
        ("IC-7", "icon", (60, 830, 360, 1130), "Large portrait frame (300 px) around the recruit."),
        ("", "none", (80, 850, 320, 1180), "Recruit drawn by the game. No art.", (330, 1130)),
        ("IC-1", "icon", (16, 1250, 1064, 1400), "Stat icons next to the six stat values."),
        ("IC-1, IC-12", "icon", (16, 1470, 420, 1600), "Stat icon and ability type badge after each ability name."),
        ("CH-11", "frame", (30, 1725, 1050, 1885), "Main-action button (RECRUIT)."),
    ]),
    ("10-key-sheet.png", "Key panel", "Pop-up panel for one key. A key opens a timed dungeon run; its add-ons change what drops.", [
        ("CH-9", "frame", (0, 1022, 1080, 2280), "Pop-up panel background."),
        ("CH-10", "frame", (445, 1040, 635, 1062), "Drag handle bar.", (640, 1020)),
        ("IC-4", "icon", (670, 1045, 1050, 1150), "Close (X) button, replaces Back."),
        ("KY-1, KY-2, KY-3, KY-4", "key", (850, 1150, 1050, 1350), "Layered key icon at 200 px goes here."),
        ("CH-14", "frame", (30, 1340, 385, 1422), "Small label tag with the class name (Warrior)."),
        ("CH-15", "icon", (30, 1608, 275, 1646), "Difficulty dots.", (280, 1590)),
        ("CH-11", "frame", (30, 1910, 1050, 2070), "Main-action button (OPEN DUNGEON)."),
        ("CH-11", "frame", (30, 2110, 530, 2262), "Secondary button (FUSE)."),
        ("CH-11", "frame", (550, 2110, 1050, 2262), "Delete / sell button (red)."),
    ]),
    ("11-quest-offer-sheet.png", "Quest offer panel", "Pop-up panel shown when you tap 'Quest discovered' on the Explore screen.", [
        ("CH-9", "frame", (0, 1150, 1080, 2280), "Pop-up panel background."),
        ("CH-10", "frame", (445, 1157, 635, 1178), "Drag handle bar.", (640, 1135)),
        ("IC-4", "icon", (670, 1160, 1050, 1265), "Close (X) button, replaces Back."),
        ("IC-10, CH-14", "icon", (790, 1270, 1050, 1345), "Difficulty badge and label tag (Hard)."),
        ("IC-11", "icon", (520, 1285, 605, 1368), "Zone emblem after the quest name."),
        ("CH-11", "frame", (30, 1560, 530, 1715), "Main-action button (ACCEPT)."),
        ("CH-11", "frame", (545, 1560, 1050, 1715), "Secondary button (Decline)."),
        ("SH-3", "fx", (16, 1190, 1064, 1290), "Reward panels (quest complete, chest opened) use this same layout with a flourish across the top.", (16, 1290)),
    ]),
]


def draw_dashed_rect(d, box, color, width, dash=18, gap=12):
    x1, y1, x2, y2 = box
    def seg(a, b, horiz, fixed):
        p = a
        while p < b:
            q = min(p + dash, b)
            if horiz: d.line([p, fixed, q, fixed], fill=color, width=width)
            else: d.line([fixed, p, fixed, q], fill=color, width=width)
            p = q + gap
    seg(x1, x2, True, y1); seg(x1, x2, True, y2); seg(y1, y2, False, x1); seg(y1, y2, False, x2)


def badge(d, cx, cy, n, color, r=27):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=color, outline=(0, 0, 0), width=3)
    f = font(30 if n < 10 else 26, True)
    tw = d.textlength(str(n), font=f)
    d.text((cx - tw / 2, cy - 19), str(n), font=f, fill=(0, 0, 0))


def annotate(shot_path, callouts):
    """Return the screenshot with numbered boxes drawn on it."""
    im = Image.open(shot_path).convert("RGBA")
    d = ImageDraw.Draw(im, "RGBA")
    for n, c in enumerate(callouts, 1):
        ids, cat, box, _ = c[:4]
        color = CAT[cat][0]
        x1, y1, x2, y2 = box
        if cat == "none":
            draw_dashed_rect(d, box, color, 4)
        else:
            d.rectangle(box, outline=color, width=5)
        bx, by = (c[4] if len(c) > 4 else (x1, y1))
        bx = max(30, min(bx, SW - 30)); by = max(30, min(by, SH - 30))
        badge(d, bx, by, n, color)
    return im


def wrap(text, width):
    return textwrap.wrap(text, width)


def legend_panel(title, subtitle, callouts):
    small = len(callouts) > 16
    fs = 21 if small else 24
    width_chars = 60 if small else 52
    gap_line = fs + 6
    gap_row = 10 if small else 14
    im = Image.new("RGBA", (LEGEND_W, SH * 2), DARK + (255,))
    d = ImageDraw.Draw(im, "RGBA")
    x0 = 30
    y = 30
    d.text((x0, y), title, font=font(46, True), fill=(255, 255, 255)); y += 62
    for line in wrap(subtitle, 58):
        d.text((x0, y), line, font=font(24), fill=(200, 200, 215)); y += 32
    y += 14
    d.line([x0, y, LEGEND_W - 30, y], fill=(80, 80, 95), width=2); y += 18
    d.text((x0, y), "Numbered boxes = where your artwork goes", font=font(24, True), fill=(255, 255, 255)); y += 34
    d.text((x0, y), "Codes like CH-7 are rows in the Asset list tab", font=font(22), fill=(200, 200, 215)); y += 42
    body = font(fs); idf = font(fs, True)
    for n, c in enumerate(callouts, 1):
        ids, cat, box, text = c[:4]
        color = CAT[cat][0]
        badge(d, x0 + 24, y + 14, n, color, r=21)
        tx = x0 + 66
        if ids:
            d.text((tx, y), ids, font=idf, fill=color)
            y += gap_line
        for line in wrap(text, width_chars):
            d.text((tx, y), line, font=body, fill=(235, 235, 240)); y += gap_line
        y += gap_row
    return im.crop((0, 0, LEGEND_W, max(SH, y + 20)))


def header(width, index, title):
    im = Image.new("RGBA", (width, HEADER_H), DARK + (255,))
    d = ImageDraw.Draw(im)
    d.text((30, 18), f"{index}. {title}", font=font(44, True), fill=(255, 255, 255))
    d.text((30, 70), "Left: the game as it looks today (placeholder boxes and text). Right: what each numbered box needs.",
           font=font(22), fill=(200, 200, 215))
    x = 30; y = 108
    d.text((x, y), "Box colors:", font=font(20, True), fill=(255, 255, 255)); x += 130
    for cat, (color, name) in CAT.items():
        if cat == "none":
            draw_dashed_rect(d, (x, y + 4, x + 30, y + 24), color, 3, dash=8, gap=5)
        else:
            d.rectangle([x, y + 4, x + 30, y + 24], outline=color, width=4)
        d.text((x + 40, y), name, font=font(20), fill=(220, 220, 230))
        x += 40 + int(d.textlength(name, font=font(20))) + 26
    return im


callout_index = {}   # asset id -> list of "Screen #n"
tiles = []
for i, (fname, title, subtitle, callouts) in enumerate(SCREENS, 1):
    shot = annotate(os.path.join(SHOTS, fname), callouts)
    tiles.append((title, shot))
    leg = legend_panel(title, subtitle, callouts)
    out = Image.new("RGBA", (SW + LEGEND_W, max(SH, leg.height) + HEADER_H), DARK + (255,))
    out.paste(header(out.width, i, title), (0, 0))
    out.paste(shot, (0, HEADER_H))
    out.paste(leg, (SW, HEADER_H))
    name = f"{i:02d}-{title.lower().replace(':', '').replace(' ', '-')}.png"
    out.convert("RGB").save(os.path.join(OUT, name), optimize=True)
    for n, c in enumerate(callouts, 1):
        for aid in [s.strip() for s in c[0].split(",") if s.strip()]:
            callout_index.setdefault(aid, []).append(f"{title} #{n}")
    print("wrote", name)

# Overview: every annotated screen side by side, like a contact sheet.
scale = 0.42
tw, th = int(SW * scale), int(SH * scale)
cols, gap, cap = 6, 24, 60
rows = (len(tiles) + cols - 1) // cols
ov = Image.new("RGB", (cols * tw + (cols + 1) * gap, 100 + rows * (th + cap + gap)), DARK)
d = ImageDraw.Draw(ov)
d.text((gap, 22), "Starquill: every screen with numbered boxes showing where the artwork goes. "
       "Open the single-screen pictures (01 to 11) to read the numbers.", font=font(30, True), fill=(255, 255, 255))
for i, (title, shot) in enumerate(tiles):
    r, c = divmod(i, cols)
    x = gap + c * (tw + gap); y = 100 + r * (th + cap + gap)
    d.text((x, y + 8), f"{i + 1}. {title}", font=font(30, True), fill=(255, 255, 255))
    ov.paste(shot.resize((tw, th), Image.LANCZOS).convert("RGB"), (x, y + cap))
ov.save(os.path.join(OUT, "00-all-screens.png"), optimize=True)
print("wrote 00-all-screens.png", ov.size)

with open(os.path.join(OUT, "callouts.json"), "w") as f:
    json.dump(callout_index, f, indent=1, sort_keys=True)
print("callouts for", len(callout_index), "asset ids")
