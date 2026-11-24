#!/usr/bin/env python3
"""World v3 textures from the photo sources in art/generated (spec art/prompts/world_v3.json).

  facades  fac_*   cropped to the building (no sky strip / ground line), + normal map from luminance
  gates    gate_*  cropped, + normal map
  wall     wall_boundary  made seamless left-right only (it is a horizontal band), + normal map
  tiles    tex_wall_side / tex_asphalt_v3 / tex_palm_trunk  seamless both ways, + normal map
  frond    palm_frond  alpha kept, trimmed to the leaf
  sky      sky_golden  -> a 4096x1024 strip mirrored so it wraps around the horizon with no seam

Output: client/Assets/DriftKuwait/Resources/DriftKuwait/Tex/
"""
import glob
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

sys.path.insert(0, os.path.dirname(__file__))
from make_textures import seamless, normal_map  # noqa: E402

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
GEN = os.path.join(ROOT, "art", "generated")
OUT = os.path.join(ROOT, "client", "Assets", "DriftKuwait", "Resources", "DriftKuwait", "Tex")


def src(asset):
    files = sorted(glob.glob(os.path.join(GEN, asset, "*.png")))
    return files[-1] if files else None


def crop(im, l, t, r, b):
    w, h = im.size
    return im.crop((int(w * l), int(h * t), int(w * (1 - r)), int(h * (1 - b))))


def trim_rows(im):
    """Drop rows at the top that are sky (bright, grey, flat) and rows at the bottom that are ground."""
    a = np.asarray(im.convert("RGB")).astype(np.float32)
    h = a.shape[0]
    sat = a.max(2) - a.min(2)
    lum = a.mean(2)
    top = 0
    while top < h * 0.12 and lum[top].mean() > 175 and sat[top].mean() < 22:
        top += 1
    return im.crop((0, top, im.size[0], h))


def cut_sky(im):
    """Sky showing above / between the parapet becomes transparent; fully transparent top rows are cropped off."""
    from scipy import ndimage
    a = np.asarray(im.convert("RGB")).astype(np.float32)
    lum, sat = a.mean(2), a.max(2) - a.min(2)
    skyish = (lum > 190) & (sat < 26)
    lab, _ = ndimage.label(skyish)
    top_ids = set(np.unique(lab[0])) - {0}
    sky = np.isin(lab, list(top_ids))
    sky = ndimage.binary_dilation(sky, iterations=2)
    alpha = np.where(sky, 0, 255).astype(np.uint8)
    wall = a[~sky].mean(0) if (~sky).any() else a.reshape(-1, 3).mean(0)
    a[sky] = wall
    im = Image.fromarray(a.clip(0, 255).astype(np.uint8))
    rows = np.where(alpha.max(1) > 0)[0]
    top = rows[0] if len(rows) else 0
    out = im.convert("RGBA")
    out.putalpha(Image.fromarray(alpha))
    return out.crop((0, top, out.size[0], out.size[1]))


def save(im, name, jpg=True):
    path = os.path.join(OUT, name + (".jpg" if jpg else ".png"))
    if jpg:
        im.convert("RGB").save(path, quality=90)
    else:
        im.save(path)
    print("wrote", os.path.relpath(path, ROOT), im.size)


def flatten(im):
    """Remove large-scale light falloff so a tile has no visible bright/dark halves when repeated."""
    a = np.asarray(im).astype(np.float32)
    low = np.asarray(im.filter(ImageFilter.GaussianBlur(120))).astype(np.float32)
    out = a / np.maximum(low, 1) * a.reshape(-1, 3).mean(0)
    return Image.fromarray(out.clip(0, 255).astype(np.uint8))


def seamless_x(im):
    a = np.asarray(im).astype(np.float32)
    h, w = a.shape[:2]
    rolled = np.roll(a, w // 2, 1)
    x = np.abs(np.linspace(-1, 1, w))[None, :, None]
    m = np.clip(1.6 - x * 1.6, 0, 1)
    return Image.fromarray((a * m + rolled * (1 - m)).clip(0, 255).astype(np.uint8))


def main():
    os.makedirs(OUT, exist_ok=True)
    for f in sorted(glob.glob(os.path.join(GEN, "fac_*"))):
        a = os.path.basename(f)
        s = src(a)
        if not s:
            continue
        im = crop(Image.open(s).convert("RGB"), 0.012, 0.0, 0.012, 0.035)
        im = cut_sky(im)
        im = im.resize((2048, round(2048 * im.size[1] / im.size[0])), Image.LANCZOS)
        save(im, a, jpg=False)                      # RGBA: sky around the parapet is cut out (alpha clip in game)
        save(normal_map(im.convert("RGB"), 2.0), a + "_n", jpg=False)
    for a in ("gate_steel_a", "gate_steel_b"):
        s = src(a)
        if not s:
            continue
        im = crop(Image.open(s).convert("RGB"), 0.02, 0.03, 0.02, 0.03).resize((1024, 576), Image.LANCZOS)
        save(im, a)
        save(normal_map(im, 3.0), a + "_n", jpg=False)
    s = src("wall_boundary")
    if s:
        im = crop(Image.open(s).convert("RGB"), 0.0, 0.02, 0.0, 0.02).resize((2048, 1024), Image.LANCZOS)
        im = seamless_x(im)
        save(im, "wall_boundary")
        save(normal_map(im, 3.5), "wall_boundary_n", jpg=False)
    for a, st in (("tex_wall_side", 2.5), ("tex_asphalt_v3", 5.0), ("tex_palm_trunk", 6.0)):
        s = src(a)
        if not s:
            continue
        im = flatten(Image.open(s).convert("RGB").resize((1024, 1024), Image.LANCZOS))
        im = seamless(im)
        name = a.replace("tex_", "")
        save(im, name)
        save(normal_map(im, st), name + "_n", jpg=False)
    s = src("palm_frond")
    if s:
        im = Image.open(s).convert("RGBA")
        if im.getchannel("A").getextrema()[0] == 255:      # shot on white: key the background out
            arr = np.asarray(im.convert("RGB")).astype(np.float32)
            d = 255 - arr.min(2)
            al = np.clip((d - 18) * 6, 0, 255).astype(np.uint8)
            im.putalpha(Image.fromarray(al))
        bbox = im.getchannel("A").point(lambda v: 255 if v > 24 else 0).getbbox()
        if bbox:
            im = im.crop(bbox)
        im = im.resize((512, 1024), Image.LANCZOS)
        # bleed colour into the transparent pixels so mip maps do not halo
        rgb = im.convert("RGB").filter(ImageFilter.MaxFilter(9))
        out = Image.composite(im.convert("RGB"), rgb, im.getchannel("A").point(lambda v: 255 if v > 8 else 0))
        out.putalpha(im.getchannel("A"))
        save(out, "frond_v3", jpg=False)
    for sky_name in ("sky_golden", "sky_clear"):
      s = src(sky_name)
      if s:
        im = Image.open(s).convert("RGB").resize((2048, 1024), Image.LANCZOS)
        # Equirectangular 2:1 for Skybox/Panoramic: top half = sky (row 0 = zenith, middle row = horizon),
        # bottom half = the horizon colour (always behind the ground). Mirrored so it wraps with no seam.
        pano = Image.new("RGB", (4096, 2048), tuple(int(v) for v in np.asarray(im)[-8:].reshape(-1, 3).mean(0)))
        pano.paste(im, (0, 0))
        pano.paste(im.transpose(Image.FLIP_LEFT_RIGHT), (2048, 0))
        save(pano, sky_name)


if __name__ == "__main__":
    main()
