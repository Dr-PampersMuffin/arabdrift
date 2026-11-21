#!/usr/bin/env python3
"""World textures: makes the generated photos tile seamlessly (mirror-blend the seams), then derives a normal map from
luminance. Output: client/Assets/DriftKuwait/Resources/DriftKuwait/Tex/<name>.png and <name>_n.png (1024).

  python3 tools/art/make_textures.py
"""
import glob, os
import numpy as np
from PIL import Image, ImageFilter

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "client", "Assets", "DriftKuwait", "Resources", "DriftKuwait", "Tex")


def seamless(im):
    a = np.asarray(im).astype(np.float32)
    h, w = a.shape[:2]
    rolled = np.roll(np.roll(a, h // 2, 0), w // 2, 1)
    # blend the original with its half-offset copy using a mask that is 1 in the middle and 0 at the edges
    y = np.abs(np.linspace(-1, 1, h))[:, None]
    x = np.abs(np.linspace(-1, 1, w))[None, :]
    m = np.clip(1.6 - np.maximum(x, y) * 1.6, 0, 1)[..., None]
    out = a * m + rolled * (1 - m)
    return Image.fromarray(out.clip(0, 255).astype(np.uint8))


def normal_map(im, strength):
    g = np.asarray(im.convert("L").filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255.0
    dx = (np.roll(g, -1, 1) - np.roll(g, 1, 1)) * strength
    dy = (np.roll(g, -1, 0) - np.roll(g, 1, 0)) * strength
    n = np.dstack([-dx, dy, np.ones_like(g)])
    n /= np.linalg.norm(n, axis=2, keepdims=True)
    return Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8))


def main():
    os.makedirs(OUT, exist_ok=True)
    for name, strength in (("asphalt", 5.0), ("plaster", 3.0), ("paving", 4.0), ("sand", 3.0)):
        src = sorted(glob.glob(os.path.join(ROOT, "art", "generated", "tex_" + name, "*.png")))[-1]
        im = seamless(Image.open(src).convert("RGB").resize((1024, 1024), Image.LANCZOS))
        im.save(os.path.join(OUT, name + ".png"))
        normal_map(im, strength).save(os.path.join(OUT, name + "_n.png"))
        print(name)


if __name__ == "__main__":
    main()
