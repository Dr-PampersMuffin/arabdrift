"""Paint mask for a car: finds the body colour in the Tripo albedo atlas and writes paint.png next to it.
RGB = the albedo, A = how much of the pixel is body paint (1024, read on the CPU to repaint the car).
Usage: paint_mask.py <car dir> [--preview out.jpg]"""
import sys, os, numpy as np
from PIL import Image, ImageFilter
from scipy.cluster.vq import kmeans2

d = sys.argv[1]
im = np.asarray(Image.open(os.path.join(d, 'albedo.png')).convert('RGB')).astype(np.float32) / 255.
small = np.asarray(Image.open(os.path.join(d, 'albedo.png')).convert('RGB').resize((256, 256))).reshape(-1, 3).astype(np.float32) / 255.
lum_s = small @ [0.299, 0.587, 0.114]
cand = small[(lum_s > 0.22)]
np.random.seed(1)
cent, lab = kmeans2(cand, 6, minit='++', seed=1)
counts = np.bincount(lab, minlength=6)
body = cent[np.argmax(counts)]
lum = im @ np.array([0.299, 0.587, 0.114], np.float32)
lb = float(body @ [0.299, 0.587, 0.114])
# chroma direction distance (colour without brightness) + a loose brightness band
def chroma(x, l):
    return x / np.maximum(l[..., None], 1e-3)
cb = body / max(lb, 1e-3)
dc = np.linalg.norm(chroma(im, lum) - cb, axis=-1)
sat_body = body.max() - body.min()
tol = 0.30 if sat_body > 0.15 else 0.10
m = np.clip(1.0 - (dc - tol) / tol, 0, 1)
m *= np.clip((lum - lb * 0.28) / (lb * 0.2), 0, 1)                      # not the deep shadows / black trim
if sat_body < 0.15:                                                      # white / silver bodies: keep only bright greys
    m *= np.clip((lum - 0.42) / 0.12, 0, 1)
mi = Image.fromarray((m * 255).astype(np.uint8)).filter(ImageFilter.MedianFilter(5)).filter(ImageFilter.MaxFilter(3)).filter(ImageFilter.GaussianBlur(1.0))
grey = np.clip(lum / max(lb, 1e-3) * 0.82, 0, 1)
out = np.dstack([grey, grey, grey, np.asarray(mi, np.float32) / 255.])
# What the game reads: the albedo (RGB) with the mask (A) at 1024, uncompressed and CPU-readable; it computes the shading itself.
pk = np.dstack([im, out[..., 3]])
Image.fromarray((pk * 255).astype(np.uint8), 'RGBA').resize((1024, 1024), Image.LANCZOS).save(os.path.join(d, 'paintmask.png'))
print(os.path.basename(d), 'body', np.round(body, 2), 'coverage', round(float((np.asarray(mi) > 128).mean()), 3))
if '--preview' in sys.argv:
    p = sys.argv[sys.argv.index('--preview') + 1]
    tint = np.array([0.1, 0.35, 0.8], np.float32)
    a = out[..., 3:4]
    rec = im * (1 - a) + out[..., :3] * tint * a
    Image.fromarray((np.hstack([im, np.repeat(a, 3, -1), rec]) * 255).astype(np.uint8)).resize((1536, 512)).save(p)
