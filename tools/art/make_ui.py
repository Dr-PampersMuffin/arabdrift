#!/usr/bin/env python3
"""Builds the game's UI images in client/Assets/DriftKuwait/Resources/DriftKuwait/UI from the generated art:
backgrounds, district cards, portraits, car cut-outs for the garage tiles, the cassette, and the ARAB DRIFT logo
(drawn here with the game's own fonts, so the Arabic is always spelt and joined correctly).

  python3 tools/art/make_ui.py
"""
import glob
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont, ImageOps, ImageChops

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GEN = os.path.join(ROOT, "art", "generated")
OUT = os.path.join(ROOT, "client", "Assets", "DriftKuwait", "Resources", "DriftKuwait", "UI")
FONTS = os.path.join(ROOT, "client", "Assets", "DriftKuwait", "Resources", "DriftKuwait", "Fonts")


def latest(asset):
    return sorted(glob.glob(os.path.join(GEN, asset, asset + "__v*.png")))[-1]


def cover(im, w, h, focus=(0.5, 0.5)):
    s = max(w / im.width, h / im.height)
    im = im.resize((int(im.width * s + 0.5), int(im.height * s + 0.5)), Image.LANCZOS)
    x = int((im.width - w) * focus[0]); y = int((im.height - h) * focus[1])
    return im.crop((x, y, x + w, y + h))


def cutout(path):
    from rembg import remove, new_session
    global _SESSION
    if "_SESSION" not in globals():
        _SESSION = new_session("isnet-general-use")
    im = Image.open(path).convert("RGBA")
    out = remove(im, session=_SESSION)
    bbox = out.getbbox()
    return out.crop(bbox) if bbox else out


def logo():
    W, H = 1400, 640
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    black = ImageFont.truetype(os.path.join(FONTS, "Cairo-Black.ttf"), 250)
    lal = ImageFont.truetype(os.path.join(FONTS, "Lalezar.ttf"), 210)
    small = ImageFont.truetype(os.path.join(FONTS, "Cairo-Bold.ttf"), 46)

    # "Q8 DRIFT": heavy white letters, skewed for speed, with a dark drop shadow and a worn edge.
    txt = Image.new("L", (W, 330), 0)
    ImageDraw.Draw(txt).text((60, -40), "Q8 DRIFT", font=black, fill=255)
    txt = txt.transform(txt.size, Image.AFFINE, (1, 0.28, -60, 0, 1, 0), resample=Image.BICUBIC)
    grain = Image.effect_noise(txt.size, 90).point(lambda v: 255 if v > 18 else 0)
    worn = ImageChops.multiply(txt, grain.point(lambda v: 255 if v else 120))
    shadow = txt.filter(ImageFilter.GaussianBlur(10))
    im.paste((0, 0, 0, 200), (14, 24), shadow)
    white = Image.new("RGBA", txt.size, (250, 246, 236, 255))
    im.paste(white, (0, 10), worn)

    # Red brush swoosh under the latin title: a tapered stroke with ragged bristle edges.
    sw = Image.new("L", (W, H), 0)
    d = ImageDraw.Draw(sw)
    import random
    random.seed(7)
    for i in range(60):
        t0 = random.uniform(0, 0.08); t1 = random.uniform(0.85, 1.0)
        y0 = 300 + i * 0.55 + random.uniform(-1.5, 1.5)
        x0, x1 = 110 + t0 * 1100, 110 + t1 * 1100
        d.line([(x0, y0 + 14), (x1, y0 - 22 - i * 0.12)], fill=random.randint(170, 255), width=2)
    sw = sw.filter(ImageFilter.GaussianBlur(0.8))
    im.paste((206, 32, 28, 255), (0, 0), sw)

    # "تقحيص": Lalezar (an 80s hand-painted shop-sign style), gold to orange, right-aligned under the title.
    ar = "تقحيص"
    layer = Image.new("L", (W, 300), 0)
    dd = ImageDraw.Draw(layer)
    bb = dd.textbbox((0, 0), ar, font=lal, direction="rtl", language="ar")
    x = W - (bb[2] - bb[0]) - 90
    dd.text((x, -10), ar, font=lal, fill=255, direction="rtl", language="ar")
    grad = Image.new("RGBA", layer.size)
    gd = ImageDraw.Draw(grad)
    for y in range(layer.size[1]):
        t = y / layer.size[1]
        gd.line([(0, y), (W, y)], fill=(int(255 - 10 * t), int(206 - 90 * t), int(92 - 60 * t), 255))
    ar_shadow = layer.filter(ImageFilter.GaussianBlur(8))
    im.paste((0, 0, 0, 210), (10, 352), ar_shadow)
    im.paste(grad, (0, 340), layer)

    # Small tag line, left.
    d2 = ImageDraw.Draw(im)
    d2.text((70, 560), "KUWAIT · 1985", font=small, fill=(240, 220, 180, 230))
    bbox = im.getbbox()
    return im.crop((max(0, bbox[0] - 20), max(0, bbox[1] - 20), min(W, bbox[2] + 20), min(H, bbox[3] + 20)))


def main():
    os.makedirs(OUT, exist_ok=True)
    jpg = dict(quality=88, optimize=True)
    Image.open(latest("ui_title_bg")).convert("RGB").save(os.path.join(OUT, "title_bg.jpg"), **jpg)
    Image.open(latest("ui_garage_bg")).convert("RGB").save(os.path.join(OUT, "garage_bg.jpg"), **jpg)
    Image.open(latest("ui_loading")).convert("RGB").save(os.path.join(OUT, "loading.jpg"), **jpg)
    for d, focus in (("salwa", (0.5, 0.55)), ("rumaithiya", (0.5, 0.5)), ("ummniqa", (0.5, 0.6)), ("kabd", (0.5, 0.0))):
        src = Image.open(latest("ui_district_" + d)).convert("RGB")
        if d == "kabd":
            src = src.crop((0, 0, src.width, src.height // 2 - 8))   # this image came as two panels: keep the top one
        cover(src, 800, 500, focus).save(os.path.join(OUT, f"district_{d}.jpg"), **jpg)
    for p in ("abu_rashid", "mishari", "noura", "bader", "salem"):
        cover(Image.open(latest("por_" + p)).convert("RGB"), 512, 512, (0.5, 0.3)).save(os.path.join(OUT, f"por_{p}.jpg"), **jpg)
    cutout(latest("ui_cassette")).save(os.path.join(OUT, "cassette.png"))
    refs = {"shaheen": "car_shaheen_ref3", "ghazal": "car_ghazal_ref", "faris": "car_faris_ref", "wanait": "car_wanait_ref",
            "abusitta": "car_abusitta_ref", "dawriya": "car_dawriya_ref"}
    for cid, asset in refs.items():
        c = cutout(latest(asset))
        c.thumbnail((900, 600), Image.LANCZOS)
        c.save(os.path.join(OUT, f"car_{cid}.png"))
    logo().save(os.path.join(OUT, "logo.png"))
    for f in sorted(os.listdir(OUT)):
        print(f, os.path.getsize(os.path.join(OUT, f)) // 1024, "KB")


if __name__ == "__main__":
    main()
