import sys, random, math
from PIL import Image, ImageDraw, ImageFilter, ImageChops, ImageEnhance
carf, out, style = sys.argv[1], sys.argv[2], sys.argv[3]
N = 1024
random.seed(11)

def vgrad(stops):
    im = Image.new("RGB", (1, N)); px = im.load()
    for y in range(N):
        t = y / (N - 1)
        for i in range(len(stops) - 1):
            (o0, c0), (o1, c1) = stops[i], stops[i + 1]
            if o0 <= t <= o1:
                k = (t - o0) / max(1e-6, o1 - o0)
                px[0, y] = tuple(int(c0[j] + (c1[j] - c0[j]) * k) for j in range(3)); break
        else:
            px[0, y] = stops[-1][1] if t > stops[-1][0] else stops[0][1]
    return im.resize((N, N))

def towers(draw, x0, base, s, col):
    def needle(x, top, w):
        draw.polygon([(x - w, base), (x - w * 0.45, base - top), (x + w * 0.45, base - top), (x + w, base)], fill=col)
    needle(x0, 560 * s, 7 * s); draw.ellipse([x0 - 62 * s, base - 392 * s, x0 + 62 * s, base - 268 * s], fill=col)
    draw.ellipse([x0 - 31 * s, base - 470 * s, x0 + 31 * s, base - 408 * s], fill=col)
    x1 = x0 + 92 * s; needle(x1, 380 * s, 6 * s); draw.ellipse([x1 - 46 * s, base - 296 * s, x1 + 46 * s, base - 204 * s], fill=col)
    needle(x0 - 88 * s, 300 * s, 4 * s)

P = {
 "sunset": dict(sky=[(0, (38, 16, 52)), (0.42, (178, 58, 40)), (0.62, (242, 150, 60)), (0.66, (250, 190, 100))],
                sun=(255, 214, 140), sunpos=(330, 520, 175), tw=(48, 20, 40), ground=[(0, (40, 28, 34)), (1, (14, 10, 14))], smoke=(255, 236, 214), horizon=660),
 "night":  dict(sky=[(0, (4, 10, 22)), (0.45, (16, 46, 74)), (0.64, (28, 110, 120)), (0.66, (60, 160, 150))],
                sun=(236, 236, 222), sunpos=(250, 250, 70), tw=(5, 12, 20), ground=[(0, (20, 22, 28)), (1, (6, 6, 9))], smoke=(214, 226, 236), horizon=660),
 "flag":   dict(sky=[(0, (0, 98, 52)), (0.6, (0, 122, 61)), (0.66, (40, 150, 90))],
                sun=(255, 255, 255), sunpos=(250, 250, 0), tw=(0, 50, 28), ground=[(0, (26, 24, 26)), (1, (8, 8, 8))], smoke=(255, 255, 255), horizon=660),
}[style]
img = vgrad(P["sky"]).convert("RGBA")
d = ImageDraw.Draw(img)
sx, sy, sr = P["sunpos"]
if sr:
    glow = Image.new("RGBA", (N, N), (0, 0, 0, 0)); gd = ImageDraw.Draw(glow)
    gd.ellipse([sx - sr * 1.8, sy - sr * 1.8, sx + sr * 1.8, sy + sr * 1.8], fill=P["sun"] + (90,))
    img.alpha_composite(glow.filter(ImageFilter.GaussianBlur(60)))
    d.ellipse([sx - sr, sy - sr, sx + sr, sy + sr], fill=P["sun"])
H = P["horizon"]
towers(d, 740, H, 0.95, P["tw"])
if style == "flag":
    d.polygon([(0, 0), (300, 0), (420, H), (0, H)], fill=(0, 0, 0)) if False else None
g = vgrad(P["ground"]).crop((0, 0, N, N - H)).resize((N, N - H))
img.paste(g, (0, H))
if style == "night":
    d.rectangle([0, H, N, H + 10], fill=(0, 122, 61)); d.rectangle([0, H + 10, N, H + 18], fill=(255, 255, 255)); d.rectangle([0, H + 18, N, H + 28], fill=(206, 17, 38))
if style == "flag":
    d.rectangle([0, H, N, H + 14], fill=(255, 255, 255)); d.rectangle([0, H + 14, N, H + 30], fill=(206, 17, 38))
# tyre marks
marks = Image.new("RGBA", (N, N), (0, 0, 0, 0)); md = ImageDraw.Draw(marks)
for off in (0, 70):
    pts = [(x, 940 + off - 0.00022 * (x - 200) ** 2 - 0.08 * x) for x in range(-50, 700, 10)]
    md.line(pts, fill=(0, 0, 0, 110), width=26)
img.alpha_composite(marks.filter(ImageFilter.GaussianBlur(3)))

car = Image.open(carf).convert("RGBA")
bb = car.getchannel("A").point(lambda a: 255 if a > 235 else 0).getbbox(); car = car.crop((bb[0], bb[1], bb[2], bb[3] + 30))
W = 1000; car = car.resize((W, int(car.height * W / car.width)), Image.LANCZOS)
cx, cy = 12, 905 - car.height

puff = Image.open("smoke_puff.png").convert("RGBA")
def smoke_layer(n, x0, y0, spread, alpha, grow):
    L = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    for i in range(n):
        t = i / n
        r = int(110 + grow * t + random.uniform(-25, 25))
        x = int(x0 - spread * t + random.uniform(-50, 50)); y = int(y0 - 200 * t + random.uniform(-40, 40))
        p = puff.resize((r * 2, r * 2)).rotate(random.uniform(0, 360))
        tint = Image.new("RGBA", p.size, P["smoke"] + (0,)); tint.putalpha(ImageEnhance.Brightness(p.getchannel("A")).enhance(alpha * (1 - 0.4 * t)))
        L.alpha_composite(tint, (x - r, y - r))
    return L
rear = (cx + int(W * 0.2), 900 - 40)
img.alpha_composite(smoke_layer(40, rear[0] + 60, rear[1] - 20, 420, 0.85, 260))
# contact shadow
sh = Image.new("RGBA", (N, N), (0, 0, 0, 0)); ImageDraw.Draw(sh).ellipse([cx + 40, 860, cx + W - 40, 925], fill=(0, 0, 0, 150))
img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(18)))
tint = {"sunset": (255, 214, 180), "night": (190, 210, 235), "flag": (235, 240, 235)}[style]
rgb = ImageChops.multiply(car.convert("RGB"), Image.new("RGB", car.size, tint)); car = Image.merge("RGBA", (*rgb.split(), car.getchannel("A")))
img.alpha_composite(car, (cx, cy))
img.alpha_composite(smoke_layer(14, rear[0] - 20, rear[1] + 20, 260, 0.5, 120))
# vignette
v = Image.new("L", (N, N), 0); ImageDraw.Draw(v).ellipse([-200, -200, N + 200, N + 200], fill=255)
v = v.filter(ImageFilter.GaussianBlur(160)); dark = Image.new("RGBA", (N, N), (0, 0, 0, 255)); dark.putalpha(ImageChops.invert(v).point(lambda a: int(a * 0.55)))
img.alpha_composite(dark)
img.convert("RGB").save(out)
