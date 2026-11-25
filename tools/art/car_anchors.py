#!/usr/bin/env python3
"""Measure each player car's body (body.obj) and write Cars/<id>/anchors.json: where the customs shop's parts sit.
Unity reads OBJ with x mirrored; the cars are symmetric so only |x| is used. z is forward, y is up.
  side:   door-centre sticker spot (x = body side, y, z) for each side
  glass:  windscreen banner centre (y, z) and the glass slope normal (ny, nz), width
  wing:   boot-lid mount point (y, z) and span
  arches: per wheel, the outer body side (|x|) at the top of the arch
"""
import json, glob, os, numpy as np

ROOT = "client/Assets/DriftKuwait/Resources/DriftKuwait/Cars"
PLAYER = ["shaheen", "faris", "ghazal", "wanait", "abusitta"]

def verts(path):
    v = [list(map(float, l.split()[1:4])) for l in open(path) if l.startswith("v ")]
    return np.array(v)

def ymax_profile(V, zs, xw=0.18):
    mid = V[np.abs(V[:, 0]) < xw]
    out = []
    for z in zs:
        s = mid[np.abs(mid[:, 2] - z) < 0.04]
        out.append(s[:, 1].max() if len(s) else np.nan)
    return np.array(out)

for cid in PLAYER:
    cj = json.load(open(f"{ROOT}/{cid}/car.json"))
    V = verts(f"{ROOT}/{cid}/body.obj")
    L, W, H, R = cj["length"], cj["width"], cj["height"], cj["wheel_radius"]
    w = cj["wheels"]; zf, zr = w["FL"][2], w["RL"][2]
    # side sticker: door centre between the wheels, a bit below the window line
    z0, y0 = (zf + zr) * 0.5 - 0.05, H * 0.44
    s = V[(np.abs(V[:, 1] - y0) < 0.07) & (np.abs(V[:, 2] - z0) < 0.12)]
    sx = float(np.abs(s[:, 0]).max()) if len(s) else W * 0.5
    # profile along the centre line (a wide strip, so the glass edges count even where the glass itself is open)
    zs = np.arange(-L / 2 + 0.05, L / 2 - 0.05, 0.02)
    prof = ymax_profile(V, zs, xw=0.72)   # the A-pillars follow the windscreen line
    ok = ~np.isnan(prof)
    zs, prof = zs[ok], prof[ok]
    roof = prof.max()
    roofz = zs[prof >= roof - 0.04]
    zE = float(roofz.max()); yE = float(prof[zs == roofz.max()][0])          # front edge of the roof
    ahead = [(z, y) for z, y in zip(zs, prof) if z > zE and y < roof - 0.22]
    zB, yB = ahead[0] if ahead else (zE + 0.35, roof - 0.3)                    # where the glass meets the bonnet
    t = 0.2                                                                   # banner along the top of the glass
    gz, gy = zE + (zB - zE) * t, yE + (yB - yE) * t
    dz, dy = zB - zE, yB - yE
    n = np.array([0.0, dz, -dy]); n /= np.linalg.norm(n)                      # (x, y, z): up and forward
    # boot lid for the wing: 30 cm in from the tail
    bz = -L / 2 + 0.30
    bi = int(np.argmin(np.abs(zs - bz))); by = float(prof[bi])
    # arches: outer body side above each wheel
    arches = []
    for k in ("FL", "FR", "RL", "RR"):
        c = w[k]
        a = V[(np.abs(V[:, 2] - c[2]) < 0.25) & (V[:, 1] > c[1] + R * 0.6) & (V[:, 1] < c[1] + R * 1.35)]
        arches.append(float(np.abs(a[:, 0]).max()) if len(a) else W * 0.5)
    out = {"side": [round(sx, 4), round(y0, 4), round(z0, 4)],
           "glass": [round(float(gy), 4), round(float(gz), 4), round(float(n[1]), 4), round(float(n[2]), 4), round(W * 0.5, 4)],
           "wing": [round(by, 4), round(bz, 4), round(W * 0.9, 4)],
           "arches": [round(a, 4) for a in arches], "roof": round(float(roof), 4)}
    json.dump(out, open(f"{ROOT}/{cid}/anchors.json", "w"))
    print(cid, out)
