"""Prepare a Tripo car model for the game: real size, wheels cut out, wheel data for the Unity rig.

Tripo returns one fused mesh about 1 unit long (X = length, front at +X, Y up, Z = width). The game needs wheels
that spin and steer, so this script:
  1. scales the car to its real length (metres), puts the tyres on the ground (y = 0) and centres it;
  2. finds the four wheels from the side profile (the tyres reach lower than the sills);
  3. deletes the fused tyre and rim faces inside each wheel cylinder (the game builds real wheels there);
  4. writes body.glb (texture kept) and wheels.json (centres, radius, width) next to the source.

  python3 tools/art/car_prep.py art/models/car_shaheen/model_pbr.glb --length 4.60
"""
import argparse
import json
import os

import numpy as np
import trimesh


def profile_dips(v, side_sign, half_w):
    """X ranges on one side where the lowest vertex dips well below the sill line: the two tyres."""
    side = v[(v[:, 2] * side_sign) > half_w * 0.80]
    xs = np.linspace(v[:, 0].min(), v[:, 0].max(), 121)
    mins = []
    for a, b in zip(xs[:-1], xs[1:]):
        m = (side[:, 0] >= a) & (side[:, 0] < b)
        mins.append(side[m, 1].min() if m.any() else np.inf)
    mins = np.array(mins)
    centres = (xs[:-1] + xs[1:]) / 2
    ground = np.min(mins)
    sill = np.median(mins[np.isfinite(mins)])
    thr = ground + (sill - ground) * 0.45          # well below the sill, near the ground
    low = mins < thr
    runs, start = [], None
    for i, f in enumerate(low):
        if f and start is None:
            start = i
        if (not f or i == len(low) - 1) and start is not None:
            end = i if f else i - 1
            runs.append((start, end))
            start = None
    runs = sorted(runs, key=lambda r: r[1] - r[0], reverse=True)[:2]
    if len(runs) < 2:
        raise SystemExit("could not find two tyres on side %+d" % side_sign)
    out = []
    for s, e in sorted(runs):
        seg = mins[s:e + 1]
        x0 = centres[s:e + 1][np.argmin(seg)]
        out.append(float(x0))
    return out, float(ground), float(sill)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("glb")
    ap.add_argument("--length", type=float, required=True, help="real car length in metres")
    ap.add_argument("--radius", type=float, default=None, help="override tyre radius (metres)")
    ap.add_argument("--unity", default=None, help="also write body.obj, 2K maps and car.json to this Unity Resources folder")
    a = ap.parse_args()

    scene = trimesh.load(a.glb)
    name, g = next(iter(scene.geometry.items()))
    v = g.vertices.copy()
    s = a.length / (v[:, 0].max() - v[:, 0].min())
    v *= s
    v[:, 0] -= (v[:, 0].max() + v[:, 0].min()) / 2
    v[:, 2] -= (v[:, 2].max() + v[:, 2].min()) / 2
    v[:, 1] -= v[:, 1].min()
    half_w = np.abs(v[:, 2]).max()

    xl, ground, sill = profile_dips(v, +1, half_w)
    xr, _, _ = profile_dips(v, -1, half_w)
    rear_x, front_x = (xl[0] + xr[0]) / 2, (xl[1] + xr[1]) / 2
    # tyre radius: the lower outline of a tyre is a circle touching the ground, so a point at distance d from the
    # contact patch and height y gives r = (d^2 + y^2) / 2y. Median over the outline points of both axles.
    est = []
    for cx in (rear_x, front_x):
        side = v[(np.abs(v[:, 2]) > half_w * 0.6) & (np.abs(v[:, 0] - cx) < 0.5)]
        for x in np.arange(cx - 0.45, cx + 0.45, 0.02):
            m = np.abs(side[:, 0] - x) < 0.01
            if not m.any():
                continue
            y, d = side[m, 1].min(), abs(x - cx)
            if 0.06 < y < 0.26 and d > 0.12:
                est.append((d * d + y * y) / (2 * y))
    radius = a.radius or float(np.clip(np.median(est), 0.26, 0.38))
    width = float(np.clip(half_w * 0.24, 0.17, 0.26))
    track_z = half_w - width * 0.55

    centres = {"FL": [front_x, radius, track_z], "FR": [front_x, radius, -track_z],
               "RL": [rear_x, radius, track_z], "RR": [rear_x, radius, -track_z]}

    # delete faces whose centroid sits inside a wheel cylinder (a little larger than the tyre), outer part only
    fc = v[g.faces].mean(axis=1)
    kill = np.zeros(len(g.faces), bool)
    for c in centres.values():
        dx, dy = fc[:, 0] - c[0], fc[:, 1] - c[1]
        inside = (dx * dx + dy * dy) < (radius * 1.0) ** 2
        outer = (fc[:, 2] * np.sign(c[2])) > (abs(c[2]) - width * 0.75)
        kill |= inside & outer
    g.vertices = v
    g.update_faces(~kill)
    g.remove_unreferenced_vertices()

    out_dir = os.path.dirname(a.glb)
    scene.export(os.path.join(out_dir, "body.glb"))     # for the web viewer / previews

    # Unity copy: OBJ + 2K maps. Unity's OBJ importer mirrors X (right- to left-handed), so writing (-z, y, x)
    # lands the car in Unity with +Z = front, +Y = up, +X = the car's right, not mirrored.
    if a.unity:
        os.makedirs(a.unity, exist_ok=True)
        uv = g.visual.uv
        vn = g.vertex_normals
        with open(os.path.join(a.unity, "body.obj"), "w") as f:
            f.write("# Arab Drift car body (tools/art/car_prep.py)\nmtllib none\no body\n")
            for p_ in g.vertices:
                f.write("v %.5f %.5f %.5f\n" % (-p_[2], p_[1], p_[0]))
            for t in uv:
                f.write("vt %.5f %.5f\n" % (t[0], t[1]))
            for n in vn:
                f.write("vn %.4f %.4f %.4f\n" % (-n[2], n[1], n[0]))
            for fa in g.faces + 1:
                f.write("f %d/%d/%d %d/%d/%d %d/%d/%d\n" % (fa[0], fa[0], fa[0], fa[1], fa[1], fa[1], fa[2], fa[2], fa[2]))
        m = g.visual.material
        size = (2048, 2048)
        m.baseColorTexture.convert("RGB").resize(size).save(os.path.join(a.unity, "albedo.png"))
        if m.normalTexture is not None:
            m.normalTexture.convert("RGB").resize(size).save(os.path.join(a.unity, "normal.png"))
        if m.metallicRoughnessTexture is not None:
            from PIL import Image
            mr = m.metallicRoughnessTexture.convert("RGB").resize(size)
            r_, g_, b_ = mr.split()                       # glTF: G = roughness, B = metallic
            smooth = g_.point(lambda x: int(255 * (0.40 + 0.55 * (1 - x / 255))))   # glossier paint: 0.40..0.95
            Image.merge("RGBA", (b_, b_, b_, smooth)).save(os.path.join(a.unity, "metallic.png"))  # Unity: R metallic, A smoothness

    # lights: find tail-light red and headlight white on the texture, near the rear / front faces (Unity space)
    lights = {"tail": [], "head": []}
    try:
        tex = np.asarray(g.visual.material.baseColorTexture.convert("RGB"))
        th, tw = tex.shape[:2]
        uvv = np.asarray(g.visual.uv)
        px = np.clip((uvv[:, 0] % 1) * (tw - 1), 0, tw - 1).astype(int)
        py = np.clip((1 - (uvv[:, 1] % 1)) * (th - 1), 0, th - 1).astype(int)
        col = tex[py, px].astype(float)
        U = np.stack([g.vertices[:, 2], g.vertices[:, 1], g.vertices[:, 0]], 1)   # Unity (x, y, z)
        L = a.length
        rr, gg, bb = col[:, 0], col[:, 1], col[:, 2]
        red = (U[:, 2] < -L / 2 + 0.30) & (rr > 100) & (rr > 2.5 * gg) & (rr > 2.5 * bb) & (U[:, 1] > 0.35)
        white = (U[:, 2] > L / 2 - 0.35) & (rr > 190) & (gg > 190) & (bb > 185) & (U[:, 1] > 0.35) & (U[:, 1] < 1.0)
        for key, m in (("tail", red), ("head", white)):
            for sgn in (-1, 1):
                q = U[m & (np.sign(U[:, 0]) == sgn) & (np.abs(U[:, 0]) > 0.15)]
                if len(q) >= 3:
                    lo, hi = np.percentile(q, 5, 0), np.percentile(q, 95, 0)
                    c = (lo + hi) / 2
                    lights[key].append([round(float(c[0]), 3), round(float(c[1]), 3), round(float(hi[2] if key == "head" else lo[2]), 3),
                                        round(float(hi[0] - lo[0]), 3), round(float(max(hi[1] - lo[1], 0.05)), 3)])
    except Exception as e:  # lights are optional
        print("light detection skipped:", e)

    # wheel data in Unity space: (x, y, z) = (tripo z, y, tripo x); x > 0 is the car's right
    uw = {k: [c[2], c[1], c[0]] for k, c in centres.items()}
    uw = {("F" if c[2] > 0 else "R") + ("L" if c[0] < 0 else "R"): c for c in uw.values()}
    info = {"length": a.length, "width": float(half_w * 2), "height": float(v[:, 1].max()),
            "wheel_radius": radius, "wheel_width": width, "wheelbase": front_x - rear_x,
            "wheels": uw, "lights": lights, "faces_removed": int(kill.sum()), "faces_left": int(len(g.faces))}
    js = json.dumps(info, indent=1)
    open(os.path.join(out_dir, "wheels.json"), "w").write(js)
    if a.unity:
        open(os.path.join(a.unity, "car.json"), "w").write(js)
    print(js)

if __name__ == "__main__":
    main()
