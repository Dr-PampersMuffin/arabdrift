#!/usr/bin/env python3
"""Draws the HUD and menu kit (icons, gauge, pedals, handbrake, steering buttons, panels) as crisp PNGs at 4x and
downsamples them, into client/Assets/DriftKuwait/Resources/DriftKuwait/UI/hud/.

  python3 tools/art/make_hud.py
"""
import math
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "client", "Assets", "DriftKuwait", "Resources", "DriftKuwait", "UI", "hud")
FONTS = os.path.join(ROOT, "client", "Assets", "DriftKuwait", "Resources", "DriftKuwait", "Fonts")
S = 4  # supersampling
WHITE = (255, 255, 255, 255)
AR_DIGITS = "٠١٢٣٤٥٦٧٨٩"


def canvas(w, h):
    im = Image.new("RGBA", (w * S, h * S), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def save(im, name, w, h):
    im.resize((w, h), Image.LANCZOS).save(os.path.join(OUT, name + ".png"))


def icon(name, draw_fn, size=96):
    im, d = canvas(size, size)
    draw_fn(d, size * S)
    save(im, "ic_" + name, size, size)


def ic_pause(d, s):
    d.rounded_rectangle((s * .28, s * .22, s * .42, s * .78), s * .03, fill=WHITE)
    d.rounded_rectangle((s * .58, s * .22, s * .72, s * .78), s * .03, fill=WHITE)


def ic_gear(d, s):
    c = s / 2
    for i in range(8):
        a = i * math.pi / 4
        x, y = c + math.cos(a) * s * .33, c + math.sin(a) * s * .33
        d.ellipse((x - s * .09, y - s * .09, x + s * .09, y + s * .09), fill=WHITE)
    d.ellipse((c - s * .3, c - s * .3, c + s * .3, c + s * .3), fill=WHITE)
    d.ellipse((c - s * .12, c - s * .12, c + s * .12, c + s * .12), fill=(0, 0, 0, 0))


def ic_car(d, s):
    d.polygon([(s * .1, s * .62), (s * .14, s * .48), (s * .3, s * .44), (s * .4, s * .3), (s * .64, s * .3), (s * .76, s * .44),
               (s * .9, s * .48), (s * .92, s * .62)], fill=WHITE)
    d.polygon([(s * .43, s * .35), (s * .62, s * .35), (s * .7, s * .44), (s * .36, s * .44)], fill=(0, 0, 0, 0))
    for x in (.28, .74):
        d.ellipse((s * (x - .09), s * .55, s * (x + .09), s * .73), fill=WHITE)
        d.ellipse((s * (x - .04), s * .6, s * (x + .04), s * .68), fill=(0, 0, 0, 0))


def ic_pin(d, s):
    d.ellipse((s * .28, s * .12, s * .72, s * .56), fill=WHITE)
    d.polygon([(s * .3, s * .42), (s * .7, s * .42), (s * .5, s * .88)], fill=WHITE)
    d.ellipse((s * .41, s * .25, s * .59, s * .43), fill=(0, 0, 0, 0))


def ic_people(d, s):
    for x, r, y in ((.35, .11, .3), (.66, .1, .32)):
        d.ellipse((s * (x - r), s * (y - r), s * (x + r), s * (y + r)), fill=WHITE)
        d.pieslice((s * (x - .2), s * (y + .14), s * (x + .2), s * (y + .55)), 180, 360, fill=WHITE)


def ic_flag(d, s):
    d.rectangle((s * .2, s * .12, s * .25, s * .9), fill=WHITE)
    n = 4
    for r in range(3):
        for c in range(n):
            if (r + c) % 2 == 0:
                x0 = s * (.27 + c * .14); y0 = s * (.14 + r * .12)
                d.rectangle((x0, y0, x0 + s * .14, y0 + s * .12), fill=WHITE)
    d.rectangle((s * .27, s * .14, s * .83, s * .5), outline=WHITE, width=int(s * .02))


def ic_camera(d, s):
    d.rounded_rectangle((s * .12, s * .3, s * .88, s * .78), s * .07, fill=WHITE)
    d.rectangle((s * .36, s * .22, s * .6, s * .32), fill=WHITE)
    d.ellipse((s * .34, s * .38, s * .66, s * .7), fill=(0, 0, 0, 0))
    d.ellipse((s * .41, s * .45, s * .59, s * .63), fill=WHITE)


def ic_video(d, s):
    d.rounded_rectangle((s * .1, s * .3, s * .66, s * .72), s * .06, fill=WHITE)
    d.polygon([(s * .68, s * .51), (s * .9, s * .32), (s * .9, s * .7)], fill=WHITE)


def ic_lights(d, s):
    d.pieslice((s * .12, s * .22, s * .56, s * .78), 90, 270, fill=WHITE)
    d.rectangle((s * .33, s * .22, s * .4, s * .78), fill=WHITE)
    for i in range(4):
        y = s * (.3 + i * .13)
        d.line([(s * .52, y), (s * .86, y + s * .03)], fill=WHITE, width=int(s * .055))


def ic_horn(d, s):
    d.polygon([(s * .12, s * .4), (s * .3, s * .4), (s * .78, s * .18), (s * .78, s * .82), (s * .3, s * .6), (s * .12, s * .6)], fill=WHITE)
    d.rectangle((s * .8, s * .38, s * .88, s * .62), fill=WHITE)


def ic_more(d, s):
    for x in (.27, .5, .73):
        d.ellipse((s * (x - .07), s * .43, s * (x + .07), s * .57), fill=WHITE)


def ic_wrench(d, s):
    d.line([(s * .25, s * .78), (s * .62, s * .4)], fill=WHITE, width=int(s * .12))
    d.ellipse((s * .5, s * .14, s * .86, s * .5), fill=WHITE)
    d.polygon([(s * .66, s * .1), (s * .8, s * .1), (s * .74, s * .33)], fill=(0, 0, 0, 0))
    d.ellipse((s * .2, s * .72, s * .3, s * .82), fill=WHITE)


def ic_spray(d, s):
    d.rounded_rectangle((s * .3, s * .38, s * .62, s * .9), s * .05, fill=WHITE)
    d.rectangle((s * .38, s * .24, s * .54, s * .38), fill=WHITE)
    for i, (x, y) in enumerate(((.66, .2), (.76, .14), (.74, .3), (.84, .24), (.86, .36))):
        d.ellipse((s * (x - .025), s * (y - .025), s * (x + .025), s * (y + .025)), fill=WHITE)


def ic_map(d, s):
    pts = [(s * .12, s * .24), (s * .36, s * .14), (s * .64, s * .24), (s * .88, s * .14), (s * .88, s * .76), (s * .64, s * .86), (s * .36, s * .76), (s * .12, s * .86)]
    d.polygon(pts, outline=WHITE, width=int(s * .055))
    d.line([(s * .36, s * .14), (s * .36, s * .76)], fill=WHITE, width=int(s * .05))
    d.line([(s * .64, s * .24), (s * .64, s * .86)], fill=WHITE, width=int(s * .05))


def ic_trophy(d, s):
    d.pieslice((s * .26, s * .06, s * .74, s * .62), 0, 180, fill=WHITE)
    d.rectangle((s * .26, s * .14, s * .74, s * .34), fill=WHITE)
    d.rectangle((s * .45, s * .58, s * .55, s * .76), fill=WHITE)
    d.rounded_rectangle((s * .3, s * .74, s * .7, s * .86), s * .02, fill=WHITE)
    d.arc((s * .1, s * .16, s * .36, s * .44), 90, 270, fill=WHITE, width=int(s * .05))
    d.arc((s * .64, s * .16, s * .9, s * .44), 270, 90, fill=WHITE, width=int(s * .05))


def ic_chev(d, s, left):
    if left:
        d.line([(s * .62, s * .2), (s * .34, s * .5), (s * .62, s * .8)], fill=WHITE, width=int(s * .1), joint="curve")
    else:
        d.line([(s * .38, s * .2), (s * .66, s * .5), (s * .38, s * .8)], fill=WHITE, width=int(s * .1), joint="curve")


def ic_lock(d, s):
    d.rounded_rectangle((s * .22, s * .44, s * .78, s * .88), s * .06, fill=WHITE)
    d.arc((s * .32, s * .14, s * .68, s * .6), 180, 360, fill=WHITE, width=int(s * .08))
    d.rectangle((s * .32, s * .36, s * .4, s * .46), fill=WHITE)
    d.rectangle((s * .6, s * .36, s * .68, s * .46), fill=WHITE)


def ic_coin(d, s):
    d.ellipse((s * .08, s * .08, s * .92, s * .92), fill=(246, 196, 74, 255))
    d.ellipse((s * .2, s * .2, s * .8, s * .8), outline=(178, 120, 20, 255), width=int(s * .05))
    d.ellipse((s * .2, s * .14, s * .5, s * .36), fill=(255, 236, 170, 200))


def ic_rep(d, s):   # سمعة: a gold star
    pts = []
    for i in range(10):
        a = -math.pi / 2 + i * math.pi / 5
        r = .42 if i % 2 == 0 else .18
        pts.append((s * (.5 + math.cos(a) * r), s * (.52 + math.sin(a) * r)))
    d.polygon(pts, fill=(255, 210, 90, 255))


def ic_flame(d, s, col=(255, 70, 40, 255)):
    d.polygon([(s * .5, s * .06), (s * .78, s * .5), (s * .74, s * .76), (s * .5, s * .92), (s * .26, s * .76), (s * .22, s * .5), (s * .4, s * .3), (s * .44, s * .5)], fill=col)
    d.polygon([(s * .5, s * .45), (s * .63, s * .68), (s * .5, s * .84), (s * .37, s * .68)], fill=(255, 200, 90, 255))


def ic_back(d, s):
    ic_chev(d, s, False)


def ic_play(d, s):
    d.polygon([(s * .3, s * .18), (s * .82, s * .5), (s * .3, s * .82)], fill=WHITE)


def ic_music(d, s):
    d.ellipse((s * .18, s * .6, s * .42, s * .84), fill=WHITE)
    d.ellipse((s * .58, s * .52, s * .82, s * .76), fill=WHITE)
    d.rectangle((s * .36, s * .2, s * .42, s * .72), fill=WHITE)
    d.rectangle((s * .76, s * .12, s * .82, s * .64), fill=WHITE)
    d.polygon([(s * .36, s * .2), (s * .82, s * .12), (s * .82, s * .24), (s * .36, s * .32)], fill=WHITE)


def ic_wheel(d, s):
    w = int(s * .08)
    d.ellipse((s * .1, s * .1, s * .9, s * .9), outline=WHITE, width=w)
    d.ellipse((s * .4, s * .4, s * .6, s * .6), fill=WHITE)
    d.line([(s * .14, s * .5), (s * .4, s * .5)], fill=WHITE, width=w)
    d.line([(s * .6, s * .5), (s * .86, s * .5)], fill=WHITE, width=w)
    d.line([(s * .5, s * .6), (s * .5, s * .86)], fill=WHITE, width=w)


def ic_tilt(d, s):
    d.rounded_rectangle((s * .3, s * .12, s * .7, s * .88), s * .07, outline=WHITE, width=int(s * .06))
    d.arc((s * .05, s * .3, s * .3, s * .7), 120, 240, fill=WHITE, width=int(s * .05))
    d.arc((s * .7, s * .3, s * .95, s * .7), 300, 60, fill=WHITE, width=int(s * .05))


def ic_arrows(d, s):
    d.polygon([(s * .08, s * .5), (s * .38, s * .25), (s * .38, s * .75)], fill=WHITE)
    d.polygon([(s * .92, s * .5), (s * .62, s * .25), (s * .62, s * .75)], fill=WHITE)


def circle_button(name, size, fill, ring, glyph=None, glyph_fn=None):
    im, d = canvas(size, size)
    s = size * S
    d.ellipse((s * .03, s * .03, s * .97, s * .97), fill=fill, outline=ring, width=int(s * .035))
    if glyph_fn:
        g = Image.new("RGBA", (s, s), (0, 0, 0, 0))
        glyph_fn(ImageDraw.Draw(g), s)
        g = g.resize((int(s * .5), int(s * .5)), Image.LANCZOS)
        im.alpha_composite(g, (int(s * .25), int(s * .25)))
    save(im, name, size, size)


def handbrake():
    size = 220
    im, d = canvas(size, size)
    s = size * S
    glow = Image.new("RGBA", im.size, (0, 0, 0, 0))
    ImageDraw.Draw(glow).ellipse((s * .04, s * .04, s * .96, s * .96), fill=(255, 40, 30, 120))
    im.alpha_composite(glow.filter(ImageFilter.GaussianBlur(s * .03)))
    d.ellipse((s * .1, s * .1, s * .9, s * .9), fill=(150, 18, 16, 255), outline=(255, 120, 110, 255), width=int(s * .02))
    d.ellipse((s * .16, s * .16, s * .84, s * .84), fill=(196, 28, 22, 255))
    d.ellipse((s * .2, s * .17, s * .8, s * .5), fill=(255, 255, 255, 40))
    # (!) inside brackets: the handbrake warning symbol
    d.ellipse((s * .33, s * .33, s * .67, s * .67), outline=WHITE, width=int(s * .035))
    d.arc((s * .25, s * .3, s * .75, s * .7), 120, 240, fill=WHITE, width=int(s * .035))
    d.arc((s * .25, s * .3, s * .75, s * .7), 300, 60, fill=WHITE, width=int(s * .035))
    d.rounded_rectangle((s * .475, s * .39, s * .525, s * .54), s * .02, fill=WHITE)
    d.ellipse((s * .475, s * .57, s * .525, s * .62), fill=WHITE)
    save(im, "handbrake", size, size)


def pedal(name, w, h, ribs):
    im, d = canvas(w, h)
    sw, sh = w * S, h * S
    # brushed aluminium body with a dark rubber rim
    body = Image.new("RGBA", im.size, (0, 0, 0, 0))
    bd = ImageDraw.Draw(body)
    for y in range(sh):
        t = y / sh
        c = int(200 - 70 * t + 25 * math.sin(t * 30))
        bd.line([(0, y), (sw, y)], fill=(c, c, c + 6, 255))
    mask = Image.new("L", im.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle((sw * .06, sh * .03, sw * .94, sh * .97), sw * .14, fill=255)
    im.paste(body, (0, 0), mask)
    d.rounded_rectangle((sw * .06, sh * .03, sw * .94, sh * .97), sw * .14, outline=(40, 40, 44, 255), width=int(sw * .04))
    for i in range(ribs):
        y = sh * (.14 + i * (.74 / ribs))
        d.rounded_rectangle((sw * .2, y, sw * .8, y + sh * (.74 / ribs) * .45), sw * .05, fill=(38, 38, 42, 255))
    d.line([(sw * .12, sh * .08), (sw * .12, sh * .9)], fill=(255, 255, 255, 90), width=int(sw * .03))
    save(im, name, w, h)


def steer_button(name, left):
    size = 170
    im, d = canvas(size, size)
    s = size * S
    d.ellipse((s * .04, s * .04, s * .96, s * .96), fill=(20, 20, 24, 120), outline=(255, 255, 255, 150), width=int(s * .025))
    g = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    ic_chev(ImageDraw.Draw(g), s, left)
    g = g.resize((int(s * .5), int(s * .5)), Image.LANCZOS)
    im.alpha_composite(g, (int(s * .25), int(s * .25)))
    save(im, name, size, size)


def gauge():
    """Rev-counter dial 0-8 (x1000 rpm) in Eastern Arabic numerals, red zone from 6.5, room for digital speed."""
    size = 420
    im, d = canvas(size, size)
    s = size * S
    c = s / 2
    d.ellipse((s * .02, s * .02, s * .98, s * .98), fill=(10, 10, 12, 215), outline=(200, 200, 205, 255), width=int(s * .012))
    d.ellipse((s * .05, s * .05, s * .95, s * .95), outline=(60, 60, 66, 255), width=int(s * .006))
    a0, a1 = 135, 405          # sweep 270 degrees, clockwise from bottom-left
    red_from = a0 + (a1 - a0) * 6.5 / 8
    d.arc((s * .1, s * .1, s * .9, s * .9), red_from, a1, fill=(220, 30, 26, 255), width=int(s * .05))
    font = ImageFont.truetype(os.path.join(FONTS, "Cairo-Bold.ttf"), int(s * .085))
    for i in range(0, 81):
        v = i / 10
        a = math.radians(a0 + (a1 - a0) * v / 8)
        major = i % 10 == 0
        mid = i % 5 == 0
        r0 = .43 if major else (.44 if mid else .455)
        r1 = .475
        col = (220, 30, 26, 255) if v >= 6.5 else WHITE
        d.line([(c + math.cos(a) * s * r0, c + math.sin(a) * s * r0), (c + math.cos(a) * s * r1, c + math.sin(a) * s * r1)],
               fill=col, width=int(s * (.012 if major else .005)))
        if major:
            tx, ty = c + math.cos(a) * s * .35, c + math.sin(a) * s * .35
            t = AR_DIGITS[int(v)]
            bb = d.textbbox((0, 0), t, font=font)
            d.text((tx - (bb[2] - bb[0]) / 2 - bb[0], ty - (bb[3] - bb[1]) / 2 - bb[1]), t, font=font, fill=col)
    small = ImageFont.truetype(os.path.join(FONTS, "Cairo-Bold.ttf"), int(s * .04))
    t = "x١٠٠٠"
    bb = d.textbbox((0, 0), t, font=small)
    d.text((c - (bb[2] - bb[0]) / 2, s * .27), t, font=small, fill=(170, 170, 176, 255))
    save(im, "gauge", size, size)

    # needle: pivot at the image centre, pointing up
    im, d = canvas(size, size)
    d.polygon([(c - s * .012, c), (c, c - s * .44), (c + s * .012, c)], fill=(255, 60, 40, 255))
    d.polygon([(c - s * .02, c), (c + s * .02, c), (c, c + s * .08)], fill=(255, 60, 40, 255))
    d.ellipse((c - s * .045, c - s * .045, c + s * .045, c + s * .045), fill=(30, 30, 34, 255), outline=(200, 200, 205, 255), width=int(s * .008))
    save(im, "needle", size, size)


def minimap_frame():
    size = 340
    im, d = canvas(size, size)
    s = size * S
    d.ellipse((s * .02, s * .02, s * .98, s * .98), outline=(235, 235, 240, 230), width=int(s * .02))
    d.ellipse((s * .05, s * .05, s * .95, s * .95), outline=(0, 0, 0, 120), width=int(s * .01))
    font = ImageFont.truetype(os.path.join(FONTS, "Cairo-Black.ttf"), int(s * .085))
    d.text((s * .74, s * .06), "N", font=font, fill=WHITE)
    save(im, "minimap_frame", size, size)
    im, d = canvas(size, size)
    d.ellipse((s * .04, s * .04, s * .96, s * .96), fill=WHITE)
    save(im, "circle_mask", size, size)
    im, d = canvas(64, 64)
    s2 = 64 * S
    d.polygon([(s2 * .5, s2 * .08), (s2 * .86, s2 * .9), (s2 * .5, s2 * .7), (s2 * .14, s2 * .9)], fill=(70, 160, 255, 255), outline=WHITE, width=int(s2 * .05))
    save(im, "player_arrow", 64, 64)
    im, d = canvas(48, 48)
    s3 = 48 * S
    d.polygon([(s3 * .5, s3 * .1), (s3 * .9, s3 * .85), (s3 * .1, s3 * .85)], fill=(235, 40, 30, 255), outline=WHITE, width=int(s3 * .06))
    save(im, "police_marker", 48, 48)


def panel_sprites():
    # 9-slice rounded panel and pill (white; tinted in Unity), and a soft shadow
    for name, r in (("panel", 28), ("pill", 60), ("panel_sharp", 10)):
        im, d = canvas(128, 128)
        s = 128 * S
        d.rounded_rectangle((0, 0, s - 1, s - 1), r * S, fill=WHITE)
        save(im, name, 128, 128)
        im, d = canvas(128, 128)
        d.rounded_rectangle((S * 2, S * 2, s - S * 3, s - S * 3), r * S, outline=WHITE, width=S * 3)
        save(im, name + "_outline", 128, 128)
    # vertical gradient for darkening behind text
    g = Image.new("RGBA", (8, 256))
    for y in range(256):
        g.putpixel((0, y), (0, 0, 0, int(255 * (y / 255) ** 1.6)))
        for x in range(1, 8):
            g.putpixel((x, y), g.getpixel((0, y)))
    g.save(os.path.join(OUT, "grad_v.png"))
    h = Image.new("RGBA", (256, 8))
    for x in range(256):
        for y in range(8):
            h.putpixel((x, y), (0, 0, 0, int(255 * (x / 255) ** 1.4)))
    h.save(os.path.join(OUT, "grad_h.png"))


def angle_bar():
    w, h = 520, 40
    im, d = canvas(w, h)
    sw, sh = w * S, h * S
    n = 14
    for i in range(n):
        x0 = sw * (.02 + i * .96 / n)
        d.rounded_rectangle((x0, sh * .2, x0 + sw * .96 / n * .8, sh * .8), S * 3, fill=(255, 255, 255, 255))
    save(im, "angle_bar", w, h)


def main():
    os.makedirs(OUT, exist_ok=True)
    for n, f in (("pause", ic_pause), ("gear", ic_gear), ("car", ic_car), ("pin", ic_pin), ("people", ic_people), ("flag", ic_flag),
                 ("camera", ic_camera), ("video", ic_video), ("lights", ic_lights), ("horn", ic_horn), ("more", ic_more),
                 ("wrench", ic_wrench), ("spray", ic_spray), ("map", ic_map), ("trophy", ic_trophy), ("lock", ic_lock),
                 ("coin", ic_coin), ("rep", ic_rep), ("flame", ic_flame), ("back", ic_back), ("play", ic_play), ("music", ic_music),
                 ("wheel", ic_wheel), ("tilt", ic_tilt), ("arrows", ic_arrows),
                 ("chev_l", lambda d, s: ic_chev(d, s, True)), ("chev_r", lambda d, s: ic_chev(d, s, False))):
        icon(n, f)
    handbrake()
    pedal("pedal_gas", 150, 230, 6)
    pedal("pedal_brake", 130, 160, 4)
    steer_button("steer_l", True)
    steer_button("steer_r", False)
    circle_button("round_dark", 120, (16, 16, 20, 170), (255, 255, 255, 120))
    gauge()
    minimap_frame()
    panel_sprites()
    angle_bar()
    print(len(os.listdir(OUT)), "files in", OUT)


if __name__ == "__main__":
    main()
